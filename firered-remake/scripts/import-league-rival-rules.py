#!/usr/bin/env python3
"""Extract the three late Route22 teams from pinned Git objects, without running reference code."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

PIN = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
PROFILE = "firered-league-rival-trainers-v1"


def extract(source):
    blobs = {}

    def read(path):
        if path not in blobs:
            blobs[path] = subprocess.check_output(["git", "show", PIN + ":" + path], cwd=source)
        return blobs[path].decode("utf-8")

    def ref(path, symbol, start, end=None):
        read(path)
        return dict(path=path, symbol=symbol, startLine=start, endLine=end or start,
                    sha256=hashlib.sha256(blobs[path]).hexdigest())

    def block(path, pattern):
        text = read(path)
        match = re.search(pattern, text, re.M)
        if match is None:
            raise ValueError((path, pattern))
        return match.group(0), text[:match.start()].count("\n") + 1

    def number(path, symbol):
        match = re.search(r"^#define\s+" + re.escape(symbol) + r"\s+([0-9a-fA-Fx]+)\b", read(path), re.M)
        if match is None:
            raise ValueError((path, symbol))
        return int(match[1], 0)

    def encode(name):
        return [int(re.search(r"^'" + re.escape(ch) + r"'\s*=\s*([0-9A-Fa-f]{2})\b", read("charmap.txt"), re.M)[1], 16)
                for ch in name]

    trainers, battles, resolved = [], [], {}
    names_path, parties_path, trainers_path = "src/data/text/species_names.h", "src/data/trainer_parties.h", "src/data/trainers.h"
    for starter, source_id in [("Squirtle", 435), ("Bulbasaur", 436), ("Charmander", 437)]:
        trainer_symbol = "TRAINER_RIVAL_ROUTE22_LATE_" + starter.upper()
        party_symbol = "sParty_RivalRoute22Late" + starter
        trainer_raw, trainer_line = block(trainers_path, r"^    \[" + trainer_symbol + r"\] = \{[\s\S]*?^    \},")
        party_raw, party_line = block(parties_path, r"^static const struct TrainerMonNoItemCustomMoves " + party_symbol + r"\[\] = \{[\s\S]*?^\};")
        if ".items = {}" not in trainer_raw or ".doubleBattle = FALSE" not in trainer_raw or "NO_ITEM_CUSTOM_MOVES(" + party_symbol + ")" not in trainer_raw:
            raise ValueError("Unexpected trainer declaration")
        ai_symbols = re.search(r"\.aiFlags = (.*),", trainer_raw)[1].split(" | ")
        ai_flags = sum(1 << int(re.search(r"^#define\s+" + symbol + r"\s+\(1 << (\d+)\)", read("include/constants/battle_ai.h"), re.M)[1]) for symbol in ai_symbols)
        trainer_id = number("include/constants/opponents.h", trainer_symbol)
        if trainer_id != source_id or ai_flags != 7:
            raise ValueError("Unexpected trainer identity or AI flags")
        name = re.search(r'\.trainerName = _\("([^"]+)"\)', trainer_raw)[1]
        party = []
        for match in re.finditer(r"    \{\n([\s\S]*?)    \},", party_raw):
            raw = match[1]
            symbol = re.search(r"\.species = (SPECIES_\w+)", raw)[1]
            name_match = re.search(r'^.*\[' + symbol + r'\]\s*=\s*_\("([^"]+)"\).*$', read(names_path), re.M)
            species_name = name_match[1]
            species_line = read(names_path)[:name_match.start()].count("\n") + 1
            moves = re.search(r"\.moves = \{([^}]+)\}", raw)[1].split(", ")
            if len(moves) != 4:
                raise ValueError("Expected four custom moves")
            for move in moves:
                move_raw, move_line = block("src/data/battle_moves.h", r"^    \[" + move + r"\] =[\s\S]*?^    \},")
                resolved[move] = dict(pp=int(re.search(r"\.pp = (\d+)", move_raw)[1]),
                    sourceMoveId=number("include/constants/moves.h", move),
                    source=ref("src/data/battle_moves.h", move, move_line, move_line + move_raw.count("\n")))
            party.append(dict(species={"value": "pret:" + symbol}, sourceSpeciesId=number("include/constants/species.h", symbol),
                speciesName=species_name, encodedSpeciesName=encode(species_name),
                level=int(re.search(r"\.lvl = (\d+)", raw)[1]), sourceIv=int(re.search(r"\.iv = (\d+)", raw)[1]),
                heldItem="ITEM_NONE", customMoves=[{"value": "pret:" + m} for m in moves],
                source=ref(parties_path, party_symbol, party_line + party_raw[:match.start()].count("\n"), party_line + party_raw[:match.end()].count("\n")),
                speciesNameSource=ref(names_path, symbol, species_line)))
        if len(party) != 6:
            raise ValueError("Expected six party members")
        money_raw, money_line = block("src/battle_main.c", r"^.*\{TRAINER_CLASS_RIVAL_LATE,\s*\d+\}.*$")
        base_money = int(re.search(r"TRAINER_CLASS_RIVAL_LATE,\s*(\d+)", money_raw)[1])
        trainer = dict(id={"value": "pret:" + trainer_symbol}, sourceTrainerId=trainer_id,
            trainerClass="TRAINER_CLASS_RIVAL_LATE", sourceTrainerClass=number("include/constants/trainers.h", "TRAINER_CLASS_RIVAL_LATE"),
            trainerPic="TRAINER_PIC_RIVAL_LATE", sourceTrainerPic=number("include/constants/trainers.h", "TRAINER_PIC_RIVAL_LATE"),
            sourceTrainerName=name, encodedTrainerName=encode(name), encounterMusicGender=0,
            doubleBattle=False, partyFlags=1, aiFlags=ai_flags, aiFlagSymbols=ai_symbols, baseMoney=base_money,
            items=["ITEM_NONE"] * 4, party=party,
            source=ref(trainers_path, trainer_symbol, trainer_line, trainer_line + trainer_raw.count("\n")),
            moneySource=ref("src/battle_main.c", "TRAINER_CLASS_RIVAL_LATE", money_line))
        label = "Route22_EventScript_LateRival" + starter
        script_raw, script_line = block("data/maps/Route22/scripts.inc", r"^" + label + r"::[^\n]*\n(?:(?!^[A-Za-z_][A-Za-z0-9_]*::).*(?:\n|$))*")
        script_raw = script_raw.rstrip()
        if "trainerbattle_no_intro " + trainer_symbol + ", Route22_Text_LateRivalDefeat" not in script_raw:
            raise ValueError("Unexpected battle call")
        trainers.append(trainer)
        battles.append(dict(trainer=trainer["id"], mode=3, rivalFlags=0, battleTypeFlags=8, scriptEntry=label,
            introText="Route22_Text_LateRivalIntro", defeatText="Route22_Text_LateRivalDefeat", victoryText="",
            postBattleText="Route22_Text_LateRivalPostBattle", postBattleScript=None, musicOption=None,
            entryScript="EventScript_DoNoIntroTrainerBattle", callback="CB2_EndTrainerBattle",
            source=ref("data/maps/Route22/scripts.inc", label, script_line, script_line + script_raw.count("\n"))))
    document = dict(sourceCommit=PIN, profile=PROFILE, completeSourceRoster=False,
                    trainers=trainers, battles=battles, resolvedMoves=resolved)
    inputs = dict(sourceCommit=PIN, profile=PROFILE, inputs=[dict(path=path, byteLength=len(data), sha256=hashlib.sha256(data).hexdigest())
                  for path, data in sorted(blobs.items())])
    return document, inputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--output-root", type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    output = args.output_root / "content/generated/league-rival-rules"
    documents = extract(args.source)
    output.mkdir(parents=True, exist_ok=True)
    for name, document in zip(("trainers.json", "source-input-hashes.json"), documents):
        data = (json.dumps(document, indent=2, sort_keys=True, ensure_ascii=False) + "\n").encode("utf-8")
        (output / name).write_bytes(data)
        print(name, hashlib.sha256(data).hexdigest())


if __name__ == "__main__":
    main()

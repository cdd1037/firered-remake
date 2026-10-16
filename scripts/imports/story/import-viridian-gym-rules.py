#!/usr/bin/env python3
"""Read pinned pret Git blobs; emit only nine teams, TM26, and source rule evidence.

Usage: python scripts/imports/story/import-viridian-gym-rules.py PRET_REPO PREDECESSOR_ROOT OUTPUT_ROOT [--check]
No upstream code is executed. --check compares exact bytes without writing.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
import argparse
import json
import re
import subprocess
from pathlib import Path

import machine_catalog as shared_machines

COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TRAINERS = ("TAMER_JASON", "TAMER_COLE", "BLACK_BELT_ATSUSHI", "BLACK_BELT_KIYO",
            "BLACK_BELT_TAKASHI", "COOLTRAINER_SAMUEL", "COOLTRAINER_YUJI",
            "COOLTRAINER_WARREN", "LEADER_GIOVANNI")


def need(value, message):
    if not value:
        raise ValueError(message)

def generate(reference, dependency):
    texts, inputs = {}, {}
    def read(path):
        if path not in texts:
            data = subprocess.check_output(["git", "show", COMMIT + ":" + path], cwd=reference)
            inputs[path] = dict(path=path)
            texts[path] = data.decode()
        return texts[path]
    def one(path, pattern):
        matches = list(re.finditer(pattern, read(path), re.M | re.S))
        need(len(matches) == 1, "Expected one source declaration: " + pattern)
        return matches[0]
    def ref(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=read(path).count("\n", 0, match.start()) + 1,
                    endLine=read(path).count("\n", 0, match.end()) + 1)
    def number(path, symbol):
        return int(one(path, r"^#define " + symbol + r"\s+(\d+)\b[^\n]*")[1])
    def encode(name):
        return [int(one("charmap.txt", "^'" + re.escape(c) + r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1], 16) for c in name]
    def identity(symbol):
        return dict(value="pret:" + symbol)
    def ai_flags(symbols):
        return sum(1 << int(one("include/constants/battle_ai.h", r"^#define " + s + r"\s+\(1 << (\d+)\)[^\n]*")[1]) for s in symbols)
    def natural_moves(species, level):
        pointer = one("src/data/pokemon/level_up_learnset_pointers.h", r"^    \[" + species + r"\] = (\w+),$")[1]
        body = one("src/data/pokemon/level_up_learnsets.h", r"^static const u16 " + pointer + r"\[\] = \{\n(.*?)^\};")[1]
        moves = []
        for lv, move in re.findall(r"LEVEL_UP_MOVE\(\s*(\d+), (MOVE_\w+)\)", body):
            if int(lv) <= level and move not in moves:
                moves = (moves + [move])[-4:]
        need(len(moves) == 4, "Expected four natural moves")
        return moves

    trainers, resolved = [], []
    for short in TRAINERS:
        symbol, path = "TRAINER_" + short, "src/data/trainers.h"
        match = one(path, r"^    \[" + symbol + r"\] = \{\n(.*?)^    \},")
        fields = dict(re.findall(r"^        \.(\w+) = (.*),$", match[1], re.M))
        shape, party_symbol = re.fullmatch(r"((?:NO_ITEM|ITEM)_(?:DEFAULT|CUSTOM)_MOVES)\((\w+)\)", fields["party"]).groups()
        custom, held = "CUSTOM" in shape, shape.startswith("ITEM_")
        flags = int(custom) | (int(held) << 1)
        party_path = "src/data/trainer_parties.h"
        party = one(party_path, r"^static const struct TrainerMon" + ("Item" if held else "NoItem") + ("Custom" if custom else "Default") + "Moves " + party_symbol + r"\[\] = \{\n(.*?)^\};")
        rows = list(re.finditer(r"    \{\n(.*?)    \},\n", party[1], re.S))
        need("".join(r[0] for r in rows) == party[1], "Unhandled trainer party syntax")
        members, movesets = [], []
        for row in rows:
            mf = dict(re.findall(r"^        \.(\w+) = (.*),$", row[1], re.M))
            need(set(mf) == {"iv", "lvl", "species"} | ({"heldItem"} if held else set()) | ({"moves"} if custom else set()), "Unexpected member fields")
            species, level = mf["species"], int(mf["lvl"])
            names_path = "src/data/text/species_names.h"
            name = one(names_path, r'^    \[' + species + r'\] = _\("([^"]+)"\),$')
            moves = mf["moves"].strip("{}").split(", ") if custom else natural_moves(species, level)
            members.append(dict(species=identity(species), sourceSpeciesId=number("include/constants/species.h", species),
                speciesName=name[1], encodedSpeciesName=encode(name[1]), level=level, sourceIv=int(mf["iv"]),
                heldItem=mf.get("heldItem", "ITEM_NONE"), customMoves=[identity(m) for m in moves] if custom else [],
                source=ref(party_path, party_symbol, party), speciesNameSource=ref(names_path, species, name)))
            movesets.append(dict(species=identity(species), level=level, moves=moves))
        name = re.fullmatch(r'_\("([A-Z ]+)"\)', fields["trainerName"])[1]
        items = fields["items"].strip("{}").split(", ") if fields["items"] != "{}" else []
        items += ["ITEM_NONE"] * (4 - len(items))
        money = one("src/battle_main.c", r"^\s*\{" + fields["trainerClass"] + r", (\d+)\},$")
        ai = fields["aiFlags"].split(" | ")
        trainers.append(dict(id=identity(symbol), sourceTrainerId=number("include/constants/opponents.h", symbol),
            trainerClass=fields["trainerClass"], sourceTrainerClass=number("include/constants/trainers.h", fields["trainerClass"]),
            trainerPic=fields["trainerPic"], sourceTrainerPic=number("include/constants/trainers.h", fields["trainerPic"]),
            sourceTrainerName=name, encodedTrainerName=encode(name),
            encounterMusicGender=number("include/constants/trainers.h", fields["encounterMusic_gender"]),
            items=items, doubleBattle=fields["doubleBattle"] == "TRUE", partyFlags=flags,
            aiFlags=ai_flags(ai), aiFlagSymbols=ai, baseMoney=int(money[1]), party=members,
            source=ref(path, symbol, match), moneySource=ref("src/battle_main.c", fields["trainerClass"], money)))
        resolved.append(dict(trainer=symbol, party=movesets))
    battles = []
    path = "data/maps/ViridianCity_Gym/scripts.inc"
    for label in re.finditer(r"^(\w+)::\n((?:(?!^\w+::).)*)", read(path), re.M | re.S):
        battle = re.search(r"^\ttrainerbattle_single (.*)$", label[2], re.M)
        if not battle:
            continue
        args = battle[1].split(", ")
        need(args[0] in {"TRAINER_" + t for t in TRAINERS}, "Unexpected gym trainer")
        leader = args[0] == "TRAINER_LEADER_GIOVANNI"
        need(len(args) == (5 if leader else 3), "Trainer callback differs")
        command = one(path, r"^\ttrainerbattle_single " + re.escape(battle[1]) + "$")
        post = re.search(r"^\tmsgbox (\w+)", label[2], re.M)
        mode = "TRAINER_BATTLE_CONTINUE_SCRIPT_NO_MUSIC" if leader else "TRAINER_BATTLE_SINGLE"
        battles.append(dict(trainer=identity(args[0]), mode=number("include/constants/battle_setup.h", mode),
            rivalFlags=0, battleTypeFlags=8, scriptEntry=label[1], introText=args[1], defeatText=args[2], victoryText="",
            postBattleText=post[1] if post else "", entryScript="EventScript_TryDoNormalTrainerBattle",
            callback="CB2_EndTrainerBattle", source=ref(path, label[1], command),
            postBattleScript=args[3] if leader else None, musicOption=args[4] if leader else None))
    need(len(trainers) == len(battles) == 9 and sum(len(t["party"]) for t in trainers) == 29, "Roster scope differs")
    need(sum(t["partyFlags"] == 2 for t in trainers) == 3 and sum(m["heldItem"] == "ITEM_BLACK_BELT" for t in trainers for m in t["party"]) == 6, "Held/default shape differs")
    trainer_doc = dict(  completeSourceRoster=False,
                       trainers=trainers, battles=battles, resolvedMoves=resolved)

    machine = shared_machines.admission_view(dependency, 'content/generated/data/regions/viridian-gym/machines.json', '/machine')
    machine_doc = dict(  machine=machine)

    path = "src/overworld.c"
    multipliers = one(path, r"^static const u8 sWhiteOutMoneyLossMultipliers\[\] = \{(.*?)^\};")
    factors = [int(v) for v in re.findall(r"\d+", multipliers[1])]
    need(factors == [2, 4, 6, 9, 12, 16, 20, 25, 30], "Whiteout multipliers differ")
    badge_flags = one(path, r"^static const u16 sWhiteOutMoneyLossBadgeFlagIDs\[\] = \{(.*?)^\};")
    need(re.findall(r"FLAG_\w+", badge_flags[1]) == [f"FLAG_BADGE0{i}_GET" for i in range(1, 9)], "Badge count differs")
    compute = one(path, r"^u32 ComputeWhiteOutMoneyLoss\(void\)\n\{(.*?)^\}")
    need("toplevel * 4 * sWhiteOutMoneyLossMultipliers[nbadges]" in compute[1] and "losings = money" in compute[1], "Whiteout formula differs")
    read("src/battle_script_commands.c")
    read("src/battle_ai_script_commands.c")
    read("src/pokemon.c")
    read("src/data/pokemon/species_info.h")
    rules = dict(  whiteoutMultipliers=factors,
                 whiteoutSources=[ref(path, s, m) for s, m in [("sWhiteOutMoneyLossMultipliers", multipliers), ("sWhiteOutMoneyLossBadgeFlagIDs", badge_flags), ("ComputeWhiteOutMoneyLoss", compute)]],
                 basePayouts={t["id"]["value"]: 4 * t["baseMoney"] * t["party"][-1]["level"] for t in trainers},
                 species=sorted({m["species"]["value"] for t in trainers for m in t["party"]}),
                 moves=sorted({m for t in resolved for p in t["party"] for m in p["moves"]}),
                 heldItem="ITEM_BLACK_BELT", tm26Type="TYPE_GROUND")
    need(len(rules["species"]) == 14 and len(rules["moves"]) == 43, "Move/species closure differs")
    outputs = {"trainers.json": trainer_doc, "machines.json": machine_doc, "rules.json": rules}

    return outputs

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reference", type=Path)
    parser.add_argument("dependency", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    outputs = generate(args.reference, args.dependency)
    output = args.output / "content/generated/data/regions/viridian-gym"
    files = {name: (trainer_json(args.output / 'content/generated', value, check=args.check) if name == 'trainers.json' else (json.dumps(value, indent=2, ensure_ascii=False) + "\n").encode()) for name, value in outputs.items()}
    lock = dict( outputs=[dict(path="content/generated/data/regions/viridian-gym/" + name) for name, data in files.items()])
    paths = {output / name: data for name, data in files.items()}
    paths[args.output / "scripts/recipes/story/viridian-gym-rules.lock.json"] = (json.dumps(lock, indent=2) + "\n").encode()
    for path, data in paths.items():
        if args.check:
            need(path.read_bytes() == data, "Regeneration differs: " + str(path))
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    print(json.dumps(dict(checked=args.check, trainers=9, members=29, heldDefaultMembers=6, ownedSpecies=115,
                         rosterSpecies=14, moves=43, tm26EligibleSpecies=sum(shared_machines.load_catalog(args.dependency).compatibility('pret:ITEM_TM26', s)['canLearn'] for s in outputs['machines.json']['machine']['species'])), indent=2))

if __name__ == "__main__":
    main()

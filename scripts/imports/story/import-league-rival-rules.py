#!/usr/bin/env python3
"""Extract the three late Route22 teams from pinned Git objects, without running reference code."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
from trainer_import import read_source, selected_trainers
import argparse
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
            blobs[path] = read_source(source, path, PIN)
        return blobs[path].decode("utf-8")

    def ref(path, symbol, start, end=None):
        read(path)
        return dict(path=path, symbol=symbol, startLine=start, endLine=end or start)

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
        trainer = selected_trainers(read, [trainer_symbol])[0]
        if (trainer['sourceTrainerId'] != source_id or trainer['aiFlags'] != 7 or
                trainer['doubleBattle'] or trainer['partyFlags'] != 1 or
                trainer['items'] != ['ITEM_NONE'] * 4 or len(trainer['party']) != 6):
            raise ValueError('Unexpected late-rival trainer declaration')
        for member in trainer['party']:
            for slot in member['customMoves']:
                move = slot['value'][5:]
                move_raw, move_line = block('src/data/battle_moves.h', r'^    \[' + move + r'\] =[\s\S]*?^    \},')
                resolved[move] = dict(pp=int(re.search(r'\.pp = (\d+)', move_raw)[1]),
                    sourceMoveId=number('include/constants/moves.h', move),
                    source=ref('src/data/battle_moves.h', move, move_line, move_line + move_raw.count('\n')))
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
    document = dict(  completeSourceRoster=False,
                    trainers=trainers, battles=battles, resolvedMoves=resolved)
    return document


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--output-root", type=Path, default=_PROJECT_ROOT)
    args = parser.parse_args()
    output = args.output_root / "content/generated/data/trainer-battles/league-rival-rules.json"
    document = extract(args.source)
    output.parent.mkdir(parents=True, exist_ok=True)
    for name, document in [("trainers.json", document)]:
        data = trainer_json(args.output_root / 'content/generated', document)
        output.write_bytes(data)
        print(name)


if __name__ == "__main__":
    main()

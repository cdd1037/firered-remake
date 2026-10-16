#!/usr/bin/env python3
"""Compile the seven Cerulean trainer parties and battle entries into domain data."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
from trainer_import import selected_trainers
import argparse
import json
import pathlib
import re

ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-cerulean-trainers-v1'


def compile_catalog(root):
    generated = root / 'content/generated'
    raw = root / 'content/raw/pokefirered/037335f4'
    def read(path):
        return json.loads((generated / path).read_text())
    rows = read('maps/regions/cerulean/dependencies.json')['trainers']
    setup = (raw / 'src/battle_setup.c').read_text()
    for statement in ('gMain.savedCallback = CB2_EndTrainerBattle;', 'return EventScript_DoNoIntroTrainerBattle;', 'return EventScript_TryDoNormalTrainerBattle;'):
        if statement not in setup: raise ValueError('Cerulean callback source differs')
    trainers = selected_trainers(lambda path: (raw / path).read_text(), [row['symbol'] for row in rows])
    battles = []
    if len(rows) != 7:
        raise ValueError('Cerulean party closure')
    for trainer in trainers:
        symbol = trainer['id']['value'][5:]
        name = trainer['sourceTrainerName']
        trainer_id = trainer['id']
        if trainer['doubleBattle'] or trainer['partyFlags'] & 2:
            raise ValueError('Unsupported Cerulean battle party')
        if any(item not in ('ITEM_NONE', 'ITEM_SUPER_POTION') for item in trainer['items']):
            raise ValueError('Cerulean trainer items')
        for member in trainer['party']:
            slots = [slot['value'] for slot in member['customMoves']]
            nonempty = [slot for slot in slots if slot != 'pret:MOVE_NONE']
            if slots and slots != nonempty + ['pret:MOVE_NONE'] * (4 - len(nonempty)):
                raise ValueError('Nontrailing empty custom move slot')
        rival = symbol.startswith('TRAINER_RIVAL_'); grunt = symbol == 'TRAINER_TEAM_ROCKET_GRUNT_5'; misty = symbol == 'TRAINER_LEADER_MISTY'
        map_name = 'CeruleanCity' if rival or grunt else 'CeruleanCity_Gym'
        person = 'Rival' if rival else 'Grunt' if grunt else name.title()
        suffix = symbol.split('_')[-1].title() if rival else ''
        entry = map_name + '_EventScript_' + person + suffix
        intro = map_name + '_Text_' + person + 'Intro'; defeat = map_name + '_Text_' + person + 'Defeat'
        script_path = 'data/maps/' + map_name + '/scripts.inc'; script = (raw / script_path).read_text()
        expected = 'trainerbattle_no_intro ' + symbol + ', ' + defeat if rival or grunt else 'trainerbattle_single ' + symbol + ', ' + intro + ', ' + defeat + (', CeruleanCity_Gym_EventScript_MistyDefeated, NO_MUSIC' if misty else '')
        match = re.search('^' + entry + r'::\n(?:(?!^\w+::).)*?\t' + re.escape(expected) + '$', script, re.M | re.S)
        if not match: raise ValueError('Cerulean battle operands: ' + symbol)
        line = script[:match.start()].count('\n') + 1
        battles.append(dict(trainer=trainer_id, mode=3 if rival or grunt else 1 if misty else 0,
            rivalFlags=0, battleTypeFlags=8, scriptEntry=entry, introText=intro, defeatText=defeat, victoryText='',
            postBattleText='CeruleanCity_Gym_Text_ExplainCascadeBadge' if misty else 'CeruleanCity_Text_RecoveredTM28FromGrunt' if grunt else map_name + '_Text_' + person + 'PostBattle',
            entryScript='EventScript_DoNoIntroTrainerBattle' if rival or grunt else 'EventScript_TryDoNormalTrainerBattle', callback='CB2_EndTrainerBattle',
            source=dict(path=script_path, symbol=entry, startLine=line, endLine=line + match[0].count('\n')),
            postBattleScript='CeruleanCity_Gym_EventScript_MistyDefeated' if misty else None, musicOption='NO_MUSIC' if misty else None))
    if sum(len(t['party']) for t in trainers) != 19: raise ValueError('Cerulean party member closure')
    return dict(schemaVersion=1,   completeSourceRoster=False, trainers=trainers, battles=battles)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    data = trainer_json(ROOT / 'content/generated', compile_catalog(ROOT), check=args.check)
    path = ROOT / 'content/generated/data/trainer-battles/cerulean/trainers.json'
    if args.check:
        if path.read_bytes() != data: raise ValueError('Cerulean trainer output differs')
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    print(json.dumps(dict(trainers=7, checked=args.check)))

if __name__ == '__main__': main()

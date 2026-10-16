#!/usr/bin/env python3
"""Strict local extraction of three Oak's lab trainers; never execute upstream source."""
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
PROFILE = 'firered-oaks-lab-trainers-v1'
SYMBOLS = ['TRAINER_RIVAL_OAKS_LAB_SQUIRTLE', 'TRAINER_RIVAL_OAKS_LAB_BULBASAUR',
           'TRAINER_RIVAL_OAKS_LAB_CHARMANDER']


def import_catalog(raw, out, generated_root=None, check=False):
    out = pathlib.Path(out).absolute()
    if generated_root is None:
        if out.parent.parts[-2:] != ("data", "trainer-battles"):
            raise ValueError("Use --generated-root for an output outside data/trainer-battles")
        generated_root = out.parents[2]
    generated_root = pathlib.Path(generated_root).absolute()
    lock = json.loads((ROOT / 'scripts/recipes/trainers/lab-trainers.lock.json').read_text(encoding='utf-8'))
    if (lock['schemaVersion'] != 1 or lock['commit'] != COMMIT or
            lock['repository'] != 'pret/pokefirered' or lock['profile'] != PROFILE or
            lock['completeSourceRoster'] is not False or lock['trainerSymbols'] != SYMBOLS):
        raise ValueError('Unknown source identity, profile or trainer scope')
    sources, inputs = {}, {}
    for entry in lock['files']:
        path = pathlib.PurePosixPath(entry['path'])
        if path.is_absolute() or '..' in path.parts or str(path) in inputs:
            raise ValueError('Unsafe or duplicate source path')
        data = (raw / path).read_bytes()
        sources[str(path)] = data.decode('utf-8')
        inputs[str(path)] = entry
    if len(inputs) != 29:
        raise ValueError('Expected the complete bounded 29-input research manifest')

    def one(pattern, text):
        matches = list(re.finditer(pattern, text, re.M | re.S))
        if len(matches) != 1:
            raise ValueError(f'Expected one match for {pattern}; got {len(matches)}')
        return matches[0]

    def source(path, symbol, start, end):
        return dict(path=path, symbol=symbol, startLine=sources[path].count('\n', 0, start) + 1,
                    endLine=sources[path].count('\n', 0, end) + 1)

    def constant(path, symbol):
        expression = one(r'^#define ' + re.escape(symbol) + r'\s+([^\n]+)', sources[path])[1].split('//')[0].strip()
        if re.fullmatch(r'\d+', expression):
            return int(expression)
        shift = re.fullmatch(r'\(1 << (\d+)\)', expression)
        if shift:
            return 1 << int(shift[1])
        raise ValueError('Unsupported constant: ' + expression)

    def function_ref(path, symbol):
        match = one(r'^(?:static )?(?:inline )?\w+[ *]+' + re.escape(symbol) + r'\([^;\n]*\)\n\{\n.*?^\}', sources[path])
        return source(path, symbol, match.start(), match.end())

    trainers = selected_trainers(sources.__getitem__, SYMBOLS)
    battles = []
    script_path = 'data/maps/PalletTown_ProfessorOaksLab/scripts.inc'
    for trainer in trainers:
        symbol, trainer_id = trainer['id']['value'][5:], trainer['id']
        if (trainer['doubleBattle'] or trainer['partyFlags'] != 0 or
                trainer['items'] != ['ITEM_NONE'] * 4 or len(trainer['party']) != 1):
            raise ValueError('Lab requires one default-move member and empty-item single battle')
        if len(set(trainer['aiFlagSymbols'])) != len(trainer['aiFlagSymbols']):
            raise ValueError('Duplicate AI flags')
        battle_match = one(r'^\ttrainerbattle_earlyrival ' + symbol + r', (\w+), (\w+), (\w+)\n\tgoto (\w+)\n\tend$', sources[script_path])
        flags = constant('include/constants/battle.h', battle_match[1])
        battle_flags = constant('include/constants/battle.h', 'BATTLE_TYPE_TRAINER')
        if flags & constant('include/constants/battle.h', 'RIVAL_BATTLE_TUTORIAL'):
            battle_flags |= constant('include/constants/battle.h', 'BATTLE_TYPE_FIRST_BATTLE')
        battles.append(dict(trainer=trainer_id, mode=constant('include/constants/battle_setup.h', 'TRAINER_BATTLE_EARLY_RIVAL'),
                            rivalFlags=flags, battleTypeFlags=battle_flags, defeatText=battle_match[2], victoryText=battle_match[3],
                            entryScript='EventScript_DoNoIntroTrainerBattle', callback='CB2_EndTrainerBattle',
                            postBattleScript=battle_match[4], source=source(script_path, symbol, battle_match.start(), battle_match.end())))
    rules_sources = [function_ref(path, symbol) for path, symbol in [
        ('src/battle_main.c', 'CreateNPCTrainerParty'), ('src/pokemon.c', 'CreateBoxMon'),
        ('src/pokemon.c', 'GiveBoxMonInitialMoveset'), ('src/battle_setup.c', 'StartTrainerBattle'),
        ('src/battle_setup.c', 'CB2_EndTrainerBattle'), ('src/battle_setup.c', 'IsPlayerDefeated'),
        ('src/battle_ai_script_commands.c', 'BattleAI_SetupAIData')]]
    document = dict(schemaVersion=1,   completeSourceRoster=False,
                      trainers=trainers, battles=battles)
    data = trainer_json(generated_root, document, check=check)
    output = dict(path='lab-trainers.json')
    manifest = dict(schemaVersion=1,   trainerCount=len(trainers),
                    partyMemberCount=sum(len(t['party']) for t in trainers), completeSourceRoster=False,
                      outputs=[output])
    # All input/grammar checks finish before touching any output.
    if not check:
        out.mkdir(parents=True, exist_ok=True)
    for path, content in [('lab-trainers.json', data), ('manifest.json', (json.dumps(manifest, indent=2) + '\n').encode('utf-8'))]:
        if check:
            if (out / path).read_bytes() != content:
                raise ValueError('Lab trainer output differs: ' + path)
            continue
        temporary = out / (path + '.tmp')
        temporary.write_bytes(content)
        temporary.replace(out / path)
    print(json.dumps(manifest))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--raw-root', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out-root', type=pathlib.Path, default=ROOT / 'content/generated/data/trainer-battles/lab')
    parser.add_argument("--generated-root", type=pathlib.Path)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    import_catalog(args.raw_root, args.out_root, args.generated_root, args.check)

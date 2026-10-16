#!/usr/bin/env python3
"""Compile Route 12–15 trainer occurrences and shared source definitions."""
import argparse
import json
from pathlib import Path
import re
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'lib'))
from pokemon_import_io import safe, read_source
from presentation_source import SOURCE_COMMIT
from project_paths import PROJECT_ROOT
from shared_trainers import trainer_json
from trainer_import import selected_trainers

RESOURCE = 'data/pokemon/fuchsia-route/trainers.json'


def generate(raw, reference=None):
    texts = {}
    def read(path):
        if path not in texts:
            texts[path] = read_source(raw, path, commit=SOURCE_COMMIT, repository=reference).decode()
        return texts[path]
    def one(path, pattern):
        matches = list(re.finditer(pattern, read(path), re.M | re.S))
        if len(matches) != 1:
            raise ValueError('Expected one trainer binding: ' + path + ' ' + pattern)
        return matches[0]

    battles, actors, symbols = [], [], []
    script_path = 'data/scripts/trainers.inc'
    for map_name in ('Route12', 'Route13', 'Route14', 'Route15'):
        for local_id, obj in enumerate(json.loads(read(f'data/maps/{map_name}/map.json'))['object_events'], 1):
            if obj.get('trainer_type') != 'TRAINER_TYPE_NORMAL':
                continue
            label = one(script_path, r'^' + re.escape(obj['script']) + r'::\n(.*?)(?=^\w+::|\Z)')
            call = one(script_path, r'^' + re.escape(obj['script']) + r'::\n\ttrainerbattle_(single|double) (TRAINER_\w+), ([^\n]+)')
            double, symbol, arguments = call[1] == 'double', call[2], call[3].split(', ')
            if len(arguments) != (3 if double else 2):
                raise ValueError('Unsupported trainer battle operands: ' + symbol)
            post = re.search(r'^\tmsgbox (\w+)', label[1], re.M)
            if post is None:
                raise ValueError('Missing post-battle dialogue: ' + symbol)
            battles.append(dict(trainer=dict(value='pret:' + symbol), mode=4 if double else 0,
                rivalFlags=0, battleTypeFlags=9 if double else 8, scriptEntry=obj['script'],
                introText=arguments[0], defeatText=arguments[1], victoryText='', postBattleText=post[1],
                entryScript='EventScript_TryDoDoubleTrainerBattle' if double else 'EventScript_TryDoNormalTrainerBattle',
                callback='CB2_EndTrainerBattle', source=dict(path=script_path, symbol=obj['script'],
                    startLine=read(script_path).count('\n', 0, label.start()) + 1,
                    endLine=read(script_path).count('\n', 0, label.end()) + 1),
                postBattleScript=None, musicOption=None))
            actors.append(dict(map=map_name, localId=local_id, script=obj['script'], trainer=dict(value='pret:' + symbol),
                x=obj['x'], y=obj['y'], sight=int(obj['trainer_sight_or_berry_tree_id']),
                notEnoughPokemonText=arguments[2] if double else None))
            if symbol not in symbols:
                symbols.append(symbol)
    trainers = selected_trainers(read, symbols)
    by_id = {trainer['id']['value']: trainer for trainer in trainers}
    for battle in battles:
        if by_id[battle['trainer']['value']]['doubleBattle'] != (battle['mode'] == 4):
            raise ValueError('Trainer party and battle mode differ')
    return dict(schemaVersion=1, trainers=trainers, battles=battles, actors=actors)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=Path, default=PROJECT_ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--reference-root', type=Path, help='Read fixed Git blobs instead of raw files')
    parser.add_argument('--out', type=Path, default=PROJECT_ROOT / 'content/generated')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    document = generate(args.raw, args.reference_root)
    output = safe(args.out, RESOURCE)
    data = trainer_json(args.out, document, check=args.check)
    if args.check:
        if output.read_bytes() != data:
            raise ValueError('Fuchsia route trainer bindings differ')
    else:
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_bytes(data)
    print(json.dumps(dict(checked=args.check, trainers=len(document['trainers']), battles=len(document['battles']))))


if __name__ == '__main__':
    main()

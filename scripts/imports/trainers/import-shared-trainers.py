#!/usr/bin/env python3
"""Rebuild shared trainer business data from local, inert source declarations."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import ast
import json
from pathlib import Path
import re

from shared_trainers import publish_trainers, read_trainers

ROOT = _PROJECT_ROOT


def generate(raw, identities):
    raw = Path(raw)
    cache = {}
    def text(path):
        if path not in cache:
            cache[path] = (raw / path).read_text()
        return cache[path]
    def one(path, pattern):
        matches = list(re.finditer(pattern, text(path), re.M | re.S))
        if len(matches) != 1:
            raise ValueError('Expected one declaration: ' + path + ' ' + pattern)
        return matches[0]
    definitions = {}
    for path in ['include/constants/opponents.h', 'include/constants/trainers.h',
                 'include/constants/species.h', 'include/constants/battle_ai.h']:
        definitions.update(re.findall(r'^#define\s+(\w+)\s+([^\n]+)', text(path), re.M))
    def number(expression):
        expression = expression.split('//')[0].strip()
        def visit(node):
            if isinstance(node, ast.Constant) and type(node.value) is int:
                return node.value
            if isinstance(node, ast.Name):
                return number(definitions[node.id])
            if isinstance(node, ast.BinOp):
                left, right = visit(node.left), visit(node.right)
                if isinstance(node.op, ast.BitOr): return left | right
                if isinstance(node.op, ast.LShift): return left << right
            raise ValueError('Unsupported trainer numeric expression: ' + expression)
        return visit(ast.parse(expression, mode='eval').body)
    def encode(name):
        return [int(one('charmap.txt', "^'" + re.escape("\\'" if c == "'" else c) + r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1], 16) for c in name]
    def fields(body):
        rows = [re.fullmatch(r'\s*\.(\w+) = (.*),', line) for line in body.splitlines()]
        if any(row is None for row in rows):
            raise ValueError('Unrecognized trainer declaration syntax')
        result = {row[1]: row[2] for row in rows}
        if len(result) != len(rows):
            raise ValueError('Duplicate trainer declaration field')
        return result
    def identity(symbol): return {'value': 'pret:' + symbol}
    trainers = []
    for stable_id in identities:
        symbol = stable_id.removeprefix('pret:')
        declaration = one('src/data/trainers.h', r'^    \[' + re.escape(symbol) + r'\] = \{\n(.*?)^    \},')
        f = fields(declaration[1])
        if set(f) != {'trainerClass', 'encounterMusic_gender', 'trainerPic', 'trainerName', 'items', 'doubleBattle', 'aiFlags', 'party'}:
            raise ValueError('Unrecognized trainer fields: ' + stable_id)
        match = re.fullmatch(r'((?:NO_ITEM|ITEM)_(?:DEFAULT|CUSTOM)_MOVES)\((\w+)\)', f['party'])
        if not match: raise ValueError('Unrecognized trainer party type: ' + stable_id)
        shape, party_symbol = match.groups()
        custom, held = 'CUSTOM' in shape, shape.startswith('ITEM_')
        party = one('src/data/trainer_parties.h', r'^static const struct TrainerMon' + ('Item' if held else 'NoItem') + ('Custom' if custom else 'Default') + 'Moves ' + party_symbol + r'\[\] = \{\n(.*?)^\};')
        rows = list(re.finditer(r'    \{\n(.*?)    \},\n', party[1], re.S))
        if ''.join(row[0] for row in rows) != party[1] or not 1 <= len(rows) <= 6:
            raise ValueError('Unrecognized trainer party: ' + stable_id)
        members = []
        for row in rows:
            mf = fields(row[1])
            if set(mf) != {'iv', 'lvl', 'species'} | ({'heldItem'} if held else set()) | ({'moves'} if custom else set()):
                raise ValueError('Unrecognized party fields: ' + stable_id)
            species = mf['species']
            species_name = one('src/data/text/species_names.h', r'^    \[' + species + r'\] = _\("([^"\n]+)"\),$')[1]
            moves = [move.strip() for move in mf['moves'].strip('{}').split(',')] if custom else []
            if custom and len(moves) != 4:
                raise ValueError('Custom trainer party must preserve all four move slots')
            members.append(dict(species=identity(species), sourceSpeciesId=number(species),
                speciesName=species_name, encodedSpeciesName=encode(species_name), level=int(mf['lvl']),
                sourceIv=int(mf['iv']), heldItem=mf.get('heldItem', 'ITEM_NONE'), customMoves=[identity(move) for move in moves]))
        name = re.fullmatch(r'_\("([^"\n]+)"\)', f['trainerName'])[1]
        items = [item.strip() for item in f['items'].strip('{}').split(',') if item.strip()]
        if len(items) > 4 or f['doubleBattle'] not in ('TRUE', 'FALSE'):
            raise ValueError('Unrecognized trainer items or battle mode: ' + stable_id)
        items += ['ITEM_NONE'] * (4 - len(items))
        money = int(one('src/battle_main.c', r'^\s*\{' + f['trainerClass'] + r', (\d+)\},$')[1])
        trainers.append(dict(id=identity(symbol), sourceTrainerId=number(symbol), trainerClass=f['trainerClass'],
            sourceTrainerClass=number(f['trainerClass']), trainerPic=f['trainerPic'], sourceTrainerPic=number(f['trainerPic']),
            sourceTrainerName=name, encodedTrainerName=encode(name), encounterMusicGender=number(f['encounterMusic_gender']),
            items=items, doubleBattle=f['doubleBattle'] == 'TRUE', partyFlags=int(custom) | (int(held) << 1),
            aiFlags=number(f['aiFlags']), aiFlagSymbols=f['aiFlags'].split(' | '), baseMoney=money, party=members))
    return sorted(trainers, key=lambda trainer: trainer['sourceTrainerId'])


def reference_ids(generated):
    identities = set()
    for path in sorted(Path(generated).rglob('*.json')):
        if path.parent.name == 'shared-trainers': continue
        document = json.loads(path.read_text())
        if not isinstance(document, dict): continue
        references = document.get('trainers')
        if isinstance(document.get('trainer'), str) and document['trainer'].startswith('pret:TRAINER_'):
            identities.add(document['trainer'])
        if isinstance(references, list) and references and all(isinstance(row, str) and row.startswith('pret:TRAINER_') for row in references):
            if len(set(references)) != len(references):
                raise ValueError('Duplicate trainer reference in ' + str(path))
            identities.update(references)
    if not identities: raise ValueError('No regional trainer references')
    return sorted(identities)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw-root', type=Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--generated-root', type=Path, default=ROOT / 'content/generated')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    identities = reference_ids(args.generated_root)
    definitions = generate(args.raw_root, identities)
    publish_trainers(args.generated_root, definitions, check=args.check)
    if set(read_trainers(args.generated_root)) != set(identities):
        raise ValueError('Shared trainer table contains unreferenced definitions')
    print(json.dumps(dict(checked=args.check, trainers=len(definitions), members=sum(len(t['party']) for t in definitions))))


if __name__ == '__main__': main()

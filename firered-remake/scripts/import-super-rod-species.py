#!/usr/bin/env python3
"""Authenticate the single Qwilfish ownership extension and 41 existing machine cells.

Reads pinned local Git blobs and authenticated shared source resources. No upstream code runs.
All other Super Rod slot species and all 354 moves are inherited unchanged.
"""
import argparse
import json
from pathlib import Path
import re
import subprocess

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE = 'content/generated/super-rod-species/'
DEPENDENCIES = (
    'content/generated/canonical-pokemon/catalog.json',
    'content/generated/seafoam-rules/pokemon.json',
    'content/generated/seafoam-rules/machines.json',
)


def encode(value): return (json.dumps(value, indent=2, ensure_ascii=False) + '\n').encode()
def need(value, message):
    if not value: raise ValueError(message)


def generate(reference, dependency):
    dependencies = []
    def dependent(path):
        data = (dependency / path).read_bytes()
        dependencies.append(dict(path=path))
        return json.loads(data)
    canonical, previous, machines = [dependent(p) for p in DEPENDENCIES]
    need(previous['ownedSpeciesCount'] == 162 and previous['opponentSpeciesCount'] == 191 and
         machines['speciesCount'] == 162 and machines['machineCount'] == 41, 'Exact Seafoam predecessor')
    symbol = 'SPECIES_QWILFISH'
    definition = next(s for s in canonical['species'] if s['id']['value'] == 'pret:' + symbol)
    metadata = next(s for s in canonical['metadata'] if s['id']['value'] == 'pret:' + symbol)
    need(definition['sourceSpeciesId'] == metadata['nationalDexNumber'] == 211 and
         not definition['evolutions'] and not metadata['sourceEvolutions'], 'Terminal Qwilfish source')
    need((definition['ability1'], definition['ability2']) == ('ABILITY_POISON_POINT', 'ABILITY_SWIFT_SWIM'), 'Qwilfish ability pair')
    inputs, raw = {}, {}
    def read(path):
        if path not in raw:
            data = subprocess.check_output(['git', 'show', COMMIT + ':' + path], cwd=reference)
            shared = 'content/raw/pokefirered/037335f4/' + path
            need((dependency / shared).read_bytes() == data, 'Shared source differs: ' + path)
            inputs[path] = dict(path=path, localPath=shared, shared=True)
            raw[path] = data
        return raw[path].decode()
    for sr in [definition['source'], definition['learnsetSource'], metadata['learnsetPointerSource'], metadata['dexSource']] + [m['source'] for m in definition['levelUpMoves']]:
        read(sr['path'])
    tm = 'src/data/pokemon/tmhm_learnsets.h'
    text = read(tm)
    matches = list(re.finditer(r'^    \[SPECIES_QWILFISH\]\s*=\s*TMHM_LEARNSET\((.*?)\),', text, re.M | re.S))
    need(len(matches) == 1, 'Exact Qwilfish machine row')
    row = matches[0]
    source = dict(path=tm, symbol=symbol, startLine=text.count('\n', 0, row.start()) + 1,
                  endLine=text.count('\n', 0, row.end()) + 1)
    machine_ids = sorted(r['item']['value'][5:] for r in machines['additions'])
    need(len(machine_ids) == 41, 'Exactly 41 inherited machines')
    cells = [dict(item=dict(value='pret:' + item), compatibility=[dict(species=dict(value='pret:' + symbol),
        sourceSpeciesId=211, canLearn=bool(re.search(r'TMHM\(' + item[5:] + r'_', row[1])), source=source)]) for item in machine_ids]
    docs = {
        'pokemon.json': dict(sourceCommit=COMMIT,  profile='firered-super-rod-owned-pokemon-v1',

            previousOwnedSpeciesCount=162, ownedSpeciesCount=163, previousOpponentSpeciesCount=191, opponentSpeciesCount=192,
            ownedSpeciesAdded=[symbol], opponentSpeciesAdded=[symbol], trainerOnlySpeciesAdded=[], evolutions=[],
            movesAdded=[]),
        'machines.json': dict(sourceCommit=COMMIT, profile='firered-super-rod-teaching-v1', previousSpeciesCount=162,
            speciesCount=163, machineCount=41, additions=cells,  itemsAdded=[]),
        'source-input-hashes.json': dict(sourceCommit=COMMIT,
            inputs=[inputs[p] for p in sorted(inputs)], dependencyInputs=dependencies),
    }
    return docs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for argument in ['reference', 'dependency', 'output']: parser.add_argument(argument, type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    docs = generate(args.reference, args.dependency)
    files = {BASE + name: encode(doc) for name, doc in docs.items()}
    lock = dict(sourceCommit=COMMIT,  profile='firered-super-rod-species-v1',

        files=[dict(path=p) for p, data in sorted(files.items())])
    files['scripts/super-rod-species.lock.json'] = encode(lock)
    for name, data in files.items():
        path = args.output / name
        if args.check: need(path.read_bytes() == data, 'Regeneration differs: ' + name)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    print(json.dumps(dict(checked=args.check, owned=163, opponents=192, movesAdded=0, newMachineCells=41), indent=2))


if __name__ == '__main__': main()

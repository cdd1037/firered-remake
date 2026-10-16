#!/usr/bin/env python3
"""Authenticate the single Qwilfish ownership extension and 41 existing machine cells.

Reads pinned local Git blobs and authenticated shared source resources. No upstream code runs.
All other Super Rod slot species and all 354 moves are inherited unchanged.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import json
from pathlib import Path
import re
import subprocess

import sys
from machine_catalog import compatibility_additions, machine_definitions, admission_species

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE = 'content/generated/data/pokemon/super-rod/'
DEPENDENCIES = (
    'content/generated/data/pokemon/canonical/catalog.json',
    'content/generated/data/regions/seafoam/pokemon.json',
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
    canonical, previous = [dependent(p) for p in DEPENDENCIES]
    need(previous['ownedSpeciesCount'] == 162 and previous['opponentSpeciesCount'] == 191, 'Exact Seafoam ownership')
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
    cells = compatibility_additions(dependency, 'content/generated/data/pokemon/super-rod/machines.json')
    docs = {
        'pokemon.json': dict(previousOwnedSpeciesCount=162, ownedSpeciesCount=163, previousOpponentSpeciesCount=191, opponentSpeciesCount=192, ownedSpeciesAdded=[symbol], opponentSpeciesAdded=[symbol], trainerOnlySpeciesAdded=[], evolutions=[], movesAdded=[]),
        'machines.json': dict(previousSpeciesCount=162, speciesCount=163, machineCount=41, additions=cells, itemsAdded=[]),

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
    for name, data in files.items():
        path = args.output / name
        if args.check: need(path.read_bytes() == data, 'Regeneration differs: ' + name)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    print(json.dumps(dict(checked=args.check, owned=163, opponents=192, movesAdded=0, newMachineCells=41), indent=2))


if __name__ == '__main__': main()

#!/usr/bin/env python3
"""Review helper: print a candidate lock from the pinned Git tree; never execute upstream code."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import hashlib
import json
from pathlib import Path
import subprocess

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-canonical-species-metadata-v1'
FILES = sorted([
    'include/constants/abilities.h', 'include/constants/items.h',
    'include/constants/moves.h', 'include/constants/pokedex.h',
    'include/constants/pokemon.h', 'include/constants/species.h',
    'include/pokedex.h', 'include/random.h',
    'src/data/pokemon/evolution.h', 'src/data/pokemon/experience_tables.h',
    'src/data/pokemon/level_up_learnset_pointers.h',
    'src/data/pokemon/level_up_learnsets.h', 'src/data/pokemon/pokedex_entries.h',
    'src/data/pokemon/species_info.h', 'src/pokemon.c',
])

def encode(value):
    return (json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False) + '\n').encode('utf-8')

def git(repo, *args):
    return subprocess.check_output(['git', '-C', str(repo), *args])

def matches(path, expected):
    with path.open('rb') as stream:
        return stream.read(len(expected) + 1) == expected

def inventory(repo):
    if git(repo, 'rev-parse', COMMIT + '^{tree}').decode().strip() != TREE:
        raise ValueError('Pinned Git tree differs')
    rows = []
    for path in FILES:
        data = git(repo, 'show', COMMIT + ':' + path)
        blob = git(repo, 'rev-parse', COMMIT + ':' + path).decode().strip()
        rows.append(dict(path=path, byteLength=len(data), gitBlobSha1=blob,
                         sha256=hashlib.sha256(data).hexdigest()))
    return dict(schemaVersion=1, repository='pret/pokefirered', commit=COMMIT,
                tree=TREE, profile=PROFILE, gameEdition='FIRERED', romRevision=0,
                canonicalSpeciesCount=386, officialAbilityCount=76, files=rows)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source_root', type=Path)
    parser.add_argument('--check', type=Path, help='Require exact byte equality with an already reviewed lock.')
    args = parser.parse_args()
    data = encode(inventory(args.source_root))
    if args.check:
        if not matches(args.check, data):
            raise SystemExit('Source inventory differs from reviewed lock')
        print('PASS source inventory generated from the pinned Git tree')
    else:
        print(data.decode(), end='')

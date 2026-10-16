#!/usr/bin/env python3
"""Rebuild Celio's three species and 117 machine cells from local raw files or pinned Git objects.
Usage: [REFERENCE] DEPENDENCY OUTPUT [--raw-root PATH] [--check]. Never executes upstream code.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import json
import re
from pathlib import Path
from import_canonical_pokemon import Sources
from pokemon_import_io import safe, read_source, write_files
from machine_catalog import compatibility_additions

ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
ADDITIONS = ['SPECIES_ENTEI', 'SPECIES_RAIKOU', 'SPECIES_SUICUNE']
DEPENDENCIES = (
    'content/generated/data/pokemon/canonical/catalog.json',
    'content/generated/data/regions/five-island/pokemon.json',
    'scripts/recipes/story/five-island-predecessor.json',
)

def need(value, message):
    if not value: raise ValueError(message)
def encoded(value): return (json.dumps(value, separators=(',', ':'), ensure_ascii=False) + '\n').encode()

def generate(reference, dependency, raw=None):
    raw_root = raw if raw is not None else ROOT / "content/raw/pokefirered/037335f4"
    texts = {}
    def read(path):
        if path not in texts:
            data = read_source(raw_root, path, commit=COMMIT, repository=reference)
            texts[path] = data.decode()
        return texts[path]
    def dep(path):
        data = safe(dependency, path).read_bytes()
        return json.loads(data)
    def one(path, pattern):
        rows = list(re.finditer(pattern, read(path), re.M | re.S))
        need(len(rows) == 1, 'Exactly one source declaration: ' + pattern)
        return rows[0]
    def source_ref(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=read(path).count('\n', 0, match.start()) + 1,
                    endLine=read(path).count('\n', 0, match.end()) + 1)
    canonical = dep('content/generated/data/pokemon/canonical/catalog.json')
    five_pokemon = dep('content/generated/data/regions/five-island/pokemon.json')
    baseline = dep('scripts/recipes/story/five-island-predecessor.json')
    species = {s['id']['value'][5:]: s for s in canonical['species']}
    metadata = {s['id']['value'][5:]: s for s in canonical['metadata']}
    need(len(species) == 386 and len(canonical['abilities']) == 76, 'Canonical source closure')
    owned = set(baseline['ownedSpecies']) | {'pret:' + s for s in five_pokemon['ownedSpeciesAdded']}
    opponents = set(baseline['opponentSpecies']) | owned | {'pret:' + s for s in five_pokemon['opponentSpeciesAdded']}
    added_ids = {'pret:' + s for s in ADDITIONS}
    need(len(owned) == 135 and len(opponents) == 159 and owned <= opponents and not opponents & added_ids, 'Exact Five predecessor scope')
    cells = compatibility_additions(ROOT, 'content/generated/data/pokemon/celio/machines.json')
    need(len(cells) == 39, 'Exact admitted machine scope')
    source = Sources({path: read(path) for path in (
        'src/data/pokemon/species_info.h', 'src/data/pokemon/level_up_learnset_pointers.h',
        'src/data/pokemon/level_up_learnsets.h', 'include/constants/species.h',
        'include/constants/pokemon.h', 'include/constants/abilities.h')}, {})
    species_rows, level50 = [], []
    for name in ADDITIONS:
        original, meta = species[name], metadata[name]
        parsed, _ = source.species_definition(name)
        need(parsed == original, 'Canonical source mechanics differ: ' + name)
        need((parsed['ability1'], parsed['ability2']) == ('ABILITY_PRESSURE', 'ABILITY_NONE'), 'Pressure identity')
        raw_moves = [(row['level'], row['move']['value']) for row in parsed['levelUpMoves']]
        need(not meta['sourceEvolutions'] and not original['evolutions'], 'Terminal legendary evolution')
        need(not re.search(r'^    \[' + name + r'\]', read('src/data/pokemon/evolution.h'), re.M), 'No source evolution declaration')
        initial = []
        for level, move in raw_moves:
            if level > 50: break
            if move not in initial: initial = (initial + [move])[-4:]
        need(len(initial) == 4, 'Four level-50 moves')
        level50.append(dict(species=original['id'], level=50, moves=[dict(value=m) for m in initial]))
        species_rows.append(dict(definition=dict(id=original['id'],evolutions=original['evolutions']), metadata=meta))
    ability = next(a for a in canonical['abilities'] if a['symbol'] == 'ABILITY_PRESSURE')
    # Includes the initialization declaration establishing why these three species are admitted.
    roamer = read('src/roamer.c')
    for name in ADDITIONS: need('a = ' + name + ';' in roamer, 'Original roamer selection: ' + name)
    pokemon = dict(previousOwnedSpeciesCount=len(owned), ownedSpeciesCount=len(owned | added_ids), previousOpponentSpeciesCount=len(opponents), opponentSpeciesCount=len(opponents | added_ids), speciesAdded=ADDITIONS, trainerSpeciesAdded=[], species=species_rows, level50Moves=level50, pressureAbility=dict(symbol=ability['symbol'], sourceAbilityId=ability['sourceAbilityId']), limitation='Catalog authentication alone does not admit roaming encounters, lifecycle, battle AI, capture, presentation or save migration.')
    machines = dict(previousSpeciesCount=len(owned), speciesCount=len(owned | added_ids), machineCount=len(cells), additions=cells, itemsAdded=[])
    return {'pokemon.json': pokemon, 'machines.json': machines}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('reference', type=Path, nargs='?', help='Optional local Git repository at the pinned revision')
    for name in ['dependency', 'output']: parser.add_argument(name, type=Path)
    parser.add_argument('--raw-root', type=Path, help='Local raw input directory when no reference repository is selected')
    parser.add_argument('--check', action='store_true'); args = parser.parse_args()
    if args.reference is not None and args.raw_root is not None: parser.error('Choose either REFERENCE or --raw-root')
    docs = generate(args.reference, args.dependency, args.raw_root)
    files = {'content/generated/data/pokemon/celio/' + name: encoded(value) for name, value in docs.items()}
    lock = dict(sourceCommit=COMMIT,  profile='firered-celio-pokemon-source-v1', files=[dict(path=path) for path, data in sorted(files.items())])
    if args.check:
        for path, data in files.items():
            need(safe(args.output, path).read_bytes() == data, 'Deterministic regeneration differs: ' + path)
    else:
        write_files(args.output, files)
    print(json.dumps(dict(checked=args.check, owned=docs['pokemon.json']['ownedSpeciesCount'], opponents=docs['pokemon.json']['opponentSpeciesCount'], machines=39, cells=117), indent=2))
if __name__ == '__main__': main()

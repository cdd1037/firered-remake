#!/usr/bin/env python3
"""Rebuild Celio's three species and 117 machine cells from pinned local Git objects.
Usage: REFERENCE DEPENDENCY OUTPUT [--check]. Never executes upstream code.
"""
import argparse
import json
import re
import subprocess
from pathlib import Path

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
ADDITIONS = ['SPECIES_ENTEI', 'SPECIES_RAIKOU', 'SPECIES_SUICUNE']
DEPENDENCIES = (
    'content/generated/canonical-pokemon/catalog.json',
    'content/generated/five-island-rules/items.json',
    'content/generated/five-island-rules/pokemon.json',
    'scripts/five-island-predecessor.json',
)

def need(value, message):
    if not value: raise ValueError(message)
def encoded(value): return (json.dumps(value, indent=2, ensure_ascii=False) + '\n').encode()

def generate(reference, dependency):
    texts, inputs, dependencies = {}, {}, []
    def read(path):
        if path not in texts:
            data = subprocess.check_output(['git', 'show', COMMIT + ':' + path], cwd=reference)
            inputs[path] = dict(path=path)
            texts[path] = data.decode()
        return texts[path]
    def dep(path):
        data = (dependency / path).read_bytes()
        dependencies.append(dict(path=path))
        return json.loads(data)
    def one(path, pattern):
        rows = list(re.finditer(pattern, read(path), re.M | re.S))
        need(len(rows) == 1, 'Exactly one source declaration: ' + pattern)
        return rows[0]
    def source_ref(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=read(path).count('\n', 0, match.start()) + 1,
                    endLine=read(path).count('\n', 0, match.end()) + 1)
    canonical = dep('content/generated/canonical-pokemon/catalog.json')
    five_items = dep('content/generated/five-island-rules/items.json')
    five_pokemon = dep('content/generated/five-island-rules/pokemon.json')
    baseline = dep('scripts/five-island-predecessor.json')
    species = {s['id']['value'][5:]: s for s in canonical['species']}
    metadata = {s['id']['value'][5:]: s for s in canonical['metadata']}
    need(len(species) == 386 and len(canonical['abilities']) == 76, 'Canonical source closure')
    owned = {r['species']['value'] for r in five_items['tm36']['compatibility']}
    opponents = set(baseline['opponentSpecies']) | owned | {'pret:' + s for s in five_pokemon['opponentSpeciesAdded']}
    added_ids = {'pret:' + s for s in ADDITIONS}
    need(len(owned) == 135 and len(opponents) == 159 and owned <= opponents and not opponents & added_ids, 'Exact Five predecessor scope')
    machine_ids = [r['item']['value'][5:] for r in five_items['machineAdditions']] + ['ITEM_TM36']
    need(len(machine_ids) == len(set(machine_ids)) == 39, 'Exact inherited machine scope')
    tm_path = 'src/data/pokemon/tmhm_learnsets.h'
    tm_rows = {m[1]: m for m in re.finditer(r'\[(SPECIES_\w+)\]\s*=\s*TMHM_LEARNSET\((.*?)\),', read(tm_path), re.S)}
    species_rows, level50, cells = [], [], []
    for name in ADDITIONS:
        original, meta = species[name], metadata[name]
        for sr in [original['source'], original['learnsetSource'], meta['learnsetPointerSource'], meta['dexSource']] + [m['source'] for m in original['levelUpMoves']]:
            read(sr['path'])
        info = one('src/data/pokemon/species_info.h', r'^    \[' + name + r'\] =\n    \{\n(.*?)^    \},')
        fields = dict(re.findall(r'^        \.(\w+) = (.*),$', info[1], re.M))
        need(fields == meta['originalFields'], 'Complete source fields: ' + name)
        for source_key, key in [('baseHP','hp'),('baseAttack','attack'),('baseDefense','defense'),('baseSpeed','speed'),('baseSpAttack','specialAttack'),('baseSpDefense','specialDefense')]:
            need(original['baseStats'][key] == int(fields[source_key]), 'Base stats: ' + name)
        need(fields['abilities'] == '{ABILITY_PRESSURE, ABILITY_NONE}' and original['ability1'] == 'ABILITY_PRESSURE' and original['ability2'] == 'ABILITY_NONE', 'Pressure identity')
        source_id = int(one('include/constants/species.h', r'^#define ' + name + r'\s+(\d+)\s*$')[1])
        need(source_id == original['sourceSpeciesId'], 'Species numeric identity')
        pointer = one('src/data/pokemon/level_up_learnset_pointers.h', r'^    \[' + name + r'\] = (\w+),$')[1]
        moves = one('src/data/pokemon/level_up_learnsets.h', r'^static const u16 ' + pointer + r'\[\] = \{\n(.*?)^\};')
        raw_moves = [(int(l), 'pret:' + m) for l, m in re.findall(r'LEVEL_UP_MOVE\(\s*(\d+), (MOVE_\w+)\)', moves[1])]
        need(raw_moves == [(m['level'], m['move']['value']) for m in original['levelUpMoves']], 'Complete natural learnset')
        need(not meta['sourceEvolutions'] and not original['evolutions'], 'Terminal legendary evolution')
        need(not re.search(r'^    \[' + name + r'\]', read('src/data/pokemon/evolution.h'), re.M), 'No source evolution declaration')
        initial = []
        for level, move in raw_moves:
            if level > 50: break
            if move not in initial: initial = (initial + [move])[-4:]
        need(len(initial) == 4, 'Four level-50 moves')
        level50.append(dict(species=original['id'], level=50, moves=[dict(value=m) for m in initial]))
        species_rows.append(dict(definition=original, metadata=meta))
    for item in sorted(machine_ids):
        rows = []
        for name in ADDITIONS:
            match = tm_rows[name]
            rows.append(dict(species=dict(value='pret:' + name), sourceSpeciesId=species[name]['sourceSpeciesId'],
                canLearn=bool(re.search(r'TMHM\(' + item[5:] + r'_', match[2])), source=source_ref(tm_path, name, match)))
        cells.append(dict(item=dict(value='pret:' + item), compatibility=rows))
    ability = next(a for a in canonical['abilities'] if a['symbol'] == 'ABILITY_PRESSURE')
    read(ability['source']['path'])
    # Includes the initialization declaration establishing why these three species are admitted.
    roamer = read('src/roamer.c')
    for name in ADDITIONS: need('a = ' + name + ';' in roamer, 'Original roamer selection: ' + name)
    pokemon = dict(sourceCommit=COMMIT,  profile='firered-celio-owned-pokemon-v1',
        previousOwnedSpeciesCount=len(owned), ownedSpeciesCount=len(owned | added_ids), previousOpponentSpeciesCount=len(opponents), opponentSpeciesCount=len(opponents | added_ids),
        speciesAdded=ADDITIONS, trainerSpeciesAdded=[], species=species_rows, level50Moves=level50,
        pressureAbility=dict(symbol=ability['symbol'], sourceAbilityId=ability['sourceAbilityId'], source=ability['source']),
        limitation='Catalog authentication alone does not admit roaming encounters, lifecycle, battle AI, capture, presentation or save migration.')
    machines = dict(sourceCommit=COMMIT, profile='firered-celio-teaching-v1', previousSpeciesCount=len(owned), speciesCount=len(owned | added_ids),
        machineCount=len(machine_ids), additions=cells,  itemsAdded=[])
    evidence = dict(sourceCommit=COMMIT,  files=[inputs[k] for k in sorted(inputs)], dependencyInputs=dependencies)
    return {'pokemon.json': pokemon, 'machines.json': machines, 'source-input-hashes.json': evidence}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['reference', 'dependency', 'output']: parser.add_argument(name, type=Path)
    parser.add_argument('--check', action='store_true'); args = parser.parse_args()
    docs = generate(args.reference, args.dependency)
    files = {'content/generated/celio-pokemon/' + name: encoded(value) for name, value in docs.items()}
    lock = dict(sourceCommit=COMMIT,  profile='firered-celio-pokemon-source-v1', files=[dict(path=path) for path, data in sorted(files.items())])
    files['scripts/celio-pokemon.lock.json'] = encoded(lock)
    for path, data in files.items():
        target = args.output / path
        if args.check: need(target.read_bytes() == data, 'Deterministic regeneration differs: ' + path)
        else: target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(data)
    print(json.dumps(dict(checked=args.check, owned=docs['pokemon.json']['ownedSpeciesCount'], opponents=docs['pokemon.json']['opponentSpeciesCount'], machines=39, cells=117), indent=2))
if __name__ == '__main__': main()

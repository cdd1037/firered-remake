#!/usr/bin/env python3
"""Rebuild the Cerulean Cave catalog extension from pinned local Git objects.

Usage: REFERENCE DEPENDENCY OUTPUT [--check]. Never executes upstream code.
The full encounter metadata is retained; runtime encounter admission is separate.
"""
import argparse
import json
import re
import subprocess
from pathlib import Path

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
MAPS = ['MAP_CERULEAN_CAVE_1F', 'MAP_CERULEAN_CAVE_2F', 'MAP_CERULEAN_CAVE_B1F']
DEPENDENCIES = (
    'content/generated/canonical-pokemon/catalog.json',
    'content/generated/celio-pokemon/pokemon.json',
    'content/generated/celio-pokemon/machines.json',
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
    celio = dep('content/generated/celio-pokemon/pokemon.json')
    celio_machines = dep('content/generated/celio-pokemon/machines.json')
    five_items = dep('content/generated/five-island-rules/items.json')
    five_pokemon = dep('content/generated/five-island-rules/pokemon.json')
    baseline = dep('scripts/five-island-predecessor.json')
    species = {s['id']['value'][5:]: s for s in canonical['species']}
    metadata = {s['id']['value'][5:]: s for s in canonical['metadata']}
    need(len(species) == 386 and len(canonical['abilities']) == 76, 'Canonical source closure')
    owned = {r['species']['value'] for r in five_items['tm36']['compatibility']} | {'pret:' + s for s in celio['speciesAdded']}
    opponents = set(baseline['opponentSpecies']) | owned | {'pret:' + s for s in five_pokemon['opponentSpeciesAdded']}
    need(len(owned) == 138 and len(opponents) == 162 and owned <= opponents, 'Exact Celio predecessor scope')
    machine_ids = [r['item']['value'][5:] for r in celio_machines['additions']]
    need(len(machine_ids) == len(set(machine_ids)) == 39, 'Exact inherited machine scope')

    wild_path = 'src/data/wild_encounters.json'
    wild = json.loads(read(wild_path))
    encounters = [e for g in wild['wild_encounter_groups'] for e in g.get('encounters', [])
                  if e['map'] in MAPS and e['base_label'].endswith('_FireRed')]
    need([e['map'] for e in encounters] == MAPS, 'Exactly three FireRed floors, in original order')
    fields = next(g['fields'] for g in wild['wild_encounter_groups'] if 'fields' in g)
    fishing_field = next(f for f in fields if f['type'] == 'fishing_mons')
    old_rod_slots = fishing_field['groups']['old_rod']
    need(old_rod_slots == [0, 1], 'Source Old Rod slots')
    admitted_species, metadata_species = set(), set()
    for e in encounters:
        for name in ['land_mons', 'water_mons', 'rock_smash_mons', 'fishing_mons']:
            for i, row in enumerate(e.get(name, {}).get('mons', [])):
                metadata_species.add(row['species'])
                if name in ['land_mons', 'water_mons'] or name == 'fishing_mons' and i in old_rod_slots:
                    admitted_species.add(row['species'])
    static_path = 'data/maps/CeruleanCave_B1F/scripts.inc'
    static = one(static_path, r'^\s*setwildbattle (SPECIES_\w+), (\d+)\s*$')
    static_name, static_level = static[1], int(static[2])
    need(static_name == 'SPECIES_MEWTWO' and static_level == 70, 'Source stationary Mewtwo')
    additions = sorted((admitted_species | {static_name}) - {s[5:] for s in owned})
    need(additions == ['SPECIES_MEWTWO'] and len(admitted_species) == 12 and len(metadata_species) == 18,
         'Minimal source-required species extension')
    need({'pret:' + s for s in metadata_species} <= owned, 'Every retained inactive encounter species already exists')
    need(not {'pret:' + s for s in additions} & opponents, 'No added opponent overlaps')

    species_rows, cells = [], []
    for name in additions:
        original, meta = species[name], metadata[name]
        for sr in [original['source'], original['learnsetSource'], meta['learnsetPointerSource'], meta['dexSource']] + [m['source'] for m in original['levelUpMoves']]:
            read(sr['path'])
        info = one('src/data/pokemon/species_info.h', r'^    \[' + name + r'\] =\n    \{\n(.*?)^    \},')
        source_fields = dict(re.findall(r'^        \.(\w+) = (.*),$', info[1], re.M))
        need(source_fields == meta['originalFields'], 'Complete original species fields')
        for source_key, key in [('baseHP','hp'),('baseAttack','attack'),('baseDefense','defense'),('baseSpeed','speed'),('baseSpAttack','specialAttack'),('baseSpDefense','specialDefense')]:
            need(original['baseStats'][key] == int(source_fields[source_key]), 'Base stats: ' + name)
        need(source_fields['abilities'] == '{ABILITY_PRESSURE, ABILITY_NONE}' and original['ability1'] == 'ABILITY_PRESSURE' and original['ability2'] == 'ABILITY_NONE', 'Pressure identity')
        need(int(one('include/constants/species.h', r'^#define ' + name + r'\s+(\d+)\s*$')[1]) == original['sourceSpeciesId'], 'Numeric species identity')
        pointer = one('src/data/pokemon/level_up_learnset_pointers.h', r'^    \[' + name + r'\] = (\w+),$')[1]
        moves = one('src/data/pokemon/level_up_learnsets.h', r'^static const u16 ' + pointer + r'\[\] = \{\n(.*?)^\};')
        raw_moves = [(int(l), 'pret:' + m) for l, m in re.findall(r'LEVEL_UP_MOVE\(\s*(\d+), (MOVE_\w+)\)', moves[1])]
        need(raw_moves == [(m['level'], m['move']['value']) for m in original['levelUpMoves']], 'Complete natural learnset')
        need(not meta['sourceEvolutions'] and not original['evolutions'], 'Terminal Mewtwo evolution')
        need(not re.search(r'^    \[' + name + r'\]', read('src/data/pokemon/evolution.h'), re.M), 'No source evolution declaration')
        initial = []
        for level, move in raw_moves:
            if level > static_level: break
            if move not in initial: initial = (initial + [move])[-4:]
        need(initial == ['pret:MOVE_SWIFT', 'pret:MOVE_RECOVER', 'pret:MOVE_SAFEGUARD', 'pret:MOVE_PSYCHIC'], 'Original level70 moves')
        species_rows.append(dict(definition=original, metadata=meta))
    tm_path = 'src/data/pokemon/tmhm_learnsets.h'
    for item in sorted(machine_ids):
        rows = []
        for name in additions:
            match = one(tm_path, r'^    \[' + name + r'\]\s*=\s*TMHM_LEARNSET\((.*?)\),')
            rows.append(dict(species=dict(value='pret:' + name), sourceSpeciesId=species[name]['sourceSpeciesId'],
                canLearn=bool(re.search(r'TMHM\(' + item[5:] + r'_', match[1])), source=source_ref(tm_path, name, match)))
        cells.append(dict(item=dict(value='pret:' + item), compatibility=rows))
    ability = next(a for a in canonical['abilities'] if a['symbol'] == 'ABILITY_PRESSURE')
    read(ability['source']['path'])
    pokemon = dict(sourceCommit=COMMIT,  profile='firered-cerulean-cave-owned-pokemon-v1',

        previousOwnedSpeciesCount=len(owned), ownedSpeciesCount=len(owned) + len(additions),
        previousOpponentSpeciesCount=len(opponents), opponentSpeciesCount=len(opponents) + len(additions),
        speciesAdded=additions, trainerSpeciesAdded=[], species=species_rows,
        staticEncounter=dict(species=dict(value='pret:' + static_name), level=static_level, moves=[dict(value=m) for m in initial],
                             source=source_ref(static_path, 'CeruleanCave_B1F_EventScript_Mewtwo', static)),
        admittedEncounterSpecies=sorted(admitted_species), originalEncounters=encounters, originalEncounterFields=fields,
        runtimeMethods=['land', 'surf', 'oldRod'], retainedMetadataOnlyMethods=['rockSmash', 'goodRod', 'superRod'],
        pressureAbility=dict(symbol=ability['symbol'], sourceAbilityId=ability['sourceAbilityId'], source=ability['source']),
        limitation='This catalog authenticates data only; it grants no encounter, battle, trainer, map, lifecycle or save admission.')
    machines = dict(sourceCommit=COMMIT, profile='firered-cerulean-cave-teaching-v1', previousSpeciesCount=len(owned),
        speciesCount=len(owned) + len(additions), machineCount=len(machine_ids), additions=cells,
         itemsAdded=[])
    evidence = dict(sourceCommit=COMMIT,  files=[inputs[k] for k in sorted(inputs)], dependencyInputs=dependencies)
    return {'pokemon.json': pokemon, 'machines.json': machines, 'source-input-hashes.json': evidence}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['reference', 'dependency', 'output']: parser.add_argument(name, type=Path)
    parser.add_argument('--check', action='store_true'); args = parser.parse_args()
    docs = generate(args.reference, args.dependency)
    files = {'content/generated/cerulean-cave-pokemon/' + name: encoded(value) for name, value in docs.items()}
    lock = dict(sourceCommit=COMMIT,  profile='firered-cerulean-cave-pokemon-source-v1',
                files=[dict(path=p) for p, d in sorted(files.items())])
    files['scripts/cerulean-cave-pokemon.lock.json'] = encoded(lock)
    for path, data in files.items():
        target = args.output / path
        if args.check: need(target.read_bytes() == data, 'Deterministic regeneration differs: ' + path)
        else: target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(data)
    print(json.dumps(dict(checked=args.check, owned=139, opponents=163, machines=39, addedCells=39), indent=2))


if __name__ == '__main__': main()

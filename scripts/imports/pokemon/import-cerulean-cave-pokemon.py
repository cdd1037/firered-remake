#!/usr/bin/env python3
"""Rebuild the Cerulean Cave catalog extension from local raw files or pinned Git objects.

Usage: [REFERENCE] DEPENDENCY OUTPUT [--raw-root PATH] [--check]. Never executes upstream code.
The full encounter metadata is retained; runtime encounter admission is separate.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import json
import re
from pathlib import Path
from pokemon_import_io import safe, read_source, write_files
from machine_catalog import compatibility_additions

ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
MAPS = ['MAP_CERULEAN_CAVE_1F', 'MAP_CERULEAN_CAVE_2F', 'MAP_CERULEAN_CAVE_B1F']
DEPENDENCIES = (
    'content/generated/data/pokemon/canonical/catalog.json',
    'content/generated/data/pokemon/celio/pokemon.json',
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
    celio = dep('content/generated/data/pokemon/celio/pokemon.json')
    five_pokemon = dep('content/generated/data/regions/five-island/pokemon.json')
    baseline = dep('scripts/recipes/story/five-island-predecessor.json')
    species = {s['id']['value'][5:]: s for s in canonical['species']}
    metadata = {s['id']['value'][5:]: s for s in canonical['metadata']}
    need(len(species) == 386 and len(canonical['abilities']) == 76, 'Canonical source closure')
    owned = set(baseline['ownedSpecies']) | {'pret:' + s for s in five_pokemon['ownedSpeciesAdded'] + celio['speciesAdded']}
    opponents = set(baseline['opponentSpecies']) | owned | {'pret:' + s for s in five_pokemon['opponentSpeciesAdded']}
    need(len(owned) == 138 and len(opponents) == 162 and owned <= opponents, 'Exact Celio predecessor scope')
    cells = compatibility_additions(ROOT, 'content/generated/data/pokemon/cerulean-cave/machines.json')
    need(len(cells) == 39, 'Exact admitted machine scope')

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

    species_rows = []
    for name in additions:
        original, meta = species[name], metadata[name]
        info = one('src/data/pokemon/species_info.h', r'^    \[' + name + r'\] =\n    \{\n(.*?)^    \},')
        source_fields = dict(re.findall(r'^        \.(\w+) = (.*),$', info[1], re.M))

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
        species_rows.append(dict(definition=dict(id=original['id'],evolutions=original['evolutions']), metadata=meta))
    ability = next(a for a in canonical['abilities'] if a['symbol'] == 'ABILITY_PRESSURE')
    pokemon = dict(previousOwnedSpeciesCount=len(owned), ownedSpeciesCount=len(owned) + len(additions), previousOpponentSpeciesCount=len(opponents), opponentSpeciesCount=len(opponents) + len(additions), speciesAdded=additions, trainerSpeciesAdded=[], species=species_rows, staticEncounter=dict(species=dict(value='pret:' + static_name), level=static_level, moves=[dict(value=m) for m in initial], source=source_ref(static_path, 'CeruleanCave_B1F_EventScript_Mewtwo', static)), admittedEncounterSpecies=sorted(admitted_species), originalEncounters=encounters, originalEncounterFields=fields, runtimeMethods=['land', 'surf', 'oldRod'], retainedMetadataOnlyMethods=['rockSmash', 'goodRod', 'superRod'], pressureAbility=dict(symbol=ability['symbol'], sourceAbilityId=ability['sourceAbilityId']), limitation='This catalog authenticates data only; it grants no encounter, battle, trainer, map, lifecycle or save admission.')
    machines = dict(previousSpeciesCount=len(owned), speciesCount=len(owned) + len(additions), machineCount=len(cells), additions=cells, itemsAdded=[])
    return {'pokemon.json': pokemon, 'machines.json': machines}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('reference', type=Path, nargs='?', help='Optional local Git repository at the pinned revision')
    for name in ['dependency', 'output']: parser.add_argument(name, type=Path)
    parser.add_argument('--raw-root', type=Path, help='Local raw input directory when no reference repository is selected')
    parser.add_argument('--check', action='store_true'); args = parser.parse_args()
    if args.reference is not None and args.raw_root is not None: parser.error('Choose either REFERENCE or --raw-root')
    docs = generate(args.reference, args.dependency, args.raw_root)
    files = {'content/generated/data/pokemon/cerulean-cave/' + name: encoded(value) for name, value in docs.items()}
    lock = dict(sourceCommit=COMMIT,  profile='firered-cerulean-cave-pokemon-source-v1',
                files=[dict(path=p) for p, d in sorted(files.items())])
    if args.check:
        for path, data in files.items():
            need(safe(args.output, path).read_bytes() == data, 'Deterministic regeneration differs: ' + path)
    else:
        write_files(args.output, files)
    print(json.dumps(dict(checked=args.check, owned=139, opponents=163, machines=39, addedCells=39), indent=2))


if __name__ == '__main__': main()

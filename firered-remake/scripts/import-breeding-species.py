#!/usr/bin/env python3
"""Authenticate the eight breeding-family species and 51 existing machine rows.

Reads pinned local Git blobs and authenticated shared source resources. No upstream code runs.
All 174 predecessor species and all 354 moves are inherited unchanged.
"""
import argparse
import json
from pathlib import Path
import re
from pokemon_import_io import read_source
from machine_catalog import admission_species, compatibility_additions

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE = 'content/generated/breeding-species/'
DEPENDENCIES = (
    'content/generated/canonical-pokemon/catalog.json',
    'content/generated/game-corner-species/pokemon.json',
)
SHARED_INPUTS = {'include/constants/pokemon.h': 'content/raw/pokefirered/037335f4/include/constants/pokemon.h', 'src/data/party_menu.h': 'content/raw/pokefirered/037335f4/src/data/party_menu.h', 'src/data/pokemon/evolution.h': 'content/raw/pokefirered/037335f4/src/data/pokemon/evolution.h', 'src/data/pokemon/level_up_learnset_pointers.h': 'content/raw/pokefirered/037335f4/src/data/pokemon/level_up_learnset_pointers.h', 'src/data/pokemon/level_up_learnsets.h': 'content/raw/pokefirered/037335f4/src/data/pokemon/level_up_learnsets.h', 'src/data/pokemon/pokedex_entries.h': 'content/raw/pokefirered/037335f4/src/data/pokemon/pokedex_entries.h', 'src/data/pokemon/species_info.h': 'content/raw/pokefirered/037335f4/src/data/pokemon/species_info.h', 'src/daycare.c': 'content/raw/pokefirered/037335f4/src/daycare.c', 'src/pokemon.c': 'content/raw/pokefirered/037335f4/src/pokemon.c'}
IDENTITIES = [('PICHU',172), ('CLEFFA',173), ('IGGLYBUFF',174), ('TYROGUE',236), ('SMOOCHUM',238), ('ELEKID',239), ('WYNAUT',360), ('HITMONTOP',237)]


def encode(value, *, compact=False):
    return (json.dumps(value, ensure_ascii=False, indent=None if compact else 2,
                       separators=(',', ':') if compact else None) + '\n').encode()
def need(value, message):
    if not value: raise ValueError(message)


def generate(reference, dependency):
    dependencies = []
    def dependent(path):
        data = (dependency / path).read_bytes()
        dependencies.append(dict(path=path))
        return json.loads(data)
    canonical, previous = [dependent(p) for p in DEPENDENCIES]
    dependencies.extend(dict(path=p) for p in ['scripts/import-config/machine-admissions.json', 'scripts/import-config/machine-catalog.json'])
    need(previous['ownedSpeciesCount'] == 174 and previous['opponentSpeciesCount'] == 196, 'Exact predecessor')
    identities = IDENTITIES
    definitions = []
    for name, number in identities:
        symbol = 'SPECIES_' + name
        definition = next(s for s in canonical['species'] if s['id']['value'] == 'pret:' + symbol)
        metadata = next(s for s in canonical['metadata'] if s['id']['value'] == 'pret:' + symbol)
        need(definition['sourceSpeciesId'] == metadata['nationalDexNumber'] == number and not definition['evolutions'], 'Canonical source: ' + symbol)
        definitions.append(dict(definition=definition, metadata=metadata))
    inputs, raw = {}, {}
    def read(path):
        if path not in raw:
            data = read_source(dependency / 'content/raw/pokefirered/037335f4', path, commit=COMMIT, repository=reference)
            shared = SHARED_INPUTS.get(path)
            if shared: need((dependency / shared).read_bytes() == data, 'Shared source differs: ' + path)
            local = shared or 'content/raw/pokefirered/037335f4/' + path
            inputs[path] = dict(path=path, localPath=local, shared=shared is not None)
            raw[path] = data
        return raw[path].decode()
    for row in definitions:
        definition, metadata = row['definition'], row['metadata']
        for sr in [definition['source'], definition['learnsetSource'], metadata['learnsetPointerSource'], metadata['dexSource']] + [e['source'] for e in metadata['sourceEvolutions']]:
            read(sr['path'])
    cells = compatibility_additions(dependency, BASE + 'machines.json')
    need(len(cells) == 51, 'Exactly 51 admitted machines')
    old_owned = list(admission_species(dependency, 'content/generated/game-corner-prizes/machines.json', '/additions/0'))
    owned = old_owned + ['pret:SPECIES_' + n for n, _ in identities]
    metadata_by_id = {r['id']['value']: r for r in canonical['metadata']}
    sorted_species = sorted(canonical['species'], key=lambda r:r['sourceSpeciesId'])
    def base_species(species):
        for _ in range(5):
            found = next((r['id']['value'] for r in sorted_species if any(e['target']['value'] == species for e in metadata_by_id[r['id']['value']]['sourceEvolutions'])), None)
            if found is None: break
            species = found
        return species
    groups = dict((k,int(v)) for k,v in re.findall(r'^#define (EGG_GROUP_\w+)\s+(\d+)',read('include/constants/pokemon.h'), re.M))
    egg = read('src/data/pokemon/egg_moves.h')
    egg_moves = {n: re.findall(r'MOVE_\w+', body) for n,body in re.findall(r'egg_moves\(([A-Z0-9_]+),\s*(.*?)\)',egg,re.S)}
    table = read('src/data/party_menu.h')
    body = re.search(r'static const u16 sTMHMMoves\[\]\s*=\s*\{(.*?)\};',table,re.S)[1]
    all_moves = re.findall(r'MOVE_\w+', body)
    need(len(all_moves) == 58, 'Original 50 TM and eight HM order')
    rows = []
    for species in owned:
        original = metadata_by_id[species]['originalFields']
        rows.append(dict(breeding=dict(species=dict(value=species),eggGroup1=groups[original['eggGroups'].split(',')[0].strip().strip('{} ').strip()],eggGroup2=groups[original['eggGroups'].split(',')[1].strip().strip('{} ').strip()],eggCycles=int(original['eggCycles'])),
            baseEggSpecies=dict(value=base_species(species)),eggMoves=[dict(value='pret:'+m) for m in egg_moves.get(species[13:],[])]))
    read('src/daycare.c');read('src/pokemon.c')
    evolution_rows=[dict(species=r['definition']['id'], **e) for r in definitions for e in r['metadata']['sourceEvolutions']]
    docs = {
        'pokemon.json': dict(sourceCommit=COMMIT,  profile='firered-breeding-owned-pokemon-v1',

            previousOwnedSpeciesCount=174, ownedSpeciesCount=182, previousOpponentSpeciesCount=196, opponentSpeciesCount=203,
            opponentSpeciesAdded=['SPECIES_' + name for name, _ in identities if name != 'HITMONTOP'],
            ownedSpeciesAdded=['SPECIES_' + name for name, _ in identities], evolutions=evolution_rows,
            movesAdded=[],
            acquisitionDependencies=[dict(species='SPECIES_WYNAUT',requires='ITEM_LAX_INCENSE',status='Original Lost Cave pickup is not yet admitted; no replacement source or item is added.')],
            excludedPreexistingGaps=['SPECIES_RHYDON','SPECIES_SLOWBRO','SPECIES_CROBAT','SPECIES_BLISSEY']),
        'machines.json': dict(sourceCommit=COMMIT, profile='firered-breeding-teaching-v1', previousSpeciesCount=174,
            speciesCount=182, machineCount=51, additions=cells,  itemsAdded=[]),
        'metadata.json': dict(sourceCommit=COMMIT, profile='firered-breeding-metadata-v1', speciesCount=182,
            allMachineMoves=[dict(value='pret:'+m) for m in all_moves], species=rows),
        'source-input-hashes.json': dict(sourceCommit=COMMIT,
            inputs=[inputs[p] for p in sorted(inputs)], dependencyInputs=dependencies),
    }
    docs['_raw']={inputs[p]['localPath']:data for p,data in raw.items() if not inputs[p]['shared']}

    return docs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('reference', type=lambda value: None if value == '-' else Path(value), help='Pinned Git checkout, or - to use retained raw inputs')
    for argument in ['dependency', 'output']: parser.add_argument(argument, type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    docs = generate(args.reference, args.dependency)
    raw = docs.pop('_raw')
    files = {BASE + name: encode(doc, compact=name in ('metadata.json', 'machines.json')) for name, doc in docs.items()}
    lock = dict(sourceCommit=COMMIT,  profile='firered-breeding-species-v1',

        files=[dict(path=p) for p, data in sorted(files.items())])
    for name, data in {**files, **raw}.items():
        path = args.output / name
        if args.check: need(path.read_bytes() == data, 'Regeneration differs: ' + name)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    print(json.dumps(dict(checked=args.check, owned=182, movesAdded=0, newMachineCells=408), indent=2))


if __name__ == '__main__': main()

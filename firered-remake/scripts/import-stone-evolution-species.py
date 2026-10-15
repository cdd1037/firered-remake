#!/usr/bin/env python3
"""Authenticate the seven direct stone targets and 42 existing machine rows.

Reads pinned local Git blobs and authenticated shared source resources. No upstream code runs.
All 163 predecessor species and all 354 moves are inherited unchanged.
"""
import argparse
import json
from pathlib import Path
import re
import subprocess

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE = 'content/generated/stone-evolution-species/'
DEPENDENCIES = (
    'content/generated/canonical-pokemon/catalog.json',
    'content/generated/super-rod-species/pokemon.json',
    'content/generated/super-rod-species/machines.json',
    'content/generated/department-machine-stock/machines.json',
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
    canonical, previous, machines, department = [dependent(p) for p in DEPENDENCIES]
    need(previous['ownedSpeciesCount'] == 163 and previous['opponentSpeciesCount'] == 192 and
         machines['speciesCount'] == 163 and machines['machineCount'] == 41 and
         department['speciesCount'] == 163 and department['machineCount'] == 42, 'Exact Super Rod and Department predecessor')
    identities = [('NINETALES',38), ('ARCANINE',59), ('POLIWRATH',62), ('CLOYSTER',91), ('EXEGGUTOR',103), ('VAPOREON',134), ('FLAREON',136)]
    definitions = []
    for name, number in identities:
        symbol = 'SPECIES_' + name
        definition = next(s for s in canonical['species'] if s['id']['value'] == 'pret:' + symbol)
        metadata = next(s for s in canonical['metadata'] if s['id']['value'] == 'pret:' + symbol)
        need(definition['sourceSpeciesId'] == metadata['nationalDexNumber'] == number and
             not definition['evolutions'] and not metadata['sourceEvolutions'], 'Terminal source: ' + symbol)
        definitions.append(dict(definition=definition, metadata=metadata))
    inputs, raw = {}, {}
    def read(path):
        if path not in raw:
            data = subprocess.check_output(['git', 'show', COMMIT + ':' + path], cwd=reference)
            shared = 'content/raw/pokefirered/037335f4/' + path
            need((dependency / shared).read_bytes() == data, 'Shared source differs: ' + path)
            inputs[path] = dict(path=path, localPath=shared, shared=True)
            raw[path] = data
        return raw[path].decode()
    for row in definitions:
        definition, metadata = row['definition'], row['metadata']
        for sr in [definition['source'], definition['learnsetSource'], metadata['learnsetPointerSource'], metadata['dexSource']] + [m['source'] for m in definition['levelUpMoves']]:
            read(sr['path'])
    tm = 'src/data/pokemon/tmhm_learnsets.h'
    text = read(tm)
    machine_ids = sorted([r['item']['value'][5:] for r in machines['additions']] + ['ITEM_TM15'])
    need(len(machine_ids) == len(set(machine_ids)) == 42, 'Exactly 42 inherited machines')
    cells = []
    for item in machine_ids:
        compatibility = []
        for name, number in identities:
            symbol = 'SPECIES_' + name
            matches = list(re.finditer(r'^    \[' + symbol + r'\]\s*=\s*TMHM_LEARNSET\((.*?)\),', text, re.M | re.S))
            need(len(matches) == 1, 'Exact machine row: ' + symbol)
            row = matches[0]
            source = dict(path=tm, symbol=symbol, startLine=text.count('\n', 0, row.start()) + 1,
                          endLine=text.count('\n', 0, row.end()) + 1)
            compatibility.append(dict(species=dict(value='pret:' + symbol), sourceSpeciesId=number,
                canLearn=bool(re.search(r'TMHM\(' + item[5:] + r'_', row[1])), source=source))
        cells.append(dict(item=dict(value='pret:' + item), compatibility=compatibility))
    docs = {
        'pokemon.json': dict(sourceCommit=COMMIT,  profile='firered-stone-evolution-owned-pokemon-v1',

            previousOwnedSpeciesCount=163, ownedSpeciesCount=170, previousOpponentSpeciesCount=192, opponentSpeciesCount=195,
            ownedSpeciesAdded=['SPECIES_' + name for name, _ in identities], opponentSpeciesAdded=['SPECIES_POLIWRATH', 'SPECIES_VAPOREON', 'SPECIES_FLAREON'], trainerOnlySpeciesAdded=[], evolutions=[],
            movesAdded=[]),
        'machines.json': dict(sourceCommit=COMMIT, profile='firered-stone-evolution-teaching-v1', previousSpeciesCount=163,
            speciesCount=170, machineCount=42, additions=cells,  itemsAdded=[]),
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
    lock = dict(sourceCommit=COMMIT,  profile='firered-stone-evolution-species-v1',

        files=[dict(path=p) for p, data in sorted(files.items())])
    files['scripts/stone-evolution-species.lock.json'] = encode(lock)
    for name, data in files.items():
        path = args.output / name
        if args.check: need(path.read_bytes() == data, 'Regeneration differs: ' + name)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    print(json.dumps(dict(checked=args.check, owned=170, opponents=195, movesAdded=0, newMachineCells=294), indent=2))


if __name__ == '__main__': main()

#!/usr/bin/env python3
"""Authenticate the eight breeding-family species and 51 existing machine rows.

Reads pinned local Git blobs and authenticated shared source resources. No upstream code runs.
All 174 predecessor species and all 354 moves are inherited unchanged.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE = 'content/generated/breeding-species/'
DEPENDENCIES = {
    'content/generated/canonical-pokemon/catalog.json': 'c5ef32d4b425c9ce720b0b29305d5855e6c7f5eff8ff05cc36d2cb51584eb9bc',
    'content/generated/game-corner-species/pokemon.json': '6eaabfb58b19e2513f78fbb1f326cbc09f85011db33b26a650c90566901fa9c8',
    'content/generated/game-corner-prizes/machines.json': 'c30c6f9ef42bef171aa4b618438c4646c32a99816ccd2893f4d12fcde118e2bf',
    'content/generated/field-fly/hm02.json': '3c5f91a9fd351f8e8881bcbebae1de1a6b4368f6ce39ea97a8c3afa0cc6b3968',
}
SHARED_INPUTS = {'include/constants/pokemon.h': 'content/raw/bill-pokemon/include/constants/pokemon.h', 'src/data/party_menu.h': 'content/raw/bill-machines/src/data/party_menu.h', 'src/data/pokemon/evolution.h': 'content/raw/bill-pokemon/src/data/pokemon/evolution.h', 'src/data/pokemon/level_up_learnset_pointers.h': 'content/raw/bill-pokemon/src/data/pokemon/level_up_learnset_pointers.h', 'src/data/pokemon/level_up_learnsets.h': 'content/raw/bill-pokemon/src/data/pokemon/level_up_learnsets.h', 'src/data/pokemon/pokedex_entries.h': 'content/raw/bill-pokemon/src/data/pokemon/pokedex_entries.h', 'src/data/pokemon/species_info.h': 'content/raw/bill-pokemon/src/data/pokemon/species_info.h', 'src/data/pokemon/tmhm_learnsets.h': 'content/raw/bill-machines/src/data/pokemon/tmhm_learnsets.h', 'src/daycare.c': 'content/raw/route5-daycare/src/daycare.c', 'src/pokemon.c': 'content/raw/bill-items/src/pokemon.c'}
IDENTITIES = [('PICHU',172), ('CLEFFA',173), ('IGGLYBUFF',174), ('TYROGUE',236), ('SMOOCHUM',238), ('ELEKID',239), ('WYNAUT',360), ('HITMONTOP',237)]



def sha(data): return hashlib.sha256(data).hexdigest()
def fingerprint(value): return sha(json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False).encode())
def encode(value): return (json.dumps(value, indent=2, ensure_ascii=False) + '\n').encode()
def need(value, message):
    if not value: raise ValueError(message)


def generate(reference, dependency):
    need(subprocess.check_output(['git', 'rev-parse', COMMIT + '^{tree}'], cwd=reference).decode().strip() == TREE, 'Pinned source tree')
    dependencies = []
    def dependent(path):
        data = (dependency / path).read_bytes()
        need(sha(data) == DEPENDENCIES[path], 'Dependency changed: ' + path)
        dependencies.append(dict(path=path, byteLength=len(data), sha256=sha(data)))
        return json.loads(data)
    canonical, previous, machines, fly = [dependent(p) for p in DEPENDENCIES]
    need(previous['ownedSpeciesCount'] == 174 and previous['opponentSpeciesCount'] == 196 and machines['machineCount'] == 50, 'Exact predecessor')
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
            data = subprocess.check_output(['git', 'show', COMMIT + ':' + path], cwd=reference)
            shared = SHARED_INPUTS.get(path)
            if shared: need((dependency / shared).read_bytes() == data, 'Shared source differs: ' + path)
            local = shared or 'content/raw/breeding-species/' + path
            inputs[path] = dict(path=path, localPath=local, shared=shared is not None, byteLength=len(data),
                gitBlobSha1=hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest(), sha256=sha(data))
            raw[path] = data
        return raw[path].decode()
    for row in definitions:
        definition, metadata = row['definition'], row['metadata']
        for sr in [definition['source'], definition['learnsetSource'], metadata['learnsetPointerSource'], metadata['dexSource']] + [m['source'] for m in definition['levelUpMoves']] + [e['source'] for e in metadata['sourceEvolutions']]:
            read(sr['path'])
            need(inputs[sr['path']]['sha256'] == sr['sha256'], 'Canonical source identity')
    tm = 'src/data/pokemon/tmhm_learnsets.h'
    text = read(tm)
    machine_ids = sorted([r['item']['value'][5:] for r in machines['inherited']] + [r['machine']['item']['id']['value'][5:] for r in machines['additions']] + ['ITEM_HM02'])
    need(len(machine_ids) == len(set(machine_ids)) == 51, 'Exactly 51 inherited machines')
    cells = []
    for item in machine_ids:
        compatibility = []
        for name, number in identities:
            symbol = 'SPECIES_' + name
            matches = list(re.finditer(r'^    \[' + symbol + r'\]\s*=\s*TMHM_LEARNSET\((.*?)\),', text, re.M | re.S))
            need(len(matches) == 1, 'Exact machine row: ' + symbol)
            row = matches[0]
            source = dict(path=tm, symbol=symbol, startLine=text.count('\n', 0, row.start()) + 1,
                          endLine=text.count('\n', 0, row.end()) + 1, sha256=inputs[tm]['sha256'])
            compatibility.append(dict(species=dict(value='pret:' + symbol), sourceSpeciesId=number,
                canLearn=bool(re.search(r'TMHM\(' + item[5:] + r'_', row[1])), source=source))
        cells.append(dict(item=dict(value='pret:' + item), compatibility=compatibility))
    old_owned = [r['species']['value'] for r in machines['additions'][0]['compatibility']]
    owned = old_owned + ['pret:SPECIES_' + n for n, _ in identities]
    species_by_id = {r['id']['value']: r for r in canonical['species']}
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
        expression = re.search(r'^    \['+species[5:]+r'\]\s*=\s*TMHM_LEARNSET\((.*?)\),',text,re.M|re.S)[1]
        compatible = []
        for kind, number, move in re.findall(r'TMHM\((TM|HM)(\d+)_(\w+)\)',expression):
            index = int(number)-1+(50 if kind == 'HM' else 0)
            need(all_moves[index]=='MOVE_'+move, 'Original machine index binding')
            compatible.append(dict(value='pret:MOVE_'+move))
        rows.append(dict(breeding=dict(species=dict(value=species),eggGroup1=groups[original['eggGroups'].split(',')[0].strip().strip('{} ').strip()],eggGroup2=groups[original['eggGroups'].split(',')[1].strip().strip('{} ').strip()],eggCycles=int(original['eggCycles'])),
            baseEggSpecies=dict(value=base_species(species)),eggMoves=[dict(value='pret:'+m) for m in egg_moves.get(species[13:],[])],machineMoves=compatible))
    read('src/daycare.c');read('src/pokemon.c')
    evolution_rows=[dict(species=r['definition']['id'], **e) for r in definitions for e in r['metadata']['sourceEvolutions']]
    docs = {
        'pokemon.json': dict(sourceCommit=COMMIT, sourceTree=TREE, profile='firered-breeding-owned-pokemon-v1',
            canonicalCatalogSha256=dependencies[0]['sha256'], predecessorCatalogSha256=dependencies[1]['sha256'],
            previousOwnedSpeciesCount=174, ownedSpeciesCount=182, previousOpponentSpeciesCount=196, opponentSpeciesCount=203,
            opponentSpeciesAdded=['SPECIES_' + name for name, _ in identities if name != 'HITMONTOP'],
            ownedSpeciesAdded=['SPECIES_' + name for name, _ in identities], evolutions=evolution_rows,
            movesAdded=[], canonicalSpeciesFingerprint=fingerprint(definitions),
            acquisitionDependencies=[dict(species='SPECIES_WYNAUT',requires='ITEM_LAX_INCENSE',status='Original Lost Cave pickup is not yet admitted; no replacement source or item is added.')],
            excludedPreexistingGaps=['SPECIES_RHYDON','SPECIES_SLOWBRO','SPECIES_CROBAT','SPECIES_BLISSEY']),
        'machines.json': dict(sourceCommit=COMMIT, profile='firered-breeding-teaching-v1', previousSpeciesCount=174,
            speciesCount=182, machineCount=51, additions=cells, additionsSemanticSha256=fingerprint(cells), itemsAdded=[]),
        'metadata.json': dict(sourceCommit=COMMIT, profile='firered-breeding-metadata-v1', speciesCount=182,
            allMachineMoves=[dict(value='pret:'+m) for m in all_moves], species=rows,
            semanticSha256=fingerprint(dict(allMachineMoves=[dict(value='pret:'+m) for m in all_moves],species=rows))),
        'source-input-hashes.json': dict(sourceCommit=COMMIT, sourceTree=TREE,
            inputs=[inputs[p] for p in sorted(inputs)], dependencyInputs=dependencies),
    }
    docs['_raw']={inputs[p]['localPath']:data for p,data in raw.items() if not inputs[p]['shared']}

    return docs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for argument in ['reference', 'dependency', 'output']: parser.add_argument(argument, type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    docs = generate(args.reference, args.dependency)
    raw = docs.pop('_raw')
    files = {BASE + name: encode(doc) for name, doc in docs.items()}
    lock = dict(sourceCommit=COMMIT, sourceTree=TREE, profile='firered-breeding-species-v1',
        importerSha256=sha(Path(__file__).read_bytes()),
        files=[dict(path=p, byteLength=len(data), sha256=sha(data)) for p, data in sorted(files.items())])
    files['scripts/breeding-species.lock.json'] = encode(lock)
    for name, data in {**files, **raw}.items():
        path = args.output / name
        if args.check: need(path.read_bytes() == data, 'Regeneration differs: ' + name)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    print(json.dumps(dict(checked=args.check, owned=182, movesAdded=0, newMachineCells=408,
        machineFingerprint=docs['machines.json']['additionsSemanticSha256'], hashes={p: sha(b) for p, b in files.items()}), indent=2))


if __name__ == '__main__': main()

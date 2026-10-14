#!/usr/bin/env python3
"""Authenticate the single Qwilfish ownership extension and 41 existing machine cells.

Reads pinned local Git blobs and authenticated shared source resources. No upstream code runs.
All other Super Rod slot species and all 354 moves are inherited unchanged.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE = 'content/generated/super-rod-species/'
DEPENDENCIES = {
    'content/generated/canonical-pokemon/catalog.json': 'c5ef32d4b425c9ce720b0b29305d5855e6c7f5eff8ff05cc36d2cb51584eb9bc',
    'content/generated/seafoam-rules/pokemon.json': '7713707128f4c5d19883db77ef995938b798a848c1cee7ebf43750aaeb04af4d',
    'content/generated/seafoam-rules/machines.json': 'b4d08d47e03b25c5eda92f0ab53192f2dec08c3c47554be7a09f96eb23e752c0',
}


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
            shared = 'content/raw/fossil-lab-rules/' + path
            need((dependency / shared).read_bytes() == data, 'Shared source differs: ' + path)
            inputs[path] = dict(path=path, localPath=shared, shared=True, byteLength=len(data),
                gitBlobSha1=hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest(), sha256=sha(data))
            raw[path] = data
        return raw[path].decode()
    for sr in [definition['source'], definition['learnsetSource'], metadata['learnsetPointerSource'], metadata['dexSource']] + [m['source'] for m in definition['levelUpMoves']]:
        read(sr['path'])
        need(inputs[sr['path']]['sha256'] == sr['sha256'], 'Canonical source identity')
    tm = 'src/data/pokemon/tmhm_learnsets.h'
    text = read(tm)
    matches = list(re.finditer(r'^    \[SPECIES_QWILFISH\]\s*=\s*TMHM_LEARNSET\((.*?)\),', text, re.M | re.S))
    need(len(matches) == 1, 'Exact Qwilfish machine row')
    row = matches[0]
    source = dict(path=tm, symbol=symbol, startLine=text.count('\n', 0, row.start()) + 1,
                  endLine=text.count('\n', 0, row.end()) + 1, sha256=inputs[tm]['sha256'])
    machine_ids = sorted(r['item']['value'][5:] for r in machines['additions'])
    need(len(machine_ids) == 41, 'Exactly 41 inherited machines')
    cells = [dict(item=dict(value='pret:' + item), compatibility=[dict(species=dict(value='pret:' + symbol),
        sourceSpeciesId=211, canLearn=bool(re.search(r'TMHM\(' + item[5:] + r'_', row[1])), source=source)]) for item in machine_ids]
    docs = {
        'pokemon.json': dict(sourceCommit=COMMIT, sourceTree=TREE, profile='firered-super-rod-owned-pokemon-v1',
            canonicalCatalogSha256=dependencies[0]['sha256'], predecessorCatalogSha256=dependencies[1]['sha256'],
            previousOwnedSpeciesCount=162, ownedSpeciesCount=163, previousOpponentSpeciesCount=191, opponentSpeciesCount=192,
            ownedSpeciesAdded=[symbol], opponentSpeciesAdded=[symbol], trainerOnlySpeciesAdded=[], evolutions=[],
            movesAdded=[], canonicalSpeciesFingerprint=fingerprint([dict(definition=definition, metadata=metadata)])),
        'machines.json': dict(sourceCommit=COMMIT, profile='firered-super-rod-teaching-v1', previousSpeciesCount=162,
            speciesCount=163, machineCount=41, additions=cells, additionsSemanticSha256=fingerprint(cells), itemsAdded=[]),
        'source-input-hashes.json': dict(sourceCommit=COMMIT, sourceTree=TREE,
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
    lock = dict(sourceCommit=COMMIT, sourceTree=TREE, profile='firered-super-rod-species-v1',
        importerSha256=sha(Path(__file__).read_bytes()),
        files=[dict(path=p, byteLength=len(data), sha256=sha(data)) for p, data in sorted(files.items())])
    files['scripts/super-rod-species.lock.json'] = encode(lock)
    for name, data in files.items():
        path = args.output / name
        if args.check: need(path.read_bytes() == data, 'Regeneration differs: ' + name)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    print(json.dumps(dict(checked=args.check, owned=163, opponents=192, movesAdded=0, newMachineCells=41,
        machineFingerprint=docs['machines.json']['additionsSemanticSha256'], hashes={p: sha(b) for p, b in files.items()}), indent=2))


if __name__ == '__main__': main()

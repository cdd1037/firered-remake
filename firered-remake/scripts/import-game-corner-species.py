#!/usr/bin/env python3
"""Authenticate FireRed Game Corner's four new owned species and two level edges.

Reads only pinned local Git blobs and authenticated shared resources. No upstream
code runs, no source tree is copied, and all 195 ordinary opponents are retained.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE = 'content/generated/game-corner-species/'
DEPENDENCIES = {
    'content/generated/canonical-pokemon/catalog.json': 'c5ef32d4b425c9ce720b0b29305d5855e6c7f5eff8ff05cc36d2cb51584eb9bc',
    'content/generated/stone-evolution-species/pokemon.json': '43ce70520ebb5d4629285acaea21f4acec81ea9c59885876dc8400efdc97c490',
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
    canonical, previous = [dependent(path) for path in DEPENDENCIES]
    need(previous['ownedSpeciesCount'] == 170 and previous['opponentSpeciesCount'] == 195, 'Exact Stone Evolution predecessor')
    identities = [('DRATINI', 147), ('DRAGONAIR', 148), ('DRAGONITE', 149), ('PORYGON', 137)]
    definitions, evolutions, excluded = [], [], []
    inputs = {}
    def read(source):
        path = source['path']
        if path not in inputs:
            data = subprocess.check_output(['git', 'show', COMMIT + ':' + path], cwd=reference)
            shared = 'content/raw/fossil-lab-rules/' + path
            need((dependency / shared).read_bytes() == data, 'Shared source differs: ' + path)
            inputs[path] = dict(path=path, localPath=shared, shared=True, byteLength=len(data),
                gitBlobSha1=hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest(), sha256=sha(data))
        need(inputs[path]['sha256'] == source['sha256'], 'Canonical source identity: ' + path)
    for name, number in identities:
        symbol = 'SPECIES_' + name
        definition = next(s for s in canonical['species'] if s['id']['value'] == 'pret:' + symbol)
        metadata = next(s for s in canonical['metadata'] if s['id']['value'] == 'pret:' + symbol)
        need(definition['sourceSpeciesId'] == metadata['nationalDexNumber'] == number and not definition['evolutions'], 'Canonical species identity: ' + symbol)
        raw = metadata['sourceEvolutions']
        if name in ('DRATINI', 'DRAGONAIR'):
            level, target = (30, 'DRAGONAIR') if name == 'DRATINI' else (55, 'DRAGONITE')
            need(len(raw) == 1 and raw[0]['method'] == 'EVO_LEVEL' and raw[0]['sourceMethodId'] == 4 and
                 raw[0]['parameter'] == str(level) and raw[0]['parameterValue'] == level and
                 raw[0]['target']['value'] == 'pret:SPECIES_' + target, 'Exact source level edge: ' + symbol)
            evolutions.append(dict(species=definition['id'], level=level, target=raw[0]['target'], source=raw[0]['source']))
        elif name == 'PORYGON':
            need(len(raw) == 1 and raw[0]['method'] == 'EVO_TRADE_ITEM' and raw[0]['sourceMethodId'] == 6 and
                 raw[0]['parameter'] == 'ITEM_UP_GRADE' and raw[0]['parameterValue'] == 218 and
                 raw[0]['target']['value'] == 'pret:SPECIES_PORYGON2', 'Exact excluded trade-only edge')
            excluded.append(dict(species=definition['id'], **raw[0]))
        else:
            need(not raw, 'Terminal Dragonite source')
        for source in [definition['source'], definition['learnsetSource'], metadata['learnsetPointerSource'], metadata['dexSource']] + [m['source'] for m in definition['levelUpMoves']] + [e['source'] for e in raw]:
            read(source)
        definitions.append(dict(definition=definition, metadata=metadata))
    return {
        'pokemon.json': dict(sourceCommit=COMMIT, sourceTree=TREE, profile='firered-game-corner-owned-pokemon-v1',
            canonicalCatalogSha256=dependencies[0]['sha256'], predecessorCatalogSha256=dependencies[1]['sha256'],
            previousOwnedSpeciesCount=170, ownedSpeciesCount=174, previousOpponentSpeciesCount=195, opponentSpeciesCount=196,
            ownedSpeciesAdded=['SPECIES_' + name for name, _ in identities], opponentSpeciesAdded=['SPECIES_DRATINI'], trainerOnlySpeciesAdded=[],
            evolutions=evolutions, excludedEvolutions=excluded, movesAdded=[], canonicalSpeciesFingerprint=fingerprint(definitions)),
        'source-input-hashes.json': dict(sourceCommit=COMMIT, sourceTree=TREE,
            inputs=[inputs[path] for path in sorted(inputs)], dependencyInputs=dependencies),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for argument in ['reference', 'dependency', 'output']: parser.add_argument(argument, type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    docs = generate(args.reference, args.dependency)
    files = {BASE + name: encode(doc) for name, doc in docs.items()}
    lock = dict(sourceCommit=COMMIT, sourceTree=TREE, profile='firered-game-corner-species-v1',
        importerSha256=sha(Path(__file__).read_bytes()),
        files=[dict(path=path, byteLength=len(data), sha256=sha(data)) for path, data in sorted(files.items())])
    files['scripts/game-corner-species.lock.json'] = encode(lock)
    for name, data in files.items():
        path = args.output / name
        if args.check: need(path.read_bytes() == data, 'Regeneration differs: ' + name)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    print(json.dumps(dict(checked=args.check, owned=174, opponents=196, movesAdded=0, levelEdges=2,
        hashes={path: sha(data) for path, data in files.items()}), indent=2))


if __name__ == '__main__': main()

#!/usr/bin/env python3
"""Read-only source/presentation reuse audit; emits facts, never imports or wires art."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
import subprocess

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
MANIFESTS = {
    'pokemon-league-presentation/manifest.json': '211d6c3c6377699241beb9ac00645c4181d0aab08e46b96f5f02070b5fb76b1c',
    'pokemon-league-presentation/synth-manifest.json': 'fc6b8faf25cd3ec914e0667d0d01739af2cdde949f7eec4dc63dcae655c017aa',
    'five-island-presentation/manifest.json': '839ab8c43dfe6b5ff504dfb74e2ce30fc6fba761e43170970f00ce1756db0106',
    'five-island-presentation/synth-manifest.json': '3a51837796752ade545fa6d27b0b5e62d68b550248eae2e0abeea98bcc6dfe70',
    'campaign-hit-art/manifest.json': 'f65895babddf0327ad6884ba4d86d2c582e780c17bf91064599a657361acc94c',
}


def need(value, message):
    if not value: raise ValueError(message)


def sha(data): return hashlib.sha256(data).hexdigest()


def audit(reference, dependency):
    need(subprocess.check_output(['git', 'rev-parse', COMMIT + '^{tree}'], cwd=reference).decode().strip() == TREE, 'Pinned source tree')
    inputs, source_bytes = {}, {}
    def source(path):
        if path not in inputs:
            data = subprocess.check_output(['git', 'show', COMMIT + ':' + path], cwd=reference)
            inputs[path] = dict(path=path, byteLength=len(data), sha256=sha(data),
                gitBlobSha1=hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest())
            source_bytes[path] = data
        return source_bytes[path]
    manifests = {}
    for path, expected in MANIFESTS.items():
        data = (dependency / 'content/generated' / path).read_bytes()
        need(sha(data) == expected, 'Authenticated presentation manifest differs: ' + path)
        manifests[path] = json.loads(data)
    cry_table = re.findall(r'^\s*cry Cry_(\w+)\s*$', source('sound/cry_tables.inc').decode().split('gCryTable::', 1)[1].split('gCryTable_Reverse::', 1)[0], re.M)
    cry_bytes = (dependency / 'content/generated/synth-audio/normal-cries.bin').read_bytes()
    pack = (dependency / 'content/generated/synth-audio/firered.pack').read_bytes()
    need(sha(cry_bytes) == '623fe59bb7fc0b8d3907dd67bc677a7985037e0086698636f35db82e113d7f2e' and
         sha(pack) == '00b3a8376e2a44f1919d4a2573a63cb2873a19b5662df496c4f292c16ad0b46d', 'Existing complete cry data differs')
    cry_rows = {struct.unpack_from('<H', cry_bytes, pos)[0]: struct.unpack_from('<HHIIB3x', cry_bytes, pos) for pos in range(12, len(cry_bytes), 16)}
    rows = []
    for name, number in [('Dratini', 147), ('Dragonair', 148), ('Dragonite', 149), ('Porygon', 137)]:
        stem = name.lower()
        symbol = 'SPECIES_' + name.upper()
        package = None if number == 147 else 'five-island-presentation' if number == 137 else 'pokemon-league-presentation'
        for asset in ['front.png', 'back.png', 'normal.pal', 'shiny.pal', 'icon.png', 'footprint.png']:
            source('graphics/pokemon/' + stem + '/' + asset)
        existing = []
        if package:
            manifest = manifests[package + '/manifest.json']
            assets = [row for collection in ['sprites', 'pokemonIcons'] for row in manifest[collection] if row['sourceSpeciesId'] == number]
            need(len(assets) == 5, 'Four battlers and one party icon required: ' + name)
            for asset in assets:
                files = []
                for key in ['rgbaPath', 'texturePath']:
                    path = asset[key]
                    if not path.startswith('content/'): path = 'content/generated/' + package + '/' + path
                    data = (dependency / path).read_bytes()
                    relative = str(Path(path).relative_to('content/generated/' + package))
                    expected = next(row for row in manifest['outputs'] if row['path'] == relative)
                    need(len(data) == expected['byteLength'] and sha(data) == expected['sha256'], 'Existing asset bytes differ: ' + path)
                    files.append(dict(path=path, byteLength=len(data), sha256=sha(data)))
                existing.append(dict(id=asset['id'], files=files))
            for item in manifest['inputs']:
                if item['path'].startswith('graphics/pokemon/' + stem + '/'):
                    need(sha(source(item['path'])) == item['sha256'], 'Existing source image/palette differs: ' + item['path'])
        geometry = [row for row in manifests['campaign-hit-art/manifest.json']['geometry'] if row['species'] == number]
        need(len(geometry) == 2 and {r['side'] for r in geometry} == {'front', 'back'}, 'Both source geometry rows required')
        # These authenticated manifest rows are also independently checked against pinned coordinates.
        for row in geometry:
            text = source('src/data/pokemon_graphics/' + row['side'] + '_pic_coordinates.h').decode()
            match = re.search(r'\[' + symbol + r'\]\s*=\s*\{(.*?)\}', text, re.S)
            need(match is not None, 'Source geometry entry missing')
            dims = re.search(r'MON_COORDS_SIZE\((\d+),\s*(\d+)\)', match[1])
            offset = re.search(r'\.y_offset\s*=\s*(\d+)', match[1])
            need(dims is not None and offset is not None and [row['sourceWidth'], row['sourceHeight'], row['yOffset']] ==
                 [int(dims[1]), int(dims[2]), int(offset[1])], 'Source geometry values differ')
        elevation = re.search(r'\[' + symbol + r'\]\s*=\s*(\d+)', source('src/data/pokemon_graphics/enemy_mon_elevation.h').decode())
        need(next(row for row in geometry if row['side'] == 'front')['elevation'] == (int(elevation[1]) if elevation else 0), 'Source elevation differs')
        cry = cry_rows[number]
        need(cry_table[number - 1] == name and cry[1] == number - 1 and cry[2] > 0, 'Source cry identity differs')
        cue = None if not package else next(c for c in manifests[package + '/synth-manifest.json']['cues'] if c['species'] == number)
        if cue:
            need(cue['id'] == 'CRY_' + name.upper() + '_NORMAL' and cue['sourceCommit'] == COMMIT and cue['sourceSymbol'] == symbol and
                 cue['sourcePlayer'] == 4 and cue['priority'] == 10 and cue.get('cryMode', 0) == 0 and not cue['looping'], 'Existing cry cue differs')
        # Scan all current derived assets so a differently named package cannot hide a reusable footprint.
        matching = [str(p.relative_to(dependency)) for p in (dependency / 'content/generated').rglob('*') if p.is_file() and
                    (p.name.startswith(stem + '-') or p.name == stem + '.png' or p.name == stem + '.rgba')]
        need(not any('footprint' in p for p in matching), 'Audit delta changed: an existing footprint is now present')
        if number == 147: need(not matching, 'Audit delta changed: Dratini derived art is now present')
        missing = ([stem + '-' + side + '-' + variant for side in ['front', 'back'] for variant in ['normal', 'shiny']] +
                   [stem + '-icon'] if number == 147 else []) + [stem + '-footprint']
        rows.append(dict(species=symbol, sourceSpeciesId=number, inheritedAssets=existing, geometry=geometry,
            inheritedCry=dict(cryIndex=cry[1], sampleCount=cry[2], payloadBytes=cry[3], guardStored=bool(cry[4])),
            inheritedCue=cue, missingDerivedAssets=missing, missingCryCue=number == 147))
    return dict(passed=True, sourceCommit=COMMIT, sourceTree=TREE, species=rows,
        manifests=[dict(path='content/generated/' + path, sha256=value) for path, value in MANIFESTS.items()],
        sourceInputs=[inputs[path] for path in sorted(inputs)],
        missingDelta=dict(imageAssets=9, pngAndRgbaFiles=18, cryCueBindings=1, newAudioSamples=0, dexBindings=4, hitGeometryRows=0),
        scope='Static source/reuse audit only. Missing assets are identified, not generated or wired. No Godot/UI or physical audio acceptance.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for argument in ['reference', 'dependency', 'result']: parser.add_argument(argument, type=Path)
    args = parser.parse_args()
    result = audit(args.reference, args.dependency)
    args.result.parent.mkdir(parents=True, exist_ok=True)
    args.result.write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(dict(passed=True, missingDelta=result['missingDelta'], result=str(args.result))))


if __name__ == '__main__': main()

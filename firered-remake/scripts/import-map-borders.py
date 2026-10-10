#!/usr/bin/env python3
"""Convert the pinned five-map border patterns to source-phase 16px layer atlases."""
import argparse
import base64
import hashlib
import importlib.util
import json
import pathlib
import re
import struct
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
SOURCE_HASH = '8795cbf31a2b799b33428fa6f88207a6f33a46574e5a4195bb9f79018ef088d4'
INVENTORY_HASH = '2157e54e07dd6ed7bc977d6ddd7acaf843c93427cbbbf40f20fb837b58ee7d26'
LOCK_HASH = 'd065353dc19a1efb4d867ba08f5d855375bdfb2d493c4768a90c4574ee49e33e'
# These are project-owned helpers/constants, never imported upstream code.
spec = importlib.util.spec_from_file_location('pallet_project_importer', ROOT / 'scripts/import-pallet.py')
pallet = importlib.util.module_from_spec(spec)
spec.loader.exec_module(pallet)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False) + '\n'


def inventory(files):
    return sha(''.join(f'{f["path"]}\0{f["gitBlobSha1"]}\0{f["byteLength"]}\0{f["sha256"]}\0{f["sourceUrl"]}\n'
                       for f in sorted(files, key=lambda f: f['path'])).encode())


def verify_inputs(raw, rules_raw, rule_lock):
    data = rule_lock.read_bytes()
    if sha(data) != LOCK_HASH:
        raise ValueError('Map-border source lock differs from reviewed pin')
    rule_files = json.loads(data)['files']
    manifest = json.loads((raw / 'manifest.json').read_bytes())
    files = manifest['files']
    if manifest['repository'] != 'pret/pokefirered' or manifest['commit'] != COMMIT or inventory(files) != INVENTORY_HASH:
        raise ValueError('Pallet source inventory differs from reviewed pin')
    lookup = {e['path']: e for e in files}
    if len(lookup) != len(files):
        raise ValueError('Duplicate source file')
    full = sha(''.join(p + '\0' + lookup[p]['sha256'] + '\n' for p in sorted(lookup)).encode())
    if full != SOURCE_HASH or manifest['fullSourceHash'] != SOURCE_HASH:
        raise ValueError('Pallet source set hash differs')
    def verify(data, e):
        blob = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
        if len(data) != e['byteLength'] or blob != e['gitBlobSha1'] or sha(data) != e['sha256']:
            raise ValueError('Source hash mismatch: ' + e['path'])
    for e in files:
        verify((raw / e['path']).read_bytes(), e)
    for e in rule_files:
        if e != lookup[e['path']]:
            raise ValueError('Rule source differs from existing pinned map source')
        verify((rules_raw / e['path']).read_bytes(), e)
    return manifest, [e['path'] for e in rule_files]


def pattern_coordinate(map_coordinate, period):
    """Map-local coordinates. Euclidean extension of source's finite +8*period bias."""
    if period <= 0:
        raise ValueError('Border period must be positive')
    return map_coordinate % period


class BorderRenderer:
    """Same project-owned pixel rules as import-pallet.py, restricted to border metatiles."""
    def __init__(self, raw, layout):
        self.raw = raw
        self.bases = ['data/tilesets/' + pallet.TILESETS[layout[k]] for k in ('primary_tileset', 'secondary_tileset')]
        self.attributes = [self.units(b + '/metatile_attributes.bin', 'I') for b in self.bases]
        self.metatiles = [self.units(b + '/metatiles.bin', 'H') for b in self.bases]
        if len(self.attributes[0]) != 640 or any(len(m) != len(a) * 8 for m, a in zip(self.metatiles, self.attributes)):
            raise ValueError('Invalid tileset sizes')
        self.graphics = [Image.open(raw / (b + '/tiles.png')) for b in self.bases]
        if any(i.mode != 'P' or i.width % 8 or i.height % 8 or i.getextrema()[1] > 15 for i in self.graphics):
            raise ValueError('Invalid source graphics')
        rules = (raw / 'tileset_rules.mk').read_text()
        self.emitted = []
        for b, image in zip(self.bases, self.graphics):
            rule = re.search(re.escape('$(TILESETGFXDIR)/' + b.removeprefix('data/tilesets/') + '/tiles.4bpp:') + r'.*?\n\t[^\n]*?-num_tiles (\d+)', rules)
            count = int(rule.group(1)) if rule else image.width * image.height // 64
            if count > image.width * image.height // 64:
                raise ValueError('Emitted graphics exceeds PNG slots')
            self.emitted.append(count)
        self.palettes = []
        for n in range(13):
            lines = (raw / (self.bases[0 if n < 7 else 1] + f'/palettes/{n:02d}.pal')).read_text().splitlines()
            if lines[:3] != ['JASC-PAL', '0100', '16'] or len(lines) != 19:
                raise ValueError('Invalid palette')
            self.palettes.append([tuple(((int(c) >> 3) << 3) | ((int(c) >> 3) >> 2) for c in row.split()) for row in lines[3:]])
        self.palettes[0][0] = (0, 0, 0)

    def units(self, path, code):
        data = (self.raw / path).read_bytes()
        size = struct.calcsize(code)
        if len(data) % size:
            raise ValueError('Misaligned source: ' + path)
        return list(struct.unpack('<' + str(len(data) // size) + code, data))

    def tile(self, entry):
        tile_id = entry & 1023
        palette = entry >> 12
        group = 0 if tile_id < 640 else 1
        local = tile_id if group == 0 else tile_id - 640
        if palette >= 13 or local >= self.emitted[group]:
            raise ValueError('Uninitialized source tile')
        graphic = self.graphics[group]
        pixels = []
        for y in range(8):
            for x in range(8):
                sx = 7 - x if entry & 0x400 else x
                sy = 7 - y if entry & 0x800 else y
                index = graphic.getpixel(((local % (graphic.width // 8)) * 8 + sx, (local // (graphic.width // 8)) * 8 + sy))
                pixels.append((*self.palettes[palette][index], 0 if index == 0 else 255))
        image = Image.new('RGBA', (8, 8))
        image.putdata(pixels)
        return image

    def render(self, blocks, width, height):
        layers = [Image.new('RGBA', (width * 16, height * 16)) for _ in range(3)]
        cells = []
        for i, raw_block in enumerate(blocks):
            mid = raw_block & 1023
            group = 0 if mid < 640 else 1
            local = mid if group == 0 else mid - 640
            attr = self.attributes[group][local]
            entries = self.metatiles[group][local * 8:local * 8 + 8]
            layer_type = (attr >> 29) & 3
            if layer_type == 0:
                groups = [[0x3014] * 4, entries[:4], entries[4:]]
            elif layer_type == 1:
                groups = [entries[:4], entries[4:], [0] * 4]
            elif layer_type == 2:
                groups = [entries[:4], [0] * 4, entries[4:]]
            else:
                raise ValueError('Unsupported reserved metatile layer')
            x, y = i % width, i // width
            for layer, words in zip(layers, groups):
                for q, entry in enumerate(words):
                    layer.paste(self.tile(entry), (x * 16 + q % 2 * 8, y * 16 + q // 2 * 8))
            cells.append({'x': x, 'y': y, 'sourceRawBlock': raw_block, 'metatileId': mid,
                          'rawAttributes': attr, 'layerType': layer_type, 'tileEntries': entries})
        composite = Image.new('RGBA', layers[0].size, (0, 0, 0, 255))
        for layer in layers:
            composite = Image.alpha_composite(composite, layer)
        return cells, [*layers, composite]


def generate(raw, rules_raw, rule_lock, output):
    manifest, rule_paths = verify_inputs(raw, rules_raw, rule_lock)
    files = {e['path']: e for e in manifest['files']}
    layouts = json.loads((raw / 'data/layouts/layouts.json').read_bytes())['layouts']
    for name, stable_id, directory in pallet.MAPS:
        map_path = f'data/maps/{name}/map.json'
        map_source = json.loads((raw / map_path).read_bytes())
        layout = next(l for l in layouts if l.get('id') == map_source['layout'])
        renderer = BorderRenderer(raw, layout)
        blocks = renderer.units(layout['border_filepath'], 'H')
        width, height = layout['border_width'], layout['border_height']
        if width <= 0 or height <= 0 or len(blocks) != width * height:
            raise ValueError('Invalid source border dimensions')
        cells, images = renderer.render(blocks, width, height)
        out = output / directory
        out.mkdir(parents=True, exist_ok=True)
        artifacts = []
        for filename, image in zip(('bottom.png', 'middle.png', 'top.png', 'map.png'), images):
            image.save(out / filename, optimize=False, compress_level=9)
            artifacts.append({'path': filename, 'sha256': sha((out / filename).read_bytes()), 'width': image.width, 'height': image.height})
        paths = sorted([map_path, 'data/layouts/layouts.json', layout['border_filepath'], *rule_paths,
                        *[b + suffix for b in renderer.bases for suffix in ('/metatiles.bin', '/metatile_attributes.bin')]])
        embedded = [{'path': p, 'sha256': files[p]['sha256'], 'base64': base64.b64encode((raw / p).read_bytes()).decode()} for p in paths]
        def source(path, symbol):
            return {'kind': 'pret', 'path': path, 'symbol': symbol, 'repository': 'pret/pokefirered', 'commit': COMMIT, 'sha256': files[path]['sha256']}
        document = {'schemaVersion': 1, 'stableId': stable_id, 'width': layout['width'], 'height': layout['height'], 'tileSize': 16,
                    'borderWidth': width, 'borderHeight': height, 'sourceRef': source(layout['border_filepath'], map_source['layout']),
                    'ruleSourceRef': source('src/fieldmap.c', 'GetBorderBlockAt'),
                    'repeatRule': {'coordinateSpace': 'map-local-metatiles', 'engineMapOffset': 7, 'sourceBiasPeriods': 8,
                                   'phaseX': 0, 'phaseY': 0, 'negativeCoordinates': 'euclidean-remainder', 'connections': 'not-implemented'},
                    'metadata': {'repository': 'pret/pokefirered', 'commit': COMMIT, 'fullSourceHash': SOURCE_HASH,
                                 'rights': manifest['rights'], 'scope': 'Five-map static border fallback only. Connections, movement, animation and camera behavior are separate.',
                                 'renderProfile': 'Original indexed PNG tiles; RGB555 bit replication; BG3 bottom, BG2 middle, BG1 top; source origin phase.', 'files': manifest['files']},
                    'cells': cells, 'artifacts': artifacts, 'embeddedSource': embedded}
        (out / 'border.json').write_text(canonical(document), encoding='utf-8', newline='\n')
        print(f'{stable_id}: {width}x{height} source border, map-local phase (0,0), {len(artifacts)} atlases')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pallet')
    parser.add_argument('--rules-raw', type=pathlib.Path, default=ROOT / 'content/raw/map-borders')
    parser.add_argument('--rules-lock', type=pathlib.Path, default=ROOT / 'scripts/map-borders-source.lock.json')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/generated/map-borders')
    args = parser.parse_args()
    generate(args.raw, args.rules_raw, args.rules_lock, args.output)


if __name__ == '__main__':
    main()

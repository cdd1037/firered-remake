#!/usr/bin/env python3
"""Import only the two original Mart OnLoad counter metatiles from locked local Git objects."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT


import argparse
import io
import json
import pathlib
import re
import shutil
import struct
import subprocess
import tempfile

from PIL import Image

ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-mart-metatiles-v1'
METATILES = [(0x2bf, 'METATILE_Mart_CounterMid_Top', 1, 3),
             (0x2c0, 'METATILE_Mart_CounterMid_Bottom', 1, 4)]


def canonical(value):
    return (json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False) + '\n').encode()


def safe(root, path):
    if not path or pathlib.PurePosixPath(path).is_absolute() or '\\' in path or any(p in ('', '.', '..') for p in path.split('/')):
        raise ValueError('Unsafe path: ' + path)
    target = root / path
    if any(p.is_symlink() for p in [target, *target.parents]):
        raise ValueError('Symlink input/output: ' + str(target))
    return target


def read_lock(path=None):
    data = (path or ROOT / 'scripts/recipes/items/mart-metatiles.lock.json').read_bytes()
    lock = json.loads(data)
    if (lock['commit'], lock['profile'], lock['metatileIds']) != (COMMIT, PROFILE, [703, 704]):
        raise ValueError('Mart source identity differs')
    return lock


def read_sources(raw, reference_repo, lock):
    def git(*args):
        return subprocess.check_output(['git', '-C', str(reference_repo), *args])
    tree = None
    if reference_repo:
        tree = {}
        for row in git('ls-tree', '-rz', COMMIT).split(b'\0'):
            if row:
                fields, name = row.split(b'\t')
                tree[name.decode()] = tuple(fields.decode().split())
    result = {}
    for entry in lock['files']:
        path = entry['path']
        target = safe(raw, path)
        if tree is not None:
            data = git('show', COMMIT + ':' + path)
        else:
            data = target.read_bytes()
        result[path] = data
    return result


def render(data, lock, output):
    def text(path):
        return data[path].decode()

    def units(path, code):
        raw = data[path]
        size = struct.calcsize(code)
        if len(raw) % size:
            raise ValueError('Misaligned source: ' + path)
        return struct.unpack('<' + str(len(raw) // size) + code, raw)

    entries = {e['path']: e for e in lock['files']}

    def source(path, symbol):
        return dict(kind='pret', repository='pret/pokefirered', commit=COMMIT, path=path,
                    symbol=symbol)

    bases = ['data/tilesets/primary/building', 'data/tilesets/secondary/mart']
    map_data = json.loads(data['data/maps/ViridianCity_Mart/map.json'])
    layout = next(x for x in json.loads(data['data/layouts/layouts.json'])['layouts'] if x.get('id') == map_data['layout'])
    if (layout['primary_tileset'], layout['secondary_tileset'], layout['blockdata_filepath'], layout['width'], layout['height']) != ('gTileset_Building', 'gTileset_Mart', 'data/layouts/Mart/map.bin', 11, 9):
        raise ValueError('Mart tileset/layout binding differs')
    declarations = text('src/data/tilesets/graphics.h') + text('src/graphics.c')
    for symbol, base in zip(('gTileset_Building', 'gTileset_Mart'), bases):
        body = re.search(r'const struct Tileset ' + symbol + r' =\s*\{(.*?)\n\};', text('src/data/tilesets/headers.h'), re.S)[1]
        graphic = re.search(r'\.tiles = (\w+)', body)[1]
        if re.search(r'\b' + graphic + r'\[\] = INCBIN_U32\("(.*?)"\)', declarations)[1] != base + '/tiles.4bpp.lz':
            raise ValueError('Tileset graphics binding differs')
    attributes = [units(b + '/metatile_attributes.bin', 'I') for b in bases]
    metatiles = [units(b + '/metatiles.bin', 'H') for b in bases]
    if len(attributes[0]) != 640 or any(len(m) != len(a) * 8 for m, a in zip(metatiles, attributes)):
        raise ValueError('Tileset dimensions differ')
    graphics = [Image.open(io.BytesIO(data[b + '/tiles.png'])) for b in bases]
    if any(i.mode != 'P' or i.width % 8 or i.height % 8 or i.getextrema()[1] > 15 for i in graphics):
        raise ValueError('Expected indexed four-bit graphics')
    emitted = []
    for base, image in zip(bases, graphics):
        rule = re.search(re.escape('$(TILESETGFXDIR)/' + base.removeprefix('data/tilesets/') + '/tiles.4bpp:') + r'.*?\n\t[^\n]*?-num_tiles (\d+)', text('tileset_rules.mk'))
        count = int(rule[1]) if rule else image.width * image.height // 64
        if count > image.width * image.height // 64:
            raise ValueError('Tileset emission exceeds image')
        emitted.append(count)
    palettes = []
    for index in range(13):
        lines = text(bases[0 if index < 7 else 1] + f'/palettes/{index:02d}.pal').splitlines()
        if lines[:3] != ['JASC-PAL', '0100', '16'] or len(lines) != 19:
            raise ValueError('Invalid source palette')
        colors = [tuple(((int(v) >> 3) << 3) | ((int(v) >> 3) >> 2) for v in row.split()) for row in lines[3:]]
        if any(len(c) != 3 or any(v < 0 or v > 255 for v in c) for c in colors):
            raise ValueError('Invalid palette RGB')
        palettes.append(colors)
    palettes[0][0] = (0, 0, 0)

    # Same tile selection, flip bits, RGB555 expansion and BG mapping as import-pallet/viridian-region.
    def tile(entry):
        tile_id, palette = entry & 1023, entry >> 12
        index, local = (0, tile_id) if tile_id < 640 else (1, tile_id - 640)
        if palette >= 13 or local >= emitted[index]:
            raise ValueError('Uninitialized tile/palette reference')
        image = graphics[index]
        pixels = []
        for y in range(8):
            for x in range(8):
                ix, iy = (7 - x if entry & 0x400 else x), (7 - y if entry & 0x800 else y)
                color = image.getpixel(((local % (image.width // 8)) * 8 + ix, (local // (image.width // 8)) * 8 + iy))
                pixels.append((*palettes[palette][color], 0 if color == 0 else 255))
        result = Image.new('RGBA', (8, 8))
        result.putdata(pixels)
        return result

    behavior_names = {int(v, 16): n for n, v in re.findall(r'^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)', text('include/constants/metatile_behaviors.h'), re.M)}
    blocks = units(layout['blockdata_filepath'], 'H')
    definitions, patches = [], []
    for mid, symbol, x, y in METATILES:
        if not re.search(r'^#define\s+' + symbol + r'\s+0x' + f'{mid:03X}' + r'\b', text('include/constants/metatile_labels.h'), re.M | re.I):
            raise ValueError('Metatile symbol differs')
        if f'setmetatile {x}, {y}, {symbol}, 1' not in text('data/maps/ViridianCity_Mart/scripts.inc'):
            raise ValueError('Original OnLoad patch differs')
        local = mid - 640
        attr = attributes[1][local]
        values = metatiles[1][local * 8:local * 8 + 8]
        layer_type = (attr >> 29) & 3
        if layer_type == 0:
            groups = [[0x3014] * 4, values[:4], values[4:]]
        elif layer_type == 1:
            groups = [values[:4], values[4:], [0] * 4]
        elif layer_type == 2:
            groups = [values[:4], [0] * 4, values[4:]]
        else:
            raise ValueError('Reserved layer type')
        textures = {}
        for name, group in zip(('bottom', 'middle', 'top'), groups):
            image = Image.new('RGBA', (16, 16))
            for k, entry in enumerate(group):
                image.paste(tile(entry), ((k % 2) * 8, (k // 2) * 8))
            filename = f'{mid:03x}-{name}.png'
            image.save(output / filename, optimize=False, compress_level=9)
            textures[name + 'TexturePath'] = filename
        definitions.append(dict(rawMetatileId=mid, symbol=symbol, rawAttributes=attr,
                                behavior=attr & 511, behaviorName=behavior_names[attr & 511],
                                terrain=(attr >> 9) & 31, attribute2=(attr >> 14) & 15,
                                attribute3=(attr >> 18) & 63, encounterType=(attr >> 24) & 7,
                                attribute5=(attr >> 27) & 3, layerType=layer_type, attribute7=attr >> 31,
                                tileEntries=list(values), **textures,
                                sourceRef=source(bases[1] + '/metatile_attributes.bin', f'metatile_{local:03x}')))
        raw_block = blocks[y * layout['width'] + x]
        patches.append(dict(x=x, y=y, rawMetatileId=mid, isImpassable=True, collision=3,
                            sourceRawBlock=raw_block, sourceElevation=raw_block >> 12,
                            resultingRawBlock=(raw_block & 0xf000) | mid | 0xc00))
    result = dict(schemaVersion=1, repository='pret/pokefirered',
                   tileSize=16, mapId='map.viridian-mart',
                  metatiles=definitions, onLoadPatches=patches, preserveSourceElevation=True,
                  onLoadProgram='ViridianCity_Mart_EventScript_HideQuestionnaire')
    (output / 'metatiles.json').write_bytes(canonical(result))
    outputs = [dict(path=p.name)
               for p in sorted(output.iterdir())]
    manifest = dict(schemaVersion=1,    inputCount=len(lock['files']), outputs=outputs)
    (output / 'manifest.json').write_bytes(canonical(manifest))
    return manifest


def run(raw, output, reference_repo=None, lock_path=None):
    raw, output = pathlib.Path(raw), pathlib.Path(output)
    safe(output, 'manifest.json')
    if raw.resolve().is_relative_to(output.resolve()) or output.resolve().is_relative_to(raw.resolve()):
        raise ValueError('Raw and output directories overlap')
    lock = read_lock(lock_path)
    data = read_sources(raw, reference_repo, lock)
    # Finish validation and rendering before replacing any existing generated data.
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.mart-metatiles-', dir=output.parent) as temporary:
        staging = pathlib.Path(temporary)
        manifest = render(data, lock, staging)
        for path, value in data.items():
            target = safe(raw, path)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(value)
        if output.exists():
            # Only this dedicated two-metatile output directory is replaced.
            for existing in output.rglob('*'):
                safe(output, existing.relative_to(output).as_posix())
            shutil.rmtree(output)
        shutil.copytree(staging, output)
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo', type=pathlib.Path, help='Read the fixed commit with git show, never the reference worktree.')
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/generated/maps/objects/mart-metatiles')
    args = parser.parse_args()
    manifest = run(args.raw, args.output, args.reference_repo)
    print(json.dumps(dict(profile=PROFILE, inputs=manifest['inputCount'], outputs=len(manifest['outputs'])), sort_keys=True))

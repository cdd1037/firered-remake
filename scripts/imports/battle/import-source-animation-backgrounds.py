#!/usr/bin/env python3
"""Convert source BG3 writes, retaining indexed tiles and screenblock order.

Reads the pinned local source tree (or exact Git objects). Never runs source code,
fetches a ROM, or publishes graphics. Output is one deduplicated atlas and its index.
"""
import argparse
import io
import json
from pathlib import Path
import re
import struct
import subprocess
from PIL import Image

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
MOVE_BACKGROUNDS = ('BG_NONE', 'BG_DARK', 'BG_PSYCHIC')


def read(reference, path):
    return (subprocess.check_output(['git', '-C', str(reference), 'show', COMMIT + ':' + path])
            if (reference / '.git').exists() else (reference / path).read_bytes())


def palette(data):
    rows = data.decode().splitlines()
    if rows[:2] != ['JASC-PAL', '0100']:
        raise ValueError('Source palette header differs')
    values = [tuple(map(int, row.split())) for row in rows[3:]]
    if len(values) != int(rows[2]) or any(len(row) != 3 or any(c < 0 or c > 255 for c in row) for row in values):
        raise ValueError('Source palette shape differs')
    return struct.pack('<' + 'H' * len(values), *(sum((c // 8) << (i * 5) for i, c in enumerate(row)) for row in values))


def indexed_tiles(data, count=None):
    with Image.open(io.BytesIO(data)) as image:
        image.load()
        if image.mode != 'P' or image.width % 8 or image.height % 8:
            raise ValueError('Source tiles must be indexed 8x8 multiples')
        # gbagfx packs each 8x8 tile in row order, with the left index in the low nibble.
        tiles = [image.crop((x, y, x + 8, y + 8)).tobytes()
                 for y in range(0, image.height, 8) for x in range(0, image.width, 8)]
        if count is not None:
            if count < 1 or count > len(tiles):
                raise ValueError('Source num_tiles exceeds PNG')
            tiles = tiles[:count]
        indices = b''.join(tiles)
        if any(x > 15 for x in indices):
            raise ValueError('Source index exceeds 4bpp')
        colors = image.getpalette()[:48]
        pal = struct.pack('<16H', *(sum((colors[j * 3 + c] // 8) << (5 * c) for c in range(3)) for j in range(16)))
    return bytes(indices[i] | indices[i + 1] << 4 for i in range(0, len(indices), 2)), pal


def build(reference):
    bg = read(reference, 'src/battle_bg.c').decode()
    anim = read(reference, 'src/battle_anim.c').decode()
    main = read(reference, 'src/battle_main.c').decode()
    rules = read(reference, 'graphics_file_rules.mk').decode()
    # These are conversion assumptions, not runtime provenance or count gates.
    for text, needle in [(bg, '.bg = 3,\n        .charBaseIndex = 2,\n        .mapBaseIndex = 26,\n        .screenSize = 1'),
                         (main, 'CpuFill32(0, (void *)(VRAM), VRAM_SIZE);'),
                         (anim, 'LZDecompressVram(gBattleAnimBackgroundTable[bgId].tilemap, (void *)(BG_SCREEN_ADDR(26)));'),
                         (anim, 'LoadCompressedPalette(gBattleAnimBackgroundTable[bgId].palette, BG_PLTT_ID(2), PLTT_SIZE_4BPP);'),
                         (bg, 'LoadCompressedPalette(sBattleTerrainTable[terrain].palette, BG_PLTT_ID(2), 3 * PLTT_SIZE_4BPP);')]:
        if needle not in text:
            raise ValueError('Source BG3 initialization/load semantics differ')
    declarations = dict(re.findall(r'(\w+)\[\] = INCBIN_U32\("([^"]+)"\)', bg + '\n' + read(reference, 'src/graphics.c').decode()))
    numbers = dict((name, int(value)) for name, value in re.findall(r'^#define\s+(BG_\w+|BATTLE_TERRAIN_\w+)\s+(\d+)',
        read(reference, 'include/constants/battle.h').decode() + '\n' + read(reference, 'include/constants/battle_anim.h').decode(), re.M))
    atlas = bytearray()
    blocks = {}
    def add(data):
        if data not in blocks:
            blocks[data] = [len(atlas), len(data)]
            atlas.extend(data)
        return blocks[data]
    def resource(id, image_symbol, map_symbol, palette_symbol, terrain):
        png = declarations[image_symbol].replace('.4bpp.lz', '.png')
        rule_path = png.replace('graphics/battle_terrain', '$(BATTLETERRAINGFXDIR)').replace('.png', '.4bpp')
        match = re.search(re.escape(rule_path) + r':[^\n]*\n\s*\$\(GFX\)[^\n]*-num_tiles\s+(\d+)', rules)
        tiles, png_palette = indexed_tiles(read(reference, png), int(match[1]) if match else None)
        tilemap = read(reference, declarations[map_symbol].removesuffix('.lz'))
        if len(tilemap) < 2 or len(tilemap) > 4096 or len(tilemap) % 2 or terrain and len(tilemap) != 4096:
            raise ValueError('Source BG3 tilemap size differs')
        pal_path = declarations[palette_symbol].replace('.gbapal.lz', '.pal')
        # Move .gbapal is made from its indexed PNG; terrain palettes are explicit JASC files.
        pal = palette(read(reference, pal_path)) if terrain else indexed_tiles(read(reference, declarations[palette_symbol].replace('.gbapal.lz', '.png')))[1]
        if len(pal) != (96 if terrain else 32):
            raise ValueError('Source BG3 palette ownership differs')
        for entry in struct.unpack('<' + 'H' * (len(tilemap) // 2), tilemap):
            tile, bank = entry & 1023, entry >> 12
            if tile * 32 >= len(tiles) or bank not in ((0, 2, 3, 4) if terrain else (0, 2)):
                raise ValueError('Source tilemap reference exceeds imported graphics')
            if terrain and bank == 0 and any(tiles[tile * 32:(tile + 1) * 32]):
                raise ValueError('Unexpected nonblank source textbox-bank tile')
        return dict(id=id, tiles=add(tiles), tilemap=add(tilemap), palette=add(pal))
    terrain_table = re.search(r'sBattleTerrainTable\[\]\s*=\s*\{(.*?)\n\};', bg, re.S)[1]
    terrains = []
    for name, body in re.findall(r'\[(BATTLE_TERRAIN_\w+)\]\s*=\s*\{(.*?)\}', terrain_table, re.S):
        fields = dict(re.findall(r'\.(tileset|tilemap|palette)\s*=\s*(\w+)', body))
        terrains.append(resource(numbers[name], fields['tileset'], fields['tilemap'], fields['palette'], True))
    if not terrains or len({r['id'] for r in terrains}) != len(terrains):
        raise ValueError('Missing or duplicate source terrain')
    table = read(reference, 'src/data/battle_anim.h').decode().split('gBattleAnimBackgroundTable[] =', 1)[1]
    moves = []
    for name in MOVE_BACKGROUNDS:
        match = re.search(r'\[' + name + r'\]\s*=\s*\{(\w+),\s*(\w+),\s*(\w+)\}', table)
        if match is None:
            raise ValueError('Missing source move background ' + name)
        moves.append(resource(numbers[name], match[1], match[3], match[2], False))
    base_palette = b''.join(palette(read(reference, 'graphics/battle_interface/textbox' + str(i) + '.pal')) for i in (1, 2))
    if len(base_palette) != 64:
        raise ValueError('Source textbox palette ownership differs')
    manifest = dict(schemaVersion=1, basePalette=add(base_palette), terrains=terrains, moves=moves)
    return {'manifest.json': (json.dumps(manifest, separators=(',', ':')) + '\n').encode(), 'atlas.bin': bytes(atlas)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('reference', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    files = build(args.reference)
    if args.output.exists():
        raise SystemExit('Refuse existing output directory')
    args.output.mkdir(parents=True)
    for name, data in files.items():
        (args.output / name).write_bytes(data)
    print(json.dumps({'passed': True, 'files': len(files), 'bytes': sum(map(len, files.values()))}))


if __name__ == '__main__':
    main()

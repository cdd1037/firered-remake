"""Compile all region preview art from the shared original registry.

PNG indices are reduced to 4bpp exactly as gbagfx ConvertBitDepth does, then
resolved through each tilemap entry's palette bank. No source paths or recipes
enter the runtime catalog. The source's unused third palette bank is not read.
"""
import base64
import io
import re
from PIL import Image
from field_cave_art import preview_registry, rgb555, uncomment
from map_runtime import encode


def compile_art(src, row, declarations):
    bindings = [declarations.get(row[key]) for key in ('tilesptr', 'tilemapptr', 'palptr')]
    tiles, tilemap, palette = bindings
    if any(value is None for value in bindings) or not re.fullmatch(r'graphics/map_preview/[a-z0-9_]+/tiles\.4bpp\.lz', tiles):
        raise ValueError('Missing or unsupported source preview graphics binding')
    base = tiles.removesuffix('/tiles.4bpp.lz')
    if tilemap != base + '/tilemap.bin.lz' or palette != base + '/tiles.gbapal':
        raise ValueError('Source preview tiles/tilemap/palette binding differs')
    image = Image.open(io.BytesIO(src.data[base + '/tiles.png']))
    image.load()
    if image.mode != 'P' or not image.width or not image.height or image.width % 8 or image.height % 8 or image.width * image.height > 1024 * 64:
        raise ValueError('Expected bounded indexed preview tiles')
    colors = image.getpalette()
    if colors is None or len(colors) != 32 * 3 or image.getextrema()[1] >= 32:
        raise ValueError('Expected the two source preview palette banks')
    colors = [rgb555(colors[i:i + 3]) for i in range(0, len(colors), 3)]
    entries = src.units(base + '/tilemap.bin', 'H')
    if len(entries) != 32 * 20:
        raise ValueError('Preview requires a 256x160 tilemap')
    rules = src.text('graphics_file_rules.mk')
    rule = re.search(re.escape('$(MAPPREVIEWGFXDIR)/' + base.rsplit('/', 1)[1] + '/tiles.4bpp:') +
                     r'[^\n]*\n\t[^\n]*?-num_tiles (\d+) -Wnum_tiles', rules)
    columns, tile_count = image.width // 8, image.width * image.height // 64
    if rule:
        declared = int(rule[1])
        if not 0 < declared <= tile_count:
            raise ValueError('Preview tile emission exceeds source sheet')
        # -Wnum_tiles retains the whole sheet if its truncated tail is nonzero.
        if not any(image.getpixel((tile % columns * 8 + x, tile // columns * 8 + y)) & 15
                   for tile in range(declared, tile_count) for y in range(8) for x in range(8)):
            tile_count = declared
    pixels = bytearray(256 * 160)
    for cell, entry in enumerate(entries):
        tile, bank = entry & 1023, entry >> 12
        if tile >= tile_count:
            raise ValueError('Preview tile exceeds emitted source sheet')
        for y in range(8):
            for x in range(8):
                sx = 7 - x if entry & 0x400 else x
                sy = 7 - y if entry & 0x800 else y
                index = image.getpixel((tile % columns * 8 + sx, tile // columns * 8 + sy)) & 15
                if bank not in (13, 14) and (bank != 0 or index):
                    raise ValueError('Preview pixel references an unloaded palette')
                pixels[(cell // 32 * 8 + y) * 256 + cell % 32 * 8 + x] = index + (bank - 13) * 16 if bank in (13, 14) else 0
    return dict(width=256, height=160, indices8Bpp=base64.b64encode(pixels).decode('ascii'), palette=colors)


def compile_outputs(src):
    registry = preview_registry(src)
    text = uncomment(src.text('src/map_preview_screen.c'))
    declarations = dict(re.findall(r'static const u8 (\w+)\[\] = INCBIN_U8\("([^"]+)"\);', text))
    if 'LoadPalette(sMapPreviewScreenData[idx].palptr, BG_PLTT_ID(13), 3 * PLTT_SIZE_4BPP);' not in text:
        raise ValueError('Unsupported source preview palette loading')
    names = {}
    for row in src.json('src/data/region_map/region_map_sections.json')['map_sections']:
        if row['id'] in names:
            raise ValueError('Duplicate source region section')
        names[row['id']] = row.get('name')
    art_indices, art, entries = {}, [], []
    for section, row in registry.items():
        name = names.get(section)
        if not isinstance(name, str) or not name or len(name) > 64 or any(ord(c) < 32 for c in name):
            raise ValueError('Missing or invalid source preview name: ' + section)
        binding = tuple(row[key] for key in ('tilesptr', 'tilemapptr', 'palptr'))
        if binding not in art_indices:
            art_indices[binding] = len(art)
            art.append(compile_art(src, row, declarations))
        entries.append(dict(regionSection=section, name=name, type=row['type'].removeprefix('MPS_TYPE_').lower(), art=art_indices[binding]))
    if not entries:
        raise ValueError('Empty map preview catalog')
    return {'maps/map-previews.json': encode(dict(schemaVersion=1, art=art, entries=entries))}

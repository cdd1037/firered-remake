"""Bounded map renderer derived from the reviewed regional import_map function.

Only difference in rendering semantics: authenticated map-entry VRAM clear plus
compressed/callback-free source tilesets defines absent tile pixels as zero.
Original regional helper and compact converter remain byte-for-byte unchanged.
"""
import base64
import collections
import io
import re
from PIL import Image

def import_map(src, name, stable_id, out, codec):
    # Source-authenticated map-entry loading clears all VRAM before copying only
    # the emitted compressed tile bytes. No animation callback writes it later.
    TILESETS, PROFILE = codec.TILESETS, codec.PROFILE
    digest, canonical, movement = codec.digest, codec.canonical, codec.movement
    reset = src.text('src/overworld.c')
    loader = src.text('src/fieldmap.c')
    decompress = src.text('src/new_menu_helpers.c')
    if not all(part in reset for part in (
        'static void InitOverworldBgs(void)\n{\n    MoveSaveBlocks_ResetHeap_();\n    ResetScreenForMapLoad();',
        'DmaFillLarge16(3, 0, (void *)(VRAM + 0x0), 0x18000, 0x1000);',
        'case 0:\n        InitOverworldBgs();',
        'case 8:\n        CopySecondaryTilesetToVram(gMapHeader.mapLayout);')) or \
       'DecompressAndCopyTileDataToVram2(2, tileset->tiles, numTiles * 32, offset, 0);' not in loader or \
       'if (sizeOut > size)\n            sizeOut = size;' not in decompress or \
       'CopyDecompressedTileDataToVram(bgId, ptr, sizeOut, offset, mode);' not in decompress:
        raise ValueError('Cleared VRAM load rule differs from pinned source')
    cleared_references = collections.Counter()

    path = f'data/maps/{name}/map.json'
    events = src.json(path)
    layouts = src.json('data/layouts/layouts.json')['layouts']
    layout = next(x for x in layouts if x.get('id') == events['layout'])
    width, height = layout['width'], layout['height']
    bw, bh = layout['border_width'], layout['border_height']
    blocks = src.units(layout['blockdata_filepath'], 'H')
    border = src.units(layout['border_filepath'], 'H')
    if len(blocks) != width * height or len(border) != bw * bh:
        raise ValueError('Map binary dimensions differ')
    bases = ['data/tilesets/' + TILESETS[layout[k]] for k in ('primary_tileset', 'secondary_tileset')]
    headers = src.text('src/data/tilesets/headers.h')
    graphics_text = src.text('src/data/tilesets/graphics.h') + src.text('src/graphics.c')
    graphics_bases = []
    for kind, base in zip(('primary_tileset', 'secondary_tileset'), bases):
        body = re.search(r'const struct Tileset ' + layout[kind] + r' =\s*\{(.*?)\n\};', headers, re.S)[1]
        if not re.search(r'\.isCompressed = TRUE', body) or not re.search(r'\.callback = NULL', body):
            raise ValueError('Cleared VRAM renderer only admits compressed callback-free tilesets')
        symbol = re.search(r'\.tiles = (\w+)', body)[1]
        declared = re.search(r'\b' + symbol + r'\[\] = INCBIN_U32\("(.*?)"\)', graphics_text)[1]
        graphics_base = declared.removesuffix('/tiles.4bpp.lz')
        if graphics_base != base and (base, graphics_base) != ('data/tilesets/secondary/silph_co', 'data/tilesets/secondary/condominiums'):
            raise ValueError('Unreviewed tileset graphics alias')
        palette_symbol = re.search(r'\.palettes = (\w+)', body)[1]
        palette_body = re.search(r'\b' + palette_symbol + r'\[\]\[16\] =\s*\{(.*?)\n\};', graphics_text, re.S)[1]
        if re.findall(r'INCBIN_U16\("(.*?)"\)', palette_body) != [graphics_base + f'/palettes/{i:02d}.gbapal' for i in range(16)]:
            raise ValueError('Tileset palette binding differs')
        graphics_bases.append(graphics_base)
    attrs = [src.units(p + '/metatile_attributes.bin', 'I') for p in bases]
    metatiles = [src.units(p + '/metatiles.bin', 'H') for p in bases]
    if len(attrs[0]) != 640 or any(len(m) != len(a) * 8 for m, a in zip(metatiles, attrs)):
        raise ValueError('Unsupported tileset dimensions')
    behavior_names = {int(v, 16): n for n, v in re.findall(r'^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)', src.text('include/constants/metatile_behaviors.h'), re.M)}

    def attributes(mid):
        index = 0 if mid < 640 else 1
        local = mid if index == 0 else mid - 640
        if local >= len(attrs[index]):
            raise ValueError('Metatile outside tileset')
        return attrs[index][local], metatiles[index][local * 8:local * 8 + 8], bases[index], local

    def cell(raw, x, y):
        mid = raw & 1023
        a, _, base, local = attributes(mid)
        behavior = a & 511
        return dict(x=x, y=y, rawBlock=raw, metatileId=mid, collision=(raw >> 10) & 3,
                    elevation=raw >> 12, rawAttributes=a, behaviorId=behavior,
                    behaviorName=behavior_names.get(behavior, f'UNKNOWN_0x{behavior:03X}'),
                    terrain=(a >> 9) & 31, attribute2=(a >> 14) & 15, attribute3=(a >> 18) & 63,
                    encounterType=(a >> 24) & 7, attribute5=(a >> 27) & 3, layerType=(a >> 29) & 3,
                    attribute7=a >> 31, movement=movement(behavior),
                    sourceRef=src.ref(base + '/metatile_attributes.bin', f'metatile_{local:03x}'))

    cells = [cell(b, i % width, i // width) for i, b in enumerate(blocks)]
    graphics = [Image.open(io.BytesIO(src.data[p + '/tiles.png'])) for p in graphics_bases]
    if any(i.mode != 'P' or i.width % 8 or i.height % 8 or i.getextrema()[1] > 15 for i in graphics):
        raise ValueError('Expected indexed four-bit graphics')
    emitted = []
    for base, img in zip(graphics_bases, graphics):
        rule = re.search(re.escape('$(TILESETGFXDIR)/' + base.removeprefix('data/tilesets/') + '/tiles.4bpp:') + r'.*?\n\t[^\n]*?-num_tiles (\d+)', src.text('tileset_rules.mk'))
        emitted.append(int(rule[1]) if rule else img.width * img.height // 64)
        if emitted[-1] > min(img.width * img.height // 64, 640 if len(emitted) == 1 else 384):
            raise ValueError('Tileset emission exceeds source image')
    palettes = []
    for i in range(13):
        lines = src.text(graphics_bases[0 if i < 7 else 1] + f'/palettes/{i:02d}.pal').splitlines()
        if lines[:3] != ['JASC-PAL', '0100', '16'] or len(lines) != 19:
            raise ValueError('Invalid JASC palette')
        colors = [tuple(((int(v) >> 3) << 3) | ((int(v) >> 3) >> 2) for v in s.split()) for s in lines[3:]]
        if any(len(c) != 3 or any(v < 0 or v > 255 for v in c) for c in colors):
            raise ValueError('Invalid palette RGB')
        palettes.append(colors)
    palettes[0][0] = (0, 0, 0)
    tile_cache = {}

    def tile(entry):
        if entry not in tile_cache:
            tid, palette = entry & 1023, entry >> 12
            index, local = (0, tid) if tid < 640 else (1, tid - 640)
            if palette >= 13:
                raise ValueError('Uninitialized palette reference')
            if local >= emitted[index]:
                result = Image.new('RGBA', (8, 8))
                tile_cache[entry] = result
                cleared_references[(index, local, tid, palette)] += 1
                return result
            img = graphics[index]
            pixels = []
            for y in range(8):
                for x in range(8):
                    ix, iy = (7 - x if entry & 0x400 else x), (7 - y if entry & 0x800 else y)
                    color = img.getpixel(((local % (img.width // 8)) * 8 + ix, (local // (img.width // 8)) * 8 + iy))
                    pixels.append((*palettes[palette][color], 0 if color == 0 else 255))
            result = Image.new('RGBA', (8, 8))
            result.putdata(pixels)
            tile_cache[entry] = result
        return tile_cache[entry]

    layers = [Image.new('RGBA', (width * 16, height * 16)) for _ in range(3)]
    for c in cells:
        _, tiles, _, _ = attributes(c['metatileId'])
        if c['layerType'] == 0:
            groups = [[0x3014] * 4, tiles[:4], tiles[4:]]
        elif c['layerType'] == 1:
            groups = [tiles[:4], tiles[4:], [0] * 4]
        elif c['layerType'] == 2:
            groups = [tiles[:4], [0] * 4, tiles[4:]]
        else:
            raise ValueError('Reserved metatile layer type')
        for layer, entries in zip(layers, groups):
            for k, entry in enumerate(entries):
                layer.paste(tile(entry), (c['x'] * 16 + (k % 2) * 8, c['y'] * 16 + (k // 2) * 8))
    composite = Image.new('RGBA', layers[0].size, (0, 0, 0, 255))
    for layer in layers:
        composite = Image.alpha_composite(composite, layer)
    out.mkdir(parents=True)
    artifacts = []
    for filename, img in zip(('bottom.png', 'middle.png', 'top.png', 'map.png'), layers + [composite]):
        img.save(out / filename, optimize=False, compress_level=9)
        artifacts.append(dict(path=filename, sha256=digest((out / filename).read_bytes()), width=img.width, height=img.height))
    # Render the exact periodic source border cells separately;
    # do not infer the common 2x2 pattern or stretch a map-edge screenshot.
    border_cells = [cell(b, i % bw, i // bw) for i, b in enumerate(border)]
    border_layers = [Image.new('RGBA', (bw * 16, bh * 16)) for _ in range(3)]
    for c in border_cells:
        _, tiles, _, _ = attributes(c['metatileId'])
        groups = {0: [[0x3014] * 4, tiles[:4], tiles[4:]],
                  1: [tiles[:4], tiles[4:], [0] * 4],
                  2: [tiles[:4], [0] * 4, tiles[4:]]}.get(c['layerType'])
        if groups is None:
            raise ValueError('Reserved border layer type')
        for layer, entries in zip(border_layers, groups):
            for k, entry in enumerate(entries):
                layer.paste(tile(entry), (c['x'] * 16 + k % 2 * 8, c['y'] * 16 + k // 2 * 8))
    border_composite = Image.new('RGBA', border_layers[0].size, (0, 0, 0, 255))
    for layer in border_layers:
        border_composite = Image.alpha_composite(border_composite, layer)
    for filename, img in zip(('border-bottom.png','border-middle.png','border-top.png','border.png'), border_layers + [border_composite]):
        img.save(out / filename, optimize=False, compress_level=9)
        artifacts.append(dict(path=filename, sha256=digest((out / filename).read_bytes()), width=img.width, height=img.height))
    embedded_paths = [path, f'data/maps/{name}/scripts.inc', f'data/maps/{name}/text.inc',
                      'data/layouts/layouts.json', 'include/constants/metatile_behaviors.h',
                      layout['blockdata_filepath'], layout['border_filepath']] + [p + '/metatile_attributes.bin' for p in bases]
    world = dict(schemaVersion=1, profile=PROFILE, stableId=stable_id, sourceRef=src.ref(path, events['id']),
                 isPlayable=False, runtimeIntegrated=False, width=width, height=height, tileSize=16,
                 borderWidth=bw, borderHeight=bh, texturePath='map.png', artifacts=artifacts,
                 layout=layout, cells=cells, borderCells=border_cells,
                 events=events, embeddedSource=[dict(path=p, sha256=src.entries[p]['sha256'],
                                                  base64=base64.b64encode(src.data[p]).decode()) for p in embedded_paths if p in src.data])
    world['clearedVramTiles'] = [dict(tileset=bases[i], localTile=local, tile=tid, palette=pal) for i, local, tid, pal in sorted(cleared_references)]
    (out / 'world.json').write_bytes(canonical(world).encode('utf-8'))
    return world

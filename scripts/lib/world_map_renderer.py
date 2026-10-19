"""Render pinned source maps directly into the final compact runtime contract."""
import base64
import io
import re
import struct
from pathlib import Path
from PIL import Image
from map_runtime import encode, movement


def tileset_graphics(src, layout):
    """Resolve current layout graphics and palette banks from source declarations."""
    headers = src.text('src/data/tilesets/headers.h')
    graphics_text = src.text('src/data/tilesets/graphics.h') + src.text('src/graphics.c')
    graphics_bases = []
    cleared_banks = []
    for kind in ('primary_tileset', 'secondary_tileset'):
        body = re.search(r'const struct Tileset ' + layout[kind] + r' =\s*\{(.*?)\n\};', headers, re.S)[1]
        cleared_banks.append(bool(re.search(r'\.isCompressed = TRUE', body) and re.search(r'\.callback = NULL', body)))
        symbol = re.search(r'\.tiles = (\w+)', body)[1]
        declared = re.search(r'\b' + symbol + r'\[\] = INCBIN_U32\("(.*?)"\)', graphics_text)[1]
        if not declared.startswith('data/tilesets/') or not declared.endswith('/tiles.4bpp.lz') or '..' in declared.split('/'):
            raise ValueError('Invalid source tileset graphics binding')
        graphics_base = declared.removesuffix('/tiles.4bpp.lz')
        palette_symbol = re.search(r'\.palettes = (\w+)', body)[1]
        palette_body = re.search(r'\b' + palette_symbol + r'\[\]\[16\] =\s*\{(.*?)\n\};', graphics_text, re.S)[1]
        if re.findall(r'INCBIN_U16\("(.*?)"\)', palette_body) != [graphics_base + f'/palettes/{i:02d}.gbapal' for i in range(16)]:
            raise ValueError('Tileset palette binding differs')
        graphics_bases.append(graphics_base)
    return graphics_bases, cleared_banks


def map_palettes(src, graphics_bases):
    """The 13 loaded field palette banks, quantized exactly through RGB555."""
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
    return palettes


def render_map(src, name, stable_id, output, *, tilesets, render_border=True, layout_symbol=None, technical_spawn=None, metatile_exports=None, map_previews=None):
    output = Path(output)
    out = output.parent
    prefix = output.stem + "."
    path = f'data/maps/{name}/map.json'
    events = src.json(path)
    layouts = src.json('data/layouts/layouts.json')['layouts']
    original_layout = next(x for x in layouts if x.get('id') == events['layout'])
    layout = next((x for x in layouts if x.get('id') == (layout_symbol or events['layout'])), None)
    if layout is None:
        raise ValueError('Unknown source layout: ' + str(layout_symbol))
    if layout_symbol and any(layout[key] != original_layout[key] for key in
                             ('width','height','border_width','border_height','primary_tileset','secondary_tileset')):
        raise ValueError('Variant layout is incompatible with map events: ' + name)
    width, height = layout['width'], layout['height']
    bw, bh = layout['border_width'], layout['border_height']
    blocks = src.units(layout['blockdata_filepath'], 'H')
    border = src.units(layout['border_filepath'], 'H')
    if len(blocks) != width * height or len(border) != bw * bh:
        raise ValueError('Map binary dimensions differ')
    bases = ['data/tilesets/' + tilesets[layout[k]] for k in ('primary_tileset', 'secondary_tileset')]
    graphics_bases, cleared_banks = tileset_graphics(src, layout)
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
                    attribute7=a >> 31, movement=movement(behavior))

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
    palettes = map_palettes(src, graphics_bases)
    tile_cache = {}

    def tile(entry):
        if entry not in tile_cache:
            tid, palette = entry & 1023, entry >> 12
            index, local = (0, tid) if tid < 640 else (1, tid - 640)
            if palette >= 13:
                raise ValueError('Uninitialized palette reference')
            if local >= emitted[index] and palette < 13 and cleared_banks[index]:
                reset = src.text('src/overworld.c')
                loader = src.text('src/fieldmap.c')
                decompress = src.text('src/new_menu_helpers.c')
                if ('DmaFillLarge16(3, 0, (void *)(VRAM + 0x0), 0x18000, 0x1000);' not in reset
                    or 'DecompressAndCopyTileDataToVram2(2, tileset->tiles, numTiles * 32, offset, 0);' not in loader
                    or 'if (sizeOut > size)' not in decompress):
                    raise ValueError('Source cleared-VRAM loading semantics differ')
                tile_cache[entry] = Image.new('RGBA', (8, 8))
                return tile_cache[entry]
            if palette >= 13 or local >= emitted[index]:
                raise ValueError(f'Uninitialized tile/palette reference: {name} tile={tid} palette={palette} emitted={emitted[index]}')
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

    if metatile_exports is not None:
        result = {}
        for mid in metatile_exports:
            raw_attribute, entries, _, _ = attributes(mid)
            layer_type = (raw_attribute >> 29) & 3
            groups = ([[0x3014] * 4, entries[:4], entries[4:]] if layer_type == 0 else
                      [entries[:4], entries[4:], [0] * 4] if layer_type == 1 else
                      [entries[:4], [0] * 4, entries[4:]] if layer_type == 2 else None)
            if groups is None:
                raise ValueError('Reserved metatile layer type')
            images = []
            for group in groups:
                image = Image.new('RGBA', (16, 16))
                for k, entry in enumerate(group):
                    image.paste(tile(entry), ((k % 2) * 8, (k // 2) * 8))
                images.append(image)
            result[mid] = images
        return result

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
    out.mkdir(parents=True, exist_ok=True)
    artifacts = []
    for filename, img in zip(('bottom.png', 'middle.png', 'top.png', 'map.png'), layers + [composite]):
        filename = prefix + filename
        img.save(out / filename, optimize=False, compress_level=9)
        artifacts.append(dict(path=filename,  width=img.width, height=img.height))
    # Render the exact periodic source border cells separately;
    # do not infer the common 2x2 pattern or stretch a map-edge screenshot.
    border_cells = [cell(b, i % bw, i // bw) for i, b in enumerate(border)]
    if render_border:
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
            filename = prefix + filename
            img.save(out / filename, optimize=False, compress_level=9)
            artifacts.append(dict(path=filename,  width=img.width, height=img.height))
    scripts = []
    for leaf in ('scripts.inc', 'text.inc'):
        script = f'data/maps/{name}/{leaf}'
        if src.exists(script):
            src.data[script]
            scripts.append(script)
    if not any(p.endswith('/scripts.inc') for p in scripts):
        raise ValueError('Missing map scripts: ' + name)
    mids = sorted({block & 1023 for block in blocks + border})
    if map_previews is None:
        from field_map_preview_metadata import compile_metadata
        map_previews = compile_metadata(src)
    preview = map_previews.get(name)
    if preview is not None and (set(preview) != {'type', 'visitFlag'} or preview['type'] not in ('cave', 'forest') or
                                (preview['visitFlag'] is None and preview['type'] != 'forest') or
                                (preview['visitFlag'] is not None and not re.fullmatch(r'FLAG_[A-Z0-9_]+', preview['visitFlag']))):
        raise ValueError('Invalid map preview metadata')
    document = dict(schemaVersion=3, mapPreview=preview, stableId=stable_id, sourceMapSymbol=events['id'],
        sourceMapName=name, width=width, height=height, tileSize=16,
        borderWidth=bw, borderHeight=bh, texturePath=prefix+'map.png', artifacts=artifacts,
        technicalSpawn=technical_spawn, events=events,
        blocks=base64.b64encode(struct.pack('<' + str(len(blocks)) + 'H', *blocks)).decode(),
        borderBlocks=base64.b64encode(struct.pack('<' + str(len(border)) + 'H', *border)).decode(),
        metatiles=[[mid, attributes(mid)[0]] for mid in mids],
        behaviors=[dict(id=value, name=behavior_names.get(value, f'UNKNOWN_0x{value:03X}'))
                   for value in sorted({attributes(mid)[0] & 511 for mid in mids})], scriptPaths=scripts)
    output.write_bytes(encode(document))
    return document

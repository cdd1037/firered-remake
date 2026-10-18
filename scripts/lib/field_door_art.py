"""Source-driven small-door replacement layers for the selected world's layouts.

The admission recipe bounds retained artwork. It never assigns maps or numeric
metatiles: those bindings come from field_door.c and the current layout tables.
"""
import io
import json
import re
from PIL import Image
from map_runtime import encode
from map_source import tileset_bindings
from project_paths import PROJECT_ROOT
from world_map_renderer import map_palettes, tileset_graphics


def source_doors(src):
    text = re.sub(r'/\*.*?\*/|//[^\n]*', '', src.text('src/field_door.c'), flags=re.S)
    labels = {name: int(value, 16) for name, value in re.findall(
        r'^#define\s+(METATILE_\w+)\s+(0x[0-9A-Fa-f]+)',
        src.text('include/constants/metatile_labels.h'), re.M)}
    tiles = dict(re.findall(r'static const u8 (sDoorAnimTiles_\w+)\[\] = INCBIN_U8\("([^"]+)"\);', text))
    palettes = {name: [int(value.strip()) for value in body.split(',') if value.strip()]
                for name, body in re.findall(r'static const u8 (sDoorAnimPalettes_\w+)\[\] = \{([^}]+)\};', text)}
    table = re.search(r'static const struct DoorGraphics sDoorGraphics\[\] = \{(.*?)\n\};', text, re.S)
    if table is None:
        raise ValueError('Missing source door graphics table')
    # These are rendering semantics, not a historical table-count check.
    if not re.search(r'if \(GetDoorSoundType\(sDoorGraphics, x, y\) == DOOR_SOUND_NORMAL\)\s*return SE_DOOR;\s*else\s*return SE_SLIDING_DOOR;', text):
        raise ValueError('Unsupported door sound dispatch')
    camera = src.text('src/field_camera.c')
    door_draw = re.search(r'void DrawDoorMetatileAt\(.*?\n\}', camera, re.S)
    if door_draw is None or 'DrawMetatile(1, tiles, offset);' not in door_draw[0]:
        raise ValueError('Unsupported door replacement-layer semantics')
    result = {}
    for body in re.findall(r'\{([^{}]*)\}', table[1]):
        if not body.strip():
            continue
        parts = [part.strip() for part in body.split(',')]
        if len(parts) != 5:
            raise ValueError('Invalid source door table row')
        label, sound, size, pixels, colors = parts
        if (label not in labels or pixels not in tiles or colors not in palettes
                or label in result or sound not in ('DOOR_SOUND_NORMAL', 'DOOR_SOUND_SLIDING')
                or size not in ('DOOR_SIZE_1x1', 'DOOR_SIZE_1x2')):
            raise ValueError('Invalid source door graphics binding: ' + label)
        path = tiles[pixels]
        if not re.fullmatch(r'graphics/door_anims/[a-z0-9_]+\.4bpp', path):
            raise ValueError('Unsupported source door tile path')
        numbers = palettes[colors]
        if len(numbers) != 8 or any(value < 0 or value >= 13 for value in numbers):
            raise ValueError('Door references an unloaded map palette')
        result[label] = dict(metatile=labels[label], sound='SE_DOOR' if sound == 'DOOR_SOUND_NORMAL' else 'SE_SLIDING_DOOR',
                             height=16 if size == 'DOOR_SIZE_1x1' else 32,
                             path=path.removesuffix('.4bpp') + '.png', palettes=numbers)
    if not result:
        raise ValueError('Empty source door graphics table')
    return result


def tile_pixels(image, tile, palette):
    """GBA tiled 4bpp ordering; index zero is transparent on every BG layer."""
    columns = image.width // 8
    if tile < 0 or tile >= image.width * image.height // 64:
        raise ValueError('Door tile exceeds source sheet')
    pixels = []
    for y in range(8):
        for x in range(8):
            index = image.getpixel((tile % columns * 8 + x, tile // columns * 8 + y))
            pixels.append((*palette[index], 255 if index else 0))
    result = Image.new('RGBA', (8, 8))
    result.putdata(pixels)
    return result


def indexed_image(src, path):
    image = Image.open(io.BytesIO(src.data[path]))
    image.load()
    if image.mode != 'P' or image.width % 8 or image.height % 8 or image.getextrema()[1] > 15:
        raise ValueError('Expected indexed 4bpp door/tileset pixels: ' + path)
    return image


def compile_outputs(src, used_layouts, *, admitted=None):
    if admitted is None:
        admitted = json.loads((PROJECT_ROOT / 'scripts/recipes/presentation/field-door-art.json').read_bytes())['metatiles']
    if not admitted or len(set(admitted)) != len(admitted):
        raise ValueError('Empty or duplicate door art admission')
    doors = source_doors(src)
    if set(admitted) - doors.keys():
        raise ValueError('Door art admission has no source table row')
    chosen = [doors[label] for label in admitted]
    if any(door['height'] != 16 for door in chosen):
        raise ValueError('Only ordinary small-door replacement art is supported')
    # Required selected inputs are read eagerly. An absent admitted sheet is an
    # error, never a silent reduction in runtime coverage.
    sheets = {door['path']: indexed_image(src, door['path']) for door in chosen}
    if any(image.size != (16, 48) for image in sheets.values()):
        raise ValueError('Small door requires three original 16x16 frames')
    behavior = re.search(r'^#define\s+MB_WARP_DOOR\s+(0x[0-9A-Fa-f]+)',
                         src.text('include/constants/metatile_behaviors.h'), re.M)
    if behavior is None:
        raise ValueError('Missing source door behavior')
    behavior = int(behavior[1], 16)
    bindings = tileset_bindings(src)
    layouts = {row['id']: row for row in src.json('data/layouts/layouts.json')['layouts'] if 'id' in row}
    if not used_layouts or set(used_layouts) - layouts.keys():
        raise ValueError('Missing selected door layouts')
    entries, rows, deduplicated = [], [], {}
    for identity in sorted(used_layouts):
        layout = layouts[identity]
        attributes = [src.units('data/tilesets/' + bindings[layout[key]] + '/metatile_attributes.bin', 'I')
                      for key in ('primary_tileset', 'secondary_tileset')]
        applicable = []
        for door in chosen:
            mid = door['metatile']
            bank, index = (0, mid) if mid < 640 else (1, mid - 640)
            if index < len(attributes[bank]) and attributes[bank][index] & 511 == behavior:
                applicable.append(door)
        if not applicable:
            continue
        graphics, _ = tileset_graphics(src, layout)
        palettes = map_palettes(src, graphics)
        primary = indexed_image(src, graphics[0] + '/tiles.png')
        for door in applicable:
            # Three columns are bottom/middle/top, and rows are open stages.
            strip = Image.new('RGBA', (48, 48))
            for frame in range(3):
                for quadrant in range(4):
                    x, y = quadrant % 2 * 8, frame * 16 + quadrant // 2 * 8
                    strip.paste(tile_pixels(sheets[door['path']], frame * 4 + quadrant, palettes[door['palettes'][quadrant]]), (x, y))
                    strip.paste(tile_pixels(primary, 0, palettes[door['palettes'][quadrant + 4]]), (16 + x, y))
                    strip.paste(tile_pixels(primary, 0, palettes[0]), (32 + x, y))
            pixels = strip.tobytes()
            if pixels not in deduplicated:
                deduplicated[pixels] = len(rows) * 48
                rows.append(strip)
            entries.append(dict(layout=identity, metatileId=door['metatile'], soundEffect=door['sound'],
                                width=16, height=16, atlasY=deduplicated[pixels]))
    # A selected world may have no compatible door; its empty catalog is honest.
    atlas = Image.new('RGBA', (48, max(1, len(rows)) * 48))
    for index, row in enumerate(rows):
        atlas.paste(row, (0, index * 48))
    output = io.BytesIO()
    atlas.save(output, format='PNG', optimize=False, compress_level=9)
    return {'maps/door-animations.json': encode(dict(schemaVersion=1, texturePath='maps/door-animations.png',
                                                   width=atlas.width, height=atlas.height, entries=entries)),
            'maps/door-animations.png': output.getvalue()}

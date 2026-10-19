"""Source-driven door replacement layers for the selected world's layouts.

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
    placement = re.search(r'static void DrawCurrentDoorAnimFrame\([^;]*?\)\s*\{(.*?)\n\}', text, re.S)
    expected = '''u16 tiles[8];
        if (gfx->size == DOOR_SIZE_1x1)
            BuildDoorTiles(tiles, DOOR_TILE_START, paletteNums);
        else {
            BuildDoorTiles(tiles, DOOR_TILE_START, paletteNums);
            DrawDoorMetatileAt(x, y - 1, tiles);
            BuildDoorTiles(tiles, DOOR_TILE_START + 4, &paletteNums[4]);
        }
        DrawDoorMetatileAt(x, y, tiles);'''
    if placement is None or re.sub(r'\s+', '', placement[1]) != re.sub(r'\s+', '', expected):
        raise ValueError('Unsupported source door tile placement')
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


def frame_tiles(src, height):
    """Read each open-stage tile offset, independently of atlas row packing."""
    name = 'sDoorAnimFrames_Open' + ('Small' if height == 16 else 'Large')
    table = re.search(r'static const struct DoorAnimFrame ' + name + r'\[\] = \{(.*?)\n\};',
                      src.text('src/field_door.c'), re.S)
    if table is None:
        raise ValueError('Missing source door frames: ' + name)
    rows = re.findall(r'\{\s*\d+\s*,\s*([^}]+)\}', table[1])
    if len(rows) != 4 or rows[0].strip() != 'CLOSED_DOOR_TILES_OFFSET':
        raise ValueError('Unsupported source door frames: ' + name)
    offsets = [re.fullmatch(r'\s*(\d+)\s*\*\s*TILE_SIZE_4BPP\s*', row) for row in rows[1:]]
    if any(offset is None for offset in offsets):
        raise ValueError('Unsupported source door tile offsets: ' + name)
    return [int(offset[1]) for offset in offsets]


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
    # Required selected inputs are read eagerly. An absent admitted sheet is an
    # error, never a silent reduction in runtime coverage.
    sheets = {door['path']: indexed_image(src, door['path']) for door in chosen}
    for door in chosen:
        if sheets[door['path']].size != (16, door['height'] * 3):
            raise ValueError('Door requires three original 16x' + str(door['height']) + ' frames')
    offsets = {height: frame_tiles(src, height) for height in {door['height'] for door in chosen}}
    behavior = re.search(r'^#define\s+MB_WARP_DOOR\s+(0x[0-9A-Fa-f]+)',
                         src.text('include/constants/metatile_behaviors.h'), re.M)
    if behavior is None:
        raise ValueError('Missing source door behavior')
    behavior = int(behavior[1], 16)
    bindings = tileset_bindings(src)
    layouts = {row['id']: row for row in src.json('data/layouts/layouts.json')['layouts'] if 'id' in row}
    if not used_layouts or set(used_layouts) - layouts.keys():
        raise ValueError('Missing selected door layouts')
    entries, rows, deduplicated, atlas_height = [], [], {}, 0
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
            height = door['height']
            if height == 32 and primary.crop((0, 0, 8, 8)).getextrema() != (0, 0):
                raise ValueError('Large door requires transparent primary tile 0')
            # Three columns are bottom/middle/top, and rows are open stages.
            strip = Image.new('RGBA', (48, height * 3))
            for frame, first_tile in enumerate(offsets[height]):
                for tile in range(height // 4):
                    x, y = tile % 2 * 8, frame * height + tile // 2 * 8
                    strip.paste(tile_pixels(sheets[door['path']], first_tile + tile, palettes[door['palettes'][tile]]), (x, y))
                    # BuildDoorTiles uses paletteNums[4..7] for tile 0. Its
                    # second large-door call starts at &paletteNums[4], so the
                    # lower blank layer reads beyond the declared eight slots.
                    # Transparent tile 0 makes that palette unobservable; keep
                    # its lower RGBA canonical instead of guessing adjacent data.
                    if tile < 4:
                        strip.paste(tile_pixels(primary, 0, palettes[door['palettes'][tile + 4]]), (16 + x, y))
                    strip.paste(tile_pixels(primary, 0, palettes[0]), (32 + x, y))
            key = height, strip.tobytes()
            if key not in deduplicated:
                deduplicated[key] = atlas_height
                rows.append(strip)
                atlas_height += strip.height
            entries.append(dict(layout=identity, metatileId=door['metatile'], soundEffect=door['sound'],
                                width=16, height=height, atlasY=deduplicated[key]))
    # A selected world may have no compatible door; its empty catalog is honest.
    atlas = Image.new('RGBA', (48, max(48, atlas_height)))
    y = 0
    for row in rows:
        atlas.paste(row, (0, y))
        y += row.height
    output = io.BytesIO()
    atlas.save(output, format='PNG', optimize=False, compress_level=9)
    return {'maps/door-animations.json': encode(dict(schemaVersion=1, texturePath='maps/door-animations.png',
                                                   width=atlas.width, height=atlas.height, entries=entries)),
            'maps/door-animations.png': output.getvalue()}

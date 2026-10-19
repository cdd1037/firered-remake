"""Pinned cave-transition pixels and source-derived map-preview eligibility.

The transition keeps palette indices: index zero selects the screen backdrop,
not color zero of bank 14. No dungeon-preview pictures enter this resource.
"""
import base64
import io
import re
from PIL import Image
from map_runtime import encode


def uncomment(text):
    return re.sub(r'/\*.*?\*/|//[^\n]*', '', text, flags=re.S)


def preview_registry(src):
    """Read the complete source registry; all projections share this authority."""
    text = uncomment(src.text('src/map_preview_screen.c'))
    header = uncomment(src.text('include/map_preview_screen.h'))
    table = re.search(r'static const struct MapPreviewScreen sMapPreviewScreenData\[MPS_COUNT\] = \{(.*?)\n\};', text, re.S)
    enum = re.search(r'enum MapPreviewScreenId\s*\{(.*?)\};', header, re.S)
    if table is None or enum is None:
        raise ValueError('Missing source map preview registry')
    if not re.search(r'return sMapPreviewScreenData\[idx\]\.type == type \? TRUE : FALSE;', text):
        raise ValueError('Unsupported source preview eligibility')
    constants = dict(re.findall(r'^#define\s+(MPS_TYPE_\w+)\s+(\d+)\b', header, re.M))
    if set(constants) != {'MPS_TYPE_CAVE', 'MPS_TYPE_FOREST', 'MPS_TYPE_ANY'} or len(set(constants.values())) != 3:
        raise ValueError('Unsupported source preview type declarations')
    expected = re.findall(r'\bMPS_\w+\b', enum[1])
    if not expected or expected[-1] != 'MPS_COUNT' or len(set(expected)) != len(expected):
        raise ValueError('Invalid source preview identity declarations')
    pattern = r'\[(MPS_\w+)\]\s*=\s*\{([^{}]+)\}\s*,?'
    rows = re.findall(pattern, table[1])
    if re.sub(pattern, '', table[1]).strip() or set(name for name, _ in rows) != set(expected[:-1]) or len(rows) != len(expected) - 1:
        raise ValueError('Incomplete or duplicate source preview rows')
    result = {}
    for identity, body in rows:
        fields = re.findall(r'\.(\w+)\s*=\s*(\w+)\s*,?', body)
        if re.sub(r'\.(\w+)\s*=\s*(\w+)\s*,?', '', body).strip() or len(fields) != len(dict(fields)):
            raise ValueError('Invalid source preview row: ' + identity)
        fields = dict(fields)
        if set(fields) != {'mapsec', 'type', 'flagId', 'tilesptr', 'tilemapptr', 'palptr'}:
            raise ValueError('Incomplete source preview row: ' + identity)
        section, kind = fields['mapsec'], fields['type']
        if not re.fullmatch(r'MAPSEC_[A-Z0-9_]+', section) or section in result or kind not in ('MPS_TYPE_CAVE', 'MPS_TYPE_FOREST'):
            raise ValueError('Invalid source preview section/type: ' + identity)
        if not re.fullmatch(r'FLAG_[A-Z0-9_]+', fields['flagId']):
            raise ValueError('Invalid source preview visit flag: ' + identity)
        result[section] = fields
    return result


def preview_types(src):
    return {section: row['type'] for section, row in preview_registry(src).items()}


def cave_preview_flags(src):
    return {section: row['flagId'] for section, row in preview_registry(src).items()
            if row['type'] == 'MPS_TYPE_CAVE'}


def rgb555(rgb):
    if len(rgb) != 3 or any(not isinstance(value, int) or value < 0 or value > 255 for value in rgb):
        raise ValueError('Invalid cave palette RGB')
    return sum((value >> 3) << (channel * 5) for channel, value in enumerate(rgb))


def palette(src, path):
    lines = src.text(path).splitlines()
    if lines[:3] != ['JASC-PAL', '0100', '16'] or len(lines) != 19:
        raise ValueError('Expected a 16-color cave palette: ' + path)
    return [rgb555([int(v) for v in line.split()]) for line in lines[3:]]


def compile_outputs(src):
    text = uncomment(src.text('src/fldeff_flash.c'))
    # Resolve only the five actual compiled art bindings, not arbitrary includes.
    declarations = dict(re.findall(r'static const u(?:16|32) (sCaveTransition\w*)\[\] = INCBIN_U(?:16|32)\("([^"]+)"\);', text))
    expected = {
        'sCaveTransitionPalette_White': 'graphics/cave_transition/white.gbapal',
        'sCaveTransitionPalette_Black': 'graphics/cave_transition/black.gbapal',
        'sCaveTransitionPalette': 'graphics/cave_transition/tiles.gbapal',
        'sCaveTransitionTilemap': 'graphics/cave_transition/tilemap.bin.lz',
        'sCaveTransitionTiles': 'graphics/cave_transition/tiles.4bpp.lz',
    }
    if declarations != expected:
        raise ValueError('Unsupported source cave transition bindings')
    image = Image.open(io.BytesIO(src.data[declarations['sCaveTransitionTiles'].replace('.4bpp.lz', '.png')]))
    image.load()
    if image.mode != 'P' or not image.width or not image.height or image.width % 8 or image.height % 8 or image.width * image.height > 1024 * 64:
        raise ValueError('Expected bounded indexed cave transition tiles')
    if image.getextrema()[1] > 15:
        raise ValueError('Cave transition tile index exceeds 4bpp')
    colors = image.getpalette()
    if colors is None or len(colors) < 48:
        raise ValueError('Missing cave transition PNG palette')
    colors = [rgb555(colors[i:i + 3]) for i in range(0, 48, 3)]
    entries = src.units(declarations['sCaveTransitionTilemap'].removesuffix('.lz'), 'H')
    if len(entries) != 32 * 32:
        raise ValueError('Cave transition requires a 256x256 tilemap')
    pixels = bytearray(256 * 256)
    columns, tile_count = image.width // 8, image.width * image.height // 64
    for cell, entry in enumerate(entries):
        tile, bank = entry & 1023, entry >> 12
        if tile >= tile_count:
            raise ValueError('Cave transition tile exceeds source sheet')
        for y in range(8):
            for x in range(8):
                sx = 7 - x if entry & 0x400 else x
                sy = 7 - y if entry & 0x800 else y
                index = image.getpixel((tile % columns * 8 + sx, tile // columns * 8 + sy))
                # Original blank tilemap cells use bank 0. Zero is transparent
                # in any bank; every visible pixel must use the loaded bank 14.
                if index and bank != 14:
                    raise ValueError('Cave transition pixel references an unloaded palette')
                pixels[(cell // 32 * 8 + y) * 256 + cell % 32 * 8 + x] = index
    packed = bytes(pixels[i] | pixels[i + 1] << 4 for i in range(0, len(pixels), 2))
    document = dict(schemaVersion=1, width=256, height=256,
                    indices4Bpp=base64.b64encode(packed).decode('ascii'), transitionPalette=colors,
                    whitePalette=palette(src, declarations['sCaveTransitionPalette_White'].replace('.gbapal', '.pal')),
                    blackPalette=palette(src, declarations['sCaveTransitionPalette_Black'].replace('.gbapal', '.pal')))
    return {'maps/cave-transition.json': encode(document)}

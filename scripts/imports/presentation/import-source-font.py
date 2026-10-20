#!/usr/bin/env python3
"""Convert pinned English glyph pixels/advances, without ROMs or upstream programs."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
import argparse
import io
import importlib.util
import json
import pathlib
import re
import sys

sys.dont_write_bytecode = True
from PIL import Image

ROOT = _PROJECT_ROOT
from image_io import png
spec = importlib.util.spec_from_file_location('source_font_inputs', ROOT / 'scripts/imports/source/fetch-source-font.py')
inputs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(inputs)
FACES = (('normal', 16, 16, 14), ('small', 8, 32, 13))


def decode_indices(data, height):
    """Read glyph roles as palette indices, never as RGB or grayscale values."""
    with Image.open(io.BytesIO(data)) as image:
        if image.format != 'PNG' or image.mode != 'P' or image.size != (256, height):
            raise ValueError('Unsupported source font dimensions/format')
        if image.getpalette('RGB') != [144, 200, 255, 56, 56, 56, 216, 216, 216, 255, 255, 255]:
            raise ValueError('Unexpected source font roles/palette')
        indices = image.tobytes()
        if any(index > 3 for index in indices):
            raise ValueError('Source glyph role exceeds two bits')
        return indices


def encode_png(rgba, width, height):
    if len(rgba) != width * height * 4:
        raise ValueError('RGBA length differs')
    return png(width, height, rgba)


def character_map(text):
    latin = text.split('@ Hiragana')[0]
    chars = {}
    for spelling, value in re.findall(r"^'((?:\\.|[^'])*)'\s*=\s*([0-9A-F]{2})\s*$", latin, re.M):
        char = "'" if spelling == "\\'" else spelling
        glyph = int(value, 16)
        if len(char) != 1:
            raise ValueError('Unsupported character mapping')
        if glyph == 255:  # '$' is EOS, NOT a money symbol.
            continue
        if char in chars:
            raise ValueError('Duplicate source character')
        chars[char] = glyph
    for char, symbol in [('↑', 'UP_ARROW'), ('↓', 'DOWN_ARROW'), ('←', 'LEFT_ARROW'), ('→', 'RIGHT_ARROW')]:
        match = re.search(r'^' + symbol + r'\s*=\s*([0-9A-F]{2})\s*$', latin, re.M)
        if not match:
            raise ValueError('Missing source arrow symbol')
        chars[char] = int(match[1], 16)
    if chars.get('é') != 0x1B or chars.get('¥') != 0xB7 or chars.get('?') != 0xAC or len(chars) != 146:
        raise ValueError('English character profile differs')
    # Help exposes this source symbol as Unicode; keep its original pixels and
    # face-specific advance rather than asking a supplemental CJK font for it.
    if not re.search(r'^CIRCLE_DOT\s*=\s*F9 15\s*$', text, re.M):
        raise ValueError('Source CIRCLE_DOT mapping differs')
    chars['⊙'] = 0x115
    return chars


def widths(text, name):
    match = re.search(r'static const u8 sFont' + name + r'LatinGlyphWidths\[\]\s*=\s*\{([\s\d,]+)\};', text)
    if not match:
        raise ValueError('Missing source width table: ' + name)
    result = [int(v) for v in re.findall(r'\d+', match[1])]
    if len(result) != 512 or not all(1 <= v <= 16 for v in result):
        raise ValueError('Invalid source width table')
    return result


def level_prefix_outputs( sources, foreground, shadow):
    """One explicitly needed extended symbol; preserve the original English atlas/catalog unchanged."""
    if not re.search(r'^LV_2\s*=\s*F9 05\s*$', sources['charmap.txt'].decode(), re.M):
        raise ValueError('Source LV_2 mapping differs')
    if '#define CHAR_EXTRA_SYMBOL      0xF9' not in sources['include/characters.h'].decode():
        raise ValueError('Source extended-symbol prefix differs')
    indices = decode_indices(sources['graphics/fonts/latin_small.png'], 256)
    advance = widths(sources['src/text.c'].decode(), 'Small')[0x105]
    if advance != 8:
        raise ValueError('Source LV_2 advance differs')
    # Source small glyph 0x105: column5,row8 in 32 columns of 8x16 cells.
    roles = bytes(indices[(128 + y) * 256 + 40 + x] for y in range(13) for x in range(8))
    roles = bytes(role if role in (1, 2) else 0 for role in roles)
    rgba = bytes(channel for role in roles for channel in ([0, 0, 0, 0], foreground, shadow)[role])
    png = encode_png(rgba, 8, 13)
    catalog = dict(schemaVersion=1,
         face='small', sourceFontId=0,
        sourceToken='LV_2', sourceBytes=[0xF9, 0x05], sourceGlyphId=0x105, codepoint=0xE000,
        width=8, height=13, advance=8, baseline=13, texturePath='level-prefix-small.png',

        limitation='Only the healthbox LV_2 symbol, exposed as a presentation-only private-use character; no general control-code parser.')
    outputs = {'level-prefix-small.png': png, 'level-prefix.json': inputs.canonical(catalog)}
    outputs['level-prefix-manifest.json'] = inputs.canonical(dict(schemaVersion=1,

        outputs=[dict(path=path) for path, data in sorted(outputs.items())],
         ))
    return outputs


def convert(lock, sources):
    chars = character_map(sources['charmap.txt'].decode())
    text = sources['src/text.c'].decode()
    normal = widths(text, 'Normal')
    for name in ['NormalCopy1', 'Male', 'Female']:
        if widths(text, name) != normal:
            raise ValueError('English font alias widths differ')
    pixels = decode_indices(sources['graphics/fonts/latin_normal.png'], 512)
    for alias in ['male', 'female']:
        if decode_indices(sources[f'graphics/fonts/latin_{alias}.png'], 512) != pixels:
            raise ValueError('English font alias pixels differ')
    palette = sources['graphics/text_window/stdpal_0.pal'].decode().splitlines()
    if palette[:3] != ['JASC-PAL', '0100', '16'] or len(palette) != 19:
        raise ValueError('Invalid text window palette')
    rgb = [list(map(int, row.split())) for row in palette[3:]]
    if any(len(row) != 3 or any(v < 0 or v > 255 for v in row) for row in rgb):
        raise ValueError('Invalid text palette channel')
    colors = [[(v // 8) * 255 // 31 for v in row] + [255] for row in rgb]
    foreground, shadow = colors[2], colors[3]
    outputs, fonts = {}, []
    for name, cell_width, columns, height in FACES:
        source = decode_indices(sources[f'graphics/fonts/latin_{name}.png'], 512 if name == 'normal' else 256)
        advances = widths(text, name.title())
        # Keep explicitly used Latin and display symbols in a compact atlas.
        glyphs = sorted(set(chars.values()))
        atlas_height = ((len(glyphs) + 15) // 16) * 16
        rgba = bytearray(256 * atlas_height * 4)
        locations = {}
        for index, gid in enumerate(glyphs):
            x, y = (index % 16) * 16, (index // 16) * 16
            sx, sy = (gid % columns) * cell_width, (gid // columns) * 16
            advance = advances[gid]
            if advance > cell_width:
                raise ValueError('Glyph exceeds source cell')
            roles = bytearray()
            for gy in range(height):
                for gx in range(advance):
                    role = source[(sy + gy) * 256 + sx + gx]
                    role = role if role in (1, 2) else 0  # 0 and 3 both map to background in source LUT.
                    roles.append(role)
                    if role:
                        at = ((y + gy) * 256 + x + gx) * 4
                        rgba[at:at + 4] = bytes(foreground if role == 1 else shadow)
            locations[gid] = dict(sourceGlyphId=gid, x=x, y=y, width=advance, height=height,
                                  advance=advance)
        filename = name + '.png'
        outputs[filename] = encode_png(rgba, 256, atlas_height)
        fonts.append(dict(id=name, sourceFontId=2 if name == 'normal' else 0,
            pixelSize=height, baseline=height, glyphHeight=height,
            defaultLineStep=height, dialogueLineStep=height + 1, menuLineStep=16,
            latinLetterSpacing=0, texturePath=filename, textureWidth=256, textureHeight=atlas_height,

            glyphs=[dict(character=ch, codepoint=ord(ch), **locations[gid]) for ch, gid in sorted(chars.items(), key=lambda item: ord(item[0]))]))
    catalog = dict(schemaVersion=1,
         foregroundRgba=foreground, shadowRgba=shadow,
        maleForegroundRgba=colors[8], femaleForegroundRgba=colors[4], fonts=fonts,
        fallback=dict(character='?', sourceGlyphId=0xAC, policy='Explicit replacement with diagnostic; system font fallback disabled'))
    outputs['source-font.json'] = inputs.canonical(catalog)
    outputs['manifest.json'] = runtime_json_bytes(dict(schemaVersion=1,
        commit=inputs.COMMIT,
        outputs=[dict(path=path) for path, data in sorted(outputs.items())],
         ), sort_keys=True)
    # This companion has a separate profile/manifest, preserving the original v1 glyph pixels and metrics.
    outputs.update(level_prefix_outputs( sources, foreground, shadow))
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/ui/font')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/recipes/presentation/source-font.lock.json')
    parser.add_argument('--project-lock', type=pathlib.Path, default=ROOT / 'scripts/recipes/config/source.json')
    args = parser.parse_args()
    lock = inputs.load_lock(args.lock, args.project_lock)
    outputs = convert(lock, inputs.read_verified(lock, args.raw))
    # All verification/conversion completes before existing output is touched.
    for filename in outputs:
        inputs.safe_path(args.out, filename)
        inputs.safe_path(args.out, filename + '.tmp')
    args.out.mkdir(parents=True, exist_ok=True)
    for filename, data in outputs.items():
        path = inputs.safe_path(args.out, filename)
        temporary = inputs.safe_path(args.out, filename + '.tmp')
        temporary.write_bytes(data)
        temporary.replace(path)
    print(json.dumps(dict(status='imported', fonts=2, outputFiles=len(outputs))))


if __name__ == '__main__':
    main()

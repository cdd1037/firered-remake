#!/usr/bin/env python3
"""Convert pinned English glyph pixels/advances, without ROMs or upstream programs."""
import argparse
import binascii
import importlib.util
import json
import pathlib
import re
import struct
import sys
import zlib

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location('source_font_inputs', ROOT / 'scripts/fetch-source-font.py')
inputs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(inputs)
SIGNATURE = b'\x89PNG\r\n\x1a\n'
FACES = (('normal', 16, 16, 14), ('small', 8, 32, 13))


def decode_indices(data, height):
    """Strict 256-wide, indexed 2bpp PNG reader; independent tests use Pillow."""
    if not data.startswith(SIGNATURE):
        raise ValueError('Invalid PNG signature')
    offset, chunks = 8, []
    while offset < len(data):
        if offset + 12 > len(data):
            raise ValueError('Truncated PNG')
        size = struct.unpack_from('>I', data, offset)[0]
        tag = data[offset + 4:offset + 8]
        end = offset + 12 + size
        if end > len(data):
            raise ValueError('Truncated PNG chunk')
        payload = data[offset + 8:end - 4]
        if binascii.crc32(tag + payload) & 0xffffffff != struct.unpack_from('>I', data, end - 4)[0]:
            raise ValueError('PNG CRC mismatch')
        chunks.append((tag, payload))
        offset = end
    expected = ([b'IHDR', b'PLTE', b'IDAT', b'IEND'] if height == 512 else
                [b'IHDR', b'iCCP', b'PLTE', b'bKGD', b'pHYs', b'tIME', b'IDAT', b'IEND'])
    if [tag for tag, _ in chunks] != expected:
        raise ValueError('Unsupported PNG chunk profile')
    chunks = dict(chunks)
    if chunks[b'IHDR'] != struct.pack('>IIBBBBB', 256, height, 2, 3, 0, 0, 0):
        raise ValueError('Unsupported source font dimensions/format')
    if chunks[b'PLTE'] != bytes([144, 200, 255, 56, 56, 56, 216, 216, 216, 255, 255, 255]) or chunks[b'IEND']:
        raise ValueError('Unexpected source font roles/palette')
    decoder = zlib.decompressobj()
    scan = decoder.decompress(chunks[b'IDAT'], height * 65 + 1)
    if len(scan) != height * 65 or not decoder.eof or decoder.unused_data or decoder.unconsumed_tail:
        raise ValueError('PNG decompressed size/termination differs')
    result, previous = bytearray(), [0] * 64
    for y in range(height):
        method, row = scan[y * 65], []
        if method not in range(5):
            raise ValueError('Invalid PNG filter')
        for x, value in enumerate(scan[y * 65 + 1:(y + 1) * 65]):
            a, b, c = row[x - 1] if x else 0, previous[x], previous[x - 1] if x else 0
            estimate = a + b - c
            distances = [abs(estimate - a), abs(estimate - b), abs(estimate - c)]
            prediction = [0, a, b, (a + b) // 2, [a, b, c][distances.index(min(distances))]][method]
            v = (value + prediction) & 255
            row.append(v)
            result.extend((v >> 6, (v >> 4) & 3, (v >> 2) & 3, v & 3))
        previous = row
    return bytes(result)


def png_chunk(tag, data):
    return struct.pack('>I', len(data)) + tag + data + struct.pack('>I', binascii.crc32(tag + data) & 0xffffffff)


def encode_png(rgba, width, height):
    if len(rgba) != width * height * 4:
        raise ValueError('RGBA length differs')
    scan = b''.join(b'\0' + rgba[y * width * 4:(y + 1) * width * 4] for y in range(height))
    stream = b'\x78\x01'
    for start in range(0, len(scan), 65535):
        part = scan[start:start + 65535]
        stream += bytes([int(start + len(part) == len(scan))]) + struct.pack('<HH', len(part), len(part) ^ 65535) + part
    stream += struct.pack('>I', zlib.adler32(scan) & 0xffffffff)
    return (SIGNATURE + png_chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0))
            + png_chunk(b'IDAT', stream) + png_chunk(b'IEND', b''))


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
    return chars


def widths(text, name):
    match = re.search(r'static const u8 sFont' + name + r'LatinGlyphWidths\[\]\s*=\s*\{([\s\d,]+)\};', text)
    if not match:
        raise ValueError('Missing source width table: ' + name)
    result = [int(v) for v in re.findall(r'\d+', match[1])]
    if len(result) != 512 or not all(1 <= v <= 16 for v in result):
        raise ValueError('Invalid source width table')
    return result


def level_prefix_outputs(lock, sources, foreground, shadow):
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
    catalog = dict(schemaVersion=1, profile='firered-healthbox-lv2-v1', sourceCommit=inputs.COMMIT,
        fullSourceHash=inputs.source_set_hash(lock), face='small', sourceFontId=0,
        sourceToken='LV_2', sourceBytes=[0xF9, 0x05], sourceGlyphId=0x105, codepoint=0xE000,
        width=8, height=13, advance=8, baseline=13, texturePath='level-prefix-small.png',
        pngSha256=inputs.digest(png), rgbaSha256=inputs.digest(rgba), roleSha256=inputs.digest(roles),
        limitation='Only the healthbox LV_2 symbol, exposed as a presentation-only private-use character; no general control-code parser.')
    outputs = {'level-prefix-small.png': png, 'level-prefix.json': inputs.canonical(catalog)}
    outputs['level-prefix-manifest.json'] = inputs.canonical(dict(schemaVersion=1, profile=catalog['profile'],
        sourceCommit=inputs.COMMIT, fullSourceHash=inputs.source_set_hash(lock),
        outputs=[dict(path=path, byteLength=len(data), sha256=inputs.digest(data)) for path, data in sorted(outputs.items())],
        upstreamScriptsExecuted=False, romDownloaded=False, publiclyUploaded=False))
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
        # Keep only explicitly mapped English glyphs in a compact atlas; no Japanese/extra symbols.
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
                                  advance=advance, roleSha256=inputs.digest(roles))
        filename = name + '.png'
        outputs[filename] = encode_png(rgba, 256, atlas_height)
        fonts.append(dict(id=name, sourceFontId=2 if name == 'normal' else 0,
            pixelSize=height, baseline=height, glyphHeight=height,
            defaultLineStep=height, dialogueLineStep=height + 1, menuLineStep=16,
            latinLetterSpacing=0, texturePath=filename, textureWidth=256, textureHeight=atlas_height,
            pngSha256=inputs.digest(outputs[filename]), rgbaSha256=inputs.digest(rgba),
            sourceIndexSha256=inputs.digest(source), sourceWidthTableSha256=inputs.digest(bytes(advances)),
            glyphs=[dict(character=ch, codepoint=ord(ch), **locations[gid]) for ch, gid in sorted(chars.items(), key=lambda item: ord(item[0]))]))
    catalog = dict(schemaVersion=1, profile=inputs.PROFILE, sourceCommit=inputs.COMMIT,
        fullSourceHash=inputs.source_set_hash(lock), foregroundRgba=foreground, shadowRgba=shadow,
        maleForegroundRgba=colors[8], femaleForegroundRgba=colors[4], fonts=fonts,
        fallback=dict(character='?', sourceGlyphId=0xAC, policy='Explicit replacement with diagnostic; system font fallback disabled'),
        limitations=['English quoted one-byte charmap entries plus four named arrows only; no control-code interpreter.',
                     'No Chinese or general Unicode font support, naming contract, ROM parsing, or source audio.',
                     'Background supplied by window; source glyph role 0/3 transparent, foreground/shadow retained.',
                     'Baseline is an adapter convention: draw at source top-left plus glyphHeight.',
                     'Local reference only; source and generated assets must not be publicly distributed.'])
    outputs['source-font.json'] = inputs.canonical(catalog)
    outputs['manifest.json'] = inputs.canonical(dict(schemaVersion=1, profile=inputs.PROFILE,
        commit=inputs.COMMIT, fullSourceHash=inputs.source_set_hash(lock), inputs=lock['files'],
        outputs=[dict(path=path, byteLength=len(data), sha256=inputs.digest(data)) for path, data in sorted(outputs.items())],
        upstreamScriptsExecuted=False, romDownloaded=False, publiclyUploaded=False))
    # This companion has a separate profile/manifest, leaving every byte of the original v1 assets intact.
    outputs.update(level_prefix_outputs(lock, sources, foreground, shadow))
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/source-font')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/source-font')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/source-font.lock.json')
    parser.add_argument('--project-lock', type=pathlib.Path, default=ROOT / 'content/source-lock.json')
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
    print(json.dumps(dict(status='imported', fonts=2, outputFiles=len(outputs), fullSourceHash=inputs.source_set_hash(lock))))


if __name__ == '__main__':
    main()

#!/usr/bin/env python3
"""Deterministic, dependency-free static front portraits for the three starter pic boxes.

This deliberately handles only the pinned 64x64 indexed PNG + JASC palette profile.
It never executes upstream tooling, reads ROMs, or downloads anything.
"""
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
_spec = importlib.util.spec_from_file_location('starter_art_inputs', ROOT / 'scripts/fetch-starter-art.py')
inputs = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(inputs)
NAMES = (('Bulbasaur', 1), ('Charmander', 4), ('Squirtle', 7))
PNG_SIGNATURE = b'\x89PNG\r\n\x1a\n'


def indexed_png(data):
    """Strict small PNG decoder. Independent golden checks use Pillow, not this code."""
    if not data.startswith(PNG_SIGNATURE):
        raise ValueError('Invalid PNG signature')
    position, chunks = 8, []
    while position < len(data):
        if position + 12 > len(data):
            raise ValueError('Truncated PNG chunk')
        length = struct.unpack_from('>I', data, position)[0]
        tag = data[position + 4:position + 8]
        payload = data[position + 8:position + 8 + length]
        end = position + length + 12
        if end > len(data):
            raise ValueError('Truncated PNG payload')
        expected_crc = struct.unpack_from('>I', data, end - 4)[0]
        if binascii.crc32(tag + payload) & 0xffffffff != expected_crc:
            raise ValueError('PNG CRC mismatch')
        chunks.append((tag, payload))
        position = end
    # The bounded source profile has one IDAT, no ancillary metadata and no extra frames.
    if [tag for tag, _ in chunks] != [b'IHDR', b'PLTE', b'tRNS', b'IDAT', b'IEND']:
        raise ValueError('Unsupported PNG chunk profile')
    if chunks[0][1] != struct.pack('>IIBBBBB', 64, 64, 4, 3, 0, 0, 0):
        raise ValueError('Expected a noninterlaced 64x64 indexed 4bpp PNG')
    if len(chunks[1][1]) != 48 or chunks[2][1] != b'\0' or chunks[4][1]:
        raise ValueError('Expected sixteen colors and transparent palette index zero')
    decompressor = zlib.decompressobj()
    scanlines = decompressor.decompress(chunks[3][1], 2113)
    if len(scanlines) != 2112 or not decompressor.eof or decompressor.unused_data or decompressor.unconsumed_tail:
        raise ValueError('PNG pixel stream length/termination differs')
    previous = [0] * 32
    indices = bytearray()
    for y in range(64):
        method = scanlines[y * 33]
        if method not in range(5):
            raise ValueError('Unsupported PNG filter')
        encoded = scanlines[y * 33 + 1:(y + 1) * 33]
        row = []
        for x, value in enumerate(encoded):
            left, up, upper_left = row[x - 1] if x else 0, previous[x], previous[x - 1] if x else 0
            if method == 0:
                prediction = 0
            elif method == 1:
                prediction = left
            elif method == 2:
                prediction = up
            elif method == 3:
                prediction = (left + up) // 2
            else:
                estimate = left + up - upper_left
                distances = (abs(estimate - left), abs(estimate - up), abs(estimate - upper_left))
                prediction = (left, up, upper_left)[distances.index(min(distances))]
            decoded = (value + prediction) & 255
            row.append(decoded)
            indices.extend((decoded >> 4, decoded & 15))
        previous = row
    return bytes(indices)


def palette(data):
    lines = data.decode('ascii').splitlines()
    if len(lines) != 19 or lines[:3] != ['JASC-PAL', '0100', '16']:
        raise ValueError('Expected a sixteen-color JASC-PAL 0100 palette')
    colors, words = [], []
    for line in lines[3:]:
        if not re.fullmatch(r'[0-9]+ [0-9]+ [0-9]+', line):
            raise ValueError('Invalid JASC RGB row')
        rgb = [int(channel) for channel in line.split(' ')]
        if any(channel > 255 for channel in rgb):
            raise ValueError('Palette channel outside 0..255')
        five = [channel // 8 for channel in rgb]
        words.append(five[0] | (five[1] << 5) | (five[2] << 10))
        # gbagfx WriteGbaPalette then ReadGbaPalette's exact integer mapping.
        colors.append(tuple(channel * 255 // 31 for channel in five))
    return colors, words


def rgba_pixels(indices, colors):
    if len(indices) != 4096 or len(colors) != 16 or any(index > 15 for index in indices):
        raise ValueError('Unsupported pixel/palette profile')
    return bytes(channel for index in indices for channel in (*colors[index], 0 if index == 0 else 255))


def png_chunk(tag, data):
    return struct.pack('>I', len(data)) + tag + data + struct.pack('>I', binascii.crc32(tag + data) & 0xffffffff)


def rgba_png(rgba):
    if len(rgba) != 64 * 64 * 4:
        raise ValueError('Expected one 64x64 RGBA frame')
    scanlines = b''.join(b'\0' + rgba[row * 256:(row + 1) * 256] for row in range(64))
    # One RFC 1951 stored block. Fixed bytes avoid zlib/Pillow encoder-version drift.
    count = len(scanlines)
    stream = b'\x78\x01\x01' + struct.pack('<HH', count, count ^ 0xffff) + scanlines
    stream += struct.pack('>I', zlib.adler32(scanlines) & 0xffffffff)
    return (PNG_SIGNATURE + png_chunk(b'IHDR', struct.pack('>IIBBBBB', 64, 64, 8, 6, 0, 0, 0))
            + png_chunk(b'IDAT', stream) + png_chunk(b'IEND', b''))


def reference(lock, sources, path, symbol, needle=None):
    entry = next(entry for entry in lock['files'] if entry['path'] == path)
    result = dict(kind='pret', repository=inputs.REPOSITORY, commit=inputs.COMMIT,
                  path=path, symbol=symbol, sha256=entry['sha256'], url=entry['sourceUrl'])
    if needle is not None:
        text = sources[path].decode('utf-8')
        if text.count(needle) != 1:
            raise ValueError('Missing/ambiguous source binding: ' + symbol)
        result['startLine'] = text.count('\n', 0, text.index(needle)) + 1
        result['endLine'] = result['startLine'] + needle.count('\n')
        result['url'] += f'#L{result["startLine"]}-L{result["endLine"]}'
    return result


def convert(lock, sources):
    def ref(path, symbol, needle=None):
        return reference(lock, sources, path, symbol, needle)
    runtime = [
        ('src/script_menu.c', 'ScriptMenu_ShowPokemonPic',
         '    spriteId = CreateMonSprite_PicBox(species, 8 * x + 40, 8 * y + 40, FALSE);'),
        ('src/script_menu.c', 'CreateWindowFromRect',
         '    struct WindowTemplate template = SetWindowTemplateFields(0, left + 1, top + 1, width, height, 15, 0x038);'),
        ('src/field_effect.c', 'CreateMonSprite_PicBox',
         '    u16 spriteId = CreateMonPicSprite_HandleDeoxys(species, 0, 0x8000, TRUE, x, y, 0, gMonPaletteTable[species].tag);'),
        ('src/data.c', 'sAnim_MonPic_0',
         'static const union AnimCmd sAnim_MonPic_0[] =\n{\n    ANIMCMD_FRAME(0, 0),\n    ANIMCMD_END,\n};'),
        ('include/constants/pokemon.h', 'SHINY_ODDS', '#define SHINY_ODDS 8 // Actual probability is SHINY_ODDS/65536'),
        ('include/pokemon.h', 'GET_SHINY_VALUE', '#define GET_SHINY_VALUE(otId, personality) (HIHALF(otId) ^ LOHALF(otId) ^ HIHALF(personality) ^ LOHALF(personality))'),
        ('tools/gbagfx/gfx.c', 'DOWNCONVERT_BIT_DEPTH', '#define DOWNCONVERT_BIT_DEPTH(x) ((x) / 8)'),
        ('tools/gbagfx/gfx.c', 'UPCONVERT_BIT_DEPTH', '#define UPCONVERT_BIT_DEPTH(x) (((x) * 255) / 31)'),
    ]
    display = dict(command='showmonpic', tileSize=8, interiorWidth=64, interiorHeight=64,
                   windowWidthTiles=8, windowHeightTiles=8, windowInsetTiles=1,
                   centerOffsetPixels={'x': 40, 'y': 40}, imageTopLeftOffsetPixels={'x': 8, 'y': 8},
                   applyBattleFrontPicYOffset=False, otId=0, personality=32768, shiny=False,
                   textureFilter='nearest', flipH=False, frameIndex=0,
                   sourceRefs=[ref(*row) for row in runtime])
    sprites, outputs = [], {}
    for name, sid in NAMES:
        stem, symbol = name.lower(), 'SPECIES_' + name.upper()
        image_path, palette_path = f'graphics/pokemon/{stem}/front.png', f'graphics/pokemon/{stem}/normal.pal'
        species_text = sources['include/constants/species.h'].decode()
        if not re.search(r'^#define ' + symbol + r'\s+' + str(sid) + r'\s*$', species_text, re.M):
            raise ValueError('Source species ID differs')
        front_symbol, pal_symbol = 'gMonFrontPic_' + name, 'gMonPalette_' + name
        refs = [
            ref(image_path, front_symbol), ref(palette_path, pal_symbol),
            ref('src/data/graphics/pokemon.h', front_symbol,
                f'const u32 {front_symbol}[] = INCBIN_U32("graphics/pokemon/{stem}/front.4bpp.lz");'),
            ref('src/data/graphics/pokemon.h', pal_symbol,
                f'const u32 {pal_symbol}[] = INCBIN_U32("graphics/pokemon/{stem}/normal.gbapal.lz");'),
            ref('src/data/pokemon_graphics/front_pic_table.h', 'gMonFrontPicTable',
                f'    SPECIES_SPRITE({name.upper()}, {front_symbol}),'),
            ref('src/data/pokemon_graphics/palette_table.h', 'gMonPaletteTable',
                f'    SPECIES_PAL({name.upper()}, {pal_symbol}),'),
        ]
        indices = indexed_png(sources[image_path])
        colors, words = palette(sources[palette_path])
        rgba = rgba_pixels(indices, colors)
        filename = stem + '-front.png'
        outputs[filename] = rgba_png(rgba)
        opaque = [i for i, index in enumerate(indices) if index]
        if not opaque:
            raise ValueError('Empty portrait')
        left, top = min(i % 64 for i in opaque), min(i // 64 for i in opaque)
        right, bottom = max(i % 64 for i in opaque) + 1, max(i // 64 for i in opaque) + 1
        sprites.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=sid, texturePath=filename,
                            sha256=inputs.digest(outputs[filename]), rgbaSha256=inputs.digest(rgba),
                            indexSha256=inputs.digest(indices), width=64, height=64,
                            frameWidth=64, frameHeight=64, frameCount=1, sourceFrameIndex=0,
                            frames=[{'index': 0, 'x': 0, 'y': 0, 'width': 64, 'height': 64}],
                            alphaBounds={'x': left, 'y': top, 'width': right-left, 'height': bottom-top},
                            transparentIndex=0, paletteVariant='normal', paletteRgb555=words,
                            paletteExpansion='floor(rgb5 * 255 / 31)', sourceRefs=refs))
    catalog = dict(schemaVersion=1, profile=inputs.PROFILE, repository=inputs.REPOSITORY,
                   sourceCommit=inputs.COMMIT, fullSourceHash=inputs.source_set_hash(lock),
                   display=display, sprites=sprites,
                   limitations=[
                       'Three species only; a single normal front frame per species, not general Pokemon animations.',
                       'Portrait pixels only; source window border, cry audio, fades, timing and script lifecycle belong to the runtime.',
                       'No battle offsets, back sprites, icons, shiny variants, evolutions or personality-dependent forms.',
                       'Local reference research only. Original game assets are not cleared for redistribution.',
                   ])
    outputs['starter-art.json'] = inputs.canonical(catalog)
    manifest = dict(schemaVersion=1, profile=inputs.PROFILE, repository=inputs.REPOSITORY, commit=inputs.COMMIT,
                    fullSourceHash=inputs.source_set_hash(lock), inputs=lock['files'],
                    outputs=[dict(path=path, byteLength=len(data), sha256=inputs.digest(data))
                             for path, data in sorted(outputs.items())],
                    counts={'species': 3, 'frames': 3}, pngEncoding='RGBA8 filter-0 one stored DEFLATE block',
                    upstreamScriptsExecuted=False, romDownloaded=False, publiclyUploaded=False)
    outputs['manifest.json'] = inputs.canonical(manifest)
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/starter-art')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/generated/starter-art')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/starter-art.lock.json')
    parser.add_argument('--project-lock', type=pathlib.Path, default=ROOT / 'content/source-lock.json')
    args = parser.parse_args()
    if args.output.resolve().is_relative_to(args.raw.resolve()) or args.raw.resolve().is_relative_to(args.output.resolve()):
        raise ValueError('Raw and output directories must be disjoint')
    lock = inputs.load_lock(args.lock, args.project_lock)
    outputs = convert(lock, inputs.read_verified(lock, args.raw))
    # Input hashes and all conversions pass before outputs are touched.
    targets = [(inputs.safe_path(args.output, path), data) for path, data in outputs.items()]
    args.output.mkdir(parents=True, exist_ok=True)
    for target, data in targets:
        stage = inputs.safe_path(args.output, target.name + '.tmp')
        stage.write_bytes(data)
        stage.replace(target)
    print(json.dumps({'status': 'imported', 'sprites': 3, 'frames': 3,
                      'catalog': str(args.output / 'starter-art.json'),
                      'fullSourceHash': inputs.source_set_hash(lock)}))


if __name__ == '__main__':
    main()

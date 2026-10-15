#!/usr/bin/env python3
"""Static front portraits for the three starter pic boxes.

This deliberately handles only the pinned 64x64 indexed PNG + JASC palette profile.
It never executes upstream tooling, reads ROMs, or downloads anything.
"""
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
_spec = importlib.util.spec_from_file_location('starter_art_inputs', ROOT / 'scripts/imports/source/fetch-starter-art.py')
inputs = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(inputs)
NAMES = (('Bulbasaur', 1), ('Charmander', 4), ('Squirtle', 7))


def indexed_png(data):
    """Read source indices directly; external GBA palettes provide output colors."""
    with Image.open(io.BytesIO(data)) as image:
        if image.format != 'PNG' or image.mode != 'P' or image.size != (64, 64):
            raise ValueError('Expected a 64x64 indexed PNG')
        if len(image.getpalette('RGB')) != 48 or image.info.get('transparency') != 0:
            raise ValueError('Expected sixteen colors and transparent palette index zero')
        indices = image.tobytes()
        if any(index > 15 for index in indices):
            raise ValueError('Source palette index exceeds four bits')
        return indices


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


def rgba_png(rgba):
    if len(rgba) != 64 * 64 * 4:
        raise ValueError('Expected one 64x64 RGBA frame')
    return png(64, 64, rgba)


def reference(lock, sources, path, symbol, needle=None):
    entry = next(entry for entry in lock['files'] if entry['path'] == path)
    result = dict(kind='pret', repository=inputs.REPOSITORY, commit=inputs.COMMIT,
                  path=path, symbol=symbol,  url=entry['sourceUrl'])
    if needle is not None:
        text = sources[path].decode('utf-8')
        if text.count(needle) != 1:
            raise ValueError('Missing/ambiguous source binding: ' + symbol)
        result['startLine'] = text.count('\n', 0, text.index(needle)) + 1
        result['endLine'] = result['startLine'] + needle.count('\n')
        result['url'] += f'#L{result["startLine"]}-L{result["endLine"]}'
    return result


def convert(lock, sources):
    display = dict(command='showmonpic', tileSize=8, interiorWidth=64, interiorHeight=64,
                   windowWidthTiles=8, windowHeightTiles=8, windowInsetTiles=1,
                   centerOffsetPixels={'x': 40, 'y': 40}, imageTopLeftOffsetPixels={'x': 8, 'y': 8},
                   applyBattleFrontPicYOffset=False, otId=0, personality=32768, shiny=False,
                   textureFilter='nearest', flipH=False, frameIndex=0)
    sprites, outputs = [], {}
    for name, sid in NAMES:
        stem, symbol = name.lower(), 'SPECIES_' + name.upper()
        image_path, palette_path = f'graphics/pokemon/{stem}/front.png', f'graphics/pokemon/{stem}/normal.pal'
        species_text = sources['include/constants/species.h'].decode()
        if not re.search(r'^#define ' + symbol + r'\s+' + str(sid) + r'\s*$', species_text, re.M):
            raise ValueError('Source species ID differs')
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
                             width=64, height=64,
                            frameWidth=64, frameHeight=64, frameCount=1, sourceFrameIndex=0,
                            frames=[{'index': 0, 'x': 0, 'y': 0, 'width': 64, 'height': 64}],
                            alphaBounds={'x': left, 'y': top, 'width': right-left, 'height': bottom-top},
                            transparentIndex=0, paletteVariant='normal', paletteRgb555=words,
                            paletteExpansion='floor(rgb5 * 255 / 31)'))
    catalog = dict(schemaVersion=1,  repository=inputs.REPOSITORY,

                   display=display, sprites=sprites)
    outputs['starter-art.json'] = inputs.canonical(catalog)
    manifest = dict(schemaVersion=1,  repository=inputs.REPOSITORY, commit=inputs.COMMIT,

                    outputs=[dict(path=path)
                             for path, data in sorted(outputs.items())],
                    counts={'species': 3, 'frames': 3}, pngEncoding='RGBA8 via Pillow',
                     )
    outputs['manifest.json'] = runtime_json_bytes(manifest, sort_keys=True)
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/generated/sprites/trainers/starter')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/recipes/presentation/starter-art.lock.json')
    parser.add_argument('--project-lock', type=pathlib.Path, default=ROOT / 'scripts/recipes/config/source.json')
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
                      'catalog': str(args.output / 'starter-art.json')}))


if __name__ == '__main__':
    main()

#!/usr/bin/env python3
"""Import twelve pinned starter battle sprites locally, without upstream execution.

Only ordinary, untransformed singles with Bulbasaur, Charmander or Squirtle are
covered. Existing local indexed-PNG/RGB555 helpers are shared with starter art;
the independent tests use Pillow and a previously frozen source oracle.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
import argparse
import json
import pathlib
import re
import sys

sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
REPOSITORY = 'pret/pokefirered'
PROFILE = 'firered-lab-battle-static-v1'
NAMES = (('Bulbasaur', 1), ('Charmander', 4), ('Squirtle', 7))
from image_io import png
from pokemon_import import battle_indices, battle_palette, rgba_pixels, section, display_contract
REQUIRED_PATHS = {
    f'graphics/pokemon/{name.lower()}/{filename}'
    for name, _ in NAMES for filename in ('front.png', 'back.png', 'normal.pal', 'shiny.pal')
} | {
    'include/constants/species.h', 'include/constants/battle.h', 'include/battle_anim.h',
    'src/battle_anim_mons.c', 'src/battle_controller_player.c', 'src/battle_controller_opponent.c',
    'src/data/graphics/pokemon.h', 'tools/gbagfx/gfx.c',
    'src/data/pokemon_graphics/front_pic_coordinates.h',
    'src/data/pokemon_graphics/back_pic_coordinates.h',
    'src/data/pokemon_graphics/back_pic_table.h',
    'src/data/pokemon_graphics/shiny_palette_table.h',
    'src/data/pokemon_graphics/enemy_mon_elevation.h',
}


def canonical(document):
    return (json.dumps(document, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode('utf-8')


def safe_path(root, relative):
    root = pathlib.Path(root).resolve()
    path = root / relative
    if not path.resolve().is_relative_to(root):
        raise ValueError('Input/output symlink escapes root: ' + relative)
    return path


def load_inputs(lock_path, project_lock_path, raw):
    lock = json.loads(pathlib.Path(lock_path).read_bytes())
    project = json.loads(pathlib.Path(project_lock_path).read_bytes())
    if (lock.get('repository'), lock.get('commit')) != (REPOSITORY, COMMIT):
        raise ValueError('Unsupported lab-battle-art lock identity')
    if (project['source']['repository'], project['source']['sourceCommit'],
            project['referenceGame']['gameEdition'], project['referenceGame']['romRevision']) != (
            REPOSITORY, COMMIT, 'FIRERED', 0):
        raise ValueError('Project source/edition/revision differs')
    entries = lock.get('files', [])
    paths = [entry['path'] for entry in entries]
    if len(paths) != len(set(paths)) or not REQUIRED_PATHS.issubset(paths):
        raise ValueError('Missing or duplicate lab-battle-art input')
    sources = {}
    for entry in entries:
        path = pathlib.PurePosixPath(entry['path'])
        if path.is_absolute() or '..' in path.parts or str(path) != entry['path'] or '\\' in str(path):
            raise ValueError('Unsafe source path')
        data = safe_path(raw, entry['path']).read_bytes()
        sources[entry['path']] = data
    return lock, sources


def convert(lock, sources):
    display = display_contract(lock, sources)
    sprites, outputs = [], {}
    graphics_path = 'src/data/graphics/pokemon.h'
    elevation_path = 'src/data/pokemon_graphics/enemy_mon_elevation.h'
    for name, sid in NAMES:
        stem, symbol = name.lower(), 'SPECIES_' + name.upper()
        species_row = re.search(r'^#define ' + symbol + r'\s+' + str(sid) + r'\s*$',
                                sources['include/constants/species.h'].decode(), re.M)
        if species_row is None:
            raise ValueError('Unsupported source species ID')
        elevation_source = section(sources[elevation_path].decode(), 'const u8 gEnemyMonElevation[NUM_SPECIES] =')
        elevation_body = elevation_source.split('{', 1)[1].rsplit('}', 1)[0]
        if not re.fullmatch(r'(?:\s*\[SPECIES_[A-Z0-9_]+\] = \d+,\s*)+', elevation_body):
            raise ValueError('Unsupported enemy elevation initializer')
        elevation_row = re.search(r'^\s*\[' + symbol + r'\]\s*=\s*(\d+),', elevation_source, re.M)
        # C static-array members omitted from designated initializers are zero.
        enemy_elevation = int(elevation_row.group(1)) if elevation_row else 0
        for side in ('front', 'back'):
            image_path = f'graphics/pokemon/{stem}/{side}.png'
            coords_path = f'src/data/pokemon_graphics/{side}_pic_coordinates.h'
            coordinates = re.search(r'    \[' + symbol + r'\] =\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+),\s*\},',
                                    sources[coords_path].decode())
            if coordinates is None:
                raise ValueError('Missing source image coordinates')
            width, height, offset = map(int, coordinates.groups())
            indices = battle_indices(sources[image_path])
            opaque = [i for i, index in enumerate(indices) if index]
            if not opaque:
                raise ValueError('Empty battle sprite')
            left, top = min(i % 64 for i in opaque), min(i // 64 for i in opaque)
            right, bottom = max(i % 64 for i in opaque) + 1, max(i // 64 for i in opaque) + 1
            base = display['playerBaseCenter' if side == 'back' else 'opponentBaseCenter']
            elevation = 0 if side == 'back' else enemy_elevation
            center = {'x': base['x'], 'y': base['y'] + offset - elevation}
            for variant in ('normal', 'shiny'):
                palette_path = f'graphics/pokemon/{stem}/{variant}.pal'
                colors, words = battle_palette(sources[palette_path])
                rgba = rgba_pixels(indices, colors)
                filename = f'{stem}-{side}-{variant}.png'
                outputs[filename] = png(64, 64, rgba)
                sprites.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=sid, side=side,
                                    paletteVariant=variant, texturePath=filename,
                                    sourceImagePath=image_path, sourcePalettePath=palette_path,
                                     width=64, height=64,
                                    frameWidth=64, frameHeight=64, frameCount=1, sourceFrameIndex=0,
                                    sourceWidth=width, sourceHeight=height, sourceYOffset=offset,
                                    sourceEnemyElevation=elevation, singleBattleBaseCenter=base,
                                    singleBattleCenter=center,
                                    singleBattleTopLeft={'x': center['x'] - 32, 'y': center['y'] - 32},
                                    alphaBounds={'x': left, 'y': top, 'width': right-left, 'height': bottom-top},
                                    transparentIndex=0, paletteRgb555=words,
                                    paletteExpansion='floor(rgb5 * 255 / 31)', flipH=False))
    used = [entry for entry in lock['files'] if entry['path'] in REQUIRED_PATHS]
    catalog = dict(schemaVersion=1,  repository=REPOSITORY,

                   display=display, sprites=sprites)
    outputs['lab-battle-art.json'] = canonical(catalog)
    manifest = dict(schemaVersion=1, profile=PROFILE, repository=REPOSITORY, commit=COMMIT,

                     usedArtInputPaths=sorted(REQUIRED_PATHS), sprites=sprites,
                    outputs=[dict(path=path)
                             for path, data in sorted(outputs.items())],
                    counts={'species': 3, 'sourceFrames': 6, 'palettes': 6, 'textures': 12},
                    pngEncoding='RGBA8 via Pillow',
                     )
    outputs['manifest.json'] = runtime_json_bytes(manifest, sort_keys=True)
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/generated/battle/art/lab-battle-art')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/recipes/source/fetch-lab-battle-extra.lock.json')
    parser.add_argument('--project-lock', type=pathlib.Path, default=ROOT / 'scripts/recipes/config/source.json')
    args = parser.parse_args()
    if args.raw.resolve().is_relative_to(args.output.resolve()) or args.output.resolve().is_relative_to(args.raw.resolve()):
        raise ValueError('Raw and output directories must be disjoint')
    lock, sources = load_inputs(args.lock, args.project_lock, args.raw)
    outputs = convert(lock, sources)
    targets = [(safe_path(args.output, name), safe_path(args.output, name + '.tmp'), data)
               for name, data in outputs.items()]
    args.output.mkdir(parents=True, exist_ok=True)
    for target, staged, data in targets:
        staged.write_bytes(data)
        staged.replace(target)
    print(json.dumps(dict(status='imported', textures=12, catalog=str(args.output / 'lab-battle-art.json'))))


if __name__ == '__main__':
    main()

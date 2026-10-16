#!/usr/bin/env python3
"""Import selected owned Pokemon menu art, and opponent battle art, reusing verified existing portraits."""
from __future__ import annotations
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT

import argparse
import io
import json
import re
from PIL import Image
from generated_output_compare import generated_output_matches
from image_io import indexed_png, png
from pokemon_import import battle_indices, battle_palette, footprint_indices, icon_palette, kanto_palette, rgba, rgba_pixels
from pokemon_import_io import canonical, load_recipe, read_sources, safe, write_files

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
BASE = 'content/generated/sprites/pokemon/collection/'
RECIPE = PROJECT_ROOT / 'scripts/recipes/pokemon/collection-presentation.json'


def convert(recipe, sources, root):
    rows = recipe['species']
    symbols = [row['symbol'] for row in rows]
    if not rows or len(symbols) != len(set(symbols)):
        raise ValueError('Collection art selection must be nonempty and unique')
    policy = json.loads(safe(root, 'data/game-pokemon.json').read_bytes())
    owned = {row['id']['value'] for row in policy['owned']}
    opponents = {row['id']['value'] for row in policy['opponents']}
    catalog = json.loads(safe(root, 'content/generated/data/pokemon/canonical/catalog.json').read_bytes())
    definitions = {row['id']['value']: row for row in catalog['species']}
    graphics = sources['src/data/graphics/pokemon.h'].decode()
    icons = sources['src/pokemon_icon.c'].decode()
    feet = sources['src/data/pokemon_graphics/footprint_table.h'].decode()
    constants = sources['include/constants/species.h'].decode()
    outputs, entries, sprites = {}, [], []
    for row in rows:
        symbol, name = row['symbol'], row['graphicsName']
        if not re.fullmatch(r'[A-Z0-9_]+', symbol) or not re.fullmatch(r'[A-Za-z0-9]+', name):
            raise ValueError('Invalid source species symbol')
        identity = 'pret:SPECIES_' + symbol
        if identity not in opponents or identity not in definitions:
            raise ValueError('Presentation species is not admitted')
        number = definitions[identity]['sourceSpeciesId']
        if not re.search(r'^#define SPECIES_' + symbol + r'\s+' + str(number) + r'\s*$', constants, re.M):
            raise ValueError('Source species identity differs')
        stem = symbol.lower()
        if identity in owned:
            source_icon = f'graphics/pokemon/{stem}/icon.png'
            source_foot = f'graphics/pokemon/{stem}/footprint.png'
            if not re.search(r'\[SPECIES_' + symbol + r'\]\s*=\s*gMonIcon_' + name + r',', icons) or \
               f'gMonIcon_{name}[] = INCBIN_U8("{source_icon[:-4]}.4bpp")' not in graphics or \
               not re.search(r'\[SPECIES_' + symbol + r'\]\s*=\s*gMonFootprint_' + name + r',', feet) or \
               f'gMonFootprint_{name}[] = INCBIN_U8("{source_foot[:-4]}.1bpp")' not in graphics:
                raise ValueError('Original menu art binding differs')
            palette_index = re.search(r'\[SPECIES_' + symbol + r'\]\s*=\s*(\d+),', icons)[1]
            width, height, indices, _ = indexed_png(sources[source_icon])
            if (width, height) != (32, 64):
                raise ValueError('Original icon must contain two 32x32 frames')
            palette = icon_palette(sources[f'graphics/pokemon/icon_palettes/icon_palette_{palette_index}.pal'])
            outputs[f'{stem}-icon.png'] = png(32, 32, rgba(indices[:1024], palette))
            outputs[f'{stem}-footprint.png'] = png(16, 16, rgba(footprint_indices(sources[source_foot]),
                kanto_palette(sources['graphics/pokedex/kanto_dex_bgpals.pal'])))
        portrait_path = row.get('portraitPath')
        if portrait_path is None:
            elevation = re.search(r'\[SPECIES_' + symbol + r'\]\s*=\s*(\d+)', sources['src/data/pokemon_graphics/enemy_mon_elevation.h'].decode())
            elevation = int(elevation[1]) if elevation else 0
            for side in ('front', 'back'):
                match = re.search(r'\[SPECIES_' + symbol + r'\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+)',
                    sources[f'src/data/pokemon_graphics/{side}_pic_coordinates.h'].decode())
                if not match:
                    raise ValueError('Missing battle coordinates')
                width, height, offset = map(int, match.groups())
                source_image = f'graphics/pokemon/{stem}/{side}.png'
                if f'gMon{side.title()}Pic_{name}[] = INCBIN_U32("{source_image[:-4]}.4bpp.lz")' not in graphics:
                    raise ValueError('Battle image binding differs')
                for variant in ('normal', 'shiny'):
                    palette_path = f'graphics/pokemon/{stem}/{variant}.pal'
                    palette_symbol = 'gMonPalette_' if variant == 'normal' else 'gMonShinyPalette_'
                    if f'{palette_symbol}{name}[] = INCBIN_U32("{palette_path[:-4]}.gbapal.lz")' not in graphics:
                        raise ValueError('Battle palette binding differs')
                    filename = f'{stem}-{side}-{variant}.png'
                    outputs[filename] = png(64, 64, rgba_pixels(battle_indices(sources[source_image]), battle_palette(sources[palette_path])[0]))
                    sprites.append(dict(species=dict(value=identity), sourceSpeciesId=number, side=side,
                        shiny=variant == 'shiny', texture=dict(texturePath=BASE + filename, width=64, height=64),
                        x=144 if side == 'front' else 40, y=8 + offset - elevation if side == 'front' else 48 + offset))
            portrait_path = BASE + f'{stem}-front-normal.png'
        else:
            if not portrait_path.startswith('content/generated/'):
                raise ValueError('Portrait must be a generated resource')
            with Image.open(io.BytesIO(safe(root, portrait_path).read_bytes())) as portrait:
                expected = rgba_pixels(battle_indices(sources[f'graphics/pokemon/{stem}/front.png']),
                    battle_palette(sources[f'graphics/pokemon/{stem}/normal.pal'])[0])
                if portrait.size != (64, 64) or portrait.convert('RGBA').tobytes() != expected:
                    raise ValueError('Reused portrait differs from original source pixels')
        if identity in owned:
            texture = lambda path, size: dict(texturePath=path, width=size, height=size)
            entries.append(dict(species=dict(value=identity), sourceSpeciesId=number,
                icon=texture(BASE + f'{stem}-icon.png', 32), portrait=texture(portrait_path, 64),
                footprint=texture(BASE + f'{stem}-footprint.png', 16)))
    outputs['catalog.json'] = canonical(dict(schemaVersion=1, entries=entries, sprites=sprites))
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=PROJECT_ROOT)
    parser.add_argument('--raw', type=Path)
    parser.add_argument('--out', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    root = safe(args.root)
    recipe = load_recipe(RECIPE, commit=COMMIT)
    sources = read_sources(recipe, args.raw or root / 'content/raw/pokefirered/037335f4')
    output = convert(recipe, sources, root)
    destination = safe(args.out or root / BASE)
    if args.check:
        for path, data in output.items():
            if not generated_output_matches(safe(destination, path), data):
                raise ValueError('Collection art output differs: ' + path)
    else:
        write_files(destination, output)
    print(json.dumps(dict(passed=True, check=args.check, species=len(recipe['species']), outputs=len(output))))


if __name__ == '__main__':
    main()

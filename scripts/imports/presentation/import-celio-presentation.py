#!/usr/bin/env python3
"""Rebuild the bounded Celio pixels from pinned Git blobs, without source execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
from presentation_pixels import PixelReader
from species_art import selected_species_art
from presentation_source import Source, need
from presentation_output import asset_emitter, publish
import argparse
import io
import json
import pathlib
import re
import struct
from PIL import Image

ROOT = _PROJECT_ROOT
BASE = 'content/generated/sprites/pokemon/celio/'
RAW = 'content/raw/pokefirered/037335f4/'
OWNED = [('Raikou', 243), ('Entei', 244), ('Suicune', 245)]
SPECIES = OWNED
REUSED_ASSETS = {}


def build(root, reference, dependency):
    source = Source(root, reference, dependency)
    out, assets = {}, []

    emit = asset_emitter(source, out, assets, BASE, REUSED_ASSETS)

    palette = PixelReader(source).palette

    sprites, icons, footprints = selected_species_art(source, SPECIES, SPECIES, emit)

    tiles, layers = [], []
    labels = {k: int(v, 16) for k, v in re.findall(r'^#define\s+(METATILE_\w+)\s+(0x[\da-fA-F]+)', source.text('include/constants/metatile_labels.h'), re.M)}
    layouts = json.loads(source.read('data/layouts/layouts.json'))['layouts']
    tile_graphics = source.text('src/data/tilesets/graphics.h') + '\n' + source.text('src/graphics.c')
    tile_headers = source.text('src/data/tilesets/headers.h')
    tile_bindings = source.text('src/data/tilesets/metatiles.h')
    tile_rules = source.text('tileset_rules.mk')
    patch_specs = [
        ('METATILE_PokemonCenter_NetworkMachine_Sapphire', 'OneIsland_PokemonCenter_1F', 'map.one-island-pokemon-center-1f', [(16, 4)], 1),
        ('METATILE_PokemonCenter_NetworkMachine_ScreenLeft_On', 'OneIsland_PokemonCenter_1F', 'map.one-island-pokemon-center-1f', [(14, 4)], 1),
        ('METATILE_PokemonCenter_NetworkMachine_ScreenRight_On', 'OneIsland_PokemonCenter_1F', 'map.one-island-pokemon-center-1f', [(15, 4)], 1)]
    for constant, map_name, map_id, positions, collision in patch_specs:
        source_map = json.loads(source.read('data/maps/' + map_name + '/map.json'))
        layout = next(row for row in layouts if row.get('id') == source_map['layout'])
        script = source.text('data/maps/' + map_name + '/scripts.inc')
        for x, y in positions:
            need(f'setmetatile {x}, {y}, {constant}, {collision}' in script, 'Map patch source binding differs')
        bases, metatile_paths, attribute_paths = [], [], []
        for secondary, key in enumerate(('primary_tileset', 'secondary_tileset')):
            symbol = layout[key]
            header = re.search(r'const struct Tileset ' + symbol + r'\s*=\s*\{(.*?)\};', tile_headers, re.S)[1]
            need('.isSecondary = ' + ('TRUE' if secondary else 'FALSE') in header, 'Map tileset side differs')
            image_symbol = re.search(r'\.tiles = (\w+)', header)[1]
            image_match = re.search(image_symbol + r'\[\]\s*= INCBIN_U32\("([^\"]+)"\)', tile_graphics)
            need(image_match, 'Map tileset graphics binding missing')
            base = image_match[1].rsplit('/', 1)[0]
            for field, paths in [('metatiles', metatile_paths), ('metatileAttributes', attribute_paths)]:
                binding = re.search(r'\.' + field + r' = (\w+)', header)[1]
                bound = re.search(binding + r'\[\] = INCBIN_U(?:16|32)\("([^\"]+)"\)', tile_bindings)
                need(bound, 'Map metatile binding differs')
                paths.append(bound[1])
            bases.append(base)
        images = [Image.open(io.BytesIO(source.read(base + '/tiles.png'))) for base in bases]
        palettes = [palette(bases[0 if n < 7 else 1] + f'/palettes/{n:02d}.pal') for n in range(13)]
        palettes[0][0] = (0, 0, 0)
        emitted = []
        for base, im in zip(bases, images):
            need(im.mode == 'P' and im.width % 8 == 0 and im.height % 8 == 0 and max(im.tobytes()) < 16, 'Map tile pixels differ')
            match = re.search(re.escape('$(TILESETGFXDIR)/' + base.removeprefix('data/tilesets/') + '/tiles.4bpp:') + r'.*?\n\t[^\n]*?-num_tiles (\d+)', tile_rules)
            emitted.append(int(match[1]) if match else im.width * im.height // 64)
        mid = labels[constant]
        bi, local = (0, mid) if mid < 640 else (1, mid - 640)
        attribute = struct.unpack_from('<I', source.read(attribute_paths[bi]), local * 4)[0]
        values = struct.unpack_from('<8H', source.read(metatile_paths[bi]), local * 16)
        layer_type = (attribute >> 29) & 3
        groups = {0: [[0x3014] * 4, values[:4], values[4:]], 1: [values[:4], values[4:], [0] * 4], 2: [values[:4], [0] * 4, values[4:]]}.get(layer_type)
        need(groups is not None, 'Reserved map layer type')
        tiles.append(dict(map=map_id, sourceMap=map_name, constant=constant, metatileId=mid,
                          layerType=layer_type, rawAttributes=attribute, tileEntries=values, positions=[dict(x=x,y=y) for x,y in positions], collision=collision))
        for layer, group in enumerate(groups):
            rgba = bytearray(16 * 16 * 4)
            for n, value in enumerate(group):
                tid, pal = value & 1023, value >> 12
                tile_bi, tile_local = (0, tid) if tid < 640 else (1, tid - 640)
                im = images[tile_bi]
                need(pal < 13 and tile_local < emitted[tile_bi], 'Uninitialized map tile/palette')
                for y in range(8):
                    for x in range(8):
                        ix, iy = (7 - x if value & 0x400 else x), (7 - y if value & 0x800 else y)
                        color = im.getpixel((tile_local % (im.width // 8) * 8 + ix, tile_local // (im.width // 8) * 8 + iy))
                        pos = ((n // 2 * 8 + y) * 16 + n % 2 * 8 + x) * 4
                        rgba[pos:pos + 4] = bytes((*palettes[pal][color], 255 if color else 0))
            layers.append(dict(map=map_id, metatileId=mid, layer=layer, **emit('tiles/' + str(mid) + '-' + str(layer), 16, 16, bytes(rgba), bases + metatile_paths + attribute_paths)))

    source_script = 'data/maps/OneIsland_PokemonCenter_1F/scripts.inc'
    handoff = source.text(source_script).split('OneIsland_PokemonCenter_1F_EventScript_GiveCelioSapphire::', 1)[1].split('\n\n', 1)[0]
    handoff_cues = re.findall(r'^\s*(playfanfare|playse) (\w+)$', handoff, re.M)
    need(handoff_cues == [('playfanfare', 'MUS_OBTAIN_TMHM'), ('playse', 'SE_CLICK'), ('playse', 'SE_PC_ON')], 'Celio handoff cue order differs')
    data = json.loads(source.read('data/maps/OneIsland_PokemonCenter_1F/map.json'))
    need(data['music'] == 'MUS_NET_CENTER', 'Source Network Center music differs')
    # Existing source move animations already cover these source geometry rows.
    geometry_manifest = 'content/generated/battle/art/campaign-hit-art/manifest.json'
    geometry = json.loads(source.reuse(geometry_manifest))['geometry']
    inherited_geometry = []
    for row in sprites:
        if row['paletteVariant'] != 'normal':
            continue
        expected = dict(species=row['sourceSpeciesId'], side=row['side'], sourceWidth=row['sourceWidth'],
                        sourceHeight=row['sourceHeight'], yOffset=row['sourceYOffset'],
                        elevation=row['sourceEnemyElevation'] if row['side'] == 'front' else 0)
        need(expected in geometry, 'Inherited move-animation geometry differs')
        inherited_geometry.append(expected)

    manifest = runtime_json_bytes(dict(schemaVersion=1,

        assets=assets, sprites=sprites, pokemonIcons=icons, footprints=footprints, tiles=tiles, layers=layers,
        newOwnedSpecies=[number for _, number in OWNED], newOpponentOnlySpecies=[],
        moveAnimationGeometry=dict(inheritedManifest=geometry_manifest, rows=inherited_geometry),
        handoff=dict(sourcePath=source_script, sourceLabel='OneIsland_PokemonCenter_1F_EventScript_GiveCelioSapphire',
                     orderedCues=[dict(opcode=op, cue=cue) for op, cue in handoff_cues], fanfareCounter=220,
                     mapMusic='MUS_NET_CENTER', playbackAuthority='existing committed presentation effects; no custom replay'),

        limits=['No runtime rules are created by this presentation package.', 'Party icons use original frame zero shared by normal/shiny variants.',
                'Move art reuses the existing bounded animation dispatch; this package adds no move animation coverage.'],
        outputs=[dict(path=p) for p, b in sorted(out.items())]), sort_keys=True)
    out['manifest.json'] = manifest
    result = {BASE + p: b for p, b in out.items()}
    result.update({RAW + p: b for p, b in source.raw.items()})

    report = dict(passed=True, sourceFiles=len(source.raw), sprites=len(sprites), pokemonIcons=len(icons), footprints=len(footprints),
        dynamicMetatiles=len(tiles), dynamicLayers=len(layers), moveGeometry=len(inherited_geometry), outputs=len(out) - 1,
         manifestBytes=len(manifest),
          sourceAndStaticChecksOnly=True)
    return result, report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=pathlib.Path, default=ROOT)
    parser.add_argument('--reference-root', type=pathlib.Path)
    parser.add_argument('--dependency-root', type=pathlib.Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    output, report = build(args.project_root, args.reference_root, args.dependency_root or args.project_root)
    publish(args.project_root, output, check=args.check)
    print(json.dumps(report, sort_keys=True))

#!/usr/bin/env python3
"""Rebuild the bounded Four Island pixels from pinned Git blobs, without source execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
from presentation_pixels import PixelReader
from item_source import selected_item_art
from trainer_portraits import selected_trainer_art
from species_art import selected_species_art
from presentation_source import Source, need
from presentation_output import asset_emitter, publish
import argparse
import io
import json
import pathlib
import re
import struct
import tempfile
from PIL import Image
from overworld_sprites import graphics_records

ROOT = _PROJECT_ROOT
BASE = 'content/generated/sprites/pokemon/four-island/'
RAW = 'content/raw/pokefirered/037335f4/'
SPECIES = [('Seel', 86), ('Dewgong', 87), ('Wooper', 194), ('Quagsire', 195), ('Swinub', 220), ('Piloswine', 221), ('Delibird', 225)]
TRAINERS = ('TEAM_ROCKET_GRUNT_45',)
INHERITED = {'ROCKET_GRUNT_M': 'content/generated/sprites/trainers/silph/rocket-grunt-m-front.png'}
ITEMS = ('HM07', 'NEVER_MELT_ICE')


def build(root, reference):
    source = Source(root, reference)
    out, assets = {}, []

    # Retain and validate the house actors' source sheets through the shared
    # renderer. Their runtime textures belong solely to the shared NPC atlas.
    house = json.loads(source.read('data/maps/FourIsland_LoreleisHouse/map.json'))
    house_graphics = {event['graphics_id'] for event in house['object_events']}
    with tempfile.TemporaryDirectory(prefix='four-island-overworld-') as temporary:
        overworld = graphics_records(source, house_graphics, pathlib.Path(temporary) / 'npc', render=True)

    emit = asset_emitter(source, out, assets, BASE)

    palette = PixelReader(source).palette

    sprites, icons, footprints = selected_species_art(source, SPECIES, SPECIES, emit)

    # Dewgong already appears as a Victory Road opponent. Keep its inherited
    # pixels bit-identical while adding its owned dex footprint in this profile.
    for side in ('front', 'back'):
        for variant in ('normal', 'shiny'):
            name = f'dewgong-{side}-{variant}.png'
            need(Image.open(io.BytesIO(source.reuse('content/generated/sprites/pokemon/victory-road/' + name))).convert('RGBA').tobytes() == Image.open(io.BytesIO(out[name])).convert('RGBA').tobytes(), 'Inherited Dewgong battle pixels differ')
    need(Image.open(io.BytesIO(source.reuse('content/generated/sprites/pokemon/victory-road/icons/dewgong.png'))).convert('RGBA').tobytes() == Image.open(io.BytesIO(out['icons/dewgong.png'])).convert('RGBA').tobytes(), 'Inherited Dewgong icon pixels differ')

    trainers, portraits = selected_trainer_art(source, TRAINERS, INHERITED, emit, assets, inherited_ids=False)
    need(len(trainers) == 1 and not trainers[0]['doubleBattle'], 'Trainer cohort differs')

    items = selected_item_art(source, ITEMS, emit)

    tiles, layers = [], []
    labels = {k: int(v, 16) for k, v in re.findall(r'^#define\s+(METATILE_\w+)\s+(0x[\da-fA-F]+)', source.text('include/constants/metatile_labels.h'), re.M)}
    layouts = json.loads(source.read('data/layouts/layouts.json'))['layouts']
    tile_graphics = source.text('src/data/tilesets/graphics.h') + '\n' + source.text('src/graphics.c')
    tile_headers = source.text('src/data/tilesets/headers.h')
    tile_bindings = source.text('src/data/tilesets/metatiles.h')
    tile_rules = source.text('tileset_rules.mk')
    for constant in ('METATILE_SeafoamIslands_CrackedIce', 'METATILE_SeafoamIslands_IceHole'):
        map_name, map_id = 'FourIsland_IcefallCave_1F', 'map.four-island-icefall-cave-1f'
        source_map = json.loads(source.read('data/maps/' + map_name + '/map.json'))
        layout = next(row for row in layouts if row.get('id') == source_map['layout'])
        field_tasks = source.text('src/field_tasks.c')
        need(constant in field_tasks, 'Dynamic ice metatile callback binding differs')
        bases = []
        for secondary, key in enumerate(('primary_tileset', 'secondary_tileset')):
            symbol = layout[key]
            header = re.search(r'const struct Tileset ' + symbol + r'\s*=\s*\{(.*?)\};', tile_headers, re.S)[1]
            need('.isSecondary = ' + ('TRUE' if secondary else 'FALSE') in header, 'Map tileset side differs')
            image_symbol = re.search(r'\.tiles = (\w+)', header)[1]
            image_match = re.search(image_symbol + r'\[\]\s*= INCBIN_U32\("([^\"]+)"\)', tile_graphics)
            need(image_match, 'Map tileset graphics binding missing')
            base = image_match[1].rsplit('/', 1)[0]
            for field, suffix in [('metatiles', 'metatiles.bin'), ('metatileAttributes', 'metatile_attributes.bin')]:
                binding = re.search(r'\.' + field + r' = (\w+)', header)[1]
                need(re.search(binding + r'\[\] = INCBIN_U(?:16|32)\("' + re.escape(base + '/' + suffix) + r'"\)', tile_bindings), 'Map metatile binding differs')
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
        attribute = struct.unpack_from('<I', source.read(bases[bi] + '/metatile_attributes.bin'), local * 4)[0]
        values = struct.unpack_from('<8H', source.read(bases[bi] + '/metatiles.bin'), local * 16)
        layer_type = (attribute >> 29) & 3
        groups = {0: [[0x3014] * 4, values[:4], values[4:]], 1: [values[:4], values[4:], [0] * 4], 2: [values[:4], [0] * 4, values[4:]]}.get(layer_type)
        need(groups is not None, 'Reserved map layer type')
        tiles.append(dict(map=map_id, sourceMap=map_name, constant=constant, metatileId=mid,
                          layerType=layer_type, rawAttributes=attribute, tileEntries=values))
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
            layers.append(dict(map=map_id, metatileId=mid, layer=layer, **emit('tiles/' + str(mid) + '-' + str(layer), 16, 16, bytes(rgba), bases)))

    terrains = []
    for terrain, terrain_id in [('cave', 7), ('pond', 5)]:
        terrain_source = source.text('src/battle_bg.c')
        terrain_block = re.search(r'\[BATTLE_TERRAIN_' + terrain.upper() + r'\]\s*=\s*\{(.*?)\}', terrain_source, re.S)[1]
        for field, symbol in [('tileset', 'sBattleTerrainTiles_' + terrain.title()), ('tilemap', 'sBattleTerrainTilemap_' + terrain.title()), ('palette', 'sBattleTerrainPalette_' + terrain.title())]:
            need('.' + field + ' = ' + symbol in terrain_block, f'{terrain.title()} terrain source selection differs')
        terrain_prefix = f'graphics/battle_terrain/{terrain}/terrain.'
        for symbol, suffix in [('sBattleTerrainTiles_' + terrain.title(), '4bpp.lz'), ('sBattleTerrainTilemap_' + terrain.title(), 'bin.lz'), ('sBattleTerrainPalette_' + terrain.title(), 'gbapal.lz')]:
            need(symbol + '[] = INCBIN_U32("' + terrain_prefix + suffix + '")' in terrain_source, f'{terrain.title()} terrain resource binding differs')
        terrain_image = Image.open(io.BytesIO(source.read(terrain_prefix + 'png')))
        need(terrain_image.mode == 'P' and terrain_image.width % 8 == 0 and terrain_image.height % 8 == 0, f'{terrain.title()} terrain sheet differs')
        terrain_tiles = [bytes(terrain_image.getpixel((tx * 8 + x, ty * 8 + y)) for y in range(8) for x in range(8)) for ty in range(terrain_image.height // 8) for tx in range(terrain_image.width // 8)]
        terrain_palette = palette(terrain_prefix + 'pal')
        terrain_map = source.read(terrain_prefix + 'bin')
        need(len(terrain_palette) == 48 and len(terrain_map) == 4096, f'{terrain.title()} terrain palette/map framing differs')
        entries = struct.unpack('<2048H', terrain_map)
        for value in entries:
            tile, bank = value & 1023, value >> 12
            need(tile < len(terrain_tiles) and bank in (0, 2, 3, 4) and (bank or set(terrain_tiles[tile]) == {0}), f'{terrain.title()} terrain tile reference differs')
        terrain_rgba = bytearray(240 * 160 * 4)
        for y in range(160):
            for x in range(240):
                value = entries[(y // 8) * 32 + x // 8]
                bank = value >> 12
                if bank == 0:
                    continue
                ix, iy = (7 - x % 8 if value & 1024 else x % 8), (7 - y % 8 if value & 2048 else y % 8)
                color = terrain_tiles[value & 1023][iy * 8 + ix]
                terrain_rgba[(y * 240 + x) * 4:(y * 240 + x + 1) * 4] = bytes((*terrain_palette[(bank - 2) * 16 + color], 255 if color else 0))
        need(set(terrain_rgba[3:240 * 112 * 4:4]) == {255} and set(terrain_rgba[240 * 112 * 4 + 3::4]) == {0}, f'{terrain.title()} terrain scene coverage differs')
        terrains.append(dict(terrain='BATTLE_TERRAIN_' + terrain.upper(), terrainId=terrain_id, **emit(terrain, 240, 112, bytes(terrain_rgba[:240 * 112 * 4]), [terrain_prefix + suffix for suffix in ('png', 'pal', 'bin')])))

    manifest = runtime_json_bytes(dict(schemaVersion=1,

        assets=assets, sprites=sprites, pokemonIcons=icons, footprints=footprints, items=items, trainers=trainers,
        portraits=portraits, tiles=tiles, layers=layers, terrains=terrains, newOwnedSpecies=[number for _, number in SPECIES], inheritedOpponentSprites=[87],

        limits=['No runtime rules are created by this presentation package.', 'Party icons use original frame zero shared by normal/shiny variants.'],
        outputs=[dict(path=p) for p, b in sorted(out.items())]), sort_keys=True)
    out['manifest.json'] = manifest
    result = {BASE + p: b for p, b in out.items()}
    result.update({RAW + p: b for p, b in source.raw.items()})

    report = dict(passed=True, sourceFiles=len(source.raw), sprites=len(sprites), pokemonIcons=len(icons), footprints=len(footprints),
        overworldGraphics=len(overworld),
        trainers=len(trainers), portraits=len(portraits), newPortraits=sum(not p['inherited'] for p in portraits),
        itemIcons=len(items), dynamicMetatiles=len(tiles), dynamicLayers=len(layers), outputs=len(out) - 1,
         manifestBytes=len(manifest),
          sourceAndStaticChecksOnly=True)
    return result, report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=pathlib.Path, default=ROOT)
    parser.add_argument('--reference-root', type=pathlib.Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    output, report = build(args.project_root, args.reference_root)
    publish(args.project_root, output, check=args.check)
    print(json.dumps(report, sort_keys=True))

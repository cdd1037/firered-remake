#!/usr/bin/env python3
"""Import pinned settled Sand/Plain/Building BG3 for bounded outdoor city/shore battles."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
import argparse
import json
import pathlib
import re
import struct
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
REPOSITORY = 'pret/pokefirered'
PROFILE = 'firered-city-battle-terrain-static-v1'
MAPS = [
    ('PalletTown', 'MAP_PALLET_TOWN', 'map.pallet-town', 'MAP_TYPE_TOWN'),
    ('ViridianCity', 'MAP_VIRIDIAN_CITY', 'map.viridian-city', 'MAP_TYPE_TOWN'),
    ('PewterCity', 'MAP_PEWTER_CITY', 'map.pewter-city', 'MAP_TYPE_TOWN'),
    ('CeruleanCity', 'MAP_CERULEAN_CITY', 'map.cerulean-city', 'MAP_TYPE_TOWN'),
    ('VermilionCity', 'MAP_VERMILION_CITY', 'map.vermilion-city', 'MAP_TYPE_TOWN'),
    *[(f'Route{n}', f'MAP_ROUTE{n}', f'map.route-{n}', 'MAP_TYPE_ROUTE') for n in (4, 6, 22, 24, 25)],
    ('SSAnne_Exterior', 'MAP_SSANNE_EXTERIOR', 'map.ssanne-exterior', 'MAP_TYPE_INDOOR'),
]
VARIANTS = [
    ('plain', 9, 'building', 'graphics/battle_terrain/indoor/plain.pal', 77, 80, [0]),
    ('building', 8, 'building', 'graphics/battle_terrain/building/terrain.pal', 77, 80, [0]),
    ('sand', 2, 'sand', 'graphics/battle_terrain/sand/terrain.pal', 83, 88, [23, 33]),
]
REQUIRED_PATHS = frozenset([
    'Makefile', 'graphics_file_rules.mk', 'src/battle_bg.c', 'src/battle_setup.c',
    'src/battle_main.c', 'src/metatile_behavior.c', 'include/constants/battle.h',
    'include/constants/metatile_behaviors.h', 'tools/gbagfx/gfx.c', 'tools/gbagfx/convert_png.c',
    *[f'data/maps/{name}/map.json' for name, _, _, _ in MAPS],
    *[f'graphics/battle_terrain/{folder}/terrain.{ext}' for folder in ('building', 'sand') for ext in ('png', 'bin')],
    *[v[3] for v in VARIANTS],
])
import ui_tiles as ui
from presentation_source import canonical, safe as safe_path


def load_inputs(lock_path, project_path, raw, source_repo=None):
    lock_bytes = pathlib.Path(lock_path).read_bytes()
    lock = json.loads(lock_bytes)
    project = json.loads(pathlib.Path(project_path).read_bytes())
    if (lock.get('schemaVersion'), lock.get('profile'), lock.get('repository'), lock.get('commit')) != (1, PROFILE, REPOSITORY, COMMIT):
        raise ValueError('Unsupported city terrain lock identity')
    if (project['source']['repository'], project['source']['sourceCommit'], project['referenceGame']['gameEdition'], project['referenceGame']['romRevision']) != (REPOSITORY, COMMIT, 'FIRERED', 0):
        raise ValueError('Project source/edition/revision differs')
    entries = lock.get('files', [])
    if len(entries) != len(REQUIRED_PATHS) or {e['path'] for e in entries} != REQUIRED_PATHS:
        raise ValueError('City terrain dependency set differs')
    sources = {}
    for entry in entries:
        path = entry['path']
        target = safe_path(raw, path)
        data = target.read_bytes() if source_repo is None else subprocess.run(
            ['git', '-C', str(source_repo), 'show', COMMIT + ':' + path], check=True, capture_output=True, timeout=30).stdout
        sources[path] = data
    return lock, sources


def reference(lock, sources, path, symbol, needle):
    source = sources[path].decode()
    if source.count(needle) != 1:
        raise ValueError('Missing/ambiguous source semantics: ' + symbol)


def semantics(lock, sources):
    for name, symbol, _, map_type in MAPS:
        header = json.loads(sources[f'data/maps/{name}/map.json'])
        if (header['id'], header['map_type'], header['battle_scene']) != (symbol, map_type, 'MAP_BATTLE_SCENE_NORMAL'):
            raise ValueError('City terrain map selector differs')
    checks = [
        ('src/battle_setup.c', 'BattleSetup_GetTerrainId reads player tile before map type',
         '    PlayerGetDestCoords(&x, &y);\n    tileBehavior = MapGridGetMetatileBehaviorAt(x, y);\n    if (MetatileBehavior_IsTallGrass(tileBehavior))\n        return BATTLE_TERRAIN_GRASS;\n    if (MetatileBehavior_IsLongGrass(tileBehavior))\n        return BATTLE_TERRAIN_LONG_GRASS;\n    if (MetatileBehavior_IsSandOrShallowFlowingWater(tileBehavior))\n        return BATTLE_TERRAIN_SAND;'),
        ('src/battle_setup.c', 'Outdoor city/route branch', '    case MAP_TYPE_TOWN:\n    case MAP_TYPE_CITY:\n    case MAP_TYPE_ROUTE:\n        break;'),
        ('src/battle_setup.c', 'Indoor terrain branch', '    case MAP_TYPE_INDOOR:\n    case MAP_TYPE_SECRET_BASE:\n        return BATTLE_TERRAIN_BUILDING;'),
        ('src/battle_setup.c', 'Plain fallback after all terrain tests', '    return BATTLE_TERRAIN_PLAIN;\n}\n\nstatic u8 GetBattleTransitionTypeByMap(void)'),
        ('src/metatile_behavior.c', 'Sand and shallow water', 'bool8 MetatileBehavior_IsSandOrShallowFlowingWater(u8 metatileBehavior)\n{\n    if (metatileBehavior == MB_SAND || metatileBehavior == MB_SHALLOW_WATER)\n        return TRUE;\n    else\n        return FALSE;\n}'),
        ('include/constants/metatile_behaviors.h', 'Normal ground', '#define MB_NORMAL 0x00'),
        ('include/constants/metatile_behaviors.h', 'Sand ground', '#define MB_SAND 0x21'),
        ('include/constants/metatile_behaviors.h', 'Shallow water ground', '#define MB_SHALLOW_WATER 0x17'),
        ('src/battle_bg.c', 'Normal map scene preserves selected terrain', '    battleScene = GetCurrentMapBattleScene();\n    if (battleScene == MAP_BATTLE_SCENE_NORMAL)\n    {\n        return gBattleTerrain;\n    }'),
        ('src/battle_main.c', 'BG3 origin and terrain selection', '    gBattle_BG3_X = 0;\n    gBattle_BG3_Y = 0;\n\n    gBattleTerrain = BattleSetup_GetTerrainId();'),
        ('src/battle_bg.c', 'BG3 layout', '        .bg = 3,\n        .charBaseIndex = 2,\n        .mapBaseIndex = 26,\n        .screenSize = 1,\n        .paletteMode = 0,\n        .priority = 3,\n        .baseTile = 0x000'),
        ('src/battle_bg.c', 'LoadBattleTerrainGfx', '    // Copy to bg3\n    LZDecompressVram(sBattleTerrainTable[terrain].tileset, (void *)BG_CHAR_ADDR(2));\n    LZDecompressVram(sBattleTerrainTable[terrain].tilemap, (void *)BG_SCREEN_ADDR(26));\n    LoadCompressedPalette(sBattleTerrainTable[terrain].palette, BG_PLTT_ID(2), 3 * PLTT_SIZE_4BPP);'),
        ('tools/gbagfx/gfx.c', 'RGB8 to RGB5', '#define DOWNCONVERT_BIT_DEPTH(x) ((x) / 8)'),
    ]
    for variant, terrain_id, folder, palette_path, count, _, _ in VARIANTS:
        name, tile_name = variant.title(), folder.title()
        checks.extend([
            ('include/constants/battle.h', 'Terrain ID ' + name, f'#define BATTLE_TERRAIN_{variant.upper():<12} {terrain_id}'),
            ('src/battle_bg.c', 'Terrain table ' + name,
             f'    [BATTLE_TERRAIN_{variant.upper()}] =\n    {{\n        .tileset = sBattleTerrainTiles_{tile_name},\n        .tilemap = sBattleTerrainTilemap_{tile_name},\n        .entryTileset = sBattleTerrainAnimTiles_{tile_name},\n        .entryTilemap = sBattleTerrainAnimTilemap_{tile_name},\n        .palette = sBattleTerrainPalette_{name}\n    }},'),
            ('src/battle_bg.c', 'Terrain bindings ' + name,
             f'static const u32 sBattleTerrainTiles_{tile_name}[] = INCBIN_U32("graphics/battle_terrain/{folder}/terrain.4bpp.lz");\nstatic const u32 sBattleTerrainTilemap_{tile_name}[] = INCBIN_U32("graphics/battle_terrain/{folder}/terrain.bin.lz");'),
            ('src/battle_bg.c', 'Palette binding ' + name,
             f'static const u32 sBattleTerrainPalette_{name}[] = INCBIN_U32("{palette_path[:-4]}.gbapal.lz");'),
            ('graphics_file_rules.mk', 'Tile count ' + name,
             f'$(BATTLETERRAINGFXDIR)/{folder}/terrain.4bpp: %.4bpp: %.png\n\t$(GFX) $< $@ -num_tiles {count} -Wnum_tiles'),
        ])
    ui.require(sources['tools/gbagfx/convert_png.c'].decode(), '% (1 << destBitDepth)')
    for check in checks:
        reference(lock, sources, *check)


def convert(lock, sources):
    semantics(lock, sources)
    outputs, assets, variants = {}, [], []
    for variant, terrain_id, folder, palette_path, tile_count, sheet_height, behaviors in VARIANTS:
        prefix = f'graphics/battle_terrain/{folder}/terrain.'
        source_png, tilemap = sources[prefix + 'png'], sources[prefix + 'bin']
        if ui.indexed_png(source_png)[:2] != (64, sheet_height) or len(tilemap) != 4096:
            raise ValueError('Unsupported terrain sheet/map dimensions')
        tiles = ui.tile_list(source_png)[:tile_count]
        for entry in struct.unpack('<2048H', tilemap):
            tile, bank = entry & 1023, entry >> 12
            if tile >= tile_count or bank not in (0, 2, 3, 4) or (bank == 0 and set(tiles[tile]) != {0}):
                raise ValueError('Unsupported terrain tile/palette reference')
        palette = ui.palette(sources[palette_path])
        if len(palette) != 48:
            raise ValueError('Terrain palette must contain three banks')
        full = ui.render_map(tiles, tilemap, {i + 2: palette[i * 16:(i + 1) * 16] for i in range(3)}, 240, 160)
        scene = full[:240 * 112 * 4]
        if set(scene[3::4]) != {255} or set(full[240 * 112 * 4 + 3::4]) != {0}:
            raise ValueError('Unsupported scene coverage')
        for suffix, height, pixels in [('scene', 112, scene), ('background', 160, full)]:
            name = variant + '-' + suffix
            outputs[name + '.png'] = ui.png(240, height, pixels)
            assets.append(dict(id=name, texturePath=name + '.png', width=240, height=height, x=0, y=0))
        variants.append(dict(id=variant, terrain='BATTLE_TERRAIN_' + variant.upper(), terrainId=terrain_id,
                             playerSourceBehaviors=behaviors, paletteSource=palette_path, sourceTileCount=tile_count))
    catalog = dict(schemaVersion=1,  repository=REPOSITORY,
         maps=[dict(stableId=stable, sourceName=name, sourceSymbol=symbol, mapType=kind) for name, symbol, stable, kind in MAPS],
        display=dict(logicalWidth=240, logicalHeight=160, mapBattleScene='MAP_BATTLE_SCENE_NORMAL', settled=True,
                     backgroundLayer=3, charBaseIndex=2, mapBaseIndex=26, priority=3, paletteMode=0,
                     sourceMapWidth=512, sourceMapHeight=256, sourceTileDepth=4, paletteBanks=[2, 3, 4],
                     paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)', sourceOrigin=[0, 0], textureFilter='nearest',
                     groundBasesIncluded=True, additionalGroundBaseTexturesRequired=False),
        variants=variants, assets=assets)
    outputs['city-battle-terrain.json'] = runtime_json_bytes(catalog, sort_keys=True)
    outputs['manifest.json'] = runtime_json_bytes(dict(schemaVersion=1,  commit=COMMIT,
         outputs=[dict(path=name) for name, data in sorted(outputs.items())],
         ), sort_keys=True)
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-repo', type=pathlib.Path)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/generated/battle/terrain/city')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/recipes/config/city-battle-terrain.json')
    parser.add_argument('--project-lock', type=pathlib.Path, default=ROOT / 'scripts/recipes/config/source.json')
    args = parser.parse_args()
    if args.raw.resolve().is_relative_to(args.output.resolve()) or args.output.resolve().is_relative_to(args.raw.resolve()):
        raise ValueError('Raw and output roots must be disjoint')
    lock, sources = load_inputs(args.lock, args.project_lock, args.raw, args.source_repo)
    outputs = convert(lock, sources)
    writes = [(safe_path(args.output, name), data) for name, data in outputs.items()]
    if args.source_repo is not None:
        writes += [(safe_path(args.raw, name), data) for name, data in sources.items()]
    targets = [(target, safe_path(target.parent, target.name + '.tmp'), data) for target, data in writes]
    for target, staged, data in targets:
        target.parent.mkdir(parents=True, exist_ok=True)
        staged.write_bytes(data)
        staged.replace(target)
    print(json.dumps(dict(status='imported', textures=6)))


if __name__ == '__main__':
    main()

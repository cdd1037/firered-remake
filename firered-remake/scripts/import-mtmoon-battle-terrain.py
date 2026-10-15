#!/usr/bin/env python3
"""Import the pinned, settled Mt. Moon cave battle BG3; never execute upstream code.

Accept only the bounded raw set or read those exact objects from --source-repo.
No network, ROM, entry animation, Godot mutation, or public upload is performed.
"""
import argparse
import importlib.util
import json
import pathlib
import re
import struct
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
REPOSITORY = 'pret/pokefirered'
PROFILE = 'firered-mtmoon-battle-terrain-static-v1'
TERRAIN = 'graphics/battle_terrain/cave/'
REQUIRED_PATHS = frozenset([
    'Makefile', 'data/maps/MtMoon_1F/map.json', 'data/maps/MtMoon_B1F/map.json',
    'data/maps/MtMoon_B2F/map.json', 'graphics_file_rules.mk',
    'src/battle_bg.c', 'src/battle_setup.c', 'src/battle_main.c',
    'src/metatile_behavior.c', 'include/constants/battle.h',
    'tools/gbagfx/gfx.c', 'tools/gbagfx/convert_png.c',
    TERRAIN + 'terrain.png', TERRAIN + 'terrain.pal', TERRAIN + 'terrain.bin',
])
_spec = importlib.util.spec_from_file_location('local_lab_ui_pixels', ROOT / 'scripts/import-lab-battle-ui.py')
ui = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ui)
canonical = ui.inputs.canonical
safe_path = ui.inputs.safe_path


def load_inputs(lock_path, project_path, raw, source_repo=None):
    lock_bytes = pathlib.Path(lock_path).read_bytes()
    lock = json.loads(lock_bytes)
    project = json.loads(pathlib.Path(project_path).read_bytes())
    if (lock.get('schemaVersion'), lock.get('profile'), lock.get('repository'), lock.get('commit')) != (1, PROFILE, REPOSITORY, COMMIT):
        raise ValueError('Unsupported Mt. Moon terrain lock identity')
    if (project['source']['repository'], project['source']['sourceCommit'],
            project['referenceGame']['gameEdition'], project['referenceGame']['romRevision']) != (REPOSITORY, COMMIT, 'FIRERED', 0):
        raise ValueError('Project source/edition/revision differs')
    entries = lock.get('files', [])
    paths = [entry['path'] for entry in entries]
    if len(paths) != len(set(paths)) or set(paths) != REQUIRED_PATHS:
        raise ValueError('Mt. Moon terrain dependency set differs')
    sources = {}
    for entry in entries:
        path = entry['path']
        target = safe_path(raw, path)
        if source_repo is None:
            data = target.read_bytes()
        else:
            data = subprocess.run(['git', '-C', str(source_repo), 'show', COMMIT + ':' + path],
                                  check=True, capture_output=True, timeout=30).stdout
        sources[path] = data
    return lock, sources


def reference(lock, sources, path, symbol, needle):
    source = sources[path].decode()
    if source.count(needle) != 1:
        raise ValueError('Missing/ambiguous source semantics: ' + symbol)
    first = source.count('\n', 0, source.index(needle)) + 1
    last = first + needle.count('\n')
    entry = next(e for e in lock['files'] if e['path'] == path)
    return dict(path=path, symbol=symbol, startLine=first, endLine=last,
                url=entry['sourceUrl'] + f'#L{first}-L{last}')


def semantics(lock, sources):
    for name, map_id in [('MtMoon_1F', 'MAP_MT_MOON_1F'), ('MtMoon_B1F', 'MAP_MT_MOON_B1F'), ('MtMoon_B2F', 'MAP_MT_MOON_B2F')]:
        source = json.loads(sources[f'data/maps/{name}/map.json'])
        if (source['id'], source['map_type'], source['battle_scene']) != (map_id, 'MAP_TYPE_UNDERGROUND', 'MAP_BATTLE_SCENE_NORMAL'):
            raise ValueError('Mt. Moon terrain selector differs')
    checks = [
        ('src/battle_setup.c', 'BattleSetup_GetTerrainId: ordinary underground floor',
         '    case MAP_TYPE_UNDERGROUND:\n        if (MetatileBehavior_IsIndoorEncounter(tileBehavior))\n            return BATTLE_TERRAIN_BUILDING;\n        if (MetatileBehavior_IsSurfable(tileBehavior))\n            return BATTLE_TERRAIN_POND;\n        return BATTLE_TERRAIN_CAVE;'),
        ('include/constants/battle.h', 'BATTLE_TERRAIN_CAVE', '#define BATTLE_TERRAIN_CAVE         7'),
        ('src/battle_bg.c', 'GetBattleTerrainOverride: normal map scene',
         '    battleScene = GetCurrentMapBattleScene();\n    if (battleScene == MAP_BATTLE_SCENE_NORMAL)\n    {\n        return gBattleTerrain;\n    }'),
        ('src/battle_bg.c', 'sBattleTerrainTable[BATTLE_TERRAIN_CAVE]',
         '    [BATTLE_TERRAIN_CAVE] =\n    {\n        .tileset = sBattleTerrainTiles_Cave,\n        .tilemap = sBattleTerrainTilemap_Cave,\n        .entryTileset = sBattleTerrainAnimTiles_Cave,\n        .entryTilemap = sBattleTerrainAnimTilemap_Cave,\n        .palette = sBattleTerrainPalette_Cave\n    },'),
        ('src/battle_bg.c', 'cave tile/palette/map bindings',
         'static const u32 sBattleTerrainPalette_Cave[] = INCBIN_U32("graphics/battle_terrain/cave/terrain.gbapal.lz");\nstatic const u32 sBattleTerrainTiles_Cave[] = INCBIN_U32("graphics/battle_terrain/cave/terrain.4bpp.lz");\nstatic const u32 sBattleTerrainTilemap_Cave[] = INCBIN_U32("graphics/battle_terrain/cave/terrain.bin.lz");'),
        ('src/battle_bg.c', 'gBattleBgTemplates: BG3',
         '        .bg = 3,\n        .charBaseIndex = 2,\n        .mapBaseIndex = 26,\n        .screenSize = 1,\n        .paletteMode = 0,\n        .priority = 3,\n        .baseTile = 0x000'),
        ('src/battle_bg.c', 'LoadBattleTerrainGfx',
         '    // Copy to bg3\n    LZDecompressVram(sBattleTerrainTable[terrain].tileset, (void *)BG_CHAR_ADDR(2));\n    LZDecompressVram(sBattleTerrainTable[terrain].tilemap, (void *)BG_SCREEN_ADDR(26));\n    LoadCompressedPalette(sBattleTerrainTable[terrain].palette, BG_PLTT_ID(2), 3 * PLTT_SIZE_4BPP);'),
        ('src/battle_main.c', 'initial BG3 origin and terrain selection',
         '    gBattle_BG3_X = 0;\n    gBattle_BG3_Y = 0;\n\n    gBattleTerrain = BattleSetup_GetTerrainId();'),
        ('graphics_file_rules.mk', 'cave tile count',
         '$(BATTLETERRAINGFXDIR)/cave/terrain.4bpp: %.4bpp: %.png\n\t$(GFX) $< $@ -num_tiles 84 -Wnum_tiles'),
        ('tools/gbagfx/gfx.c', 'RGB8 to RGB5', '#define DOWNCONVERT_BIT_DEPTH(x) ((x) / 8)'),
    ]
    ui.require(sources['tools/gbagfx/convert_png.c'].decode(), '% (1 << destBitDepth)')
    return [reference(lock, sources, *check) for check in checks]


def convert(lock, sources):
    refs = semantics(lock, sources)
    source_png = sources[TERRAIN + 'terrain.png']
    width, height, indices, _ = ui.indexed_png(source_png)
    if (width, height) != (64, 88):
        raise ValueError('Unsupported cave tile sheet dimensions')
    tiles = ui.tile_list(source_png)[:84]
    palette = ui.palette(sources[TERRAIN + 'terrain.pal'])
    if len(palette) != 48:
        raise ValueError('Cave palette must contain three banks')
    tilemap = sources[TERRAIN + 'terrain.bin']
    if len(tilemap) != 4096:
        raise ValueError('Cave tilemap must contain two horizontal screenblocks')
    # Validate the entire source map, not just the visible crop. Bank 0 uses blank tile 0.
    entries = struct.unpack('<2048H', tilemap)
    for entry in entries:
        tile, bank = entry & 1023, entry >> 12
        if tile >= 84 or bank not in (0, 2, 3, 4) or (bank == 0 and set(tiles[tile]) != {0}):
            raise ValueError('Unsupported cave tile/palette reference')
    full = ui.render_map(tiles, tilemap, {i + 2: palette[i * 16:(i + 1) * 16] for i in range(3)}, 240, 160)
    scene = full[:240 * 112 * 4]
    if set(scene[3::4]) != {255} or set(full[240 * 112 * 4 + 3::4]) != {0}:
        raise ValueError('Unsupported cave scene coverage')
    outputs, assets = {}, []
    for name, h, pixels in [('background-scene', 112, scene), ('background', 160, full)]:
        filename = name + '.png'
        outputs[filename] = ui.png(240, h, pixels)
        assets.append(dict(id=name, texturePath=filename, width=240, height=h, x=0, y=0,

                           opaquePixels=pixels[3::4].count(255), transparentPixels=pixels[3::4].count(0)))
    catalog = dict(schemaVersion=1, profile=PROFILE, repository=REPOSITORY, sourceCommit=COMMIT,
                    display=dict(
                       logicalWidth=240, logicalHeight=160, terrain='BATTLE_TERRAIN_CAVE', terrainId=7,
                       mapIds=['MAP_MT_MOON_1F', 'MAP_MT_MOON_B1F', 'MAP_MT_MOON_B2F'], mapType='MAP_TYPE_UNDERGROUND', mapBattleScene='MAP_BATTLE_SCENE_NORMAL',
                       selector='MAP_TYPE_UNDERGROUND ordinary non-indoor, non-surfable floor',
                       settled=True, backgroundLayer=3, charBaseIndex=2, mapBaseIndex=26,
                       priority=3, paletteMode=0, sourceMapWidth=512, sourceMapHeight=256,
                       sourceTileCount=84, sourceTileDepth=4, sourcePngDepth=4,
                       paletteBanks=[2, 3, 4], paletteRgb8=palette,
                       paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)', sourceOrigin=[0, 0],
                       textureFilter='nearest', preferredTexture='background-scene',
                       drawBefore=['battler sprites', 'healthboxes', 'menus'],
                       groundBasesIncluded=True, additionalGroundBaseTexturesRequired=False),
                   assets=assets, sourceRefs=refs, inputs=lock['files'], limitations=[
                       'Settled ordinary Mt. Moon cave-floor singles only; no entry animation or palette fades.',
                       'The BG3 source already contains both cave ground bases; do not draw duplicate bases.',
                       'Full background retains source transparency below y=112; menus are drawn separately.',
                       'RGB555 bit replication matches the established lab UI display profile; no new emulator capture is claimed.',
                       'No sprite, healthbox, text, audio or battle-rule changes; presentation-only selection is separate from battle rules.',
                       'No ROM download or upstream code execution by this importer.',
                   ])
    outputs['mtmoon-battle-terrain.json'] = canonical(catalog)
    outputs['manifest.json'] = canonical(dict(
        schemaVersion=1, profile=PROFILE, commit=COMMIT,
        inputs=lock['files'], outputs=[dict(path=name)
                                     for name, data in sorted(outputs.items())],
        upstreamScriptsExecuted=False, romDownloaded=False, publiclyUploaded=False))
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-repo', type=pathlib.Path)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/generated/mtmoon-battle-terrain')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/mtmoon-battle-terrain.lock.json')
    parser.add_argument('--project-lock', type=pathlib.Path, default=ROOT / 'content/source-lock.json')
    args = parser.parse_args()
    if args.raw.resolve().is_relative_to(args.output.resolve()) or args.output.resolve().is_relative_to(args.raw.resolve()):
        raise ValueError('Raw and output roots must be disjoint')
    lock, sources = load_inputs(args.lock, args.project_lock, args.raw, args.source_repo)
    outputs = convert(lock, sources)
    targets = [(safe_path(args.output, name), safe_path(args.output, name + '.tmp'), data)
               for name, data in outputs.items()]
    args.output.mkdir(parents=True, exist_ok=True)
    for target, staged, data in targets:
        staged.write_bytes(data)
        staged.replace(target)
    print(json.dumps(dict(status='imported', textures=2,
                          catalog=str(args.output / 'mtmoon-battle-terrain.json'))))


if __name__ == '__main__':
    main()

#!/usr/bin/env python3
"""Import the pinned, settled Route 1 grass battle BG3; never execute upstream code.

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
PROFILE = 'firered-route1-battle-terrain-static-v1'
LOCK_SHA256 = '668f5970002e16c9540f7e69cbe9c89b9146c89d197b0e1111febedca20b057d'
EXPECTED_OUTPUT_HASHES = {
    'background-scene.png': '1517c01c0754425bf4f69b09ce8a1998a585e3d3ace47b2fc4e1c58e6eb77c9a',
    'background.png': 'b910365bb3d6fcc7d41d35ab7265cfb61e1e6e41e4fbf14eef840c2b4b9e6e2e',
    'route1-battle-terrain.json': '05f1929e6f8e2f06902a588302fe948c4cdc07cc00901b3cb3ba3f1d4b02f38a',
    'manifest.json': 'fc6b2eed740b141f49ac415342b5a126d8ce4ab9effb7dc1a3377fcb0139278c',
}
TERRAIN = 'graphics/battle_terrain/grass/'
REQUIRED_PATHS = frozenset([
    'Makefile', 'data/maps/Route1/map.json', 'graphics_file_rules.mk',
    'src/battle_bg.c', 'src/battle_setup.c', 'src/battle_main.c',
    'src/metatile_behavior.c', 'include/constants/battle.h',
    'tools/gbagfx/gfx.c', 'tools/gbagfx/convert_png.c',
    TERRAIN + 'terrain.png', TERRAIN + 'terrain.pal', TERRAIN + 'terrain.bin',
])
_spec = importlib.util.spec_from_file_location('local_lab_ui_pixels', ROOT / 'scripts/import-lab-battle-ui.py')
ui = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ui)
sha = ui.inputs.digest
canonical = ui.inputs.canonical
safe_path = ui.inputs.safe_path
source_set_hash = ui.inputs.source_set_hash


def load_inputs(lock_path, project_path, raw, source_repo=None):
    lock_bytes = pathlib.Path(lock_path).read_bytes()
    if sha(lock_bytes) != LOCK_SHA256:
        raise ValueError('Route 1 terrain lock hash mismatch')
    lock = json.loads(lock_bytes)
    project = json.loads(pathlib.Path(project_path).read_bytes())
    if (lock.get('schemaVersion'), lock.get('profile'), lock.get('repository'), lock.get('commit')) != (1, PROFILE, REPOSITORY, COMMIT):
        raise ValueError('Unsupported Route 1 terrain lock identity')
    if (project['source']['repository'], project['source']['sourceCommit'],
            project['referenceGame']['gameEdition'], project['referenceGame']['romRevision']) != (REPOSITORY, COMMIT, 'FIRERED', 0):
        raise ValueError('Project source/edition/revision differs')
    entries = lock.get('files', [])
    paths = [entry['path'] for entry in entries]
    if len(paths) != len(set(paths)) or set(paths) != REQUIRED_PATHS:
        raise ValueError('Route 1 terrain dependency set differs')
    sources = {}
    for entry in entries:
        path = entry['path']
        target = safe_path(raw, path)
        if type(entry['byteLength']) is not int or not 0 < entry['byteLength'] <= 1_000_000:
            raise ValueError('Invalid source byte length')
        if (not re.fullmatch('[0-9a-f]{64}', entry['sha256'])
                or not re.fullmatch('[0-9a-f]{40}', entry['gitBlobSha1'])
                or entry['sourceUrl'] != f'https://github.com/{REPOSITORY}/blob/{COMMIT}/{path}'):
            raise ValueError('Invalid source hash/URL')
        if source_repo is None:
            data = target.read_bytes()
        else:
            data = subprocess.run(['git', '-C', str(source_repo), 'show', COMMIT + ':' + path],
                                  check=True, capture_output=True, timeout=30).stdout
        ui.inputs.verify(data, entry)
        sources[path] = data
    return lock, sources


def reference(lock, sources, path, symbol, needle):
    source = sources[path].decode()
    if source.count(needle) != 1:
        raise ValueError('Missing/ambiguous source semantics: ' + symbol)
    first = source.count('\n', 0, source.index(needle)) + 1
    last = first + needle.count('\n')
    entry = next(e for e in lock['files'] if e['path'] == path)
    return dict(path=path, symbol=symbol, startLine=first, endLine=last, sha256=entry['sha256'],
                url=entry['sourceUrl'] + f'#L{first}-L{last}')


def semantics(lock, sources):
    route = json.loads(sources['data/maps/Route1/map.json'])
    if (route['id'], route['map_type'], route['battle_scene']) != ('MAP_ROUTE1', 'MAP_TYPE_ROUTE', 'MAP_BATTLE_SCENE_NORMAL'):
        raise ValueError('Route 1 terrain selector differs')
    checks = [
        ('src/battle_setup.c', 'BattleSetup_GetTerrainId: tall grass',
         '    if (MetatileBehavior_IsTallGrass(tileBehavior))\n        return BATTLE_TERRAIN_GRASS;'),
        ('src/metatile_behavior.c', 'MetatileBehavior_IsTallGrass',
         'bool8 MetatileBehavior_IsTallGrass(u8 metatileBehavior)\n{\n    if (metatileBehavior == MB_TALL_GRASS || metatileBehavior == MB_CYCLING_ROAD_PULL_DOWN_GRASS)\n        return TRUE;\n    else\n        return FALSE;\n}'),
        ('include/constants/battle.h', 'BATTLE_TERRAIN_GRASS', '#define BATTLE_TERRAIN_GRASS        0'),
        ('src/battle_bg.c', 'GetBattleTerrainOverride: normal map scene',
         '    battleScene = GetCurrentMapBattleScene();\n    if (battleScene == MAP_BATTLE_SCENE_NORMAL)\n    {\n        return gBattleTerrain;\n    }'),
        ('src/battle_bg.c', 'sBattleTerrainTable[BATTLE_TERRAIN_GRASS]',
         '    [BATTLE_TERRAIN_GRASS] =\n    {\n        .tileset = sBattleTerrainTiles_Grass,\n        .tilemap = sBattleTerrainTilemap_Grass,\n        .entryTileset = sBattleTerrainAnimTiles_Grass,\n        .entryTilemap = sBattleTerrainAnimTilemap_Grass,\n        .palette = sBattleTerrainPalette_Grass\n    },'),
        ('src/battle_bg.c', 'grass tile/palette/map bindings',
         'static const u32 sBattleTerrainPalette_Grass[] = INCBIN_U32("graphics/battle_terrain/grass/terrain.gbapal.lz");\nstatic const u32 sBattleTerrainTiles_Grass[] = INCBIN_U32("graphics/battle_terrain/grass/terrain.4bpp.lz");\nstatic const u32 sBattleTerrainTilemap_Grass[] = INCBIN_U32("graphics/battle_terrain/grass/terrain.bin.lz");'),
        ('src/battle_bg.c', 'gBattleBgTemplates: BG3',
         '        .bg = 3,\n        .charBaseIndex = 2,\n        .mapBaseIndex = 26,\n        .screenSize = 1,\n        .paletteMode = 0,\n        .priority = 3,\n        .baseTile = 0x000'),
        ('src/battle_bg.c', 'LoadBattleTerrainGfx',
         '    // Copy to bg3\n    LZDecompressVram(sBattleTerrainTable[terrain].tileset, (void *)BG_CHAR_ADDR(2));\n    LZDecompressVram(sBattleTerrainTable[terrain].tilemap, (void *)BG_SCREEN_ADDR(26));\n    LoadCompressedPalette(sBattleTerrainTable[terrain].palette, BG_PLTT_ID(2), 3 * PLTT_SIZE_4BPP);'),
        ('src/battle_main.c', 'initial BG3 origin and terrain selection',
         '    gBattle_BG3_X = 0;\n    gBattle_BG3_Y = 0;\n\n    gBattleTerrain = BattleSetup_GetTerrainId();'),
        ('graphics_file_rules.mk', 'grass tile count',
         '$(BATTLETERRAINGFXDIR)/grass/terrain.4bpp: %.4bpp: %.png\n\t$(GFX) $< $@ -num_tiles 98 -Wnum_tiles'),
        ('tools/gbagfx/gfx.c', 'RGB8 to RGB5', '#define DOWNCONVERT_BIT_DEPTH(x) ((x) / 8)'),
    ]
    ui.require(sources['tools/gbagfx/convert_png.c'].decode(), '% (1 << destBitDepth)')
    return [reference(lock, sources, *check) for check in checks]


def convert(lock, sources):
    refs = semantics(lock, sources)
    source_png = sources[TERRAIN + 'terrain.png']
    width, height, indices, _ = ui.indexed_png(source_png)
    if (width, height) != (64, 104):
        raise ValueError('Unsupported grass tile sheet dimensions')
    tiles = ui.tile_list(source_png)[:98]
    palette = ui.palette(sources[TERRAIN + 'terrain.pal'])
    if len(palette) != 48:
        raise ValueError('Grass palette must contain three banks')
    tilemap = sources[TERRAIN + 'terrain.bin']
    if len(tilemap) != 4096:
        raise ValueError('Grass tilemap must contain two horizontal screenblocks')
    # Validate the entire source map, not just the visible crop. Bank 0 uses blank tile 0.
    entries = struct.unpack('<2048H', tilemap)
    for entry in entries:
        tile, bank = entry & 1023, entry >> 12
        if tile >= 98 or bank not in (0, 2, 3, 4) or (bank == 0 and set(tiles[tile]) != {0}):
            raise ValueError('Unsupported grass tile/palette reference')
    full = ui.render_map(tiles, tilemap, {i + 2: palette[i * 16:(i + 1) * 16] for i in range(3)}, 240, 160)
    scene = full[:240 * 112 * 4]
    if set(scene[3::4]) != {255} or set(full[240 * 112 * 4 + 3::4]) != {0}:
        raise ValueError('Unsupported grass scene coverage')
    outputs, assets = {}, []
    for name, h, pixels in [('background-scene', 112, scene), ('background', 160, full)]:
        filename = name + '.png'
        outputs[filename] = ui.png(240, h, pixels)
        assets.append(dict(id=name, texturePath=filename, width=240, height=h, x=0, y=0,
                           sha256=sha(outputs[filename]), rgbaSha256=sha(pixels),
                           opaquePixels=pixels[3::4].count(255), transparentPixels=pixels[3::4].count(0)))
    catalog = dict(schemaVersion=1, profile=PROFILE, repository=REPOSITORY, sourceCommit=COMMIT,
                   fullSourceHash=source_set_hash(lock), display=dict(
                       logicalWidth=240, logicalHeight=160, terrain='BATTLE_TERRAIN_GRASS', terrainId=0,
                       mapId='MAP_ROUTE1', mapType='MAP_TYPE_ROUTE', mapBattleScene='MAP_BATTLE_SCENE_NORMAL',
                       selector='MetatileBehavior_IsTallGrass(player destination tile)',
                       settled=True, backgroundLayer=3, charBaseIndex=2, mapBaseIndex=26,
                       priority=3, paletteMode=0, sourceMapWidth=512, sourceMapHeight=256,
                       sourceTileCount=98, sourceTileDepth=4, sourcePngDepth=4,
                       paletteBanks=[2, 3, 4], paletteRgb8=palette,
                       paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)', sourceOrigin=[0, 0],
                       textureFilter='nearest', preferredTexture='background-scene',
                       drawBefore=['battler sprites', 'healthboxes', 'menus'],
                       groundBasesIncluded=True, additionalGroundBaseTexturesRequired=False),
                   assets=assets, sourceRefs=refs, inputs=lock['files'], limitations=[
                       'Settled ordinary Route 1 tall-grass singles only; no entry animation or palette fades.',
                       'The BG3 source already contains both grass ground bases; do not draw duplicate bases.',
                       'Full background retains source transparency below y=112; menus are drawn separately.',
                       'RGB555 bit replication matches the established lab UI display profile; no new emulator capture is claimed.',
                       'No sprite, healthbox, text, audio or battle-rule changes; native integration is outside this importer.',
                       'Local source research only; no ROM download, upstream execution or public asset distribution.',
                   ])
    outputs['route1-battle-terrain.json'] = canonical(catalog)
    outputs['manifest.json'] = canonical(dict(
        schemaVersion=1, profile=PROFILE, commit=COMMIT, fullSourceHash=source_set_hash(lock),
        inputs=lock['files'], outputs=[dict(path=name, byteLength=len(data), sha256=sha(data))
                                     for name, data in sorted(outputs.items())],
        upstreamScriptsExecuted=False, romDownloaded=False, publiclyUploaded=False))
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-repo', type=pathlib.Path)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/route1-battle-terrain')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/generated/route1-battle-terrain')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/route1-battle-terrain.lock.json')
    parser.add_argument('--project-lock', type=pathlib.Path, default=ROOT / 'content/source-lock.json')
    args = parser.parse_args()
    if args.raw.resolve().is_relative_to(args.output.resolve()) or args.output.resolve().is_relative_to(args.raw.resolve()):
        raise ValueError('Raw and output roots must be disjoint')
    lock, sources = load_inputs(args.lock, args.project_lock, args.raw, args.source_repo)
    outputs = convert(lock, sources)
    if {name: sha(data) for name, data in outputs.items()} != EXPECTED_OUTPUT_HASHES:
        raise ValueError('Route 1 terrain output hash mismatch')
    targets = [(safe_path(args.output, name), safe_path(args.output, name + '.tmp'), data)
               for name, data in outputs.items()]
    args.output.mkdir(parents=True, exist_ok=True)
    for target, staged, data in targets:
        staged.write_bytes(data)
        staged.replace(target)
    print(json.dumps(dict(status='imported', textures=2, fullSourceHash=source_set_hash(lock),
                          catalog=str(args.output / 'route1-battle-terrain.json'),
                          catalogSha256=sha(outputs['route1-battle-terrain.json']),
                          sceneSha256=sha(outputs['background-scene.png']))))


if __name__ == '__main__':
    main()

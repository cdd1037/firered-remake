#!/usr/bin/env python3
"""Import pinned settled Gym/Leader BG3 pixels; no upstream execution or network."""
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
PROFILE = 'firered-gym-battle-terrain-static-v1'
LOCK_SHA256 = '2cdf1d129577cdaffb1923e7e191f7ace203c50b10ed29f79164878efdc28878'
EXPECTED_OUTPUT_HASHES = {
    "gym-scene.png": "3b4dc43162492edf48cbfbf65db03a36efb60e4660b7012f08a17ddf91d6e92f",
    "gym-background.png": "9a2a2c39c009af4e4f1e466c0341406892a1d7f65c2e6da54890ce808b346fe2",
    "leader-scene.png": "3b4dc43162492edf48cbfbf65db03a36efb60e4660b7012f08a17ddf91d6e92f",
    "leader-background.png": "9a2a2c39c009af4e4f1e466c0341406892a1d7f65c2e6da54890ce808b346fe2",
    "gym-battle-terrain.json": "070890afe9fc7fee021d9c93f519b2451c9dae8de6ae47eb77efb2e4d62043c3",
    "manifest.json": "7cd7d583faa5a0d8da3d784f7d10a6159cb8f5b4c8e15ebf731cd9d505055ba2"
}
TERRAIN = 'graphics/battle_terrain/building/'
PALETTES = 'graphics/battle_terrain/indoor/'
REQUIRED_PATHS = frozenset([
    'Makefile', 'data/maps/PewterCity_Gym/map.json', 'data/maps/CeruleanCity_Gym/map.json',
    'graphics_file_rules.mk', 'src/battle_bg.c', 'src/battle_main.c', 'include/constants/battle.h',
    'tools/gbagfx/gfx.c', 'tools/gbagfx/convert_png.c', TERRAIN + 'terrain.png',
    TERRAIN + 'terrain.bin', PALETTES + 'gym.pal', PALETTES + 'leader.pal',
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
        raise ValueError('Gym terrain lock hash mismatch')
    lock = json.loads(lock_bytes)
    project = json.loads(pathlib.Path(project_path).read_bytes())
    if (lock.get('schemaVersion'), lock.get('profile'), lock.get('repository'), lock.get('commit')) != (1, PROFILE, REPOSITORY, COMMIT):
        raise ValueError('Unsupported Gym terrain lock identity')
    if (project['source']['repository'], project['source']['sourceCommit'],
            project['referenceGame']['gameEdition'], project['referenceGame']['romRevision']) != (REPOSITORY, COMMIT, 'FIRERED', 0):
        raise ValueError('Project source/edition/revision differs')
    entries = lock.get('files', [])
    paths = [entry['path'] for entry in entries]
    if len(paths) != len(set(paths)) or set(paths) != REQUIRED_PATHS:
        raise ValueError('Gym terrain dependency set differs')
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
        data = target.read_bytes() if source_repo is None else subprocess.run(
            ['git', '-C', str(source_repo), 'show', COMMIT + ':' + path],
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
    for name, map_id in [('PewterCity_Gym', 'MAP_PEWTER_CITY_GYM'), ('CeruleanCity_Gym', 'MAP_CERULEAN_CITY_GYM')]:
        source = json.loads(sources[f'data/maps/{name}/map.json'])
        if (source['id'], source['map_type'], source['battle_scene']) != (map_id, 'MAP_TYPE_INDOOR', 'MAP_BATTLE_SCENE_GYM'):
            raise ValueError('Gym terrain selector differs')
    checks = [
        ('include/constants/battle.h', 'Gym/Leader terrain identifiers',
         '#define BATTLE_TERRAIN_GYM         11\n#define BATTLE_TERRAIN_LEADER      12'),
        ('src/battle_bg.c', 'Gym map scene mapping',
         '    {MAP_BATTLE_SCENE_GYM,      BATTLE_TERRAIN_GYM},'),
        ('src/battle_bg.c', 'GetBattleTerrainOverride: leader precedes map scene',
         '    else if (gBattleTypeFlags & BATTLE_TYPE_TRAINER)\n    {\n        if (gTrainers[gTrainerBattleOpponent_A].trainerClass == TRAINER_CLASS_LEADER)\n            return BATTLE_TERRAIN_LEADER;\n        else if (gTrainers[gTrainerBattleOpponent_A].trainerClass == TRAINER_CLASS_CHAMPION)\n            return BATTLE_TERRAIN_CHAMPION;\n    }\n    battleScene = GetCurrentMapBattleScene();'),
        ('src/battle_bg.c', 'building tiles and map binding',
         'static const u32 sBattleTerrainTiles_Building[] = INCBIN_U32("graphics/battle_terrain/building/terrain.4bpp.lz");\nstatic const u32 sBattleTerrainTilemap_Building[] = INCBIN_U32("graphics/battle_terrain/building/terrain.bin.lz");'),
        ('src/battle_bg.c', 'Gym/Leader palette bindings',
         'static const u32 sBattleTerrainPalette_Gym[] = INCBIN_U32("graphics/battle_terrain/indoor/gym.gbapal.lz");\nstatic const u32 sBattleTerrainPalette_Leader[] = INCBIN_U32("graphics/battle_terrain/indoor/leader.gbapal.lz");'),
        ('src/battle_bg.c', 'gBattleBgTemplates: BG3',
         '        .bg = 3,\n        .charBaseIndex = 2,\n        .mapBaseIndex = 26,\n        .screenSize = 1,\n        .paletteMode = 0,\n        .priority = 3,\n        .baseTile = 0x000'),
        ('src/battle_bg.c', 'LoadBattleTerrainGfx',
         '    // Copy to bg3\n    LZDecompressVram(sBattleTerrainTable[terrain].tileset, (void *)BG_CHAR_ADDR(2));\n    LZDecompressVram(sBattleTerrainTable[terrain].tilemap, (void *)BG_SCREEN_ADDR(26));\n    LoadCompressedPalette(sBattleTerrainTable[terrain].palette, BG_PLTT_ID(2), 3 * PLTT_SIZE_4BPP);'),
        ('src/battle_main.c', 'initial BG3 origin and terrain selection',
         '    gBattle_BG3_X = 0;\n    gBattle_BG3_Y = 0;\n\n    gBattleTerrain = BattleSetup_GetTerrainId();'),
        ('graphics_file_rules.mk', 'building tile count',
         '$(BATTLETERRAINGFXDIR)/building/terrain.4bpp: %.4bpp: %.png\n\t$(GFX) $< $@ -num_tiles 77 -Wnum_tiles'),
        ('tools/gbagfx/gfx.c', 'RGB8 to RGB5', '#define DOWNCONVERT_BIT_DEPTH(x) ((x) / 8)'),
    ]
    for name in ('Gym', 'Leader'):
        checks.append(('src/battle_bg.c', f'sBattleTerrainTable[BATTLE_TERRAIN_{name.upper()}]',
                       f'    [BATTLE_TERRAIN_{name.upper()}] =\n    {{\n        .tileset = sBattleTerrainTiles_Building,\n        .tilemap = sBattleTerrainTilemap_Building,\n        .entryTileset = sBattleTerrainAnimTiles_Building,\n        .entryTilemap = sBattleTerrainAnimTilemap_Building,\n        .palette = sBattleTerrainPalette_{name}\n    }},'))
    ui.require(sources['tools/gbagfx/convert_png.c'].decode(), '% (1 << destBitDepth)')
    return [reference(lock, sources, *check) for check in checks]


def convert(lock, sources):
    refs = semantics(lock, sources)
    source_png = sources[TERRAIN + 'terrain.png']
    width, height, _, _ = ui.indexed_png(source_png)
    if (width, height) != (64, 80):
        raise ValueError('Unsupported building tile sheet dimensions')
    tiles = ui.tile_list(source_png)[:77]
    tilemap = sources[TERRAIN + 'terrain.bin']
    if len(tilemap) != 4096:
        raise ValueError('Building tilemap must contain two horizontal screenblocks')
    for entry in struct.unpack('<2048H', tilemap):
        tile, bank = entry & 1023, entry >> 12
        if tile >= 77 or bank not in (0, 2, 3, 4) or (bank == 0 and set(tiles[tile]) != {0}):
            raise ValueError('Unsupported building tile/palette reference')
    outputs, assets, variants = {}, [], []
    for variant, terrain_id in [('gym', 11), ('leader', 12)]:
        palette = ui.palette(sources[PALETTES + variant + '.pal'])
        if len(palette) != 48:
            raise ValueError('Gym/Leader palette must contain three banks')
        full = ui.render_map(tiles, tilemap, {i + 2: palette[i * 16:(i + 1) * 16] for i in range(3)}, 240, 160)
        scene = full[:240 * 112 * 4]
        if set(scene[3::4]) != {255} or set(full[240 * 112 * 4 + 3::4]) != {0}:
            raise ValueError('Unsupported Gym/Leader scene coverage')
        for suffix, h, pixels in [('scene', 112, scene), ('background', 160, full)]:
            name = variant + '-' + suffix
            filename = name + '.png'
            outputs[filename] = ui.png(240, h, pixels)
            assets.append(dict(id=name, texturePath=filename, width=240, height=h, x=0, y=0,
                               byteLength=len(outputs[filename]), sha256=sha(outputs[filename]), rgbaSha256=sha(pixels),
                               opaquePixels=pixels[3::4].count(255), transparentPixels=pixels[3::4].count(0)))
        variants.append(dict(id=variant, terrain='BATTLE_TERRAIN_' + variant.upper(), terrainId=terrain_id,
                             paletteSource=PALETTES + variant + '.pal', paletteRgb8=palette, sceneTexture=variant + '-scene'))
    catalog = dict(schemaVersion=1, profile=PROFILE, repository=REPOSITORY, sourceCommit=COMMIT,
                   fullSourceHash=source_set_hash(lock), display=dict(
                       logicalWidth=240, logicalHeight=160,
                       mapIds=['MAP_PEWTER_CITY_GYM', 'MAP_CERULEAN_CITY_GYM'],
                       mapType='MAP_TYPE_INDOOR', mapBattleScene='MAP_BATTLE_SCENE_GYM',
                       selector='TRAINER_CLASS_LEADER overrides Gym map scene; ordinary Gym trainers use Gym palette',
                       settled=True, backgroundLayer=3, charBaseIndex=2, mapBaseIndex=26,
                       priority=3, paletteMode=0, sourceMapWidth=512, sourceMapHeight=256,
                       sourceTileCount=77, sourceTileDepth=4, paletteBanks=[2, 3, 4],
                       paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)', sourceOrigin=[0, 0],
                       textureFilter='nearest', drawBefore=['battler sprites', 'healthboxes', 'menus'],
                       groundBasesIncluded=True, additionalGroundBaseTexturesRequired=False),
                   variants=variants, assets=assets, sourceRefs=refs, inputs=lock['files'], limitations=[
                       'Settled Gym/Leader singles background only; no entry animation or palette fades.',
                       'Current application selection is bounded to Pewter and Cerulean Gyms; additional gyms are not inferred.',
                       'BG3 already contains both ground bases; do not draw duplicate bases.',
                       'Full backgrounds retain source transparency below y=112; menus are drawn separately.',
                       'RGB555 bit replication follows the existing display profile; no emulator capture comparison is claimed.',
                       'No battle rule, sprite, healthbox, audio, Godot renderer or runtime selector changes in this content import.',
                       'No ROM download or upstream code execution.',
                   ])
    outputs['gym-battle-terrain.json'] = canonical(catalog)
    outputs['manifest.json'] = canonical(dict(
        schemaVersion=1, profile=PROFILE, commit=COMMIT, fullSourceHash=source_set_hash(lock),
        inputs=lock['files'], outputs=[dict(path=name, byteLength=len(data), sha256=sha(data))
                                     for name, data in sorted(outputs.items())],
        upstreamScriptsExecuted=False, romDownloaded=False, publiclyUploaded=False))
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-repo', type=pathlib.Path)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/gym-battle-terrain')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/generated/gym-battle-terrain')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/gym-battle-terrain.lock.json')
    parser.add_argument('--project-lock', type=pathlib.Path, default=ROOT / 'content/source-lock.json')
    args = parser.parse_args()
    if args.raw.resolve().is_relative_to(args.output.resolve()) or args.output.resolve().is_relative_to(args.raw.resolve()):
        raise ValueError('Raw and output roots must be disjoint')
    lock, sources = load_inputs(args.lock, args.project_lock, args.raw, args.source_repo)
    outputs = convert(lock, sources)
    if {name: sha(data) for name, data in outputs.items()} != EXPECTED_OUTPUT_HASHES:
        raise ValueError('Gym terrain output hash mismatch')
    # Preflight all destination paths before any writes. Exact Git imports also retain the raw closure.
    writes = [(safe_path(args.output, name), data) for name, data in outputs.items()]
    if args.source_repo is not None:
        writes += [(safe_path(args.raw, name), data) for name, data in sources.items()]
    targets = [(target, safe_path(target.parent, target.name + '.tmp'), data) for target, data in writes]
    for target, staged, data in targets:
        target.parent.mkdir(parents=True, exist_ok=True)
        staged.write_bytes(data)
        staged.replace(target)
    print(json.dumps(dict(status='imported', textures=4, fullSourceHash=source_set_hash(lock),
                          catalog=str(args.output / 'gym-battle-terrain.json'), outputHashes=EXPECTED_OUTPUT_HASHES)))


if __name__ == '__main__':
    main()

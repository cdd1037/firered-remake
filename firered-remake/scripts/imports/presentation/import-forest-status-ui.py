#!/usr/bin/env python3
"""Convert pinned single-battle and party status tiles offline; never execute upstream code."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
import argparse
import importlib.util
import json
import pathlib
import re
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
REPOSITORY = 'pret/pokefirered'
PROFILE = 'firered-forest-status-ui-v1'
# Reviewed after an independent PIL source-pixel reconstruction.
BATTLE = 'graphics/battle_interface/'
PARTY = 'graphics/interface/status_icons.png'
PATHS = frozenset([
    'Makefile', 'graphics_file_rules.mk', 'data/maps/ViridianForest/map.json', 'data/maps/Route1/map.json',
    'src/battle_interface.c', 'src/party_menu.c', 'src/data/party_menu.h', 'src/graphics.c',
    'src/battle_setup.c', 'src/battle_bg.c', 'src/metatile_behavior.c',
    'src/battle_message.c', 'src/battle_util.c', 'data/battle_scripts_1.s',
    'include/constants/battle.h', 'include/constants/party_menu.h',
    'tools/gbagfx/gfx.c', 'tools/gbagfx/convert_png.c',
    BATTLE + 'healthbox_elements.png', BATTLE + 'healthbox.pal', BATTLE + 'healthbar.pal', PARTY,
])
spec = importlib.util.spec_from_file_location('forest_status_codec', ROOT / 'scripts/imports/presentation/import-lab-battle-ui.py')
codec = importlib.util.module_from_spec(spec)
spec.loader.exec_module(codec)


def canonical(value):
    return (json.dumps(value, sort_keys=True, indent=2) + '\n').encode()


def pairs(entries):
    result = {}
    for key, value in entries:
        if key in result:
            raise ValueError('Duplicate JSON property: ' + key)
        result[key] = value
    return result


def safe(root, relative):
    root = pathlib.Path(root).absolute()
    parts = pathlib.PurePosixPath(relative)
    if not relative or parts.is_absolute() or '..' in parts.parts or '\\' in relative:
        raise ValueError('Unsafe forest status path: ' + relative)
    target = root / relative
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError('Escaping forest status path: ' + relative)
    if any(p.is_symlink() for p in [target, *target.parents]):
        raise ValueError('Forest status symlink rejected: ' + relative)
    return target


def load_lock(path=ROOT / 'scripts/recipes/presentation/forest-status-ui.lock.json', project_path=ROOT / 'scripts/recipes/config/source.json'):
    data = pathlib.Path(path).read_bytes()
    lock = json.loads(data, object_pairs_hook=pairs)
    if (lock['schemaVersion'], lock['profile'], lock['repository'], lock['commit']) != (1, PROFILE, REPOSITORY, COMMIT):
        raise ValueError('Forest status source identity differs')
    if len(lock['files']) != len(PATHS) or {e['path'] for e in lock['files']} != PATHS:
        raise ValueError('Forest status source set differs')
    project = json.loads(pathlib.Path(project_path).read_bytes(), object_pairs_hook=pairs)
    if (project['source']['repository'], project['source']['sourceCommit'],
            project['referenceGame']['gameEdition'], project['referenceGame']['romRevision']) != (REPOSITORY, COMMIT, 'FIRERED', 0):
        raise ValueError('Forest status project edition/revision differs')
    for entry in lock['files']:
        if (entry['sourceUrl'] != f"https://github.com/{REPOSITORY}/blob/{COMMIT}/{entry['path']}"):
            raise ValueError('Forest status source metadata differs')
    return lock


def read_sources(lock, raw, repository=None):
    sources = {}
    for entry in lock['files']:
        target = safe(raw, entry['path'])
        data = target.read_bytes() if repository is None else subprocess.run(
            ['git', '-C', str(repository), 'show', COMMIT + ':' + entry['path']],
            check=True, capture_output=True, timeout=30).stdout
        sources[entry['path']] = data
    return sources


def semantics(lock, sources):

    def require(path, symbol, needle):
        text = sources[path].decode()
        if text.count(needle) != 1:
            raise ValueError('Missing/ambiguous forest status source semantics: ' + symbol)

    for name, map_id in [('ViridianForest', 'MAP_VIRIDIAN_FOREST'), ('Route1', 'MAP_ROUTE1')]:
        path = f'data/maps/{name}/map.json'
        value = json.loads(sources[path], object_pairs_hook=pairs)
        if (value['id'], value['map_type'], value['battle_scene']) != (map_id, 'MAP_TYPE_ROUTE', 'MAP_BATTLE_SCENE_NORMAL'):
            raise ValueError('Forest/Route 1 battle scene differs')
        require(path, map_id + ' normal battle scene', '"battle_scene": "MAP_BATTLE_SCENE_NORMAL"')
    for path, symbol, needle in [
        ('src/battle_setup.c', 'BattleSetup_GetTerrainId tall grass priority',
         '    tileBehavior = MapGridGetMetatileBehaviorAt(x, y);\n    if (MetatileBehavior_IsTallGrass(tileBehavior))\n        return BATTLE_TERRAIN_GRASS;'),
        ('src/metatile_behavior.c', 'MetatileBehavior_IsTallGrass',
         'bool8 MetatileBehavior_IsTallGrass(u8 metatileBehavior)\n{\n    if (metatileBehavior == MB_TALL_GRASS || metatileBehavior == MB_CYCLING_ROAD_PULL_DOWN_GRASS)\n        return TRUE;\n    else\n        return FALSE;\n}'),
        ('src/battle_bg.c', 'GetBattleTerrainOverride normal scene',
         '    battleScene = GetCurrentMapBattleScene();\n    if (battleScene == MAP_BATTLE_SCENE_NORMAL)\n    {\n        return gBattleTerrain;\n    }'),
        ('include/constants/battle.h', 'BATTLE_TERRAIN_GRASS', '#define BATTLE_TERRAIN_GRASS        0'),
        ('src/battle_bg.c', 'sBattleTerrainTable grass',
         '    [BATTLE_TERRAIN_GRASS] =\n    {\n        .tileset = sBattleTerrainTiles_Grass,\n        .tilemap = sBattleTerrainTilemap_Grass,\n        .entryTileset = sBattleTerrainAnimTiles_Grass,\n        .entryTilemap = sBattleTerrainAnimTilemap_Grass,\n        .palette = sBattleTerrainPalette_Grass\n    },'),
        ('src/battle_bg.c', 'grass terrain bindings',
         'static const u32 sBattleTerrainPalette_Grass[] = INCBIN_U32("graphics/battle_terrain/grass/terrain.gbapal.lz");\nstatic const u32 sBattleTerrainTiles_Grass[] = INCBIN_U32("graphics/battle_terrain/grass/terrain.4bpp.lz");\nstatic const u32 sBattleTerrainTilemap_Grass[] = INCBIN_U32("graphics/battle_terrain/grass/terrain.bin.lz");'),
        ('src/graphics.c', 'battle status tile/palette bindings',
         'const u16 gBattleInterface_Healthbox_Pal[] = INCBIN_U16("graphics/battle_interface/healthbox.gbapal");\nconst u16 gBattleInterface_Healthbar_Pal[] = INCBIN_U16("graphics/battle_interface/healthbar.gbapal");\nconst u8 gBattleInterface_Gfx[] = INCBIN_U8("graphics/battle_interface/healthbox_elements.4bpp");'),
        ('src/graphics.c', 'party status tile/palette bindings',
         'const u32 gStatusGfx_Icons[] = INCBIN_U32("graphics/interface/status_icons.4bpp.lz");\nconst u32 gStatusPal_Icons[] = INCBIN_U32("graphics/interface/status_icons.gbapal.lz");'),
        ('src/battle_interface.c', 'single healthbox positions',
         '        if (GetBattlerSide(battler) != B_SIDE_PLAYER)\n            x = 44, y = 30;\n        else\n            x = 158, y = 88;'),
        ('src/battle_interface.c', 'single player status destination tile',
         '        if (!IsDoubleBattle())\n            tileNumAdder = 0x1A;\n        else\n            tileNumAdder = 0x12;'),
        ('src/battle_interface.c', 'opponent status destination tile',
         '        status = GetMonData(&gEnemyParty[gBattlerPartyIndexes[battlerId]], MON_DATA_STATUS);\n        tileNumAdder = 0x11;'),
        ('src/battle_interface.c', 'status palette and three-tile copy',
         '    pltAdder += battlerId + 12;\n\n    FillPalette(sStatusIconColors[statusPalId], pltAdder + OBJ_PLTT_OFFSET, PLTT_SIZEOF(1));'),
        ('src/battle_interface.c', 'opponent status HP-label suppression',
         '    if (IsDoubleBattle() == TRUE || GetBattlerSide(battlerId) == B_SIDE_OPPONENT)\n    {\n        if (!gBattleSpritesDataPtr->battlerData[battlerId].hpNumbersNoBars)\n        {\n            CpuCopy32(GetBattleInterfaceGfxPtr(B_INTERFACE_GFX_TRANSPARENT),\n                     (void *)(OBJ_VRAM0 + gSprites[healthBarSpriteId].oam.tileNum * TILE_SIZE_4BPP),\n                     1 * TILE_SIZE_4BPP);\n            CpuCopy32(GetBattleInterfaceGfxPtr(B_INTERFACE_GFX_HP_BAR_LEFT_BORDER),\n                     (void *)(OBJ_VRAM0 + (gSprites[healthBarSpriteId].oam.tileNum + 1) * TILE_SIZE_4BPP),\n                     1 * TILE_SIZE_4BPP);'),
        ('src/data/party_menu.h', 'party status sprite width/height', '.shape = SPRITE_SHAPE(32x8),\n    .x = 0,\n    .matrixNum = 0,\n    .size = SPRITE_SIZE(32x8),'),
        ('src/party_menu.c', 'party fainting precedes status',
         '    if (GetMonData(mon, MON_DATA_HP) == 0)\n        return AILMENT_FNT;\n    ailment = GetAilmentFromStatus(GetMonData(mon, MON_DATA_STATUS));'),
        ('src/party_menu.c', 'party healthy status hidden',
         '    case AILMENT_NONE:\n    case AILMENT_PKRS:\n        gSprites[menuBox->statusSpriteId].invisible = TRUE;\n        break;\n    default:\n        StartSpriteAnim(&gSprites[menuBox->statusSpriteId], status - 1);'),
        ('data/battle_scripts_1.s', 'BattleScript_ShedSkinActivates status message and icon',
         'BattleScript_ShedSkinActivates::\n\tprintstring STRINGID_PKMNSXCUREDYPROBLEM\n\twaitmessage B_WAIT_TIME_LONG\n\tupdatestatusicon BS_ATTACKER\n\tend3'),
        ('src/battle_message.c', 'Shed Skin source text roles',
         'static const u8 sText_PkmnsXCuredYProblem[] = _("{B_SCR_ACTIVE_NAME_WITH_PREFIX}\'s {B_SCR_ACTIVE_ABILITY}\\ncured its {B_BUFF1} problem!");'),
        ('src/battle_util.c', 'Shed Skin script battler role',
         '                        gBattleScripting.battler = gActiveBattler = battler;\n                        BattleScriptPushCursorAndCallback(BattleScript_ShedSkinActivates);'),
        ('src/battle_util.c', 'Static secondary paralysis effect',
         '            case ABILITY_STATIC:\n                if (!(gMoveResultFlags & MOVE_RESULT_NO_EFFECT)\n                 && gBattleMons[gBattlerAttacker].hp != 0\n                 && !gProtectStructs[gBattlerAttacker].confusionSelfDmg\n                 && TARGET_TURN_DAMAGED\n                 && (gBattleMoves[move].flags & FLAG_MAKES_CONTACT)\n                 && (Random() % 3) == 0)\n                {\n                    gBattleCommunication[MOVE_EFFECT_BYTE] = MOVE_EFFECT_AFFECTS_USER | MOVE_EFFECT_PARALYSIS;'),
        ('src/battle_message.c', 'paralysis ability text selection',
         'const u16 gGotParalyzedStringIds[] =\n{\n    [B_MSG_STATUSED]            = STRINGID_PKMNWASPARALYZED,\n    [B_MSG_STATUSED_BY_ABILITY] = STRINGID_PKMNWASPARALYZEDBY\n};'),
        ('src/battle_message.c', 'status names used by cure text',
         'const u8 gText_Poison[] = _("poison");\nconst u8 gText_Burn[] = _("burn");\nconst u8 gText_Paralysis[] = _("paralysis");'),
        ('Makefile', 'PNG to 4bpp conversion', '%.4bpp:   %.png  ; $(GFX) $< $@'),
        ('Makefile', 'PNG to palette conversion', '%.gbapal: %.png  ; $(GFX) $< $@'),
        ('tools/gbagfx/gfx.c', 'RGB8 to RGB5', '#define DOWNCONVERT_BIT_DEPTH(x) ((x) / 8)'),
        ('tools/gbagfx/convert_png.c', '4bpp modulo conversion', '% (1 << destBitDepth)'),
    ]:
        require(path, symbol, needle)
    for name, token, player_tile, opponent_tile, rgb, party_anim, party_tile, ailment in [
        ('poison', 'PSN', 21, 71, (24, 12, 24), 'Poison', 0, 1),
        ('paralysis', 'PAR', 24, 74, (23, 23, 3), 'Paralyzed', 4, 2),
        ('burn', 'BRN', 33, 83, (28, 14, 10), 'Burn', 16, 5),
    ]:
        for battler, tile in [(0, player_tile), (1, opponent_tile)]:
            src = sources['src/battle_interface.c'].decode()
            matches = re.findall(r'^#define B_INTERFACE_GFX_STATUS_' + token + '_BATTLER' + str(battler) + r'\s+(\d+)\b', src, re.M)
            if matches != [str(tile)]:
                raise ValueError('Status source tile index differs: ' + name)
        require('src/battle_interface.c', 'sStatusIconColors ' + token,
                f'[PAL_STATUS_{token}] = RGB({rgb[0]}, {rgb[1]}, {rgb[2]})')
        require('src/data/party_menu.h', 'party animation ' + name,
                f'static const union AnimCmd sSpriteAnim_Status{party_anim}[] =\n{{\n    ANIMCMD_FRAME({party_tile}, 0),')
        require('include/constants/party_menu.h', 'party ailment ' + name,
                f'#define AILMENT_{"PRZ" if token == "PAR" else token}   {ailment}')
    require('src/data/party_menu.h', 'party faint animation',
            'static const union AnimCmd sSpriteAnim_StatusFaint[] =\n{\n    ANIMCMD_FRAME(24, 0),')
    require('src/data/party_menu.h', 'single party status centers',
            '        { 16,  40,  20,  50,  56,  52,  16,  34},\n        {104,  18, 108,  28, 144,  27, 102,  25},\n        {104,  42, 108,  52, 144,  51, 102,  49},\n        {104,  66, 108,  76, 144,  75, 102,  73},\n        {104,  90, 108, 100, 144,  99, 102,  97},\n        {104, 114, 108, 124, 144, 123, 102, 121},')


def convert(lock, sources):
    if set(sources) != PATHS:
        raise ValueError('Incomplete forest status source data')
    semantics(lock, sources)
    elements = codec.tile_list(sources[BATTLE + 'healthbox_elements.png'])
    if len(elements) != 120:
        raise ValueError('Battle status tile sheet dimensions differ')
    health_palette = codec.palette(sources[BATTLE + 'healthbox.pal'])
    bar_palette = codec.palette(sources[BATTLE + 'healthbar.pal'])
    pw, ph, party_indices, party_rgb = codec.indexed_png(sources[PARTY])
    if (pw, ph) != (256, 8) or len(party_rgb) != 16:
        raise ValueError('Party status sprite dimensions/palette differ')
    party_palette = codec.colors(party_rgb)
    outputs, assets = {}, []

    def add(id, pixels, width, x, y, context, status, source_tile, palette, source_image, source_palette):
        filename = ('battle-' + status if context in ('battle-player', 'battle-opponent') and status in ('poison', 'paralysis', 'burn') else id) + '.png'
        encoded = codec.png(width, 8, pixels)
        if filename in outputs and outputs[filename] != encoded:
            raise ValueError('Shared battle status pixels differ: ' + status)
        outputs[filename] = encoded
        assets.append(dict(id=id, texturePath=filename, width=width, height=8, x=x, y=y, context=context, status=status, sourceTile=source_tile, sourceImagePath=source_image, sourcePalettePath=source_palette, paletteRgb555=[r >> 3 | g >> 3 << 5 | b >> 3 << 10 for r, g, b in palette], transparentIndex=0))

    def party_pixels(tile):
        return codec.rgba(bytes(party_indices[y * pw + tile * 8 + x] for y in range(8) for x in range(32)), party_palette)

    for name, player_tile, opponent_tile, rgb, party_tile in [
        ('poison', 21, 71, (24, 12, 24), 0),
        ('paralysis', 24, 74, (23, 23, 3), 4),
        ('burn', 33, 83, (28, 14, 10), 16),
    ]:
        for side, battler, tile, x, y in [('player', 0, player_tile, 142, 96), ('opponent', 1, opponent_tile, 20, 30)]:
            palette = list(health_palette)
            palette[battler + 12] = tuple((c << 3) | (c >> 2) for c in rgb)
            strip = codec.canvas(24, 8)
            for offset in range(3):
                codec.blit(strip, 24, codec.tile_rgba(elements[tile + offset], palette), 8, 8, offset * 8, 0)
            add('battle-' + side + '-' + name, strip, 24, x, y, 'battle-' + side, name,
                tile, palette, BATTLE + 'healthbox_elements.png', BATTLE + 'healthbox.pal')
        add('party-' + name, party_pixels(party_tile),
            32, 0, 0, 'party', name, party_tile, party_palette, PARTY, PARTY)
    add('battle-opponent-hp-border', codec.tile_rgba(elements[65], bar_palette), 8, 44, 30,
        'battle-opponent', 'any-status-hp-border', 65, bar_palette, BATTLE + 'healthbox_elements.png', BATTLE + 'healthbar.pal')
    add('party-fainted', party_pixels(24), 32, 0, 0,
        'party', 'fainted', 24, party_palette, PARTY, PARTY)
    document = dict(schemaVersion=1,  repository=REPOSITORY,
                      assets=assets,
                    display=dict(logicalWidth=240, logicalHeight=160, textureFilter='nearest',
                                 paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)', playerKeepsHpLabel=True,
                                 opponentSuppressesHpLabel=True, opponentStatusHidesCaughtBall=True,
                                 partyStatusCenters=[dict(x=x, y=y) for x, y in [(56, 52), (144, 27), (144, 51), (144, 75), (144, 99), (144, 123)]],
                                 partyStatusTopLeft=[dict(x=x, y=y) for x, y in [(40, 48), (128, 23), (128, 47), (128, 71), (128, 95), (128, 119)]],
                                 partyFaintedPrecedesStatus=True, partyHealthyIconHidden=True),
                    terrain=dict(mapId='MAP_VIRIDIAN_FOREST', comparisonMapId='MAP_ROUTE1',
                                 mapType='MAP_TYPE_ROUTE', mapBattleScene='MAP_BATTLE_SCENE_NORMAL',
                                 terrain='BATTLE_TERRAIN_GRASS', terrainId=0,
                                 condition='ordinary tall-grass encounter on the player destination tile',
                                 sameAsRoute1TallGrass=True,
                                 texturePath='content/generated/battle/terrain/route1/background-scene.png'))
    outputs['forest-status-ui.json'] = runtime_json_bytes(document, sort_keys=True)
    outputs['manifest.json'] = canonical(dict(schemaVersion=1,
         outputs=[dict(path=name)
            for name, data in sorted(outputs.items())],
        ))
    return outputs


def run(lock_path, project_path, raw, output, repository=None):
    if raw.resolve().is_relative_to(output.resolve()) or output.resolve().is_relative_to(raw.resolve()):
        raise ValueError('Forest status raw/output roots overlap')
    lock = load_lock(lock_path, project_path)
    sources = read_sources(lock, raw, repository)
    outputs = convert(lock, sources)
    targets = [(safe(raw, path), data) for path, data in sources.items()]
    targets += [(safe(output, path), data) for path, data in outputs.items()]
    for target, _ in targets:
        safe(target.parent, target.name + '.tmp')
    for target, data in targets:
        target.parent.mkdir(parents=True, exist_ok=True)
        staged = target.with_name(target.name + '.tmp')
        staged.write_bytes(data)
        staged.replace(target)
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-repo', type=pathlib.Path)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/generated/ui/forest-status-ui')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/recipes/presentation/forest-status-ui.lock.json')
    parser.add_argument('--project-lock', type=pathlib.Path, default=ROOT / 'scripts/recipes/config/source.json')
    args = parser.parse_args()
    outputs = run(args.lock, args.project_lock, args.raw, args.output, args.source_repo)
    print(json.dumps(dict(status='verified', sourceFiles=len(PATHS), assets=11, outputs=len(outputs))))


if __name__ == '__main__':
    main()

#!/usr/bin/env python3
"""Recover the pinned GAMER and freeze source art; execute no upstream code."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes

import argparse
import json
import pathlib
import re
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-route11-art-v1'
REPOSITORY = 'pret/pokefirered'
RAW = 'content/raw/pokefirered/037335f4'
GENERATED = 'content/generated/sprites/trainers/route11'
PATHS = frozenset('''Makefile
src/battle_interface.c
src/party_menu.c
src/data/party_menu.h
src/graphics.c
include/constants/party_menu.h
tools/gbagfx/gfx.c
tools/gbagfx/convert_png.c
graphics/battle_interface/healthbox_elements.png
graphics/battle_interface/healthbox.pal
graphics/interface/status_icons.png
graphics/trainers/front_pics/gamer_front_pic.png
graphics/trainers/palettes/gamer.pal
src/data/graphics/trainers.h
src/data/trainer_graphics/front_pic_tables.h
src/battle_controller_opponent.c
include/constants/trainers.h
src/data/trainers.h'''.splitlines())


def encoded(value): return (json.dumps(value, sort_keys=True, indent=2) + '\n').encode()
def pairs(rows):
    result = {}
    for key, value in rows:
        if key in result: raise ValueError('Duplicate JSON property: ' + key)
        result[key] = value
    return result


def safe(root, relative=None):
    root = pathlib.Path(root)
    if '..' in root.parts: raise ValueError('Parent traversal root rejected')
    root = root.absolute()
    if relative is not None:
        if (not relative or pathlib.PurePosixPath(relative).is_absolute() or '\\' in relative
                or any(p in ('', '.', '..') for p in relative.split('/'))):
            raise ValueError('Unsafe Route11 art path')
        root /= relative
    for part in [root, *root.parents]:
        if part.is_symlink(): raise ValueError('Route11 art symlink rejected')
    return root


def read_lock(path=ROOT / 'scripts/recipes/presentation/route11-art.lock.json'):
    data = safe(path).read_bytes()
    lock = json.loads(data, object_pairs_hook=pairs)
    if (lock['schemaVersion'], lock['profile'], lock['repository'], lock['commit'], lock['gameVersion'], lock['revision']) != (1, PROFILE, REPOSITORY, COMMIT, 'FIRERED', 0):
        raise ValueError('Route11 art source identity differs')
    if len(lock['files']) != len(PATHS) or [e['path'] for e in lock['files']] != sorted(PATHS):
        raise ValueError('Route11 art source inventory differs')
    for e in lock['files']:
        if (e['sourceUrl'] != f"https://github.com/{REPOSITORY}/blob/{COMMIT}/{e['path']}"):
            raise ValueError('Source metadata differs')
    return lock


def read_sources(lock, raw, repository=None):
    sources = {}
    raw = safe(raw)
    if repository:
        repository = safe(repository)
        tree = subprocess.check_output(['git', '-C', str(repository), 'rev-parse', COMMIT + '^{tree}']).decode().strip()
    for entry in lock['files']:
        path = entry['path']; target = safe(raw, path)
        if repository:
            data = subprocess.check_output(['git', '-C', str(repository), 'show', COMMIT + ':' + path])
        else:
            data = target.read_bytes()
        sources[path] = data
    return sources


def convert(lock, sources):
    if set(sources) != PATHS: raise ValueError('Incomplete Route11 art source data')
    import ui_tiles as codec

    def require(path, symbol, needle):
        source = sources[path].decode()
        if source.count(needle) != 1: raise ValueError('Missing/ambiguous source semantics: ' + symbol)

    for path, symbol, needle in SOURCE_REQUIREMENTS: require(path, symbol, needle)
    p = 'src/data/trainer_graphics/front_pic_tables.h'
    coords = re.findall(r'\{\.size = (\d+), \.y_offset = (\d+)\}', sources[p].decode().split('const struct CompressedSpriteSheet')[0])
    if coords[97] != ('8', '1'): raise ValueError('GAMER source coordinate entry differs')
    require(p, 'gTrainerFrontPicCoords', sources[p].decode().split('const struct CompressedSpriteSheet')[0].strip())
    for trainer in ['HUGO', 'JASPER', 'DARIAN', 'DIRK']:
        p = 'src/data/trainers.h'
        matches = re.findall(r'^    \[TRAINER_GAMER_' + trainer + r'\] = \{.*?^    \},', sources[p].decode(), re.M | re.S)
        if len(matches) != 1 or '.trainerPic = TRAINER_PIC_GAMER,' not in matches[0]:
            raise ValueError('Route11 GAMER trainer binding differs')
        require(p, 'TRAINER_GAMER_' + trainer, matches[0])
    outputs, assets = {}, []
    def add(id, indices, palette, width, height, x, y, tile, image, pal, kind):
        pixels = codec.rgba(indices, palette)
        outputs[id + '.png'] = codec.png(width, height, pixels)
        assets.append(dict(id=id, kind=kind, width=width, height=height, x=x, y=y, texturePath=id + '.png', sourceTile=tile, sourceImagePath=image, sourcePalettePath=pal, paletteRgb555=[r >> 3 | g >> 3 << 5 | b >> 3 << 10 for r, g, b in palette], transparentIndex=0))
    image, pal = 'graphics/trainers/front_pics/gamer_front_pic.png', 'graphics/trainers/palettes/gamer.pal'
    w, h, indices, _ = codec.indexed_png(sources[image])
    if (w, h) != (64, 64): raise ValueError('GAMER source dimensions differ')
    add('gamer-front', indices, codec.palette(sources[pal]), 64, 64, 144, 8, 0, image, pal, 'trainer')
    assets[-1].update(sourceTrainerPicId=97, sourceFrame=0, sourceSpriteCenterX=176, sourceSpriteCenterY=40)
    image, pal = 'graphics/battle_interface/healthbox_elements.png', 'graphics/battle_interface/healthbox.pal'
    if codec.indexed_png(sources[image])[:2] != (320, 24): raise ValueError('Battle atlas geometry differs')
    tiles = codec.tile_list(sources[image])
    for side, battler, tile, x, y in [('player', 0, 30, 142, 96), ('opponent', 1, 80, 20, 30)]:
        palette = list(codec.palette(sources[pal]))
        if len(palette) != 16: raise ValueError('Battle palette length differs')
        palette[12 + battler] = tuple((c << 3) | (c >> 2) for c in (17, 22, 28))
        indices = bytes(tiles[tile + px // 8][py * 8 + px % 8] for py in range(8) for px in range(24))
        add('battle-' + side + '-freeze', indices, palette, 24, 8, x, y, tile, image, pal, 'status')
    image = 'graphics/interface/status_icons.png'
    w, h, indices, rgb = codec.indexed_png(sources[image])
    if (w, h, len(rgb)) != (256, 8, 16): raise ValueError('Party atlas differs')
    add('party-freeze', bytes(indices[y * w + 96 + x] for y in range(8) for x in range(32)),
        codec.colors(rgb), 32, 8, 0, 0, 12, image, image, 'status')
    document = dict(schemaVersion=1,  repository=REPOSITORY,
         gameVersion='FIRERED', revision=0, rawRoot=RAW,   assets=assets,
        partyStatusTopLeft=[dict(x=x, y=y) for x, y in [(40, 48), (128, 23), (128, 47), (128, 71), (128, 95), (128, 119)]])
    outputs['route11-art.json'] = runtime_json_bytes(document, sort_keys=True)
    outputs['manifest.json'] = encoded(dict(schemaVersion=1,

        outputs=[dict(path=p) for p, b in sorted(outputs.items())],
          ))
    return outputs


SOURCE_REQUIREMENTS = [('src/battle_interface.c', 'player FRZ tile', '#define B_INTERFACE_GFX_STATUS_FRZ_BATTLER0     30'),
 ('src/battle_interface.c', 'opponent FRZ tile', '#define B_INTERFACE_GFX_STATUS_FRZ_BATTLER1     80'),
 ('src/battle_interface.c', 'FRZ RGB555 color', '[PAL_STATUS_FRZ] = RGB(17, 22, 28)'),
 ('src/battle_interface.c',
  'freeze status selection',
  '    else if (status & STATUS1_FREEZE)\n'
  '    {\n'
  '        statusGfxPtr = GetBattleInterfaceGfxPtr(GetStatusIconForBattlerId(B_INTERFACE_GFX_STATUS_FRZ_BATTLER0, '
  'battlerId));\n'
  '        statusPalId = PAL_STATUS_FRZ;\n'
  '    }'),
 ('src/battle_interface.c',
  'single healthbox positions',
  '        if (GetBattlerSide(battler) != B_SIDE_PLAYER)\n'
  '            x = 44, y = 30;\n'
  '        else\n'
  '            x = 158, y = 88;'),
 ('src/battle_interface.c',
  'single player destination tile',
  '        if (!IsDoubleBattle())\n            tileNumAdder = 0x1A;\n        else\n            tileNumAdder = 0x12;'),
 ('src/battle_interface.c',
  'opponent destination tile',
  '        status = GetMonData(&gEnemyParty[gBattlerPartyIndexes[battlerId]], MON_DATA_STATUS);\n'
  '        tileNumAdder = 0x11;'),
 ('src/battle_interface.c',
  'dynamic palette entry',
  '    pltAdder += battlerId + 12;\n'
  '\n'
  '    FillPalette(sStatusIconColors[statusPalId], pltAdder + OBJ_PLTT_OFFSET, PLTT_SIZEOF(1));'),
 ('src/battle_interface.c',
  'three-tile copy',
  '    CpuCopy32(statusGfxPtr, (void *)(OBJ_VRAM0 + (gSprites[healthboxSpriteId].oam.tileNum + tileNumAdder) * '
  'TILE_SIZE_4BPP), 3 * TILE_SIZE_4BPP);'),
 ('src/graphics.c',
  'battle tile binding',
  'const u8 gBattleInterface_Gfx[] = INCBIN_U8("graphics/battle_interface/healthbox_elements.4bpp");'),
 ('src/graphics.c',
  'battle palette binding',
  'const u16 gBattleInterface_Healthbox_Pal[] = INCBIN_U16("graphics/battle_interface/healthbox.gbapal");'),
 ('src/graphics.c',
  'party graphics and palette binding',
  'const u32 gStatusGfx_Icons[] = INCBIN_U32("graphics/interface/status_icons.4bpp.lz");\n'
  'const u32 gStatusPal_Icons[] = INCBIN_U32("graphics/interface/status_icons.gbapal.lz");'),
 ('src/data/party_menu.h',
  'party freeze tile',
  'static const union AnimCmd sSpriteAnim_StatusFrozen[] =\n{\n    ANIMCMD_FRAME(12, 0),\n    ANIMCMD_END\n};'),
 ('src/data/party_menu.h',
  'party animation index 3',
  '    sSpriteAnim_StatusPoison,\n'
  '    sSpriteAnim_StatusParalyzed,\n'
  '    sSpriteAnim_StatusSleep,\n'
  '    sSpriteAnim_StatusFrozen,'),
 ('src/data/party_menu.h',
  'party 32x8 sprite',
  '.shape = SPRITE_SHAPE(32x8),\n    .x = 0,\n    .matrixNum = 0,\n    .size = SPRITE_SIZE(32x8),'),
 ('src/data/party_menu.h',
  'single party icon centers',
  '        { 16,  40,  20,  50,  56,  52,  16,  34},\n'
  '        {104,  18, 108,  28, 144,  27, 102,  25},\n'
  '        {104,  42, 108,  52, 144,  51, 102,  49},\n'
  '        {104,  66, 108,  76, 144,  75, 102,  73},\n'
  '        {104,  90, 108, 100, 144,  99, 102,  97},\n'
  '        {104, 114, 108, 124, 144, 123, 102, 121},'),
 ('include/constants/party_menu.h', 'party freeze ailment', '#define AILMENT_FRZ   4'),
 ('src/party_menu.c', 'party freeze selection', '    if (status & STATUS1_FREEZE)\n        return AILMENT_FRZ;'),
 ('src/party_menu.c',
  'party status animation dispatch',
  '        StartSpriteAnim(&gSprites[menuBox->statusSpriteId], status - 1);'),
 ('src/party_menu.c',
  'fainting precedes freeze',
  '    if (GetMonData(mon, MON_DATA_HP) == 0)\n        return AILMENT_FNT;'),
 ('Makefile', 'PNG to 4bpp conversion', '%.4bpp:   %.png  ; $(GFX) $< $@'),
 ('Makefile', 'PNG to palette conversion', '%.gbapal: %.png  ; $(GFX) $< $@'),
 ('tools/gbagfx/gfx.c', 'RGB8 to RGB5', '#define DOWNCONVERT_BIT_DEPTH(x) ((x) / 8)'),
 ('tools/gbagfx/convert_png.c', '4bpp source index modulo', '% (1 << destBitDepth)'),
 ('include/constants/trainers.h', 'TRAINER_PIC_GAMER', '#define TRAINER_PIC_GAMER                  97'),
 ('src/data/graphics/trainers.h',
  'GAMER image binding',
  'gTrainerFrontPic_Gamer[] = INCBIN_U32("graphics/trainers/front_pics/gamer_front_pic.4bpp.lz")'),
 ('src/data/graphics/trainers.h',
  'GAMER palette binding',
  'gTrainerPalette_Gamer[] = INCBIN_U32("graphics/trainers/palettes/gamer.gbapal.lz")'),
 ('src/data/trainer_graphics/front_pic_tables.h',
  'GAMER sprite table',
  'TRAINER_SPRITE(GAMER, gTrainerFrontPic_Gamer, 0x800)'),
 ('src/data/trainer_graphics/front_pic_tables.h', 'GAMER palette table', 'TRAINER_PAL(GAMER, gTrainerPalette_Gamer)'),
 ('src/battle_controller_opponent.c',
  'single trainer sprite center',
  'CreateSprite(&gMultiuseSpriteTemplate,\n'
  '                                                     176,\n'
  '                                                     (8 - gTrainerFrontPicCoords[trainerPicId].size) * 4 + 40,\n'
  '                                                     GetBattlerSpriteSubpriority(gActiveBattler));')]


def run(lock_path, raw, output, repository=None):
    raw, output = safe(raw), safe(output)
    if raw == output or raw.is_relative_to(output) or output.is_relative_to(raw):
        raise ValueError('Route11 art raw/output roots overlap')
    lock = read_lock(lock_path); sources = read_sources(lock, raw, repository)
    outputs = convert(lock, sources)
    writes = [(safe(output, p), b) for p, b in outputs.items()]
    if repository: writes += [(safe(raw, p), b) for p, b in sources.items()] + []
    for target, _ in writes: safe(target.parent, target.name + '.tmp')
    for target, data in writes:
        target.parent.mkdir(parents=True, exist_ok=True)
        staged = target.with_name(target.name + '.tmp'); staged.write_bytes(data); staged.replace(target)
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / RAW)
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / GENERATED)
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/recipes/presentation/route11-art.lock.json')
    parser.add_argument('--reference-root', type=pathlib.Path)
    args = parser.parse_args()
    result = run(args.lock, args.raw, args.output, args.reference_root)
    print(json.dumps(dict(status='verified', sourceFiles=len(PATHS), assets=4, outputs=len(result))))


if __name__ == '__main__': main()

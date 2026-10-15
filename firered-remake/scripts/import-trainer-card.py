#!/usr/bin/env python3
"""Authenticated static FireRed zero-star Trainer Card and display metadata.

Only immutable local Git objects or verified raw cache bytes are read. No upstream
code, ROM, network, runtime shaders, battle effects or evolution executor is used.
"""
import sys as _path_sys
from pathlib import Path as _SourcePath
_script_directory = str(_SourcePath(__file__).resolve().parent)
if _script_directory not in _path_sys.path:
    _path_sys.path.insert(0, _script_directory)
from raw_source_paths import raw_metadata_directory
import argparse
import importlib.util
import json
import os
import pathlib
import re
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-trainer-card-v1'
PATHS = frozenset("""src/trainer_card.c
src/trainer_pokemon_sprites.c
src/strings.c
src/graphics.c
src/pokedex.c
src/event_data.c
src/data/graphics/trainers.h
src/data/trainer_graphics/front_pic_tables.h
src/data/pokemon/trainer_class_lookups.h
src/window.c
src/blit.c
include/trainer_card.h
include/constants/trainer_card.h
include/constants/pokedex.h
include/constants/flags.h
include/characters.h
include/text.h
Makefile
tools/gbagfx/gfx.c
tools/gbagfx/convert_png.c
graphics/trainer_card/front.bin
graphics/trainer_card/back.bin
graphics/trainer_card/bg.bin
graphics/trainer_card/tiles.png
graphics/trainer_card/blue.pal
graphics/trainer_card/female_bg.pal
graphics/trainer_card/badges.png
graphics/trainers/front_pics/red_front_pic.png
graphics/trainers/front_pics/leaf_front_pic.png
graphics/trainers/palettes/red.pal
graphics/trainers/palettes/leaf.pal""".splitlines())
_spec = importlib.util.spec_from_file_location('trainer_card_pixels', ROOT / 'scripts/import-lab-battle-ui.py')
pixels = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(pixels)


def canonical(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode('utf-8')


def unique_pairs(values):
    result = {}
    for key, value in values:
        if key in result:
            raise ValueError('Duplicate JSON member: ' + key)
        result[key] = value
    return result


def safe(root, relative=None):
    root = pathlib.Path(root).absolute()
    if relative is None:
        target = root
    else:
        path = pathlib.PurePosixPath(relative)
        if not relative or relative == '.' or path.is_absolute() or '..' in path.parts or '\\' in relative or str(path) != relative:
            raise ValueError('Unsafe trainer card path: ' + relative)
        target = root / relative
    if any(p.is_symlink() for p in [target, *target.parents]):
        raise ValueError('Symlink input/output or ancestor rejected: ' + str(target))
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError('Trainer card path escapes root')
    return target


def load_lock(path=None):
    path = path or ROOT / 'scripts/fetch-trainer-card.lock.json'
    data = safe(path).read_bytes()
    lock = json.loads(data, object_pairs_hook=unique_pairs)
    if (lock.get('schemaVersion'), lock.get('repository'), lock.get('commit'), lock.get('profile'), lock.get('gameEdition'), lock.get('romRevision')) != (1, 'pret/pokefirered', COMMIT, PROFILE, 'FIRERED', 0):
        raise ValueError('Trainer card source identity differs')
    if len(lock['files']) != len(PATHS) or {e['path'] for e in lock['files']} != PATHS:
        raise ValueError('Trainer card source dependency set differs')
    return lock


def raw_manifest(lock):
    return canonical(dict(schemaVersion=1, profile=PROFILE, commit=COMMIT,

                          inputCount=len(lock['files'])))


def read_sources(lock, raw, repository=None):
    safe(raw)
    sources = {}
    if repository is not None:
        tree = subprocess.run(['git', '-C', str(repository), 'rev-parse', COMMIT + '^{tree}'], check=True, capture_output=True, timeout=30).stdout.decode('ascii').strip()
    for entry in lock['files']:
        target = safe(raw, entry['path'])
        if repository is None:
            data = target.read_bytes()
        else:
            data = subprocess.run(['git', '-C', str(repository), 'show', COMMIT + ':' + entry['path']],
                                  check=True, capture_output=True, timeout=30).stdout
        sources[entry['path']] = data
    return sources


def render_tilemap(tiles, data, palettes):
    import struct
    if len(data) != 30 * 20 * 2:
        raise ValueError('Unexpected Trainer Card tilemap dimensions')
    result = pixels.canvas(240, 160)
    for offset, entry in enumerate(struct.unpack('<600H', data)):
        number, bank = entry & 1023, entry >> 12
        if number >= len(tiles) or bank not in palettes:
            raise ValueError('Trainer Card tile/palette outside source')
        rgba = pixels.tile_rgba(tiles[number], palettes[bank], bool(entry & 1024), bool(entry & 2048))
        pixels.blit(result, 240, rgba, 8, 8, (offset % 30) * 8, (offset // 30) * 8)
    return bytes(result)


def overlay(destination, width, source, sw, sh, x=0, y=0):
    if x < 0 or y < 0 or x + sw > width or y + sh > len(destination)//width//4:
        raise ValueError('Trainer Card overlay outside image')
    for row in range(sh):
        for col in range(sw):
            start = (row*sw+col)*4
            if source[start+3]:
                target = ((row+y)*width+col+x)*4
                destination[target:target+4] = source[start:start+4]


def convert( sources):
    assets, outputs = [], {}
    code = sources['src/trainer_card.c'].decode('utf-8')
    def require(path, needle):
        if needle not in sources[path].decode('utf-8'):
            raise ValueError('Unsupported Trainer Card semantics: ' + needle)
    for needle in [
        'gKantoTrainerCardBlue_Pal,   // Default (0 stars)',
        'sTrainerCardFrontNameXPositions[] = {0x14, 0x10}',
        'sTrainerCardFrontNameYPositions[] = {0x1D, 0x21}',
        'sTrainerCardIdXPositions[] = {0x8E, 0x80}',
        'sTrainerCardIdYPositions[] = {0xA, 0x9}',
        '.tilemapLeft = 1,\n        .tilemapTop = 1,',
        '.tilemapLeft = 19,\n        .tilemapTop = 5,\n        .width = 9,\n        .height = 10,',
        '[MALE]   = {13, 4}', '[FEMALE] = {13, 4}',
        'x = -122 - 6 * StringLength(buffer);', 'x = -120 - 6 * StringLength(buffer);',
        'sTrainerCardFontIds[1], 20, 56,', 'sTrainerCardFontIds[1], 20, 72,',
        'sTrainerCardFontIds[1], 20, 88,',
        'sTrainerCardTimeHoursXPositions[] = {0x65, 0x55}',
        'sTrainerCardTimeHoursYPositions[] = {0x77, 0x67}',
        'sTrainerCardTimeMinutesXPositions[] = {0x7C, 0x6C}',
        'sTrainerCardTimeMinutesYPositions[] = {0x58, 0x59}',
        'sTrainerCardBackNameXPositions[] = {0x8A, 0xD8}',
        'sTrainerCardBackNameYPositions[] = {0xB, 0xA}',
        'trainerCard->rse.trainerId = (gSaveBlock2Ptr->playerTrainerId[1] << 8) | gSaveBlock2Ptr->playerTrainerId[0];',
        'STR_CONV_MODE_LEADING_ZEROS, 5', 'STR_CONV_MODE_LEFT_ALIGN, 6',
        'if (FlagGet(FLAG_SYS_POKEDEX_GET))',
        'if (IsNationalPokedexEnabled())\n        return GetNationalPokedexCount(FLAG_GET_CAUGHT);\n    else\n        return GetKantoPokedexCount(FLAG_GET_CAUGHT);',
        'badgeFlag = FLAG_BADGE01_GET; badgeFlag <= FLAG_BADGE08_GET; badgeFlag++, i++',
        'for (i = 0; i < NUM_BADGES; i++, tileNum += 2, x += 3)',
        'FillBgTilemapBufferRect(3, tileNum, x, 16, 1, 1, palNum);',
        'LoadPalette(sKantoTrainerCardFemaleBg_Pal, BG_PLTT_ID(1), sizeof(sKantoTrainerCardFemaleBg_Pal));']:
        require('src/trainer_card.c', needle)
    require('include/constants/trainer_card.h', '#define CARD_TYPE_FRLG     0')
    require('src/event_data.c', 'gSaveBlock2Ptr->pokedex.nationalMagic == 0xB9\n            && VarGet(VAR_NATIONAL_DEX) == 0x6258\n            && FlagGet(FLAG_SYS_NATIONAL_DEX)')
    require('src/trainer_pokemon_sprites.c', 'BlitBitmapRectToWindow(windowId, framePics, 0, 0, 0x40, 0x40, destX, destY, 0x40, 0x40);')
    require('src/blit.c', 'if (dst->width - dstX < width)')
    require('tools/gbagfx/convert_png.c', '% (1 << destBitDepth)')

    def add(identifier, w, h, rgba, x=0, y=0, input_paths=()):
        if len(rgba) != w*h*4:
            raise ValueError('Trainer Card RGBA size differs')
        png_path = identifier + '.png'
        outputs[png_path] = pixels.png(w, h, rgba)
        assets.append(dict(id=identifier, texturePath=png_path, width=w, height=h, x=x, y=y, sourcePaths=list(input_paths)))

    base = 'graphics/trainer_card/'
    tiles = pixels.tile_list(sources[base+'tiles.png'])
    if len(tiles) != 192:
        raise ValueError('Unexpected Trainer Card tile count')
    blue = pixels.palette(sources[base+'blue.pal'])
    female = pixels.palette(sources[base+'female_bg.pal'])
    if (len(blue),len(female)) != (48,16):
        raise ValueError('Unexpected Trainer Card palette size')
    for gender in ('male','female'):
        palettes = {0:blue[:16],1:blue[16:32] if gender=='male' else female,2:blue[32:48]}
        background = render_tilemap(tiles,sources[base+'bg.bin'],palettes)
        for side in ('front','back'):
            rgba = pixels.canvas(240,160,(*blue[0],255))
            overlay(rgba,240,background,240,160)
            overlay(rgba,240,render_tilemap(tiles,sources[base+side+'.bin'],palettes),240,160)
            paths=[base+'tiles.png',base+'blue.pal',base+'bg.bin',base+side+'.bin']
            if gender=='female': paths.append(base+'female_bg.pal')
            add(side+'-'+gender,240,160,rgba,input_paths=paths)
    for trainer in ('red','leaf'):
        path='graphics/trainers/front_pics/'+trainer+'_front_pic.png'
        palette_path='graphics/trainers/palettes/'+trainer+'.pal'
        w,h,indices,_=pixels.indexed_png(sources[path])
        if (w,h)!=(64,64): raise ValueError('Unexpected trainer portrait dimensions')
        require('src/data/graphics/trainers.h', '"'+path.replace('.png','.4bpp.lz')+'"')
        # Source window is 72x80 at (152,40); portrait offset is (13,4).
        # BlitBitmapRect4Bit clips its last five columns at that window boundary.
        portrait=pixels.rgba(indices,pixels.palette(sources[palette_path]))
        clipped=b''.join(portrait[row*256:row*256+59*4] for row in range(64))
        add('portrait-'+trainer,59,64,clipped,165,44,(path,palette_path))
    w,h,indices,palette=pixels.indexed_png(sources[base+'badges.png'])
    if (w,h)!=(128,16) or len(palette)!=16: raise ValueError('Unexpected badge sheet')
    for i in range(8):
        badge=bytes(indices[y*128+i*16+x] for y in range(16) for x in range(16))
        add('badge-'+str(i+1),16,16,pixels.rgba(badge,pixels.colors(palette)),32+i*24,128,(base+'badges.png',))

    names={'name':'gText_TrainerCardName','id':'gText_TrainerCardIDNo','money':'gText_TrainerCardMoney',
        'currency':'gText_TrainerCardYen','pokedex':'gText_TrainerCardPokedex','time':'gText_TrainerCardTime',
        'colon':'gText_Colon2','hallOfFame':'gText_HallOfFameDebut','linkBattles':'gText_LinkBattles',
        'trades':'gText_PokemonTrades','unionRoom':'gText_UnionRoomTradesBattles','berryCrush':'gText_BerryCrushes'}
    strings=sources['src/strings.c'].decode('utf-8'); texts={}
    for key,symbol in names.items():
        matches=re.findall(r'const u8 '+re.escape(symbol)+r'\[\] = _\("([^"\\]*)"\);',strings)
        if len(matches)!=1: raise ValueError('Expected one literal Trainer Card string: '+symbol)
        texts[key]=matches[0]
    point=lambda x,y:dict(x=x,y=y)
    layout=dict(logicalWidth=240,logicalHeight=160,fontId=2,textColor=2,shadowColor=3,
        textWindow=point(8,8),name=point(28,37),id=point(150,18),moneyLabel=point(28,64),moneyRightX=142,
        pokedexLabel=point(28,80),pokedexRightX=144,timeLabel=point(28,96),timeHours=point(109,96),
        timeColon=point(127,96),timeMinutes=point(132,96),backName=point(146,19),
        portraitWindow=dict(x=152,y=40,width=72,height=80),decimalCellWidth=6,
        badges=[dict(index=i+1,flag='FLAG_BADGE%02d_GET'%(i+1),assetId='badge-'+str(i+1),x=32+i*24,y=128) for i in range(8)])
    rules=dict(trainerIdMask=65535,trainerIdDigits=5,maxMoney=999999,kantoDexCount=151,nationalDexCount=386,
        pokedexVisibleFlag='FLAG_SYS_POKEDEX_GET',nationalDexMagic=185,nationalDexVariable='VAR_NATIONAL_DEX',
        nationalDexVariableValue=25176,nationalDexFlag='FLAG_SYS_NATIONAL_DEX',starCount=0,
        playTimePersisted=False,gameStatisticsPersisted=False,
        backRows=[dict(id='hallOfFame',x=18,y=43,valueX=172,gate='any HOF debut time component nonzero'),
                  dict(id='linkBattles',x=18,y=59,valueX=152,gate='link wins or losses nonzero'),
                  dict(id='trades',x=18,y=75,valueX=194,gate='trade count nonzero'),
                  dict(id='unionRoom',x=18,y=91,valueX=194,gate='union-room count nonzero'),
                  dict(id='berryCrush',x=18,y=107,valueX=194,gate='berry-crush points nonzero')])
    refs=[]
    for symbol in ['PrintNameOnCardFront','PrintIdOnCard','PrintMoneyOnCard','GetCaughtMonsCount','PrintPokedexOnCard',
                   'PrintTimeOnCard','SetPlayerCardData','SetDataFromTrainerCard','DrawStarsAndBadgesOnCard','CreateTrainerCardTrainerPic']:
        match=re.search(r'^static [^;\n]+ '+symbol+r'\([^;\n]*\)\n\{',code,re.M)
        if match is None: raise ValueError('Missing source function '+symbol)
        refs.append(dict(path='src/trainer_card.c',symbol=symbol,startLine=code.count('\n',0,match.start())+1))
    outputs['trainer-card.json']=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,
        assets=assets,texts=texts,layout=layout,rules=rules,sourceRefs=refs,
        limitations=['Static FRLG own-card, zero-star graphics only. No card-flip timing or link profile.',
            'Play time and game statistics are not persisted by the current campaign. Do not manufacture them from steps.',
            'Back-side frame is supplied, but statistics and sticker/selected-icon systems remain unimplemented.',
            'Pixel-authenticated source conversion is not evidence of complete native-frame equivalence.']))
    outputs['manifest.json']=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,
        inputCount=len(sources),assetCount=len(assets),
        upstreamScriptsExecuted=False,romDownloaded=False,outputs=[dict(path=p) for p,b in sorted(outputs.items())]))
    return outputs


def import_content(raw, out, repository=None, lock_path=None):
    raw,out=safe(raw),safe(out)
    if raw.resolve().is_relative_to(out.resolve()) or out.resolve().is_relative_to(raw.resolve()):
        raise ValueError('Raw and output directories must be disjoint')
    if repository is not None:
        repo=pathlib.Path(repository).resolve()
        if any(p.resolve().is_relative_to(repo) or repo.is_relative_to(p.resolve()) for p in (raw,out)):
            raise ValueError('Materialization must not overlap the reference repository')
    lock=load_lock(lock_path)
    sources=read_sources(lock,raw,repository)
    outputs=convert(sources)
    material={(raw,p):b for p,b in sources.items()} if repository is not None else {}
    if repository is not None: material[(raw_metadata_directory(raw, 'trainer-card'),'manifest.json')]=raw_manifest(lock)
    material.update({(out,p):b for p,b in outputs.items()})
    targets=[(safe(root,p),safe(root,p+'.tmp'),b) for (root,p),b in material.items()]
    if any(p.exists() and not p.is_file() for target,stage,_ in targets for p in (target,stage)):
        raise ValueError('Input/output target is not a regular file')
    if any(parent.exists() and not parent.is_dir() for target,_,_ in targets for parent in target.parents):
        raise ValueError('Input/output ancestor is not a directory')
    for target,stage,data in targets:
        target.parent.mkdir(parents=True,exist_ok=True)
        stage.write_bytes(data)
        stage.replace(target)
    return dict(profile=PROFILE,inputCount=len(sources),outputCount=len(outputs))


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo',type=pathlib.Path,default=os.environ.get('FIRERED_ORIGINAL_REFERENCE'))
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/trainer-card')
    parser.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/fetch-trainer-card.lock.json')
    args=parser.parse_args()
    print(json.dumps(import_content(args.raw,args.out,args.reference_repo,args.lock),sort_keys=True))


if __name__=='__main__': main()

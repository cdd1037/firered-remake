#!/usr/bin/env python3
"""Convert a closed set of local, pinned Old Man tutorial art; no ROM or upstream execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
import argparse, json, os, pathlib, re, subprocess, sys
sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-old-man-art-v1'
REPOSITORY = pathlib.Path(os.environ.get('FIRERED_ORIGINAL_REFERENCE','/workspace/shared/firered-reference-build/pokefirered'))
PATHS = frozenset('''Makefile
graphics/trainers/back_pics/old_man_back_pic.png
graphics/trainers/palettes/old_man_back_pic.pal
graphics/pokemon/weedle/front.png
graphics/pokemon/weedle/normal.pal
graphics/pokemon/weedle/shiny.pal
graphics/items/icons/teachy_tv.png
graphics/items/icon_palettes/teachy_tv.pal
graphics/interface/ball/poke.png
graphics/interface/ball_open.png
src/data/graphics/interface_pokeballs.h
src/data/trainer_graphics/back_pic_anims.h
src/data/trainer_graphics/back_pic_tables.h
src/data/graphics/trainers.h
src/data/item_icon_table.h
src/data/items.json
src/data/pokemon_graphics/front_pic_coordinates.h
src/data/pokemon_graphics/enemy_mon_elevation.h
src/battle_anim_mons.c
src/battle_anim_special.c
src/battle_controller_oak_old_man.c
src/pokeball.c'''.splitlines())
import ui_tiles as codec

def canonical(value): return (json.dumps(value, sort_keys=True, indent=2)+'\n').encode()
def pairs(values):
    result = {}
    for key, value in values:
        if key in result: raise ValueError('Duplicate JSON member: '+key)
        result[key] = value
    return result

def safe(root, relative):
    root = pathlib.Path(root)
    if any(p.is_symlink() for p in [root, *root.parents]): raise ValueError('Old Man art root symlink rejected')
    root = root.resolve(); path = pathlib.PurePosixPath(relative); target = root/relative
    if path.is_absolute() or '..' in path.parts or '\\' in relative or str(path)!=relative or not target.resolve().is_relative_to(root):
        raise ValueError('Unsafe Old Man art path: '+relative)
    if any(p.is_symlink() for p in [target, *target.parents] if p.is_relative_to(root)):
        raise ValueError('Old Man art symlink rejected: '+relative)
    return target

def load_lock(path=ROOT/'scripts/recipes/presentation/old-man-art.lock.json'):
    data = pathlib.Path(path).read_bytes()
    lock = json.loads(data, object_pairs_hook=pairs)
    if (lock['schemaVersion'],lock['repository'],lock['commit'],lock['profile'])!=(1,'pret/pokefirered',COMMIT,PROFILE):
        raise ValueError('Old Man art source identity differs')
    if len(lock['files'])!=len(PATHS) or {e['path'] for e in lock['files']}!=PATHS:
        raise ValueError('Old Man art source closure differs')
    return lock


def read_sources(lock, raw):
    sources = {}; pending = []
    for entry in lock['files']:
        target = safe(raw,entry['path'])
        data = target.read_bytes() if target.exists() else subprocess.run(
            ['git','-C',str(REPOSITORY),'show',COMMIT+':'+entry['path']],
            check=True,capture_output=True,timeout=30).stdout
        sources[entry['path']]=data; pending.append((target,data))
    for target,data in pending:
        target.parent.mkdir(parents=True,exist_ok=True); target.write_bytes(data)
    return sources

def section(text, signature):
    if text.count(signature)!=1: raise ValueError('Missing or ambiguous source block: '+signature)
    start=text.index(signature); end=text.index('\n}',start)+2
    return text[start:end]

def convert(lock,sources):
    by_path = {e['path']:e for e in lock['files']}
    def source(path,symbol,needle):
        text=sources[path].decode()
        if text.count(needle)!=1: raise ValueError('Missing or ambiguous source binding: '+symbol)
    def block(path,signature,symbol):
        value=section(sources[path].decode(),signature);source(path,symbol,value);return value
    def require(text,needle):
        if needle not in text: raise ValueError('Unsupported source semantics: '+needle)
    anim=block('src/data/trainer_graphics/back_pic_anims.h','static const union AnimCmd sAnimCmd_OldMan_1[] =','sAnimCmd_OldMan_1')
    throw_frames=[dict(frame=int(f),ticks=int(t)) for f,t in re.findall(r'ANIMCMD_FRAME\((\d+), (\d+)\)',anim)]
    if throw_frames!=[dict(frame=f,ticks=t) for f,t in [(1,24),(2,9),(3,24),(0,9)]]: raise ValueError('Old Man throw frames differ')
    p='src/pokeball.c';ball_animations=[]
    for name,number,expected in [('closed',0,[(0,1)]),('open',1,[(1,5),(2,5)]),('close',2,[(1,5),(0,5)])]:
        value=block(p,'static const union AnimCmd sBallAnimSeq'+str(number)+'[] =','sBallAnimSeq'+str(number))
        frames=[(int(f)//4,int(t)) for f,t in re.findall(r'ANIMCMD_FRAME\((\d+), (\d+)\)',value)]
        if frames!=expected: raise ValueError('Pokeball frame animation differs')
        ball_animations.append(dict(id=name,frames=[dict(frame=f,ticks=t) for f,t in frames]))
    value=block(p,'void LoadBallGfx(u8 ballId)\n','LoadBallGfx')
    require(value,'LZDecompressVram(gOpenPokeballGfx, (void *)(OBJ_VRAM0 + 0x100 + var * 32));')
    source(p,'gBallSpriteSheets[BALL_POKE]','[BALL_POKE]    = {gBallGfx_Poke,    384, GFX_TAG_POKE_BALL},')
    source(p,'gBallSpritePalettes[BALL_POKE]','[BALL_POKE]    = {gBallPal_Poke,    GFX_TAG_POKE_BALL},')
    source('Makefile','PNG palette conversion','%.gbapal: %.png  ; $(GFX) $< $@')
    p='src/data/graphics/interface_pokeballs.h'
    for symbol,target in [('gBallGfx_Poke','graphics/interface/ball/poke.4bpp.lz'),('gBallPal_Poke','graphics/interface/ball/poke.gbapal.lz'),('gOpenPokeballGfx','graphics/interface/ball_open.4bpp.lz')]:
        source(p,symbol,f'const u32 {symbol}[] = INCBIN_U32("{target}");')
    p='src/battle_anim_special.c'
    functions=['AnimTask_ThrowBallSpecial','AnimTask_ThrowBallSpecial_PlaySfx','SpriteCB_ThrowBall_Init','SpriteCB_ThrowBall_ArcFlight','SpriteCB_ThrowBall_TenFrameDelay','SpriteCB_ThrowBall_ShrinkMon','SpriteCB_ThrowBall_InitialFall','SpriteCB_ThrowBall_Bounce','SpriteCB_ThrowBall_InitShake','SpriteCB_ThrowBall_DoShake','SpriteCB_ThrowBall_InitClick','SpriteCB_ThrowBall_DoClick','SpriteCB_ThrowBall_FinishClick']
    blocks={}
    for name in functions:
        signature=('void ' if name=='AnimTask_ThrowBallSpecial' else 'static void ')+name+('('+('u8 taskId' if name.startswith('AnimTask') else 'struct Sprite *sprite')+')\n')
        blocks[name]=block(p,signature,name)
    for name,needles in {
        'AnimTask_ThrowBallSpecial':['x = 28;','y = 11;','x | 32, y | 80','data[0] = 34;','BATTLER_COORD_Y) - 16;'],
        'AnimTask_ThrowBallSpecial_PlaySfx':['animCmdIndex == 1'],
        'SpriteCB_ThrowBall_Init':['sprite->sTransl_ArcAmpl = -40;'],
        'SpriteCB_ThrowBall_TenFrameDelay':['++sprite->data[5] == 10'],
        'SpriteCB_ThrowBall_ShrinkMon':['gMonShrinkDuration = 28;','data[10] >= 0x480'],
        'SpriteCB_ThrowBall_InitialFall':['sprite->data[4] = 40;'],
        'SpriteCB_ThrowBall_Bounce':['bounceCount == 4'],
        'SpriteCB_ThrowBall_InitShake':['++sprite->data[3] == 31'],
        'SpriteCB_ThrowBall_DoShake':['++sprite->data[5] == 31','BALL_3_SHAKES_SUCCESS && state == 3'],
        'SpriteCB_ThrowBall_DoClick':['data[4] == 40','data[4] == 60','data[4] == 95','data[4] == 315']}.items():
        for needle in needles:require(blocks[name],needle)
    coords=block('src/battle_anim_mons.c','static const struct UCoords8 sBattlerCoords[][MAX_BATTLERS_COUNT] =','sBattlerCoords')
    require(coords,'{ 176, 40 },')
    getter=block('src/battle_anim_mons.c','u8 GetBattlerSpriteCoord(u8 battlerId, u8 coordType)\n','GetBattlerSpriteCoord')
    require(getter,'case BATTLER_COORD_Y:\n        retVal = sBattlerCoords[IS_DOUBLE_BATTLE()][GetBattlerPosition(battlerId)].y;')
    trainer=block('src/battle_controller_oak_old_man.c','static void OakOldManHandleDrawTrainerPic(void)\n','OakOldManHandleDrawTrainerPic')
    require(trainer,'gSprites[gBattlerSpriteIds[gActiveBattler]].x2 = DISPLAY_WIDTH;');require(trainer,'data[0] = -2;')
    back=block('src/data/trainer_graphics/back_pic_tables.h','const struct MonCoords gTrainerBackPicCoords[] =','gTrainerBackPicCoords')
    require(back,'[TRAINER_BACK_PIC_OLD_MAN] = {.size = 8, .y_offset = 4}')
    front='    [SPECIES_WEEDLE] =\n    {\n        .size = MON_COORDS_SIZE(40, 32),\n        .y_offset = 17,\n    },'
    source('src/data/pokemon_graphics/front_pic_coordinates.h','gMonFrontPicCoords[SPECIES_WEEDLE]',front)
    elevation=block('src/data/pokemon_graphics/enemy_mon_elevation.h','const u8 gEnemyMonElevation[NUM_SPECIES] =','gEnemyMonElevation')
    if '[SPECIES_WEEDLE]' in elevation: raise ValueError('Unexpected explicit Weedle elevation')
    source('src/data/item_icon_table.h','gItemIconTable[ITEM_TEACHY_TV]','[ITEM_TEACHY_TV]      = {gItemIcon_TeachyTV, gItemIconPalette_TeachyTV},')
    items=json.loads(sources['src/data/items.json'],object_pairs_hook=pairs)['items']
    selected = [row for row in items if row['itemId'] == 'ITEM_TEACHY_TV']
    if len(selected) != 1: raise ValueError('Expected one Teachy TV declaration')
    item = selected[0]
    if item['fieldUseFunc'] != 'FieldUseFunc_TeachyTv': raise ValueError('Teachy TV field use differs')
    tv_item = dict(name=item['english'], fieldUseFunction=item['fieldUseFunc'])
    tv = dict(description=item['description_english'].replace(r'\n', '\n'))
    outputs={};assets=[]
    def add(id,image_path,palette_path,fw,fh,count,palette_kind='jasc-pal',patch=None):
        w,h,indices,png_palette=codec.indexed_png(sources[image_path])
        if (w,h)!=(fw,fh*count):raise ValueError('Old Man art source dimensions differ: '+id)
        colors=codec.palette(sources[palette_path]) if palette_kind=='jasc-pal' else codec.colors(png_palette)
        if len(colors)!=16 or max(indices)>15:raise ValueError('Old Man palette shape differs')
        patches=[]
        if patch:
            pw,ph,pi,_=codec.indexed_png(sources[patch])
            if (pw,ph)!=(16,16):raise ValueError('Open ball dimensions differ')
            indices=indices[:512]+pi
            patches=[dict(path=patch,frame=2)]
        rgba=codec.rgba(indices,colors);data=codec.png(w,h,rgba);outputs[id+'.png']=data
        assets.append(dict(id=id,texturePath=id+'.png',width=w,height=h,frameWidth=fw,frameHeight=fh,frameCount=count,
            frameLayout='vertical',
            sourceImagePath=image_path,sourcePalettePath=palette_path,
            sourcePaletteKind=palette_kind,transparentIndex=0,
            paletteRgb555=[(r>>3)|((g>>3)<<5)|((b>>3)<<10) for r,g,b in colors],sourcePatches=patches))
    add('old-man-back','graphics/trainers/back_pics/old_man_back_pic.png','graphics/trainers/palettes/old_man_back_pic.pal',64,64,4)
    for variant in ['normal','shiny']:add('weedle-front-'+variant,'graphics/pokemon/weedle/front.png','graphics/pokemon/weedle/'+variant+'.pal',64,64,1)
    add('teachy-tv-icon','graphics/items/icons/teachy_tv.png','graphics/items/icon_palettes/teachy_tv.pal',24,24,1)
    add('poke-ball','graphics/interface/ball/poke.png','graphics/interface/ball/poke.png',16,16,3,'png-plte','graphics/interface/ball_open.png')
    doc=dict(schemaVersion=1,
        assets=assets,teachyTv=dict(id='pret:ITEM_TEACHY_TV',sourceSymbol='ITEM_TEACHY_TV',name=tv_item['name'],description=tv['description'],icon='teachy-tv-icon',fieldUseFunction=tv_item['fieldUseFunction']),
        paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)',oldManThrowFrames=throw_frames,ballAnimations=ball_animations,
        layout=dict(trainerCenterX=80,trainerCenterY=80,trainerSlideOffsetX=240,trainerSlidePixelsPerTick=-2,
            weedleCenterX=176,weedleCenterY=57,weedleSourceWidth=40,weedleSourceHeight=32,weedleSourceYOffset=17,
            ballStartX=60,ballStartY=91,ballTargetX=176,ballTargetY=24),
        ballTiming=dict(releaseTrainerCommandIndex=1,arcTicks=34,arcAmplitude=-40,hitDelayTicks=10,shrinkDuration=28,
            fallAmplitude=40,bounceCount=4,firstShakeDelayTicks=31,betweenShakeDelayTicks=31,successShakeCount=3,
            clickTicks=40,paletteRestoreTicks=60,caughtIntroTicks=95,removeMonsterTicks=315))
    outputs['old-man-art.json']=runtime_json_bytes(doc, sort_keys=True)
    outputs['manifest.json']=canonical(dict(outputs=[dict(path=p) for p,b in sorted(outputs.items())],))
    return outputs

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--output',type=pathlib.Path,default=ROOT/'content/generated/sprites/pokemon/old-man')
    parser.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/recipes/presentation/old-man-art.lock.json')
    args=parser.parse_args()
    if args.output.resolve().is_relative_to(args.raw.resolve()) or args.raw.resolve().is_relative_to(args.output.resolve()):raise ValueError('Old Man raw and output roots overlap')
    lock=load_lock(args.lock);outputs=convert(lock,read_sources(lock,args.raw))
    pending=[(safe(args.output,name),safe(args.output,name+'.tmp'),data) for name,data in outputs.items()]
    for target,stage,data in pending:
        target.parent.mkdir(parents=True,exist_ok=True);stage.write_bytes(data);stage.replace(target)
    print(json.dumps(dict(status='verified',sourceFiles=len(lock['files']),assets=5)))
if __name__=='__main__':main()

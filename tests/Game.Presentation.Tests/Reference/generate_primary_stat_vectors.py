#!/usr/bin/env python3
"""Compile pinned Leer, Tail Glow, Fake Tears and BlendParticle source bodies.

No product code or upstream scripts execute. Shared sprite frame/affine players,
linear/arc/sine arithmetic, callback transitions and palette blending are source C.
Host adapters provide battler coordinates/dimensions, pointers and the established
BIOS matrix adapter; native scale/rotation are retained independently as well.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[3]
SOURCE_SHA256 = {
    'src/sprite.c': '2a804302eb5a89d31c3ec2f33dc645a80a0c645113d4b33b9bd571064c0286ae',
    'src/battle_anim_mons.c': 'fcb002acc48b8cd6b8cf8b7eddcb3034d721ff5887eb6bdeedfe183b7b3908bb',
    'src/battle_anim_effects_3.c': 'bdf8ba956af35ca232d7e5101090e2ff31a35f0ae91286a21b216e7787ba6c1f',
    'src/battle_anim_bug.c': '4f71759597e362d998f978aa41a9b64b617d1db4d966db5bbf32549d15b2dcb2',
    'src/battle_anim_dark.c': '2d4607e9b60aec356c3d75bdb12bba30fa8ee5c6d50741eff54ab7760df98bfb',
    'src/trig.c': '4144065a90e5376ce0259796a01ebaae33c6cc6c7ef716de95b9d9a13c85a810',
    'src/battle_anim_utility_funcs.c': '8dbf0d05319980658ac1f698b1fed5a9dba02d1bda5aba487fec42d9ae16d297',
    'src/blend_palette.c': '219a02b2ed9ab3010e203e0131fa8133571dcc271e025d7f6f14bf7993a51090',
    'include/sprite.h': 'a77aa1c837dfb58cf60b3eac299c7cc29a23ce27735ba710b9e8eeed102bf773',
    'include/battle_anim.h': '15d0a815fec54ed669e1cf7130d323070d799ecfba0f3a935da4b9c28d6f09a7',
    'include/global.h': '044325b90e7c79146cc0cf73d31b6be3e4840059e2e778b63d9b5448fe538429',
    'include/gba/types.h': 'd83a5cb845180b881f3de081ef9c2694b2da017a406fd3d6b0830329cc64ed93',
}
LEER_ARGS = [[24, -12], [0, 0], [-17, 9], [-32768, 32767], [32767, -32768]]
DIMENSIONS = [[64, 64], [1, 1], [63, 63], [16, 8], [64, 128]]
COLUMNS = ['tick', 'active', 'x', 'y', 'x2', 'y2', 'tile', 'visible',
           'frameEnded', 'frameCommand', 'frameDelay', 'affineEnded',
           'affineCommand', 'affineDelay', 'scaleX', 'scaleY', 'rotation',
           'callback', 'matrixBitmap', 'matrixWrites', 'affineNum', 'a', 'b', 'c', 'd']
PALETTE_CONFIGS = [[5, 0, 8, 8, 0], [4, 0, 0, 16, 32767],
                   [14, 2, 16, 0, 31], [15, 1, 2, 5, 31744],
                   [4, 3, 16, 13, 992], [7, 255, 8, 8, 32767]]
PALETTE_INPUT = [0, 32767, 31, 992, 31744, 12345, 1, 32, 1024,
                 16912, 15855, 32736, 31775, 1023, 21845, 10922]
PALETTE_COLUMNS = ['tick', 'active', 'counter', 'coefficient', 'writeCount',
                   'offset', 'writtenCoefficient'] + ['color' + str(i) for i in range(16)]


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def build(repo, reference):
    sys.dont_write_bytecode = True
    ref = load('primary_stat_reference', repo / 'tests/Reference/reference_c.py')
    ref.verify_reference(reference)
    for path, expected in SOURCE_SHA256.items():
        if hashlib.sha256((reference / path).read_bytes()).hexdigest() != expected:
            raise ValueError("Pinned primary-stat source mismatch: " + path)
    extract = load('primary_stat_extract', repo / 'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py')
    names = ['sprite', 'battle_anim_mons', 'battle_anim_effects_3', 'battle_anim_bug',
             'battle_anim_dark', 'trig', 'battle_anim_utility_funcs', 'blend_palette']
    sources = {name: (reference / ('src/' + name + '.c')).read_text() for name in names}
    header = (reference / 'include/sprite.h').read_text()
    battle_header = (reference / 'include/battle_anim.h').read_text()
    funcs = [extract.function(sources['sprite'], name) for name in [
        'AnimateSprites', 'AnimateSprite', 'BeginAnim', 'ContinueAnim', 'AnimCmd_frame',
        'AnimCmd_end', 'AnimCmd_jump', 'AnimCmd_loop', 'BeginAnimLoop', 'ContinueAnimLoop',
        'JumpToTopOfAnimLoop', 'DecrementAnimDelayCounter', 'BeginAffineAnim',
        'ContinueAffineAnim', 'AffineAnimDelay', 'AffineAnimCmd_jump', 'AffineAnimCmd_end',
        'AffineAnimCmd_frame', 'AffineAnimCmd_loop', 'BeginAffineAnimLoop',
        'ContinueAffineAnimLoop', 'JumpToTopOfAffineAnimLoop', 'AffineAnimStateRestartAnim',
        'AffineAnimStateStartAnim', 'AffineAnimStateReset', 'ApplyAffineAnimFrameAbsolute',
        'DecrementAffineAnimDelayCounter', 'ApplyAffineAnimFrameRelativeAndUpdateMatrix',
        'ConvertScaleParam', 'GetAffineAnimFrame', 'ApplyAffineAnimFrame',
        'StartSpriteAffineAnim', 'InitSpriteAffineAnim', 'AllocOamMatrix', 'FreeOamMatrix',
        'FreeSpriteOamMatrix']]
    funcs += [extract.function(sources['battle_anim_mons'], name) for name in [
        'SetSpriteCoordsToAnimAttackerCoords', 'SetAnimSpriteInitialXOffset',
        'RunStoredCallbackWhenAnimEnds', 'RunStoredCallbackWhenAffineAnimEnds',
        'DestroySpriteAndMatrix', 'InitAnimArcTranslation', 'TranslateAnimHorizontalArc',
        'InitAnimLinearTranslation', 'AnimTranslateLinear', 'GetBattlerSpriteCoordAttr']]
    funcs += [extract.function(sources[source], name) for source, name in [
        ('battle_anim_effects_3', 'AnimLeer'), ('battle_anim_bug', 'AnimTailGlowOrb'),
        ('battle_anim_dark', 'AnimTearDrop'), ('battle_anim_dark', 'AnimTearDrop_Step'),
        ('trig', 'Sin')]]
    h = r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8;typedef uint16_t u16;typedef uint32_t u32;
typedef int8_t s8;typedef int16_t s16;typedef int32_t s32;typedef u8 bool8;
#define TRUE 1
#define FALSE 0
#define MAX_SPRITES 1
#define OAM_MATRIX_COUNT 32
#define ST_OAM_AFFINE_ON_MASK 1
#define ST_OAM_AFFINE_OFF 0
#define B_SIDE_PLAYER 0
#define ANIM_ATTACKER 0
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define BATTLER_COORD_ATTR_HEIGHT 0
#define BATTLER_COORD_ATTR_WIDTH 1
#define BATTLER_COORD_ATTR_TOP 2
#define BATTLER_COORD_ATTR_BOTTOM 3
#define BATTLER_COORD_ATTR_LEFT 4
#define BATTLER_COORD_ATTR_RIGHT 5
#define BATTLER_COORD_ATTR_RAW_BOTTOM 6
#define sAnchorX data[6]
#define sAnchorY data[7]
#define SAFE_DIV(a,b) (assert((b)!=0),(a)/(b))
#define MON_DATA_SPECIES 0
#define MON_DATA_PERSONALITY 1
#define SPECIES_UNOWN 201
#define SPECIES_UNOWN_B 413
#define SPECIES_CASTFORM 385
#define NUM_SPECIES 412
#define GET_UNOWN_LETTER(personality) 0
#define GET_MON_COORDS_WIDTH(size) width
#define GET_MON_COORDS_HEIGHT(size) height
_Static_assert((-1>>1)==-1,"Source needs arithmetic signed shift");
'''
    h += header[header.index('struct AnimFrameCmd\n'):header.index('\nenum\n')]
    h += '\n' + '\n'.join(re.findall(r'^#define sTransl_.*$', battle_header, re.M)) + '\n'
    h += re.search(r'^#define Q_8_8\(.*$', (reference / 'include/global.h').read_text(), re.M)[0] + '\n'
    h += re.search(r'^(?:static )?const s16 gSineTable\[\]\s*=\s*\{.*?\};', sources['trig'], re.M | re.S)[0]
    h += r'''
struct Sprite;typedef void(*SpriteCallback)(struct Sprite*);
struct Sprite {
 u8 animCmdIndex,animLoopCounter,animNum;u8 animDelayCounter:6;
 bool8 inUse,animEnded,animBeginning,animPaused,usingSheet,hFlip,vFlip,invisible;
 bool8 affineAnimBeginning,affineAnimEnded,affineAnimPaused,anchored;
 int sheetTileStart,images;s16 x,y,x2,y2,data[8];
 struct{u8 affineMode,matrixNum,shape,size;int tileNum;}oam;
 const union AnimCmd *const *anims;const union AffineAnimCmd *const *affineAnims;
 SpriteCallback callback,stored;
};
struct ObjAffineSrcData{s16 xScale,yScale;u16 rotation;};struct OamMatrix{s16 a,b,c,d;};
struct MonCoords{u8 size,y_offset;};struct BattleSpriteInfo{u16 transformSpecies;};
static struct{struct BattleSpriteInfo battlerData[4];}battleSprites,*gBattleSpritesDataPtr=&battleSprites;
static int gPlayerParty[4],gEnemyParty[4];static u8 gBattlerPartyIndexes[4],gBattleMonForms[4];static u32 gTransformedPersonalities[4];
static struct MonCoords gMonBackPicCoords[440],gMonFrontPicCoords[440],gCastformFrontSpriteCoords[4];
static struct Sprite gSprites[1];static struct AffineAnimState sAffineAnimStates[32];
static u32 gOamMatrixAllocBitmap;static bool8 gAffineAnimsDisabled;
static u8 gBattleAnimAttacker,gBattleAnimTarget;static s16 gBattleAnimArgs[8];
static int doubles,width,height,writes;static struct OamMatrix matrix;static u16 rotation;
static const s16 coords[2][4][2]={{{72,96},{176,48},{48,56},{112,88}},{{32,96},{200,48},{90,104},{152,40}}};
static u32 GetMonData(int*p,int kind){return kind==MON_DATA_SPECIES?1:0;}
static u8 GetBattlerSide(u8 battler){assert(battler<4);return battler&1;}
static u8 GetBattlerSpriteCoord(u8 battler,u8 kind){assert(battler<4&&kind<4);return coords[doubles][battler][kind&1];}
static void SetSpriteOamFlipBits(struct Sprite*s,u8 x,u8 y){s->hFlip=x;s->vFlip=y;}
static void RequestSpriteFrameImageCopy(int a,int b,int c){abort();}
static void StoreSpriteCallbackInData6(struct Sprite*s,SpriteCallback callback){s->stored=callback;}
static void SetCallbackToStoredInData6(struct Sprite*s){s->callback=s->stored;}
static void DestroyAnimSprite(struct Sprite*s){assert(gOamMatrixAllocBitmap==0);s->inUse=FALSE;}
static u8 GetSpriteMatrixNum(struct Sprite*s){return s->oam.matrixNum;}
static void UpdateSpriteMatrixAnchorPos(struct Sprite*s,s32 x,s32 y){abort();}
static void CopyOamMatrix(u8 n,struct OamMatrix*m){assert(n==0);matrix=*m;}
static void SetOamMatrix(u8 n,s16 a,s16 b,s16 c,s16 d){assert(n==0&&a==256&&b==0&&c==0&&d==256);matrix=(struct OamMatrix){a,b,c,d};}
static void CalcCenterToCornerVec(struct Sprite*s,u8 shape,u8 size,u8 affine){assert(affine==1);}
BIOS_ADAPTER
'''
    adapter = (repo / 'tests/Game.Presentation.Tests/Reference/generate_projectile_adapter_vectors.py').read_text()
    bios = re.search(r'static const s16 quarter\[\].*?static void ObjAffineSet[^\n]+', adapter, re.S)[0]
    bios = bios.replace('rotation=s->rotation;', 'writes++;rotation=s->rotation;')
    h = h.replace('BIOS_ADAPTER', bios)
    h += '\n'.join(f.split('\n{', 1)[0] + ';' for f in funcs) + '\n'
    h += r'''
static void(*sAnimFuncs[])(struct Sprite*)={ContinueAnim,BeginAnim};
static void(*sAnimCmdFuncs[])(struct Sprite*)={AnimCmd_loop,AnimCmd_jump,AnimCmd_end,AnimCmd_frame};
static void(*sAffineAnimFuncs[])(struct Sprite*)={ContinueAffineAnim,BeginAffineAnim};
static void(*sAffineAnimCmdFuncs[])(u8,struct Sprite*)={AffineAnimCmd_loop,AffineAnimCmd_jump,AffineAnimCmd_end,AffineAnimCmd_frame};
'''
    for source, name in [('battle_anim_effects_3', 'sLeerAnimCmds'), ('battle_anim_bug', 'sAffineAnim_TailGlowOrb'),
                         ('battle_anim_dark', 'sAffineAnim_TearDrop_0'), ('battle_anim_dark', 'sAffineAnim_TearDrop_1')]:
        h += extract.declaration(sources[source], name) + '\n'
    h += 'static const union AnimCmd dummyAnim[]={ANIMCMD_END};\n'
    h += 'static const union AnimCmd *const leerAnims[]={sLeerAnimCmds},*const dummyAnims[]={dummyAnim};\n'
    h += 'static const union AffineAnimCmd *const tailAnims[]={sAffineAnim_TailGlowOrb},*const tearAnims[]={sAffineAnim_TearDrop_0,sAffineAnim_TearDrop_1};\n'
    h += '\n'.join(funcs) + '\n'
    h += 'static const s16 leer[][2]={' + ','.join('{' + ','.join(map(str, row)) + '}' for row in LEER_ARGS) + '};\n'
    h += 'static const s16 dimensions[][2]={' + ','.join('{' + ','.join(map(str, row)) + '}' for row in DIMENSIONS) + '};\n'
    h += r'''
static int callback(struct Sprite*s){
 if(s->callback==RunStoredCallbackWhenAnimEnds)return 0;
 if(s->callback==RunStoredCallbackWhenAffineAnimEnds)return 1;
 if(s->callback==AnimTearDrop_Step)return 2;
 if(s->callback==DestroyAnimSprite)return 3;
 if(s->callback==DestroySpriteAndMatrix)return 4;
 abort();
}
static void emit(int kind,int config,int dimension,int tick){
 struct Sprite*s=&gSprites[0];struct AffineAnimState*a=&sAffineAnimStates[0];
 int row[]={tick,s->inUse,s->x,s->y,s->x2,s->y2,s->oam.tileNum,!s->invisible,
 s->animEnded,s->animCmdIndex,s->animDelayCounter,s->affineAnimEnded,a->animCmdIndex,
 a->delayCounter,a->xScale,a->yScale,a->rotation,callback(s),(int)gOamMatrixAllocBitmap,writes,a->animNum,matrix.a,matrix.b,matrix.c,matrix.d};
 printf("%d:%d:%d:%d:%d:%d",kind,doubles,gBattleAnimAttacker,gBattleAnimTarget,config,dimension);
 for(unsigned i=0;i<sizeof(row)/sizeof(row[0]);i++)printf(",%d",row[i]);puts("");
}
static void run(int kind,int config,int dimension){
 memset(gSprites,0,sizeof(gSprites));memset(sAffineAnimStates,0,sizeof(sAffineAnimStates));memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));
 gOamMatrixAllocBitmap=0;matrix=(struct OamMatrix){256,0,0,256};rotation=0;width=dimensions[dimension][0];height=dimensions[dimension][1];
 struct Sprite*s=&gSprites[0];s->inUse=TRUE;s->animBeginning=TRUE;s->usingSheet=TRUE;
 s->anims=kind==0?leerAnims:dummyAnims;
 if(kind==0){memcpy(gBattleAnimArgs,leer[config],sizeof(leer[config]));s->callback=AnimLeer;}
 else{s->oam.affineMode=1;s->affineAnims=kind==1?tailAnims:tearAnims;InitSpriteAffineAnim(s);
  gBattleAnimArgs[0]=kind==1?config:config/4;gBattleAnimArgs[1]=config%4;
  s->callback=kind==1?AnimTailGlowOrb:AnimTearDrop;}
 for(int tick=-1;tick<256;tick++){writes=0;AnimateSprites();emit(kind,config,dimension,tick);if(!s->inUse)return;}abort();
}
int main(void){for(doubles=0;doubles<2;doubles++)for(gBattleAnimAttacker=0;gBattleAnimAttacker<(doubles?4:2);gBattleAnimAttacker++)
 for(gBattleAnimTarget=0;gBattleAnimTarget<(doubles?4:2);gBattleAnimTarget++)for(int kind=0;kind<3;kind++)
 for(int config=0;config<(kind==0?5:kind==1?2:8);config++)for(int dim=0;dim<(kind==2?5:1);dim++)run(kind,config,dim);
}
'''

    palette_funcs = [extract.function(sources['battle_anim_utility_funcs'], name) for name in
                     ['AnimTask_BlendParticle', 'StartBlendAnimSpriteColor', 'AnimTask_BlendSpriteColor_Step2']]
    blend = extract.function(sources['blend_palette'], 'BlendPalette')
    palette = r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
typedef uint8_t u8;typedef uint16_t u16;typedef uint32_t u32;typedef int8_t s8;typedef int16_t s16;
struct Task{s16 data[16];void(*func)(u8);};static struct Task gTasks[1];static s16 gBattleAnimArgs[8];
static u16 gPlttBufferUnfaded[512],gPlttBufferFaded[512];
static int active,slot,writes,lastOffset,lastCoefficient;
static u8 IndexOfSpritePaletteTag(u16 tag){assert(tag==10000);return slot;}
static void DestroyAnimVisualTask(u8 task){assert(task==0);active=0;}
'''
    types = (reference / 'include/gba/types.h').read_text()
    palette += re.search(r'struct PlttData\s*\{.*?\};', types, re.S)[0] + '\n' + blend + '\n'
    palette += 'static void ObservedBlendPalette(u16 offset,u16 count,u8 coefficient,u16 color){writes++;lastOffset=offset;lastCoefficient=coefficient;BlendPalette(offset,count,coefficient,color);assert(offset==(slot+16)*16&&count==16); }\n#define BlendPalette ObservedBlendPalette\n'
    palette += '\n'.join(f.split('\n{', 1)[0] + ';' for f in palette_funcs) + '\n' + '\n'.join(palette_funcs) + '\n'
    palette += 'static const int configs[][5]={' + ','.join('{' + ','.join(map(str, row)) + '}' for row in PALETTE_CONFIGS) + '};\n'
    palette += 'static const u16 input[16]={' + ','.join(map(str, PALETTE_INPUT)) + '};\n'
    palette += r'''
int main(void){for(unsigned config=0;config<sizeof(configs)/sizeof(configs[0]);config++){
 memset(gTasks,0,sizeof(gTasks));memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));
 for(int i=0;i<512;i++)gPlttBufferUnfaded[i]=gPlttBufferFaded[i]=input[i%16];
 slot=configs[config][0];gBattleAnimArgs[0]=10000;for(int i=1;i<5;i++)gBattleAnimArgs[i]=configs[config][i];active=1;
 for(int tick=-1;tick<5000;tick++){
  writes=0;lastOffset=lastCoefficient=-1;if(tick==-1)AnimTask_BlendParticle(0);else gTasks[0].func(0);
  printf("%d,%d,%d,%d,%d,%d,%d,%d",config,tick,active,gTasks[0].data[9],gTasks[0].data[10],writes,lastOffset,lastCoefficient);
  for(int i=0;i<16;i++)printf(",%d",gPlttBufferFaded[(slot+16)*16+i]);puts("");
  for(int i=0;i<512;i++)if(i/16!=slot+16)assert(gPlttBufferFaded[i]==input[i%16]);
  if(!active)break;assert(tick<4999);
 }
}}
'''

    def run(code, name, source_shift=False):
        with tempfile.TemporaryDirectory(prefix='primary-stat-source-') as folder:
            c = Path(folder) / (name + '.c'); exe = Path(folder) / name
            c.write_text(code)
            flags = ['-fsanitize=undefined', '-fno-sanitize-recover=all'] if os.name != 'nt' else []
            if flags and source_shift:
                flags += ['-fno-sanitize=shift-base']
            ref.compile_c(['cc', '-std=c11', '-O0', *flags, str(c), '-o', str(exe)])
            return subprocess.check_output([str(exe)], text=True,
                env={**os.environ, 'UBSAN_OPTIONS': 'halt_on_error=1:print_stacktrace=1'})

    cases = {}
    for line in run(h, 'sprites').splitlines():
        key, *row = line.split(','); values = list(map(int, row))
        assert len(values) == len(COLUMNS)
        cases.setdefault(key, []).append(values)
    palettes = {}
    for line in run(palette, 'palette', source_shift=True).splitlines():
        key, *row = line.split(','); values = list(map(int, row))
        assert len(values) == len(PALETTE_COLUMNS)
        palettes.setdefault(key, []).append(values)
    assert len(cases) == 940 and len(palettes) == len(PALETTE_CONFIGS)
    for key, rows in cases.items():
        assert rows[0][0] == -1 and rows[-1][1] == 0 and rows[-1][18] == 0, key
        assert all(row[1] == 1 and row[7] == 1 for row in rows[:-1]), key
        kind = int(key.split(':')[0])
        assert len(rows) == [18, 118, 34][kind], key
        if kind == 0:
            assert next(row[0] for row in rows if row[8]) == 14, key
        else:
            assert next(row[0] for row in rows if row[11]) == (114 if kind == 1 else 8), key
            assert all(row[18] == 1 for row in rows[:-1]), key
        if kind == 2:
            assert all(row[6] == 4 for row in rows), key
    for key, rows in palettes.items():
        slot, delay, start, end, color = PALETTE_CONFIGS[int(key)]
        assert len(rows) == (abs(end-start)+1)*(delay+1), key
        assert rows[-1][1] == 0 and all(row[1] == 1 for row in rows[:-1]), key
    return dict(sourceCommit=ref.COMMIT, sourceSha256={name: hashlib.sha256((reference / ('src/' + name + '.c')).read_bytes()).hexdigest() for name in names},
                columns=COLUMNS, leerArgs=LEER_ARGS, dimensions=DIMENSIONS, cases=cases,
                paletteColumns=PALETTE_COLUMNS, paletteConfigs=PALETTE_CONFIGS,
                paletteInput=PALETTE_INPUT, palettes=palettes,
                notes=[
                    'Case key is kind:doubles:actor:target:config:dimension. Kinds 0=Leer, 1=Tail Glow, 2=Fake Tears. Tail config is participant 0/1; tear config is participant*4+variant.',
                    'Battler coordinates use normal singles/doubles layout with YOffset=8, Elevation=0. All occupied actor/target pairs, self targets and partner targets are included.',
                    'Tick -1 is creation callback then AnimateSprite. Later ticks use source AnimateSprites. Visibility is the source invisible bit; active separately records destruction.',
                    'Dimensions are host descriptor inputs to the complete source GetBattlerSpriteCoordAttr body; source species selection uses ordinary species 1. Odd and oversized synthetic dimensions exercise geometry arithmetic.',
                    'Native gDummySpriteAnimTable is an END command. Tail/Tear tile selection remains static; generated dummy frame-loop adapters need not share native dummy frameEnded/counters.',
                    'Shared source allocation/free and native affine scale/rotation are observed. Matrix values use the established projectile oracle BIOS adapter; it is not original source BIOS code.',
                    'Callback codes: 0=frame END wait, 1=affine END wait, 2=tear step, 3=DestroyAnimSprite, 4=DestroySpriteAndMatrix.',
                    'Palette configs are slot,delay,start,end,color. Original task and BlendPalette bodies verify all 16 selected entries and assert every other bank is untouched.',
                    'Palette slot 15 preserves upstream signed shifts into bit 31. Only shift-base sanitizer is disabled for that harness; other undefined-behavior checks remain fatal.',
                    'No upstream scripts, product code or gameplay RNG execute.'],
                sanitizer='undefined; no-recover (palette shift-base excluded)' if os.name != 'nt' else 'not enabled')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--repo', type=Path, default=ROOT)
    parser.add_argument('--reference', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error('Refusing to overwrite an existing fixture; select a new candidate output.')
    data = build(args.repo, args.reference or args.repo / 'content/raw/pokefirered/037335f4')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    print('Wrote', len(data['cases']), 'sprite cases,', sum(map(len, data['cases'].values())),
          'sprite rows,', len(data['palettes']), 'palette cases,', sum(map(len, data['palettes'].values())), 'palette rows')

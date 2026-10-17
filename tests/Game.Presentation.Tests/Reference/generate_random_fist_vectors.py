#!/usr/bin/env python3
"""Reproduce random-fist vectors from pinned C, without a managed compiler.

Only authored bounded harness code and exact extracted source function/table
bodies are compiled. All scheduling, registry, frame, affine, destruction, and
RNG behavior comes from the pinned source. Battler picture anchors are fixture
inputs. Picture dimensions are parsed directly from pinned coordinate tables.
The no-rotation BIOS adapter returns its exact diagonal result; other rotations
abort. Native cc is run under strict undefined-behavior sanitizer by default.

Run from any directory with --repo /path/to/FireRed-main. The output captures
script initialization at tick -1 and source AnimateSprites passes at tick >=0.
Raw-initialization fixtures intentionally initialize their parent during tick0.
"""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile

sys.dont_write_bytecode = True
SEEDS = [0, 1, 0xFFFFFFFF, 0x12345678, 0x80000000, 0x7FFFFFFF]
GEOMETRIES = [[0,1],[1,0],[2,3],[3,2],[0,2],[2,0],[1,3],[3,1]]
COORDINATES = [[32,88],[200,40],[90,96],[152,32]]
STATE_COLUMNS = ['event','tick','phase','serial','slot','kind','callback','callbackCount',
 'inUse','x','y','x2','y2','data0','data7','animNum','tile','hflip','vflip',
 'frameBeginning','frameEnded','frameCommand','frameDelay','affineBeginning','affineEnded',
 'matrix','scaleX','scaleY','matrixA','matrixD','affineCommand','affineDelay','matrixWrites',
 'subpriority','liveCount','visualCount','matrixBitmap','drawCount','arg2']
EVENT_COLUMNS = ['event','tick','operation','serial','slot','argument','liveCount','visualCount','matrixBitmap']
BOUNDARY_COLUMNS = ['tick','liveCount','visualCount','matrixBitmap','drawCount','rngState','arg2']
RNG_COLUMNS = ['event','tick','parentSerial','drawIndex','before','result','after']
ALLOCATION_COLUMNS = ['event','tick','serial','slot','kind','parentSerial','x','y','subpriority']
SUFFIX_COLUMNS = ['event','tick','parentSerial','rngState','drawCount','arg2','next0','next1','next2','next3','next4','next5','next6','next7']
OPS = ['allocated','initializer_enter','initializer_exit','step_enter','step_exit','dummy_callback',
 'animate_enter','animate_exit','begin_frame_enter','begin_frame_exit','begin_affine_enter',
 'begin_affine_exit','start_affine_enter','start_affine_exit','free_matrix_enter','free_matrix_exit',
 'free_sprite_matrix','destroy_enter','destroy_exit','create_failed']
PHASES = ['allocated','initialized','after_player','before_destroy','after_reset','boundary']


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def source_declaration(source, name):
    pattern = r'^(?:static )?const (?:union (?:AnimCmd|AffineAnimCmd)(?: \*const| \* const)?|struct SpriteTemplate) ' + re.escape(name) + r'(?:\[\])?\s*=\s*\{.*?\};'
    match = re.search(pattern, source, re.M | re.S)
    if not match:
        raise ValueError('Missing source declaration ' + name)
    return match[0]


def dimensions(reference):
    front = (reference/'src/data/pokemon_graphics/front_pic_coordinates.h').read_text()
    back = (reference/'src/data/pokemon_graphics/back_pic_coordinates.h').read_text()
    mons = (reference/'src/battle_anim_mons.c').read_text()
    def regular(text, species):
        match = re.search(r'\['+species+r'\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\)', text)
        assert match, species
        return list(map(int, match.groups()))
    profiles = [{'name':'bulbasaur','back':regular(back,'SPECIES_BULBASAUR'),'front':regular(front,'SPECIES_BULBASAUR')}]
    for form in ['NORMAL','FIRE','WATER','ICE']:
        match = re.search(r'\[CASTFORM_'+form+r'\]\s*=\s*\{ \.size = MON_COORDS_SIZE\((\d+), (\d+)\)', mons)
        assert match, form
        profiles.append({'name':'castform_'+form.lower(),'back':regular(back,'SPECIES_CASTFORM'),'front':list(map(int,match.groups()))})
    assert [p['front'] for p in profiles] == [[40,40],[32,32],[48,48],[32,48],[64,48]]
    return profiles


def configurations():
    cases = []
    def add(name, actor=0, target=1, arg0=1, duration=20, arg2=1, seed=0x12345678, profile=0, layout=0, count=1, subpriority=32):
        cases.append(dict(name=name, actor=actor,target=target,arg0=arg0,duration=duration,arg2=arg2,seed=seed,profile=profile,layout=layout,count=count,subpriority=subpriority))
    for actor,target in GEOMETRIES:
        for selector in [0,1]:
            add('geometry_%d_%d_select%d'%(actor,target,selector),actor,target,selector,arg2=(actor+target+selector)%5)
    for profile in range(1,5):
        for selector in [0,1]:
            add('dimensions_%d_select%d'%(profile,selector),arg0=selector,profile=profile,arg2=4)
    for seed in SEEDS:
        for anim in [-1,2]:
            add('seed_%08x_anim%d'%(seed,anim),seed=seed,arg2=anim,duration=1)
    for duration in [0,1,20]:
        for layout in [0,1,2,3]:
            add('duration%d_layout%d'%(duration,layout),duration=duration,layout=layout)
    add('shared_random_arg2',arg2=-1,count=2)
    add('signed_duration_wrap',duration=-32768)
    add('nonzero_selector',arg0=-32768,arg2=0)
    add('subpriority_wrap',subpriority=255,arg2=3)
    return cases


def build(repo, reference, helper, extractor, sanitize=True):
    ref=load('random_fist_reference',helper); ref.verify_reference(reference)
    extract=load('random_fist_extract',extractor)
    function=extract.function
    source={name:(reference/('src/'+name+'.c')).read_text() for name in ['sprite','battle_anim_fight','battle_anim_normal','battle_anim','random']}
    sp=source['sprite']; fight=source['battle_anim_fight']; normal=source['battle_anim_normal']
    header=(reference/'include/sprite.h').read_text()
    profiles=dimensions(reference); configs=configurations()
    ordinary=['AnimateSprites','CreateSprite','CreateSpriteAndAnimate','ResetSprite',
      'ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop',
      'ContinueAnimLoop','JumpToTopOfAnimLoop','DecrementAnimDelayCounter','ContinueAffineAnim',
      'AffineAnimDelay','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame',
      'AffineAnimStateRestartAnim','AffineAnimStateStartAnim','AffineAnimStateReset',
      'ApplyAffineAnimFrameAbsolute','DecrementAffineAnimDelayCounter',
      'ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame',
      'ApplyAffineAnimFrame','GetSpriteMatrixNum','AllocOamMatrix','InitSpriteAffineAnim',
      'SetSpriteSheetFrameTileNum','StartSpriteAnim','SetSpriteOamFlipBits']
    wrapped=['CreateSpriteAt','AnimateSprite','BeginAnim','BeginAffineAnim','StartSpriteAffineAnim',
      'DestroySprite','FreeSpriteOamMatrix','FreeOamMatrix','CopyOamMatrix','SpriteCallbackDummy']
    callbacks=['AnimFistOrFootRandomPos','AnimFistOrFootRandomPos_Step']
    funcs=[function(sp,n) for n in ordinary+wrapped]+[function(fight,n) for n in callbacks]+[function(source['battle_anim'],'DestroyAnimSprite')]
    h=r'''
#include <assert.h>
#include <inttypes.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8;typedef uint16_t u16;typedef uint32_t u32;typedef int8_t s8;typedef int16_t s16;typedef int32_t s32;typedef u8 bool8;
#define TRUE 1
#define FALSE 0
#define MAX_SPRITES 64
#define OAM_MATRIX_COUNT 32
#define ST_OAM_AFFINE_ON_MASK 1
#define ST_OAM_AFFINE_OFF 0
#define TAG_NONE 65535
#define TILE_SIZE_4BPP 32
#define FREE_SPRITE_TILE(i) abort()
#define B_SIDE_PLAYER 0
#define BIT_SIDE 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define BATTLER_COORD_ATTR_WIDTH 1
#define BATTLER_COORD_ATTR_HEIGHT 0
#define ANIM_TAG_HANDS_AND_FEET 1
#define ANIM_TAG_IMPACT 2
#define ANIM_END 0xFFFF
#define AFFINE_ANIM_END 0x7FFF
#define sAnchorX data[6]
#define sAnchorY data[7]
#define SAFE_DIV(a,b) (assert((b)!=0),(a)/(b))
_Static_assert((-1 >> 1)==-1,"arithmetic signed shifts");
_Static_assert((s16)32768==-32768 && (s16)-32769==32767,"GBA-compatible signed16 narrowing");
'''
    h+=header[header.index('struct AnimFrameCmd\n'):header.index('\nenum\n')]
    h+='\n'+re.search(r'^#define Q_8_8\(.*$',(reference/'include/global.h').read_text(),re.M)[0]+'\n'
    h+='\n'.join(re.findall(r'^#define (?:RAND_MULT|ISO_RANDOMIZE1)\b.*$',(reference/'include/random.h').read_text(),re.M))+'\n'
    h+=r'''
struct Sprite;typedef void(*SpriteCallback)(struct Sprite*);
struct OamData{u8 affineMode,shape,size,matrixNum,paletteNum;u16 tileNum;};
struct SpriteFrameImage{const void*data;u16 size;};
struct SpriteTemplate{u16 tileTag,paletteTag;const struct OamData*oam;const union AnimCmd*const*anims;const struct SpriteFrameImage*images;const union AffineAnimCmd*const*affineAnims;SpriteCallback callback;};
struct Sprite{
 struct OamData oam;const union AnimCmd*const*anims;const struct SpriteFrameImage*images;
 const union AffineAnimCmd*const*affineAnims;const struct SpriteTemplate*template;SpriteCallback callback;
 s16 x,y,x2,y2,data[8];u8 animNum,animCmdIndex,animLoopCounter,subpriority;u8 animDelayCounter:6;
 bool8 inUse,animEnded,animBeginning,animPaused,usingSheet,hFlip,vFlip;
 bool8 affineAnimBeginning,affineAnimEnded,affineAnimPaused,anchored;u16 sheetTileStart;
};
struct ObjAffineSrcData{s16 xScale,yScale;u16 rotation;};struct OamMatrix{s16 a,b,c,d;};
static struct Sprite gSprites[64],sDummySprite;
static struct AffineAnimState sAffineAnimStates[32];static struct OamMatrix gOamMatrices[32];
static u32 gOamMatrixAllocBitmap,gRngValue;static bool8 gAffineAnimsDisabled;
static u8 gBattleAnimAttacker,gBattleAnimTarget,gAnimVisualTaskCount,gBattlerPositions[4]={0,1,2,3};static s16 gBattleAnimArgs[8];
static int caseId,tick,eventId,nextSerial,currentParent,drawCount,profile,originalDuration;
static int serials[64],kinds[64],parents[64],callbackCounts[64],matrixWrites[64];
static const u8 coords[4][2]={{32,88},{200,40},{90,96},{152,32}};
extern const struct SpriteTemplate gFistFootRandomPosSpriteTemplate,gBasicHitSplatSpriteTemplate;
static void Event(int,struct Sprite*,int);static void State(struct Sprite*,int);static void Suffix(struct Sprite*);
static int Selected(void){return originalDuration>=0||tick<=1||tick==8||tick==9||tick>=32766;}
static int Live(void){int n=0;for(int i=0;i<64;i++)n+=gSprites[i].inUse&&serials[i]!=0;return n;}
static int Slot(struct Sprite*s){assert(s>=gSprites&&s<gSprites+64);return (int)(s-gSprites);}
static u8 GetBattlerSpriteCoord(u8 b,u8 c){assert(b<4&&(c==2||c==3));return coords[b][c&1];}
'''
    h+='static const u8 dimensions[][2][2]={'+','.join('{'+','.join('{'+','.join(map(str,p[side]))+'}' for side in ['back','front'])+'}' for p in profiles)+'};\n'
    h+=r'''
static s16 GetBattlerSpriteCoordAttr(u8 b,u8 a){assert(b<4&&(a==0||a==1));return dimensions[profile][gBattlerPositions[b]&1][a==0];}
static void CalcCenterToCornerVec(struct Sprite*s,u8 a,u8 b,u8 c){(void)s;(void)a;(void)b;(void)c;}
static s16 AllocSpriteTiles(u8 n){abort();}
static u16 GetSpriteTileStartByTag(u16 n){assert(n==1||n==2);return 0;}
static u8 IndexOfSpritePaletteTag(u16 n){assert(n==1||n==2);return 0;}
static void RequestSpriteFrameImageCopy(u16 a,u16 b,const struct SpriteFrameImage*c){abort();}
static void UpdateSpriteMatrixAnchorPos(struct Sprite*s,s32 x,s32 y){abort();}
static void SetOamMatrix(u8 n,s16 a,s16 b,s16 c,s16 d){assert(n<32);gOamMatrices[n]=(struct OamMatrix){a,b,c,d};}
static void ObjAffineSet(const struct ObjAffineSrcData*s,struct OamMatrix*d,int n,int stride){assert(n==1&&stride==2&&s->rotation==0);*d=(struct OamMatrix){s->xScale,0,0,s->yScale};}
static void UnsupportedAffineLoop(u8 n,struct Sprite*s){abort();}
static void AnimHitSplatBasic(struct Sprite*s){abort();}
'''
    h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'
    h+=r'''
static void(*sAnimFuncs[])(struct Sprite*)={ContinueAnim,BeginAnim};
static void(*sAnimCmdFuncs[])(struct Sprite*)={AnimCmd_loop,AnimCmd_jump,AnimCmd_end,AnimCmd_frame};
static void(*sAffineAnimFuncs[])(struct Sprite*)={ContinueAffineAnim,BeginAffineAnim};
static void(*sAffineAnimCmdFuncs[])(u8,struct Sprite*)={UnsupportedAffineLoop,AffineAnimCmd_jump,AffineAnimCmd_end,AffineAnimCmd_frame};
'''
    h+=function(source['random'],'Random')+r'''
static u16 ObservedRandom(void){u32 before=gRngValue;u16 r=Random();printf("R,%d,%d,%d,%d,%d,%"PRIu32",%u,%"PRIu32"\n",caseId,eventId++,tick,currentParent,drawCount++,before,r,gRngValue);return r;}
#define Random ObservedRandom
'''
    for f in funcs:
        name=re.search(r'(\w+)\([^;]+?\)\n\{',f).group(1)
        if name in wrapped+callbacks:
            h+='#define '+name+' Source'+name+'\n'+f+'\n#undef '+name+'\n'
        else:
            h+=f+'\n'
    h+='#undef Random\n'
    for name in ['sAnim_Fist','sAnim_FootWide','sAnim_FootTall','sAnim_HandLeft','sAnim_HandRight','sAnims_HandsAndFeet']:
        h+=source_declaration(fight,name)+'\n'
    for name in ['sAffineAnim_HitSplat_0','sAffineAnim_HitSplat_1','sAffineAnim_HitSplat_2','sAffineAnim_HitSplat_3','sAffineAnims_HitSplat']:
        h+=source_declaration(normal,name)+'\n'
    for name in ['sDummyAnim','sDummyAffineAnim','gDummySpriteAnimTable','gDummySpriteAffineAnimTable']:
        h+=source_declaration(sp,name)+'\n'
    h+=r'''
static const struct OamData gOamData_AffineOff_ObjNormal_32x32={0,0,2},gOamData_AffineNormal_ObjBlend_32x32={1,0,2};
'''
    h+=source_declaration(fight,'gFistFootRandomPosSpriteTemplate')+'\n'+source_declaration(normal,'gBasicHitSplatSpriteTemplate')+'\n'
    h+=r'''
static void Event(int op,struct Sprite*s,int arg){int i=s?Slot(s):-1;int ev=eventId++;if(!Selected())return;printf("E,%d,%d,%d,%d,%d,%d,%d,%d,%d,%"PRIu32"\n",caseId,ev,tick,op,i<0?0:serials[i],i,arg,Live(),gAnimVisualTaskCount,gOamMatrixAllocBitmap);}
static void State(struct Sprite*s,int phase){
 int i=Slot(s),ev=eventId++;if(!Selected()||!serials[i])return;int aff=(s->oam.affineMode&1)!=0;
 struct AffineAnimState*a=&sAffineAnimStates[aff?s->oam.matrixNum:0];
 int cb=s->callback==AnimFistOrFootRandomPos?0:s->callback==AnimFistOrFootRandomPos_Step?1:s->callback==SpriteCallbackDummy?2:3;
 int row[]={ev,tick,phase,serials[i],i,kinds[i],cb,callbackCounts[i],s->inUse,s->x,s->y,s->x2,s->y2,s->data[0],s->data[7],s->animNum,s->oam.tileNum,(s->oam.matrixNum>>3)&1,(s->oam.matrixNum>>4)&1,
 s->animBeginning,s->animEnded,s->animCmdIndex,s->animDelayCounter,s->affineAnimBeginning,s->affineAnimEnded,aff?s->oam.matrixNum:-1,
 aff?a->xScale:256,aff?a->yScale:256,aff?gOamMatrices[s->oam.matrixNum].a:256,aff?gOamMatrices[s->oam.matrixNum].d:256,
 aff?a->animCmdIndex:0,aff?a->delayCounter:0,matrixWrites[i],s->subpriority,Live(),gAnimVisualTaskCount,(int)gOamMatrixAllocBitmap,drawCount,gBattleAnimArgs[2]};
 printf("S,%d",caseId);for(unsigned j=0;j<sizeof(row)/sizeof(row[0]);j++)printf(",%d",row[j]);puts("");
}
u8 CreateSpriteAt(u8 i,const struct SpriteTemplate*t,s16 x,s16 y,u8 subpriority){
 u8 result=SourceCreateSpriteAt(i,t,x,y,subpriority);assert(result==i);serials[i]=++nextSerial;kinds[i]=t==&gFistFootRandomPosSpriteTemplate?1:2;parents[i]=currentParent;callbackCounts[i]=matrixWrites[i]=0;
 Event(0,&gSprites[i],parents[i]);printf("A,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",caseId,eventId++,tick,serials[i],i,kinds[i],parents[i],x,y,subpriority);State(&gSprites[i],0);return result;
}
void CopyOamMatrix(u8 n,struct OamMatrix*m){SourceCopyOamMatrix(n,m);for(int i=0;i<64;i++)if(gSprites[i].inUse&&(gSprites[i].oam.affineMode&1)&&gSprites[i].oam.matrixNum==n)matrixWrites[i]++;}
void AnimateSprite(struct Sprite*s){if(!serials[Slot(s)]){SourceAnimateSprite(s);return;}Event(6,s,0);SourceAnimateSprite(s);Event(7,s,0);State(s,2);}
void BeginAnim(struct Sprite*s){if(serials[Slot(s)])Event(8,s,0);SourceBeginAnim(s);if(serials[Slot(s)])Event(9,s,0);}
void BeginAffineAnim(struct Sprite*s){if(serials[Slot(s)])Event(10,s,0);SourceBeginAffineAnim(s);if(serials[Slot(s)])Event(11,s,0);}
void StartSpriteAffineAnim(struct Sprite*s,u8 n){Event(12,s,n);SourceStartSpriteAffineAnim(s,n);Event(13,s,n);}
void FreeOamMatrix(u8 n){Event(14,NULL,n);SourceFreeOamMatrix(n);Event(15,NULL,n);}
void FreeSpriteOamMatrix(struct Sprite*s){Event(16,s,s->oam.matrixNum);SourceFreeSpriteOamMatrix(s);}
void DestroySprite(struct Sprite*s){if(serials[Slot(s)]){Event(17,s,0);State(s,3);}SourceDestroySprite(s);if(serials[Slot(s)]){Event(18,s,0);State(s,4);}}
void SpriteCallbackDummy(struct Sprite*s){SourceSpriteCallbackDummy(s);if(serials[Slot(s)]&&s->inUse){callbackCounts[Slot(s)]++;Event(5,s,0);}}
static void Suffix(struct Sprite*s){u32 before=gRngValue;printf("F,%d,%d,%d,%d,%"PRIu32",%d,%d",caseId,eventId++,tick,serials[Slot(s)],before,drawCount,gBattleAnimArgs[2]);for(int n=0;n<8;n++)printf(",%u",Random());puts("");gRngValue=before;}
static void AnimFistOrFootRandomPos(struct Sprite*s){int i=Slot(s);callbackCounts[i]++;Event(1,s,0);int previous=currentParent;currentParent=serials[i];SourceAnimFistOrFootRandomPos(s);currentParent=previous;Event(2,s,s->data[7]);State(s,1);if(s->data[7]!=64)State(&gSprites[s->data[7]],1);else Event(19,s,64);Suffix(s);}
static void AnimFistOrFootRandomPos_Step(struct Sprite*s){callbackCounts[Slot(s)]++;Event(3,s,0);SourceAnimFistOrFootRandomPos_Step(s);Event(4,s,0);}
static void Reset(u32 seed){
 memset(&sDummySprite,0,sizeof(sDummySprite));sDummySprite.callback=SpriteCallbackDummy;sDummySprite.x=304;sDummySprite.y=160;sDummySprite.subpriority=255;sDummySprite.anims=gDummySpriteAnimTable;sDummySprite.affineAnims=gDummySpriteAffineAnimTable;
 for(int i=0;i<64;i++)gSprites[i]=sDummySprite;
 memset(serials,0,sizeof(serials));memset(kinds,0,sizeof(kinds));memset(parents,0,sizeof(parents));memset(callbackCounts,0,sizeof(callbackCounts));memset(matrixWrites,0,sizeof(matrixWrites));memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));
 for(int i=0;i<32;i++){AffineAnimStateReset(i);SetOamMatrix(i,256,0,0,256);}
 gOamMatrixAllocBitmap=0;gRngValue=seed;eventId=nextSerial=currentParent=drawCount=gAnimVisualTaskCount=0;tick=-1;
}
static void Reserve(int i){gSprites[i]=sDummySprite;gSprites[i].inUse=TRUE;gSprites[i].usingSheet=TRUE;gSprites[i].animPaused=TRUE;gSprites[i].affineAnimPaused=TRUE;gSprites[i].animBeginning=FALSE;gSprites[i].affineAnimBeginning=FALSE;}
static void Boundary(void){if(!Selected())return;u32 before=gRngValue;int draws=drawCount;printf("B,%d,%d,%d,%d,%"PRIu32",%d,%"PRIu32",%d\n",caseId,tick,Live(),gAnimVisualTaskCount,gOamMatrixAllocBitmap,drawCount,gRngValue,gBattleAnimArgs[2]);for(int i=0;i<64;i++)if(serials[i]&&gSprites[i].inUse)State(&gSprites[i],5);assert(gRngValue==before&&drawCount==draws);}
struct Config{int actor,target,arg0,duration,arg2;u32 seed;int profile,layout,count,subpriority;};
'''
    keys=['actor','target','arg0','duration','arg2','seed','profile','layout','count','subpriority']
    h+='static const struct Config configs[]={'+','.join('{'+','.join(str(c[k])+('u' if k=='seed' else '') for k in keys)+'}' for c in configs)+'};\n'
    h+=r'''
int main(void){
 for(caseId=0;caseId<(int)(sizeof(configs)/sizeof(configs[0]));caseId++){
  const struct Config*c=&configs[caseId];Reset(c->seed);gBattleAnimAttacker=c->actor;gBattleAnimTarget=c->target;profile=c->profile;originalDuration=c->duration;gBattleAnimArgs[0]=c->arg0;gBattleAnimArgs[1]=c->duration;gBattleAnimArgs[2]=c->arg2;
  printf("K,%d\n",caseId);
  // 0=script eager parent; 1=raw parent, higher child; 2=raw parent, lower child;
  // 3=script eager parent with all remaining 63 slots reserved (allocation fails).
  if(c->layout==2)Reserve(0);if(c->layout==3)for(int i=1;i<64;i++)Reserve(i);
  for(int n=0;n<c->count;n++){
   u8 id=c->layout==1||c->layout==2?CreateSprite(&gFistFootRandomPosSpriteTemplate,7,11,c->subpriority):CreateSpriteAndAnimate(&gFistFootRandomPosSpriteTemplate,7,11,c->subpriority);assert(id<64);gAnimVisualTaskCount++;
  }
  if(c->layout==2)SourceDestroySprite(&gSprites[0]);Boundary();
  u32 stableRng=gRngValue;int stableDraws=drawCount;
  for(tick=0;Live();tick++){
   assert(tick<32770);AnimateSprites();Boundary();
   if(tick==0){stableRng=gRngValue;stableDraws=drawCount;}else assert(gRngValue==stableRng&&drawCount==stableDraws);
  }
  assert(!Live()&&gAnimVisualTaskCount==0&&gOamMatrixAllocBitmap==0);
  printf("Z,%d,%d,%d,%d,%"PRIu32",%u\n",caseId,tick,nextSerial,drawCount,gRngValue,gOamMatrixAllocBitmap);
 }
}
'''
    with tempfile.TemporaryDirectory() as tmp:
        c=Path(tmp)/'random_fist.c'; exe=Path(tmp)/'random_fist'; c.write_text(h)
        flags=['-fsanitize=undefined','-fno-sanitize-recover=all'] if sanitize and os.name!='nt' else []
        ref.compile_c(['cc','-std=gnu11','-O0',*flags,str(c),'-o',str(exe)])
        output=subprocess.check_output([str(exe)],text=True,env={**os.environ,'UBSAN_OPTIONS':'halt_on_error=1:print_stacktrace=1'})
    cases={}
    for line in output.splitlines():
        kind,key,*data=line.split(',');key=int(key)
        if kind=='K':cases[key]={**configs[key],'states':[],'events':[],'rng':[],'allocations':[],'suffixes':[],'boundaries':[]}
        else:
            field={'S':'states','E':'events','R':'rng','A':'allocations','F':'suffixes','B':'boundaries','Z':'end'}[kind]
            values=list(map(int,data))
            if field=='end':cases[key][field]=values
            else:cases[key][field].append(values)
    validate(cases,profiles)
    # Canonical duration20/layout0 retains every transition. Other variants retain
    # the complete RNG/allocation/free history and meaningful frame boundaries.
    for case in cases.values():
        if case['name']=='duration20_layout0':continue
        boundary_ticks={-1,0,1,7,8,9,case['end'][0]-2,case['end'][0]-1}
        if case['duration']<0:boundary_ticks|={32766,32767,32768}
        case['states']=[r for r in case['states'] if r[2] in [0,1,3,4] or r[1] in boundary_ticks]
        case['events']=[r for r in case['events'] if r[2] in [0,1,2,12,13,14,15,16,17,18,19] or r[1] in boundary_ticks]
        case['boundaries']=[r for r in case['boundaries'] if r[0] in boundary_ticks]
    return {'sourceCommit':ref.COMMIT,
     'coordinates':COORDINATES,'dimensionProfiles':profiles,'seeds':SEEDS,'geometries':GEOMETRIES,
     'stateColumns':STATE_COLUMNS,'statePhases':PHASES,'eventColumns':EVENT_COLUMNS,'eventOperations':OPS,
     'boundaryColumns':BOUNDARY_COLUMNS,'rngColumns':RNG_COLUMNS,'allocationColumns':ALLOCATION_COLUMNS,
     'suffixColumns':SUFFIX_COLUMNS,'endColumns':['nextTickAfterLastPass','allocations','rngDraws','rngState','matrixBitmap'],
     'kinds':{'1':'counted script parent','2':'uncounted raw hit-splat child'},
     'callbacks':['random-fist initializer','random-fist step','SpriteCallbackDummy','AnimHitSplatBasic (must never execute)'],
     'layouts':['script eager parent','raw parent with higher child','raw parent with released lower slot','all other 63 slots reserved'],
     'notes':[
      'Tick -1 is setup. Layout0/3 uses exact CreateSpriteAndAnimate: parent initializer then frame/affine player. Script increments visual count after that call.',
      'Raw children use CreateSprite only, then StartSpriteAffineAnim(0) and SpriteCallbackDummy; no eager child callback, BeginAnim, or BeginAffineAnim occurs.',
      'AnimateSprites visits live slots in ascending order: a child created above its parent receives a callback/frame/affine update during the same pass; a lower child waits until the next pass.',
      'Each AnimateSprite processes the frame player before the affine player. Events preserve this ordering and every explicit allocation/free/destroy boundary.',
      'The source dummy frame command is END: BeginAnim leaves child animBeginning true and animEnded false. Its normalized sheet tile remains0. Hit-splat affine0 begins at scale256 and delay7, END becomes true on its ninth player update.',
      'Parent destruction first frees the raw child matrix explicitly, then destroys that raw child, then DestroyAnimSprite frees any parent matrix, destroys the parent, and decrements only the counted parent visual count.',
      'The source callback treats any nonzero arg0 as target and mutates negative shared arg2 to Random()%5. Fixed arg2 consumes four draws, negative arg2 consumes five. A second parent sees the shared mutation and consumes four.',
      'Coordinates are explicit picture-anchor harness inputs. Dimensions are extracted pinned source values: player back dimensions and opponent front dimensions, with Castform form-specific front values. These are not inferred species screen anchors.',
      'OAM tile/palette sheet bases are normalized to0. Source template commands, OAM shape32x32, affine-on/off mode, and relative tile values are exact. Zero-rotation BIOS result is exact diagonal reciprocal scale; nonzero rotation aborts.',
      'Reservations are initialized inert source-compatible objects: valid dummy tables/callback, usingSheet true, animBeginning false, affineAnimBeginning false, and both animation players paused. No uninitialized dummy-array traversal occurs.',
      'RNG suffixes run eight exact source Random draws on a copied state immediately after each initializer, then restore it. State/boundary observations consume no RNG. After parent initialization every later callback preserves RNG state.',
      'The signed duration -32768 fixture executes the whole lifetime. The first decrement narrows -32769 to32767, then reaches0 and destroys at tick32768. Explicit compiler narrowing assertions document this GBA-compatible signed16 behavior.',
      'LiveCount counts tracked family parents and children, excluding inert reservations. VisualCount counts only parents. Matrix bitmap includes allocated raw child matrices.'],
     'sanitizer':'undefined; no-recover; halt_on_error' if sanitize and os.name!='nt' else 'not enabled',
     'cases':cases}


def validate(cases,profiles):
    assert len(cases)==52,len(cases)
    C={name:i for i,name in enumerate(STATE_COLUMNS)}
    def get(row,name):return row[C[name]]
    for case in cases.values():
        start=case['seed']
        for i,row in enumerate(case['rng']):
            _,_,_,draw,before,value,after=row
            assert draw==i and before==start and after==(before*1103515245+24691)&0xffffffff and value==after>>16
            start=after
        expected_draws=4*case['count']+int(case['arg2']<0)
        assert len(case['rng'])==expected_draws
        assert case['end'][2:]==[expected_draws,start,0]
        for suffix in case['suffixes']:
            state=suffix[3]
            for value in suffix[6:]:
                state=(state*1103515245+24691)&0xffffffff;assert value==state>>16
        parents=[a for a in case['allocations'] if a[4]==1]
        children=[a for a in case['allocations'] if a[4]==2]
        assert len(parents)==case['count'] and len(children)==(0 if case['layout']==3 else case['count'])
        for allocation in parents:
            serial=allocation[2];rows=[s for s in case['states'] if get(s,'serial')==serial]
            initial=next(s for s in rows if get(s,'phase')==1)
            draws=[r[5] for r in case['rng'] if r[2]==serial]
            anim=draws.pop(0)%5 if len(draws)==5 else get(initial,'arg2')
            battler=case['actor'] if case['arg0']==0 else case['target'];w,h=profiles[case['profile']]['front' if battler&1 else 'back']
            x=draws[0]%(w//2);y=draws[1]%(h//4)
            if draws[2]&1:x=-x
            if draws[3]&1:y=-y
            if not battler&1:y-=16
            assert [get(initial,'x'),get(initial,'y')]==[COORDINATES[battler][0]+x,COORDINATES[battler][1]+y]
            assert get(initial,'data0')==case['duration'] and get(initial,'animNum')==anim
            frames=[s for s in rows if get(s,'phase')==2]
            assert all(get(s,'tile')==[0,16,32,48,48][anim] for s in frames)
            assert all(get(s,'hflip')==int(anim==4) for s in frames)
            dead=next(s for s in rows if get(s,'phase')==3)
            expected_death=(case['duration'] if case['duration']>=0 else 32768)+int(case['layout'] in [1,2])
            assert get(dead,'tick')==expected_death,(case['name'],dead)
            if case['layout']==3:assert get(initial,'data7')==64
            else:
                child=next(a for a in children if a[5]==serial);child_rows=[s for s in case['states'] if get(s,'serial')==child[2]]
                init=next(s for s in child_rows if get(s,'phase')==1)
                assert [get(init,n) for n in ['callback','callbackCount','frameBeginning','affineBeginning','matrixWrites','scaleX','matrixA']]==[2,0,1,1,0,256,256]
                assert get(init,'subpriority')==(case['subpriority']+1)&255
                allocated=next(s for s in child_rows if get(s,'phase')==0)
                assert get(allocated,'callback')==3 and get(allocated,'callbackCount')==0
                ev=case['events']
                allocation_event=next(e for e in ev if e[2]==0 and e[3]==child[2])
                start_enter=next(e for e in ev if e[2]==12 and e[3]==child[2])
                start_exit=next(e for e in ev if e[2]==13 and e[3]==child[2])
                init_exit=next(e for e in ev if e[2]==2 and e[3]==serial)
                assert allocation_event[0]<start_enter[0]<start_exit[0]<init_exit[0]
                assert start_enter[5]==start_exit[5]==0
                assert not any(e[2]==16 and e[3]==child[2] for e in ev)
                callbacks=[e for e in ev if e[2]==5 and e[3]==child[2]]
                if callbacks:assert init_exit[0]<callbacks[0][0]
                else:assert case['duration']==0 and case['layout']==0
                child_frames=[s for s in child_rows if get(s,'phase')==2]
                if child_frames:
                    expected_first=1 if case['layout']==2 else 0
                    assert get(child_frames[0],'tick')==expected_first
                assert all([get(s,n) for n in ['tile','frameBeginning','frameEnded','scaleX','scaleY','matrixA','matrixD']]==[0,1,0,256,256,256,256] for s in child_frames)
                for s in child_frames:
                    assert get(s,'affineEnded')==int(get(s,'callbackCount')>=9),(case['name'],s)
                    assert get(s,'matrixWrites')==get(s,'callbackCount')
                child_dead=next(s for s in child_rows if get(s,'phase')==3)
                assert get(child_dead,'tick')==expected_death and get(child_dead,'matrixBitmap')&(1<<get(child_dead,'matrix'))==0
                ev=case['events'];free=next(e for e in ev if e[2]==14 and e[5]==get(child_dead,'matrix'));cd=next(e for e in ev if e[2]==17 and e[3]==child[2]);pd=next(e for e in ev if e[2]==17 and e[3]==serial)
                assert free[0]<cd[0]<pd[0]
        assert case['boundaries'][-1][1:4]==[0,0,0]
        initialized=case['suffixes'][-1]
        for boundary in case['boundaries']:
            if boundary[0]>=initialized[1]:assert boundary[4:7]==[expected_draws,start,initialized[5]]
        # Every observed AnimateSprite explicitly enters frame before affine.
        for enter in (e for e in case['events'] if e[2]==6):
            matching=[e for e in case['events'] if e[0]>enter[0] and e[3]==enter[3]]
            stop=next(e for e in matching if e[2]==7)
            within=[e for e in matching if e[0]<stop[0]]
            frame=[e for e in within if e[2]==8];affine=[e for e in within if e[2]==10]
            if frame and affine:assert frame[0][0]<affine[0][0]


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--repo',type=Path,default=Path.cwd())
    parser.add_argument('--reference',type=Path)
    parser.add_argument('--helper',type=Path)
    parser.add_argument('--extractor',type=Path)
    parser.add_argument('--output',type=Path,default=Path(__file__).with_name('random-fist-source-vectors.json'))
    parser.add_argument('--no-sanitize',action='store_true')
    args=parser.parse_args();repo=args.repo.resolve()
    result=build(repo,args.reference or repo/'content/raw/pokefirered/037335f4',args.helper or repo/'tests/Reference/reference_c.py',args.extractor or repo/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py',not args.no_sanitize)
    args.output.write_text(json.dumps(result,separators=(',',':'))+'\n')
    print(str(args.output)+': '+str(len(result['cases']))+' cases, '+str(args.output.stat().st_size)+' bytes; '+result['sanitizer'])

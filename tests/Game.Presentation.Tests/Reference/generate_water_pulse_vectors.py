#!/usr/bin/env python3
"""Pinned extracted-C Water Pulse oracle; no managed compiler or upstream script.

The registry, allocation, frame interpreter, affine interpreter, callbacks and
Random implementation are verbatim source bodies. Observation wrappers rename
symbols, never approximate phase or movement. Resource lookup uses known sheets;
zero-rotation ObjAffineSet is exact diag(input reciprocal scales). Nonzero
rotation and unsupported resource paths abort. All source functions run before
sampling. Tick -1 is script CreateSpriteAndAnimate, subsequent ticks are normal
AnimateSprites. The extracted script delay driver produces three-parent
creation at -1,7,14: initial creation is setup before tick0; later script
callbacks run before the live sprite pass, beginning at tick1.

No original battler/healthbox slot IDs are claimed. Empty local registry is the
normal fixture; reuse fixtures reserve invisible slots and release lower slots
before the first burst. Raw lower slots wait for the next live ascending pass. A separate pair at -1,20
uses natural parent/child destruction for lower-slot reuse without reservations.
Raw children get neither eager BeginAnim nor eager BeginAffineAnim.
"""
import argparse, importlib.util, json, re, subprocess, sys, tempfile
from pathlib import Path
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[3]
SEEDS = [0, 1, 0xFFFFFFFF, 0x12345678, 0x80000000, 0x7FFFFFFF]
GEOMETRIES = [[0,1],[1,0],[0,2],[2,0]]
FLOAT_ARGS = [[100,100,8,1,20,40,0],[20,100,16,2,10,35,1],
 [200,80,8,1,40,20,0],[80,60,10,3,20,50,0],[140,100,16,1,20,30,1]]
PROBES = [[20,-129,128,32760,-32760],[20,32767,-32768,1,-1],
 [2,-1,1,-128,127],[20,-256,-257,-32760,32760]]
STATE_COLUMNS = ['event','tick','serial','slot','phase','callbackCount','x','y','x2','y2',
 'data0','data1','data2','data3','data4','data5','tile','frameBeginning','frameEnded',
 'affineBeginning','affineEnded','scaleX','matrixA','affineCmdIndex','affineDelay','matrixWrites','subpriority','visualCount','drawCount']
RNG_COLUMNS = ['event','tick','parentSerial','drawIndex','before','result','after']
RENDERED_COLUMNS = ['tick','slot','kind','x','y','x2','y2','tile','matrixA','subpriority','remainingLifetime','drawCount']

def load(name, path):
 spec=importlib.util.spec_from_file_location(name,path); module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module

def build(reference, helper, extractor):
 ref=load('water_reference_c',helper);ref.verify_reference(reference)
 extract=load('water_extract',extractor)
 function,declaration=extract.function,extract.declaration
 src={n:(reference/('src/'+n+'.c')).read_text() for n in ['sprite','battle_anim_water','battle_anim_effects_2','battle_anim_mons','battle_anim','trig','random']}
 header=(reference/'include/sprite.h').read_text()
 sp=src['sprite'];wa=src['battle_anim_water']
 ordinary=['AnimateSprites','CreateSprite','CreateSpriteAt','CreateSpriteAndAnimate','ResetSprite',
 'BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop',
 'BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','DecrementAnimDelayCounter',
 'BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_jump','AffineAnimCmd_end',
 'AffineAnimCmd_frame','AffineAnimStateRestartAnim','AffineAnimStateReset','ApplyAffineAnimFrameAbsolute',
 'DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam',
 'GetAffineAnimFrame','ApplyAffineAnimFrame','GetSpriteMatrixNum','AllocOamMatrix',
 'FreeOamMatrix','InitSpriteAffineAnim','SetSpriteSheetFrameTileNum']
 wrapped=['AnimateSprite','DestroySprite','FreeSpriteOamMatrix','CopyOamMatrix']
 water_funcs=['AnimWaterPulseBubble','AnimWaterPulseBubble_Step','AnimWaterPulseRingBubble',
 'AnimWaterPulseRing','AnimWaterPulseRing_Step','CreateWaterPulseRingBubbles']
 funcs=[function(sp,n) for n in ordinary+wrapped]+[function(wa,n) for n in water_funcs]
 funcs += [function(src['battle_anim_mons'],n) for n in ['InitSpritePosToAnimAttacker','SetAnimSpriteInitialXOffset','GetBattlerSpriteSubpriority']]
 funcs += [function(src['battle_anim'],n) for n in ['DestroyAnimSprite','Cmd_delay','WaitAnimFrameCount','RunAnimScriptCommand']]
 funcs += [function(src['trig'],'Sin')]
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
#define B_POSITION_PLAYER_LEFT 0
#define B_POSITION_OPPONENT_LEFT 1
#define B_POSITION_PLAYER_RIGHT 2
#define B_POSITION_OPPONENT_RIGHT 3
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define sAnchorX data[6]
#define sAnchorY data[7]
#define SAFE_DIV(a,b) (assert((b)!=0),(a)/(b))
_Static_assert((-1 >> 1) == -1,"Pinned source requires arithmetic signed shifts");
'''
 h+=header[header.index('struct AnimFrameCmd\n'):header.index('\nenum\n')]
 h+='\n'+re.search(r'^#define Q_8_8\(.*$',(reference/'include/global.h').read_text(),re.M)[0]+'\n'
 h+=re.search(r'^const s16 gSineTable\[\]\s*=\s*\{.*?\};',src['trig'],re.M|re.S)[0]+'\n'
 h+='\n'.join(re.findall(r'^#define (?:RAND_MULT|ISO_RANDOMIZE1)\b.*$',(reference/'include/random.h').read_text(),re.M))+'\n'
 h+=r'''
struct Sprite;typedef void (*SpriteCallback)(struct Sprite*);
struct OamData {u8 affineMode,shape,size,matrixNum,paletteNum;u16 tileNum;};
struct SpriteFrameImage {const void *data;u16 size;};
struct SpriteTemplate {u16 tileTag,paletteTag;const struct OamData*oam;const union AnimCmd *const *anims;const struct SpriteFrameImage*images;const union AffineAnimCmd *const *affineAnims;SpriteCallback callback;};
struct Sprite {
 struct OamData oam;const union AnimCmd *const *anims;const struct SpriteFrameImage*images;
 const union AffineAnimCmd *const *affineAnims;const struct SpriteTemplate*template;SpriteCallback callback;
 s16 x,y,x2,y2,data[8];u8 animNum,animCmdIndex,animLoopCounter,subpriority;u8 animDelayCounter:6;
 bool8 inUse,animEnded,animBeginning,animPaused,usingSheet,hFlip,vFlip;
 bool8 affineAnimBeginning,affineAnimEnded,affineAnimPaused,anchored;u16 sheetTileStart;
};
struct ObjAffineSrcData{s16 xScale,yScale;u16 rotation;};struct OamMatrix{s16 a,b,c,d;};
static struct Sprite gSprites[MAX_SPRITES+1],sDummySprite;
static struct AffineAnimState sAffineAnimStates[32];static struct OamMatrix gOamMatrices[32];
static u32 gOamMatrixAllocBitmap,gRngValue;static bool8 gAffineAnimsDisabled;
static u8 gBattleAnimAttacker,gBattleAnimTarget,gAnimVisualTaskCount;static s16 gBattleAnimArgs[8];
static int tick,caseId,eventId,nextSerial,currentParent,drawCount;
static s16 sAnimFramesToWait;static bool8 gAnimScriptActive;static const u8*sBattleAnimScriptPtr;static void(*gAnimScriptCallback)(void);
static void HarnessCreateRingCommand(void);static void HarnessEndCommand(void);
static int serials[65],kinds[65],parents[65],callbackCounts[65],matrixWrites[65];
static const u8 coords[4][2]={{32,88},{200,40},{90,96},{152,32}};
static const struct SpriteTemplate gWaterPulseRingBubbleSpriteTemplate,gWaterPulseBubbleSpriteTemplate,gWaterPulseRingSpriteTemplate;
static void ObserveState(struct Sprite*,int);static void ObserveEvent(int,struct Sprite*);
static u8 GetBattlerSide(u8 b){assert(b<4);return b&1;}
static u8 GetBattlerSpriteCoord(u8 b,u8 c){assert(b<4&&c<4);return coords[b][c&1];}
static u8 GetBattlerSpriteCoord2(u8 b,u8 c){return GetBattlerSpriteCoord(b,c);}
static u8 GetBattlerPosition(u8 b){assert(b<4);return b;}
static void CalcCenterToCornerVec(struct Sprite*s,u8 a,u8 b,u8 c){(void)s;(void)a;(void)b;(void)c;}
static s16 AllocSpriteTiles(u8 n){abort();}
static u16 GetSpriteTileStartByTag(u16 n){assert(n==1||n==2);return 0;}
static u8 IndexOfSpritePaletteTag(u16 n){assert(n==1||n==2);return 0;}
static void SetSpriteOamFlipBits(struct Sprite*s,u8 h,u8 v){s->hFlip=h;s->vFlip=v;}
static void RequestSpriteFrameImageCopy(u16 a,u16 b,const struct SpriteFrameImage*c){abort();}
static void UpdateSpriteMatrixAnchorPos(struct Sprite*s,s32 x,s32 y){abort();}
static void SetOamMatrix(u8 n,s16 a,s16 b,s16 c,s16 d){gOamMatrices[n]=(struct OamMatrix){a,b,c,d};}
// BIOS ObjAffineSet at rotation0: source already supplied reciprocal scales.
static void ObjAffineSet(const struct ObjAffineSrcData*s,struct OamMatrix*d,int n,int stride){assert(n==1&&stride==2&&s->rotation==0);*d=(struct OamMatrix){s->xScale,0,0,s->yScale};}
static void UnsupportedAffineLoop(u8 n,struct Sprite*s){abort();}
static void Dummy(struct Sprite*s){(void)s;}
'''
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'
 h+=r'''
static void (*sScriptCmdTable[])(void)={HarnessCreateRingCommand,Cmd_delay,HarnessEndCommand};
static void (*sAnimFuncs[])(struct Sprite*)={ContinueAnim,BeginAnim};
static void (*sAnimCmdFuncs[])(struct Sprite*)={AnimCmd_loop,AnimCmd_jump,AnimCmd_end,AnimCmd_frame};
static void (*sAffineAnimFuncs[])(struct Sprite*)={ContinueAffineAnim,BeginAffineAnim};
static void (*sAffineAnimCmdFuncs[])(u8,struct Sprite*)={UnsupportedAffineLoop,AffineAnimCmd_jump,AffineAnimCmd_end,AffineAnimCmd_frame};
'''
 h+=function(src['random'],'Random')+r'''
static u16 ObservedRandom(void){u32 before=gRngValue;u16 r=Random();printf("R,%d,%d,%d,%d,%d,%"PRIu32",%u,%"PRIu32"\n",caseId,eventId++,tick,currentParent,drawCount++,before,r,gRngValue);return r;}
#define Random ObservedRandom
'''
 for f in funcs:
  name=re.search(r'(\w+)\([^;]+?\)\n\{',f).group(1)
  if name in wrapped+water_funcs+['CreateSpriteAt']:
   h+='#define '+name+' Source'+name+'\n'+f+'\n#undef '+name+'\n'
  else:h+=f+'\n'
 h+='#undef Random\n'
 for n in ['sAnim_WaterPulseBubble_0','sAnim_WaterPulseBubble_1','sAffineAnim_WaterPulseRingBubble_0','sAffineAnim_WaterPulseRingBubble_1']:
  h+=declaration(wa,n)+'\n'
 h+=declaration(src['battle_anim_effects_2'],'sWaterPulseRingAffineAnimCmds')+'\n'
 h+=r'''
static const union AnimCmd dummyAnim=ANIMCMD_END;static const union AffineAnimCmd dummyAffine=AFFINEANIMCMD_END;
static const union AnimCmd *const dummyAnims[]={&dummyAnim};static const union AffineAnimCmd *const dummyAffines[]={&dummyAffine};
static const union AnimCmd *const bubbles[]={sAnim_WaterPulseBubble_0,sAnim_WaterPulseBubble_1};
static const union AffineAnimCmd *const childAffines[]={sAffineAnim_WaterPulseRingBubble_0,sAffineAnim_WaterPulseRingBubble_1};
static const union AffineAnimCmd *const ringAffines[]={sWaterPulseRingAffineAnimCmds};
static const struct OamData floatOam={0,0,0},childOam={1,0,0},ringOam={3,2,2};
static const struct SpriteTemplate gWaterPulseBubbleSpriteTemplate={1,1,&floatOam,bubbles,NULL,dummyAffines,AnimWaterPulseBubble};
static const struct SpriteTemplate gWaterPulseRingBubbleSpriteTemplate={1,1,&childOam,bubbles,NULL,childAffines,AnimWaterPulseRingBubble};
static const struct SpriteTemplate gWaterPulseRingSpriteTemplate={2,2,&ringOam,dummyAnims,NULL,ringAffines,AnimWaterPulseRing};
static void ObserveEvent(int op,struct Sprite*s){int i=(int)(s-gSprites);printf("E,%d,%d,%d,%d,%d,%d,%d\n",caseId,eventId++,tick,op,serials[i],i,parents[i]);}
static void ObserveState(struct Sprite*s,int phase){
 int i=(int)(s-gSprites),affine=kinds[i]!=1;struct AffineAnimState*a=&sAffineAnimStates[s->oam.matrixNum];
 int row[]={eventId++,tick,serials[i],i,phase,callbackCounts[i],s->x,s->y,s->x2,s->y2,
 s->data[0],s->data[1],s->data[2],s->data[3],s->data[4],s->data[5],s->oam.tileNum,
 s->animBeginning,s->animEnded,s->affineAnimBeginning,s->affineAnimEnded,
 affine?a->xScale:256,affine?gOamMatrices[s->oam.matrixNum].a:256,
 affine?a->animCmdIndex:0,affine?a->delayCounter:0,matrixWrites[i],s->subpriority,gAnimVisualTaskCount,drawCount};
 printf("S,%d",caseId);for(unsigned j=0;j<sizeof(row)/sizeof(row[0]);j++)printf(",%d",row[j]);puts("");
}
u8 CreateSpriteAt(u8 i,const struct SpriteTemplate*t,s16 x,s16 y,u8 subpriority){
 u8 result=SourceCreateSpriteAt(i,t,x,y,subpriority);assert(result==i);
 serials[i]=++nextSerial;kinds[i]=t==&gWaterPulseBubbleSpriteTemplate?1:t==&gWaterPulseRingSpriteTemplate?2:3;
 parents[i]=currentParent;callbackCounts[i]=matrixWrites[i]=0;ObserveEvent(0,&gSprites[i]);
 printf("A,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",caseId,serials[i],i,kinds[i],parents[i],tick,x,y,subpriority);
 if(kinds[i]!=3)ObserveState(&gSprites[i],0);
 return result;
}
void CopyOamMatrix(u8 n,struct OamMatrix*m){SourceCopyOamMatrix(n,m);for(int i=0;i<64;i++)if(gSprites[i].inUse&&(gSprites[i].oam.affineMode&1)&&gSprites[i].oam.matrixNum==n)matrixWrites[i]++;}
void AnimateSprite(struct Sprite*s){int i=(int)(s-gSprites);SourceAnimateSprite(s);if(!serials[i])return;ObserveEvent(2,s);ObserveState(s,1);}
void FreeSpriteOamMatrix(struct Sprite*s){ObserveEvent(3,s);SourceFreeSpriteOamMatrix(s);}
void DestroySprite(struct Sprite*s){if(serials[s-gSprites]){ObserveEvent(4,s);ObserveState(s,2);}SourceDestroySprite(s);}
#define WRAP(name) static void name(struct Sprite*s){int i=(int)(s-gSprites);callbackCounts[i]++;ObserveEvent(1,s);int p=currentParent;currentParent=serials[i];Source##name(s);currentParent=p;}
WRAP(AnimWaterPulseBubble)
WRAP(AnimWaterPulseBubble_Step)
WRAP(AnimWaterPulseRingBubble)
void AnimWaterPulseRing(struct Sprite*s){int i=(int)(s-gSprites);callbackCounts[i]++;ObserveEvent(1,s);SourceAnimWaterPulseRing(s);}
WRAP(AnimWaterPulseRing_Step)
static void CreateWaterPulseRingBubbles(struct Sprite*s,s32 x,s32 y){int before=nextSerial;SourceCreateWaterPulseRingBubbles(s,x,y);for(int i=0;i<64;i++)if(gSprites[i].inUse&&serials[i]>before)ObserveState(&gSprites[i],0);}
static void Reset(u32 seed){
 memset(&sDummySprite,0,sizeof(sDummySprite));sDummySprite.callback=Dummy;sDummySprite.x=304;sDummySprite.y=160;sDummySprite.subpriority=255;sDummySprite.anims=dummyAnims;sDummySprite.affineAnims=dummyAffines;
 for(int i=0;i<65;i++)gSprites[i]=sDummySprite;
 memset(serials,0,sizeof(serials));memset(kinds,0,sizeof(kinds));memset(parents,0,sizeof(parents));memset(callbackCounts,0,sizeof(callbackCounts));memset(matrixWrites,0,sizeof(matrixWrites));
 memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));for(int i=0;i<32;i++){AffineAnimStateReset(i);SetOamMatrix(i,256,0,0,256);}
 gOamMatrixAllocBitmap=0;gRngValue=seed;eventId=nextSerial=currentParent=drawCount=gAnimVisualTaskCount=0;tick=-1;
}
static void ObserveRendered(void){
 u32 rng=gRngValue;int draws=drawCount;
 for(int i=0;i<64;i++)if(gSprites[i].inUse&&serials[i]){
  struct Sprite*s=&gSprites[i];int remaining=kinds[i]==1?s->data[3]:kinds[i]==2?s->data[3]-s->data[0]+1:s->data[0];
  int matrix=kinds[i]==1?256:gOamMatrices[s->oam.matrixNum].a;
  printf("Q,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",caseId,tick,i,kinds[i],s->x,s->y,s->x2,s->y2,s->oam.tileNum,matrix,s->subpriority,remaining,drawCount);
 }assert(gRngValue==rng&&drawCount==draws);
}
static int Active(void){int n=0;for(int i=0;i<64;i++)n+=gSprites[i].inUse&&serials[i]!=0;return n;}
static void CreateRing(void){gBattleAnimArgs[0]=gBattleAnimArgs[1]=0;gBattleAnimArgs[2]=40;gBattleAnimArgs[3]=15;CreateSpriteAndAnimate(&gWaterPulseRingSpriteTemplate,coords[gBattleAnimTarget][0],coords[gBattleAnimTarget][1],GetBattlerSpriteSubpriority(gBattleAnimTarget)-2);gAnimVisualTaskCount++;}
static void HarnessCreateRingCommand(void){sBattleAnimScriptPtr++;CreateRing();}
static void HarnessEndCommand(void){sBattleAnimScriptPtr++;gAnimScriptActive=0;}
static const u8 ringScript[]={0,1,5,0,1,5,0,2};
static void StartRingScript(void){sBattleAnimScriptPtr=ringScript;sAnimFramesToWait=0;gAnimScriptActive=1;gAnimScriptCallback=RunAnimScriptCommand;gAnimScriptCallback();}
static void EndCase(void){printf("Z,%d,%d,%d,%d,%"PRIu32",%u\n",caseId,tick,nextSerial,drawCount,gRngValue,gOamMatrixAllocBitmap);assert(!Active()&&gAnimVisualTaskCount==0&&gOamMatrixAllocBitmap==0);caseId++;}
'''
 h+='static const u32 seeds[]={'+','.join(str(x)+'u' for x in SEEDS)+'};\n'
 h+='static const u8 geometries[][2]={'+','.join('{'+','.join(map(str,x))+'}' for x in GEOMETRIES)+'};\n'
 h+='static const s16 floatArgs[][7]={'+','.join('{'+','.join(map(str,x))+'}' for x in FLOAT_ARGS)+'};\n'
 h+='static const s16 probes[][5]={'+','.join('{'+','.join(map(str,x))+'}' for x in PROBES)+'};\n'
 h+=r'''
int main(void){
 for(int f=0;f<5;f++){
  Reset(0);printf("K,%d,float,%d,0,0,0\n",caseId,f);memcpy(gBattleAnimArgs,floatArgs[f],sizeof(floatArgs[f]));
  CreateSpriteAndAnimate(&gWaterPulseBubbleSpriteTemplate,200,40,32);gAnimVisualTaskCount++;ObserveRendered();
  for(tick=0;Active();tick++){assert(tick<60);AnimateSprites();ObserveRendered();}EndCase();
 }
 for(int g=0;g<4;g++)for(unsigned seed=0;seed<sizeof(seeds)/sizeof(seeds[0]);seed++){
  Reset(seeds[seed]);gBattleAnimAttacker=geometries[g][0];gBattleAnimTarget=geometries[g][1];printf("K,%d,ring,%d,%u,0,0\n",caseId,g,seed);CreateRing();ObserveRendered();
  for(tick=0;Active();tick++){assert(tick<70);AnimateSprites();ObserveRendered();}EndCase();
 }
 for(int layout=0;layout<4;layout++){
  Reset(0x12345678);gBattleAnimAttacker=0;gBattleAnimTarget=1;printf("K,%d,%s,0,3,%d,0\n",caseId,layout==3?"pair":"schedule",layout);
  // layout1 frees both lower slots; layout2 frees only slot0, so each burst
  // emits one deferred lower child and one same-pass higher child.
  if(layout==1||layout==2)for(int i=0;i<2;i++){gSprites[i]=sDummySprite;gSprites[i].inUse=1;gSprites[i].usingSheet=1;gSprites[i].animPaused=1;gSprites[i].affineAnimPaused=1;}
  if(layout==3)CreateRing();else StartRingScript();ObserveRendered();
  for(tick=0;Active();tick++){
   assert(tick<80);if((layout==1||layout==2)&&tick==14){SourceDestroySprite(&gSprites[0]);if(layout==1)SourceDestroySprite(&gSprites[1]);}
   if(layout==3){if(tick==20)CreateRing();}else if(tick>0&&gAnimScriptActive)gAnimScriptCallback();
   AnimateSprites();ObserveRendered();
  }EndCase();
 }
 for(int p=0;p<4;p++){
  Reset(0);printf("K,%d,probe,%d,0,0,0\n",caseId,p);u8 i=CreateSprite(&gWaterPulseRingBubbleSpriteTemplate,100,80,29);memcpy(gSprites[i].data,probes[p],sizeof(probes[p]));ObserveState(&gSprites[i],0);ObserveRendered();
  for(tick=0;Active();tick++){assert(tick<20);AnimateSprites();ObserveRendered();}EndCase();
 }
}
'''
 with tempfile.TemporaryDirectory() as tmp:
  c=Path(tmp)/'water.c';exe=Path(tmp)/'water';c.write_text(h)
  ref.compile_c(['cc','-std=gnu11','-O0',str(c),'-o',str(exe)])
  output=subprocess.check_output([str(exe)],text=True)
 cases={}
 for line in output.splitlines():
  kind,key,*data=line.split(',');key=int(key)
  if kind=='K':cases[key]={'kind':data[0],'config':list(map(int,data[1:])),'states':[],'events':[],'rng':[],'allocations':[],'rendered':[]}
  else:
   field={'S':'states','E':'events','R':'rng','A':'allocations','Z':'end','Q':'rendered'}[kind]
   values=list(map(int,data))
   if field=='end':cases[key][field]=values
   else:cases[key][field].append(values)
 validate(cases)
 # Preserve full canonical callback/player trajectories. Other seeds/geometries
 # retain every allocation/death and first/last child update; selected complete
 # rendered schedules carry every live post-pass frame without duplicate events.
 for case in cases.values():
  kind=case['kind']
  if kind=='ring' and case['config'][:2]!=[0,0]:
   case['states']=[s for s in case['states'] if s[4]!=1 or
     (s[2]==1 and s[1] in [-1,0,14,29,39]) or (s[2]!=1 and s[5] in [1,19])]
  elif kind in ['schedule','pair']:
   kinds={a[0]:a[2] for a in case['allocations']}
   case['states']=[s for s in case['states'] if s[4]!=1 or s[5]==1 or
     (kinds[s[2]]==2 and s[5] in [16,31,41])]
  del case['events']
  if kind=='ring' and case['config'][1]!=0:case['rendered']=[]
  if kind=='schedule' and case['config'][2]!=0:case['rendered']=[]
 return {'sourceCommit':ref.COMMIT,
 'coordinates':[[32,88],[200,40],[90,96],[152,32]],'battlerSubpriorities':[30,40,20,50],'geometries':GEOMETRIES,'seeds':SEEDS,'floatArgs':FLOAT_ARGS,'signedAccumulatorProbes':PROBES,
 'stateColumns':STATE_COLUMNS,'statePhases':['allocated/initialized, before first raw callback','after frame/affine player','before raw slot reset, after matrix release'],
 'renderedColumns':RENDERED_COLUMNS,
 'caseConfigColumns':{'float':['floatArgsIndex','unused','unused','unused'],'ring':['geometryIndex','seedIndex','unused','unused'],'schedule':['geometryIndex','seedIndex','reservationLayout:0=none/1=two lower/2=one lower','unused'],'pair':['geometryIndex','seedIndex','syntheticLayout3','unused'],'probe':['signedAccumulatorProbeIndex','unused','unused','unused']},
 'allocationColumns':['serial','slot','kind:1=float/2=ring/3=raw child','parentSerial','tick','x','y','allocationSubpriority'],
 'rngColumns':RNG_COLUMNS,'endColumns':['nextTickAfterLastPass','allocations','rngDraws','rngState','matrixBitmap'],
 'notes':['Tick -1 script creation invokes initializer and AnimateSprite immediately; normal ticks start at0.',
 'Initial script creation is setup at tick-1. Subsequent script callbacks run before AnimateSprites at ticks1 onward. Extracted delay5 creates later parents at7,14; their normal callbacks run in those same passes.',
 'Synthetic natural-reuse pair creates at -1 and before the normal pass at20. At tick49 the second ring emits child slot0 (deferred) and child slot4 (same-pass), after the first parent frees slot0 at tick40.',
 'All data words are signed16; child offsets use arithmetic >>7; ring division and float /10 truncate toward zero.',
 'Reciprocal matrixA=65536/source scaleX at zero rotation; Y values equal X; BIOS rotation beyond0 is not covered.',
 'All raw children use frame0 tile8 and affine0. Float args6 is ignored by source.',
 'Rendered snapshots are observed after each normal pass; scheduled script creation precedes that pass; tick-1 captures initial script creation. remainingLifetime counts future callbacks, including the final destroying callback. Empty tick means no live sampled sprites.',
 'Snapshot phases never advance animation or consume RNG. MatrixWrites counts actual completed source affine updates; every active affine update writes even after END.',
 'Parent destruction resets its slot and the trailing source data0++ changes only inactive reset data0 to1.',
 'Resource lookups, coordinate adapters and declared slot reservations are harness inputs, not reconstructed global battler slot identities.'],
 'cases':cases}

def signed16(x):return ((x+32768)&65535)-32768

def validate(cases):
 assert len(cases)==37,len(cases)
 for case in cases.values():
  states,events,allocs=case['states'],case['events'],case['allocations']
  kind=case['kind'];rng=case['rng'];start=0 if kind in ['float','probe'] else SEEDS[case['config'][1]]
  for i,row in enumerate(rng):
   _,_,_,draw,before,result,after=row
   assert draw==i and before==start and after==(before*1103515245+24691)&0xffffffff and result==after>>16
   start=after
  serial_meta={a[0]:a for a in allocs}
  for serial,meta in serial_meta.items():
   rows=[r for r in states if r[2]==serial];frames=[r for r in rows if r[4]==1];dead=[r for r in rows if r[4]==2]
   assert len(dead)==1,(kind,serial)
   typ=meta[2]
   if typ==1:
    args=FLOAT_ARGS[case['config'][0]];assert len(frames)==args[5]
    for row in frames:
     t=row[1];steps=t+1
     assert row[6:8]==args[:2] and row[10:14]==[args[2],args[3],args[4],args[5]-steps]
     assert row[9]==int((-steps*args[2])/10) and row[14]==-steps*args[2] and row[15]==steps*args[3]&255
     assert row[16]==8 and row[18]==int(t>=0)
    assert dead[0][1]==args[5]-1
   elif typ==2:
    assert len(frames)==41 and dead[0][5]==42
    assert frames[0][1]==meta[4] and frames[0][21:23]==[261,251]
    for row in frames:
     assert row[22]==65536//row[21] and row[25]==row[5] and row[19]==0
     steps=row[5]-1;dx=row[11]-row[6];dy=row[12]-row[7]
     assert row[10]==steps and row[15]==steps%15
     assert row[8:10]==([0,0] if steps==0 else [int((steps-1)*dx/40),int((steps-1)*dy/40)])
     count=row[5];scale=256+5*min(count,10)
     for stage in range(1,7):scale+=(-10 if stage%2 else 10)*min(max(count-stage*10,0),10)
     assert row[21]==scale
    first_normal=0 if meta[4]==-1 else meta[4]
    assert dead[0][1]==first_normal+40 and dead[0][8:10]==[dead[0][11]-dead[0][6],dead[0][12]-dead[0][7]]
    parent_rng=[r for r in rng if r[2]==serial]
    assert len(parent_rng)==4 and [r[1]-first_normal for r in parent_rng]==[14,14,29,29]
    children=[a for a in allocs if a[3]==serial]
    assert len(children)==4
    for burst in range(2):
     left,right=children[burst*2:burst*2+2];birth=left[4]
     parent=next(r for r in frames if r[1]==birth)
     dy=parent[12]-parent[7];dx=parent[11]-parent[6]
     vy=dy+parent_rng[burst*2][5]%10-5;vx=-dx+parent_rng[burst*2+1][5]%10-5
     for child,sign in [(left,1),(right,-1)]:
      initial=next(r for r in states if r[2]==child[0] and r[4]==0)
      separation=(parent[10]-1)//2
      assert initial[6:8]==[parent[6]+parent[8],parent[7]+parent[9]+sign*separation]
      assert initial[10:15]==[20,vy,abs(vx)*sign,0,0]
      assert initial[26]==[30,40,20,50][GEOMETRIES[case['config'][0]][0]]-1
      assert parent_rng[burst*2+1][0]<initial[0]
      if kind=='ring':assert initial[28]==burst*2+2
   else:
    lifetime=PROBES[case['config'][0]][0] if kind=='probe' else 20
    assert len(frames)==lifetime-1 and dead[0][5]==lifetime,(kind,serial,len(frames),dead)
    initial=next(r for r in rows if r[4]==0)
    assert initial[17:21]==[1,0,1,0] and initial[21:23]==[256,256]
    d1,d2=initial[11:13];d3,d4=initial[13:15]
    for row in frames:
     updates=row[5];x=signed16(d3+updates*d1);y=signed16(d4+updates*d2)
     assert row[8:10]==[x>>7,y>>7] and row[13:15]==[x,y]
     scale=256-10*min(updates-1,15)
     assert row[21:23]==[scale,65536//scale] and row[20]==int(updates>=17)
     assert row[18]==int(updates>=2) and row[16]==8 and row[25]==updates
    first_frame=frames[0][1]
    if kind!='probe':
     parent_slot=serial_meta[meta[3]][1]
     assert first_frame==meta[4]+int(meta[1]<parent_slot),(kind,meta,first_frame)
    assert dead[0][1]==first_frame+lifetime-1
    final_x=signed16(d3+lifetime*d1);final_y=signed16(d4+lifetime*d2)
    assert dead[0][8:10]==[final_x>>7,final_y>>7] and dead[0][13:15]==[final_x,final_y]
    assert dead[0][10]==0 and dead[0][25]==lifetime-1 and dead[0][22]==256
    assert not any(e[2]==2 and e[3]==serial and e[1]==dead[0][1] for e in events)
  if kind=='ring':assert len(allocs)==5 and len(rng)==4
  if kind=='schedule':
   assert len(allocs)==15 and len(rng)==12
   assert [a[4] for a in allocs if a[2]==2]==[-1,7,14]
  if kind=='pair':assert len(allocs)==10 and len(rng)==8
  assert case['end'][-1]==0
  # Snapshot recording is a separate observation, cross-checked against complete
  # callback/frame states before fixture sampling or event removal.
  live={};state_groups={};render_groups={}
  for row in states:state_groups.setdefault(row[1],[]).append(row)
  for row in case['rendered']:render_groups.setdefault(row[0],[]).append(row)
  for tick in range(-1,case['end'][0]):
   for row in sorted(state_groups.get(tick,[])):
    if row[4]==2:live.pop(row[3],None)
    else:live[row[3]]=row
   draws=sum(r[1]<=tick for r in rng)
   expected=[]
   for slot,row in sorted(live.items()):
    typ=serial_meta[row[2]][2]
    remaining=row[13] if typ==1 else row[13]-row[10]+1 if typ==2 else row[10]
    expected.append([tick,slot,typ,*row[6:10],row[16],row[22],row[26],remaining,draws])
   assert render_groups.get(tick,[])==expected,(kind,tick,render_groups.get(tick,[]),expected)
  if kind=='pair':
   assert next(r for r in case['rendered'] if r[0]==49 and r[1]==0)==[49,0,3,153,68,0,0,8,256,29,20,8]
   assert next(r for r in case['rendered'] if r[0]==49 and r[1]==4)==[49,4,3,153,40,-1,-2,8,256,29,19,8]

if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');parser.add_argument('--helper',type=Path,default=ROOT/'tests/Reference/reference_c.py');parser.add_argument('--extractor',type=Path,default=ROOT/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py');parser.add_argument('--output',type=Path,default=Path(__file__).with_name('water-pulse-source-vectors.json'));args=parser.parse_args()
 result=build(args.reference,args.helper,args.extractor);args.output.write_text(json.dumps(result,separators=(',',':'))+'\n');print(str(args.output)+': '+str(len(result['cases']))+' cases, '+str(args.output.stat().st_size)+' bytes')

#!/usr/bin/env python3
"""Extract pinned Meteor Mash / raw Mini Twinkling Star C bodies and scheduler.

No upstream scripts or managed compiler run. All callbacks, slot allocation,
live traversal, frame interpreter, Random and script delays are extracted at
regeneration. Instrumentation wraps exact bodies; affine-only dependencies abort
because both source templates are affine-off. Resource/coordinate adapters and
explicit reservations are harness inputs, not inferred battler slot identities.
"""
import argparse, importlib.util, json, re, subprocess, sys, tempfile
from pathlib import Path
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[3]
SEEDS=[0,1,0xffffffff,0x12345678,0x80000000,0x7fffffff]
COORDS=[[32,88],[200,40],[90,96],[152,32]]
ARGS=[[-48,-64,72,32,30],[-112,-64,8,32,30],[-80,-64,40,32,30]]
PROBES=[[32760,-32760,-32768,32767,32767,-32768,30],[-32768,32767,32767,-32768,-32768,32767,30],[1,-1,-129,128,32760,-32760,2],[32767,-32768,-1,1,1,-1,1]]
STATE_COLUMNS=['event','tick','serial','slot','phase','callbacks','x','y','x2','y2','data0','data1','data2','data3','data4','data5','tile','frameBeginning','frameEnded','affineBeginning','affineEnded','invisible','subpriority','visualCount','drawCount']
RENDER_COLUMNS=['tick','slot','serial','kind','x','y','x2','y2','tile','invisible','remainingCallbacks','drawCount']

def load(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def build(reference,helper,extractor,keep_c=None):
 ref=load('meteor_reference',helper);ref.verify_reference(reference)
 ext=load('meteor_extract',extractor);function=ext.function
 src={n:(reference/('src/'+n+'.c')).read_text() for n in ['sprite','battle_anim_effects_3','battle_anim','random']}
 header=(reference/'include/sprite.h').read_text()
 script_source=(reference/'data/battle_anim_scripts.s').read_text().split('Move_METEOR_MASH:',1)[1].split('Move_REVENGE:',1)[0]
 script_args=[list(map(int,m.split(','))) for m in re.findall(r'createsprite gMeteorMashStarSpriteTemplate, ANIM_TARGET, 3, ([^\n]+)',script_source)]
 script_delays=list(map(int,re.findall(r'^\s*delay (\d+)$',script_source,re.M)))
 assert script_args==ARGS and script_delays[:2]==[10,40]
 script_bytes=[]
 for index,args in enumerate(script_args):
  script_bytes += [0,0,0,0,0,128|3,len(args)]
  for word in args:script_bytes += [word&255,(word>>8)&255]
  if index<2:script_bytes += [1,script_delays[index]]
 script_bytes += [2]
 ordinary=['AnimateSprites','CreateSpriteAndAnimate','ResetSprite','BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','DecrementAnimDelayCounter','SetSpriteSheetFrameTileNum','BeginAffineAnim','ContinueAffineAnim','FreeSpriteOamMatrix']
 wrapped=['CreateSprite','CreateSpriteAt','AnimateSprite','DestroySprite']
 callbacks=['AnimMeteorMashStar','AnimMeteorMashStar_Step','AnimMiniTwinklingStar','AnimMiniTwinklingStar_Step']
 funcs=[function(src['sprite'],n) for n in ordinary+wrapped]+[function(src['battle_anim_effects_3'],n) for n in callbacks]+[function(src['battle_anim'],n) for n in ['DestroyAnimSprite','Cmd_createsprite','Cmd_delay','WaitAnimFrameCount','RunAnimScriptCommand']]
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
#define ST_OAM_AFFINE_ON_MASK 1
#define ST_OAM_AFFINE_OFF 0
#define TAG_NONE 65535
#define TILE_SIZE_4BPP 32
#define FREE_SPRITE_TILE(i) abort()
#define B_SIDE_PLAYER 0
#define ANIMSPRITE_IS_TARGET 128
#define T1_READ_16(ptr) ((ptr)[0] | ((ptr)[1]<<8))
#define T2_READ_32(ptr) ResolveTemplate(ptr)
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define sAnchorX data[6]
#define sAnchorY data[7]
'''
 h+=header[header.index('struct AnimFrameCmd\n'):header.index('\nenum\n')]
 h+='\n'+'\n'.join(re.findall(r'^#define (?:RAND_MULT|ISO_RANDOMIZE1)\b.*$',(reference/'include/random.h').read_text(),re.M))+'\n'
 h+=r'''
struct Sprite;typedef void (*SpriteCallback)(struct Sprite*);
struct OamData{u8 affineMode,shape,size,matrixNum,paletteNum;u16 tileNum;};
struct SpriteFrameImage{const void*data;u16 size;};
struct SpriteTemplate{u16 tileTag,paletteTag;const struct OamData*oam;const union AnimCmd*const*anims;const struct SpriteFrameImage*images;const union AffineAnimCmd*const*affineAnims;SpriteCallback callback;};
struct Sprite{
 struct OamData oam;const union AnimCmd*const*anims;const struct SpriteFrameImage*images;const union AffineAnimCmd*const*affineAnims;const struct SpriteTemplate*template;SpriteCallback callback;
 s16 x,y,x2,y2,data[8];u8 animNum,animCmdIndex,animLoopCounter,subpriority;u8 animDelayCounter:6;
 bool8 inUse,invisible,animEnded,animBeginning,animPaused,usingSheet,hFlip,vFlip,affineAnimBeginning,affineAnimEnded,affineAnimPaused,anchored;u16 sheetTileStart;
};
static struct Sprite gSprites[MAX_SPRITES],sDummySprite;
static struct AffineAnimState sAffineAnimStates[32];static bool8 gAffineAnimsDisabled;
static u32 gRngValue;static u8 gBattleAnimTarget,gBattleAnimAttacker,gAnimVisualTaskCount;static s16 gBattleAnimArgs[8];
static int tick,caseId,eventId,nextSerial,currentParent,drawCount,failures,peakRaw,peakAnimated,peakTotal,framePeakRaw,framePeakAnimated,framePeakTotal;
static int serials[64],kinds[64],parents[64],callbackCounts[64];
static s16 sAnimFramesToWait;static bool8 gAnimScriptActive;static const u8*sBattleAnimScriptPtr;static void(*gAnimScriptCallback)(void);
static const u8 coords[4][2]={{32,88},{200,40},{90,96},{152,32}};
extern const struct SpriteTemplate gMeteorMashStarSpriteTemplate,gMiniTwinklingStarSpriteTemplate;
static void HarnessEnd(void);static void State(struct Sprite*,int);static void Counts(void);
static uintptr_t ResolveTemplate(const u8*p){assert(p[0]==0&&p[1]==0&&p[2]==0&&p[3]==0);return (uintptr_t)&gMeteorMashStarSpriteTemplate;}
static u8 GetBattlerSpriteSubpriority(u8 b){static const u8 sub[]={30,40,20,50};assert(b<4);return sub[b];}
static u8 GetBattlerSide(u8 b){assert(b<4);return b&1;}
static u8 GetBattlerSpriteCoord(u8 b,u8 c){assert(b<4&&(c==2||c==3));return coords[b][c&1];}
static void CalcCenterToCornerVec(struct Sprite*s,u8 a,u8 b,u8 c){(void)s;(void)a;(void)b;assert(c==0);}
static s16 AllocSpriteTiles(u8 n){abort();}
static u16 GetSpriteTileStartByTag(u16 n){assert(n==1);return 0;}
static u8 IndexOfSpritePaletteTag(u16 n){assert(n==1);return 0;}
static void SetSpriteOamFlipBits(struct Sprite*s,u8 h,u8 v){s->hFlip=h;s->vFlip=v;}
static void RequestSpriteFrameImageCopy(u16 a,u16 b,const struct SpriteFrameImage*c){abort();}
static void InitSpriteAffineAnim(struct Sprite*s){abort();}
static void FreeOamMatrix(u8 n){abort();}
static u8 GetSpriteMatrixNum(struct Sprite*s){abort();}
static void AffineAnimStateRestartAnim(u8 n){abort();}
static void GetAffineAnimFrame(u8 n,struct Sprite*s,struct AffineAnimFrameCmd*f){abort();}
static void ApplyAffineAnimFrame(u8 n,struct AffineAnimFrameCmd*f){abort();}
static void UpdateSpriteMatrixAnchorPos(struct Sprite*s,s32 a,s32 b){abort();}
static void AffineAnimDelay(u8 n,struct Sprite*s){abort();}
static void UnsupportedAffine(u8 n,struct Sprite*s){abort();}
static void Dummy(struct Sprite*s){(void)s;}
'''
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'
 h+=r'''
static void(*sAnimFuncs[])(struct Sprite*)={ContinueAnim,BeginAnim};
static void(*sAnimCmdFuncs[])(struct Sprite*)={AnimCmd_loop,AnimCmd_jump,AnimCmd_end,AnimCmd_frame};
static void(*sAffineAnimFuncs[])(struct Sprite*)={ContinueAffineAnim,BeginAffineAnim};
static void(*sAffineAnimCmdFuncs[])(u8,struct Sprite*)={UnsupportedAffine,UnsupportedAffine,UnsupportedAffine,UnsupportedAffine};
static void(*sScriptCmdTable[])(void)={Cmd_createsprite,Cmd_delay,HarnessEnd};
'''
 h+=function(src['random'],'Random')+r'''
static u16 ObservedRandom(void){u32 before=gRngValue;u16 result=Random();printf("R,%d,%d,%d,%d,%d,%"PRIu32",%u,%"PRIu32"\n",caseId,eventId++,tick,currentParent,drawCount++,before,result,gRngValue);return result;}
#define Random ObservedRandom
'''
 for f in funcs:
  name=re.search(r'(\w+)\([^;]+?\)\n\{',f).group(1)
  if name in wrapped+callbacks:h+='#define '+name+' Source'+name+'\n'+f+'\n#undef '+name+'\n'
  else:h+=f+'\n'
 h+='#undef Random\n'+r'''
static const union AnimCmd dummyAnim=ANIMCMD_END;static const union AffineAnimCmd dummyAffine=AFFINEANIMCMD_END;
static const union AnimCmd*const dummyAnims[]={&dummyAnim};static const union AffineAnimCmd*const dummyAffines[]={&dummyAffine};
static const struct OamData parentOam={0,0,1},rawOam={0,0,0};
static const struct SpriteTemplate gMeteorMashStarSpriteTemplate={1,1,&parentOam,dummyAnims,NULL,dummyAffines,AnimMeteorMashStar};
static const struct SpriteTemplate gMiniTwinklingStarSpriteTemplate={1,1,&rawOam,dummyAnims,NULL,dummyAffines,AnimMiniTwinklingStar};
static void Counts(void){int raw=0,animated=0,total=0;for(int i=0;i<64;i++)if(gSprites[i].inUse){total++;if(serials[i]){animated++;raw+=kinds[i]==2;}}
 if(raw>peakRaw)peakRaw=raw;if(animated>peakAnimated)peakAnimated=animated;if(total>peakTotal)peakTotal=total;
 if(raw>framePeakRaw)framePeakRaw=raw;if(animated>framePeakAnimated)framePeakAnimated=animated;if(total>framePeakTotal)framePeakTotal=total;
}
static void State(struct Sprite*s,int phase){int i=(int)(s-gSprites);int row[]={eventId++,tick,serials[i],i,phase,callbackCounts[i],s->x,s->y,s->x2,s->y2,s->data[0],s->data[1],s->data[2],s->data[3],s->data[4],s->data[5],s->oam.tileNum,s->animBeginning,s->animEnded,s->affineAnimBeginning,s->affineAnimEnded,s->invisible,s->subpriority,gAnimVisualTaskCount,drawCount};printf("S,%d",caseId);for(unsigned j=0;j<sizeof(row)/sizeof(row[0]);j++)printf(",%d",row[j]);puts("");}
u8 CreateSpriteAt(u8 i,const struct SpriteTemplate*t,s16 x,s16 y,u8 subpriority){u8 result=SourceCreateSpriteAt(i,t,x,y,subpriority);assert(result==i);serials[i]=++nextSerial;kinds[i]=t==&gMeteorMashStarSpriteTemplate?1:2;parents[i]=currentParent;callbackCounts[i]=0;printf("A,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",caseId,serials[i],i,kinds[i],parents[i],tick,x,y,subpriority);State(&gSprites[i],0);Counts();return result;}
u8 CreateSprite(const struct SpriteTemplate*t,s16 x,s16 y,u8 subpriority){u32 rng=gRngValue;int draws=drawCount;u8 result=SourceCreateSprite(t,x,y,subpriority);if(result==64){failures++;assert(gRngValue==rng&&drawCount==draws);printf("F,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",caseId,eventId++,tick,currentParent,x,y,subpriority,drawCount,result);}return result;}
void AnimateSprite(struct Sprite*s){SourceAnimateSprite(s);if(serials[s-gSprites])State(s,1);Counts();}
void DestroySprite(struct Sprite*s){if(serials[s-gSprites])State(s,2);SourceDestroySprite(s);Counts();}
#define WRAP(name) static void name(struct Sprite*s){int i=(int)(s-gSprites);callbackCounts[i]++;int p=currentParent;currentParent=serials[i];Source##name(s);currentParent=p;Counts();}
WRAP(AnimMeteorMashStar)
WRAP(AnimMeteorMashStar_Step)
WRAP(AnimMiniTwinklingStar)
WRAP(AnimMiniTwinklingStar_Step)
static int Active(void){int n=0;for(int i=0;i<64;i++)n+=gSprites[i].inUse&&serials[i]!=0;return n;}
static void Reset(u32 seed){memset(&sDummySprite,0,sizeof(sDummySprite));sDummySprite.callback=Dummy;sDummySprite.x=304;sDummySprite.y=160;sDummySprite.subpriority=255;sDummySprite.anims=dummyAnims;sDummySprite.affineAnims=dummyAffines;sDummySprite.usingSheet=1;for(int i=0;i<64;i++)gSprites[i]=sDummySprite;memset(serials,0,sizeof(serials));memset(kinds,0,sizeof(kinds));memset(parents,0,sizeof(parents));memset(callbackCounts,0,sizeof(callbackCounts));memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));gRngValue=seed;eventId=nextSerial=currentParent=drawCount=failures=peakRaw=peakAnimated=peakTotal=framePeakRaw=framePeakAnimated=framePeakTotal=gAnimVisualTaskCount=0;tick=-1;gAnimScriptActive=0;}
static void Reserve(int count,int fragmented){for(int j=0;j<count;j++){int i=fragmented?(j*3)%64:j;assert(!gSprites[i].inUse);gSprites[i].inUse=1;gSprites[i].invisible=1;gSprites[i].animPaused=1;gSprites[i].affineAnimPaused=1;}Counts();}
static void BeginTick(void){framePeakRaw=framePeakAnimated=framePeakTotal=0;Counts();}
static void Snapshot(void){u32 rng=gRngValue;int draws=drawCount;struct Sprite copy[64];memcpy(copy,gSprites,sizeof(copy));int raw=0,parentsCount=0,total=0;for(int i=0;i<64;i++)if(gSprites[i].inUse){total++;if(serials[i]){struct Sprite*s=&gSprites[i];raw+=kinds[i]==2;parentsCount+=kinds[i]==1;int remaining=kinds[i]==1?s->data[4]-s->data[5]+1:62-callbackCounts[i];printf("Q,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",caseId,tick,i,serials[i],kinds[i],s->x,s->y,s->x2,s->y2,s->oam.tileNum,s->invisible,remaining,drawCount);}}
 printf("P,%d,%d,%d,%d,%d,%d,%d,%d\n",caseId,tick,parentsCount,raw,total,framePeakAnimated,framePeakRaw,framePeakTotal);assert(gRngValue==rng&&drawCount==draws&&!memcmp(copy,gSprites,sizeof(copy)));}
static void HarnessEnd(void){sBattleAnimScriptPtr++;gAnimScriptActive=0;}
'''
 h+='static const u8 script[]={'+','.join(map(str,script_bytes))+'};\n'
 h+=r'''
static int parentIndex;
static void CreateParent(void){sBattleAnimScriptPtr=script;Cmd_createsprite();}
static void StartScript(void){sBattleAnimScriptPtr=script;sAnimFramesToWait=0;gAnimScriptActive=1;gAnimScriptCallback=RunAnimScriptCommand;gAnimScriptCallback();}
static void End(void){printf("Z,%d,%d,%d,%d,%"PRIu32",%d,%d,%d,%d,%d\n",caseId,tick,nextSerial,drawCount,gRngValue,failures,peakAnimated,peakRaw,peakTotal,gAnimVisualTaskCount);assert(!Active()&&gAnimVisualTaskCount==0);u16 r1=Random(),r2=Random();printf("T,%d,%u,%u,%"PRIu32"\n",caseId,r1,r2,gRngValue);caseId++;}
'''
 h+='static const u32 seeds[]={'+','.join(str(x)+'u' for x in SEEDS)+'};\n'
 h+='static const s16 probes[][7]={'+','.join('{'+','.join(map(str,x))+'}' for x in PROBES)+'};\n'
 h+=r'''
int main(void){
 for(int geometry=0;geometry<4;geometry++)for(int seed=0;seed<6;seed++){
  if(geometry!=1&&seed!=0)continue;
  Reset(seeds[seed]);gBattleAnimTarget=geometry;printf("K,%d,single,%d,%d,0\n",caseId,geometry,seed);parentIndex=0;CreateParent();Snapshot();
  for(tick=0;Active();tick++){assert(tick<100);BeginTick();AnimateSprites();Snapshot();}End();
 }
 for(int layout=0;layout<4;layout++){
  Reset(0x12345678);gBattleAnimTarget=1;printf("K,%d,schedule,1,3,%d\n",caseId,layout);Reserve(layout==1?9:layout>=2?18:0,layout==3);StartScript();Snapshot();
  for(tick=0;Active()||gAnimScriptActive;tick++){assert(tick<160);BeginTick();if(tick>0&&gAnimScriptActive)gAnimScriptCallback();AnimateSprites();Snapshot();}End();
 }
 for(int probe=0;probe<4;probe++){
  Reset(seeds[probe]);gBattleAnimTarget=probe;printf("K,%d,boundary,%d,%d,%d\n",caseId,probe,probe,probe);memcpy(gBattleAnimArgs,probes[probe]+2,sizeof(s16)*5);u8 i=CreateSpriteAndAnimate(&gMeteorMashStarSpriteTemplate,probes[probe][0],probes[probe][1],3);assert(i<64);gAnimVisualTaskCount++;Snapshot();
  for(tick=0;Active();tick++){assert(tick<100);BeginTick();AnimateSprites();Snapshot();}End();
 }
 // Reserve lower slots for creation, then release them before the live pass:
 // raw slot0 is deferred because parent slot2 was already reached.
 Reset(1);printf("K,%d,reuse,1,1,0\n",caseId);gBattleAnimTarget=1;Reserve(2,0);parentIndex=0;CreateParent();Snapshot();SourceDestroySprite(&gSprites[0]);SourceDestroySprite(&gSprites[1]);
 for(tick=0;Active();tick++){assert(tick<100);BeginTick();AnimateSprites();Snapshot();}End();
 // Source allocation exhaustion: every attempted child returns MAX_SPRITES,
 // consumes no RNG, and never receives an initializer.
 Reset(0x12345678);printf("K,%d,exhaustion,1,3,0\n",caseId);gBattleAnimTarget=1;Reserve(63,0);parentIndex=0;CreateParent();Snapshot();
 for(tick=0;Active();tick++){assert(tick<40);BeginTick();AnimateSprites();Snapshot();}End();
 // A failed raw allocation can be retried after a free slot. The successful raw
 // child is observable with tile0 and consumes exactly two draws next pass.
 Reset(0xffffffff);printf("K,%d,retry,1,2,0\n",caseId);Reserve(64,0);assert(CreateSprite(&gMiniTwinklingStarSpriteTemplate,10,20,5)==64);SourceDestroySprite(&gSprites[17]);assert(CreateSprite(&gMiniTwinklingStarSpriteTemplate,10,20,5)==17);Snapshot();
 for(tick=0;Active();tick++){assert(tick<70);BeginTick();AnimateSprites();Snapshot();}End();
}
'''
 # Use the complete source templates, with only known tag/table/OAM symbols
 # adapted to harness resource declarations.
 for name in ['gMeteorMashStarSpriteTemplate','gMiniTwinklingStarSpriteTemplate']:
  template=re.search(r'^const struct SpriteTemplate '+name+r'\s*=\s*\{.*?\};',src['battle_anim_effects_3'],re.M|re.S)[0]
  h=re.sub(r'^static const struct SpriteTemplate '+name+r'=.*?;$',lambda _:template,h,flags=re.M)
 h=h.replace('static const struct OamData parentOam=', '#define ANIM_TAG_GOLD_STARS 1\n#define gOamData_AffineOff_ObjNormal_16x16 parentOam\n#define gOamData_AffineOff_ObjNormal_8x8 rawOam\n#define gDummySpriteAnimTable dummyAnims\n#define gDummySpriteAffineAnimTable dummyAffines\nstatic const struct OamData parentOam=')
 if keep_c:keep_c.write_text(h)
 with tempfile.TemporaryDirectory() as tmp:
  c=Path(tmp)/'meteor.c';exe=Path(tmp)/'meteor';c.write_text(h);ref.compile_c(['cc','-std=gnu11','-O0',str(c),'-o',str(exe)]);out=subprocess.check_output([str(exe)],text=True)
 cases={}
 for line in out.splitlines():
  kind,key,*data=line.split(',');key=int(key)
  if kind=='K':cases[key]={'kind':data[0],'config':list(map(int,data[1:])),'states':[],'rng':[],'allocations':[],'rendered':[],'occupancy':[],'failures':[]}
  else:
   field={'S':'states','A':'allocations','R':'rng','Q':'rendered','P':'occupancy','F':'failures','Z':'end','T':'suffix'}[kind];values=list(map(int,data))
   if field in ['end','suffix']:cases[key][field]=values
   else:cases[key][field].append(values)
 validate(cases)
 for case in cases.values():
  canonical=case['kind']=='single' and case['config'][:2]==[1,0]
  schedule=case['kind']=='schedule' and case['config'][2]==0
  types={a[0]:a[2] for a in case['allocations']}
  first_raw=next((a[0] for a in case['allocations'] if a[2]==2),0)
  case['states']=[r for r in case['states'] if
   r[4]==2 or (r[4]==0 and types[r[2]]==1) or
   (r[4]==1 and (r[5]==1 or (canonical and (types[r[2]]==1 or r[2]==first_raw)) or
    (case['kind']=='boundary' and types[r[2]]==1) or
    (case['kind'] in ['reuse','retry'] and r[2]==first_raw and r[5] in [2,30,31,32,33,61])))]
  if not schedule:
   # Explicit pre-Begin tile0 and same-pass lower-slot deferral snapshots.
   case['rendered']=[r for r in case['rendered'] if case['kind'] in ['reuse','retry'] and r[0] in [-1,0,1]]
  if case['kind']!='schedule':
   case['occupancy']=[r for r in case['occupancy'] if r[0] in [-1,0,29,30,31,60,61] or r[0]==case['end'][0]-1]
 return {'sourceCommit':ref.COMMIT,'coordinates':COORDS,'battlerSubpriorities':[30,40,20,50],'seeds':SEEDS,'parentArgs':ARGS,'boundaryProbes':PROBES,
 'stateColumns':STATE_COLUMNS,'statePhases':['allocation before callback/BeginAnim','after callback and frame player','before DestroySprite reset'],
 'allocationColumns':['serial','slot','kind:1=parent/2=raw','parentSerial','tick','x','y','subpriority'],
 'rngColumns':['event','tick','serial','drawIndex','before','result','after'],'renderedColumns':RENDER_COLUMNS,
 'occupancyColumns':['tick','postPassParents','postPassRaw','postPassTotal','microstepAnimatedPeak','microstepRawPeak','microstepTotalPeak'],
 'failureColumns':['event','tick','parentSerial','x','y','subpriority','drawCount','returnSlot'],
 'suffixColumns':['nextRandom','secondRandom','stateAfterTwoExplicitPostCleanupDraws'],
 'endColumns':['nextTickAfterLastPass','allocations','rngDraws','rngState','failedAllocations','peakAnimated','peakRaw','peakTotal','visualCount'],
 'caseConfigColumns':['targetPosition','seedIndex','layoutOrProbeIndex'],
 'notes':['Setup creation is tick -1; normal callbacks start at0. Extracted delays10/40 produce parent creation -1,12,54.',
 'Single canonical target1 seed0 preserves every parent and first raw child callback/frame state. Every case preserves every raw initialization, death, RNG draw and allocation. Schedule layout0 preserves all read-only render snapshots; all four schedule layouts preserve per-frame occupancy. Other states are sampled only after complete source validation.',
 'Schedule reservations: layout0 none, layout1 slots0..8, layout2 slots0..17, layout3 slots(j*3)%64 for j0..17. Reserved sprites are inert/invisible and do not claim original global battler identities.',
 'Parent emits on data5 even0..duration, including the terminal callback before its own destruction. Raw children initialize at their first live-slot callback, with two RNG draws; higher slots initialize in the allocation pass, lower slots next pass.',
 'Mini child initializer chooses tile4 if first Random&3 is0, otherwise tile5; second Random&7 gives y2, negating4..7. Source dummy-END BeginAnim preserves those manual tiles and leaves beginning true/ended false.',
 'Mini children take61 Step callbacks after initialization and die at data0=61. Invisible sprites still occupy slots and receive callbacks. Render snapshots are read-only and retain invisible state for verification.',
 'Suffix values are two explicit source Random calls after cleanup and after the end state is recorded; they are not render observations or child callbacks. Reservation layouts must have identical suffixes.',
 'Both templates are affine-off. Affine allocation/update/free dependencies abort if reached; no matrices are allocated.',
 'Signed16 coordinates/data preserve conversion wraps; interpolation uses promoted signed differences and C truncation toward zero. Target sprite dimensions are unused.',
 'Spinning fist, impact, battler and healthbox occupancy are separate; supplied reservations model their bounded external occupancy.'], 'cases':cases}

def s16(value):return ((value+32768)&65535)-32768
def truncdiv(a,b):return (abs(a)//abs(b))*(-1 if (a<0)!=(b<0) else 1)

def validate(cases):
 assert len(cases)==20,len(cases)
 for case in cases.values():
  allocs=case['allocations'];states=case['states'];rng=case['rng'];kind=case['kind'];seed=SEEDS[case['config'][1]]
  for draw,r in enumerate(rng):
   event,tick,serial,index,before,result,after=r
   assert index==draw and before==seed and after==(before*1103515245+24691)&0xffffffff and result==after>>16
   seed=after
  for a in allocs:
   serial,slot,typ,parent,birth,x,y,sub=a;rows=[r for r in states if r[2]==serial];frames=[r for r in rows if r[4]==1];dead=[r for r in rows if r[4]==2]
   assert len(dead)==1,(kind,a,dead)
   assert rows[0][4]==0 and rows[0][16:21]==[0,1,0,1,0]
   assert all(r[17:21]==[1,0,1,0] for r in frames)
   if typ==2:
    assert len(frames)==61 and dead[0][5]==62 and dead[0][10]==61,(kind,a,len(frames),dead)
    rr=[r for r in rng if r[2]==serial];assert len(rr)==2 and rr[0][1]==frames[0][1] and rr[1][1]==frames[0][1]
    tile=4 if rr[0][5]&3==0 else 5;dy=rr[1][5]&7;dy=-dy if dy>3 else dy
    assert all(r[16]==tile and r[9]==dy for r in frames)
    assert rows[0][0]<rr[0][0]<rr[1][0]<frames[0][0] and rows[0][-1]<=rr[0][3]
    assert sub==5
    assert dead[0][1]==frames[0][1]+61
    if parent:
     parentSlot=next(a[1] for a in allocs if a[0]==parent)
     assert frames[0][1]==birth+int(slot<parentSlot),(kind,a,frames[0])
    d1=0;invisible=0
    for r in frames:
     steps=r[5]-1
     if steps:
      if steps<30:
       d1+=1
       if d1==2:invisible^=1;d1=0
      else:
       if d1==2:invisible=0
       if d1==3:invisible=1;d1=-1
       d1+=1
     assert r[10:12]==[steps,d1] and r[21]==invisible,(kind,a,r)
   else:
    duration=frames[0][14];assert len(frames)==duration+1 and dead[0][5]==duration+2
    children=[a for a in allocs if a[3]==serial];fails=[r for r in case['failures'] if r[2]==serial]
    assert len(children)+len(fails)==duration//2+1
    assert dead[0][15]==duration
    target=case['config'][0]
    if kind=='boundary':px,py,*args=PROBES[case['config'][2]]
    else:
     px,py=COORDS[target]
     args=ARGS[[a[0] for a in allocs if a[2]==1].index(serial)] if kind=='schedule' else ARGS[0]
     assert sub==max(3,[30,40,20,50][target]-3)
    sign=-1 if target%2==0 else 1
    x0,y0,x1,y1=s16(px+sign*args[0]),s16(py+args[1]),s16(px+sign*args[2]),s16(py+args[3])
    assert frames[0][6:8]==[x0,y0] and frames[0][10:15]==[x0,y0,x1,y1,duration]
    for r in frames[1:]+dead:
     step=r[5]-2
     assert r[6:10]==[x0,y0,s16(truncdiv((x1-x0)*step,duration)),s16(truncdiv((y1-y0)*step,duration))]
    first_normal=max(0,birth)
    assert dead[0][1]==first_normal+duration
    for child in children:
     step=child[4]-first_normal
     assert step%2==0 and 0<=step<=duration
     assert child[5:7]==[s16(x0+s16(truncdiv((x1-x0)*step,duration))),s16(y0+s16(truncdiv((y1-y0)*step,duration)))]
     if step==duration:assert next(r[0] for r in states if r[2]==child[0] and r[4]==0)<dead[0][0]
  if kind=='schedule':assert [a[4] for a in allocs if a[2]==1]==[-1,12,54] and len(allocs)==51 and len(rng)==96 and not case['failures']
  if kind=='exhaustion':assert len(allocs)==1 and len(case['failures'])==16 and not rng
  assert case['end'][2]==len(rng) and case['end'][3]==seed and case['end'][-1]==0
  suffix1=(seed*1103515245+24691)&0xffffffff;suffix2=(suffix1*1103515245+24691)&0xffffffff
  assert case['suffix']==[suffix1>>16,suffix2>>16,suffix2]
  assert case['end'][5:8]==[max(r[4] for r in case['occupancy']),max(r[5] for r in case['occupancy']),max(r[6] for r in case['occupancy'])]
  if kind=='retry':assert len(case['failures'])==1 and len(rng)==2
  # Reconstruct each read-only post-pass snapshot from ordered source states.
  # This proves snapshots neither advance the player nor invent raw lifetimes.
  live={};state_groups={};render_groups={};types={a[0]:a[2] for a in allocs}
  for r in states:state_groups.setdefault(r[1],[]).append(r)
  for r in case['rendered']:render_groups.setdefault(r[0],[]).append(r)
  for tick in range(-1,case['end'][0]):
   for r in sorted(state_groups.get(tick,[])):
    if r[4]==2:live.pop(r[3],None)
    else:live[r[3]]=r
   draws=sum(r[1]<=tick for r in rng);expected=[]
   for slot,r in sorted(live.items()):
    typ=types[r[2]];remaining=r[14]-r[15]+1 if typ==1 else 62-r[5]
    expected.append([tick,slot,r[2],typ,*r[6:10],r[16],r[21],remaining,draws])
   assert render_groups.get(tick,[])==expected,(kind,tick,render_groups.get(tick,[]),expected)
   occupancy=next(r for r in case['occupancy'] if r[0]==tick)
   assert occupancy[1:3]==[sum(types[r[2]]==1 for r in live.values()),sum(types[r[2]]==2 for r in live.values())]
  if kind=='schedule':
   assert case['end'][5:8]==[37,36,[37,46,55,55][case['config'][2]]]
   assert case['end'][0]==146
  if kind=='schedule':assert case['suffix']==next(c['suffix'] for c in cases.values() if c['kind']=='schedule')
  if kind=='reuse':
   lower=next(r for r in case['rendered'] if r[0]==0 and r[1]==0)
   assert lower[8]==0 and lower[-1]==0
   assert next(r for r in case['rendered'] if r[0]==1 and r[1]==0)[8] in [4,5]

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--helper',type=Path,default=ROOT/'tests/Reference/reference_c.py');p.add_argument('--extractor',type=Path,default=ROOT/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py');p.add_argument('--output',type=Path,default=Path(__file__).with_name('meteor-mash-source-vectors.json'));p.add_argument('--keep-c',type=Path);a=p.parse_args();result=build(a.reference,a.helper,a.extractor,a.keep_c);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');print(str(a.output)+': '+str(len(result['cases']))+' cases, '+str(a.output.stat().st_size)+' bytes')
 for case in result['cases'].values():
  if case['kind'] in ['schedule','exhaustion','retry']:print(case['kind'],case['config'],'end',case['end'],'peak frames',[(r[0],r[4:]) for r in case['occupancy'] if r[4]==case['end'][5]])

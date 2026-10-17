#!/usr/bin/env python3
"""Extract Amnesia and Meditate callbacks and their shared source players.

Only authored host adapters are supplied; upstream build scripts are not run.
The sprite BIOS adapter records scale/rotation; shared matrix tests cover geometry.
"""
import argparse,importlib.util,json,os,re,subprocess,tempfile
from pathlib import Path

def load(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def build(repo,reference):
 ref=load('self_stat_reference',repo/'tests/Reference/reference_c.py');ref.verify_reference(reference)
 extract=load('self_stat_extract',repo/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py')
 src={n:(reference/f'src/{n}.c').read_text() for n in ['sprite','battle_anim_mons','battle_anim_psychic']};sp=src['sprite'];mons=src['battle_anim_mons'];psychic=src['battle_anim_psychic'];header=(reference/'include/sprite.h').read_text()
 funcs=[extract.function(sp,n) for n in ['AnimateSprites','AnimateSprite','BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','DecrementAnimDelayCounter','BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame','AffineAnimCmd_loop','BeginAffineAnimLoop','ContinueAffineAnimLoop','JumpToTopOfAffineAnimLoop','AffineAnimStateRestartAnim','AffineAnimStateReset','ApplyAffineAnimFrameAbsolute','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame','ApplyAffineAnimFrame','InitSpriteAffineAnim','AllocOamMatrix','FreeOamMatrix']]
 funcs += [extract.function(psychic,n) for n in ['AnimQuestionMark','AnimQuestionMark_Step1','AnimQuestionMark_Step2']]+[extract.function(mons,'RunStoredCallbackWhenAnimEnds')]
 h=r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8;typedef uint16_t u16;typedef uint32_t u32;typedef int8_t s8;typedef int16_t s16;typedef int32_t s32;typedef u8 bool8;
#define TRUE 1
#define FALSE 0
#define MAX_SPRITES 1
#define OAM_MATRIX_COUNT 32
#define ST_OAM_AFFINE_ON_MASK 1
#define ST_OAM_AFFINE_OFF 0
#define ST_OAM_AFFINE_NORMAL 1
#define B_SIDE_OPPONENT 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define BATTLER_COORD_ATTR_WIDTH 0
#define BATTLER_COORD_ATTR_HEIGHT 1
#define sAnchorX data[6]
#define sAnchorY data[7]
#define SAFE_DIV(a,b) (assert((b)!=0),(a)/(b))
'''
 h+=header[header.index('struct AnimFrameCmd\n'):header.index('\nenum\n')]+r'''
struct Sprite;typedef void(*SpriteCallback)(struct Sprite*);
struct Sprite{
 u8 animCmdIndex,animLoopCounter,animNum;u8 animDelayCounter:6;
 bool8 inUse,animEnded,animBeginning,animPaused,usingSheet,hFlip,vFlip;
 bool8 affineAnimBeginning,affineAnimEnded,affineAnimPaused,anchored;
 int sheetTileStart,images;s16 x,y,x2,y2,data[8];
 struct {u8 affineMode,matrixNum,shape,size;int tileNum;} oam;
 const union AnimCmd *const *anims;const union AffineAnimCmd *const *affineAnims;SpriteCallback callback,stored;
};
struct ObjAffineSrcData{s16 xScale,yScale;u16 rotation;};struct OamMatrix{s16 a,b,c,d;};
static struct Sprite gSprites[MAX_SPRITES];static struct AffineAnimState sAffineAnimStates[32];static bool8 gAffineAnimsDisabled;static u32 gOamMatrixAllocBitmap;
static int actor,width,height,centerX,centerY,active,writes;static u8 gBattleAnimAttacker;
static void SetSpriteOamFlipBits(struct Sprite*s,u8 h,u8 v){s->hFlip=h;s->vFlip=v;}
static void RequestSpriteFrameImageCopy(int a,int b,int c){abort();}
static void StoreSpriteCallbackInData6(struct Sprite*s,SpriteCallback cb){s->stored=cb;}
static void SetCallbackToStoredInData6(struct Sprite*s){s->callback=s->stored;}
static void DestroyAnimSprite(struct Sprite*s){assert(gOamMatrixAllocBitmap==0);s->inUse=FALSE;active=0;}
static u8 GetSpriteMatrixNum(struct Sprite*s){return s->oam.matrixNum;}
static void UpdateSpriteMatrixAnchorPos(struct Sprite*s,s32 x,s32 y){abort();}
static void CopyOamMatrix(u8 n,struct OamMatrix*m){assert(n==0);}
static void SetOamMatrix(u8 n,s16 a,s16 b,s16 c,s16 d){assert(n==0&&a==256&&b==0&&c==0&&d==256);}
static void CalcCenterToCornerVec(struct Sprite*s,u8 shape,u8 size,u8 affine){assert(affine==1);}
static void ObjAffineSet(const struct ObjAffineSrcData*s,struct OamMatrix*d,int n,int stride){assert(n==1&&stride==2);memset(d,0,sizeof(*d));writes++;}
static s16 GetBattlerSpriteCoordAttr(u8 b,u8 type){return type==0?width:height;}
static u8 GetBattlerSpriteCoord(u8 b,u8 type){return type==2?centerX:centerY;}
static u8 GetBattlerSide(u8 b){return actor&1;}
'''
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'
 h+='static void(*sAnimFuncs[])(struct Sprite*)={ContinueAnim,BeginAnim};\nstatic void(*sAnimCmdFuncs[])(struct Sprite*)={AnimCmd_loop,AnimCmd_jump,AnimCmd_end,AnimCmd_frame};\nstatic void(*sAffineAnimFuncs[])(struct Sprite*)={ContinueAffineAnim,BeginAffineAnim};\nstatic void(*sAffineAnimCmdFuncs[])(u8,struct Sprite*)={AffineAnimCmd_loop,AffineAnimCmd_jump,AffineAnimCmd_end,AffineAnimCmd_frame};\n'
 h+=extract.declaration(psychic,'sAnim_QuestionMark')+'\n'+extract.declaration(psychic,'sAffineAnim_QuestionMark')+'\nstatic const union AnimCmd *const sAnims_QuestionMark[]={sAnim_QuestionMark};\nstatic const union AffineAnimCmd *const sAffineAnims_QuestionMark[]={sAffineAnim_QuestionMark};\n'+'\n'.join(funcs)
 h+=r'''
static int callback(struct Sprite*s){return s->callback==RunStoredCallbackWhenAnimEnds?0:s->callback==AnimQuestionMark_Step1?1:s->callback==AnimQuestionMark_Step2?2:-1;}
static void row(int kind,int key,int tick){struct Sprite*s=&gSprites[0];struct AffineAnimState*a=&sAffineAnimStates[0];printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",kind,key,tick,s->inUse,s->x,s->y,s->oam.tileNum,s->animEnded,callback(s),s->data[0],s->data[1],s->oam.affineMode,s->affineAnimEnded,a->animCmdIndex,a->delayCounter,a->xScale,a->yScale,a->rotation,(int)gOamMatrixAllocBitmap,writes,s->animCmdIndex,s->animDelayCounter);}
int main(void){
 const int coords[4][2]={{32,96},{200,48},{90,104},{152,40}};
 const int dims[5][2]={{64,64},{1,1},{63,63},{16,8},{64,128}};
 for(actor=0;actor<4;actor++)for(int dim=0;dim<5;dim++){
  width=dims[dim][0];height=dims[dim][1];centerX=coords[actor][0];centerY=coords[actor][1];struct Sprite*s=&gSprites[0];memset(s,0,sizeof(*s));memset(sAffineAnimStates,0,sizeof(sAffineAnimStates));gOamMatrixAllocBitmap=0;
  s->inUse=TRUE;s->animBeginning=TRUE;s->usingSheet=TRUE;s->anims=sAnims_QuestionMark;s->callback=AnimQuestionMark;active=1;
  for(int tick=-1;tick<150;tick++){writes=0;AnimateSprites();row(0,actor*5+dim,tick);if(!active)break;if(tick==149)abort();}
 }
 const int delays[]={18,0,-1,-32768,32767};
 for(int p=0;p<5;p++){
  struct Sprite*s=&gSprites[0];memset(s,0,sizeof(*s));gOamMatrixAllocBitmap=0;s->inUse=TRUE;s->callback=AnimQuestionMark_Step2;s->data[0]=1;s->data[1]=delays[p];active=1;
  for(int tick=0;tick<2&&active;tick++){writes=0;AnimQuestionMark_Step2(s);row(1,p,tick);}
 }
}
'''
 task_funcs=[extract.function(mons,n) for n in ['PrepareAffineAnimInTaskData','RunAffineAnimFromTaskData','SetBattlerSpriteYOffsetFromYScale']]+[extract.function(psychic,n) for n in ['AnimTask_MeditateStretchAttacker','AnimTask_MeditateStretchAttacker_Step']]
 t=r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
typedef uint8_t u8;typedef uint16_t u16;typedef int16_t s16;typedef uint32_t u32;typedef int32_t s32;typedef u8 bool8;
#define TRUE 1
#define FALSE 0
#define ST_OAM_OBJ_NORMAL 0
#define MON_PIC_HEIGHT 64
#define ANIM_ATTACKER 0
#define SAFE_DIV(a,b) ((b)?(a)/(b):0)
#define AFFINEANIMCMDTYPE_LOOP 0x7ffd
#define AFFINEANIMCMDTYPE_JUMP 0x7ffe
#define AFFINEANIMCMDTYPE_END 0x7fff
#define AFFINEANIMCMD_FRAME(x,y,r,d) {.frame={x,y,r,d}}
#define AFFINEANIMCMD_END {.type=AFFINEANIMCMDTYPE_END}
union AffineAnimCmd{s16 type;struct{s16 xScale,yScale;u8 rotation,duration;}frame;struct{s16 type,count;}loop;struct{s16 type;u16 target;}jump;uint32_t storage[2];};
struct Task{s16 data[16];void(*func)(u8);};struct Sprite{s16 y2;struct{int matrixNum;}oam;};
static struct Task gTasks[1];static struct Sprite gSprites[1];static struct{int d;}gOamMatrices[1];static union AffineAnimCmd*sAnimTaskAffineAnim;static const void*pointer;static int active,applied,yDelta,resets;
static void StorePointerInVars(s16*lo,s16*hi,const void*p){pointer=p;*lo=*hi=0;}static void*LoadPointerFromVars(s16 lo,s16 hi){return (void*)pointer;}
static void PrepareBattlerSpriteForRotScale(u8 sprite,u8 mode){active=1;}
static void SetSpriteRotScale(u8 sprite,s16 x,s16 y,u16 r){assert(r==0);gOamMatrices[0].d=y;applied=1;}
static void ResetSpriteRotScale(u8 sprite){gOamMatrices[0].d=256;resets++;}
static int GetBattlerYDeltaFromSpriteId(u8 sprite){return yDelta;}static u8 GetAnimBattlerSpriteId(int battler){return 0;}
static void DestroyAnimVisualTask(u8 task){active=0;}
'''
 t+='\n'.join(f.split('\n{',1)[0]+';' for f in task_funcs)+'\n'+extract.declaration(psychic,'sAffineAnim_MeditateStretchAttacker')+'\n'+'\n'.join(task_funcs)+r'''
int main(void){for(yDelta=0;yDelta<=32;yDelta+=8){memset(gTasks,0,sizeof(gTasks));memset(gSprites,0,sizeof(gSprites));resets=0;AnimTask_MeditateStretchAttacker(0);
 for(int tick=0;tick<64&&active;tick++){applied=0;gTasks[0].func(0);s16*d=gTasks[0].data;printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",yDelta,tick,active,d[7],d[8],d[9],d[10],d[11],d[12],gSprites[0].y2,applied,resets);}
 assert(!active&&resets==1);}}
'''
 def run(code,name):
  with tempfile.TemporaryDirectory(prefix='self-stat-source-') as folder:
   c=Path(folder)/(name+'.c');c.write_text(code);exe=Path(folder)/name
   flags=['-fsanitize=undefined','-fno-sanitize-recover=all'] if os.name!='nt' else []
   ref.compile_c(['cc','-std=gnu11','-O0',*flags,str(c),'-o',str(exe)])
   return subprocess.check_output([str(exe)],text=True,env={**os.environ,'UBSAN_OPTIONS':'halt_on_error=1:print_stacktrace=1'})
 question={};probes={};meditate={}
 for line in run(h,'question').splitlines():
  kind,key,*values=map(int,line.split(','));(question if kind==0 else probes).setdefault(str(key),[]).append(values)
 for line in run(t,'meditate').splitlines():
  key,*values=map(int,line.split(','));meditate.setdefault(str(key),[]).append(values)
 for rows in question.values():
  assert rows[-1][1]==0 and rows[-1][16]==0
  assert next(r[0] for r in rows if r[5])==53
  assert next(r[0] for r in rows if r[9])==55
  assert next(r[0] for r in rows if r[8]==18)==104
  assert rows[-1][0]==123
 for rows in meditate.values():assert len(rows)==41 and rows[-1][1]==0 and rows[-1][-1]==1
 return dict(sourceCommit=ref.COMMIT,questionColumns=['tick','active','x','y','tile','frameEnded','callback','state','delay','affineMode','affineEnded','affineCommand','affineDelay','scaleX','scaleY','rotation','matrixBitmap','matrixWrites','frameCommand','frameDelay'],question=question,probeDelays=[18,0,-1,-32768,32767],probes=probes,meditateColumns=['tick','active','command','frameTicks','loopRemaining','scaleX','scaleY','rotation','yOffset','applied','resets'],meditate=meditate,notes=['Question mark initialization at tick -1 uses exact callback and shared AnimateSprite order. Dynamic InitSpriteAffineAnim and LOOP use source bodies. Source geometry inputs cover four positions, odd dimensions and top clamping.','Object matrix allocation/free are source functions. BIOS matrix writing is stubbed; native scale and rotation are observed and product shared matrix tests own geometry.','Task pointer storage uses one host pointer instead of source halfwords; task program, foot offset and destroy callbacks are exact source.','No upstream scripts or gameplay RNG are called.'],sanitizer='undefined; no-recover' if os.name!='nt' else 'not enabled')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--repo',type=Path,default=Path.cwd());p.add_argument('--reference',type=Path);p.add_argument('--output',type=Path,required=True);a=p.parse_args();data=build(a.repo,a.reference or a.repo/'content/raw/pokefirered/037335f4');a.output.write_text(json.dumps(data,separators=(',',':'))+'\n');print('Wrote',len(data['question']),'question variants,',len(data['probes']),'delay probes,',len(data['meditate']),'task variants')

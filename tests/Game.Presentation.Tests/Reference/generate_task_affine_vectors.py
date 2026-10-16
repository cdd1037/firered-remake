#!/usr/bin/env python3
"""Explicitly extract pinned C callbacks into a bounded harness; ordinary tests read the vectors."""
import argparse, importlib.util, json, re, subprocess, tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
spec=importlib.util.spec_from_file_location('reference_c',ROOT/'tests/Reference/reference_c.py');ref=importlib.util.module_from_spec(spec);spec.loader.exec_module(ref)

def function(source,name):
 match=re.search(r'^(?:static )?(?:void|bool8) '+name+r'\([^;]+?\)\n\{',source,re.M)
 if not match:raise ValueError('Missing callback '+name)
 end=match.end();depth=1
 while depth:
  depth+=(source[end]=='{')-(source[end]=='}');end+=1
 return source[match.start():end]

def build(reference):
 mons=(reference/'src/battle_anim_mons.c').read_text();effects=(reference/'src/battle_anim_effects_2.c').read_text();ground=(reference/'src/battle_anim_ground.c').read_text();effects3=(reference/'src/battle_anim_effects_3.c').read_text()
 commands=re.search(r'static const union AffineAnimCmd sSplashEffectAffineAnimCmds\[\] =\s*\{.*?\};',effects,re.S)[0]
 harness=r'''
#include <stdio.h>
#include <stdint.h>
#include <string.h>
typedef uint8_t u8;typedef uint16_t u16;typedef int16_t s16;typedef int32_t s32;typedef u8 bool8;
#define TRUE 1
#define FALSE 0
#define ST_OAM_OBJ_NORMAL 0
#define MON_PIC_HEIGHT 64
#define SAFE_DIV(a,b) ((b)?(a)/(b):0)
#define AFFINEANIMCMDTYPE_LOOP 0x7ffd
#define AFFINEANIMCMDTYPE_JUMP 0x7ffe
#define AFFINEANIMCMDTYPE_END 0x7fff
#define AFFINEANIMCMD_FRAME(x,y,r,d) {.frame={x,y,r,d}}
#define AFFINEANIMCMD_JUMP(n) {.jump={AFFINEANIMCMDTYPE_JUMP,n}}
#define AFFINEANIMCMD_LOOP(n) {.loop={AFFINEANIMCMDTYPE_LOOP,n}}
#define AFFINEANIMCMD_END {.type=AFFINEANIMCMDTYPE_END}
#define B_SIDE_PLAYER 0
#define ANIM_ATTACKER 0
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
// The original compiler stores each affine union at its 8-byte task stride.
union AffineAnimCmd {s16 type;struct {s16 xScale,yScale;u8 rotation,duration;} frame;struct {s16 type,count;} loop;struct {s16 type;u16 target;} jump;uint32_t storage[2];};
struct Task {s16 data[16];void (*func)(u8);};
struct Sprite {s16 x,y,x2,y2;int invisible,animNum;struct {int matrixNum,tileNum;} oam;s16 data[8];void (*callback)(struct Sprite*);};
static struct Task gTasks[1];static struct Sprite gSprites[1];static struct {int d;} gOamMatrices[1];
static union AffineAnimCmd *sAnimTaskAffineAnim;static void *pointer;static s16 gBattleAnimArgs[8];static int gBattleAnimAttacker;
static int active,applied,yDelta,battleSide,centerX=72,centerY=96;
static void (*storedCallback)(struct Sprite*);
static void StorePointerInVars(s16 *lo,s16 *hi,const void *p){pointer=(void*)p;*lo=*hi=0;}
static void *LoadPointerFromVars(s16 lo,s16 hi){return pointer;}
static void PrepareBattlerSpriteForRotScale(u8 sprite,u8 mode){active=1;}
// Only Splash records y2; its rotations are all zero. Other cases compare task data, not BIOS matrices.
static void SetSpriteRotScale(u8 sprite,s16 x,s16 y,u16 r){gOamMatrices[0].d=y;applied=1;}
static void ResetSpriteRotScale(u8 sprite){gOamMatrices[0].d=256;active=0;}
static int GetBattlerYDeltaFromSpriteId(u8 sprite){return yDelta;}
static u8 GetAnimBattlerSpriteId(int battler){return 0;}
static void DestroyAnimVisualTask(u8 id){active=0;}
static int GetBattlerSpriteCoord(int battler,int type){return type==BATTLER_COORD_X_2?centerX:centerY;}
static void DestroyAnimSprite(struct Sprite *sprite){active=0;}
static int GetBattlerSide(int battler){return battleSide;}
static void StartSpriteAnim(struct Sprite *sprite,int number){sprite->animNum=number;}
static void StoreSpriteCallbackInData6(struct Sprite *sprite,void (*callback)(struct Sprite*)){storedCallback=callback;}
static void SetCallbackToStoredInData6(struct Sprite *sprite){sprite->callback=storedCallback;}
static void SetBattlerSpriteYOffsetFromYScale(u8 sprite);
static void AnimTask_Splash_Step(u8 taskId);
static void AnimTask_GrowAndShrink_Step(u8);
static void AnimTask_SlackOffSquish_Step(u8);
static void AnimMudSportDirtRising(struct Sprite*);
static void AnimMudSportDirtFalling(struct Sprite*);
'''
 harness+='\n'+commands+'\n'
 for source,name in [(effects,'sGrowAndShrinkAffineAnimCmds'),(effects3,'sSlackOffSquishAffineAnimCmds')]:harness+=re.search(r'static const union AffineAnimCmd '+name+r'\[\] =\s*\{.*?\};',source,re.S)[0]+'\n'
 harness+='\n'+re.search(r'const union AffineAnimCmd DefenseCurlDeformMonAffineAnimCmds\[\] =\s*\{.*?\};',effects3,re.S)[0]+'\n'
 for source,names in [(mons,['PrepareAffineAnimInTaskData','RunAffineAnimFromTaskData','SetBattlerSpriteYOffsetFromYScale','TranslateSpriteLinearFixedPoint']),(effects,['AnimTask_Splash','AnimTask_Splash_Step','AnimTask_GrowAndShrink','AnimTask_GrowAndShrink_Step','AnimBreathPuff']),(effects3,['AnimTask_DefenseCurlDeformMon','AnimTask_SlackOffSquish','AnimTask_SlackOffSquish_Step']),(ground,['AnimMudSportDirt','AnimMudSportDirtRising','AnimMudSportDirtFalling'])]:
  for name in names:harness+=function(source,name)+'\n'
 harness+=r'''
static const union AffineAnimCmd absolute[]={AFFINEANIMCMD_FRAME(512,128,255,0),AFFINEANIMCMD_FRAME(-1,2,255,2),AFFINEANIMCMD_END};
static const union AffineAnimCmd consecutive[]={AFFINEANIMCMD_FRAME(400,120,1,0),AFFINEANIMCMD_FRAME(200,100,2,0),AFFINEANIMCMD_FRAME(1,-1,3,1),AFFINEANIMCMD_END};
static const union AffineAnimCmd marked[]={AFFINEANIMCMD_FRAME(2,3,0,2),AFFINEANIMCMD_LOOP(0),AFFINEANIMCMD_FRAME(-3,4,0,2),AFFINEANIMCMD_LOOP(3),AFFINEANIMCMD_END};
static const union AffineAnimCmd unmarked[]={AFFINEANIMCMD_FRAME(1,-1,0,1),AFFINEANIMCMD_LOOP(2),AFFINEANIMCMD_END};
static const union AffineAnimCmd jumps[]={AFFINEANIMCMD_JUMP(2),AFFINEANIMCMD_FRAME(100,100,0,2),AFFINEANIMCMD_FRAME(-4,2,0,1),AFFINEANIMCMD_JUMP(4),AFFINEANIMCMD_END};
static const union AffineAnimCmd overflow[]={AFFINEANIMCMD_FRAME(32760,-32760,250,0),AFFINEANIMCMD_FRAME(20,-20,255,2),AFFINEANIMCMD_END};
static void program(const char *name,const union AffineAnimCmd *cmds){
 memset(gTasks,0,sizeof(gTasks));PrepareAffineAnimInTaskData(&gTasks[0],0,cmds);
 for(int i=0;i<100;i++){applied=0;int running=RunAffineAnimFromTaskData(&gTasks[0]);s16 *d=gTasks[0].data;
 printf("p,%s,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",name,i,running,d[7],d[8],d[9],d[10],d[11],d[12],applied);if(!running)return;} }
int main(void){
 for(int kind=0;kind<3;kind++){
  memset(gTasks,0,sizeof(gTasks));memset(gSprites,0,sizeof(gSprites));yDelta=12;gBattleAnimArgs[0]=0;
  if(kind==0)AnimTask_GrowAndShrink(0);else if(kind==1)AnimTask_SlackOffSquish(0);else {gTasks[0].func=AnimTask_DefenseCurlDeformMon;AnimTask_DefenseCurlDeformMon(0);}
  for(int i=0;i<100&&active;i++){gTasks[0].func(0);s16 *d=gTasks[0].data;printf("d,%d,%d,%d,%d,%d,%d,%d\n",kind,i,d[10],d[11],gSprites[0].x2,gSprites[0].y2,active);}
 }
 for(int side=0;side<2;side++){
  memset(gSprites,0,sizeof(gSprites));battleSide=side;active=1;AnimBreathPuff(&gSprites[0]);
  for(int i=0;i<100&&active;i++){gSprites[0].callback(&gSprites[0]);struct Sprite *s=&gSprites[0];printf("b,%d,%d,%d,%d,%d,%d,%d\n",side,i,s->x,s->y,s->x2,s->animNum,active);}
 }
 program("absolute",absolute);program("consecutive",consecutive);program("marked",marked);program("unmarked",unmarked);program("jumps",jumps);program("overflow",overflow);
 for(int height=0;height<=12;height+=12){
  memset(gTasks,0,sizeof(gTasks));memset(gSprites,0,sizeof(gSprites));yDelta=height;gBattleAnimArgs[0]=0;gBattleAnimArgs[1]=3;AnimTask_Splash(0);
  for(int i=0;i<150&&active;i++){gTasks[0].func(0);s16 *d=gTasks[0].data;
   printf("s,%d,%d,%d,%d,%d,%d,%d\n",height,i,d[10],d[11],gSprites[0].y2,active,d[2]);}
 }
 for(int mode=0;mode<2;mode++){
  memset(gSprites,0,sizeof(gSprites));gBattleAnimArgs[0]=mode;gBattleAnimArgs[1]=mode?120:-4;gBattleAnimArgs[2]=mode?60:-16;active=1;AnimMudSportDirt(&gSprites[0]);
  for(int i=0;i<100&&active;i++){gSprites[0].callback(&gSprites[0]);struct Sprite *s=&gSprites[0];printf("m,%d,%d,%d,%d,%d,%d,%d\n",mode,i,s->x,s->y,s->y2,s->invisible,active);}
 }
}
'''
 with tempfile.TemporaryDirectory() as tmp:
  c=Path(tmp)/'task-affine.c';c.write_text(harness);exe=Path(tmp)/'task-affine';ref.compile_c(['cc','-std=gnu11','-O0',str(c),'-o',str(exe)])
  result=subprocess.check_output([str(exe)],text=True)
 programs={};splashes={};mud={};deform={};breath={}
 for row in result.splitlines():
  kind,key,*numbers=row.split(',');values=list(map(int,numbers))
  dict(p=programs,s=splashes,m=mud,d=deform,b=breath)[kind].setdefault(key,[]).append(values)
 return dict(programs=programs,splashes=splashes,mud=mud,deform=deform,breath=breath)
if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
 args.output.write_text(json.dumps(build(args.reference),separators=(',',':'))+'\n')

#!/usr/bin/env python3
"""Compile local pinned original C Web callbacks and sprite affine player.

Tick -1 runs creation callback, then original AnimateSprite. No immediate
translation step. Coordinate/side/doubles and GPU-register adapters are explicit;
all position, lifetime, visibility, alpha and affine math executes original C.
Mode 2 hides the partner logically but never rewrites its source coordinates.
"""
import argparse,importlib.util,json,re,subprocess,tempfile
from pathlib import Path
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def build(root):
 ref=module('reference_c',root/'tests/Reference/reference_c.py');reference=root/'content/raw/pokefirered/037335f4';ref.verify_reference(reference)
 extract=module('extract',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py');function=extract.function
 sprite=(reference/'src/sprite.c').read_text();mons=(reference/'src/battle_anim_mons.c').read_text();bug=(reference/'src/battle_anim_bug.c').read_text();header=(reference/'include/sprite.h').read_text();trig=(reference/'src/trig.c').read_text()
 names=['AnimateSprites','AnimateSprite','BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','DecrementAnimDelayCounter','BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame','AffineAnimStateRestartAnim','AffineAnimStateReset','ApplyAffineAnimFrameAbsolute','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame','ApplyAffineAnimFrame']
 funcs=[function(sprite,n) for n in names]+[function(bug,n) for n in ['AnimTranslateWebThread','AnimTranslateWebThread_Step','AnimStringWrap','AnimStringWrap_Step','AnimSpiderWeb','AnimSpiderWeb_Step','AnimSpiderWeb_End']]+[function(mons,n) for n in ['SetAnimSpriteInitialXOffset','InitSpritePosToAnimAttacker','SetAverageBattlerPositions','InitAnimLinearTranslationWithSpeed','InitAnimLinearTranslation','AnimTranslateLinear']]+[function(trig,'Sin')]
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
#define ST_OAM_AFFINE_ON_MASK 1
#define sAnchorX data[6]
#define sAnchorY data[7]
#define SAFE_DIV(a,b) (assert((b)!=0),(a)/(b))
'''
 h+=header[header.index('struct AnimFrameCmd\n'):header.index('\nenum\n')]+r'''
struct Sprite;
typedef void (*SpriteCallback)(struct Sprite *);
struct Sprite {
 u8 animCmdIndex,animLoopCounter,animNum;u8 animDelayCounter:6;
 bool8 invisible,inUse,animEnded,animBeginning,animPaused,usingSheet,hFlip,vFlip;
 bool8 affineAnimBeginning,affineAnimEnded,affineAnimPaused,anchored;
 int sheetTileStart,images; s16 x,y,x2,y2,data[8];
 struct {u8 affineMode;int tileNum;} oam;
 const union AnimCmd *const *anims;const union AffineAnimCmd *const *affineAnims;
 SpriteCallback callback,storedCallback;
};
struct ObjAffineSrcData {s16 xScale,yScale;u16 rotation;};
struct OamMatrix {s16 a,b,c,d;};
static struct Sprite gSprites[MAX_SPRITES];static struct AffineAnimState sAffineAnimStates[1];static bool8 gAffineAnimsDisabled;
static int writes;
static void SetSpriteOamFlipBits(struct Sprite*s,u8 h,u8 v){s->hFlip=h;s->vFlip=v;}
static void RequestSpriteFrameImageCopy(int a,int b,int c){abort();}
static void StoreSpriteCallbackInData6(struct Sprite*s,SpriteCallback cb){s->storedCallback=cb;s->data[6]=0x1234;s->data[7]=0x5678;}
static void SetCallbackToStoredInData6(struct Sprite*s){s->callback=s->storedCallback;}
static void DestroyAnimSprite(struct Sprite*s){s->inUse=FALSE;}
static u8 GetSpriteMatrixNum(struct Sprite*s){return 0;}
static void UpdateSpriteMatrixAnchorPos(struct Sprite*s,s32 x,s32 y){abort();}
static void CopyOamMatrix(u8 n,struct OamMatrix*m){assert(n==0);}
static void ObjAffineSet(const struct ObjAffineSrcData*s,struct OamMatrix*d,int n,int stride){assert(n==1&&stride==2);memset(d,0,sizeof(*d));writes++;}
static void UnsupportedAffineLoop(u8 n,struct Sprite*s){abort();}
'''
 h+=r"""
#define B_SIDE_PLAYER 0
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define REG_OFFSET_BLDCNT 0
#define REG_OFFSET_BLDALPHA 1
#define BLDCNT_TGT2_ALL 0x3f00
#define BLDCNT_EFFECT_BLEND 0x40
#define BLDALPHA_BLEND(a,b) ((a)|((b)<<8))
#define BATTLE_PARTNER(n) ((n)^2)
#define sTransl_Speed data[0]
#define sTransl_Duration data[0]
#define sTransl_InitX data[1]
#define sTransl_DestX data[2]
#define sTransl_InitY data[3]
#define sTransl_DestY data[4]
#define Q_8_8(x) ((s16)((x)*256))
s16 gBattleAnimArgs[8];u8 gBattleAnimAttacker,gBattleAnimTarget;
static int coords[4][4]={{32,80,32,96},{200,40,200,48},{90,88,90,104},{152,32,152,40}};
static int singleCoords[2][4]={{72,80,72,96},{176,40,176,48}};
static int mode,hiddenMask,bldcnt,alpha;
static int IsContest(void){return 0;}static int IsDoubleBattle(void){return mode!=0;}
static u8 GetBattlerSide(u8 n){return n&1;}
static int GetBattlerSpriteCoord(u8 n,u8 c){return mode?coords[n][c]:singleCoords[n][c];}
static int GetBattlerSpriteCoord2(u8 n,u8 c){return mode?coords[n][c]:singleCoords[n][c];}
static void SetGpuReg(int reg,int value){if(reg==REG_OFFSET_BLDCNT)bldcnt=value;else{assert(reg==REG_OFFSET_BLDALPHA);alpha=value;}}
"""
 h+=trig[trig.index('const s16 gSineTable[]'):trig.index('};',trig.index('const s16 gSineTable[]'))+2]+'\n'
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'
 h+='static void (*sAnimFuncs[])(struct Sprite*)={ContinueAnim,BeginAnim};\nstatic void (*sAnimCmdFuncs[])(struct Sprite*)={AnimCmd_loop,AnimCmd_jump,AnimCmd_end,AnimCmd_frame};\nstatic void (*sAffineAnimFuncs[])(struct Sprite*)={ContinueAffineAnim,BeginAffineAnim};\nstatic void (*sAffineAnimCmdFuncs[])(u8,struct Sprite*)={UnsupportedAffineLoop,AffineAnimCmd_jump,AffineAnimCmd_end,AffineAnimCmd_frame};\n'
 h+='\n'.join(funcs)+'\n'+extract.declaration(bug,'sAffineAnim_SpiderWeb')+r"""
static const union AffineAnimCmd *const affine[]={sAffineAnim_SpiderWeb};
static const union AnimCmd dummyAnim[]={ANIMCMD_FRAME(0,1),ANIMCMD_END};
static const union AnimCmd *const dummy[]={dummyAnim};
static void run(int kind,int actor,int target,int average,int yOffset){
 struct Sprite*s=&gSprites[0];memset(s,0,sizeof(*s));AffineAnimStateReset(0);memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));
 gBattleAnimAttacker=actor;gBattleAnimTarget=target;hiddenMask=mode==2?(1<<(target^2)):0;bldcnt=alpha=0;
 s->inUse=TRUE;s->usingSheet=TRUE;s->animBeginning=TRUE;s->anims=dummy;s->affineAnimBeginning=TRUE;s->affineAnims=affine;s->oam.affineMode=kind==2?3:0;
 /* Cmd_createsprite defaults to target PIC center, independent of priority operand. */
 s->x=GetBattlerSpriteCoord(target,BATTLER_COORD_X_2);s->y=GetBattlerSpriteCoord(target,BATTLER_COORD_Y_PIC_OFFSET);
 if(kind==0){gBattleAnimArgs[0]=20;gBattleAnimArgs[2]=512;gBattleAnimArgs[3]=20;gBattleAnimArgs[4]=average;s->callback=AnimTranslateWebThread;}
 else if(kind==1){gBattleAnimArgs[1]=yOffset;s->callback=AnimStringWrap;}else{s->callback=AnimSpiderWeb;}
 for(int tick=-1;tick<256;tick++){
  writes=0;AnimateSprites();struct AffineAnimState*a=&sAffineAnimStates[0];
  int phase=s->callback==AnimSpiderWeb_End?1:0;
  printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",kind,actor,target,mode,average,yOffset,tick,s->inUse,s->x,s->y,s->x2,s->y2,s->invisible,phase,bldcnt,alpha,a->xScale,a->yScale,writes,hiddenMask);
  for(int i=0;i<8;i++)printf(",%d",s->data[i]);puts("");
  if(!s->inUse)return;
 }abort();
}
int main(void){for(mode=0;mode<3;mode++)for(int a=0;a<(mode?4:2);a++)for(int t=0;t<(mode?4:2);t++){run(0,a,t,0,0);run(0,a,t,1,0);run(1,a,t,0,10);run(1,a,t,0,-2);run(1,a,t,0,22);run(2,a,t,0,0);}}
"""
 with tempfile.TemporaryDirectory() as tmp:
  c=Path(tmp)/'web.c';exe=Path(tmp)/'web';c.write_text(h);ref.compile_c(['cc','-std=gnu11','-O0',str(c),'-o',str(exe)]);output=subprocess.check_output([str(exe)],text=True)
 cases=[];lookup={}
 for row in output.splitlines():
  values=list(map(int,row.split(',')));key=tuple(values[:6])
  if key not in lookup:
   case=dict(zip(['kind','actor','target','mode','average','yOffset'],key));case['rows']=[];lookup[key]=case;cases.append(case)
  lookup[key]['rows'].append(values[6:])
 # Structural source-contract assertions, independent of any managed runtime.
 for case in cases:
  rows=case['rows'];assert rows[0][0]==-1 and rows[0][1]==1 and rows[-1][1]==0
  if case['kind']==0:
   assert rows[0][4:6]==[0,0] and rows[0][14]>0
   assert rows[-1][0]==rows[0][14] and rows[-1][4:6]==rows[-2][4:6]
  elif case['kind']==1:
   assert rows[-1][0]==50 and all(r[6]==((r[0]+1)//3)%2 for r in rows)
  else:
   assert rows[0][10:12]==[16,16] and rows[-1][0]==52
   assert all(r[9]==16 for r in rows if r[0]<=20)
   assert rows[-2][0:2]==[51,1] and rows[-2][6:10]==[1,1,16192,4096]
   assert rows[-1][8:10]==[0,0] and rows[-1][12]==0
 # Hidden partner status must not affect the unfiltered source averaging helper.
 for case in cases:
  if case['mode']==2:
   key=tuple(case[k] if k!='mode' else 1 for k in ['kind','actor','target','mode','average','yOffset'])
   assert [r[:13]+r[14:] for r in case['rows']]==[r[:13]+r[14:] for r in lookup[key]['rows']]
 return dict(columns=['tick','active','x','y','x2','y2','invisible','endPhase','blendControl','alpha','xScale','yScale','matrixWrites','hiddenMask']+['data'+str(i) for i in range(8)],singleCoordinates=[[72,80,72,96],[176,40,176,48]],coordinates=[[32,80,32,96],[200,40,200,48],[90,88,90,104],[152,32,152,40]],cases=cases)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.write_text(json.dumps(build(a.root),separators=(',',':'))+'\n')

#!/usr/bin/env python3
"""Pinned frame player plus ArmThrust/FurySwipes callbacks; no gameplay draws."""
import argparse,importlib.util,json,re,subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
CONFIGS=[[0,t,10,-8,14,3] for t in [0,1,2,3,4,255]]+[[0,1,-128,127,d,3] for d in [0,1,2,32767]]+[[0,0,127,-128,2,i] for i in range(4)]+[[1,0,x,y,0,i] for i in range(2) for x,y in [(16,0),(-128,127),(127,-128)]]
def build(reference,helper,extractor):
 def module(name,path):
  spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
 ref=module('reference_c',helper);ref.verify_reference(reference);extract=module('fang_extract',extractor);function=extract.function;declaration=extract.declaration
 sprite=(reference/'src/sprite.c').read_text();fight=(reference/'src/battle_anim_fight.c').read_text();effects=(reference/'src/battle_anim_effects_2.c').read_text();header=(reference/'include/sprite.h').read_text()
 names=['AnimateSprites','AnimateSprite','BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','DecrementAnimDelayCounter','StartSpriteAnim']
 funcs=[function(sprite,n) for n in names]+[function(fight,n) for n in ['AnimArmThrustHit','AnimArmThrustHit_Step']]+[function(effects,'AnimFurySwipes')]
 h=r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8;typedef uint16_t u16;typedef uint32_t u32;typedef int16_t s16;typedef uint8_t bool8;
#define FALSE 0
#define TRUE 1
#define MAX_SPRITES 1
#define ST_OAM_AFFINE_ON_MASK 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define B_SIDE_PLAYER 0
'''
 h+=header[header.index('struct AnimFrameCmd\n'):header.index('struct AffineAnimFrameCmd\n')]+r'''
struct Sprite;typedef void(*SpriteCallback)(struct Sprite*);
struct Sprite {u8 animCmdIndex,animLoopCounter,animNum;u8 animDelayCounter:6;bool8 inUse,animEnded,animBeginning,animPaused,usingSheet,hFlip,vFlip,affineAnimBeginning;int sheetTileStart,images;s16 x,y,x2,y2,data[8];struct{u8 affineMode;int tileNum;}oam;const union AnimCmd *const *anims;SpriteCallback callback;};
static struct Sprite gSprites[1];static bool8 gAffineAnimsDisabled=TRUE;static u8 gBattleAnimTarget,gAnimMoveTurn;static s16 gBattleAnimArgs[8];static const int xs[]={32,200,90,152},ys[]={88,40,96,32};
static u8 GetBattlerSide(u8 b){return b&1;}static u8 GetBattlerSpriteCoord(u8 b,u8 c){return c==2?xs[b]:ys[b];}
static void DestroyAnimSprite(struct Sprite*s){s->inUse=FALSE;}
static void SetSpriteOamFlipBits(struct Sprite*s,u8 h,u8 v){s->hFlip=h;s->vFlip=v;}
static void RequestSpriteFrameImageCopy(int a,int b,int c){abort();}static void NoAffine(struct Sprite*s){abort();}
'''
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'
 h+='static void(*sAnimFuncs[])(struct Sprite*)={ContinueAnim,BeginAnim};\nstatic void(*sAnimCmdFuncs[])(struct Sprite*)={AnimCmd_loop,AnimCmd_jump,AnimCmd_end,AnimCmd_frame};\nstatic void(*sAffineAnimFuncs[])(struct Sprite*)={NoAffine,NoAffine};\n'+'\n'.join(funcs)+'\n'
 for name in ['sAnim_Fist','sAnim_FootWide','sAnim_FootTall','sAnim_HandLeft','sAnim_HandRight']:h+=declaration(fight,name)+'\n'
 for name in ['sAnim_FurySwipes','sAnim_FurySwipes_Flipped']:h+=declaration(effects,name)+'\n'
 h+='static const union AnimCmd *const hands[]={sAnim_Fist,sAnim_FootWide,sAnim_FootTall,sAnim_HandLeft,sAnim_HandRight};\nstatic const union AnimCmd *const swipes[]={sAnim_FurySwipes,sAnim_FurySwipes_Flipped};\n'
 h+='static const int configs[][6]={'+','.join('{'+','.join(map(str,c))+'}' for c in CONFIGS)+'};\n'
 h+=r'''
static void run(int target,int config){
 const int*c=configs[config];gBattleAnimTarget=target;gAnimMoveTurn=c[1];gBattleAnimArgs[0]=c[2];gBattleAnimArgs[1]=c[3];gBattleAnimArgs[2]=c[0]?c[5]:c[4];gBattleAnimArgs[3]=c[5];gBattleAnimArgs[7]=777;
 struct Sprite*s=&gSprites[0];memset(s,0,sizeof(*s));s->inUse=TRUE;s->animBeginning=TRUE;s->usingSheet=TRUE;s->x=xs[target];s->y=ys[target];s->anims=c[0]?swipes:hands;s->callback=c[0]?AnimFurySwipes:AnimArmThrustHit;
 for(int tick=-1;tick<32769;tick++){
  AnimateSprites();assert(gBattleAnimArgs[7]==777);
  if(tick<20||!s->inUse||tick>=c[4]-2)printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",target,config,tick,s->inUse,s->x,s->y,s->x2,s->y2,s->oam.tileNum,s->hFlip,s->animEnded,s->animNum,s->data[0]);
  if(!s->inUse)return;
 }abort();
}
int main(void){for(int target=0;target<4;target++)for(unsigned c=0;c<sizeof(configs)/sizeof(configs[0]);c++)run(target,c);}
'''
 with tempfile.TemporaryDirectory() as tmp:
  c=Path(tmp)/'strike.c';exe=Path(tmp)/'strike';c.write_text(h);ref.compile_c(['cc','-std=gnu11','-O0',str(c),'-o',str(exe)]);output=subprocess.check_output([str(exe)],text=True)
 cases={}
 for line in output.splitlines():
  target,config,*row=map(int,line.split(','));cases.setdefault(f'{target}:{config}',[]).append(row)
 for key,rows in cases.items():
  target,config=map(int,key.split(':'));mode,turn,x,y,duration,index=CONFIGS[config]
  if rows[-1][0]!=(16 if mode else duration) or rows[-1][1]!=0:raise ValueError('Wrong source lifecycle '+key)
  if rows[-1][0]!=16 and mode:raise ValueError('Wrong source frame lifetime')
  for row in rows[:-1]:
   if mode:
    if row[2:6]!=[[32,200,90,152][target]+x,[88,40,96,32][target]+y,0,0] or row[7]!=index:raise ValueError('Source unmirrored swipe differs')
   else:
    parity=((turn+int(target%2==0))&255)&1
    if row[4:6]!=[-x if parity else x,y] or row[9]!=index+parity:raise ValueError('Source parity differs')
 return dict(columns=['tick','active','x','y','x2','y2','tile','hFlip','frameEnded','animation','counter'],configs=CONFIGS,cases=cases)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--helper',type=Path,default=ROOT/'tests/Reference/reference_c.py');p.add_argument('--extractor',type=Path,default=ROOT/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py');p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.write_text(json.dumps(build(a.reference,a.helper,a.extractor),separators=(',',':'))+'\n')

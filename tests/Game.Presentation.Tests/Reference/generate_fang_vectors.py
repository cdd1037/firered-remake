#!/usr/bin/env python3
"""Extract pinned Fang callbacks plus the shared sprite frame/affine scheduler.

Creation is tick -1: callback then AnimateSprite, as CreateSpriteAndAnimate.
Normal ticks use extracted AnimateSprites. BIOS matrix geometry is already
covered by the shared affine tests; this oracle observes native scale/rotation,
frame tile, END flags and callback lifetime without a second frame interpreter.
"""
import argparse,importlib.util,json,re,subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
def function(source,name):
 m=re.search(r'^(?:static )?(?:void|u8|u16|s16|bool8) '+name+r'\([^;]+?\)\n\{',source,re.M)
 if not m:raise ValueError('Missing source function '+name)
 end=m.end();depth=1
 while depth:depth+=(source[end]=='{')-(source[end]=='}');end+=1
 return source[m.start():end]
def declaration(source,name):
 m=re.search(r'^(?:static )?const union (?:AffineAnimCmd|AnimCmd) '+name+r'\[\]\s*=\s*\{.*?\};',source,re.M|re.S)
 if not m:raise ValueError('Missing source declaration '+name)
 return m[0]
def build(reference,helper):
 spec=importlib.util.spec_from_file_location('reference_c',helper);ref=importlib.util.module_from_spec(spec);spec.loader.exec_module(ref);ref.verify_reference(reference)
 sprite=(reference/'src/sprite.c').read_text();mons=(reference/'src/battle_anim_mons.c').read_text();one=(reference/'src/battle_anim_effects_1.c').read_text();three=(reference/'src/battle_anim_effects_3.c').read_text();header=(reference/'include/sprite.h').read_text()
 names=['AnimateSprites','AnimateSprite','BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','DecrementAnimDelayCounter','BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame','AffineAnimStateRestartAnim','AffineAnimStateReset','ApplyAffineAnimFrameAbsolute','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame','ApplyAffineAnimFrame']
 funcs=[function(sprite,n) for n in names]+[function(three,'AnimFang'),function(one,'AnimSuperFang'),function(mons,'RunStoredCallbackWhenAnimEnds')]
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
 bool8 inUse,animEnded,animBeginning,animPaused,usingSheet,hFlip,vFlip;
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
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'
 h+='static void (*sAnimFuncs[])(struct Sprite*)={ContinueAnim,BeginAnim};\nstatic void (*sAnimCmdFuncs[])(struct Sprite*)={AnimCmd_loop,AnimCmd_jump,AnimCmd_end,AnimCmd_frame};\nstatic void (*sAffineAnimFuncs[])(struct Sprite*)={ContinueAffineAnim,BeginAffineAnim};\nstatic void (*sAffineAnimCmdFuncs[])(u8,struct Sprite*)={UnsupportedAffineLoop,AffineAnimCmd_jump,AffineAnimCmd_end,AffineAnimCmd_frame};\n'
 h+='\n'.join(funcs)+'\n'+declaration(three,'sFangAnimCmds')+'\n'+declaration(three,'sFangAffineAnimCmds')+'\n'+declaration(one,'sSuperFangAnimCmds')+r'''
static const union AnimCmd *const fang[]={sFangAnimCmds},*const super[]={sSuperFangAnimCmds};
static const union AffineAnimCmd *const affine[]={sFangAffineAnimCmds};
static void run(int mode){
 struct Sprite*s=&gSprites[0];memset(s,0,sizeof(*s));AffineAnimStateReset(0);
 s->inUse=TRUE;s->animBeginning=TRUE;s->usingSheet=TRUE;s->anims=mode?super:fang;s->callback=mode?AnimSuperFang:AnimFang;
 s->oam.affineMode=mode?0:3;s->affineAnimBeginning=TRUE;s->affineAnims=affine;
 for(int tick=-1;tick<40;tick++){
  writes=0;AnimateSprites();struct AffineAnimState*a=&sAffineAnimStates[0];
  int callback=s->callback==AnimFang?0:s->callback==AnimSuperFang?1:s->callback==RunStoredCallbackWhenAnimEnds?2:s->callback==DestroyAnimSprite?3:-1;
  printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",mode,tick,s->inUse,s->oam.tileNum,s->animEnded,callback,a->xScale,a->yScale,a->rotation,s->affineAnimEnded,writes);
  if(!s->inUse)return;
 }
 abort();
}
int main(void){run(0);run(1);}
'''
 with tempfile.TemporaryDirectory() as tmp:
  c=Path(tmp)/'fang.c';exe=Path(tmp)/'fang';c.write_text(h);ref.compile_c(['cc','-std=gnu11','-O0',str(c),'-o',str(exe)]);output=subprocess.check_output([str(exe)],text=True)
 result={}
 for row in output.splitlines():
  mode,*values=map(int,row.split(','));result.setdefault('fang' if mode==0 else 'super',[]).append(values)
 # Assert the native END handoff independently of the fixture's consumer.
 for mode,rows in result.items():
  end=next(r[0] for r in rows if r[3]);dead=rows[-1][0]
  if dead-end!=(1 if mode=='fang' else 2):raise ValueError('Source callback handoff differs')
  if rows[-1][1]!=0 or rows[-1][-1]!=0:raise ValueError('Destroyed sprite must not animate')
 return dict(columns=['tick','active','tile','frameEnded','callback','xScale','yScale','rotation','affineEnded','matrixWrites'],cases=result)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--helper',type=Path,default=ROOT/'tests/Reference/reference_c.py');p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.write_text(json.dumps(build(a.reference,a.helper),separators=(',',':'))+'\n')

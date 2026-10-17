#!/usr/bin/env python3
"""Extract original SeekSpriteAnim and frame callbacks into a bounded oracle."""
import argparse,importlib.util,json,re,subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
spec=importlib.util.spec_from_file_location('reference_c',ROOT/'tests/Reference/reference_c.py');ref=importlib.util.module_from_spec(spec);spec.loader.exec_module(ref)
def function(source,name):
 m=re.search(r'^(?:static )?void '+name+r'\([^;]+?\)\n\{',source,re.M)
 if not m:raise ValueError('Missing sprite function '+name)
 end=m.end();depth=1
 while depth:depth+=(source[end]=='{')-(source[end]=='}');end+=1
 return source[m.start():end]
def build(reference):
 source=(reference/'src/sprite.c').read_text();effects=(reference/'src/battle_anim_effects_3.c').read_text()
 names=['BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','DecrementAnimDelayCounter']
 harness=r'''
#include <stdio.h>
#include <stdint.h>
#include <string.h>
typedef uint8_t u8;typedef int16_t s16;typedef uint32_t u32;
#define FALSE 0
#define TRUE 1
#define ST_OAM_AFFINE_ON_MASK 1
struct AnimFrameCmd {u32 imageValue:16;u32 duration:6;u32 hFlip:1;u32 vFlip:1;};
struct AnimLoopCmd {u32 type:16;u32 count:6;};
struct AnimJumpCmd {u32 type:16;u32 target:6;};
union AnimCmd {s16 type;struct AnimFrameCmd frame;struct AnimLoopCmd loop;struct AnimJumpCmd jump;};
#define ANIMCMD_FRAME(...) {.frame={__VA_ARGS__}}
#define ANIMCMD_LOOP(n) {.loop={.type=-3,.count=n}}
#define ANIMCMD_JUMP(n) {.jump={.type=-2,.target=n}}
#define ANIMCMD_END {.type=-1}
struct Sprite {u8 animCmdIndex,animLoopCounter,animNum;u8 animDelayCounter:6;int animEnded,animBeginning,animPaused,usingSheet,sheetTileStart,images,hFlip,vFlip;const union AnimCmd **anims;struct {int affineMode,tileNum;} oam;};
static void SetSpriteOamFlipBits(struct Sprite *s,u8 h,u8 v){s->hFlip=h;s->vFlip=v;}
static void RequestSpriteFrameImageCopy(int image,int tile,int images){}
'''
 harness+='\n'.join('void '+name+'(struct Sprite *);' for name in names)+'\n'
 harness+='static void (*sAnimCmdFuncs[])(struct Sprite*)={AnimCmd_loop,AnimCmd_jump,AnimCmd_end,AnimCmd_frame};\n'
 harness+='\n'.join(function(source,name) for name in names)+'\n'
 harness+='\n'+function(source,'SeekSpriteAnim')+'\n'
 flying=(reference/'src/battle_anim_flying.c').read_text()
 body=re.search(r'static const union AnimCmd sAffineAnim_AirWaveCrescent\[\] =\s*\{(.*?)\};',flying,re.S)[1]
 harness+='static const union AnimCmd air[]={ANIMCMD_END,'+body+'};\n'
 harness+=r'''
static const union AnimCmd marked[]={ANIMCMD_END,ANIMCMD_FRAME(0,2),ANIMCMD_LOOP(0),ANIMCMD_FRAME(16,1,.hFlip=TRUE),ANIMCMD_FRAME(32,0,.vFlip=TRUE),ANIMCMD_LOOP(2),ANIMCMD_FRAME(48,2),ANIMCMD_END};
static const union AnimCmd limits[]={ANIMCMD_END,ANIMCMD_FRAME(0,0),ANIMCMD_FRAME(16,1),ANIMCMD_FRAME(32,63),ANIMCMD_END};
static void output(const char *name,int tick,struct Sprite *s){printf("%s,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",name,tick,s->animCmdIndex,s->oam.tileNum,s->hFlip,s->vFlip,s->animDelayCounter,s->animEnded,s->animLoopCounter,s->animPaused);}
static void run(const char *name,const union AnimCmd *program,int seek,int paused,int loop){
 const union AnimCmd *anims[]={program+1};struct Sprite s;memset(&s,0,sizeof(s));s.anims=anims;s.usingSheet=1;BeginAnim(&s);s.animPaused=paused;s.animEnded=1;s.animLoopCounter=loop;SeekSpriteAnim(&s,seek);output(name,0,&s);
 for(int tick=1;tick<=8;tick++){if(!s.animEnded)ContinueAnim(&s);output(name,tick,&s);}
}
int main(void){
 run("air0",air,0,0,0);run("air1",air,1,0,0);run("air2",air,2,0,0);run("air3",air,3,0,0);run("air-jump",air,4,0,0);run("air-paused",air,2,1,0);
 run("marker-frame",marked,2,0,2);run("marker-loop",marked,4,0,2);run("marker-zero",marked,1,0,2);run("marker-end",marked,6,0,2);
 run("duration0",limits,0,0,0);run("duration1",limits,1,0,0);run("duration63",limits,2,0,0);
}
'''
 with tempfile.TemporaryDirectory() as tmp:
  c=Path(tmp)/'sprite-loops.c';exe=Path(tmp)/'sprite-loops';c.write_text(harness);ref.compile_c(['cc','-std=gnu11','-O0',str(c),'-o',str(exe)])
  output=subprocess.check_output([str(exe)],text=True)
 result={}
 for line in output.splitlines():
  name,*row=line.split(',');result.setdefault(name,[]).append(list(map(int,row)))
 return result
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.write_text(json.dumps(build(a.reference),separators=(',',':'))+'\n')

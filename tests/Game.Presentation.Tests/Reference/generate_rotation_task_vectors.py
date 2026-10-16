#!/usr/bin/env python3
"""Extract source rotation tasks; a bounded Q1.14 BIOS model supplies their matrices.

The task bodies, source s16 state, global argument mutation, reset and offset
function come from pinned C. BIOS ObjAffineSet is not present in the source:
its zero-scale-independent trigonometry is modeled explicitly from mathematical
Q1.14 samples, never read from product code. Ordinary tests only read the fixture.
"""
import argparse, importlib.util, json, math, re, subprocess, tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
CONFIGS=[(16,128,2),(3,-768,2),(3,768,0),(3,768,1),(2,32767,2),(3,-32768,1)]

def function(source,name):
 match=re.search(r'^(?:static )?(?:void|bool8) '+name+r'\([^;]+?\)\n\{',source,re.M)
 if not match:raise ValueError('Missing source function '+name)
 end=match.end();depth=1
 while depth:depth+=(source[end]=='{')-(source[end]=='}');end+=1
 return source[match.start():end]

def build(reference,helper):
 spec=importlib.util.spec_from_file_location('reference_c',helper);ref=importlib.util.module_from_spec(spec);spec.loader.exec_module(ref);ref.verify_reference(reference)
 movement=(reference/'src/battle_anim_mon_movement.c').read_text();mons=(reference/'src/battle_anim_mons.c').read_text()
 functions=[function(movement,n) for n in ['AnimTask_RotateMonToSideAndRestore','AnimTask_RotateMonSpriteToSide','AnimTask_RotateMonSpriteToSide_Step']]
 functions += [function(mons,n) for n in ['SetSpriteRotScale','ShouldRotScaleSpeciesBeFlipped','ResetSpriteRotScale','SetBattlerSpriteYOffsetFromRotation']]
 quarter=[int(math.sin(i*math.pi/128)*16384) for i in range(65)];quarter[0]=0;quarter[64]=16384
 harness=r'''
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <assert.h>
typedef uint8_t u8;typedef uint16_t u16;typedef int16_t s16;typedef int32_t s32;typedef u8 bool8;
#define ANIM_ATTACKER 0
#define B_SIDE_PLAYER 0
#define FALSE 0
#define ST_OAM_AFFINE_NORMAL 1
struct Task {s16 data[16];void (*func)(u8);};
struct Sprite {s16 y2;struct {u8 matrixNum,affineMode,objMode,shape,size;} oam;bool8 affineAnimPaused;};
struct OamMatrix {s16 a,b,c,d;};
struct ObjAffineSrcData {s16 xScale,yScale;u16 rotation;};
static struct Task gTasks[1];static struct Sprite gSprites[4];static struct OamMatrix gOamMatrices[4];
static s16 gBattleAnimArgs[8];static u8 gBattleAnimAttacker,gBattleAnimTarget;static int active;
static bool8 IsContest(void){return FALSE;}
static u8 GetBattlerSide(u8 battler){assert(battler<4);return battler&1;}
static u8 GetAnimBattlerSpriteId(u8 selector){assert(selector<2);return selector?gBattleAnimTarget:gBattleAnimAttacker;}
static void PrepareBattlerSpriteForRotScale(u8 sprite,u8 mode){assert(mode==0);gSprites[sprite].oam.affineMode=3;}
static void CalcCenterToCornerVec(struct Sprite *sprite,u8 shape,u8 size,u8 mode){}
static void DestroyAnimVisualTask(u8 task){assert(task==0);active=0;}
'''
 harness+='static const s16 quarter[] = {'+','.join(map(str,quarter))+'};\n'
 harness+=r'''
static s16 sine(u8 angle){int q=angle>>6,i=angle&63;int v=quarter[(q&1)?64-i:i];return q<2?v:-v;}
static void ObjAffineSet(const struct ObjAffineSrcData *src,struct OamMatrix *dst,int count,int stride){
 assert(count==1&&stride==2);u8 angle=src->rotation>>8;int s=sine(angle),c=sine(angle+64);
 dst->a=(src->xScale*c)>>14;dst->b=-((src->xScale*s)>>14);dst->c=(src->yScale*s)>>14;dst->d=(src->yScale*c)>>14;
}
'''
 harness+='\n'.join(f.split('\n{',1)[0]+';' for f in functions)+'\n'+'\n'.join(functions)+'\n'
 harness+='static const int configs[][3]={'+','.join('{'+','.join(map(str,c))+'}' for c in CONFIGS)+'};\n'
 harness+=r'''
static void emit(int family,int slot,int selector,int config,int tick){
 s16 *d=gTasks[0].data;struct OamMatrix m=gOamMatrices[slot];
 printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",family,slot,selector,config,tick,gBattleAnimArgs[1],d[3],d[4],d[1],d[6],m.a,m.b,m.c,m.d,gSprites[slot].y2,gSprites[slot].oam.affineMode==3,active);
}
int main(void){
 for(int family=0;family<2;family++)for(int slot=0;slot<4;slot++)for(int selector=0;selector<2;selector++)for(int config=0;config<6;config++){
  memset(gTasks,0,sizeof(gTasks));memset(gSprites,0,sizeof(gSprites));memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));active=1;
  for(int i=0;i<4;i++){gSprites[i].oam.matrixNum=i;gOamMatrices[i]=(struct OamMatrix){256,0,0,256};}
  gBattleAnimAttacker=selector?(slot^1):slot;gBattleAnimTarget=selector?slot:(slot^1);
  gBattleAnimArgs[0]=configs[config][0];gBattleAnimArgs[1]=configs[config][1];gBattleAnimArgs[2]=selector;gBattleAnimArgs[3]=configs[config][2];
  if(family)AnimTask_RotateMonToSideAndRestore(0);else AnimTask_RotateMonSpriteToSide(0);
  emit(family,slot,selector,config,-1);
  for(int tick=0;tick<100&&active;tick++){gTasks[0].func(0);emit(family,slot,selector,config,tick);}
  assert(!active);
 }
}
'''
 with tempfile.TemporaryDirectory(prefix='rotation-task-c-') as tmp:
  c=Path(tmp)/'rotation.c';exe=Path(tmp)/'rotation';c.write_text(harness);ref.compile_c(['cc','-std=gnu11','-O0',str(c),'-o',str(exe)])
  output=subprocess.check_output([str(exe)],text=True)
 cases={}
 for line in output.splitlines():
  family,slot,selector,config,*row=map(int,line.split(','));cases.setdefault(f'{family}:{slot}:{selector}:{config}',[]).append(row)
 if len(cases)!=96 or any(rows[0][0]!=-1 or rows[-1][-1]!=0 for rows in cases.values()):raise ValueError('Incomplete rotation vectors')
 return dict(configs=CONFIGS,cases=cases)
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');parser.add_argument('--helper',type=Path,default=ROOT/'tests/Reference/reference_c.py');parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
 if args.output.exists():parser.error('Refusing to overwrite an existing fixture.')
 args.output.write_text(json.dumps(build(args.reference,args.helper),separators=(',',':'))+'\n')

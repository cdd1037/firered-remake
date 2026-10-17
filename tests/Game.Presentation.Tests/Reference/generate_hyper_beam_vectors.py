#!/usr/bin/env python3
"""Source-faithful Hyper Beam motion oracle, explicitly NOT an OOB-frame oracle.

AnimHyperBeamOrb uses Random()%8 against a seven-entry source table. The bounded
StartSpriteAnim adapter records that operand without dereferencing it; only the
position, fast translation, wave, priority and draw sequence are asserted here.
The separately approved safe-seven policy is tested separately and never patched
into the extracted source callback. Original operand7 remains in this fixture.
"""
import argparse,importlib.util,json,re,subprocess,sys,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
SEEDS=[0,1,0x7fffffff,0x80000000,0xffffffff,0x12345678]
def randoms(seed):
 out=[]
 for _ in range(3):seed=(seed*1103515245+24691)&0xffffffff;out.append(seed>>16)
 return out
for wanted in range(8):SEEDS.append(next(seed for seed in range(100000) if randoms(seed)[0]%8==wanted))
for wanted in [0,126,127,255]:SEEDS.append(next(seed for seed in range(100000) if randoms(seed)[2]&255==wanted))
for wanted in [0,31]:SEEDS.append(next(seed for seed in range(100000) if randoms(seed)[1]&31==wanted))
SEEDS=list(dict.fromkeys(SEEDS))
COLUMNS=['tick','x','y','x2','y2','active','animationOperand','subpriority','draws','draw1','draw2','draw3','remaining','phase']
def function(source,name):
 m=re.search(r'^(?:static )?(?:void|s16|u16|bool8) '+name+r'\([^;]+?\)\n\{',source,re.M)
 if not m:raise ValueError('Missing '+name)
 end=m.end();depth=1
 while depth:depth+=(source[end]=='{')-(source[end]=='}');end+=1
 return source[m.start():end]
def build(reference,helper):
 sys.dont_write_bytecode=True;spec=importlib.util.spec_from_file_location('reference_c',helper);ref=importlib.util.module_from_spec(spec);spec.loader.exec_module(ref);ref.verify_reference(reference)
 mons=(reference/'src/battle_anim_mons.c').read_text();effects=(reference/'src/battle_anim_effects_1.c').read_text();trig=(reference/'src/trig.c').read_text();header=(reference/'include/battle_anim.h').read_text();random_header=(reference/'include/random.h').read_text();global_header=(reference/'include/global.h').read_text()
 funcs=[function(mons,n) for n in ['InitAnimFastLinearTranslation','InitAnimFastLinearTranslationWithSpeed','AnimFastTranslateLinear']]+[function(trig,'Cos')]+[function(effects,n) for n in ['AnimHyperBeamOrb','AnimHyperBeamOrb_Step']]
 harness=r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8;typedef uint16_t u16;typedef uint32_t u32;typedef int16_t s16;typedef int32_t s32;typedef uint8_t bool8;
#define FALSE 0
#define TRUE 1
#define B_SIDE_PLAYER 0
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
struct Sprite;typedef void(*SpriteCallback)(struct Sprite*);
struct Sprite{s16 x,y,x2,y2,data[8];u8 subpriority;bool8 inUse;int anim;SpriteCallback callback;};
static u8 gBattleAnimAttacker,gBattleAnimTarget;static u32 gRngValue;static int draws;static u16 values[3];
static const s16 sX[]={32,200,90,152},sY[]={88,40,96,32};
static u8 GetBattlerSide(u8 b){assert(b<4);return b&1;}
static s16 GetBattlerSpriteCoord(u8 b,u8 c){assert(b<4&&(c==2||c==3));return c==2?sX[b]:sY[b];}
static void DestroyAnimSprite(struct Sprite*s){s->inUse=FALSE;}
static void StartSpriteAnim(struct Sprite*s,u8 animation){assert(animation<8);s->anim=animation;}
'''
 harness+='\n'.join(re.findall(r'^#define sTransl_.*$',header,re.M))+'\n'+'\n'.join(re.findall(r'^#define (?:RAND_MULT|ISO_RANDOMIZE1)\b.*$',random_header,re.M))+'\n'
 harness+=re.search(r'^#define Q_8_8\(.*$',global_header,re.M)[0]+'\n'+re.search(r'^const s16 gSineTable\[\] =\n\{.*?\n\};',trig,re.M|re.S)[0]+'\n'
 harness+=function((reference/'src/random.c').read_text(),'Random')+'\nstatic u16 ObservedRandom(void){assert(draws<3);u16 v=Random();values[draws++]=v;return v;}\n#define Random ObservedRandom\n'
 harness+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'+'\n'.join(funcs)+'\n#undef Random\n'
 harness+='static const u32 seeds[]={'+','.join(str(seed)+'u' for seed in SEEDS)+'};\n'
 harness+=r'''
static void emit(struct Sprite*s,int config,int tick){int row[]={tick,s->x,s->y,s->x2,s->y2,s->inUse,s->anim,s->subpriority,draws,values[0],values[1],values[2],s->data[0],s->data[5]};printf("%d:%d:%d",gBattleAnimAttacker,gBattleAnimTarget,config);for(unsigned i=0;i<sizeof(row)/sizeof(row[0]);i++)printf(",%d",row[i]);puts("");}
static void run(int actor,int target,int config){gBattleAnimAttacker=actor;gBattleAnimTarget=target;gRngValue=seeds[config];draws=0;struct Sprite s={0};s.inUse=TRUE;s.subpriority=10;AnimHyperBeamOrb(&s);emit(&s,config,-1);for(int tick=0;tick<100;tick++){s.callback(&s);emit(&s,config,tick);if(!s.inUse){printf("R,%d:%d:%d,%u\n",actor,target,config,gRngValue);return;}}abort();}
int main(void){for(int actor=0;actor<4;actor++)for(int target=0;target<4;target++)for(unsigned config=0;config<sizeof(seeds)/sizeof(seeds[0]);config++)run(actor,target,config);}
'''
 with tempfile.TemporaryDirectory(prefix='hyper-beam-c-') as tmp:
  c=Path(tmp)/'hyper.c';exe=Path(tmp)/'hyper';c.write_text(harness);ref.compile_c(['cc','-std=c11','-O0',str(c),'-o',str(exe)]);out=subprocess.check_output([str(exe)],text=True)
 cases={};states={}
 for line in out.splitlines():
  if line.startswith('R,'):_,key,value=line.split(',');states[key]=int(value);continue
  key,*values=line.split(',');row=list(map(int,values));assert len(row)==len(COLUMNS);cases.setdefault(key,[]).append(row)
 assert len(cases)==16*len(SEEDS)
 assert {rows[0][6] for rows in cases.values()}==set(range(8))
 for key,rows in cases.items():
  assert rows[0][0]==-1 and rows[-1][5]==0 and all(r[5]==1 and r[8]==3 for r in rows[:-1]);assert rows[-1][8]==3
 return dict(columns=COLUMNS,seeds=SEEDS,cases=cases,finalStates=states)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--helper',type=Path,default=ROOT/'tests/Reference/reference_c.py');p.add_argument('--output',type=Path,required=True);a=p.parse_args()
 if a.output.exists():p.error('Refusing to overwrite an existing source fixture')
 result=build(a.reference,a.helper);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');print('cases',len(result['cases']),'rows',sum(len(v) for v in result['cases'].values()))

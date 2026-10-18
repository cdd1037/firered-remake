#!/usr/bin/env python3
"""Original falling-feather callbacks and native frame interpreter, local pinned input only."""
import argparse,hashlib,importlib.util,json,os,re,subprocess,tempfile
from pathlib import Path

def load(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def build(root):
 raw=root/'content/raw/pokefirered/037335f4';ref=load('feather_ref',root/'tests/Reference/reference_c.py');ref.verify_reference(raw)
 ex=load('feather_extract',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py')
 flying=(raw/'src/battle_anim_flying.c').read_text();sprite=(raw/'src/sprite.c').read_text();header=(raw/'include/sprite.h').read_text();trig=(raw/'src/trig.c').read_text()
 source={p:hashlib.sha256((raw/p).read_bytes()).hexdigest() for p in ['src/battle_anim_flying.c','src/sprite.c','include/sprite.h','include/battle_anim.h','src/trig.c']}
 funcs=[ex.function(sprite,n) for n in ['BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','DecrementAnimDelayCounter']]+[ex.function(flying,n) for n in ['AnimFallingFeather','AnimFallingFeather_Step','DestroyAnimSpriteAfterTimer']]
 configs=[[0,-16,a,2,104,11304,32,1] for a in [64,32,0,224,128,192,160,96]]
 configs += [[-17,-16,a|(tilt<<8),speed,104,0x133f,32,selector] for a,tilt,speed,selector in [(63,0,2,1),(64,255,2,1),(127,127,2,1),(128,128,2,1),(191,0,2,1),(192,0,2,1),(255,255,2,1),(0,0,32770,1),(64,33,32770,1),(128,255,32770,257),(192,128,32770,257),(0,255,7,257),(64,0,32775,257)]]
 # Width/priority edge cases and the source unsigned16 vertical accumulator.
 configs += [[-32768,-32,0xff40,32769,4096,0xffff,96,257],[32767,0,191,1,1,0x0100,1,1],[17,-16,32,0,104,11304,32,1]]
 h=r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8;typedef uint16_t u16;typedef uint32_t u32;typedef int8_t s8;typedef int16_t s16;typedef int32_t s32;typedef u8 bool8;
#define Q_8_8(n) ((s16)((n)*256))
#define TRUE 1
#define FALSE 0
#define B_SIDE_PLAYER 0
#define BATTLER_COORD_ATTR_HEIGHT 0
#define BATTLER_COORD_ATTR_WIDTH 1
#define ST_OAM_AFFINE_ON_MASK 1
#define ST_OAM_AFFINE_OFF 0
_Static_assert((-1>>1)==-1,"Arithmetic signed shift required");
'''+header[header.index('struct AnimFrameCmd\n'):header.index('\nenum\n')]+r'''
struct Sprite {u8 animCmdIndex,animLoopCounter,animNum;u8 animDelayCounter:6;bool8 inUse,animEnded,animBeginning,animPaused,usingSheet,hFlip,vFlip;int sheetTileStart,images; s16 x,y,x2,y2,data[8];u8 subpriority;struct {u8 affineMode,matrixNum;u16 priority:2;int tileNum;} oam;const union AnimCmd *const *anims;void(*callback)(struct Sprite*);};
struct OamMatrix{s16 a,b,c,d;};static struct OamMatrix gOamMatrices[1];static int matrixActive,gAnimVisualTaskCount;static s16 gBattleAnimArgs[8];static u8 gBattleAnimAttacker,gBattleAnimTarget;static int doubles,split;
static void SetSpriteOamFlipBits(struct Sprite*s,u8 h,u8 v){abort();}
static void RequestSpriteFrameImageCopy(int a,int b,int c){abort();}
static void FreeOamMatrix(u8 n){assert(n==0&&matrixActive);matrixActive=0;}
static void DestroySprite(struct Sprite*s){s->inUse=0;}
static int IsContest(void){return 0;}
static int GetBattlerSide(u8 b){return b&1;}
static int GetBattlerSpriteBGPriority(u8 b){return ((b==0||b==3)?1:0)+split;}
static int GetBattlerSpriteCoord(u8 id,int type){assert(type==0||type==1);int coords[2][4][2]={{{72,80},{176,40},{48,40},{112,80}},{{32,80},{200,40},{90,88},{152,32}}};return coords[doubles][id][type];}
'''
 h+=re.search(r'const s16 gSineTable\[\]\s*=\s*\{.*?\};',trig,re.S)[0]+'\n'+re.search(r'struct FeatherDanceData\s*\{.*?\};',flying,re.S)[0]+'\n_Static_assert(sizeof(struct FeatherDanceData)==16,"Original packed callback words required");\n'
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\nstatic void (*sAnimCmdFuncs[])(struct Sprite*)={AnimCmd_loop,AnimCmd_jump,AnimCmd_end,AnimCmd_frame};\n'+'\n'.join(funcs)+'\n'
 for n in ['sAnim_FallingFeather_0','sAnim_FallingFeather_1']:h+=ex.declaration(flying,n)+'\n'
 h+='static const union AnimCmd *const frames[]={sAnim_FallingFeather_0,sAnim_FallingFeather_1};\nstatic const int configs[][8]={'+','.join('{'+','.join(map(str,c))+'}' for c in configs)+'};\n'
 h+=r'''
static void animate(struct Sprite*s){if(s->animBeginning)BeginAnim(s);else ContinueAnim(s);}
int main(void){for(int d=0;d<2;d++)for(int actor=0;actor<(d?4:2);actor++)for(int target=0;target<(d?4:2);target++)for(unsigned config=0;config<sizeof(configs)/sizeof(configs[0]);config++)for(split=0;split<2;split++){
 doubles=d;gBattleAnimAttacker=actor;gBattleAnimTarget=target;for(int i=0;i<8;i++)gBattleAnimArgs[i]=configs[config][i];struct Sprite s={0};s.inUse=s.usingSheet=s.animBeginning=1;s.anims=frames;s.oam.affineMode=1;matrixActive=gAnimVisualTaskCount=1;memset(gOamMatrices,0,sizeof gOamMatrices);AnimFallingFeather(&s);animate(&s);
 for(int tick=-1;tick<20000;tick++){if(tick>=0){s.callback(&s);if(s.inUse)animate(&s);}struct FeatherDanceData*f=(struct FeatherDanceData*)s.data;struct OamMatrix*m=&gOamMatrices[0];printf("%d:%d:%d:%u:%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",d,actor,target,config,split,tick,s.inUse,s.x,s.y,s.x2,s.y2,s.oam.tileNum,s.hFlip,s.oam.priority,m->a,m->b,m->c,m->d,s.animEnded,s.animNum,f->unk0_0a,f->unk0_0b,f->unk0_0d,f->unk0_1,f->unk2,f->unk8,f->unkE_0,s.callback==DestroyAnimSpriteAfterTimer,gBattleAnimArgs[0],matrixActive,gAnimVisualTaskCount,s.animBeginning,s.animDelayCounter);if(!s.inUse)break;assert(tick<19999);}
 }return 0;}
'''
 with tempfile.TemporaryDirectory(prefix='feather-source-') as tmp:
  c=Path(tmp)/'feather.c';c.write_text(h);outputs=[]
  variants=[['-O0'],['-O2']]+([] if os.name=='nt' else [['-O2','-fsanitize=undefined','-fno-sanitize-recover=all']])
  for n,flags in enumerate(variants):
   exe=Path(tmp)/('feather-'+str(n));ref.compile_c(['cc','-std=gnu11',*flags,'-fno-strict-aliasing',str(c),'-o',str(exe)]);outputs.append(subprocess.check_output([str(exe)],text=True))
  if any(output!=outputs[0] for output in outputs[1:]):raise ValueError('Feather native optimization/sanitizer outputs differ')
  out=outputs[0]
 cases={}
 for line in out.splitlines():
  key,*row=line.split(',');cases.setdefault(key,[]).append(list(map(int,row)))
 assert len(cases)==20*len(configs)*2
 assert all(r[0][0]==-1 and r[-1][1]==0 and r[-1][-4:-2]==[0,0] for r in cases.values())
 return dict(sourceCommit=ref.COMMIT,sourceSha256=source,configs=configs,columns=['tick','active','x','y','x2','y2','tile','hflip','priority','a','b','c','d','frameEnded','animNum','pause','amplitude','flipPending','quadrant','angle','vertical','front','destroyNext','arg0','matrixActive','visualCount','animBeginning','frameDelay'],cases=cases,notes=['Key doubles:actor:target:config:split. Tick -1 is synchronous callback plus native BeginAnim. Native dummy affine END leaves the callback-owned matrix unchanged; frame flips never alter affine OAM.','GetBattlerSpriteCoord receives numeric selectors 0/1 via ATTR_HEIGHT/WIDTH and therefore reads base X/Y. Sprite dimensions, picture offset, elevation and current x2/y2 are irrelevant.','Native callbacks and frame interpreter extracted verbatim. Host adapters supply layout/priority, no-contest, matrix ownership and allocation-free destruction only. No upstream script or managed runtime executed.'])
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
 if a.output.exists():p.error('Refusing to overwrite fixture')
 data=build(a.root);a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(data,separators=(',',':'))+'\n');print('cases',len(data['cases']),'rows',sum(map(len,data['cases'].values())))

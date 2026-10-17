#!/usr/bin/env python3
"""Pinned in-bounds shake callbacks; the native arg[15] predicate write is never run.

The source declares gBattleAnimArgs[8]. Its predicate's scalar comparison is
verified separately, and the remake's 16-register safety policy has its own test.
Only authored host adapters supply battler presence, task deletion and OAM phase.
"""
import argparse,importlib.util,json,re,subprocess,tempfile
from pathlib import Path

def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def build(root):
 raw=root/'content/raw/pokefirered/037335f4';ref=module('reference_c',root/'tests/Reference/reference_c.py');ref.verify_reference(raw)
 extract=module('extract',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py').function
 ground=(raw/'src/battle_anim_ground.c').read_text();movement=(raw/'src/battle_anim_mon_movement.c').read_text()
 if not re.search(r'^#define ANIM_ARGS_COUNT\s+8\b',(raw/'include/battle_anim.h').read_text(),re.M):raise ValueError('Source argument declaration changed')
 predicate=extract(ground,'AnimTask_IsPowerOver99')
 if re.sub(r'\s+','',predicate)!='voidAnimTask_IsPowerOver99(u8taskId){gBattleAnimArgs[15]=gAnimMovePower>99;DestroyAnimVisualTask(taskId);}':raise ValueError('Source predicate changed')
 funcs=[extract(ground,n) for n in ['AnimTask_HorizontalShake','AnimTask_ShakeTerrain','AnimTask_ShakeBattlers','SetBattlersXOffsetForShake']]+[extract(movement,n) for n in ['AnimTask_ShakeTargetBasedOnMovePowerOrDmg','AnimTask_ShakeTargetBasedOnMovePowerOrDmg_Step']]
 h=r'''
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <assert.h>
typedef uint8_t u8;typedef uint16_t u16;typedef int16_t s16;typedef int32_t s32;
#define MAX_BATTLERS_COUNT 4
#define SPRITE_NONE 255
#define ANIM_TARGET 1
#define tState data[0]
#define tDelay data[1]
#define tTimer data[2]
#define tMaxTime data[3]
#define tbattlerSpriteIds(i) data[9+(i)]
#define tNumBattlers data[13]
#define tInitialX data[13]
#define tHorizOffset data[14]
#define tInitHorizOffset data[15]
struct Task{s16 data[16];void(*func)(u8);int alive;}gTasks[1];
struct Sprite{s16 x2,y2;}gSprites[4];
s16 gBattleAnimArgs[8];u16 gAnimMovePower,gBattle_BG3_X;s32 gAnimMoveDmg;
u8 gBattlerSpriteIds[4]={0,1,2,3};static int actor,target,present;
static int IsBattlerSpriteVisible(int slot){return (present>>slot)&1;}
static u8 GetAnimBattlerSpriteId(int which){int slot=which==0?actor:which==1?target:which==2?actor^2:target^2;return IsBattlerSpriteVisible(slot)?slot:SPRITE_NONE;}
static void DestroyAnimVisualTask(u8 id){gTasks[id].alive=0;}
'''
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'+'\n'.join(funcs)
 h+=r'''
static void reset(int a,int mask,int initial){
 memset(gTasks,0,sizeof(gTasks));memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));gTasks[0].alive=1;actor=a;target=a^1;present=mask;gBattle_BG3_X=initial?65534:0;
 for(int i=0;i<4;i++){gSprites[i].x2=initial?(i%2?32766:-32767):0;gSprites[i].y2=initial?(i%2?4:-6):0;}
}
static void print(int kind,int id,int tick,int* ox,int* oy){
 printf("%d,%d,%d,%d,%u",kind,id,tick,gTasks[0].alive,gBattle_BG3_X);
 for(int i=0;i<4;i++)printf(",%d,%d,%d,%d",gSprites[i].x2,gSprites[i].y2,ox[i],oy[i]);puts("");
}
static void run(int kind,int id){
 if(kind)AnimTask_ShakeTargetBasedOnMovePowerOrDmg(0);else AnimTask_HorizontalShake(0);
 for(int tick=0;tick<300000;tick++){
  int ox[4],oy[4];for(int i=0;i<4;i++){ox[i]=gSprites[i].x2;oy[i]=gSprites[i].y2;}
  if(gTasks[0].alive)gTasks[0].func(0);
  if(tick<220||tick%4096==0||!gTasks[0].alive)print(kind,id,tick,ox,oy);
  if(!gTasks[0].alive)return;
 }abort();
}
int main(void){
 int id=0;
 const int horizontal[][4]={{0,10,4,0},{1,0,3,99},{2,-1,1,0},{3,0,2,100},{4,0,2,150},{5,10,4,0},{4,0,2,0},{5,0,2,65535}};
 for(int a=0;a<4;a++)for(int mask=0;mask<2;mask++)for(int initial=0;initial<2;initial++)for(int c=0;c<8;c++){
  if(c==7&&(a!=0||mask!=0||initial!=1))continue;
  reset(a,mask?5:15,initial);gAnimMovePower=horizontal[c][3];for(int i=0;i<3;i++)gBattleAnimArgs[i]=horizontal[c][i];run(0,id++);
 }
 id=0;const s32 damages[]={-2147483647-1,-786444,-393216,-13,-1,0,11,12,23,24,36,191,192,393216,786420,786432,2147483647};
 const int powers[]={0,11,12,23,24,35,36,191,192,65535};
 for(int a=0;a<4;a++)for(int initial=0;initial<2;initial++)for(int mode=0;mode<4;mode++)for(int c=0;c<27;c++){
  reset(a,15,initial);gBattleAnimArgs[0]=c>=10;gAnimMovePower=c<10?powers[c]:0;gAnimMoveDmg=c>=10?damages[c-10]:0;gBattleAnimArgs[1]=c%3;gBattleAnimArgs[2]=7;gBattleAnimArgs[3]=mode&1;gBattleAnimArgs[4]=(mode>>1)&1;run(1,id++);
 }
 for(int p=0;p<5;p++){const int powers[]={0,99,100,150,65535};gAnimMovePower=powers[p];printf("2,%d,%d\n",gAnimMovePower,gAnimMovePower>99);}
}
'''
 with tempfile.TemporaryDirectory(prefix='power-shake-c-') as tmp:
  c=Path(tmp)/'oracle.c';exe=Path(tmp)/'oracle';c.write_text(h);ref.compile_c(['cc','-std=gnu11','-O0',str(c),'-o',str(exe)]);output=subprocess.check_output([str(exe)],text=True)
 groups={'horizontal':{},'target':{},'predicate':[]}
 for line in output.splitlines():
  kind,key,*row=map(int,line.split(','))
  if kind==2:groups['predicate'].append([key,*row])
  else:groups['target' if kind else 'horizontal'].setdefault(str(key),[]).append(row)
 assert len(groups['horizontal'])==113 and len(groups['target'])==864
 return dict(columns=['tick','active','bg3X']+[name+str(i) for i in range(4) for name in ['x2_','y2_','oamX2_','oamY2_']],**groups)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.write_text(json.dumps(build(a.root),separators=(',',':'))+'\n')

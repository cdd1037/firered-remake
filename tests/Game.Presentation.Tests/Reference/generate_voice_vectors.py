#!/usr/bin/env python3
"""Extracted pinned noise-line and terrain-register callbacks; external staging."""
import argparse,importlib.util,json,subprocess,tempfile
from pathlib import Path
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def build(root):
 ref=module('reference_c',root/'tests/Reference/reference_c.py');extract=module('extract',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py');fn=extract.function
 raw=root/'content/raw/pokefirered/037335f4';ref.verify_reference(raw)
 e=(raw/'src/battle_anim_effects_3.c').read_text();n=(raw/'src/battle_anim_normal.c').read_text()
 functions='\n'.join(fn(e,x) for x in ['AnimRoarNoiseLine','AnimRoarNoiseLine_Step'])+'\n'+'\n'.join(fn(n,x) for x in ['AnimTask_ShakeBattleTerrain','AnimTask_ShakeBattleTerrain_Step'])
 h=r'''
#include <stdint.h>
#include <stdio.h>
#include <string.h>
typedef uint8_t u8;typedef uint16_t u16;typedef int16_t s16;
#define TRUE 1
#define B_SIDE_PLAYER 0
#define B_SIDE_OPPONENT 1
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
struct Sprite {s16 x,y,x2,y2,data[8];u8 hFlip,vFlip,animNum,dead;void(*callback)(struct Sprite*);};
struct Task {s16 data[16];u8 dead;void(*func)(u8);};
struct Task gTasks[1];s16 gBattleAnimArgs[8];u8 gBattleAnimAttacker;u16 gBattle_BG3_X,gBattle_BG3_Y;int doubles;
static void AnimRoarNoiseLine_Step(struct Sprite*);static void AnimTask_ShakeBattleTerrain_Step(u8);
int GetBattlerSide(u8 id){return id&1;}
int GetBattlerSpriteCoord(u8 id,int c){int xy[2][4][2]={{{72,80},{176,40},{48,40},{112,80}},{{32,80},{200,40},{90,88},{152,32}}};return xy[doubles][id][c];}
void StartSpriteAnim(struct Sprite*s,u8 anim){s->animNum=anim;}
void DestroyAnimSprite(struct Sprite*s){s->dead=1;}
void DestroyAnimVisualTask(u8 id){gTasks[id].dead=1;}
#define tXOffset data[0]
#define tYOffset data[1]
#define tNumShakes data[2]
#define tTimer data[3]
#define tShakeDelay data[8]
'''+functions+r'''
int main(void){
 for(int mode=0;mode<2;mode++)for(int actor=0;actor<(mode?4:2);actor++)for(int kind=0;kind<3;kind++){
  doubles=mode;gBattleAnimAttacker=actor;struct Sprite s={0};memset(gBattleAnimArgs,0,sizeof gBattleAnimArgs);gBattleAnimArgs[0]=24;gBattleAnimArgs[1]=kind==0?-8:kind==1?8:0;gBattleAnimArgs[2]=kind;AnimRoarNoiseLine(&s);
  for(int t=-1;t<14;t++){if(t>=0)s.callback(&s);printf("N,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",mode,actor,kind,t,s.x,s.y,s.x2,s.y2,s.hFlip,s.vFlip,s.animNum,gBattleAnimArgs[0],s.dead,s.data[5]);}
 }
 int vectors[][4]={{1,0,6,1},{0,2,3,0},{-3,-2,4,2},{32767,-32768,1,0}};
 for(int k=0;k<4;k++){memset(gTasks,0,sizeof gTasks);for(int i=0;i<4;i++)gBattleAnimArgs[i]=vectors[k][i];AnimTask_ShakeBattleTerrain(0);
  for(int t=-1;t<30;t++){if(t>=0&&!gTasks[0].dead)gTasks[0].func(0);printf("T,%d,%d,%u,%u,%d,%d,%d\n",k,t,gBattle_BG3_X,gBattle_BG3_Y,gTasks[0].data[2],gTasks[0].data[3],gTasks[0].dead);if(gTasks[0].dead)break;}
 }
 return 0;}
'''
 with tempfile.TemporaryDirectory() as d:
  c=Path(d)/'voice.c';exe=Path(d)/'voice';c.write_text(h);ref.compile_c(['cc','-std=gnu11','-O0',str(c),'-o',str(exe)]);out=subprocess.check_output([str(exe)],text=True)
 rows=[line.split(',') for line in out.splitlines()];return {'noise':[list(map(int,r[1:])) for r in rows if r[0]=='N'],'terrain':[list(map(int,r[1:])) for r in rows if r[0]=='T']}
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.write_text(json.dumps(build(a.root),separators=(',',':'))+'\n')

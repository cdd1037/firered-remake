#!/usr/bin/env python3
import argparse,importlib.util,json,subprocess,tempfile
from pathlib import Path
def module(n,p):
 s=importlib.util.spec_from_file_location(n,p);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def build(root):
 ref=module('reference_c',root/'tests/Reference/reference_c.py');ex=module('extract',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py');raw=root/'content/raw/pokefirered/037335f4';ref.verify_reference(raw)
 e=(raw/'src/battle_anim_effects_2.c').read_text();m=(raw/'src/battle_anim_mons.c').read_text()
 h=r'''
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
typedef uint8_t u8;typedef uint16_t u16;typedef int16_t s16;typedef int32_t s32;typedef u8 bool8;
#define TRUE 1
#define FALSE 0
#define B_SIDE_PLAYER 0
#define BATTLE_PARTNER(x) ((x)^2)
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define sTransl_Speed data[0]
#define sTransl_InitX data[1]
#define sTransl_DestX data[2]
#define sTransl_InitY data[3]
#define sTransl_DestY data[4]
struct Sprite{s16 x,y,x2,y2,data[8];u8 subpriority,dead;void(*callback)(struct Sprite*);};
struct Sprite gSprites[4];u8 gBattlerSpriteIds[4]={0,1,2,3};s16 gBattleAnimArgs[8];u8 gBattleAnimAttacker,gBattleAnimTarget;int doubles,visible;
int IsContest(void){return 0;}int IsDoubleBattle(void){return doubles;}int GetBattlerSide(int b){return b&1;}int IsBattlerSpriteVisible(int b){return visible&(1<<b);}
int GetBattlerSpriteCoord(int b,int c){int xy[2][4][2]={{{72,80},{176,40},{48,40},{112,80}},{{32,80},{200,40},{90,88},{152,32}}};int v=xy[doubles][b][c&1];if(c==3)v+=(b&1)?9:15;return v;}
void FreeSpriteOamMatrix(struct Sprite*s){}void DestroyAnimSprite(struct Sprite*s){s->dead=1;}
'''
 for src,names in [(m,['InitAnimLinearTranslation','AnimTranslateLinear','SetAverageBattlerPositions']),(e,['AnimHyperVoiceRing_WaitEnd','AnimHyperVoiceRing'])]:
  for n in names:h+=ex.function(src,n)+'\n'
 h+=r'''
int main(void){int priorities[4]={30,40,20,50};for(int mode=0;mode<2;mode++)for(int actor=0;actor<(mode?4:2);actor++)for(int target=0;target<(mode?4:2);target++)for(int mask=0;mask<(mode?16:1);mask++)for(int pic=0;pic<2;pic++){
 doubles=mode;visible=mode?mask:3;gBattleAnimAttacker=actor;gBattleAnimTarget=target;memset(gSprites,0,sizeof gSprites);for(int i=0;i<4;i++){gSprites[i].x=GetBattlerSpriteCoord(i,0);gSprites[i].subpriority=priorities[i];}memset(gBattleAnimArgs,0,sizeof gBattleAnimArgs);gBattleAnimArgs[0]=45;gBattleAnimArgs[6]=pic;struct Sprite s={0};AnimHyperVoiceRing(&s);
 for(int t=-1;t<46;t++){if(t>=0)s.callback(&s);printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",mode,actor,target,visible,pic,t,s.x,s.y,s.x2,s.y2,s.subpriority,s.dead);if(s.dead)break;}}
return 0;}
'''
 with tempfile.TemporaryDirectory() as d:
  c=Path(d)/'hyper.c';exe=Path(d)/'hyper';c.write_text(h);ref.compile_c(['cc','-std=gnu11','-O0',str(c),'-o',str(exe)]);out=subprocess.check_output([str(exe)],text=True)
 return [list(map(int,line.split(','))) for line in out.splitlines()]
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.write_text(json.dumps(build(a.root),separators=(',',':'))+'\n')

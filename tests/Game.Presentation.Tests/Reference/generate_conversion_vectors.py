#!/usr/bin/env python3
"""Original Conversion sprite/task callbacks with external coordinate and fade-request adapters."""
import argparse,importlib.util,json,subprocess,tempfile
from pathlib import Path
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def build(root):
 ref=module('reference_c',root/'tests/Reference/reference_c.py');raw=root/'content/raw/pokefirered/037335f4';ref.verify_reference(raw)
 extract=module('extract',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py').function
 one=(raw/'src/battle_anim_effects_1.c').read_text();mons=(raw/'src/battle_anim_mons.c').read_text();normal=(raw/'src/battle_anim_normal.c').read_text()
 funcs=[extract(one,n) for n in ['AnimConversion','AnimConversion2','AnimConversion2_Step','AnimTask_ConversionAlphaBlend','AnimTask_Conversion2AlphaBlend']]+[extract(mons,n) for n in ['SetAnimSpriteInitialXOffset','InitSpritePosToAnimTarget','InitAnimLinearTranslation','StartAnimLinearTranslation','AnimTranslateLinear','AnimTranslateLinear_WithFollowup']]+[extract(normal,n) for n in ['AnimTask_FlashAnimTagWithColor','AnimTask_FlashAnimTagWithColor_Step1','AnimTask_FlashAnimTagWithColor_Step2']]
 h=r'''
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <assert.h>
typedef uint8_t u8;typedef uint16_t u16;typedef uint32_t u32;typedef int16_t s16;typedef int32_t s32;typedef u8 bool8;
#define TRUE 1
#define FALSE 0
#define B_SIDE_PLAYER 0
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define REG_OFFSET_BLDALPHA 0
#define BLDALPHA_BLEND(a,b) ((a)|((b)<<8))
#define RGB(r,g,b) ((r)|((g)<<5)|((b)<<10))
#define sTransl_Speed data[0]
#define sTransl_InitX data[1]
#define sTransl_DestX data[2]
#define sTransl_InitY data[3]
#define sTransl_DestY data[4]
struct Sprite;typedef void(*SpriteCallback)(struct Sprite*);
struct Sprite{s16 x,y,x2,y2,data[8];int alive,animPaused;SpriteCallback callback,stored;};
struct Task{s16 data[16];int alive;void(*func)(u8);}gTasks[1];
struct{int active;}gPaletteFade;
s16 gBattleAnimArgs[8];u8 gBattleAnimAttacker,gBattleAnimTarget;
static int coords[4][4]={{32,80,32,96},{200,40,200,48},{90,88,90,104},{152,32,152,40}};
static int alpha,fadeCalls,fadeSlot,fadeFrom,fadeColor;
static int IsContest(void){return 0;}static u8 GetBattlerSide(u8 n){return n&1;}
static int GetBattlerSpriteCoord(u8 n,u8 c){return coords[n][c];}
static int GetBattlerSpriteCoord2(u8 n,u8 c){return coords[n][c];}
static void DestroyAnimSprite(struct Sprite*s){s->alive=0;}
static void StoreSpriteCallbackInData6(struct Sprite*s,SpriteCallback cb){s->stored=cb;s->data[6]=0x1234;s->data[7]=0x5678;}
static void SetCallbackToStoredInData6(struct Sprite*s){s->callback=s->stored;}
static void DestroyAnimVisualTask(u8 id){gTasks[id].alive=0;}
static void SetGpuReg(int reg,int value){assert(reg==0);alpha=value;}
static u8 IndexOfSpritePaletteTag(int tag){assert(tag==10000);return 4;}
static int BeginNormalPaletteFade(u32 mask,int delay,int from,int to,int color){fadeCalls++;assert(mask==(1u<<20)&&delay==0&&from==to);fadeSlot=20;fadeFrom=from;fadeColor=color;return !gPaletteFade.active;}
'''
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'+'\n'.join(funcs)
 h+=r'''
static void sprites(int kind,int actor,int target,int delay){
 struct Sprite s={0};s.alive=1;gBattleAnimAttacker=actor;gBattleAnimTarget=target;memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));gBattleAnimArgs[0]=-24;gBattleAnimArgs[1]=24;gBattleAnimArgs[2]=delay;
 s.callback=kind?AnimConversion2:AnimConversion;
 for(int tick=-1;tick<180;tick++){
  if(!kind&&tick==72)gBattleAnimArgs[7]=-1;
  s.callback(&s);
  int phase=s.callback==AnimConversion?0:s.callback==AnimConversion2_Step?1:s.callback==StartAnimLinearTranslation?2:s.callback==AnimTranslateLinear_WithFollowup?3:4;
  printf("S,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",kind,actor,target,delay,tick,s.alive,s.x,s.y,s.x2,s.y2,s.animPaused,phase);
  if(!s.alive)return;
 }abort();
}
static void alphaTask(int kind){memset(gTasks,0,sizeof(gTasks));gTasks[0].alive=1;gBattleAnimArgs[7]=0;alpha=kind?4096:16;
 for(int tick=0;tick<80;tick++){if(kind)AnimTask_Conversion2AlphaBlend(0);else AnimTask_ConversionAlphaBlend(0);printf("A,%d,%d,%d,%d,%d,%d,%d,%d\n",kind,tick,gTasks[0].alive,gTasks[0].data[0],gTasks[0].data[1],gTasks[0].data[2],alpha,gBattleAnimArgs[7]);if(!gTasks[0].alive)return;}abort();}
static void flash(int scenario){memset(gTasks,0,sizeof(gTasks));gTasks[0].alive=1;int args[]={10000,1,1,14335,12,0,0};for(int i=0;i<7;i++)gBattleAnimArgs[i]=args[i];gTasks[0].func=AnimTask_FlashAnimTagWithColor;
 for(int tick=0;tick<30;tick++){gPaletteFade.active=scenario&&tick<9;fadeCalls=0;fadeFrom=fadeColor=-1;gTasks[0].func(0);printf("F,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",scenario,tick,gTasks[0].alive,gTasks[0].data[0],gTasks[0].data[1],gTasks[0].data[2],fadeCalls,fadeFrom,fadeColor,gPaletteFade.active);if(!gTasks[0].alive)return;}abort();}
int main(void){for(int a=0;a<4;a++){sprites(0,a,a,0);sprites(1,a,a,0);sprites(1,a,a,60);sprites(1,a,a,135);sprites(1,a,a^1,60);}alphaTask(0);alphaTask(1);flash(0);flash(1);}
'''
 with tempfile.TemporaryDirectory() as d:
  c=Path(d)/'conversion.c';exe=Path(d)/'conversion';c.write_text(h);ref.compile_c(['cc','-std=gnu11','-O0',str(c),'-o',str(exe)]);output=subprocess.check_output([str(exe)],text=True)
 rows={'sprites':[],'alpha':[],'flash':[]};names={'S':'sprites','A':'alpha','F':'flash'}
 for line in output.splitlines():
  tag,*values=line.split(',');rows[names[tag]].append(list(map(int,values)))
 return rows
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.write_text(json.dumps(build(a.root),separators=(',',':'))+'\n')

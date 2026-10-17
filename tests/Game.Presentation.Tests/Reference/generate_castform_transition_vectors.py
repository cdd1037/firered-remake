#!/usr/bin/env python3
"""Extract original TransformMon/visibility/immediate-form tasks; external adapters log writes."""
import argparse,importlib.util,json,subprocess,tempfile
from pathlib import Path
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def build(root):
 ref=module('reference_c',root/'tests/Reference/reference_c.py');ref.verify_reference(root/'content/raw/pokefirered/037335f4')
 extract=module('extract',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py').function
 source=(root/'content/raw/pokefirered/037335f4/src/battle_anim_effects_3.c').read_text()
 funcs='\n'.join(extract(source,n) for n in ['AnimTask_TransformMon','AnimTask_IsMonInvisible','AnimTask_CastformGfxChange'])
 h=r'''
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <assert.h>
typedef uint8_t u8;typedef uint16_t u16;typedef int16_t s16;
#define TRUE 1
#define REG_OFFSET_MOSAIC 0
#define BG_ANIM_MOSAIC 0
#define MON_PIC_SIZE 2048
#define B_SIDE_OPPONENT 1
#define ARG_RET_ID 7
struct Task{s16 data[16];}gTasks[1];
struct Sprite{u8 invisible;}gSprites[4];
struct BattleAnimBgData{u8*bgTiles;u16 tilesOffset;};
struct {u8*sprites[4];} gfx,*gMonSpritesGfxPtr=&gfx;
struct {struct{u16 transformSpecies;}battlerData[4];} spritesData,*gBattleSpritesDataPtr=&spritesData;
u8 gBattleAnimAttacker,gBattleAnimTarget,gBattlerSpriteIds[4]={0,1,2,3},gBattleMonForms[4];s16 gBattleAnimArgs[8];
static int alive,mosaic,enabled[3],swaps,copies,loads,shadows,kind;static u8 tiles[4][8192],background[8192];
static void SetGpuReg(int r,int v){mosaic=v;}
static int GetBattlerSpriteBGPriorityRank(int slot){return slot==0||slot==3?2:1;}
static void SetAnimBgAttribute(int bg,int attr,int value){enabled[bg]=value;}
static void HandleSpeciesGfxDataChange(u8 a,u8 b,u8 mode){assert(a==b);kind=mode;swaps++;gBattleMonForms[a]=3;}
static void GetBattleAnimBgDataByPriorityRank(struct BattleAnimBgData*b,int a){b->bgTiles=background;b->tilesOffset=0;}
static int IsContest(void){return 0;}static int GetBattlerPosition(int a){return a;}
static void CpuCopy32(const void*s,void*d,int n){copies++;memcpy(d,s,n);}
static void LoadBgTiles(int bg,const void*p,int n,int offset){assert(bg==1&&n==2048);loads++;}
static int GetBattlerSide(int a){return a&1;}
static void SetBattlerShadowSpriteCallback(int a,int species){shadows++;}
static void DestroyAnimVisualTask(int task){alive=0;}
'''+funcs+r'''
int main(void){for(int slot=0;slot<4;slot++)for(int mode=0;mode<2;mode++){
 memset(gTasks,0,sizeof(gTasks));memset(enabled,0,sizeof(enabled));swaps=copies=loads=shadows=mosaic=0;alive=1;gBattleAnimAttacker=gBattleAnimTarget=slot;gBattleAnimArgs[0]=mode;
 for(int i=0;i<4;i++)gfx.sprites[i]=tiles[i];
 for(int tick=0;alive&&tick<100;tick++){AnimTask_TransformMon(0);printf("T %d %d %d %d %d %d %d %d %d %d %d %d\n",slot,mode,tick,gTasks[0].data[0],gTasks[0].data[1],gTasks[0].data[2],mosaic,enabled[1],enabled[2],swaps,copies,alive);}
 assert(!alive&&swaps==1&&copies==1&&loads==1&&shadows==((slot&1)&&!mode));
 }for(int invisible=0;invisible<2;invisible++){alive=1;gSprites[0].invisible=invisible;gBattleAnimAttacker=gBattleAnimTarget=0;AnimTask_IsMonInvisible(0);printf("V %d %d %d\n",invisible,gBattleAnimArgs[7],alive);}
 alive=1;swaps=0;AnimTask_CastformGfxChange(0);printf("I %d %d %d\n",swaps,kind,alive);
}
'''
 with tempfile.TemporaryDirectory(prefix='castform-transition-') as tmp:
  p=Path(tmp);(p/'oracle.c').write_text(h);ref.compile_c(['cc','-std=c11','-O0',str(p/'oracle.c'),'-o',str(p/'oracle')]);out=subprocess.check_output([str(p/'oracle')],text=True)
 result={'tasks':{},'visibility':[],'immediate':[]}
 for line in out.splitlines():
  k,*values=line.split();v=list(map(int,values))
  if k=='T':result['tasks'].setdefault(f'{v[0]}:{v[1]}',[]).append(v[2:])
  elif k=='V':result['visibility'].append(v)
  else:result['immediate']=v
 return result
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();r=build(a.root);a.output.write_text(json.dumps(r,separators=(',',':'))+'\n');print('8 exact task traces; 2 visibility branches; 1 immediate swap')

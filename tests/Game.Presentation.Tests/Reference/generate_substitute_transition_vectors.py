#!/usr/bin/env python3
"""Original source Substitute task callbacks with external sprite/register adapters.

No implementation callback is called here. The frame driver reproduces the shared
create-immediate, built-OAM, RunTasks order; all task data mutations are extracted C.
The direct traces additionally expose state that isn't part of a public frame.
"""
import argparse, importlib.util, json, subprocess, tempfile
from pathlib import Path

def module(name, path):
    spec=importlib.util.spec_from_file_location(name,path); result=importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result

def build(root):
    ref=module('reference_c',root/'tests/Reference/reference_c.py'); ref.verify_reference(root/'content/raw/pokefirered/037335f4')
    extract=module('extract',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py').function
    source=(root/'content/raw/pokefirered/037335f4/src/battle_anim_effects_3.c').read_text()
    funcs='\n'.join(extract(source,name) for name in ['AnimTask_MonToSubstitute','AnimTask_MonToSubstituteDoll'])
    h=r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
typedef uint8_t u8; typedef int16_t s16;
#define FALSE 0
#define TRUE 1
#define NUM_TASK_DATA 16
#define ANIM_ATTACKER 0
#define SE_M_BUBBLE2 42
#define SOUND_PAN_ATTACKER -64
struct Task {s16 data[16];void(*func)(u8);} gTasks[1];
struct Sprite {s16 x,y,x2,y2;u8 invisible,affine;} gSprites[4];
u8 gBattleAnimAttacker;
static int alive,swaps,sounds,lastPan,sx,sy,doubles,ordinal,soundTicks[2];
static u8 GetAnimBattlerSpriteId(u8 who){assert(who==ANIM_ATTACKER);return gBattleAnimAttacker;}
static void PrepareBattlerSpriteForRotScale(u8 id,u8 mode){assert(id==gBattleAnimAttacker&&!mode);gSprites[id].invisible=0;gSprites[id].affine=1;}
static void SetSpriteRotScale(u8 id,s16 x,s16 y,int angle){assert(id==gBattleAnimAttacker&&!angle);sx=x;sy=y;}
static void ResetSpriteRotScale(u8 id){gSprites[id].affine=0;sx=sy=256;}
static int BaseY(int slot){int y[2][4]={{80,40,40,80},{80,40,88,32}};return y[doubles][slot];}
static void LoadBattleMonGfxAndAnimate(u8 slot,u8 mon,u8 sprite){assert(slot==sprite&&!mon);swaps++;gSprites[slot].y=BaseY(slot)+((slot&1)?16:17);}
static int BattleAnimAdjustPanning(int pan){assert(pan==-64);return (gBattleAnimAttacker&1)?63:-64;}
static void PlaySE12WithPanning(int sound,int pan){assert(sound==SE_M_BUBBLE2&&sounds<2);soundTicks[sounds++]=ordinal;lastPan=pan;}
static void DestroyAnimVisualTask(u8 id){assert(id==0);alive=0;}
static void AnimTask_MonToSubstituteDoll(u8 taskId);
'''+funcs+r'''
static void reset(int slot,int mode){
 memset(gTasks,0,sizeof(gTasks));memset(gSprites,0,sizeof(gSprites));
 gBattleAnimAttacker=slot;doubles=mode;alive=1;swaps=sounds=0;lastPan=127;sx=sy=256;
 gSprites[slot].y=BaseY(slot)+7;gTasks[0].func=AnimTask_MonToSubstitute;
}
static void print(char kind,int tick,int ox,int oy,int oi,int oa){
 struct Sprite*s=&gSprites[gBattleAnimAttacker];s16*d=gTasks[0].data;
 printf("%c %d %d %d %d %d %d %d %d %d %d %d %d %d %d %d %d %d %d %d %d %d %d %d %d\n",kind,doubles,gBattleAnimAttacker,tick,
 gTasks[0].func==AnimTask_MonToSubstituteDoll,d[0],d[1],d[2],d[3],d[10],s->y,s->x2,s->y2,s->invisible,s->affine,sx,sy,swaps,sounds,lastPan,alive,ox,oy,oi,oa);
}
int main(void){
 for(int mode=0;mode<2;mode++)for(int slot=0;slot<(mode?4:2);slot++){
  reset(slot,mode);
  for(ordinal=0;alive&&ordinal<200;ordinal++){gTasks[0].func(0);print('T',ordinal,0,0,0,0);}
  assert(!alive&&swaps==1&&sounds==2&&gSprites[slot].x2==0&&gSprites[slot].y2==0);
  reset(slot,mode);int ox=0,oy=0,oi=0,oa=0;
  for(ordinal=0;alive&&ordinal<200;ordinal++){
   if(ordinal==0)gTasks[0].func(0);
   struct Sprite*s=&gSprites[slot];oi=s->invisible;oa=s->affine;
   if(!oi){ox=s->x2;oy=s->y2;}
   gTasks[0].func(0);print('F',ordinal,ox,oy,oi,oa);
  }
  assert(!alive&&swaps==1&&sounds==2);
 }
}
'''
    with tempfile.TemporaryDirectory(prefix='substitute-callback-') as tmp:
        p=Path(tmp); (p/'oracle.c').write_text(h)
        ref.compile_c(['cc','-std=c11','-O0',str(p/'oracle.c'),'-o',str(p/'oracle')])
        output=subprocess.check_output([str(p/'oracle')],text=True)
    result={'columns':['ordinal','doll','state','xScaleData','yScaleData','count','speed','baseY','x2','y2','invisible','affine','matrixA','matrixD','swaps','sounds','lastPan','alive','oamX2','oamY2','oamInvisible','oamAffine'],'tasks':{},'frames':{}}
    for line in output.splitlines():
        key,*values=line.split(); v=list(map(int,values))
        result['tasks' if key=='T' else 'frames'].setdefault(f'{v[0]}:{v[1]}',[]).append(v[2:])
    assert len(result['tasks'])==6 and len(result['frames'])==6
    assert all(len(row)==len(result['columns']) for group in ['tasks','frames'] for rows in result[group].values() for row in rows)
    return result

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    if a.output.exists():raise SystemExit('Refusing to overwrite an existing reference fixture.')
    result=build(a.root);a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n')
    print('6 exact callback traces; 6 source frame-order traces; 2 landing sounds each')

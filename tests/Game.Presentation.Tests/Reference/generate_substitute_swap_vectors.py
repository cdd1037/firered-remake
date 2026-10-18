#!/usr/bin/env python3
"""Pinned Substitute controller swap callback; bounded external C oracle.

The extracted source owns every callback register mutation. The small frame
adapter observes priority-10 special creation after OAM and priority-2 callbacks
on subsequent frames. It does not claim controller or full script VM parity.
"""
import argparse, importlib.util, json, subprocess, tempfile
from pathlib import Path


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def build(root):
    ref = module('reference_c', root / 'tests/Reference/reference_c.py')
    reference = root / 'content/raw/pokefirered/037335f4'
    ref.verify_reference(reference)
    extract = module('extract', root / 'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py').function
    callback = extract((reference / 'src/battle_anim_special.c').read_text(), 'AnimTask_SwapMonSpriteToFromSubstitute')
    harness = r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
typedef uint8_t u8; typedef uint32_t u32; typedef int32_t bool32; typedef int16_t s16;
#define FALSE 0
#define TRUE 1
#define B_SIDE_PLAYER 0
struct Task {s16 data[16];} gTasks[1];
struct Sprite {s16 x,y,x2,y2;u8 invisible;} gSprites[4];
u8 gBattleAnimAttacker,gBattlerSpriteIds[4]={0,1,2,3};s16 gBattleAnimArgs[8];
static int alive,swaps,defaultY,selectedMon;
static u8 GetBattlerSide(u8 slot){return slot&1;}
static void LoadBattleMonGfxAndAnimate(u8 slot,u8 mon,u8 sprite){
 assert(slot==gBattleAnimAttacker&&sprite==slot&&mon==selectedMon);swaps++;gSprites[slot].y=defaultY;
}
static void DestroyAnimVisualTask(u8 id){assert(id==0);alive=0;}
''' + callback + r'''
struct Case {int mode,slot,mon,x,y,dy,x2,y2,hidden;};
static int BaseX(int mode,int slot){int x[2][4]={{72,176,48,112},{32,200,90,152}};return x[mode][slot];}
static int BaseY(int mode,int slot){int y[2][4]={{80,40,40,80},{80,40,88,32}};return y[mode][slot];}
static void reset(struct Case c){
 memset(gTasks,0,sizeof(gTasks));memset(gSprites,0,sizeof(gSprites));memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));
 gBattleAnimAttacker=c.slot;selectedMon=gBattleAnimArgs[0]=c.mon;alive=1;swaps=0;defaultY=c.dy;
 gSprites[c.slot]=(struct Sprite){c.x,c.y,c.x2,c.y2,c.hidden};
}
static void print(char kind,int id,int tick,int oi){
 struct Sprite*s=&gSprites[gBattleAnimAttacker];s16*d=gTasks[0].data;
 printf("%c %d %d %d %d %d %d %d %d %d %d %d %d %d\n",kind,id,tick,d[10],d[11],d[0],s->x,s->y,s->x2,s->y2,s->invisible,swaps,alive,oi);
}
int main(void){
 struct Case cases[20];int count=0;
 for(int mode=0;mode<2;mode++)for(int slot=0;slot<(mode?4:2);slot++)for(int mon=0;mon<2;mon++){
  int base=BaseY(mode,slot),body=base+7,doll=base+((slot&1)?16:17);
  cases[count++]=(struct Case){mode,slot,mon,BaseX(mode,slot),mon?doll:body,mon?body:doll,0,0,0};
 }
 // Two unsigned-boundary edges, a hidden battler, and raw-coordinate reset.
 cases[count++]=(struct Case){0,0,1,72,108,87,-99,-7,1};
 cases[count++]=(struct Case){0,1,0,176,54,56,91,9,1};
 cases[count++]=(struct Case){1,2,0,94,106,105,13,-5,0};
 cases[count++]=(struct Case){1,3,1,156,59,39,-11,6,1};
 // Post-Teleport source raw x, retained rise offset and invisibility.
 cases[count++]=(struct Case){0,0,0,272,87,97,0,-64,1};
 cases[count++]=(struct Case){0,1,0,272,47,56,0,-32,1};
 // A stage may also inherit an already-offscreen secondary x offset.
 cases[count++]=(struct Case){0,0,0,72,87,97,-200,3,0};
 cases[count++]=(struct Case){0,1,1,176,56,47,180,-3,0};
 assert(count==20);
 for(int id=0;id<count;id++){
  struct Case c=cases[id];
  printf("M %d %d %d %d %d %d %d %d %d %d\n",id,c.mode,c.slot,c.mon,c.x,c.y,c.dy,c.x2,c.y2,c.hidden);
  reset(c);
  for(int tick=0;alive&&tick<200;tick++){AnimTask_SwapMonSpriteToFromSubstitute(0);print('T',id,tick,0);}
  assert(!alive&&swaps==1&&gSprites[c.slot].x==c.x&&gSprites[c.slot].y==c.dy&&gSprites[c.slot].x2==0&&gSprites[c.slot].y2==c.y2&&gSprites[c.slot].invisible==c.hidden);
  reset(c);
  for(int tick=0;alive&&tick<200;tick++){
   int oi=gSprites[c.slot].invisible; // BuildOamBuffer precedes both table creation and RunTasks.
   AnimTask_SwapMonSpriteToFromSubstitute(0);print('F',id,tick,oi);
  }
  assert(!alive&&swaps==1);
 }
}
'''
    with tempfile.TemporaryDirectory(prefix='substitute-swap-c-') as temporary:
        path = Path(temporary)
        (path / 'oracle.c').write_text(harness)
        ref.compile_c(['cc', '-std=c11', '-O0', str(path / 'oracle.c'), '-o', str(path / 'oracle')])
        output = subprocess.check_output([str(path / 'oracle')], text=True)
    result = dict(columns=['ordinal', 'phase', 'loadMon', 'accumulator', 'x', 'y', 'x2', 'y2', 'invisible', 'swaps', 'alive', 'oamInvisible'], cases={})
    for line in output.splitlines():
        kind, key, *values = line.split()
        values = list(map(int, values))
        if kind == 'M':
            result['cases'][key] = dict(zip(['doubles', 'slot', 'loadMon', 'x', 'y', 'defaultY', 'x2', 'y2', 'hidden'], values))
            result['cases'][key].update(tasks=[], frames=[])
        else:
            assert kind in ('T', 'F') and len(values) == len(result['columns'])
            result['cases'][key]['tasks' if kind == 'T' else 'frames'].append(values)
    assert len(result['cases']) == 20
    assert all(c['tasks'] and c['frames'] and len(c['tasks']) < 200 and c['tasks'][-1][10] == 0 for c in result['cases'].values())
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise SystemExit('Refusing to overwrite existing oracle evidence.')
    result = build(args.root)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, separators=(',', ':')) + '\n')
    print('20 explicit swap cases; extracted C callback; both directions and all battle positions')

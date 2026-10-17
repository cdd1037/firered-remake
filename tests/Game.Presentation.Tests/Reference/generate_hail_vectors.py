#!/usr/bin/env python3
"""Pinned Hail task/callback and live-slot traversal oracle; no upstream scripts.

Resource loading, coordinate inputs and the frame-player observation stub are
harness adapters. Source callback bodies, coordinate table, first-free allocation
loop and live sprite traversal are extracted verbatim. No RNG exists in this path.
"""
import argparse,importlib.util,json,re,subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3] if len(Path(__file__).resolve().parents)>3 else Path.cwd()
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def build(root):
 ref=module('hail_reference',root/'tests/Reference/reference_c.py');ref.verify_reference(root/'content/raw/pokefirered/037335f4')
 extract=module('hail_extract',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py').function
 raw=root/'content/raw/pokefirered/037335f4/src';ice=(raw/'battle_anim_ice.c').read_text();sprites=(raw/'sprite.c').read_text()
 funcs=[extract(ice,n) for n in ['AnimTask_Hail','AnimTask_Hail2','GenerateHailParticle','AnimHailBegin','AnimHailContinue']]+[extract(sprites,n) for n in ['CreateSprite','AnimateSprites']]
 table=re.search(r'static const struct HailStruct sHailCoordData\[\] =\s*\{.*?\n\};',ice,re.S)[0]
 h=r'''
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <assert.h>
typedef uint8_t u8;typedef int8_t s8;typedef int16_t s16;typedef u8 bool8;
#define TRUE 1
#define FALSE 0
#define MAX_SPRITES 64
#define B_POSITION_PLAYER_LEFT 0
#define B_POSITION_OPPONENT_LEFT 1
#define B_POSITION_PLAYER_RIGHT 2
#define B_POSITION_OPPONENT_RIGHT 3
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define BATTLER_COORD_ATTR_WIDTH 0
#define BATTLER_COORD_ATTR_HEIGHT 1
struct Sprite { struct {u8 matrixNum;} oam; s16 x,y,data[8];u8 inUse,subpriority,variant,kind,begun;void(*callback)(struct Sprite*); };
struct Task {s16 data[16];void(*func)(u8);};
struct SpriteTemplate {int kind;void(*callback)(struct Sprite*);};
struct HailStruct {s16 x,y;u8 bPosition;s8 unk3;};
static struct Sprite gSprites[64];static struct Task gTasks[1];
static int tick,caseId,visibleMask,coords[4][4],activeTask,live,peak,allocs,impacts,frees,initialFailure,impactFailure;
static void AnimTask_Hail2(u8);static bool8 GenerateHailParticle(u8,u8,u8,u8);static void AnimHailBegin(struct Sprite*);static void AnimHailContinue(struct Sprite*);
static const struct SpriteTemplate sHailParticleSpriteTemplate={0,AnimHailBegin},gIceCrystalHitLargeSpriteTemplate={1,AnimHailContinue};
static u8 GetBattlerAtPosition(u8 p){return p;}
static bool8 IsBattlerSpriteVisible(u8 p){return (visibleMask>>p)&1;}
static s16 GetBattlerSpriteCoord(u8 p,u8 c){return coords[p][c-2];}
static s16 GetBattlerSpriteCoordAttr(u8 p,u8 c){return coords[p][c+2];}
static void StartSpriteAffineAnim(struct Sprite*s,u8 a){s->variant=a;}
static void FreeOamMatrix(u8 m){(void)m;}
static void DestroySprite(struct Sprite*s){s->inUse=0;live--;frees++;}
static void DestroyAnimVisualTask(u8 id){(void)id;activeTask=0;}
static void AnimateSprite(struct Sprite*s){s->begun=1;}
static void Reserved(struct Sprite*s){(void)s;}
static u8 CreateSpriteAt(u8 index,const struct SpriteTemplate*t,s16 x,s16 y,u8 priority){
 struct Sprite*s=&gSprites[index];memset(s,0,sizeof(*s));s->inUse=1;s->x=x;s->y=y;s->subpriority=priority;s->kind=t->kind;s->callback=t->callback;
 live++;if(live>peak)peak=live;allocs++;if(t->kind)impacts++;
 printf("A %d %d %d %d %d %d %d\n",caseId,tick,index,t->kind,x,y,live);return index;
}
static u8 CreateSprite(const struct SpriteTemplate*,s16,s16,u8);
'''+table+'\n'+'\n'.join(funcs)+r'''
int main(void){
 int configs[25][3],count=0;
 for(int mask=0;mask<16;mask++){configs[count][0]=1;configs[count][1]=0;configs[count++][2]=mask;}
 for(int form=1;form<4;form++){configs[count][0]=1;configs[count][1]=form;configs[count++][2]=15;}
 for(int form=0;form<4;form++){configs[count][0]=0;configs[count][1]=form;configs[count++][2]=3;}
 configs[count][0]=1;configs[count][1]=4;configs[count++][2]=15;
 for(caseId=0;caseId<count;caseId++){
  memset(gSprites,0,sizeof(gSprites));memset(gTasks,0,sizeof(gTasks));live=peak=allocs=impacts=frees=0;activeTask=1;
  int doubles=configs[caseId][0],form=configs[caseId][1],reserved=doubles?4:2;visibleMask=configs[caseId][2];
  int widths[]={32,48,32,64,64},heights[]={32,48,48,48,64},ys[]={17,9,9,8,0},els[]={13,14,13,13,0};
  int dx[]={32,200,90,152},dy[]={80,40,88,32},sx[]={72,176,48,112},sy[]={80,40,40,80};
  for(int i=0;i<4;i++){
   gSprites[i].inUse=i<reserved;gSprites[i].callback=Reserved;coords[i][0]=doubles?dx[i]:sx[i];int y=(doubles?dy[i]:sy[i])+((i&1)?ys[form]-els[form]:8);coords[i][1]=y>104?104:y;
   coords[i][2]=(i&1)?widths[form]:32;coords[i][3]=(i&1)?heights[form]:40;
   printf("G %d %d %d %d %d %d\n",caseId,i,coords[i][0],coords[i][1],coords[i][2],coords[i][3]);
  }
  // Reserved battler cells use an inert callback, excluded from animation capacity.

  printf("C %d %d %d %d\n",caseId,doubles,form,visibleMask);
  AnimTask_Hail(0);
  for(tick=0;tick<180&&activeTask;tick++){
   // Reserve actor cells around the extracted live traversal with inert callbacks.
   AnimateSprites();
   for(int i=reserved;i<64;i++)if(gSprites[i].inUse){struct Sprite*s=&gSprites[i];printf("R %d %d %d %d %d %d %d %d %d\n",caseId,tick,i,s->kind,s->x,s->y,s->variant,s->data[0],s->begun);}

   gTasks[0].func(0);

   printf("T %d %d %d %d %d %d\n",caseId,tick,gTasks[0].data[0],gTasks[0].data[1],live,activeTask);
  }
  printf("E %d %d %d %d %d %d %d\n",caseId,tick,peak,allocs,impacts,frees,activeTask);
 }
 // Initial CreateSprite failure returns FALSE and owns no task counter.
 memset(gSprites,0,sizeof(gSprites));memset(gTasks,0,sizeof(gTasks));
 for(int i=0;i<64;i++){gSprites[i].inUse=1;gSprites[i].callback=Reserved;}
 int initial=GenerateHailParticle(0,0,0,1);assert(!initial);assert(gTasks[0].data[1]==0);
 printf("P 0 %d %d\n",initial,gTasks[0].data[1]);
 // Exhaust all remaining cells after one visible-hit parent is allocated.
 memset(gSprites,0,sizeof(gSprites));memset(gTasks,0,sizeof(gTasks));live=peak=allocs=impacts=frees=0;visibleMask=15;caseId=99;tick=0;
 for(int i=0;i<4;i++){gSprites[i].inUse=1;gSprites[i].callback=Reserved;}
 assert(GenerateHailParticle(1,0,0,1));gTasks[0].data[1]++;
 for(int i=5;i<64;i++){gSprites[i].inUse=1;gSprites[i].callback=Reserved;}
 for(tick=0;tick<30;tick++)AnimateSprites();
 assert(!gSprites[4].inUse&&gTasks[0].data[1]==1&&impacts==0);
 printf("P 1 %d %d %d\n",gSprites[4].inUse,gTasks[0].data[1],impacts);
}
'''
 with tempfile.TemporaryDirectory(prefix='hail-source-') as temp:
  p=Path(temp);(p/'hail.c').write_text(h);ref.compile_c(['cc','-std=c11','-O0',str(p/'hail.c'),'-o',str(p/'hail')]);out=subprocess.check_output([str(p/'hail')],text=True)
 cases={};probes=[]
 for line in out.splitlines():
  kind,*nums=line.split();v=list(map(int,nums))
  if kind=='P':probes.append(v);continue
  if v[0]==99:continue
  case=cases.setdefault(str(v[0]),{'geometry':[],'rendered':[],'tasks':[],'allocations':[]})
  key={'G':'geometry','R':'rendered','T':'tasks','A':'allocations','C':'config','E':'end'}[kind]
  if kind in 'CE':case[key]=v[1:]
  else:case[key].append(v[1:])
 for c in cases.values():
  assert c['end'][-1]==0 and c['end'][2]==30+c['end'][3] and c['end'][4]==c['end'][2]
  assert c['end'][1]<=14
 return {'cases':cases,'failureProbes':probes}
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,default=ROOT);p.add_argument('--output',type=Path,default=Path(__file__).with_name('hail-source-vectors.json'));a=p.parse_args();result=build(a.root);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');print('Hail:',len(result['cases']),'source cases; peak',max(c['end'][1] for c in result['cases'].values()))

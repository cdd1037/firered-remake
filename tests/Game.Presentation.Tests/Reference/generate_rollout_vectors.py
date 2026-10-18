#!/usr/bin/env python3
"""Extracted pinned Rollout task, raw child, horizontal arc, allocation and traversal.

Only battle coordinates/pan, resource allocation and inert dummy frame services are
host adapters. No upstream script or product implementation executes.
"""
import argparse,importlib.util,json,re,subprocess,tempfile
from pathlib import Path

def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def build(root):
 raw=root/'content/raw/pokefirered/037335f4';ref=module('rolling_ref',root/'tests/Reference/reference_c.py');ref.verify_reference(raw)
 extract=module('rolling_extract',root/'tests/Game.Presentation.Tests/Reference/generate_arc_projectile_vectors.py').function
 rock=(raw/'src/battle_anim_rock.c').read_text();mons=(raw/'src/battle_anim_mons.c').read_text();sprite=(raw/'src/sprite.c').read_text();trig=(raw/'src/trig.c').read_text()
 funcs=[extract(rock,n) for n in ['AnimTask_Rollout','AnimTask_Rollout_Step','CreateRolloutDirtSprite','AnimRolloutParticle','GetRolloutCounter']]
 funcs += [extract(mons,n) for n in ['InitAnimArcTranslation','TranslateAnimHorizontalArc','InitAnimLinearTranslation','AnimTranslateLinear']]
 panSource=(raw/'src/battle_anim.c').read_text().replace('s8 BattleAnimAdjustPanning(', 's16 BattleAnimAdjustPanning(')
 funcs += [extract(panSource,'BattleAnimAdjustPanning').replace('s16 BattleAnimAdjustPanning(', 's8 BattleAnimAdjustPanning(')]
 funcs += [extract(sprite,n) for n in ['CreateSprite','AnimateSprites']]+[extract(trig,'Sin')]
 table=re.search(r'^const s16 gSineTable\[\] =\n\{.*?\n\};',trig,re.M|re.S)[0]
 q=re.search(r'^#define Q_8_8\(n\).*$',(raw/'include/global.h').read_text(),re.M)[0]
 h=r'''
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <assert.h>
typedef uint8_t u8;typedef int8_t s8;typedef uint16_t u16;typedef int16_t s16;typedef int32_t s32;typedef u8 bool8;
#define TRUE 1
#define FALSE 0
#define MAX_SPRITES 64
#define TASK_NONE 255
#define ANIM_ATTACKER 0
#define B_SIDE_PLAYER 0
#define B_SIDE_OPPONENT 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y 1
#define BATTLE_PARTNER(b) ((b)^2)
#define SOUND_PAN_ATTACKER -64
#define SOUND_PAN_TARGET 63
#define SE_M_HEADBUTT 155
#define SE_M_DIG 168
#define sTransl_Speed data[0]
#define sTransl_InitX data[1]
#define sTransl_DestX data[2]
#define sTransl_InitY data[3]
#define sTransl_DestY data[4]
#define sTransl_ArcAmpl data[5]
struct Sprite{struct {u16 tileNum;}oam;s16 x,y,x2,y2,data[8];u8 inUse,kind,subpriority;void(*callback)(struct Sprite*);};
struct Task{s16 data[16];void(*func)(u8);};
struct SpriteTemplate{u8 kind;void(*callback)(struct Sprite*);};
struct DisableStruct{u8 rolloutTimerStartValue,rolloutTimer;}disable,*gAnimDisableStructPtr=&disable;
static struct Sprite gSprites[64];static struct Task gTasks[1];
static u8 gBattleAnimAttacker,gBattleAnimTarget;static int doubles,active,tick,caseId,reserved,allocated,failed;
static void AnimTask_Rollout_Step(u8);static void AnimRolloutParticle(struct Sprite*);
static const struct SpriteTemplate gRolloutMudSpriteTemplate={0,AnimRolloutParticle},gRolloutRockSpriteTemplate={1,AnimRolloutParticle};
static u8 GetBattlerSpriteCoord(u8 b,u8 c){int dx[]={32,200,90,152},dy[]={80,40,88,32},sx[]={72,176,48,112},sy[]={80,40,40,80};return c==2?(doubles?dx[b]:sx[b]):(doubles?dy[b]:sy[b]);}
struct BattleSpritesData{struct {int statusAnimActive;}healthBoxesData[4];}battleSprites,*gBattleSpritesDataPtr=&battleSprites;
static u8 GetBattlerSide(u8 b){return b&1;}
static u8 GetAnimBattlerSpriteId(u8 b){return b==0?gBattleAnimAttacker:gBattleAnimTarget;}
static void PlaySE12WithPanning(u16 sound,s8 pan){printf("S %d %d %u %d\n",caseId,tick,sound,pan);}
static void DestroyAnimVisualTask(u8 id){(void)id;active=0;}
static void DestroySprite(struct Sprite*s){s->inUse=0;}
static u8 FindTaskIdByFunc(void(*f)(u8)){return active&&gTasks[0].func==f?0:TASK_NONE;}
static void Reserved(struct Sprite*s){(void)s;}
static void AnimateSprite(struct Sprite*s){(void)s;}
static u8 CreateSpriteAt(u8 index,const struct SpriteTemplate*t,s16 x,s16 y,u8 priority){struct Sprite*s=&gSprites[index];memset(s,0,sizeof(*s));s->inUse=1;s->kind=t->kind;s->callback=t->callback;s->x=x;s->y=y;s->subpriority=priority;allocated++;return index;}
'''+q+'\n'+table+'\n'+'\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'+'\n'.join(funcs)+r'''
int main(void){
 caseId=0;
 for(doubles=0;doubles<2;doubles++)for(int actor=0;actor<(doubles?4:2);actor++)for(int ally=0;ally<(doubles?2:1);ally++)for(int counter=1;counter<=5;counter++)for(int saturation=0;saturation<2;saturation++){
  gBattleAnimAttacker=actor;gBattleAnimTarget=actor^(ally?2:1);reserved=doubles?4:2;active=1;allocated=failed=0;disable.rolloutTimerStartValue=5;disable.rolloutTimer=5-counter;
  memset(gSprites,0,sizeof(gSprites));memset(gTasks,0,sizeof(gTasks));
  for(int i=0;i<(saturation?64:reserved);i++){gSprites[i].inUse=1;gSprites[i].callback=Reserved;}
  printf("C %d %d %d %d %d %d\n",caseId,doubles,actor,gBattleAnimTarget,counter,saturation);AnimTask_Rollout(0);
  for(tick=0;tick<160&&active;tick++){
   AnimateSprites();int ox=gSprites[actor].x2,oy=gSprites[actor].y2;
   for(int i=reserved;i<64;i++)if(gSprites[i].inUse&&gSprites[i].callback!=Reserved){struct Sprite*s=&gSprites[i];printf("R %d %d %d %d %d %d %d %d %d\n",caseId,tick,i,s->kind,s->x,s->y,s->x2,s->y2,s->oam.tileNum);}
   gTasks[0].func(0);
   printf("T %d %d %d %d %d %d %d %d %d\n",caseId,tick,active,gSprites[actor].x2,gSprites[actor].y2,ox,oy,gTasks[0].data[11],allocated);
  }
  assert(!active);assert(gSprites[actor].x2==0&&gSprites[actor].y2==0);if(saturation)assert(allocated==0);else assert(allocated>0);caseId++;
 }
 for(int start=0;start<256;start++)for(int end=0;end<256;end++){disable.rolloutTimerStartValue=start;disable.rolloutTimer=end;int result=GetRolloutCounter();int d=(u8)(start-end);assert(result==(d>=1&&d<=5?d:1));}
 printf("P %d 65536\n",caseId);
}
'''
 with tempfile.TemporaryDirectory(prefix='rollout-c-') as temp:
  p=Path(temp);(p/'oracle.c').write_text(h);ref.compile_c(['cc','-std=c11','-O0',str(p/'oracle.c'),'-o',str(p/'oracle')]);out=subprocess.check_output([str(p/'oracle')],text=True)
 cases={};probes=[]
 for line in out.splitlines():
  kind,*v=line.split();v=list(map(int,v))
  if kind=='C':cases[str(v[0])]={'inputs':v[1:],'frames':[],'sprites':[],'sounds':[]}
  elif kind=='P':probes=v
  else:cases[str(v[0])][{'T':'frames','R':'sprites','S':'sounds'}[kind]].append(v[1:])
 assert probes==[100,65536] and len(cases)==100
 return {'frameColumns':['tick','active','x2','y2','oamX2','oamY2','children','allocated'],'spriteColumns':['tick','slot','kind','x','y','x2','y2','tile'],'soundColumns':['tick','song','pan'],'counterProbeCount':probes[1],'cases':cases}
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(build(a.root),separators=(',',':'))+'\n')

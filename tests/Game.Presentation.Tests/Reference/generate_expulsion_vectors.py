#!/usr/bin/env python3
"""Extract pinned expulsion callbacks and shared interpreters into bounded C harnesses."""
import argparse,importlib.util,json,re,subprocess,tempfile
from pathlib import Path

def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def build(root):
 raw=root/'content/raw/pokefirered/037335f4';ref=module('ref',root/'tests/Reference/reference_c.py');ref.verify_reference(raw)
 ex=module('extract',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py');fn=ex.function
 mons=(raw/'src/battle_anim_mons.c').read_text();move=(raw/'src/battle_anim_mon_movement.c').read_text();psy=(raw/'src/battle_anim_psychic.c').read_text();battle=(raw/'src/battle_anim.c').read_text()
 shim=(root/'tests/Game.Presentation.Tests/Reference/generate_task_affine_vectors.py').read_text();h=re.search(" harness=r'''(.*?)'''",shim,re.S)[1]
 h=h.replace('gSprites[1]','gSprites[64]').replace('gOamMatrices[1]','gOamMatrices[64]').replace('static int active,applied,yDelta,battleSide,centerX=72,centerY=96;','static int active,applied,yDelta,battleSide,centerX=72,centerY=96;static int presentMask=15;static u8 gBattleAnimTarget;static u8 gBattlerSpriteIds[4]={4,9,13,22};static struct {struct {int invisible;} battlerData[4];} bs,*gBattleSpritesDataPtr=&bs;')
 h=h.replace('static void PrepareBattlerSpriteForRotScale(u8 sprite,u8 mode){active=1;}','static void PrepareBattlerSpriteForRotScale(u8 sprite,u8 mode){active=1;gSprites[sprite].oam.matrixNum=sprite;gSprites[sprite].animNum=1;if(IsBattlerSpriteVisible(gSprites[sprite].data[0]))gSprites[sprite].invisible=0;}')
 h=h.replace('gOamMatrices[0].d=y','gOamMatrices[sprite].d=y').replace('gOamMatrices[0].d=256;active=0','gOamMatrices[sprite].d=256;gSprites[sprite].animNum=0')
 h=h.replace('static u8 GetAnimBattlerSpriteId(int battler){return 0;}','static bool8 IsBattlerSpritePresent(u8 battler){return (presentMask&(1<<battler))!=0;}')
 h=h.replace('static int GetBattlerSide(int battler){return battleSide;}','static int GetBattlerSide(int battler){return battler&1;}')
 h=h.replace('static void PrepareBattlerSpriteForRotScale','static bool8 IsBattlerSpriteVisible(u8);\nstatic void PrepareBattlerSpriteForRotScale')
 h+='\n#define ANIM_TARGET 1\n#define ANIM_ATK_PARTNER 2\n#define ANIM_DEF_PARTNER 3\n#define BIT_FLANK 2\n#define BATTLE_PARTNER(b) ((b)^2)\n#define DISPLAY_WIDTH 240\n#define SPRITE_NONE 255\nstatic void AnimTask_Teleport_Step(u8);static void AnimTask_SlideOffScreen_Step(u8);\n'
 h+=fn(battle,'IsBattlerSpriteVisible')+'\n'+fn(mons,'GetAnimBattlerSpriteId')+'\n'+ex.declaration(psy,'sAffineAnim_Teleport')+'\n'
 for source,names in [(mons,['PrepareAffineAnimInTaskData','RunAffineAnimFromTaskData','SetBattlerSpriteYOffsetFromYScale']),(psy,['AnimTask_Teleport','AnimTask_Teleport_Step']),(move,['AnimTask_SlideOffScreen','AnimTask_SlideOffScreen_Step'])]:
  h+='\n'.join(fn(source,n) for n in names)+'\n'
 h+=r'''
static int coords[2][4][2]={{{72,80},{176,40},{48,40},{112,80}},{{32,80},{200,40},{90,88},{152,32}}};
static void reset(int doubles,int logical,int invisible){memset(gTasks,0,sizeof gTasks);memset(gSprites,0,sizeof gSprites);memset(gBattleAnimArgs,0,sizeof gBattleAnimArgs);active=1;presentMask=doubles?15:3;for(int i=0;i<4;i++){int id=gBattlerSpriteIds[i];gSprites[id].x=coords[doubles][i][0];gSprites[id].y=coords[doubles][i][1]+yDelta;gSprites[id].x2=7;gSprites[id].y2=9;gSprites[id].data[0]=i;gSprites[id].oam.matrixNum=id;gOamMatrices[id].d=256;bs.battlerData[i].invisible=(logical>>i)&1;gSprites[id].invisible=(invisible>>i)&1;}}
int main(void){
 for(int doubles=0;doubles<2;doubles++)for(int actor=0;actor<(doubles?4:2);actor++)for(int target=0;target<(doubles?4:2);target++)for(int part=0;part<4;part++)for(int vis=0;vis<4;vis++)for(int variant=0;variant<3;variant++){
  yDelta=7;gBattleAnimAttacker=actor;gBattleAnimTarget=target;int selected=part==0?actor:part==1?target:part==2?actor^2:target^2;reset(doubles,vis&1?15:0,vis&2?15:0);gBattleAnimArgs[0]=part;gBattleAnimArgs[1]=variant==0?2:variant==1?8:32767;
  int id=gBattlerSpriteIds[selected];gSprites[id].x+=variant==1?17:0;gSprites[id].x2=variant==2?32760:7;
  AnimTask_SlideOffScreen(0);for(int t=-1;t<220;t++){if(t>=0&&active)gTasks[0].func(0);printf("S,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",doubles,actor,target,part,vis,variant,t,selected,id,gSprites[id].x,gSprites[id].x2,gSprites[id].y2,active);if(!active)break;}
 }
 for(int doubles=0;doubles<2;doubles++)for(int actor=0;actor<(doubles?4:2);actor++)for(int height=0;height<=31;height++)for(int vis=0;vis<4;vis++){
  yDelta=height;gBattleAnimAttacker=actor;gBattleAnimTarget=actor^1;reset(doubles,vis&1?15:0,vis&2?15:0);int id=gBattlerSpriteIds[actor];AnimTask_Teleport(0);
  for(int t=-1;t<32;t++){if(t>=0)gTasks[0].func(0);printf("T,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",doubles,actor,height,vis,t,gSprites[id].x,gSprites[id].y,gSprites[id].x2,gSprites[id].y2,gTasks[0].data[10],gTasks[0].data[11],gSprites[id].animNum,gSprites[id].invisible,active);if(!active)break;}
 }
 return 0;}
'''
 def run(source,name):
  with tempfile.TemporaryDirectory() as d:
   c=Path(d)/(name+'.c');exe=Path(d)/name;c.write_text(source);ref.compile_c(['cc','-std=gnu11','-O0',str(c),'-o',str(exe)]);return subprocess.check_output([str(exe)],text=True)
 rows=[l.split(',') for l in run(h,'expulsion-tasks').splitlines()];result={'slide':[list(map(int,r[1:])) for r in rows if r[0]=='S'],'teleport':[list(map(int,r[1:])) for r in rows if r[0]=='T']}
 sprite=(raw/'src/sprite.c').read_text();flying=(raw/'src/battle_anim_flying.c').read_text();header=(raw/'include/sprite.h').read_text()
 names=['BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','DecrementAnimDelayCounter','StartSpriteAnim','SeekSpriteAnim']
 funcs=[fn(sprite,n) for n in names]+[fn(mons,n) for n in ['SetAnimSpriteInitialXOffset','InitSpritePosToAnimTarget','InitSpritePosToAnimAttacker']]+[fn(flying,n) for n in ['AnimWhirlwindLine','AnimWhirlwindLine_Step']]
 h=r'''
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8;typedef uint16_t u16;typedef uint32_t u32;typedef int8_t s8;typedef int16_t s16;typedef int32_t s32;typedef u8 bool8;
#define TRUE 1
#define FALSE 0
#define ANIM_ATTACKER 0
#define ANIM_TARGET 1
#define B_SIDE_PLAYER 0
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define ST_OAM_AFFINE_ON_MASK 1
'''+header[header.index('struct AnimFrameCmd\n'):header.index('\nenum\n')]+r'''
struct Sprite {u8 animCmdIndex,animLoopCounter,animNum;u8 animDelayCounter:6;bool8 inUse,animEnded,animBeginning,animPaused,usingSheet,hFlip,vFlip;int sheetTileStart,images; s16 x,y,x2,y2,data[8];struct {u8 affineMode;int tileNum;} oam;const union AnimCmd *const *anims;void(*callback)(struct Sprite*);};
static s16 gBattleAnimArgs[8];static u8 gBattleAnimAttacker,gBattleAnimTarget;static int doubles;
static void SetSpriteOamFlipBits(struct Sprite*s,u8 h,u8 v){s->hFlip=h;s->vFlip=v;}
static void RequestSpriteFrameImageCopy(int a,int b,int c){abort();}
static void DestroyAnimSprite(struct Sprite*s){s->inUse=0;}
static int GetBattlerSide(u8 b){return b&1;}
static int GetBattlerSpriteCoord(u8 id,int type){int coords[2][4][2]={{{72,80},{176,40},{48,40},{112,80}},{{32,80},{200,40},{90,88},{152,32}}};return coords[doubles][id][type&1];}
static int GetBattlerSpriteCoord2(u8 id,int type){return GetBattlerSpriteCoord(id,type);}
'''
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\nstatic void (*sAnimCmdFuncs[])(struct Sprite*)={AnimCmd_loop,AnimCmd_jump,AnimCmd_end,AnimCmd_frame};\n'+'\n'.join(funcs)+'\n'+ex.declaration(flying,'sAnim_WhirlwindLines')+r'''
static const union AnimCmd *const frames[]={sAnim_WhirlwindLines};
static void animate(struct Sprite*s){if(s->animBeginning)BeginAnim(s);else ContinueAnim(s);}
int main(void){for(int d=0;d<2;d++)for(int actor=0;actor<(d?4:2);actor++)for(int target=0;target<(d?4:2);target++)for(int part=0;part<2;part++)for(int seek=0;seek<5;seek++)for(int life=0;life<2;life++){
 doubles=d;gBattleAnimAttacker=actor;gBattleAnimTarget=target;memset(gBattleAnimArgs,0,sizeof gBattleAnimArgs);gBattleAnimArgs[0]=-17;gBattleAnimArgs[1]=8;gBattleAnimArgs[2]=part;gBattleAnimArgs[3]=life?60:0;gBattleAnimArgs[4]=seek;struct Sprite s={0};s.inUse=s.usingSheet=s.animBeginning=1;s.anims=frames;AnimWhirlwindLine(&s);animate(&s);
 for(int tick=-1;tick<64;tick++){if(tick>=0){s.callback(&s);if(s.inUse)animate(&s);}printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",d,actor,target,part,seek,life,tick,s.x,s.y,s.x2,s.oam.tileNum,s.hFlip,s.animEnded,s.data[0],s.data[7],s.inUse);if(!s.inUse)break;}
 }return 0;}
'''
 result['whirlwind']=[list(map(int,l.split(','))) for l in run(h,'whirlwind').splitlines()]
 cry=module('cry',root/'tests/Game.Presentation.Tests/Reference/generate_cry_task_vectors.py');source=(root/'tests/Game.Presentation.Tests/Reference/generate_cry_task_vectors.py').read_text().replace('kind<3','kind<4').replace('gTasks[0].data[0]=255','gTasks[0].data[0]=kind==3?2:255').replace('if(kind==0)SoundTask_PlayDoubleCry_Step','if(kind==0||kind==3)SoundTask_PlayDoubleCry_Step')
 namespace={'__name__':'expulsion_cry','__file__':str(root/'tests/Game.Presentation.Tests/Reference/generate_cry_task_vectors.py')};exec(source,namespace);result['cries']=namespace['build'](root)
 return result
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.write_text(json.dumps(build(a.root),separators=(',',':'))+'\n')

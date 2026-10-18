#!/usr/bin/env python3
"""Pinned Psycho Boost callbacks, native affine LOOP/Change scheduler and both Psychic palette tasks.

Explicit fixture generation only; executes an authored bounded C harness, never upstream scripts.
"""
import argparse,hashlib,importlib.util,json,os,subprocess,tempfile
from pathlib import Path

def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def build(root):
 raw=root/'content/raw/pokefirered/037335f4';ref=module('reference_c',root/'tests/Reference/reference_c.py');ref.verify_reference(raw)
 extract=module('extract_fang',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py');function=extract.function;declaration=extract.declaration
 sprite=(raw/'src/sprite.c').read_text();psychic=(raw/'src/battle_anim_psychic.c').read_text();effects=(raw/'src/battle_anim_effects_3.c').read_text();header=(raw/'include/sprite.h').read_text()
 engine=(raw/'src/battle_anim.c').read_text();pan_start=engine.index('s8 BattleAnimAdjustPanning(s8 pan)\n');pan_end=engine.index('\n}',pan_start)+2;pan_function=engine[pan_start:pan_end]
 names=['AnimateSprites','AnimateSprite','BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','DecrementAnimDelayCounter','BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_loop','BeginAffineAnimLoop','ContinueAffineAnimLoop','JumpToTopOfAffineAnimLoop','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame','AffineAnimStateRestartAnim','AffineAnimStateReset','ApplyAffineAnimFrameAbsolute','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame','ApplyAffineAnimFrame','ChangeSpriteAffineAnim']
 funcs=[pan_function]+[function(sprite,n) for n in names]+[function(psychic,'AnimPsychoBoost')]+[function(effects,n) for n in ['AnimTask_SetPsychicBackground','SetPsychicBackground_Step','AnimTask_FadeScreenToWhite','FadeScreenToWhite_Step']]
 h=r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8;typedef uint16_t u16;typedef uint32_t u32;typedef int8_t s8;typedef int16_t s16;typedef int32_t s32;typedef u8 bool8;
#define TRUE 1
#define FALSE 0
#define MAX_SPRITES 1
#define ST_OAM_AFFINE_ON_MASK 1
#define sAnchorX data[6]
#define sAnchorY data[7]
#define SAFE_DIV(a,b) (assert((b)!=0),(a)/(b))
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define REG_OFFSET_BLDCNT 0
#define REG_OFFSET_BLDALPHA 1
#define BLDCNT_TGT2_ALL 0x3F00
#define BLDCNT_EFFECT_BLEND 0x40
#define BLDALPHA_BLEND(a,b) ((a)|((b)<<8))
#define BG_PLTT_ID(n) ((n)*16)
#define SE_M_TELEPORT 1
#define B_SIDE_PLAYER 0
#define B_SIDE_OPPONENT 1
#define SOUND_PAN_ATTACKER -64
#define SOUND_PAN_TARGET 63
'''
 h+=header[header.index('struct AnimFrameCmd\n'):header.index('\nenum\n')]+r'''
struct Sprite;typedef void(*SpriteCallback)(struct Sprite*);
struct Sprite{u8 animCmdIndex,animLoopCounter,animNum;u8 animDelayCounter:6;bool8 inUse,animEnded,animBeginning,animPaused,usingSheet,hFlip,vFlip;bool8 affineAnimBeginning,affineAnimEnded,affineAnimPaused,anchored,invisible;int sheetTileStart,images;s16 x,y,x2,y2,data[8];struct{u8 affineMode;int tileNum;}oam;const union AnimCmd *const *anims;const union AffineAnimCmd *const *affineAnims;SpriteCallback callback;};
struct ObjAffineSrcData{s16 xScale,yScale;u16 rotation;};struct OamMatrix{s16 a,b,c,d;};
static struct Sprite gSprites[1];static struct AffineAnimState sAffineAnimStates[1];static bool8 gAffineAnimsDisabled;
static int blendcnt,alpha,sounds,soundpan,doubles;static u8 gBattleAnimAttacker,gBattleAnimTarget;static struct BattleSprites {struct {int statusAnimActive;}healthBoxesData[4];}battleSprites;static struct BattleSprites*gBattleSpritesDataPtr=&battleSprites;
static struct Task{s16 data[16];int alive;void(*func)(u8);}gTasks[1];static int gAnimVisualTaskCount;static s16 gBattleAnimArgs[8];static u16 gPlttBufferFaded[256],gPlttBufferUnfaded[256];
static void SetSpriteOamFlipBits(struct Sprite*s,u8 h,u8 v){s->hFlip=h;s->vFlip=v;}
static void RequestSpriteFrameImageCopy(int a,int b,int c){abort();}
static void DestroyAnimSprite(struct Sprite*s){s->inUse=FALSE;}
static u8 GetSpriteMatrixNum(struct Sprite*s){return 0;}
static void UpdateSpriteMatrixAnchorPos(struct Sprite*s,s32 x,s32 y){abort();}
static void CopyOamMatrix(u8 n,struct OamMatrix*m){assert(n==0);}
static void ObjAffineSet(const struct ObjAffineSrcData*s,struct OamMatrix*d,int n,int stride){assert(n==1&&stride==2);memset(d,0,sizeof(*d));}
static int IsContest(void){return 0;}
static int GetBattlerSpriteCoord(u8 battler,u8 coord){static const int single[4][2]={{72,80},{176,40},{48,40},{112,80}},both[4][2]={{32,80},{200,40},{90,88},{152,32}};return doubles?both[battler][coord]:single[battler][coord];}
static u8 GetBattlerSide(u8 battler){return battler&1;}
static void PlaySE12WithPanning(int song,int pan){assert(song==SE_M_TELEPORT);sounds++;soundpan=pan;}
static void SetGpuReg(int reg,int value){if(reg==0)blendcnt=value;else{assert(reg==1);alpha=value;}}
static u8 GetBattleBgPaletteNum(void){return 2;}
static void DestroyTask(u8 id){assert(id==0);gTasks[id].alive=0;}
'''
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'
 h+='static void (*sAnimFuncs[])(struct Sprite*)={ContinueAnim,BeginAnim};\nstatic void (*sAnimCmdFuncs[])(struct Sprite*)={AnimCmd_loop,AnimCmd_jump,AnimCmd_end,AnimCmd_frame};\nstatic void (*sAffineAnimFuncs[])(struct Sprite*)={ContinueAffineAnim,BeginAffineAnim};\nstatic void (*sAffineAnimCmdFuncs[])(u8,struct Sprite*)={AffineAnimCmd_loop,AffineAnimCmd_jump,AffineAnimCmd_end,AffineAnimCmd_frame};\n'
 h+='\n'.join(funcs)+'\n'+declaration(psychic,'sAffineAnim_PsychoBoostOrb_0')+'\n'+declaration(psychic,'sAffineAnim_PsychoBoostOrb_1')+r'''
static const union AnimCmd dummy[]={ANIMCMD_FRAME(0,1),ANIMCMD_JUMP(0)};static const union AnimCmd *const anims[]={dummy};static const union AffineAnimCmd *const affines[]={sAffineAnim_PsychoBoostOrb_0,sAffineAnim_PsychoBoostOrb_1};
static void runOrb(int d,int actor){doubles=d;gBattleAnimAttacker=actor;gBattleAnimTarget=actor^1;struct Sprite*s=&gSprites[0];memset(s,0,sizeof(*s));AffineAnimStateReset(0);s->inUse=TRUE;s->animBeginning=TRUE;s->usingSheet=TRUE;s->anims=anims;s->affineAnims=affines;s->affineAnimBeginning=TRUE;s->oam.affineMode=3;s->callback=AnimPsychoBoost;blendcnt=alpha=sounds=soundpan=0;
 for(int tick=-1;tick<512;tick++){AnimateSprites();struct AffineAnimState*a=&sAffineAnimStates[0];printf("O,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",d,actor,tick,s->inUse,s->x,s->y,s->x2,s->y2,s->invisible,s->data[0],s->data[1],s->data[2],s->data[3],a->xScale,a->yScale,a->rotation,s->affineAnimEnded,blendcnt,alpha,sounds,soundpan,s->oam.tileNum);if(!s->inUse)return;}abort();}
static void change(int config){static const int states[][3]={{304,304,0},{256,256,256},{512,-512,65077},{-300,400,32768}};struct Sprite*s=&gSprites[0];memset(s,0,sizeof(*s));s->oam.affineMode=3;s->affineAnims=affines;AffineAnimStateReset(0);sAffineAnimStates[0].xScale=states[config][0];sAffineAnimStates[0].yScale=states[config][1];sAffineAnimStates[0].rotation=states[config][2];ChangeSpriteAffineAnim(s,1);
 for(int tick=0;tick<32;tick++){sAffineAnimFuncs[s->affineAnimBeginning](s);struct AffineAnimState*a=&sAffineAnimStates[0];printf("C,%d,%d,%d,%d,%d,%d\n",config,tick,a->xScale,a->yScale,a->rotation,s->affineAnimEnded);if(s->affineAnimEnded)return;}abort();}
static void palette(int kind,int stop){memset(gTasks,0,sizeof(gTasks));memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));for(int i=0;i<256;i++){gPlttBufferFaded[i]=i+100;gPlttBufferUnfaded[i]=i+1000;}gTasks[0].alive=1;gAnimVisualTaskCount=1;if(kind)AnimTask_FadeScreenToWhite(0);else AnimTask_SetPsychicBackground(0);
 for(int tick=0;tick<40;tick++){if(tick==9)for(int i=0;i<16;i++)gPlttBufferFaded[32+i]=gPlttBufferUnfaded[32+i]+500;if(tick==stop)gBattleAnimArgs[7]=-1;gTasks[0].func(0);printf("P,%d,%d,%d,%d,%d,%d",kind,stop,tick,gTasks[0].alive,gTasks[0].data[5],gAnimVisualTaskCount);for(int i=0;i<16;i++)printf(",%u",gPlttBufferFaded[32+i]);for(int i=0;i<16;i++)printf(",%u",gPlttBufferUnfaded[32+i]);printf("\n");if(!gTasks[0].alive)return;}abort();}
int main(void){for(int d=0;d<2;d++)for(int a=0;a<(d?4:2);a++)runOrb(d,a);for(int c=0;c<4;c++)change(c);int stops[]={0,2,3,4,11,23};for(int k=0;k<2;k++)for(int i=0;i<6;i++)palette(k,stops[i]);return 0;}
'''
 with tempfile.TemporaryDirectory() as tmp:
  c=Path(tmp)/'psycho.c';c.write_text(h);outputs=[]
  variants=[['-O0'],['-O2']]+([] if os.name=='nt' else [['-O2','-fsanitize=undefined','-fno-sanitize-recover=all']])
  for n,flags in enumerate(variants):
   exe=Path(tmp)/('psycho-'+str(n));ref.compile_c(['cc','-std=gnu11',*flags,str(c),'-o',str(exe)]);outputs.append(subprocess.check_output([str(exe)],text=True))
  if any(output!=outputs[0] for output in outputs[1:]):raise ValueError('Psycho Boost native optimization/sanitizer outputs differ')
 rows={'orb':[],'change':[],'palette':[]};names={'O':'orb','C':'change','P':'palette'}
 for line in outputs[0].splitlines():
  tag,*values=line.split(',');rows[names[tag]].append(list(map(int,values)))
 rows['sourceSha256']={name:hashlib.sha256((raw/name).read_bytes()).hexdigest() for name in ['src/battle_anim.c','src/sprite.c','src/battle_anim_psychic.c','src/battle_anim_effects_3.c','include/sprite.h']}
 rows['schema']='firered-psycho-boost-source-v1';return rows

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();data=json.dumps(build(a.root),separators=(',',':'))+'\n'
 if a.check:
  if a.output.read_text()!=data:raise SystemExit('Psycho Boost fixture differs')
 else:
  if a.output.exists():raise SystemExit('Refusing to overwrite existing fixture')
  a.output.write_text(data)
 print(json.dumps({'passed':True,'output':str(a.output)}))

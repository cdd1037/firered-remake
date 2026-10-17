#!/usr/bin/env python3
"""Pinned Sonic Boom, drifting bubbles and Bonemerang callback/affine C oracle.

Extracts callback bodies, movement helpers, signed sine arithmetic, Random and
AnimateSprites/affine players verbatim. Only host pointer storage, fixed battler
coordinates and BIOS matrix/angle operations are adapted. No product code or
upstream build script runs. Tick -1 is CreateSpriteAndAnimate; each later row is
one callback followed by AnimateSprite only while still active. Both arc legs,
the unsnapped return origin, signed bubble velocity and RNG draw order are kept.
"""
import argparse,importlib.util,json,re,subprocess,sys,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
SONIC=[[16,0,0,0,15],[20,12,10,-12,1],[-128,127,127,-128,4],[127,-128,-128,127,255]]
BUBBLES=[[seed,0,20] for seed in [0,1,0x7fffffff,0x80000000,0xffffffff,0x12345678]]
for wanted in [0,255,256,257,511]:
 for seed in range(100000):
  a=(seed*1103515245+24691)&0xffffffff;b=(a*1103515245+24691)&0xffffffff
  if ((b>>16)&511)==wanted:BUBBLES.append([seed,10,-20]);break
BUBBLES += [[1,-32768,32767],[1,32767,-32768]]
COLUMNS=['tick','x','y','x2','y2','active','callback','tile','rotation','a','b','c','d','draws','arg0','arg1','arg2','arg3','data0','data1','data2','data3','data4','data5','data6','data7']
def function(source,name):
 m=re.search(r'^(?:static )?(?:void|s8|s16|s32|u8|u16|bool8) '+name+r'\([^;]+?\)\n\{',source,re.M)
 if not m:raise ValueError('Missing source function '+name)
 i=m.end();depth=1
 while depth:depth+=(source[i]=='{')-(source[i]=='}');i+=1
 return source[m.start():i]
def declaration(source,name):
 m=re.search(r'^(?:static )?const (?:union AffineAnimCmd|s16) '+name+r'\[\]\s*=\s*\{.*?\};',source,re.M|re.S)
 if not m:raise ValueError('Missing source declaration '+name)
 return m[0]
def build(reference,helper,angle_adapter):
 sys.dont_write_bytecode=True
 spec=importlib.util.spec_from_file_location('reference_c',helper);ref=importlib.util.module_from_spec(spec);spec.loader.exec_module(ref);ref.verify_reference(reference)
 sources={n:(reference/('src/'+n+'.c')).read_text() for n in ['battle_anim_mons','battle_anim_ground','battle_anim_water','battle_anim_effects_2','sprite','trig','random']}
 header=(reference/'include/sprite.h').read_text();affine=header[header.index('struct AffineAnimFrameCmd\n'):header.index('\nenum\n')]
 global_header=(reference/'include/global.h').read_text();battle_header=(reference/'include/battle_anim.h').read_text();random_header=(reference/'include/random.h').read_text()
 funcs=[function(sources['battle_anim_mons'],n) for n in ['InitSpritePosToAnimAttacker','InitSpritePosToAnimTarget','SetAnimSpriteInitialXOffset','InitAnimLinearTranslation','StartAnimLinearTranslation','AnimTranslateLinear','AnimTranslateLinear_WithFollowup','InitAnimArcTranslation','TranslateAnimHorizontalArc','TrySetSpriteRotScale','ShouldRotScaleSpeciesBeFlipped']]
 funcs += [function(sources['battle_anim_effects_2'],'AnimSonicBoomProjectile')]
 funcs += [function(sources['battle_anim_water'],n) for n in ['AnimSmallDriftingBubbles','AnimSmallDriftingBubbles_Step']]
 funcs += [function(sources['battle_anim_ground'],n) for n in ['AnimBonemerangProjectile','AnimBonemerangProjectile_Step','AnimBonemerangProjectile_End']]
 funcs += [function(sources['trig'],'Sin')]
 funcs += [function(sources['sprite'],n) for n in ['AnimateSprites','AnimateSprite','BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame','AffineAnimStateRestartAnim','AffineAnimStateReset','ApplyAffineAnimFrameAbsolute','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame','ApplyAffineAnimFrame']]
 harness=r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8;typedef uint16_t u16;typedef uint32_t u32;typedef int8_t s8;typedef int16_t s16;typedef int32_t s32;typedef uint8_t bool8;
#define TRUE 1
#define FALSE 0
#define MAX_SPRITES 1
#define ST_OAM_AFFINE_ON_MASK 1
#define B_SIDE_PLAYER 0
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define SAFE_DIV(a,b) (assert((b)!=0),(a)/(b))
#define sAnchorX data[6]
#define sAnchorY data[7]
_Static_assert((-1>>1)==-1,"Source requires signed arithmetic shift");
'''
 harness+=affine+'\n'+'\n'.join(re.findall(r'^#define sTransl_.*$',battle_header,re.M))+'\n'
 harness+='\n'.join(re.findall(r'^#define (?:RAND_MULT|ISO_RANDOMIZE1)\b.*$',random_header,re.M))+'\n'
 harness+=re.search(r'^#define Q_8_8\(.*$',global_header,re.M)[0]+'\n'+declaration(sources['trig'],'gSineTable')+'\n'+angle_adapter.read_text()
 harness+=r'''
struct Sprite;typedef void(*SpriteCallback)(struct Sprite*);
struct Sprite {s16 x,y,x2,y2,data[8];bool8 inUse,affineAnimBeginning,affineAnimEnded,affineAnimPaused,anchored,animBeginning;struct {int affineMode,matrixNum,shape,size,tileNum;}oam;const union AffineAnimCmd *const *affineAnims;SpriteCallback callback,stored;};
struct ObjAffineSrcData {s16 xScale,yScale;u16 rotation;};struct OamMatrix{s16 a,b,c,d;};
static struct Sprite gSprites[1];static struct AffineAnimState sAffineAnimStates[1];static struct OamMatrix gOamMatrices[1];static bool8 gAffineAnimsDisabled;static u16 rotation;static u32 gRngValue;static int draws;
static u8 gBattleAnimAttacker,gBattleAnimTarget;static s16 gBattleAnimArgs[8];
static const s16 sX[]={32,200,90,152},sY[]={88,40,96,32};
static bool8 IsContest(void){return FALSE;}
static u8 GetBattlerSide(u8 b){assert(b<4);return b&1;}
static s16 GetBattlerSpriteCoord(u8 b,u8 c){assert(b<4&&c<4);return(c==0||c==2)?sX[b]:sY[b];}
static s16 GetBattlerSpriteCoord2(u8 b,u8 c){return GetBattlerSpriteCoord(b,c);}
static void StoreSpriteCallbackInData6(struct Sprite*s,SpriteCallback cb){s->stored=cb;}
static void SetCallbackToStoredInData6(struct Sprite*s){s->callback=s->stored;}
static void DestroyAnimSprite(struct Sprite*s){s->inUse=FALSE;}
static void CalcCenterToCornerVec(struct Sprite*s,int shape,int size,int mode){abort();}
static u8 GetSpriteMatrixNum(struct Sprite*s){return 0;}
static void UpdateSpriteMatrixAnchorPos(struct Sprite*s,s32 x,s32 y){abort();}
static void CopyOamMatrix(u8 n,struct OamMatrix*m){assert(n==0);gOamMatrices[0]=*m;}
static void DummyFrameAnimation(struct Sprite*s){}
static void UnsupportedAffineLoop(u8 n,struct Sprite*s){abort();}
static const s16 quarter[]={0,402,803,1205,1605,2005,2404,2801,3196,3589,3980,4369,4756,5139,5519,5896,6269,6639,7005,7366,7723,8075,8423,8765,9102,9434,9759,10079,10393,10701,11002,11297,11585,11866,12139,12406,12665,12916,13159,13395,13622,13842,14053,14255,14449,14634,14810,14978,15136,15286,15426,15557,15678,15790,15892,15985,16069,16142,16206,16260,16305,16339,16364,16379,16384};
static s16 biosSine(u8 a){int q=a>>6,i=a&63,v=quarter[(q&1)?64-i:i];return q<2?v:-v;}
static void ObjAffineSet(const struct ObjAffineSrcData*s,struct OamMatrix*d,int count,int stride){assert(count==1&&stride==2);rotation=s->rotation;u8 a=s->rotation>>8;int sn=biosSine(a),cs=biosSine(a+64);d->a=(s->xScale*cs)>>14;d->b=-((s->xScale*sn)>>14);d->c=(s->yScale*sn)>>14;d->d=(s->yScale*cs)>>14;}
'''
 harness+=function(sources['random'],'Random')+'\nstatic u16 ObservedRandom(void){draws++;return Random();}\n#define Random ObservedRandom\n'
 harness+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'
 harness+='static void(*sAnimFuncs[])(struct Sprite*)={DummyFrameAnimation,DummyFrameAnimation};\nstatic void(*sAffineAnimFuncs[])(struct Sprite*)={ContinueAffineAnim,BeginAffineAnim};\nstatic void(*sAffineAnimCmdFuncs[])(u8,struct Sprite*)={UnsupportedAffineLoop,AffineAnimCmd_jump,AffineAnimCmd_end,AffineAnimCmd_frame};\n'
 harness+='\n'.join(funcs)+'\n#undef Random\n'+declaration(sources['battle_anim_ground'],'sAffineAnim_Bonemerang')+'\n'
 harness+='static const union AffineAnimCmd dummy[]={AFFINEANIMCMD_END};\nstatic const union AffineAnimCmd *const dummyAnims[]={dummy},*const boneAnims[]={sAffineAnim_Bonemerang};\n'
 harness+='static const s16 sonic[][5]={'+','.join('{'+','.join(map(str,a))+'}' for a in SONIC)+'};\n'
 harness+='struct BubbleConfig{u32 seed;s16 x,y;};static const struct BubbleConfig bubbles[]={'+','.join('{'+str(a[0])+'u,'+','.join(map(str,a[1:]))+'}' for a in BUBBLES)+'};\n'
 harness+=r'''
static int code(SpriteCallback cb){if(cb==StartAnimLinearTranslation)return 0;if(cb==AnimTranslateLinear_WithFollowup)return 1;if(cb==DestroyAnimSprite)return 2;if(cb==AnimSmallDriftingBubbles_Step)return 3;if(cb==AnimBonemerangProjectile_Step)return 4;if(cb==AnimBonemerangProjectile_End)return 5;abort();}
static void emit(int kind,int config,int tick){struct Sprite*s=&gSprites[0];struct OamMatrix*m=&gOamMatrices[0];int row[]={tick,s->x,s->y,s->x2,s->y2,s->inUse,code(s->callback),s->oam.tileNum,rotation,m->a,m->b,m->c,m->d,draws,gBattleAnimArgs[0],gBattleAnimArgs[1],gBattleAnimArgs[2],gBattleAnimArgs[3],s->data[0],s->data[1],s->data[2],s->data[3],s->data[4],s->data[5],s->data[6],s->data[7]};printf("%d:%d:%d:%d",kind,gBattleAnimAttacker,gBattleAnimTarget,config);for(unsigned i=0;i<sizeof(row)/sizeof(row[0]);i++)printf(",%d",row[i]);puts("");}
static void run(int kind,int actor,int target,int config){gBattleAnimAttacker=actor;gBattleAnimTarget=target;memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));memset(gSprites,0,sizeof(gSprites));memset(sAffineAnimStates,0,sizeof(sAffineAnimStates));gOamMatrices[0]=(struct OamMatrix){256,0,0,256};rotation=0;draws=0;gRngValue=0;struct Sprite*s=&gSprites[0];s->x=sX[target];s->y=sY[target];s->inUse=TRUE;s->affineAnims=dummyAnims;
 if(kind==0){memcpy(gBattleAnimArgs,sonic[config],sizeof(sonic[config]));s->callback=AnimSonicBoomProjectile;s->oam.affineMode=1;s->affineAnimBeginning=TRUE;}
 else if(kind==1){gBattleAnimArgs[0]=bubbles[config].x;gBattleAnimArgs[1]=bubbles[config].y;gRngValue=bubbles[config].seed;s->callback=AnimSmallDriftingBubbles;}
 else{s->callback=AnimBonemerangProjectile;s->oam.affineMode=1;s->affineAnimBeginning=TRUE;s->affineAnims=boneAnims;}
 AffineAnimStateReset(0);
 for(int tick=-1;tick<260;tick++){AnimateSprites();emit(kind,config,tick);if(!s->inUse){printf("R,%d:%d:%d:%d,%u\n",kind,actor,target,config,gRngValue);return;}}abort();}
int main(void){for(int kind=0;kind<3;kind++)for(int actor=0;actor<4;actor++)for(int target=0;target<4;target++)for(unsigned config=0;config<(kind==0?sizeof(sonic)/sizeof(sonic[0]):kind==1?sizeof(bubbles)/sizeof(bubbles[0]):1);config++)run(kind,actor,target,config);}
'''
 with tempfile.TemporaryDirectory(prefix='projectile-adapter-c-') as tmp:
  c=Path(tmp)/'projectiles.c';exe=Path(tmp)/'projectiles';c.write_text(harness);ref.compile_c(['cc','-std=c11','-O0',str(c),'-o',str(exe)]);out=subprocess.check_output([str(exe)],text=True)
 cases={};states={}
 for line in out.splitlines():
  if line.startswith("R,"):
   _,key,value=line.split(",");states[key]=int(value);continue
  key,*row=line.split(',');values=list(map(int,row));assert len(values)==len(COLUMNS);cases.setdefault(key,[]).append(values)
 assert len(cases)==16*(len(SONIC)+len(BUBBLES)+1)
 for key,rows in cases.items():
  kind,actor,target,config=map(int,key.split(':'));length=SONIC[config][4]+3 if kind==0 else 22 if kind==1 else 43
  assert len(rows)==length and rows[0][0]==-1 and rows[-1][5]==0 and all(r[5]==1 for r in rows[:-1]),key
  assert all(r[13]==(2 if kind==1 else 0) for r in rows),key
  if kind==2:assert rows[21][3:5]==[0,0] and rows[21][6]==5,key
 return dict(columns=COLUMNS,sonic=SONIC,bubbles=BUBBLES,cases=cases,finalStates=states)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--helper',type=Path,default=ROOT/'tests/Reference/reference_c.py');p.add_argument('--angle-adapter',type=Path,default=Path(__file__).with_name('bios_angles_adapter.c'));p.add_argument('--output',type=Path,required=True);a=p.parse_args()
 if a.output.exists():p.error('Refusing to overwrite an existing fixture; choose a new candidate path')
 data=build(a.reference,a.helper,a.angle_adapter);a.output.write_text(json.dumps(data,separators=(',',':'))+'\n');print('cases',len(data['cases']),'rows',sum(len(v) for v in data['cases'].values()))

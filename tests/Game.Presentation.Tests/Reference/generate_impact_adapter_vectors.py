#!/usr/bin/env python3
"""Extracted pinned C callbacks plus sprite frame/affine schedulers for five roots.

No product code or upstream build scripts execute. Tick -1 is the initializer
and first AnimateSprite; every later row is original AnimateSprites. Coordinates
are explicit blank 64x64 battler adapters (both singles/doubles); only BIOS angle
and ObjAffineSet are adapted. Signed Q12.4 Needle Arm accumulators remain native.
"""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
CONFIGS=[
 [[16,8,v,a] for v,a in [(160,-32),(-256,-40),(128,-16),(416,-38),(-128,-22),(-384,-31)]]+[[32767,-32768,32767,32767],[-32768,32767,-32768,-32768]],
 [[0,16,0,2,0],[0,8,1,2,0],[17,-18,0,1,7],[-17,18,1,3,42],[32767,-32768,0,1,0],[-32768,32767,1,1,0]],
 [[-16,-16],[0,0],[32767,-32768],[-32768,32767]],
 [[1,0,x,y,16] for x,y in [(0,-32),(22,-22),(30,0),(20,20),(0,28),(-19,19),(-27,0),(-18,-18),(0,-25),(17,-17),(23,0),(16,16)]]+[[1,1,x,y,10] for x,y in [(0,-24),(17,-17),(24,0),(17,17),(0,24),(-17,17),(-24,0),(-17,-17)]]+[[0,0,-300,-200,3],[0,1,32767,-32768,255],[1,0,-32768,32767,1],[0,1,12,-13,0],[0,0,0,0,1]],
 [[10,8,2,0,0,100],[85,16,6,0,3,80],[85,-16,-6,0,2,75],[-25,-32768,32767,-31,1,10],[150,0,0,226,3,0],[32767,0,0,0,0,1],[-32768,0,0,0,1,255]]]
COLUMNS=['tick','active','x','y','x2','y2','invisible','tile','hflip','vflip','priority','a','b','c','d','animEnded','affineEnded','affinePaused','arg0','arg1','arg2','arg3']+['data'+str(i) for i in range(8)]
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def function(source,name):
 m=re.search(r'^(?:static )?(?:void|s8|s16|s32|u8|u16|bool8) '+name+r'\([^;]+?\)\n\{',source,re.M)
 if not m:raise ValueError('Missing source function '+name)
 i=m.end();depth=1
 while depth:depth+=(source[i]=='{')-(source[i]=='}');i+=1
 return source[m.start():i]
def declaration(source,name):
 m=re.search(r'^(?:static )?const (?:union (?:AffineAnimCmd|AnimCmd)|s16) '+name+r'\[\]\s*=\s*\{.*?\};',source,re.M|re.S)
 if not m:raise ValueError('Missing source declaration '+name)
 return m[0]
def build(root):
 ref=module('reference_c',root/'tests/Reference/reference_c.py');reference=root/'content/raw/pokefirered/037335f4';ref.verify_reference(reference)
 src={name:(reference/('src/'+name+'.c')).read_text() for name in ['sprite','battle_anim_mons','battle_anim_fight','battle_anim_effects_1','battle_anim_effects_3','trig']}
 header=(reference/'include/sprite.h').read_text()
 # Revision-bound source hashes belong to the oracle, never runtime data.
 expected={'src/sprite.c': '2a804302eb5a89d31c3ec2f33dc645a80a0c645113d4b33b9bd571064c0286ae', 'src/battle_anim_mons.c': 'fcb002acc48b8cd6b8cf8b7eddcb3034d721ff5887eb6bdeedfe183b7b3908bb', 'src/battle_anim_fight.c': '343a674d3230233af2ecd681c104f550b13e51485b011534c1d60d68ba65f5f5', 'src/battle_anim_effects_1.c': '80de93553f73982ca5b1abab6af952fe96e0b4763fefddf8aa1dc5071ad20682', 'src/battle_anim_effects_3.c': 'bdf8ba956af35ca232d7e5101090e2ff31a35f0ae91286a21b216e7787ba6c1f', 'src/trig.c': '4144065a90e5376ce0259796a01ebaae33c6cc6c7ef716de95b9d9a13c85a810', 'include/sprite.h': 'a77aa1c837dfb58cf60b3eac299c7cc29a23ce27735ba710b9e8eeed102bf773'}
 for path,digest in expected.items():assert hashlib.sha256((reference/path).read_bytes()).hexdigest()==digest,path
 names=['AnimateSprites','AnimateSprite','BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','DecrementAnimDelayCounter','BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame','AffineAnimStateRestartAnim','AffineAnimStateReset','ApplyAffineAnimFrameAbsolute','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame','ApplyAffineAnimFrame','StartSpriteAffineAnim','AffineAnimStateStartAnim','StartSpriteAnim']
 funcs=[function(src['sprite'],n) for n in names]
 funcs += [function(src['battle_anim_mons'],n) for n in ['InitSpritePosToAnimTarget','SetAnimSpriteInitialXOffset','TrySetSpriteRotScale','ShouldRotScaleSpeciesBeFlipped']]
 funcs += [function(src['battle_anim_fight'],'AnimDizzyPunchDuck')]
 funcs += [function(src['battle_anim_effects_1'],n) for n in ['AnimConstrictBinding','AnimConstrictBinding_Step1','AnimConstrictBinding_Step2','AnimNeedleArmSpike','AnimNeedleArmSpike_Step','AnimFrenzyPlantRoot','AnimRootFlickerOut']]
 funcs += [function(src['battle_anim_effects_3'],n) for n in ['AnimKnockOffStrike','AnimKnockOffStrike_Step']]
 funcs += [function(src['trig'],n) for n in ['Sin','Cos']]
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
#define B_SIDE_PLAYER 0
#define ANIM_TARGET 1
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define SAFE_DIV(a,b) (assert((b)!=0),(a)/(b))
#define sAnchorX data[6]
#define sAnchorY data[7]
#define Q_8_8(x) ((s16)((x)*256))
_Static_assert((-1>>1)==-1,"Source signed arithmetic shift required");
'''
 h+=header[header.index('struct AnimFrameCmd\n'):header.index('\nenum\n')]+declaration(src['trig'],'gSineTable')+'\n'+(root/'tests/Game.Presentation.Tests/Reference/bios_angles_adapter.c').read_text()+r'''
struct Sprite;typedef void(*SpriteCallback)(struct Sprite*);
struct Sprite {
 u8 subpriority,animCmdIndex,animLoopCounter,animNum;u8 animDelayCounter:6;
 bool8 invisible,inUse,animEnded,animBeginning,animPaused,usingSheet,hFlip,vFlip;
 bool8 affineAnimBeginning,affineAnimEnded,affineAnimPaused,anchored;
 int sheetTileStart,images;s16 x,y,x2,y2,data[8];
 struct {int affineMode,tileNum,matrixNum,shape,size;}oam;
 const union AnimCmd *const *anims;const union AffineAnimCmd *const *affineAnims;SpriteCallback callback;
};
struct ObjAffineSrcData{s16 xScale,yScale;u16 rotation;};struct OamMatrix{s16 a,b,c,d;};
static struct Sprite gSprites[1];static struct AffineAnimState sAffineAnimStates[1];static struct OamMatrix gOamMatrices[1];static bool8 gAffineAnimsDisabled;
static u8 gBattleAnimAttacker,gBattleAnimTarget;static s16 gBattleAnimArgs[8];static int mode;
static struct{s16 startX,startY,targetX,targetY;}sFrenzyPlantRootData;
static const s16 X[2][4]={{72,176,48,112},{32,200,90,152}},Y[2][4]={{80,40,40,80},{80,40,88,32}};
static bool8 IsContest(void){return FALSE;}
static u8 GetBattlerSide(u8 b){assert(b<4);return b&1;}
static s16 GetBattlerSpriteCoord(u8 b,u8 c){assert(b<4&&c<4);return(c==0||c==2)?X[mode][b]:Y[mode][b]+(c==3&&!(b&1)?8:0);}
static s16 GetBattlerSpriteCoord2(u8 b,u8 c){return GetBattlerSpriteCoord(b,c);}
static u8 GetAnimBattlerSpriteId(u8 b){assert(b==ANIM_TARGET);return 0;}
static void DestroyAnimSprite(struct Sprite*s){s->inUse=FALSE;}
static void DestroySpriteAndMatrix(struct Sprite*s){s->inUse=FALSE;}
static void SetSpriteOamFlipBits(struct Sprite*s,u8 h,u8 v){s->hFlip=h;s->vFlip=v;}
static void RequestSpriteFrameImageCopy(int a,int b,int c){abort();}
static void CalcCenterToCornerVec(struct Sprite*s,int shape,int size,int mode){abort();}
static u8 GetSpriteMatrixNum(struct Sprite*s){return 0;}
static void UpdateSpriteMatrixAnchorPos(struct Sprite*s,s32 x,s32 y){abort();}
static void CopyOamMatrix(u8 n,struct OamMatrix*m){assert(n==0);gOamMatrices[0]=*m;}
static void UnsupportedAffineLoop(u8 n,struct Sprite*s){abort();}
static const s16 quarter[]={0,402,803,1205,1605,2005,2404,2801,3196,3589,3980,4369,4756,5139,5519,5896,6269,6639,7005,7366,7723,8075,8423,8765,9102,9434,9759,10079,10393,10701,11002,11297,11585,11866,12139,12406,12665,12916,13159,13395,13622,13842,14053,14255,14449,14634,14810,14978,15136,15286,15426,15557,15678,15790,15892,15985,16069,16142,16206,16260,16305,16339,16364,16379,16384};
static s16 biosSine(u8 a){int q=a>>6,i=a&63,v=quarter[(q&1)?64-i:i];return q<2?v:-v;}
static void ObjAffineSet(const struct ObjAffineSrcData*s,struct OamMatrix*d,int count,int stride){assert(count==1&&stride==2);u8 a=s->rotation>>8;int sn=biosSine(a),cs=biosSine(a+64);d->a=(s->xScale*cs)>>14;d->b=-((s->xScale*sn)>>14);d->c=(s->yScale*sn)>>14;d->d=(s->yScale*cs)>>14;}
'''
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'
 h+='static void(*sAnimFuncs[])(struct Sprite*)={ContinueAnim,BeginAnim};\nstatic void(*sAnimCmdFuncs[])(struct Sprite*)={AnimCmd_loop,AnimCmd_jump,AnimCmd_end,AnimCmd_frame};\nstatic void(*sAffineAnimFuncs[])(struct Sprite*)={ContinueAffineAnim,BeginAffineAnim};\nstatic void(*sAffineAnimCmdFuncs[])(u8,struct Sprite*)={UnsupportedAffineLoop,AffineAnimCmd_jump,AffineAnimCmd_end,AffineAnimCmd_frame};\n'
 h+='\n'.join(funcs)+'\n'
 for source,names in [('battle_anim_effects_1',['sAnim_ConstrictBinding','sAnim_ConstrictBinding_Flipped','sAffineAnim_ConstrictBinding','sAffineAnim_ConstrictBinding_Flipped','sIngrainRootAnimCmds1','sIngrainRootAnimCmds2','sIngrainRootAnimCmds3','sIngrainRootAnimCmds4']),('battle_anim_effects_3',['sKnockOffStrikeAnimCmds','sKnockOffStrikeAffineanimCmds1','sKnockOffStrikeAffineanimCmds2'])]:
  h+='\n'.join(declaration(src[source],n) for n in names)+'\n'
 h+=r'''
static const union AnimCmd dummyFrame[]={ANIMCMD_FRAME(0,1),ANIMCMD_JUMP(0)};
static const union AffineAnimCmd dummyAffine[]={AFFINEANIMCMD_END};
static const union AnimCmd *const dummyFrames[]={dummyFrame},*const constrictFrames[]={sAnim_ConstrictBinding,sAnim_ConstrictBinding_Flipped},*const knockFrames[]={sKnockOffStrikeAnimCmds},*const rootFrames[]={sIngrainRootAnimCmds1,sIngrainRootAnimCmds2,sIngrainRootAnimCmds3,sIngrainRootAnimCmds4};
static const union AffineAnimCmd *const dummyAffines[]={dummyAffine},*const constrictAffines[]={sAffineAnim_ConstrictBinding,sAffineAnim_ConstrictBinding_Flipped},*const knockAffines[]={sKnockOffStrikeAffineanimCmds1,sKnockOffStrikeAffineanimCmds2};
'''
 for k,configs in enumerate(CONFIGS):h+='static const s16 config'+str(k)+'[][8]={'+','.join('{'+','.join(map(str,a))+'}' for a in configs)+'};\n'
 h+='static const s16 (*configs[])[8]={config0,config1,config2,config3,config4};\nstatic const int counts[]={'+','.join(str(len(c)) for c in CONFIGS)+'};\n'
 h+=r'''
static void emit(int kind,int config,int tick){struct Sprite*s=&gSprites[0];struct OamMatrix*m=&gOamMatrices[0];int row[]={tick,s->inUse,s->x,s->y,s->x2,s->y2,s->invisible,s->oam.tileNum,s->hFlip,s->vFlip,s->subpriority,m->a,m->b,m->c,m->d,s->animEnded,s->affineAnimEnded,s->affineAnimPaused,gBattleAnimArgs[0],gBattleAnimArgs[1],gBattleAnimArgs[2],gBattleAnimArgs[3]};printf("%d:%d:%d:%d:%d",kind,mode,gBattleAnimAttacker,gBattleAnimTarget,config);for(unsigned i=0;i<sizeof(row)/sizeof(row[0]);i++)printf(",%d",row[i]);for(int i=0;i<8;i++)printf(",%d",s->data[i]);puts("");}
static void run(int kind,int actor,int target,int config){gBattleAnimAttacker=actor;gBattleAnimTarget=target;memcpy(gBattleAnimArgs,configs[kind][config],sizeof(gBattleAnimArgs));memset(gSprites,0,sizeof(gSprites));memset(sAffineAnimStates,0,sizeof(sAffineAnimStates));gOamMatrices[0]=(struct OamMatrix){256,0,0,256};struct Sprite*s=&gSprites[0];s->x=GetBattlerSpriteCoord(target,2);s->y=GetBattlerSpriteCoord(target,3);s->inUse=TRUE;s->usingSheet=TRUE;s->animBeginning=TRUE;s->anims=dummyFrames;s->affineAnims=dummyAffines;s->subpriority=(int[]){30,40,20,50}[target]-2;
 if(kind==0)s->callback=AnimDizzyPunchDuck;
 if(kind==1){s->callback=AnimConstrictBinding;s->anims=constrictFrames;s->affineAnims=constrictAffines;s->oam.affineMode=1;s->affineAnimBeginning=TRUE;}
 if(kind==2){s->callback=AnimKnockOffStrike;s->anims=knockFrames;s->affineAnims=knockAffines;s->oam.affineMode=1;s->affineAnimBeginning=TRUE;}
 if(kind==3){s->callback=AnimNeedleArmSpike;s->oam.affineMode=1;s->affineAnimBeginning=TRUE;}
 if(kind==4){s->callback=AnimFrenzyPlantRoot;s->anims=rootFrames;}
 AffineAnimStateReset(0);
 for(int tick=-1;tick<400;tick++){if(kind==1&&tick==configs[kind][config][4])gBattleAnimArgs[7]=-1;AnimateSprites();emit(kind,config,tick);if(tick>=0&&!s->inUse)return;}abort();}
int main(void){for(int k=0;k<5;k++)for(mode=0;mode<2;mode++)for(int a=0;a<(mode?4:2);a++)for(int t=0;t<(mode?4:2);t++)for(int c=0;c<counts[k];c++)run(k,a,t,c);}
'''
 with tempfile.TemporaryDirectory(prefix='impact-source-c-') as tmp:
  c=Path(tmp)/'impact.c';exe=Path(tmp)/'impact';c.write_text(h);ref.compile_c(['cc','-std=c11','-O0',str(c),'-o',str(exe)]);out=subprocess.check_output([str(exe)],text=True)
 cases={}
 for line in out.splitlines():
  key,*row=line.split(',');values=list(map(int,row));assert len(values)==len(COLUMNS);cases.setdefault(key,[]).append(values)
 assert len(cases)==20*sum(map(len,CONFIGS))
 for key,rows in cases.items():
  k,mode,actor,target,config=map(int,key.split(':'));assert rows[0][0]==-1 and rows[-1][1]==0,key
  assert all(r[1]==1 for r in rows[:-1]) or k==3 and CONFIGS[k][config][4]==0,key
  if k==0:assert rows[-1][0]==40,key
  if k==2:assert rows[-1][0]==8,key
  if k==3:assert rows[-1][0]==CONFIGS[k][config][4],key
  if k==4:assert rows[-1][0]==CONFIGS[k][config][5],key
 return dict(columns=COLUMNS,configs=CONFIGS,cases=cases)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,default=ROOT);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
 if a.output.exists():p.error('Refusing to overwrite an existing fixture; choose a new candidate path')
 result=build(a.root);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');print('cases',len(result['cases']),'rows',sum(map(len,result['cases'].values())))

#!/usr/bin/env python3
"""Pinned fire/Leech Seed C callback, frame and affine oracle.

Extracts original callbacks, shared horizontal-arc/wait helpers and sprite.c
players verbatim. Only host callback-pointer storage, explicit battler geometry
and ObjAffineSet are adapted. Tick -1 is CreateSpriteAndAnimate. Source art is
not executed and no upstream build scripts run. Both singles/doubles include
nonzero picture offsets/elevations to distinguish unadjusted destinations.
"""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
CONFIGS=[[[-32, 0, 50, 5, -2, 0], [20, -10, 50, 5, 1, -1], [0, 16, 50, 5, 0, 1], [-96, 0, 24, 0, 0, 0], [0, 0, 1, 1, 1, 1], [17, -18, 7, 4, -3, 2], [32767, -32768, 7, 32767, 32767, -32768], [-32768, 32767, 7, -32768, -32768, 32767]], [[-16, 0, 70, 16, 0, 1], [0, 0, 70, 16, 0, 1], [16, 0, 80, 16, 0, 1], [0, 0, 1, 1, 1, 1], [17, -18, 7, 4, -3, 2], [32767, -32768, 7, 32767, 32767, -32768], [-32768, 32767, 7, -32768, -32768, 32767]], [[0, 0, 30, 30, -1, 0], [0, 0, 30, 30, 0, 1], [0, 0, 30, 30, -1, -1], [0, 0, 30, 30, 2, 1], [0, 0, 30, 30, 1, -1], [0, 0, 30, 30, -1, 1], [0, 0, 30, 30, 1, -2], [0, 0, 30, 30, 3, 1], [0, 0, 1, 1, 1, 1], [17, -18, 7, 4, -3, 2], [32767, -32768, 7, 32767, 32767, -32768], [-32768, 32767, 7, -32768, -32768, 32767]], [[15, 0, 0, 24, 35, -32], [15, 0, -16, 24, 35, -40], [15, 0, 16, 24, 35, -37], [0, 0, 0, 0, 1, 0], [127, -128, -128, 127, 255, -128], [32767, -32768, -32768, 32767, 7, 32767], [-32768, 32767, 32767, -32768, 7, -32768]]]
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
def build(root,emit_source=None):
 ref=module('reference_c',root/'tests/Reference/reference_c.py');reference=root/'content/raw/pokefirered/037335f4';ref.verify_reference(reference)
 src={name:(reference/('src/'+name+'.c')).read_text() for name in ['sprite','battle_anim_mons','battle_anim_fire','battle_anim_effects_1','trig']}
 header=(reference/'include/sprite.h').read_text()
 # Revision-bound source hashes belong to the oracle, never runtime data.
 expected={'src/sprite.c': '2a804302eb5a89d31c3ec2f33dc645a80a0c645113d4b33b9bd571064c0286ae', 'src/battle_anim_mons.c': 'fcb002acc48b8cd6b8cf8b7eddcb3034d721ff5887eb6bdeedfe183b7b3908bb', 'src/battle_anim_fire.c': '369439a2956832942756db1afc5b9ca8955762b7af121ff4b4aeda156e07ede6', 'src/battle_anim_effects_1.c': '80de93553f73982ca5b1abab6af952fe96e0b4763fefddf8aa1dc5071ad20682', 'src/trig.c': '4144065a90e5376ce0259796a01ebaae33c6cc6c7ef716de95b9d9a13c85a810', 'include/sprite.h': 'a77aa1c837dfb58cf60b3eac299c7cc29a23ce27735ba710b9e8eeed102bf773', 'include/battle_anim.h': '15d0a815fec54ed669e1cf7130d323070d799ecfba0f3a935da4b9c28d6f09a7'}
 for path,digest in expected.items():assert hashlib.sha256((reference/path).read_bytes()).hexdigest()==digest,path
 names=['AnimateSprites','AnimateSprite','BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','DecrementAnimDelayCounter','BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame','AffineAnimStateRestartAnim','AffineAnimStateReset','ApplyAffineAnimFrameAbsolute','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame','ApplyAffineAnimFrame','StartSpriteAffineAnim','AffineAnimStateStartAnim','StartSpriteAnim']
 funcs=[function(src['sprite'],n) for n in names]
 funcs += [function(src['battle_anim_mons'],n) for n in ['InitSpritePosToAnimAttacker','SetAnimSpriteInitialXOffset','SetSpriteCoordsToAnimAttackerCoords','InitAnimLinearTranslation','InitAnimArcTranslation','TranslateAnimHorizontalArc','AnimTranslateLinear','WaitAnimForDuration']]
 funcs += [function(src['battle_anim_fire'],n) for n in ['AnimFirePlume','AnimLargeFlame','AnimLargeFlame_Step']]
 funcs += [function(src['battle_anim_effects_1'],n) for n in ['AnimLeechSeed','AnimLeechSeed_Step','AnimLeechSeedSprouts']]
 funcs += [function(src['trig'],'Sin')]
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
 h+=header[header.index('struct AnimFrameCmd\n'):header.index('\nenum\n')]+declaration(src['trig'],'gSineTable')+'\n'+'\n'.join(re.findall(r'^#define sTransl_.*$',(reference/'include/battle_anim.h').read_text(),re.M))+r'''
struct Sprite;typedef void(*SpriteCallback)(struct Sprite*);
struct Sprite {
 u8 subpriority,animCmdIndex,animLoopCounter,animNum;u8 animDelayCounter:6;
 bool8 invisible,inUse,animEnded,animBeginning,animPaused,usingSheet,hFlip,vFlip;
 bool8 affineAnimBeginning,affineAnimEnded,affineAnimPaused,anchored;
 int sheetTileStart,images;s16 x,y,x2,y2,data[8];
 struct {int affineMode,tileNum,matrixNum,shape,size;}oam;
 const union AnimCmd *const *anims;const union AffineAnimCmd *const *affineAnims;SpriteCallback callback,stored;
};
struct ObjAffineSrcData{s16 xScale,yScale;u16 rotation;};struct OamMatrix{s16 a,b,c,d;};
static struct Sprite gSprites[1];static struct AffineAnimState sAffineAnimStates[1];static struct OamMatrix gOamMatrices[1];static bool8 gAffineAnimsDisabled;
static u8 gBattleAnimAttacker,gBattleAnimTarget;static s16 gBattleAnimArgs[8];static int mode;

static const s16 X[2][4]={{72,176,48,112},{32,200,90,152}},Y[2][4]={{80,40,40,80},{80,40,88,32}};
static bool8 IsContest(void){return FALSE;}
static u8 GetBattlerSide(u8 b){assert(b<4);return b&1;}
static s16 GetBattlerSpriteCoord(u8 b,u8 c){assert(b<4&&c<4);if(c==0||c==2)return X[mode][b];if(c==1)return Y[mode][b];int y=Y[mode][b]+(int[]){5,11,7,13}[b]-((b&1)?(int[]){2,3,4,5}[b]:0);y&=255;y+=!(b&1)?8:0;return y>104?104:y;}
static s16 GetBattlerSpriteCoord2(u8 b,u8 c){return GetBattlerSpriteCoord(b,c);}
static void StoreSpriteCallbackInData6(struct Sprite*s,SpriteCallback cb){s->stored=cb;}
static void SetCallbackToStoredInData6(struct Sprite*s){s->callback=s->stored;}
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
 for source,names in [('battle_anim_fire',['sAnim_LargeFlame','sAnim_FirePlume','sAffineAnim_LargeFlame']),('battle_anim_effects_1',['sLeechSeedAnimCmds1','sLeechSeedAnimCmds2'])]:
  h+='\n'.join(declaration(src[source],n) for n in names)+'\n'
 h+=r'''
static const union AffineAnimCmd dummyAffine[]={AFFINEANIMCMD_END};
static const union AnimCmd *const plumeFrames[]={sAnim_FirePlume},*const fireFrames[]={sAnim_LargeFlame},*const seedFrames[]={sLeechSeedAnimCmds1,sLeechSeedAnimCmds2};
static const union AffineAnimCmd *const dummyAffines[]={dummyAffine},*const fireAffines[]={sAffineAnim_LargeFlame};
'''
 for k,configs in enumerate(CONFIGS):h+='static const s16 config'+str(k)+'[][8]={'+','.join('{'+','.join(map(str,a))+'}' for a in configs)+'};\n'
 h+='static const s16 (*configs[])[8]={config0,config1,config2,config3};\nstatic const int counts[]={'+','.join(str(len(c)) for c in CONFIGS)+'};\n'
 h+=r'''
static void emit(int kind,int config,int tick){struct Sprite*s=&gSprites[0];struct OamMatrix*m=&gOamMatrices[0];int row[]={tick,s->inUse,s->x,s->y,s->x2,s->y2,s->invisible,s->oam.tileNum,s->hFlip,s->vFlip,s->subpriority,m->a,m->b,m->c,m->d,s->animEnded,s->affineAnimEnded,s->affineAnimPaused,gBattleAnimArgs[0],gBattleAnimArgs[1],gBattleAnimArgs[2],gBattleAnimArgs[3]};printf("%d:%d:%d:%d:%d",kind,mode,gBattleAnimAttacker,gBattleAnimTarget,config);for(unsigned i=0;i<sizeof(row)/sizeof(row[0]);i++)printf(",%d",row[i]);for(int i=0;i<8;i++)printf(",%d",s->data[i]);puts("");}
static void run(int kind,int actor,int target,int config){gBattleAnimAttacker=actor;gBattleAnimTarget=target;memcpy(gBattleAnimArgs,configs[kind][config],sizeof(gBattleAnimArgs));memset(gSprites,0,sizeof(gSprites));memset(sAffineAnimStates,0,sizeof(sAffineAnimStates));gOamMatrices[0]=(struct OamMatrix){256,0,0,256};struct Sprite*s=&gSprites[0];s->x=GetBattlerSpriteCoord(target,2);s->y=GetBattlerSpriteCoord(target,3);s->inUse=TRUE;s->usingSheet=TRUE;s->animBeginning=TRUE;s->anims=fireFrames;s->affineAnims=dummyAffines;s->subpriority=(int[]){30,40,20,50}[target]-2;
 if(kind==0){s->callback=AnimFirePlume;s->anims=plumeFrames;}
 if(kind==1){s->callback=AnimLargeFlame;s->affineAnims=fireAffines;s->oam.affineMode=1;s->affineAnimBeginning=TRUE;}
 if(kind==2)s->callback=AnimLargeFlame;
 if(kind==3){s->callback=AnimLeechSeed;s->anims=seedFrames;}
 AffineAnimStateReset(0);
 for(int tick=-1;tick<400;tick++){AnimateSprites();emit(kind,config,tick);if(tick>=0&&!s->inUse)return;}abort();}
int main(void){for(int k=0;k<4;k++)for(mode=0;mode<2;mode++)for(int a=0;a<(mode?4:2);a++)for(int t=0;t<(mode?4:2);t++)for(int c=0;c<counts[k];c++)run(k,a,t,c);}

'''
 if emit_source is not None:
  emit_source.write_text(h);return None
 with tempfile.TemporaryDirectory(prefix='fire-seed-source-c-') as tmp:
  c=Path(tmp)/'fire_seed.c';exe=Path(tmp)/'fire_seed';c.write_text(h);ref.compile_c(['cc','-std=c11','-O0',str(c),'-o',str(exe)]);out=subprocess.check_output([str(exe)],text=True)
 cases={}
 for line in out.splitlines():
  key,*row=line.split(',');values=list(map(int,row));assert len(values)==len(COLUMNS);cases.setdefault(key,[]).append(values)
 assert len(cases)==20*sum(map(len,CONFIGS))
 for key,rows in cases.items():
  k,mode,actor,target,config=map(int,key.split(':'));assert rows[0][0]==-1 and rows[-1][1]==0,key
  assert all(r[1]==1 for r in rows[:-1]),key
  expected_last=CONFIGS[k][config][4]+74 if k==3 else CONFIGS[k][config][2]-1
  assert rows[-1][0]==expected_last,key
  if k==3:
   duration=CONFIGS[k][config][4]
   assert all(r[6]==int(duration<=r[0]<=duration+11) for r in rows[:-1]),key

 return dict(columns=COLUMNS,configs=CONFIGS,cases=cases)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,default=ROOT);p.add_argument('--output',type=Path);p.add_argument('--emit-source',type=Path);a=p.parse_args()
 if bool(a.output)==bool(a.emit_source):p.error('Choose exactly one of --output or --emit-source')
 if a.emit_source:
  if a.emit_source.exists():p.error('Refusing to overwrite source candidate')
  build(a.root,a.emit_source);raise SystemExit(0)
 if a.output.exists():p.error('Refusing to overwrite an existing fixture; choose a new candidate path')
 result=build(a.root);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');print('cases',len(result['cases']),'rows',sum(map(len,result['cases'].values())))

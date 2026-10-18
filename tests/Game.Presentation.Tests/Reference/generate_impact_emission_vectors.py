#!/usr/bin/env python3
"""Pinned physical-impact callbacks plus real source sprite/frame/affine allocation.

Only battler coordinates, loaded sheet lookup, host callback-pointer storage,
OAM geometry not observed by these callbacks, and BIOS math are adapted. The
original allocation, immediate-create, sprite traversal, affine allocation,
counting, movement callbacks and task bodies are extracted verbatim. OAM rows
precede the task phase, while counts/arguments follow it, matching BattleMainCB2.
"""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
SOURCE_SHA256={'src/sprite.c': '2a804302eb5a89d31c3ec2f33dc645a80a0c645113d4b33b9bd571064c0286ae', 'src/battle_anim.c': '5883d9ee0483120ef67461952f5880499f322fb1c706cc213ca13c0c2b0e88c5', 'src/battle_anim_mons.c': 'fcb002acc48b8cd6b8cf8b7eddcb3034d721ff5887eb6bdeedfe183b7b3908bb', 'src/battle_anim_fight.c': '343a674d3230233af2ecd681c104f550b13e51485b011534c1d60d68ba65f5f5', 'src/battle_anim_normal.c': '73478ff40f601740b58408caa3259f5abc29f90010f77017aecc69d71803a6c3', 'src/battle_anim_flying.c': 'd51ff92894cbed0dd2c2434e73ed03bb2f2f20d930561f55fb0e31df4568ab0a', 'src/battle_anim_effects_2.c': '50561e4882a0f8813f305af6f4147ed1c394657278e32aae6c733bef31994a04', 'src/battle_anim_effects_3.c': 'bdf8ba956af35ca232d7e5101090e2ff31a35f0ae91286a21b216e7787ba6c1f', 'src/trig.c': '4144065a90e5376ce0259796a01ebaae33c6cc6c7ef716de95b9d9a13c85a810', 'include/sprite.h': 'a77aa1c837dfb58cf60b3eac299c7cc29a23ce27735ba710b9e8eeed102bf773', 'include/battle_anim.h': '15d0a815fec54ed669e1cf7130d323070d799ecfba0f3a935da4b9c28d6f09a7', 'data/battle_anim_scripts.s': 'f1566a2d43fad88619f4e3799f995bfd1c53b65fe8de4c4a91d66a440284ea9e'}
CONFIGS=[[[0,0,0],[0,0,1],[17,-18,0],[-32768,32767,1]],[[0,0,1,20],[0,0,1,36],[17,-18,0,0],[-32768,32767,0,2]],[[20,0,0,0,1152],[17,-18,13,14,256],[-48,8,32,-16,512]],[[]],[[]],[[]]]
SPRITE_COLUMNS=['slot','x','y','x2','y2','invisible','tile','hflip','vflip','subpriority','a','b','c','d','affine']
COLUMNS=['tick','count','task','matrices_before','matrices_after','arg0','arg1','arg2','arg3','arg4']
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
 ref=module('reference_c',root/'tests/Reference/reference_c.py');raw=root/'content/raw/pokefirered/037335f4';ref.verify_reference(raw)
 for name,digest in SOURCE_SHA256.items():
  if hashlib.sha256((raw/name).read_bytes()).hexdigest()!=digest:raise ValueError('Pinned impact emission source mismatch: '+name)
 names=['sprite','battle_anim','battle_anim_mons','battle_anim_fight','battle_anim_normal','battle_anim_flying','battle_anim_effects_2','battle_anim_effects_3','trig'];src={n:(raw/('src/'+n+'.c')).read_text() for n in names}
 header=(raw/'include/sprite.h').read_text();battle=(raw/'include/battle_anim.h').read_text()
 sprite_names=['AnimateSprites','CreateSprite','CreateSpriteAt','CreateSpriteAndAnimate','DestroySprite','ResetSprite','SpriteCallbackDummy','SetSpriteSheetFrameTileNum','AnimateSprite','BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','DecrementAnimDelayCounter','BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame','AffineAnimStateRestartAnim','AffineAnimStateReset','ApplyAffineAnimFrameAbsolute','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame','ApplyAffineAnimFrame','StartSpriteAffineAnim','AffineAnimStateStartAnim','SetSpriteOamFlipBits','GetSpriteMatrixNum','AllocOamMatrix','FreeOamMatrix','FreeSpriteOamMatrix','InitSpriteAffineAnim']
 funcs=[function(src['sprite'],n) for n in sprite_names]
 funcs += [function(src['battle_anim'],n) for n in ['DestroyAnimSprite','DestroyAnimVisualTask']]
 funcs += [function(src['battle_anim_mons'],n) for n in ['InitSpritePosToAnimAttacker','InitSpritePosToAnimTarget','SetAnimSpriteInitialXOffset','SetSpriteCoordsToAnimAttackerCoords','InitAnimLinearTranslation','StartAnimLinearTranslation','AnimTranslateLinear','AnimTranslateLinear_WithFollowup','InitAnimLinearTranslationWithSpeed','InitAnimLinearTranslationWithSpeedAndPos','InitAnimArcTranslation','TranslateAnimHorizontalArc','TrySetSpriteRotScale','ShouldRotScaleSpeciesBeFlipped','WaitAnimForDuration']]
 funcs += [function(src['battle_anim_fight'],n) for n in ['AnimCrossChopHand','AnimCrossChopHand_Step']]
 funcs += [function(src['battle_anim_normal'],n) for n in ['AnimCrossImpact','AnimFlashingHitSplat','AnimFlashingHitSplat_Step']]
 funcs += [function(src['battle_anim_effects_2'],n) for n in ['AnimCoinThrow','AnimFallingCoin','AnimFallingCoin_Step']]
 funcs += [function(src['battle_anim_effects_3'],n) for n in ['AnimTask_BarrageBall','AnimTask_BarrageBall_Step']]
 funcs += [function(src['battle_anim_flying'],'AnimTask_DrillPeckHitSplats')]
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
#define MAX_SPRITES 64
#define OAM_MATRIX_COUNT 32
#define ST_OAM_AFFINE_ON_MASK 1
#define ST_OAM_AFFINE_OFF 0
#define B_SIDE_PLAYER 0
#define B_SIDE_OPPONENT 1
#define ANIM_TARGET 1
#define ANIM_ATTACKER 0
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define BATTLER_COORD_ATTR_HEIGHT 0
#define SAFE_DIV(a,b) (assert((b)!=0),(a)/(b))
#define sAnchorX data[6]
#define sAnchorY data[7]
#define Q_8_8(x) ((s16)((x)*256))
#define TILE_SIZE_4BPP 32
#define TAG_NONE 65535
#define FREE_SPRITE_TILE(i) abort()
_Static_assert((-1>>1)==-1,"Source signed arithmetic shift required");
'''
 h+=header[header.index('struct AnimFrameCmd\n'):header.index('\nenum\n')]+declaration(src['trig'],'gSineTable')+'\n'+'\n'.join(re.findall(r'^#define sTransl_.*$',battle,re.M))+'\n'
 h+=(root/'tests/Game.Presentation.Tests/Reference/bios_angles_adapter.c').read_text()
 h+=r'''
struct Sprite;struct SpriteTemplate;typedef void(*SpriteCallback)(struct Sprite*);
struct OamData {u8 affineMode,matrixNum,shape,size,paletteNum;u16 tileNum;};
struct SpriteFrameImage{int size;};
struct Sprite {
 u8 subpriority,animCmdIndex,animLoopCounter,animNum;u8 animDelayCounter:6;
 bool8 invisible,inUse,animEnded,animBeginning,animPaused,usingSheet,hFlip,vFlip;
 bool8 affineAnimBeginning,affineAnimEnded,affineAnimPaused,anchored;
 int sheetTileStart;s16 x,y,x2,y2,data[8];struct OamData oam;
 const struct SpriteFrameImage*images;const struct SpriteTemplate*template;
 const union AnimCmd *const *anims;const union AffineAnimCmd *const *affineAnims;SpriteCallback callback,stored;
};
struct SpriteTemplate{u16 tileTag,paletteTag;const struct OamData*oam;const union AnimCmd *const *anims;const struct SpriteFrameImage*images;const union AffineAnimCmd *const *affineAnims;SpriteCallback callback;};
struct Task{bool8 isActive;void(*func)(u8);s16 data[16];};
struct ObjAffineSrcData{s16 xScale,yScale;u16 rotation;};struct OamMatrix{s16 a,b,c,d;};
static struct Sprite gSprites[65],sDummySprite;static struct Task gTasks[1];static struct AffineAnimState sAffineAnimStates[32];static struct OamMatrix gOamMatrices[32];static bool8 gAffineAnimsDisabled;
static u32 gOamMatrixAllocBitmap;static u8 gBattleAnimAttacker,gBattleAnimTarget,gAnimVisualTaskCount;static s16 gBattleAnimArgs[8];static int mode,slots;
static const s16 X[2][4]={{72,176,48,112},{32,200,90,152}},Y[2][4]={{80,40,40,80},{80,40,88,32}};
static bool8 IsContest(void){return FALSE;}
static u8 GetBattlerSide(u8 b){assert(b<4);return b&1;}
static s16 GetBattlerSpriteCoord(u8 b,u8 c){assert(b<4&&c<4);if(c==0||c==2)return X[mode][b];if(c==1)return Y[mode][b];int y=Y[mode][b]+(int[]){5,11,7,13}[b]-((b&1)?(int[]){2,3,4,5}[b]:0);y&=255;y+=!(b&1)?8:0;return y>104?104:y;}
static s16 GetBattlerSpriteCoord2(u8 b,u8 c){return GetBattlerSpriteCoord(b,c);}
static u8 GetBattlerSpriteSubpriority(u8 b){return (u8[]){30,40,20,50}[b];}
static s16 GetBattlerSpriteCoordAttr(u8 b,u8 attr){assert(attr==0);return (s16[]){64,48,32,56}[b];}
static void StoreSpriteCallbackInData6(struct Sprite*s,SpriteCallback cb){s->stored=cb;}
static void SetCallbackToStoredInData6(struct Sprite*s){s->callback=s->stored;}
static void DestroyTask(u8 n){assert(n==0);gTasks[n].isActive=FALSE;}
static void RequestSpriteFrameImageCopy(int a,int b,const struct SpriteFrameImage*c){abort();}
static void CalcCenterToCornerVec(struct Sprite*s,int shape,int size,int mode){}
static void UpdateSpriteMatrixAnchorPos(struct Sprite*s,s32 x,s32 y){abort();}
static s16 AllocSpriteTiles(u8 n){abort();}
static u16 GetSpriteTileStartByTag(u16 tag){return 0;}
static u8 IndexOfSpritePaletteTag(u16 tag){return 0;}
static void CopyOamMatrix(u8 n,struct OamMatrix*m){assert(n<32);gOamMatrices[n]=*m;}
static void SetOamMatrix(u8 n,s16 a,s16 b,s16 c,s16 d){assert(n<32);gOamMatrices[n]=(struct OamMatrix){a,b,c,d};}
static void UnsupportedAffineLoop(u8 n,struct Sprite*s){abort();}
static const s16 quarter[]={0,402,803,1205,1605,2005,2404,2801,3196,3589,3980,4369,4756,5139,5519,5896,6269,6639,7005,7366,7723,8075,8423,8765,9102,9434,9759,10079,10393,10701,11002,11297,11585,11866,12139,12406,12665,12916,13159,13395,13622,13842,14053,14255,14449,14634,14810,14978,15136,15286,15426,15557,15678,15790,15892,15985,16069,16142,16206,16260,16305,16339,16364,16379,16384};
static s16 biosSine(u8 a){int q=a>>6,i=a&63,v=quarter[(q&1)?64-i:i];return q<2?v:-v;}
static void ObjAffineSet(const struct ObjAffineSrcData*s,struct OamMatrix*d,int count,int stride){assert(count==1&&stride==2);u8 a=s->rotation>>8;int sn=biosSine(a),cs=biosSine(a+64);d->a=(s->xScale*cs)>>14;d->b=-((s->xScale*sn)>>14);d->c=(s->yScale*sn)>>14;d->d=(s->yScale*cs)>>14;}
'''
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'
 h+='static void(*sAnimFuncs[])(struct Sprite*)={ContinueAnim,BeginAnim};\nstatic void(*sAnimCmdFuncs[])(struct Sprite*)={AnimCmd_loop,AnimCmd_jump,AnimCmd_end,AnimCmd_frame};\nstatic void(*sAffineAnimFuncs[])(struct Sprite*)={ContinueAffineAnim,BeginAffineAnim};\nstatic void(*sAffineAnimCmdFuncs[])(u8,struct Sprite*)={UnsupportedAffineLoop,AffineAnimCmd_jump,AffineAnimCmd_end,AffineAnimCmd_frame};\n'
 for file,names in [('battle_anim_effects_2',['sCoinAnimCmds','sFallingCoinAffineAnimCmds']),('battle_anim_effects_3',['sBarrageBallAffineAnimCmds1','sBarrageBallAffineAnimCmds2']),('battle_anim_fight',['sAnim_HandLeft','sAnim_HandRight']),('battle_anim_normal',['sAffineAnim_HitSplat_0','sAffineAnim_HitSplat_1','sAffineAnim_HitSplat_2','sAffineAnim_HitSplat_3'])]:h+='\n'.join(declaration(src[file],n) for n in names)+'\n'
 h+=r'''
static const union AnimCmd dummyFrame[]={ANIMCMD_FRAME(0,1),ANIMCMD_JUMP(0)};static const union AffineAnimCmd dummyAffine[]={AFFINEANIMCMD_END};
static const union AnimCmd *const dummyFrames[]={dummyFrame},*const coinFrames[]={sCoinAnimCmds},*const handFrames[]={sAnim_HandLeft,sAnim_HandRight};
static const union AffineAnimCmd *const dummyAffines[]={dummyAffine},*const coinAffines[]={sFallingCoinAffineAnimCmds},*const ballAffines[]={sBarrageBallAffineAnimCmds1,sBarrageBallAffineAnimCmds2},*const impactAffines[]={sAffineAnim_HitSplat_0,sAffineAnim_HitSplat_1,sAffineAnim_HitSplat_2,sAffineAnim_HitSplat_3};
static const struct OamData oamOff={0},oamAffine={1};
static const struct SpriteTemplate hand={1,1,&oamOff,handFrames,NULL,dummyAffines,AnimCrossChopHand},cross={1,1,&oamOff,dummyFrames,NULL,dummyAffines,AnimCrossImpact},coin={1,1,&oamAffine,coinFrames,NULL,dummyAffines,AnimCoinThrow},fall={1,1,&oamAffine,coinFrames,NULL,coinAffines,AnimFallingCoin};
static const struct SpriteTemplate gFlashingHitSplatSpriteTemplate={1,1,&oamAffine,dummyFrames,NULL,impactAffines,AnimFlashingHitSplat},gBarrageBallSpriteTemplate={1,1,&oamAffine,dummyFrames,NULL,ballAffines,SpriteCallbackDummy};
'''
 h+='\n'.join(funcs)+'\n'
 for k,configs in enumerate(CONFIGS):h+='static const s16 config'+str(k)+'[][8]={'+','.join('{'+','.join(map(str,a or [0]))+'}' for a in configs)+'};\n'
 h+='static const s16 (*configs[])[8]={config0,config1,config2,config3,config4,config5};\nstatic const int counts[]={'+','.join(str(len(c)) for c in CONFIGS)+'};\n'
 h+=r'''
static int matrices(void){int n=0;for(int i=slots;i<32;i++)n+=(gOamMatrixAllocBitmap>>i)&1;return n;}
static void prefix(int kind,int config,int tick,char type){printf("%d:%d:%d:%d:%d,%c,%d",kind,mode,gBattleAnimAttacker,gBattleAnimTarget,config,type,tick);}
static void sprites(int kind,int config,int tick){for(int i=slots;i<MAX_SPRITES;i++){struct Sprite*s=&gSprites[i];if(!s->inUse)continue;struct OamMatrix m=(s->oam.affineMode&1)?gOamMatrices[s->oam.matrixNum]:(struct OamMatrix){256,0,0,256};int row[]={i,s->x,s->y,s->x2,s->y2,s->invisible,s->oam.tileNum,(s->oam.matrixNum>>3)&1,(s->oam.matrixNum>>4)&1,s->subpriority,m.a,m.b,m.c,m.d,s->oam.affineMode&1};if(row[14])row[7]=row[8]=0;prefix(kind,config,tick,'P');for(unsigned j=0;j<sizeof(row)/sizeof(row[0]);j++)printf(",%d",row[j]);puts("");}}
static void summary(int kind,int config,int tick,int before){prefix(kind,config,tick,'T');printf(",%d,%d,%d,%d",gAnimVisualTaskCount,gTasks[0].isActive,before,matrices());for(int i=0;i<5;i++)printf(",%d",gBattleAnimArgs[i]);puts("");}
static void run(int kind,int actor,int target,int config){
 slots=mode?4:2;gBattleAnimAttacker=actor;gBattleAnimTarget=target;memcpy(gBattleAnimArgs,configs[kind][config],sizeof(gBattleAnimArgs));memset(gSprites,0,sizeof(gSprites));memset(gTasks,0,sizeof(gTasks));memset(sAffineAnimStates,0,sizeof(sAffineAnimStates));gOamMatrixAllocBitmap=(1u<<slots)-1;gAnimVisualTaskCount=0;for(int i=0;i<32;i++)SetOamMatrix(i,256,0,0,256);
 for(int i=0;i<slots;i++){gSprites[i].inUse=TRUE;gSprites[i].usingSheet=TRUE;gSprites[i].callback=SpriteCallbackDummy;gSprites[i].anims=dummyFrames;gSprites[i].affineAnims=dummyAffines;}
 if(kind==2){s16 start=GetBattlerSpriteCoord(actor,2);int direction=start>GetBattlerSpriteCoord(target,2)?-1:start<GetBattlerSpriteCoord(target,2)?1:(actor&1)?-1:1;start+=direction*gBattleAnimArgs[0];s16 end=GetBattlerSpriteCoord(target,2)+((actor&1)?-gBattleAnimArgs[2]:gBattleAnimArgs[2]);int duration=(abs(end-start)<<8)/gBattleAnimArgs[4];if(duration<=0||duration>32767)return;}
 if(kind<4){const struct SpriteTemplate*t=(const struct SpriteTemplate*[]){&hand,&cross,&coin,&fall}[kind];CreateSpriteAndAnimate(t,GetBattlerSpriteCoord(target,2),GetBattlerSpriteCoord(target,3),GetBattlerSpriteSubpriority(target)-2);gAnimVisualTaskCount++;}
 else{gTasks[0].isActive=TRUE;gTasks[0].func=kind==4?AnimTask_DrillPeckHitSplats:AnimTask_BarrageBall;gTasks[0].func(0);gAnimVisualTaskCount++;}
 sprites(kind,config,-1);summary(kind,config,-1,matrices());
 for(int tick=0;tick<600;tick++){AnimateSprites();int before=matrices();sprites(kind,config,tick);if(gTasks[0].isActive)gTasks[0].func(0);summary(kind,config,tick,before);if(!gAnimVisualTaskCount){assert(matrices()==0);return;}}abort();
}
int main(void){for(int k=0;k<6;k++)for(mode=0;mode<2;mode++)for(int a=0;a<(mode?4:2);a++)for(int t=0;t<(mode?4:2);t++)for(int c=0;c<counts[k];c++)run(k,a,t,c);}
'''
 if emit_source is not None:emit_source.write_text(h);return None
 with tempfile.TemporaryDirectory(prefix='impact-emission-source-c-') as tmp:
  c=Path(tmp)/'impact.c';exe=Path(tmp)/'impact';c.write_text(h);ref.compile_c(['cc','-std=c11','-O0',str(c),'-o',str(exe)]);output=subprocess.check_output([str(exe)],text=True)
 cases={}
 for line in output.splitlines():
  key,type,*values=line.split(',');v=list(map(int,values));row=cases.setdefault(key,{}).setdefault(v[0],dict(sprites=[]))
  if type=='P':assert len(v)==len(SPRITE_COLUMNS)+1;row['sprites'].append(v[1:])
  else:assert len(v)==len(COLUMNS);row['state']=v
 result={key:list(rows.values()) for key,rows in cases.items()}
 assert len(result)>250
 for key,rows in result.items():
  assert rows[0]['state'][0]==-1 and rows[-1]['state'][1]==0,key
  assert rows[-1]['state'][4]==0,key
  assert max(r['state'][4] for r in rows)<=4,key
 return dict(sourceRevision=ref.COMMIT,sourceSha256=SOURCE_SHA256,columns=COLUMNS,spriteColumns=SPRITE_COLUMNS,configs=CONFIGS,cases=result)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,default=ROOT);p.add_argument('--output',type=Path);p.add_argument('--emit-source',type=Path);a=p.parse_args()
 if bool(a.output)==bool(a.emit_source):p.error('Choose one output')
 out=a.output or a.emit_source
 if out.exists():p.error('Refusing to overwrite a source-vector candidate')
 if a.emit_source:build(a.root,a.emit_source)
 else:
  result=build(a.root);out.write_text(json.dumps(result,separators=(',',':'))+'\n');print('cases',len(result['cases']),'rows',sum(map(len,result['cases'].values())))

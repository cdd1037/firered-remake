#!/usr/bin/env python3
"""Pinned source-C orbital/spray callbacks and palette/BG/task-affine numeric oracles.

All observed callbacks, source sprite/frame/affine allocation and interpolation,
Random, BlendPalette and task affine interpreter are extracted verbatim from
037335f4. Host adapters only supply static battle geometry/palette allocation,
32-bit pointer handles, BIOS ObjAffineSet and other unobserved platform hooks.
No upstream script, managed compiler, or managed implementation is executed.
Tick -1 is synchronous CreateSpriteAndAnimate / task initialization. At ordinary
ticks AnimateSprites and the OAM snapshot precede the ordinary task callback.
"""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
SOURCE_SHA256={'src/sprite.c': '2a804302eb5a89d31c3ec2f33dc645a80a0c645113d4b33b9bd571064c0286ae', 'src/battle_anim.c': '5883d9ee0483120ef67461952f5880499f322fb1c706cc213ca13c0c2b0e88c5', 'src/battle_anim_mons.c': 'fcb002acc48b8cd6b8cf8b7eddcb3034d721ff5887eb6bdeedfe183b7b3908bb', 'src/battle_anim_fight.c': '343a674d3230233af2ecd681c104f550b13e51485b011534c1d60d68ba65f5f5', 'src/battle_anim_flying.c': 'd51ff92894cbed0dd2c2434e73ed03bb2f2f20d930561f55fb0e31df4568ab0a', 'src/battle_anim_effects_1.c': '80de93553f73982ca5b1abab6af952fe96e0b4763fefddf8aa1dc5071ad20682', 'src/battle_anim_effects_2.c': '50561e4882a0f8813f305af6f4147ed1c394657278e32aae6c733bef31994a04', 'src/trig.c': '4144065a90e5376ce0259796a01ebaae33c6cc6c7ef716de95b9d9a13c85a810', 'src/blend_palette.c': '219a02b2ed9ab3010e203e0131fa8133571dcc271e025d7f6f14bf7993a51090', 'src/random.c': 'e7e90cc070d1fbf09579f5f91762cd8875d5bbe1d7a90f468bcd870df73b9de2', 'include/sprite.h': 'a77aa1c837dfb58cf60b3eac299c7cc29a23ce27735ba710b9e8eeed102bf773', 'include/battle_anim.h': '15d0a815fec54ed669e1cf7130d323070d799ecfba0f3a935da4b9c28d6f09a7', 'include/gba/types.h': 'd83a5cb845180b881f3de081ef9c2694b2da017a406fd3d6b0830329cc64ed93', 'include/random.h': '8a6049183598b2d865b5ff522abdb3d0e94012a127d724af46b9b7b69803f5b9', 'data/battle_anim_scripts.s': 'f1566a2d43fad88619f4e3799f995bfd1c53b65fe8de4c4a91d66a440284ea9e', 'src/battle_main.c': 'c290d325761d68c059f02dc21cc6178d4bbefa0d06ffddc8879341c090d37928', 'include/battle.h': 'bf64a16fb4e660f8a326eb352a511bb9e8fd54a74ebb03e328c08d5a23e5d8fa'}
FAST_CONFIGS=[[1,0,7],[2,63,9],[18,0,42],[18,32,42],[18,64,42],[18,96,42],[18,128,42],[18,160,42],[18,192,42],[18,224,42],[40,255,84],[40,0,84],[0,63,8],[-1,191,8],[2,64,2],[18,191,0]]
SCATTER_CONFIGS=[[a] for a in range(0,256,32)]
SPRAY_CONFIGS=[[direction,battler,seed] for seed in [0,1,0x12345678,0xFFFFFFFF] for direction in [0,1] for battler in [0,1]]
PALETTE_CONFIGS=[[0,1,124,0],[3,7,245,0],[14,15,0,0],[2,9,-1,0],[4,10,5,-3]]
SKY_CONFIGS=[[delay,stop,x,y] for delay,stop,x,y in [[0,12,0,0],[1,14,0,0],[20,50,0,0],[-1,18,0,0],[32767,8,32760,32760],[-32768,8,-32760,-32760],[2,4,32765,-32765],[20,0,0,0],[0,-1,0,0]]]
CONFIGS=[FAST_CONFIGS,SCATTER_CONFIGS,SPRAY_CONFIGS,PALETTE_CONFIGS,SKY_CONFIGS,[[0]]]
KINDS=['OrbitFast','OrbitScatter','SprayWaterDroplet','CycleMagicalLeafPal','MoveSkyUppercutBg','StretchTargetUp']
SPRITE_COLUMNS=['slot','x','y','x2','y2','invisible','tile','hflip','vflip','subpriority','a','b','c','d','affine','affineMode','affinePaused','affineEnded','affineIndex','affineDelay','xScale','yScale','rotation']+['data'+str(i) for i in range(8)]
STATE_COLUMNS=['tick','count','task','matrices_before','matrices_after','arg7','rngState','rngDraws','bg3x','bg3y','bgMode','toggleCalls','blendWrites','blendOffset0','blendOffset1','blendCoefficient','blendColor']+['taskData'+str(i) for i in range(16) if i not in [13,14]]
PALETTE_COLUMNS=['paletteSlot']+['color'+str(i) for i in range(16)]
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def function(source,name):
 m=re.search(r'^(?:static )?(?:void|s8|s16|s32|u8|u16|bool8) '+name+r'\([^;]+?\)\n\{',source,re.M)
 if not m:raise ValueError('Missing source function '+name)
 i=m.end();depth=1
 while depth:depth+=(source[i]=='{')-(source[i]=='}');i+=1
 return source[m.start():i]
def declaration(source,name):
 m=re.search(r'^(?:static )?const (?:union (?:AffineAnimCmd|AnimCmd)|s16|u16) '+name+r'\[\]\s*=\s*\{.*?\};',source,re.M|re.S)
 if not m:raise ValueError('Missing source declaration '+name)
 return m[0]
def build(root,section,emit_source=None):
 ref=module('reference_c',root/'tests/Reference/reference_c.py');raw=root/'content/raw/pokefirered/037335f4';ref.verify_reference(raw)
 for name,digest in SOURCE_SHA256.items():
  if hashlib.sha256((raw/name).read_bytes()).hexdigest()!=digest:raise ValueError('Pinned orbit/spray source mismatch: '+name)
 names=['sprite', 'battle_anim', 'battle_anim_mons', 'battle_anim_fight', 'battle_anim_flying', 'battle_anim_effects_1', 'battle_anim_effects_2', 'trig', 'blend_palette', 'random'];src={n:(raw/('src/'+n+'.c')).read_text() for n in names}
 header=(raw/'include/sprite.h').read_text();battle=(raw/'include/battle_anim.h').read_text()
 sprite_names=['AnimateSprites','CreateSprite','CreateSpriteAt','CreateSpriteAndAnimate','DestroySprite','ResetSprite','SpriteCallbackDummy','SetSpriteSheetFrameTileNum','AnimateSprite','BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','DecrementAnimDelayCounter','BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame','AffineAnimStateRestartAnim','AffineAnimStateReset','ApplyAffineAnimFrameAbsolute','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame','ApplyAffineAnimFrame','StartSpriteAffineAnim','AffineAnimStateStartAnim','SetSpriteOamFlipBits','GetSpriteMatrixNum','AllocOamMatrix','FreeOamMatrix','FreeSpriteOamMatrix','InitSpriteAffineAnim']
 funcs=[function(src['sprite'],n) for n in sprite_names]

 funcs += [function(src['battle_anim'],n) for n in ['DestroyAnimSprite','DestroyAnimVisualTask']]
 funcs += [function(src['battle_anim_mons'],n) for n in ['ShouldRotScaleSpeciesBeFlipped','SetSpriteRotScale','PrepareBattlerSpriteForRotScale','ResetSpriteRotScale','PrepareAffineAnimInTaskData','RunAffineAnimFromTaskData','SetBattlerSpriteYOffsetFromYScale']]
 funcs += [function(src['battle_anim_effects_2'],n) for n in ['AnimOrbitFast','AnimOrbitFast_Step','AnimOrbitScatter','AnimOrbitScatter_Step','AnimTask_StretchTargetUp']]
 funcs += [function(src['battle_anim_flying'],n) for n in ['AnimSprayWaterDroplet','AnimSprayWaterDroplet_Step']]
 funcs += [function(src['battle_anim_effects_1'],'AnimTask_CycleMagicalLeafPal')]
 funcs += [function(src['battle_anim_fight'],'AnimTask_MoveSkyUppercutBg')]
 funcs += [function(src['trig'],n) for n in ['Sin','Cos']]
 funcs += [function(src['random'],'Random')]
 extracted=list(funcs)+[function(src['blend_palette'],'BlendPalette')]
 for file,name in [('battle_anim_effects_2','sHiddenPowerOrbAffineAnimCmds'),('battle_anim_effects_2','sAffineAnims_StretchBattlerUp'),('battle_anim_effects_1','sMagicalLeafBlendColors'),('trig','gSineTable')]:extracted.append(declaration(src[file],name))
 for kind,name in [('AnimCmd','sDummyAnim'),('AffineAnimCmd','sDummyAffineAnim')]:extracted.append(re.search(r'^static const union '+kind+r' '+name+r' = .*?;',src['sprite'],re.M)[0])
 sourceSpans=[]
 for snippet in extracted:
  matches=[(name,text.index(snippet)) for name,text in src.items() if snippet in text];assert len(matches)==1
  file,index=matches[0];first=src[file][:index].count('\n')+1
  sourceSpans.append(dict(file='src/'+file+'.c',firstLine=first,lastLine=first+snippet.count('\n'),sha256=hashlib.sha256(snippet.encode()).hexdigest(),declaration=snippet.split('\n')[0]))
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
#define ST_OAM_AFFINE_NORMAL 1
#define ST_OAM_AFFINE_DOUBLE 3
#define ST_OAM_OBJ_NORMAL 0
#define ST_OAM_HFLIP 8
#define DISPLAY_WIDTH 240
#define DISPLAY_HEIGHT 160
#define MON_PIC_HEIGHT 64
#define ANIM_END -1
#define AFFINE_ANIM_END 32767
#define RGB(r,g,b) ((r)|((g)<<5)|((b)<<10))
#define OBJ_PLTT_ID(p) (((p)+16)*16)
#define ANIM_TAG_LEAF 1
#define ANIM_TAG_RAZOR_LEAF 2
#define ISO_RANDOMIZE1(val) (1103515245u*(val)+24691u)
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
 # ARM source ABI rounds affine commands to eight bytes; task interpreter uses byte offset index << 3.
 header=header.replace('union AffineAnimCmd\n{','union AffineAnimCmd\n{\n    u32 hostSourceAbiAlignment;')
 h+=header[header.index('struct AnimFrameCmd\n'):header.index('\nenum\n')]+declaration(src['trig'],'gSineTable')+'\n'+'\n'.join(re.findall(r'^#define sTransl_.*$',battle,re.M))+'\n'
 h+=r'''
_Static_assert(sizeof(union AffineAnimCmd)==8,"Source ARM affine command ABI");
struct Sprite;struct SpriteTemplate;typedef void(*SpriteCallback)(struct Sprite*);
struct OamData {u8 affineMode,matrixNum,shape,size,paletteNum,objMode;u16 tileNum;};
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
static int paletteSlots[2];static u8 IndexOfSpritePaletteTag(u16 tag){return tag==ANIM_TAG_LEAF?paletteSlots[0]:tag==ANIM_TAG_RAZOR_LEAF?paletteSlots[1]:0;}
static void CopyOamMatrix(u8 n,struct OamMatrix*m){assert(n<32);gOamMatrices[n]=*m;}
static void SetOamMatrix(u8 n,s16 a,s16 b,s16 c,s16 d){assert(n<32);gOamMatrices[n]=(struct OamMatrix){a,b,c,d};}
static void UnsupportedAffineLoop(u8 n,struct Sprite*s){abort();}
static const s16 quarter[]={0,402,803,1205,1605,2005,2404,2801,3196,3589,3980,4369,4756,5139,5519,5896,6269,6639,7005,7366,7723,8075,8423,8765,9102,9434,9759,10079,10393,10701,11002,11297,11585,11866,12139,12406,12665,12916,13159,13395,13622,13842,14053,14255,14449,14634,14810,14978,15136,15286,15426,15557,15678,15790,15892,15985,16069,16142,16206,16260,16305,16339,16364,16379,16384};
static s16 biosSine(u8 a){int q=a>>6,i=a&63,v=quarter[(q&1)?64-i:i];return q<2?v:-v;}
static void ObjAffineSet(const struct ObjAffineSrcData*s,struct OamMatrix*d,int count,int stride){assert(count==1&&stride==2);u8 a=s->rotation>>8;int sn=biosSine(a),cs=biosSine(a+64);d->a=(s->xScale*cs)>>14;d->b=-((s->xScale*sn)>>14);d->c=(s->yScale*sn)>>14;d->d=(s->yScale*cs)>>14;}
'''
 h+=r'''
static u32 gRngValue;static int rngDraws;
static u16 gPlttBufferUnfaded[512],gPlttBufferFaded[512];
static int blendWrites,blendOffset0,blendOffset1,blendCoefficient,blendColor;
static u16 gBattle_BG3_X,gBattle_BG3_Y;static int bgMode,toggleCalls;
static union AffineAnimCmd*sAnimTaskAffineAnim;
static const void *storedPointer;
static void StorePointerInVars(s16*lo,s16*hi,const void*p){storedPointer=p;*lo=1;*hi=0;}
static void*LoadPointerFromVars(s16 lo,s16 hi){assert(lo==1&&hi==0);return (void*)storedPointer;}
struct BattleSpritesData {struct {u8 matrixNum;}healthBoxesData[4];};
static struct BattleSpritesData battleSpritesData;static struct BattleSpritesData*gBattleSpritesDataPtr=&battleSpritesData;
static bool8 IsBattlerSpriteVisible(u8 b){assert(b<slots);return TRUE;}
static u8 GetAnimBattlerSpriteId(u8 which){return which?gBattleAnimTarget:gBattleAnimAttacker;}
static u16 GetBattlerYDeltaFromSpriteId(u8 n){assert(n<slots);return (u16[]){5,11,7,13}[n];}
static void ToggleBg3Mode(u8 value){bgMode=value;toggleCalls++;}
'''
 h+=re.search(r'struct PlttData\s*\{.*?\};',(raw/'include/gba/types.h').read_text(),re.S)[0]+'\n'
 h+=function(src['blend_palette'],'BlendPalette')+'\n'
 h+=r'''
static void ObservedBlendPalette(u16 offset,u16 count,u8 coefficient,u16 color){
 assert(count==16);if(blendWrites==0)blendOffset0=offset;else if(blendWrites==1)blendOffset1=offset;else abort();blendWrites++;blendCoefficient=coefficient;blendColor=color;BlendPalette(offset,count,coefficient,color);
}
#define BlendPalette ObservedBlendPalette
'''
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'
 h+='static void(*sAnimFuncs[])(struct Sprite*)={ContinueAnim,BeginAnim};\nstatic void(*sAnimCmdFuncs[])(struct Sprite*)={AnimCmd_loop,AnimCmd_jump,AnimCmd_end,AnimCmd_frame};\nstatic void(*sAffineAnimFuncs[])(struct Sprite*)={ContinueAffineAnim,BeginAffineAnim};\nstatic void(*sAffineAnimCmdFuncs[])(u8,struct Sprite*)={UnsupportedAffineLoop,AffineAnimCmd_jump,AffineAnimCmd_end,AffineAnimCmd_frame};\n'
 h+=declaration(src['battle_anim_effects_2'],'sHiddenPowerOrbAffineAnimCmds')+'\n'+declaration(src['battle_anim_effects_2'],'sAffineAnims_StretchBattlerUp')+'\n'+declaration(src['battle_anim_effects_1'],'sMagicalLeafBlendColors')+'\n'
 h+='\n'.join(re.search(r'^static const union '+kind+r' '+name+r' = .*?;',src['sprite'],re.M)[0] for kind,name in [('AnimCmd','sDummyAnim'),('AffineAnimCmd','sDummyAffineAnim')])+'\n'
 h+=r'''
static const union AnimCmd *const dummyFrames[]={&sDummyAnim};
static const union AffineAnimCmd *const dummyAffines[]={&sDummyAffineAnim},*const orbAffines[]={sHiddenPowerOrbAffineAnimCmds};
static const struct OamData oamOff={0},oamAffine={1},oamDouble={3};
static const struct SpriteTemplate fast={1,1,&oamAffine,dummyFrames,NULL,orbAffines,AnimOrbitFast},scatter={1,1,&oamDouble,dummyFrames,NULL,orbAffines,AnimOrbitScatter},spray={1,1,&oamOff,dummyFrames,NULL,dummyAffines,AnimSprayWaterDroplet};
'''
 # Keep Random itself verbatim. A narrow wrapper records how many calls creation makes.
 random_func=funcs.pop();assert random_func.startswith('u16 Random(')
 h+=random_func+'\nstatic u16 ObservedRandom(void){rngDraws++;return Random();}\n#define Random ObservedRandom\n'
 h+='\n'.join(funcs)+'\n'
 for k,configs in enumerate(CONFIGS):h+='static const long long config'+str(k)+'[][8]={'+','.join('{'+','.join(map(str,a or [0]))+'}' for a in configs)+'};\n'
 h+='static const long long (*configs[])[8]={config0,config1,config2,config3,config4,config5};\nstatic const int counts[]={'+','.join(str(len(c)) for c in CONFIGS)+'};\n'
 h+=r'''
static int matrices(void){int n=0;for(int i=slots;i<32;i++)n+=(gOamMatrixAllocBitmap>>i)&1;return n;}
static void prefix(int kind,int config,int tick,char type){printf("%d:%d:%d:%d:%d,%c,%d",kind,mode,gBattleAnimAttacker,gBattleAnimTarget,config,type,tick);}
static void sprites(int kind,int config,int tick,char type){for(int i=(kind==5?gBattleAnimTarget:slots);i<(kind==5?gBattleAnimTarget+1:MAX_SPRITES);i++){struct Sprite*s=&gSprites[i];if(!s->inUse)continue;struct OamMatrix m=(s->oam.affineMode&1)?gOamMatrices[s->oam.matrixNum]:(struct OamMatrix){256,0,0,256};struct AffineAnimState*a=&sAffineAnimStates[s->oam.matrixNum];int row[]={i,s->x,s->y,s->x2,s->y2,s->invisible,s->oam.tileNum,(s->oam.matrixNum>>3)&1,(s->oam.matrixNum>>4)&1,s->subpriority,m.a,m.b,m.c,m.d,s->oam.affineMode&1,s->oam.affineMode,s->affineAnimPaused,s->affineAnimEnded,a->animCmdIndex,a->delayCounter,a->xScale,a->yScale,a->rotation};if(row[14])row[7]=row[8]=0;prefix(kind,config,tick,type);for(unsigned j=0;j<sizeof(row)/sizeof(row[0]);j++)printf(",%d",row[j]);for(int j=0;j<8;j++)printf(",%d",s->data[j]);puts("");}}
static void palette(int kind,int config,int tick){for(int i=0;i<512;i++)if(i/16!=paletteSlots[0]+16&&i/16!=paletteSlots[1]+16)assert(gPlttBufferFaded[i]==((i*73+31)&32767));for(int p=0;p<2;p++){prefix(kind,config,tick,'L');printf(",%d",paletteSlots[p]);for(int i=0;i<16;i++)printf(",%d",gPlttBufferFaded[OBJ_PLTT_ID(paletteSlots[p])+i]);puts("");}}
static void summary(int kind,int config,int tick,int before){prefix(kind,config,tick,'T');printf(",%d,%d,%d,%d,%d,%u,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",gAnimVisualTaskCount,gTasks[0].isActive,before,matrices(),gBattleAnimArgs[7],gRngValue,rngDraws,gBattle_BG3_X,gBattle_BG3_Y,bgMode,toggleCalls,blendWrites,blendOffset0,blendOffset1,blendCoefficient,blendColor);for(int i=0;i<16;i++)if(i!=13&&i!=14)printf(",%d",gTasks[0].data[i]);puts("");}
static void resetwrites(void){blendWrites=0;blendOffset0=blendOffset1=blendCoefficient=blendColor=-1;}
static void run(int kind,int actor,int target,int config){
 slots=mode?4:2;gBattleAnimAttacker=actor;gBattleAnimTarget=target;memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));memset(gSprites,0,sizeof(gSprites));memset(gTasks,0,sizeof(gTasks));memset(sAffineAnimStates,0,sizeof(sAffineAnimStates));memset(&battleSpritesData,0,sizeof(battleSpritesData));gOamMatrixAllocBitmap=(1u<<slots)-1;gAnimVisualTaskCount=0;for(int i=0;i<32;i++)SetOamMatrix(i,256,0,0,256);
 for(int i=0;i<slots;i++){struct Sprite*s=&gSprites[i];s->inUse=TRUE;s->usingSheet=TRUE;s->animBeginning=TRUE;s->callback=SpriteCallbackDummy;s->anims=dummyFrames;s->affineAnims=dummyAffines;s->oam.affineMode=1;s->oam.matrixNum=i;s->affineAnimPaused=TRUE;s->data[0]=i;s->x=GetBattlerSpriteCoord(i,2);s->y=GetBattlerSpriteCoord(i,3);s->subpriority=GetBattlerSpriteSubpriority(i);battleSpritesData.healthBoxesData[i].matrixNum=i;AffineAnimStateReset(i);}
 gRngValue=0;rngDraws=0;gBattle_BG3_X=gBattle_BG3_Y=0;bgMode=-1;toggleCalls=0;resetwrites();int stop=1000;
 const long long*c=configs[kind][config];
 if(kind<3){gBattleAnimArgs[0]=c[0];gBattleAnimArgs[1]=c[1];if(kind==0)stop=c[2];if(kind==2)gRngValue=(u32)c[2];const struct SpriteTemplate*t=(const struct SpriteTemplate*[]){&fast,&scatter,&spray}[kind];CreateSpriteAndAnimate(t,GetBattlerSpriteCoord(target,2),GetBattlerSpriteCoord(target,3),GetBattlerSpriteSubpriority(target)-2);gAnimVisualTaskCount++;if(kind==2)assert(rngDraws==2);}
 else{
  if(kind==3){paletteSlots[0]=c[0];paletteSlots[1]=c[1];stop=c[2];for(int i=0;i<512;i++)gPlttBufferUnfaded[i]=gPlttBufferFaded[i]=(u16)((i*73+31)&32767);gTasks[0].data[9]=c[3];}
  if(kind==4){gBattleAnimArgs[0]=c[0];stop=c[1];gBattle_BG3_X=c[2];gBattle_BG3_Y=c[3];}
  if(stop==-1)gBattleAnimArgs[7]=-1;
  gTasks[0].isActive=TRUE;gTasks[0].func=(void(*[])(u8)){AnimTask_CycleMagicalLeafPal,AnimTask_MoveSkyUppercutBg,AnimTask_StretchTargetUp}[kind-3];gAnimVisualTaskCount++;gTasks[0].func(0);
 }
 sprites(kind,config,-1,'P');if(kind==5)sprites(kind,config,-1,'Q');if(kind==3)palette(kind,config,-1);summary(kind,config,-1,matrices());if(!gAnimVisualTaskCount)return;
 for(int tick=0;tick<1000;tick++){if(tick==stop)gBattleAnimArgs[7]=-1;resetwrites();AnimateSprites();int before=matrices();sprites(kind,config,tick,'P');if(gTasks[0].isActive)gTasks[0].func(0);if(kind==5)sprites(kind,config,tick,'Q');if(kind==3)palette(kind,config,tick);summary(kind,config,tick,before);if(kind==2)assert(rngDraws==2);if(!gAnimVisualTaskCount){assert(matrices()==0);return;}}abort();
}
int main(void){for(int k=0;k<6;k++)for(mode=0;mode<2;mode++)for(int a=0;a<(mode?4:2);a++)for(int t=0;t<(mode?4:2);t++)for(int c=0;c<counts[k];c++){if(k==3&&(mode!=0||a!=0||t!=1))continue;run(k,a,t,c);}}
'''
 for snippet in extracted:assert snippet in h,'Extracted source must appear verbatim in authored harness'
 if emit_source is not None:emit_source.write_text(h);return None
 with tempfile.TemporaryDirectory(prefix='orbit-spray-source-c-') as tmp:
  c=Path(tmp)/'orbit.c';exe=Path(tmp)/'orbit';c.write_text(h);ref.compile_c(['cc','-std=c11','-O0',str(c),'-o',str(exe)]);output=subprocess.check_output([str(exe)],text=True)
 cases={}
 for line in output.splitlines():
  key,type,*values=line.split(',');kind=int(key.split(':')[0])
  if (section=='sprites') != (kind<3):continue
  v=list(map(int,values));row=cases.setdefault(key,{}).setdefault(v[0],dict(sprites=[],afterTaskSprites=[],palettes=[]))
  if type in ['P','Q']:assert len(v)==len(SPRITE_COLUMNS)+1;row['sprites' if type=='P' else 'afterTaskSprites'].append(v[1:])
  elif type=='L':assert len(v)==len(PALETTE_COLUMNS)+1;row['palettes'].append(v[1:])
  else:assert len(v)==len(STATE_COLUMNS),(len(v),len(STATE_COLUMNS));row['state']=v
 result={key:list(rows.values()) for key,rows in cases.items()}
 for key,rows in result.items():
  assert rows[0]['state'][0]==-1 and rows[-1]['state'][1]==0,key
  assert rows[-1]['state'][4]==0,key
 return dict(sourceRevision=ref.COMMIT,sourceSha256=SOURCE_SHA256,sourceSpans=sourceSpans,configColumns=[['duration','phase','stopTick'],['phase'],['direction','useTarget','seed'],['leafPaletteSlot','razorLeafPaletteSlot','stopTick','initialDelayCounter'],['delay','stopTick','initialBg3X','initialBg3Y'],['unused']],paletteInputFormula='unfaded[index] = (index * 73 + 31) & 32767',kinds=KINDS,columns=STATE_COLUMNS,spriteColumns=SPRITE_COLUMNS,paletteColumns=PALETTE_COLUMNS,configs=CONFIGS,geometry=dict(x=[[72,176,48,112],[32,200,90,152]],y=[[80,40,40,80],[80,40,88,32]],yOffset=[5,11,7,13],elevation=[2,3,4,5],subpriority=[30,40,20,50]),conventions=dict(tickMinusOne='After synchronous create/init',sprites='After AnimateSprites, before task callback',state='After task callback',palette='After task callback',bg3='u16 source registers; task data are s16',afterTaskSprites='Stretch only: post-task state, including terminal restoration',taskPointerFields='taskData13 and taskData14 are omitted host pointer handles'),cases=result)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,default=ROOT);p.add_argument('--section',choices=['sprites','tasks'],default='sprites');p.add_argument('--output',type=Path);p.add_argument('--emit-source',type=Path);a=p.parse_args()
 if bool(a.output)==bool(a.emit_source):p.error('Choose one output')
 out=a.output or a.emit_source
 if out.exists():p.error('Refusing to overwrite a source-vector candidate')
 if a.emit_source:build(a.root,a.section,a.emit_source)
 else:
  result=build(a.root,a.section);out.write_text(json.dumps(result,separators=(',',':'))+'\n');print('cases',len(result['cases']),'rows',sum(map(len,result['cases'].values())))

#!/usr/bin/env python3
"""Extract Swords Dance / Sharpen numerical observations from pinned source C.

The authored harness compiles exact callback, shared translation, sprite player,
allocation and teardown bodies. It never invokes an upstream build script or a
managed compiler. Pointer storage and zero-rotation BIOS are explicit host adapters.
"""
import argparse, importlib.util, json, os, re, subprocess, sys, tempfile
from pathlib import Path
sys.dont_write_bytecode = True
COLUMNS = ['event','tick','phase','invokedCallback','callback','storedCallback','active','x','y','x2','y2',
 'data0','data1','data2','data3','data4','data5','invisible','frameBeginning','frameEnded','frameCommand','frameDelay','tile',
 'affineBeginning','affineEnded','affineCommand','affineDelay','scaleX','scaleY','matrixA','matrixD','visualCount','matrixBitmap','soundCount']
PHASES = ['after_callback','after_frame','after_affine','before_destroy','after_destroy','after_script_creation','probe_input']
CALLBACKS = ['dummy','blade_init','wait_affine_end','blade_step','start_linear','linear_followup','destroy','sharpen_init','sharpen_step']
COORDS = [[32,80,32,88],[200,40,200,32],[90,88,90,96],[152,32,152,24]]
LAYOUTS = [[0,1,2,3],[1,0,3,2]]
PROBES = [
 {'name':'elapsed_wrap','data':[32767,2,0,0,0,-64],'invisible':0,'ended':0},
 {'name':'interval_wrap','data':[32766,32767,1,0,0,-64],'invisible':0,'ended':1},
 {'name':'toggle_pair_wrap','data':[1,2,32767,0,0,-64],'invisible':0,'ended':1},
 {'name':'visible_count_wrap','data':[1,2,0,0,32767,63],'invisible':1,'ended':0},
 {'name':'negative_visible_count','data':[1,2,0,0,-1,-64],'invisible':1,'ended':0},
 {'name':'negative_interval','data':[0,-32768,0,0,0,63],'invisible':0,'ended':0},
 {'name':'ended_required','data':[0,17,0,0,0,-64],'invisible':1,'ended':0},
 {'name':'interval_strictly_above16','data':[0,16,0,0,0,-64],'invisible':1,'ended':1},
 {'name':'invisible_required','data':[0,17,0,0,0,-64],'invisible':0,'ended':1},
 {'name':'already_terminal','data':[0,17,0,0,0,-64],'invisible':1,'ended':1},
 {'name':'terminal_after_hidden_toggle','data':[16,17,0,0,1,-64],'invisible':0,'ended':1},
 {'name':'terminal_prevented_by_visible_toggle','data':[16,17,0,0,1,63],'invisible':1,'ended':1},
 {'name':'pan_signed8_positive_wrap','data':[1,2,0,0,1,32767],'invisible':1,'ended':0},
 {'name':'pan_signed8_negative_wrap','data':[1,2,0,0,1,-32768],'invisible':1,'ended':0},
]

def load(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def declaration(source,name):
 m=re.search(r'^(?:static )?const (?:union (?:AnimCmd|AffineAnimCmd)(?: \*const| \* const)?|struct SpriteTemplate) '+re.escape(name)+r'(?:\[\])?\s*=\s*\{.*?\};',source,re.M|re.S)
 if not m:raise ValueError('Missing declaration '+name)
 return m[0]

def configurations():
 out=[]
 for kind in [0,1]:
  for actor in range(4):
   for target in range(4):
    out.append(dict(name=('blade' if kind==0 else 'sharpen')+f'_actor{actor}_target{target}',kind=kind,actor=actor,target=target,layout=0,raw=0,arg0=16,arg1=0,status=0))
 for kind in [0,1]:
  for actor in range(4):
   out.append(dict(name=('blade' if kind==0 else 'sharpen')+f'_reordered_actor{actor}',kind=kind,actor=actor,target=actor,layout=1,raw=0,arg0=-16,arg1=-4,status=0))
  out.append(dict(name=('blade' if kind==0 else 'sharpen')+'_raw',kind=kind,actor=0,target=1,layout=0,raw=1,arg0=16,arg1=0,status=0))
 for actor in range(4):
  out.append(dict(name=f'sharpen_status_actor{actor}',kind=1,actor=actor,target=actor^1,layout=0,raw=0,arg0=0,arg1=0,status=1))
 for actor in range(4):
  for index,y in enumerate([-32768,-32760,-32736,-32735,32767]):
   arg1=(y-COORDS[actor][1]+32768)%65536-32768
   out.append(dict(name=f'blade_signed_actor{actor}_y{y}',kind=0,actor=actor,target=actor^1,layout=0,raw=0,arg0=(-32768 if index&1 else 32767),arg1=arg1,status=0))
 return out

def build(repo,reference,helper,extractor,sanitize=True):
 ref=load('self_attack_reference',helper);ref.verify_reference(reference)
 extraction=load('self_attack_extraction',extractor)
 def function(source,name):
  # Reuse the bounded balanced-brace extractor; its accepted types omit s8.
  if name!='BattleAnimAdjustPanning':return extraction.function(source,name)
  extracted=extraction.function(source.replace('s8 BattleAnimAdjustPanning(', 's16 BattleAnimAdjustPanning('),name)
  return extracted.replace('s16 BattleAnimAdjustPanning(', 's8 BattleAnimAdjustPanning(',1)
 src={n:(reference/f'src/{n}.c').read_text() for n in ['sprite','battle_anim_mons','battle_anim','battle_anim_effects_1','battle_anim_effects_2']}
 sp=src['sprite'];mons=src['battle_anim_mons'];ba=src['battle_anim'];one=src['battle_anim_effects_1'];two=src['battle_anim_effects_2']
 header=(reference/'include/sprite.h').read_text();configs=configurations()
 frame_commands=[list(map(int,m)) for m in re.findall(r'ANIMCMD_FRAME\(([-0-9]+), ([-0-9]+)\)',declaration(one,'sSharpenSphereAnimCmds'))]
 affine_commands=[list(map(int,m)) for m in re.findall(r'AFFINEANIMCMD_FRAME\(([-0-9]+), ([-0-9]+), ([-0-9]+), ([-0-9]+)\)',declaration(two,'sSwordsDanceBladeAffineAnimCmds'))]
 assert len(frame_commands)==14 and len(affine_commands)==3
 ordinary=['AnimateSprites','AnimateSprite','CreateSprite','CreateSpriteAndAnimate','CreateSpriteAt','ResetSprite','DestroySprite',
 'AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','DecrementAnimDelayCounter',
 'AffineAnimDelay','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame','AffineAnimStateRestartAnim','AffineAnimStateStartAnim','AffineAnimStateReset',
 'ApplyAffineAnimFrameAbsolute','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame',
 'ApplyAffineAnimFrame','GetSpriteMatrixNum','AllocOamMatrix','InitSpriteAffineAnim','SetSpriteSheetFrameTileNum','SetSpriteOamFlipBits',
 'CopyOamMatrix','FreeOamMatrix','FreeSpriteOamMatrix','SpriteCallbackDummy']
 player=['BeginAnim','ContinueAnim','BeginAffineAnim','ContinueAffineAnim']
 callbacks=['AnimSwordsDanceBlade','RunStoredCallbackWhenAffineAnimEnds','AnimSwordsDanceBlade_Step','StartAnimLinearTranslation','AnimTranslateLinear_WithFollowup','DestroyAnimSprite','AnimSharpenSphere','AnimSharpenSphere_Step']
 funcs=[function(sp,n) for n in ordinary+player]
 funcs += [function(mons,n) for n in ['InitSpritePosToAnimAttacker','SetAnimSpriteInitialXOffset','GetBattlerSide','InitAnimLinearTranslation','AnimTranslateLinear','RunStoredCallbackWhenAffineAnimEnds','StartAnimLinearTranslation','AnimTranslateLinear_WithFollowup']]
 funcs += [function(two,n) for n in ['AnimSwordsDanceBlade','AnimSwordsDanceBlade_Step']]+[function(one,n) for n in ['AnimSharpenSphere','AnimSharpenSphere_Step']]+[function(ba,n) for n in ['BattleAnimAdjustPanning','DestroyAnimSprite']]
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
#define OAM_MATRIX_COUNT 32
#define ST_OAM_AFFINE_ON_MASK 1
#define ST_OAM_AFFINE_OFF 0
#define TAG_NONE 65535
#define TILE_SIZE_4BPP 32
#define FREE_SPRITE_TILE(i) abort()
#define B_SIDE_PLAYER 0
#define B_SIDE_OPPONENT 1
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define ANIM_TAG_SWORD 1
#define ANIM_TAG_SPHERE_TO_CUBE 2
#define ANIM_END 0xFFFF
#define AFFINE_ANIM_END 0x7FFF
#define sAnchorX data[6]
#define sAnchorY data[7]
#define SAFE_DIV(a,b) (assert((b)!=0),(a)/(b))
#define GET_BATTLER_SIDE2(b) (gBattlerPositions[(b)]&1)
_Static_assert((-1 >> 1)==-1,"arithmetic signed shifts");
_Static_assert((s16)32768==-32768 && (s16)-32769==32767,"GBA signed16 narrowing");
'''
 h+=header[header.index('struct AnimFrameCmd\n'):header.index('\nenum\n')]
 h+='\n'+re.search(r'^#define Q_8_8\(.*$',(reference/'include/global.h').read_text(),re.M)[0]+'\n'
 h+='\n'.join(re.findall(r'^#define sTransl_.*$',(reference/'include/battle_anim.h').read_text(),re.M))+'\n'
 h+='\n'.join(re.findall(r'^#define SOUND_PAN_(?:ATTACKER|TARGET)\s+.*$',(reference/'include/constants/battle_anim.h').read_text(),re.M))+'\n'
 h+=re.search(r'^#define SE_M_SWAGGER2\s+.*$',(reference/'include/constants/songs.h').read_text(),re.M)[0]+'\n'
 h+=r'''
struct Sprite;typedef void(*SpriteCallback)(struct Sprite*);
struct OamData{u8 affineMode,shape,size,matrixNum,paletteNum;u16 tileNum;};
struct SpriteFrameImage{const void*data;u16 size;};
struct SpriteTemplate{u16 tileTag,paletteTag;const struct OamData*oam;const union AnimCmd*const*anims;const struct SpriteFrameImage*images;const union AffineAnimCmd*const*affineAnims;SpriteCallback callback;};
struct Sprite{
 struct OamData oam;const union AnimCmd*const*anims;const struct SpriteFrameImage*images;const union AffineAnimCmd*const*affineAnims;const struct SpriteTemplate*template;SpriteCallback callback,stored;
 s16 x,y,x2,y2,data[8];u8 animNum,animCmdIndex,animLoopCounter,subpriority;u8 animDelayCounter:6;
 bool8 inUse,animEnded,animBeginning,animPaused,usingSheet,hFlip,vFlip,invisible,affineAnimBeginning,affineAnimEnded,affineAnimPaused,anchored;u16 sheetTileStart;
};
struct ObjAffineSrcData{s16 xScale,yScale;u16 rotation;};struct OamMatrix{s16 a,b,c,d;};
static struct Sprite gSprites[1],sDummySprite;static struct AffineAnimState sAffineAnimStates[32];static struct OamMatrix gOamMatrices[32];
static u32 gOamMatrixAllocBitmap;static bool8 gAffineAnimsDisabled;static u8 gBattleAnimAttacker,gBattleAnimTarget,gAnimVisualTaskCount,gBattlerPositions[4];static s16 gBattleAnimArgs[8];
static struct {struct {bool8 statusAnimActive;}healthBoxesData[4];} sBattleSpritesData,*gBattleSpritesDataPtr=&sBattleSpritesData;
static int caseId,tick,eventId,soundCount,invokedCallback=-1;
static void State(struct Sprite*s,int phase);
static void StoreSpriteCallbackInData6(struct Sprite*s,SpriteCallback cb){s->stored=cb;}
static void SetCallbackToStoredInData6(struct Sprite*s){s->callback=s->stored;}
'''
 h+='static const u8 coords[4][4]={'+','.join('{'+','.join(map(str,c))+'}' for c in COORDS)+'};\n'
 h+=r'''
static u8 GetBattlerSpriteCoord(u8 b,u8 c){assert(b<4&&c<4);return coords[gBattlerPositions[b]][c];}
static u8 GetBattlerSpriteCoord2(u8 b,u8 c){return GetBattlerSpriteCoord(b,c);}
static void PlaySE12WithPanning(u16 id,s8 pan){assert(id==SE_M_SWAGGER2);printf("A,%d,%d,%d,%d,%d,%d,%d\n",caseId,eventId++,tick,id,pan,++soundCount,invokedCallback);}
static void CalcCenterToCornerVec(struct Sprite*s,u8 a,u8 b,u8 c){(void)s;(void)a;(void)b;(void)c;}
static s16 AllocSpriteTiles(u8 n){abort();}
static u16 GetSpriteTileStartByTag(u16 n){assert(n==1||n==2);return 0;}
static u8 IndexOfSpritePaletteTag(u16 n){assert(n==1||n==2);return 0;}
static void RequestSpriteFrameImageCopy(u16 a,u16 b,const struct SpriteFrameImage*c){abort();}
static void UpdateSpriteMatrixAnchorPos(struct Sprite*s,s32 x,s32 y){abort();}
static void SetOamMatrix(u8 n,s16 a,s16 b,s16 c,s16 d){assert(n<32);gOamMatrices[n]=(struct OamMatrix){a,b,c,d};}
static void ObjAffineSet(const struct ObjAffineSrcData*s,struct OamMatrix*d,int n,int stride){assert(n==1&&stride==2&&s->rotation==0);*d=(struct OamMatrix){s->xScale,0,0,s->yScale};}
static void UnsupportedAffineLoop(u8 n,struct Sprite*s){abort();}
'''
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'
 h+=r'''
static void(*sAnimFuncs[])(struct Sprite*)={ContinueAnim,BeginAnim};
static void(*sAnimCmdFuncs[])(struct Sprite*)={AnimCmd_loop,AnimCmd_jump,AnimCmd_end,AnimCmd_frame};
static void(*sAffineAnimFuncs[])(struct Sprite*)={ContinueAffineAnim,BeginAffineAnim};
static void(*sAffineAnimCmdFuncs[])(u8,struct Sprite*)={UnsupportedAffineLoop,AffineAnimCmd_jump,AffineAnimCmd_end,AffineAnimCmd_frame};
'''
 for f in funcs:
  name=re.search(r'(\w+)\([^;]+?\)\n\{',f).group(1)
  if name in player+callbacks:h+='#define '+name+' Source'+name+'\n'+f+'\n#undef '+name+'\n'
  else:h+=f+'\n'
 for source,names in [(sp,['sDummyAnim','sDummyAffineAnim','gDummySpriteAnimTable','gDummySpriteAffineAnimTable']),(one,['sSharpenSphereAnimCmds','sSharpenSphereAnimTable']),(two,['sSwordsDanceBladeAffineAnimCmds','sSwordsDanceBladeAffineAnimTable'])]:
  for n in names:h+=declaration(source,n)+'\n'
 h+='static const struct OamData gOamData_AffineOff_ObjNormal_32x32={0,0,2},gOamData_AffineNormal_ObjBlend_32x64={1,2,3};\n'
 h+=declaration(one,'gSharpenSphereSpriteTemplate')+'\n'+declaration(two,'gSwordsDanceBladeSpriteTemplate')+'\n'
 h+='static SpriteCallback callbacks[]={SpriteCallbackDummy,'+','.join(callbacks)+'};\n'
 h+=r'''
static int CallbackId(SpriteCallback cb){if(!cb)return -1;for(unsigned i=0;i<sizeof(callbacks)/sizeof(callbacks[0]);i++)if(cb==callbacks[i])return i;abort();}
static void State(struct Sprite*s,int phase){
 int aff=(s->oam.affineMode&1)!=0;struct AffineAnimState*a=&sAffineAnimStates[0];
 int row[]={eventId++,tick,phase,invokedCallback,CallbackId(s->callback),CallbackId(s->stored),s->inUse,s->x,s->y,s->x2,s->y2,
 s->data[0],s->data[1],s->data[2],s->data[3],s->data[4],s->data[5],s->invisible,s->animBeginning,s->animEnded,s->animCmdIndex,s->animDelayCounter,s->oam.tileNum,
 s->affineAnimBeginning,s->affineAnimEnded,aff?a->animCmdIndex:0,aff?a->delayCounter:0,aff?a->xScale:256,aff?a->yScale:256,
 aff?gOamMatrices[0].a:256,aff?gOamMatrices[0].d:256,gAnimVisualTaskCount,(int)gOamMatrixAllocBitmap,soundCount};
 printf("S,%d",caseId);for(unsigned i=0;i<sizeof(row)/sizeof(row[0]);i++)printf(",%d",row[i]);puts("");
}
'''
 for n in player:
  h+=f'void {n}(struct Sprite*s){{Source{n}(s);State(s,{1 if n in ["BeginAnim","ContinueAnim"] else 2});}}\n'
 for i,n in enumerate(callbacks,1):
  static='static ' if n.startswith('AnimSwords') or n.startswith('AnimSharpen') else ''
  before='State(s,3);' if n=='DestroyAnimSprite' else ''
  h+=f'{static}void {n}(struct Sprite*s){{int previous=invokedCallback;invokedCallback={i};{before}Source{n}(s);State(s,{4 if n=="DestroyAnimSprite" else 0});invokedCallback=previous;}}\n'
 h+=r'''
static void Reset(void){
 memset(&sDummySprite,0,sizeof(sDummySprite));sDummySprite.callback=SpriteCallbackDummy;sDummySprite.x=304;sDummySprite.y=160;sDummySprite.subpriority=255;sDummySprite.anims=gDummySpriteAnimTable;sDummySprite.affineAnims=gDummySpriteAffineAnimTable;gSprites[0]=sDummySprite;
 memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));memset(&sBattleSpritesData,0,sizeof(sBattleSpritesData));
 for(int i=0;i<32;i++){AffineAnimStateReset(i);SetOamMatrix(i,256,0,0,256);}gOamMatrixAllocBitmap=0;eventId=soundCount=gAnimVisualTaskCount=0;tick=-1;invokedCallback=-1;
}
struct Config{int kind,actor,target,layout,raw,arg0,arg1,status;};
'''
 keys=['kind','actor','target','layout','raw','arg0','arg1','status']
 h+='static const struct Config configs[]={'+','.join('{'+','.join(str(c[k]) for k in keys)+'}' for c in configs)+'};\n'
 h+='static const u8 layouts[][4]={'+','.join('{'+','.join(map(str,c))+'}' for c in LAYOUTS)+'};\n'
 h+='static const s16 probes[][8]={'+','.join('{'+','.join(map(str,c['data']+[c['invisible'],c['ended']]))+'}' for c in PROBES)+'};\n'
 h+=r'''
int main(void){
 for(caseId=0;caseId<(int)(sizeof(configs)/sizeof(configs[0]));caseId++){
  Reset();const struct Config*c=&configs[caseId];gBattleAnimAttacker=c->actor;gBattleAnimTarget=c->target;memcpy(gBattlerPositions,layouts[c->layout],4);gBattleAnimArgs[0]=c->arg0;gBattleAnimArgs[1]=c->arg1;sBattleSpritesData.healthBoxesData[c->actor].statusAnimActive=c->status;
  printf("K,%d\n",caseId);const struct SpriteTemplate*t=c->kind==0?&gSwordsDanceBladeSpriteTemplate:&gSharpenSphereSpriteTemplate;
  u8 id=c->raw?CreateSprite(t,7,11,32):CreateSpriteAndAnimate(t,7,11,32);assert(id==0);gAnimVisualTaskCount++;State(&gSprites[0],5);
  for(tick=0;gSprites[0].inUse;tick++){assert(tick<1000);AnimateSprites();}
  assert(gAnimVisualTaskCount==0&&gOamMatrixAllocBitmap==0);printf("Z,%d,%d,%d\n",caseId,tick,soundCount);
 }
 int base=caseId;
 for(int p=0;p<(int)(sizeof(probes)/sizeof(probes[0]));p++){
  caseId=base+p;Reset();printf("P,%d\n",caseId);memcpy(gBattlerPositions,layouts[0],4);gBattleAnimAttacker=0;gBattleAnimTarget=1;
  assert(CreateSprite(&gSharpenSphereSpriteTemplate,7,11,32)==0);gAnimVisualTaskCount=1;struct Sprite*s=&gSprites[0];s->callback=AnimSharpenSphere_Step;memcpy(s->data,probes[p],6*sizeof(s16));s->invisible=probes[p][6];s->animEnded=probes[p][7];State(s,6);
  for(tick=0;tick<2&&s->inUse;tick++)s->callback(s);
  printf("Z,%d,%d,%d\n",caseId,tick,soundCount);
 }
}
'''
 with tempfile.TemporaryDirectory(prefix='self-attack-source-') as tmp:
  c=Path(tmp)/'self_attack.c';exe=Path(tmp)/'self_attack';c.write_text(h)
  flags=['-fsanitize=undefined','-fno-sanitize-recover=all'] if sanitize and os.name!='nt' else []
  ref.compile_c(['cc','-std=gnu11','-O0',*flags,str(c),'-o',str(exe)])
  output=subprocess.check_output([str(exe)],text=True,env={**os.environ,'UBSAN_OPTIONS':'halt_on_error=1:print_stacktrace=1'})
 cases={};probes={}
 for line in output.splitlines():
  kind,key,*values=line.split(',');key=int(key);values=list(map(int,values))
  if kind=='K':cases[key]={**configs[key],'states':[],'sounds':[]}
  elif kind=='P':probes[key]={**PROBES[key-len(configs)],'states':[],'sounds':[]}
  else:
   item=cases[key] if key in cases else probes[key]
   if kind=='S':assert len(values)==len(COLUMNS);item['states'].append(values)
   elif kind=='A':item['sounds'].append(values)
   elif kind=='Z':item['end']=values
   else:raise ValueError(kind)
 validate(cases,probes)
 # Keep every phase only for one canonical lifetime per family. Geometry
 # variants need initialization and teardown; raw-creation variants additionally
 # retain every command/callback change and sound boundary.
 for case in cases.values():
  if case['name'] in ['blade_actor0_target1','sharpen_actor0_target1']:continue
  if not case['raw']:
   case['states']=[r for r in case['states'] if r[1]==-1 or r[2] in [3,4] or (case['name'].startswith('blade_signed_') and r[2]==0 and r[3]==5)]
   continue
  end=case['end'][0];keep={-1,0,1,end-3,end-2,end-1}
  for sound in case['sounds']:keep|={sound[1]-1,sound[1],sound[1]+1}
  last=None
  for row in case['states']:
   key=(row[4],row[19],row[20],row[24],row[25])
   if last is not None and key!=last:keep|={row[1]-1,row[1],row[1]+1}
   last=key
  case['states']=[r for r in case['states'] if r[1] in keep or r[2] in [3,4]]
 return dict(sourceCommit=ref.COMMIT,columns=COLUMNS,phases=PHASES,callbacks=CALLBACKS,coordinateColumns=['x','y','x2','yPicOffset'],coordinates=COORDS,positionLayouts=LAYOUTS,
  soundColumns=['event','tick','soundId','pan','ordinal','invokedCallback'],endColumns=['nextTick','soundCount'],
  frameCommands=frame_commands,affineCommands=affine_commands,
  notes=['Tick -1 uses exact CreateSpriteAndAnimate (initializer, frame, affine); raw cases defer initialization to tick0. Script visual count increments after eager creation.',
   'Each exact AnimateSprites pass invokes the callback before frame then affine. Begin/Continue wrappers only observe returned state. StartAnimLinearTranslation immediately calls its new linear callback; both returns are recorded.',
   'Stored callbacks use a host pointer field instead of GBA data6/data7 pointer halves. Shared wait and translation followups still use exact source and install the stored callback without executing it.',
   'Coordinates are explicit harness inputs by source position; Base Y and Y-picture differ intentionally; X2 matches source X. GetBattlerSide and BattleAnimAdjustPanning are exact source, using the selected battler-position mapping.',
   'Source frame and affine command tables are extracted, tile/palette bases normalized to zero, and the zero-rotation BIOS adapter writes the exact diagonal result. Unsupported BIOS rotation aborts.',
   'Callback-only probes execute two callbacks at most, with no frame player. Signed16 narrowing follows GBA arithmetic; pre-increment is compared after narrowing. Probes are not ordinary animation initial states.',
   'Only canonical actor0/target1 lifetimes retain every player phase; geometry variants retain initialization and teardown plus every sound, and raw variants additionally retain frame/affine/callback changes and sound boundaries. No RNG function is used.',
   'The blade destination is y minus32; exact source fixed-point velocity1365 reaches y2 minus31 after six steps without snapping to its target.'],
  sanitizer='undefined; no-recover; halt_on_error' if sanitize and os.name!='nt' else 'not enabled',cases=cases,probes=probes)

def validate(cases,probes):
 assert len(cases)==66 and len(probes)==14
 C={n:i for i,n in enumerate(COLUMNS)}
 def val(r,n):return r[C[n]]
 for case in cases.values():
  rows=case['states'];kind=case['kind'];raw=case['raw'];p=LAYOUTS[case['layout']][case['actor']];t=LAYOUTS[case['layout']][case['target']]
  init=next(r for r in rows if val(r,'phase')==0 and val(r,'invokedCallback')==(1 if kind==0 else 7))
  if kind==0:
   x=COORDS[p][0];tx=COORDS[t][0];x+=case['arg0']*(1 if x<tx or (x==tx and not(p&1)) else -1)
   narrow=lambda n:(n+32768)%65536-32768
   assert [val(init,'x'),val(init,'y')]==[narrow(x),narrow(COORDS[p][1]+case['arg1'])]
   assert not case['sounds'] and case['end']==[55+raw,0]
   first_end=next(r for r in rows if val(r,'affineEnded'))
   assert [val(first_end,'tick'),val(first_end,'phase')]==[44+raw,2]
   blade_step=next(r for r in rows if val(r,'phase')==0 and val(r,'invokedCallback')==3)
   assert [val(blade_step,'tick'),val(blade_step,'callback'),val(blade_step,'storedCallback'),val(blade_step,'data0')]==[46+raw,4,6,6]
   assert val(blade_step,'data4')==narrow(val(blade_step,'y')-32)
   first_affine=next(r for r in rows if val(r,'phase')==2)
   assert [val(first_affine,'scaleX'),val(first_affine,'scaleY'),val(first_affine,'matrixA'),val(first_affine,'matrixD')]==[16,256,4096,256]
  else:
   assert [val(init,'x'),val(init,'y')]==[COORDS[p][2],COORDS[p][3]-12]
   assert val(init,'data5')==(-64 if not(p&1) else 63)
   assert all(s[2]==187 and s[3]==val(init,'data5') and s[5]==8 for s in case['sounds'])
   assert [s[1] for s in case['sounds']]==[t+raw for t in [9,27,53,87,129,179,237]]
   assert case['end']==[287+raw,7]
   first_end=next(r for r in rows if val(r,'frameEnded'))
   assert [val(first_end,'tick'),val(first_end,'phase')]==[191+raw,1]
   # Every frame command including repeated tile0 is retained in the native
   # observation stream before compacting geometry variants.
   frame_rows=[r for r in rows if val(r,'phase')==1]
   seen=[]
   for row in frame_rows:
    if not seen or val(row,'frameCommand')!=val(seen[-1],'frameCommand'):seen.append(row)
   assert [val(r,'frameCommand') for r in seen]==list(range(14))
   assert [val(r,'tick') for r in seen]==[t+raw for t in [-1,17,23,41,47,53,71,77,83,101,107,113,131,137]]
  dead=next(r for r in rows if val(r,'phase')==3)
  assert val(dead,'active')==1 and rows[-1][6]==0 and rows[-1][31:33]==[0,0]
  if kind==0:
   assert val(dead,'tick')==54+raw
   wrapped_destination=val(init,'y')< -32736
   expected_motion=[37,74,111,149,186,223] if wrapped_destination else [-5,-10,-15,-21,-26,-31]
   assert val(dead,'y2')==expected_motion[-1]
   moved=[r for r in rows if val(r,'phase')==0 and val(r,'invokedCallback')==5 and val(r,'data0')<6]
   assert [val(r,'y2') for r in moved[:6]]==expected_motion
  else:
   assert val(dead,'frameEnded')==1 and val(dead,'data1')==17 and val(dead,'invisible')==1 and val(dead,'tick')==286+raw
   assert val(dead,'data4')==15
  # A live pass runs callback, then frame, then affine. The only extra callback
  # return is Start's eager linear step; destruction suppresses both players.
  for t in range(-1+raw,case['end'][0]-1):
   at=[r for r in rows if val(r,'tick')==t and val(r,'phase')!=5]
   assert [val(r,'phase') for r in at] in [[0,1,2],[0,0,1,2]]
  assert not any(val(r,'phase') in [1,2] for r in rows if val(r,'tick')==case['end'][0]-1)
 # Distinguish wrapping counters, death conjunction, and sound parity.
 byname={p['name']:p for p in probes.values()}
 first=lambda n:next(r for r in byname[n]['states'] if r[1]==0 and r[2]==0)
 assert first('elapsed_wrap')[11]==-32768
 assert first('interval_wrap')[12]==-32768
 assert first('toggle_pair_wrap')[13]==-32768
 assert first('visible_count_wrap')[15]==-32768 and len(byname['visible_count_wrap']['sounds'])==1
 assert first('negative_visible_count')[15]==0 and len(byname['negative_visible_count']['sounds'])==1
 for n in ['ended_required','interval_strictly_above16','invisible_required','terminal_prevented_by_visible_toggle']:assert byname[n]['states'][-1][6]==1
 for n in ['already_terminal','terminal_after_hidden_toggle']:assert byname[n]['states'][-1][6]==0
 assert byname['pan_signed8_positive_wrap']['sounds'][0][3]==-1
 assert byname['pan_signed8_negative_wrap']['sounds'][0][3]==0

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--repo',type=Path,default=Path.cwd());p.add_argument('--reference',type=Path);p.add_argument('--helper',type=Path);p.add_argument('--extractor',type=Path);p.add_argument('--output',type=Path,default=Path(__file__).with_name('self-attack-source-vectors.json'));p.add_argument('--no-sanitize',action='store_true');a=p.parse_args();repo=a.repo.resolve()
 result=build(repo,a.reference or repo/'content/raw/pokefirered/037335f4',a.helper or repo/'tests/Reference/reference_c.py',a.extractor or repo/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py',not a.no_sanitize)
 a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');print(f'{a.output}: {len(result["cases"])} cases, {len(result["probes"])} probes, {a.output.stat().st_size} bytes; {result["sanitizer"]}')

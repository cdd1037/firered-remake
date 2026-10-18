#!/usr/bin/env python3
"""Pinned cure callbacks + native frame/affine/linear players; no upstream scripts.
Default fixture keeps explicit complete branch cases. --full uses the same comparator.
"""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile
from pathlib import Path
KINDS=['gAromatherapySmallFlowerSpriteTemplate','gAromatherapyBigFlowerSpriteTemplate','gSparklingStarsSpriteTemplate','gBlendThinRingExpandingSpriteTemplate','gBellSpriteTemplate','gHealBellMusicNoteSpriteTemplate']
# Seven actual callback words plus hidden-partner probe. Every selected case retains every frame.
CONFIGS=[[[24,16,0,2,2,0,0,0],[64,24,0,3,1,1,0,0],[48,12,0,4,3,1,0,0],[100,16,0,3,2,0,0,0],[74,24,180,3,2,0,0,0],[80,30,0,4,1,1,0,0],[128,12,0,3,3,0,0,0],[32767,-32768,255,255,255,2,1,0],[-32768,32767,64,1,1,3,1,0]],
 [[16,24,0,2,1,0,0,0],[90,16,0,2,1,0,0,0],[-17,31,255,3,7,2,1,0]],
 [[-15,0,0,0,32,60,1,0],[12,-5,0,0,32,60,1,0],[-32768,32767,1,-32768,32767,1,0,0],[5,-3,0,7,-9,0,1,0],[5,-3,0,7,-9,2,1,1]],
 [[0,0,0,1,0,0,0,0],[5,-3,0,0,0,0,0,0],[-17,8,1,1,0,0,0,0],[5,-3,0,0,0,0,0,1]],
 [[0,-24,0,1,0,0,0,0]],
 [[0,-24,x,y,d,a,b,0] for x,y,d,a,b in [(48,-18,35,0,0),(-48,20,30,1,1),(-38,-29,30,2,2),(36,18,30,3,3),(19,26,35,4,4),(-34,-12,30,5,5),(41,-20,34,6,2),(-15,26,32,7,0),(-48,18,31,0,2),(48,-20,30,2,5),(38,29,33,4,3),(-36,-18,30,6,1)]]+[[32767,-32768,-32768,32767,1,7,5,0]]]
COLUMNS=['tick','active','x','y','x2','y2','tile','hflip','vflip','priority','a','b','c','d','affine','doubleSize','palette','arg0','arg2','frameEnded','affineEnded','remaining','angle','age']
def module(name,p):
 s=importlib.util.spec_from_file_location(name,p);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def build_sprite_source(root):
 raw=root/'content/raw/pokefirered/037335f4';base=module('cure_orbit_base',root/'tests/Game.Presentation.Tests/Reference/generate_orbit_spray_vectors.py')
 with tempfile.TemporaryDirectory() as d:
  p=Path(d)/'base.c';base.build(root,'tasks',p);h=p.read_text().split('static const long long config0')[0]
 source={n:(raw/('src/'+n+'.c')).read_text() for n in ['battle_anim_mons','battle_anim_effects_1','battle_anim_effects_2']}
 functions={'battle_anim_mons':['InitSpritePosToAnimAttacker','InitSpritePosToAnimTarget','SetAnimSpriteInitialXOffset','SetSpriteCoordsToAnimAttackerCoords','DestroySpriteAndMatrix','SetAverageBattlerPositions','AnimSpriteOnMonPos','TranslateSpriteLinearFixedPoint','StartAnimLinearTranslation','InitAnimLinearTranslation','AnimTranslateLinear','AnimTranslateLinear_WithFollowup'],
 'battle_anim_effects_1':['AnimFlyingParticle','AnimFlyingParticle_Step','AnimSparklingStars'], 'battle_anim_effects_2':['AnimBlendThinRing','SetMusicNotePalette','AnimHealBellMusicNote']}
 fs=[base.function(source[file],name) for file,names in functions.items() for name in names]
 h=h.replace('u8 affineMode,matrixNum,shape,size,paletteNum,objMode;','u8 affineMode,matrixNum,shape,size,paletteNum,objMode;u8 priority:2;')
 h=h.replace('static bool8 IsBattlerSpriteVisible(u8 b){assert(b<slots);return TRUE;}','static int hiddenPartner;static bool8 IsBattlerSpriteVisible(u8 b){return b<slots&&!(hiddenPartner&&b==(gBattleAnimAttacker^2));}')
 h=h.replace('static u8 IndexOfSpritePaletteTag(u16 tag){return tag==ANIM_TAG_LEAF?paletteSlots[0]:tag==ANIM_TAG_RAZOR_LEAF?paletteSlots[1]:0;}','static u8 IndexOfSpritePaletteTag(u16 tag){return tag==10206?4:tag==9999?5:tag==9998?6:tag==ANIM_TAG_LEAF?paletteSlots[0]:tag==ANIM_TAG_RAZOR_LEAF?paletteSlots[1]:7;}')
 h+=r'''
#define BATTLE_PARTNER(n) ((n)^2)
#define ANIM_TAG_FLOWER 10160
#define ANIM_TAG_BELL 10205
#define ANIM_TAG_MUSIC_NOTES_2 10206
#define ANIM_TAG_SPARKLE_2 10035
#define ANIM_TAG_THIN_RING 10203
#define ANIM_SPRITES_START 10000
#define NUM_MUSIC_NOTE_PAL_TAGS 3
#define gDummySpriteAnimTable dummyFrames
#define gDummySpriteAffineAnimTable dummyAffines
static int IsDoubleBattle(void){return mode;}
static u8 GetBattlerSpriteBGPriority(u8 b){return (u8[]){1,0,0,1}[b];}
static const struct OamData gOamData_AffineOff_ObjNormal_8x8={.priority=2},gOamData_AffineNormal_ObjNormal_16x16={.size=1,.affineMode=1,.priority=2},gOamData_AffineOff_ObjNormal_32x32={.size=2,.priority=2},gOamData_AffineDouble_ObjBlend_64x64={.size=3,.affineMode=3,.objMode=1,.priority=2},gOamData_AffineOff_ObjNormal_16x16={.size=1,.priority=2};
'''
 h+=re.search(r'const u16 gMusicNotePaletteTagsTable\[NUM_MUSIC_NOTE_PAL_TAGS\] =\s*\{.*?\};',source['battle_anim_effects_2'],re.S)[0]+'\n'
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in fs)+'\n'
 for file,prefixes in [('battle_anim_effects_1',['sPetalDanceBigFlower','sPetalDanceSmallFlower','sAromatherapyBigFlower','sGrantingStars']),('battle_anim_effects_2',['sThinRingExpanding','sBell'])]:
  for prefix in prefixes:
   snippets=re.findall(r'static const union (?:AffineAnimCmd|AnimCmd) (?:\*const )?'+prefix+r'\w*\[\]\s*=\s*\{.*?\};',source[file],re.S)
   assert snippets,prefix;h+='\n'.join(snippets)+'\n'
 for i,name in enumerate(KINDS):
  file='battle_anim_effects_1' if i<3 else 'battle_anim_effects_2';h+=re.search(r'const struct SpriteTemplate '+name+r'\s*=\s*\{.*?\};',source[file],re.S)[0]+'\n'
 h+='\n'.join(fs)+'\n'
 for k,configs in enumerate(CONFIGS):h+='static const int cureConfig'+str(k)+'[][8]={'+','.join('{'+','.join(map(str,c))+'}' for c in configs)+'};\n'
 h+='static const int(*cureConfigs[])[8]={'+','.join('cureConfig'+str(i) for i in range(len(CONFIGS)))+'};\nstatic const int counts[]={'+','.join(map(str,map(len,CONFIGS)))+'};\nstatic const struct SpriteTemplate*templates[]={'+','.join('&'+n for n in KINDS)+'};\n'
 h+=r'''
static void emit(int kind,int cfg,int tick,int id){struct Sprite*s=&gSprites[id];struct OamMatrix m=(s->oam.affineMode&1)?gOamMatrices[s->oam.matrixNum]:(struct OamMatrix){256,0,0,256};int row[]={tick,s->inUse,s->x,s->y,s->x2,s->y2,s->oam.tileNum,(s->oam.matrixNum>>3)&1,(s->oam.matrixNum>>4)&1,s->oam.priority,m.a,m.b,m.c,m.d,s->oam.affineMode&1,s->oam.affineMode==3,s->oam.paletteNum,gBattleAnimArgs[0],gBattleAnimArgs[2],s->animEnded,s->affineAnimEnded,s->data[0],s->data[0],s->data[7]};if(row[14])row[7]=row[8]=0;printf("%d:%d:%d:%d:%d",kind,mode,gBattleAnimAttacker,gBattleAnimTarget,cfg);for(unsigned i=0;i<sizeof(row)/sizeof(row[0]);i++)printf(",%d",row[i]);puts("");}
static void run(int k,int a,int t,int cfg){
 slots=mode?4:2;gBattleAnimAttacker=a;gBattleAnimTarget=t;hiddenPartner=cureConfigs[k][cfg][7];memset(gSprites,0,sizeof(gSprites));memset(sAffineAnimStates,0,sizeof(sAffineAnimStates));memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));gOamMatrixAllocBitmap=(1u<<slots)-1;gAnimVisualTaskCount=0;for(int i=0;i<32;i++)SetOamMatrix(i,256,0,0,256);
 for(int i=0;i<slots;i++){struct Sprite*s=&gSprites[i];s->inUse=TRUE;s->callback=SpriteCallbackDummy;s->anims=dummyFrames;s->affineAnims=dummyAffines;s->animPaused=s->affineAnimPaused=TRUE;}
 for(int i=0;i<7;i++)gBattleAnimArgs[i]=cureConfigs[k][cfg][i];int id=CreateSpriteAndAnimate(templates[k],GetBattlerSpriteCoord(t,2),GetBattlerSpriteCoord(t,3),GetBattlerSpriteSubpriority(a)-2);gAnimVisualTaskCount++;emit(k,cfg,-1,id);
 for(int tick=0;tick<1000;tick++){AnimateSprites();emit(k,cfg,tick,id);if(!gSprites[id].inUse){assert(gAnimVisualTaskCount==0);return;}}abort();}
int main(void){for(int k=0;k<6;k++)for(mode=0;mode<2;mode++)for(int a=0;a<(mode?4:2);a++)for(int t=0;t<(mode?4:2);t++)for(int c=0;c<counts[k];c++)run(k,a,t,c);}
'''
 return h

def build_mask_source(root):
 raw=root/'content/raw/pokefirered/037335f4';ex=module('cure_extract',root/'tests/Game.Presentation.Tests/Reference/generate_fake_out_vectors.py');src=(raw/'src/battle_anim_utility_funcs.c').read_text();mons=(raw/'src/battle_anim_mons.c').read_text();types=(raw/'include/gba/types.h').read_text()
 fs=[ex.function(src,n) for n in ['StartMonScrollingBgMask','UpdateMonScrollingBgMask']]+[ex.function(mons,'CreateInvisibleSpriteCopy')]
 h=r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include "gba/io_reg.h"
typedef uint8_t u8;typedef uint16_t u16;typedef uint32_t u32;typedef int16_t s16;typedef int32_t s32;typedef u8 bool8;
#define TRUE 1
#define FALSE 0
#define B_SIDE_PLAYER 0
#define BATTLE_PARTNER(n) ((n)^2)
#define MON_DATA_SPECIES 11
#define BG_PLTT_ID(p) ((p)*16)
#define PLTT_SIZE_4BPP 32
#define ST_OAM_OBJ_WINDOW 2
'''+types[types.index('struct BgCnt\n'):types.index('typedef volatile struct BgCnt vBgCnt;')+len('typedef volatile struct BgCnt vBgCnt;')]+r'''
struct Sprite {s16 x,y,x2,y2;int invisible,usingSheet;struct{int priority,objMode,tileNum,matrixNum,affineMode;}oam;void(*callback)(struct Sprite*);}gSprites[64];
struct Task {s16 data[16];void(*func)(u8);}gTasks[1];
struct BattleAnimBgData {int bgId,paletteId,tilesOffset,bgTilemap;};
static u16 gBattle_WIN0H,gBattle_WIN0V,gBattle_BG1_X,gBattle_BG1_Y,registers[128];static int alive,includeVisible,live[64],copies,erased,clears,mode,actor,include;static u8 gBattlerSpriteIds[]={0,1,2,3},gBattlerPartyIndexes[4];static int gEnemyParty[6],gPlayerParty[6];
static int IsContest(void){return 0;}static int IsBattlerSpriteVisible(int b){return mode&&includeVisible;}static int GetBattlerSide(int b){return b&1;}static int GetMonData(int*p,int field){return 1;}
static void SpriteCallbackDummy(struct Sprite*s){}static u8 CreateInvisibleSpriteWithCallback(void(*cb)(struct Sprite*)){for(int i=4;i<64;i++)if(!live[i]){live[i]=1;copies++;return i;}assert(0);return 64;}
static void DestroySprite(struct Sprite*s){int i=s-gSprites;assert(i>=4&&live[i]);live[i]=0;erased++;}
static u16 GetGpuReg(int offset){return registers[offset/2];}static void SetGpuReg(int offset,u16 value){registers[offset/2]=value;}static void SetGpuRegBits(int offset,u16 value){registers[offset/2]|=value;}
static void GetBattleAnimBg1Data(struct BattleAnimBgData*p){*p=(struct BattleAnimBgData){1,8,512,0};}
static void AnimLoadCompressedBgTilemap(int id,const void*p){assert(id==1);}static void RelocateBattleBgPal(int p,int m,int x,int y){assert(0);}static void AnimLoadCompressedBgGfx(int id,const void*p,int offset){assert(id==1&&offset==512);}static void LoadCompressedPalette(const void*p,int at,int size){assert(at==128&&size==32);}static void ResetBattleAnimBg(int id){assert(id==0);clears++;}static void DestroyAnimVisualTask(u8 id){alive=0;}
'''
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in fs)+'\n'+'\n'.join(fs)+r'''
static void emit(int tick){printf("%d:%d:%d:%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",mode,actor,include,includeVisible,tick,alive,gBattle_BG1_X,gBattle_BG1_Y,registers[REG_OFFSET_DISPCNT/2],registers[REG_OFFSET_WININ/2],registers[REG_OFFSET_WINOUT/2],registers[REG_OFFSET_BLDCNT/2],registers[REG_OFFSET_BLDALPHA/2],registers[REG_OFFSET_BG1CNT/2],copies,erased,clears,gBattle_WIN0H);for(int i=0;i<16;i++)printf(",%d",gTasks[0].data[i]);puts("");}
int main(void){for(mode=0;mode<2;mode++)for(actor=0;actor<(mode?4:2);actor++)for(include=0;include<2;include++)for(includeVisible=0;includeVisible<2;includeVisible++){
 memset(gTasks,0,sizeof gTasks);memset(registers,0,sizeof registers);memset(live,0,sizeof live);memset(gSprites,0,sizeof gSprites);gBattle_WIN0H=gBattle_WIN0V=gBattle_BG1_X=gBattle_BG1_Y=0;copies=erased=clears=0;alive=1;
 for(int i=0;i<4;i++){gSprites[i].x=i*20;gSprites[i].y=80;gSprites[i].oam.matrixNum=i;gSprites[i].oam.tileNum=16*i;gSprites[i].oam.affineMode=1;}
 StartMonScrollingBgMask(0,0,0x1a0,actor,include,10,2,30,0,0,0);emit(-1);for(int tick=0;alive&&tick<100;tick++){gTasks[0].func(0);emit(tick);}assert(!alive&&erased==copies&&clears==1);
 }return 0;}
'''
 return h

def compile_rows(root,h,name,out):
 ref=module('cure_reference',root/'tests/Reference/reference_c.py');ref.verify_reference(root/'content/raw/pokefirered/037335f4');p=out/(name+'.c');p.write_text(h);outputs=[]
 for opt in ['-O0','-O2']:
  exe=out/(name+opt);ref.compile_c(['cc','-std=c11',opt,'-fwrapv','-I'+str(root/'content/raw/pokefirered/037335f4/include'),str(p),'-o',str(exe)]);outputs.append(subprocess.check_output([str(exe)],text=True))
 assert outputs[0]==outputs[1],name+' source O0/O2 differs';return outputs[0]

def build(root,out,full=False):
 out.mkdir(parents=True,exist_ok=True);sprites={};masks={}
 for line in compile_rows(root,build_sprite_source(root),'cure-sprites',out).splitlines():
  key,*values=line.split(',');row=list(map(int,values));assert len(row)==len(COLUMNS);sprites.setdefault(key,[]).append(row)
 for line in compile_rows(root,build_mask_source(root),'cure-mask',out).splitlines():
  key,*values=line.split(',');masks.setdefault(key,[]).append(list(map(int,values)))
 full_count=len(sprites);selected=[]
 # Every authored/boundary configuration; mirrored first case; all doubles actor/target pair classes and hidden partners.
 for k,configs in enumerate(CONFIGS):
  selected += [f'{k}:0:0:1:{i}' for i in range(len(configs))]
  selected += [f'{k}:0:1:0:0']+[f'{k}:1:{a}:{t}:0' for a,t in [(0,0),(0,1),(0,2),(0,3),(1,0),(1,1),(1,2),(1,3),(2,0),(2,1),(2,2),(2,3),(3,0),(3,1),(3,2),(3,3)]]
  selected += [f'{k}:1:{a}:{a^1}:{i}' for i,c in enumerate(configs) if c[7] for a in range(4)]
 assert len(selected)==len(set(selected)) and set(selected)<=sprites.keys()
 if not full:sprites={k:sprites[k] for k in selected}
 assert all(rows[0][0]==-1 and rows[-1][1]==0 for rows in sprites.values());assert all(len(rows)==91 and rows[-1][1]==0 for rows in masks.values())
 return dict(configs=CONFIGS,templates=KINDS,columns=COLUMNS,cases=sprites,maskCases=masks,profile='full' if full else 'representative',coverage=dict(fullCaseCount=full_count,caseCount=len(sprites),spriteRows=sum(map(len,sprites.values())),maskCases=len(masks),maskRows=sum(map(len,masks.values()))),notes=['Whole selected cases retain every callback/frame row. Source frame/affine and linear interpreters extracted from pinned local C.','Mask row: tick,active,x,y,dispcnt,winin,winout,bldcnt,bldalpha,bg1cnt,copies,destroyed,clears,win0h,data0..15. Initialized raw mask copies share battler matrices; no matrix allocation/free.','The source music palette has80 colors. LoadMusicNotesPals consumes only first48 via NUM_MUSIC_NOTE_PAL_TAGS=3.'])
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output-dir',type=Path,required=True);p.add_argument('--full',action='store_true');a=p.parse_args();dest=a.output_dir/'cure-family-source-vectors.json'
 if dest.exists():p.error('Refusing to overwrite existing native vectors')
 result=build(a.root,a.output_dir,a.full);dest.write_text(json.dumps(result,separators=(',',':'))+'\n');print(result['coverage'])

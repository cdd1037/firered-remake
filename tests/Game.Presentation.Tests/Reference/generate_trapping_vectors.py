#!/usr/bin/env python3
"""Pinned original-C Mean Look/Block callbacks and native sprite affine scheduler.

Tick -1 models CreateSpriteAndAnimate: callback, then AnimateSprite. Later ticks
execute original AnimateSprites. Pikachu coordinates use source layout, parsed
species offsets and native GetBattlerSpriteFinal_Y. BIOS ObjAffineSet is bounded
to these programs' zero rotation; native ConvertScaleParam supplies exact scales.
No product or managed compiler dependency. reference_c.py selects cc or MSVC.
"""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile
from pathlib import Path
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def build(root):
 ref=module('reference_c',root/'tests/Reference/reference_c.py');reference=root/'content/raw/pokefirered/037335f4';ref.verify_reference(reference)
 for file,digest in {'src/sprite.c': '2a804302eb5a89d31c3ec2f33dc645a80a0c645113d4b33b9bd571064c0286ae', 'src/battle_anim_effects_3.c': 'bdf8ba956af35ca232d7e5101090e2ff31a35f0ae91286a21b216e7787ba6c1f', 'src/battle_anim_mons.c': 'fcb002acc48b8cd6b8cf8b7eddcb3034d721ff5887eb6bdeedfe183b7b3908bb', 'src/battle_anim.c': '5883d9ee0483120ef67461952f5880499f322fb1c706cc213ca13c0c2b0e88c5', 'src/trig.c': '4144065a90e5376ce0259796a01ebaae33c6cc6c7ef716de95b9d9a13c85a810', 'include/sprite.h': 'a77aa1c837dfb58cf60b3eac299c7cc29a23ce27735ba710b9e8eeed102bf773', 'src/data/pokemon_graphics/front_pic_coordinates.h': '81a9024f3aa28ab750e6f817367d525e3a97233c981ec05e23850a971c54989f', 'src/data/pokemon_graphics/back_pic_coordinates.h': 'a8d7ae72ebf462ca1a5915bc4f9a77d0ee2c13d4e9a4d9f2f66dd947256b9fab', 'src/data/pokemon_graphics/enemy_mon_elevation.h': 'bab9380267270b2eb30ac50cc643bc0e3fc684fa0195e85be0dc97fc9d28f7e1'}.items():
  assert hashlib.sha256((reference/file).read_bytes()).hexdigest()==digest,file
 extract=module('extract',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py');function=extract.function
 sprite=(reference/'src/sprite.c').read_text();mons=(reference/'src/battle_anim_mons.c').read_text();bug=(reference/'src/battle_anim_effects_3.c').read_text();battle=(reference/'src/battle_anim.c').read_text();header=(reference/'include/sprite.h').read_text();trig=(reference/'src/trig.c').read_text()
 names=['AnimateSprites','AnimateSprite','BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','DecrementAnimDelayCounter','BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame','AffineAnimStateRestartAnim','AffineAnimStateReset','ApplyAffineAnimFrameAbsolute','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame','ApplyAffineAnimFrame','StartSpriteAffineAnim','AffineAnimStateStartAnim']
 funcs=[function(sprite,n) for n in names]+[function(bug,n) for n in ['AnimMeanLookEye','AnimMeanLookEye_Step1','AnimMeanLookEye_Step2','AnimMeanLookEye_Step3','AnimMeanLookEye_Step4','AnimBlockX','AnimBlockX_Step']]+[function(mons,n) for n in ['GetBattlerSpriteSubpriority','GetBattlerSpriteFinal_Y']]+[function(battle.replace('s8 BattleAnimAdjustPanning','s16 BattleAnimAdjustPanning'),'BattleAnimAdjustPanning').replace('s16 BattleAnimAdjustPanning','s8 BattleAnimAdjustPanning')]
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
'''
 h+=header[header.index('struct AnimFrameCmd\n'):header.index('\nenum\n')]+r'''
struct Sprite;
typedef void (*SpriteCallback)(struct Sprite *);
struct Sprite {
 u8 subpriority,animCmdIndex,animLoopCounter,animNum;u8 animDelayCounter:6;
 bool8 invisible,inUse,animEnded,animBeginning,animPaused,usingSheet,hFlip,vFlip;
 bool8 affineAnimBeginning,affineAnimEnded,affineAnimPaused,anchored;
 int sheetTileStart,images; s16 x,y,x2,y2,data[8];
 struct {u8 affineMode;int tileNum;} oam;
 const union AnimCmd *const *anims;const union AffineAnimCmd *const *affineAnims;
 SpriteCallback callback,storedCallback;
};
struct ObjAffineSrcData {s16 xScale,yScale;u16 rotation;};
struct OamMatrix {s16 a,b,c,d;};
static struct Sprite gSprites[MAX_SPRITES];static struct AffineAnimState sAffineAnimStates[1];static bool8 gAffineAnimsDisabled;
static int writes;static struct OamMatrix currentMatrix;
static void SetSpriteOamFlipBits(struct Sprite*s,u8 h,u8 v){s->hFlip=h;s->vFlip=v;}
static void RequestSpriteFrameImageCopy(int a,int b,int c){abort();}
static void StoreSpriteCallbackInData6(struct Sprite*s,SpriteCallback cb){s->storedCallback=cb;s->data[6]=0x1234;s->data[7]=0x5678;}
static void SetCallbackToStoredInData6(struct Sprite*s){s->callback=s->storedCallback;}
static void DestroyAnimSprite(struct Sprite*s){s->inUse=FALSE;}
static u8 GetSpriteMatrixNum(struct Sprite*s){return 0;}
static void UpdateSpriteMatrixAnchorPos(struct Sprite*s,s32 x,s32 y){abort();}
static void CopyOamMatrix(u8 n,struct OamMatrix*m){assert(n==0);currentMatrix=*m;}
static void ObjAffineSet(const struct ObjAffineSrcData*s,struct OamMatrix*d,int n,int stride){assert(n==1&&stride==2&&s->rotation==0);d->a=s->xScale;d->b=0;d->c=0;d->d=s->yScale;writes++;}
static void UnsupportedAffineLoop(u8 n,struct Sprite*s){abort();}
'''
 h+=r"""
#define B_SIDE_PLAYER 0
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define REG_OFFSET_BLDCNT 0
#define REG_OFFSET_BLDALPHA 1
#define BLDCNT_TGT2_ALL 0x3f00
#define BLDCNT_EFFECT_BLEND 0x40
#define BLDALPHA_BLEND(a,b) ((a)|((b)<<8))
#define BATTLE_PARTNER(n) ((n)^2)
#define sTransl_Speed data[0]
#define sTransl_Duration data[0]
#define sTransl_InitX data[1]
#define sTransl_DestX data[2]
#define sTransl_InitY data[3]
#define sTransl_DestY data[4]
#define Q_8_8(x) ((s16)((x)*256))
s16 gBattleAnimArgs[8];u8 gBattleAnimAttacker,gBattleAnimTarget;
static int mode,bldcnt,alpha,sound,pan;
#define B_SIDE_OPPONENT 1
#define B_POSITION_PLAYER_LEFT 0
#define B_POSITION_OPPONENT_LEFT 1
#define B_POSITION_PLAYER_RIGHT 2
#define B_POSITION_OPPONENT_RIGHT 3
#define DISPLAY_HEIGHT 160
#define MON_PIC_HEIGHT 64
#define SOUND_PAN_ATTACKER -64
#define SOUND_PAN_TARGET 63
#define SE_M_SKETCH 198
#define SE_M_LEER 185
#define IS_DOUBLE_BATTLE() mode
static struct {struct {int statusAnimActive;} healthBoxesData[4];} battleData,*gBattleSpritesDataPtr=&battleData;
static u8 GetBattlerPosition(u8 n){return n;}
static u8 GetBattlerSide(u8 n){return n&1;}
static u8 GetBattlerYDelta(u8 n,u16 species){return n&1?PIKACHU_FRONT:PIKACHU_BACK;}
static u8 GetBattlerElevation(u8 n,u16 species){return PIKACHU_ELEVATION;}
static u8 GetBattlerSpriteFinal_Y(u8,u16,bool8);
static u8 GetBattlerSpriteCoord(u8 n,u8 c);
static void PlaySE12WithPanning(int se,int p){sound=se;pan=p;}
static void SetGpuReg(int reg,int value){if(reg==REG_OFFSET_BLDCNT)bldcnt=value;else{assert(reg==REG_OFFSET_BLDALPHA);alpha=value;}}
"""
 # Explicit species adapter values are extracted from the pinned species tables.
 for name,file in [('FRONT','front_pic_coordinates.h'),('BACK','back_pic_coordinates.h')]:
  table=(reference/'src/data/pokemon_graphics'/file).read_text()
  delta=re.search(r'\[SPECIES_PIKACHU\]\s*=\s*\{.*?\.y_offset\s*=\s*(\d+)',table,re.S)[1]
  h=h.replace('PIKACHU_'+name,delta)
 elevation=(reference/'src/data/pokemon_graphics/enemy_mon_elevation.h').read_text()
 hit=re.search(r'\[SPECIES_PIKACHU\]\s*=\s*(\d+)',elevation)
 h=h.replace('PIKACHU_ELEVATION',hit[1] if hit else '0')
 coords=re.search(r'static const struct UCoords8 sBattlerCoords\[\]\[MAX_BATTLERS_COUNT\] =.*?\};',mons,re.S)[0]
 h+='struct UCoords8 {u8 x,y;};\n'+coords.replace('MAX_BATTLERS_COUNT','4')+'\n'
 h+='static u8 GetBattlerSpriteCoord(u8 n,u8 c){return c==3?GetBattlerSpriteFinal_Y(n,25,TRUE):c==1?sBattlerCoords[mode][n].y:sBattlerCoords[mode][n].x;}\n'
 h+=trig[trig.index('const s16 gSineTable[]'):trig.index('};',trig.index('const s16 gSineTable[]'))+2]+'\n'
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'
 h+='static void (*sAnimFuncs[])(struct Sprite*)={ContinueAnim,BeginAnim};\nstatic void (*sAnimCmdFuncs[])(struct Sprite*)={AnimCmd_loop,AnimCmd_jump,AnimCmd_end,AnimCmd_frame};\nstatic void (*sAffineAnimFuncs[])(struct Sprite*)={ContinueAffineAnim,BeginAffineAnim};\nstatic void (*sAffineAnimCmdFuncs[])(u8,struct Sprite*)={UnsupportedAffineLoop,AffineAnimCmd_jump,AffineAnimCmd_end,AffineAnimCmd_frame};\n'
 h+='\n'.join(funcs)+'\n'+extract.declaration(bug,'sMeanLookEyeAffineAnimCmds1')+'\n'+extract.declaration(bug,'sMeanLookEyeAffineAnimCmds2')+r"""
static const union AffineAnimCmd *const affine[]={sMeanLookEyeAffineAnimCmds1,sMeanLookEyeAffineAnimCmds2};
static const union AnimCmd dummyAnim[]={ANIMCMD_FRAME(0,1),ANIMCMD_END};
static const union AnimCmd *const dummy[]={dummyAnim};
static void run(int kind,int actor,int target){
 struct Sprite*s=&gSprites[0];memset(s,0,sizeof(*s));AffineAnimStateReset(0);currentMatrix=(struct OamMatrix){256,0,0,256};
 gBattleAnimAttacker=actor;gBattleAnimTarget=target;bldcnt=alpha=0;
 s->inUse=TRUE;s->usingSheet=TRUE;s->animBeginning=TRUE;s->anims=dummy;s->affineAnimBeginning=TRUE;s->affineAnims=affine;s->oam.affineMode=kind==0?3:0;
 s->x=GetBattlerSpriteCoord(target,BATTLER_COORD_X_2);s->y=GetBattlerSpriteCoord(target,BATTLER_COORD_Y_PIC_OFFSET);
 s->subpriority=kind==0?GetBattlerSpriteSubpriority(actor)-2:GetBattlerSpriteSubpriority(target)+2;
 s->callback=kind==0?AnimMeanLookEye:AnimBlockX;
 for(int tick=-1;tick<256;tick++){
  writes=sound=pan=0;AnimateSprites();struct AffineAnimState*a=&sAffineAnimStates[0];
  int phase=kind==1?s->data[0]:s->callback==AnimMeanLookEye_Step1?1:s->callback==AnimMeanLookEye_Step2?2:s->callback==AnimMeanLookEye_Step3?3:4;
  int v[]={kind,actor,target,mode,tick,s->inUse,s->x,s->y,s->x2,s->y2,s->invisible,phase,s->subpriority,bldcnt,alpha,a->xScale,a->yScale,writes,a->animNum,a->animCmdIndex,a->delayCounter,s->affineAnimPaused,s->affineAnimEnded,sound,pan};
  for(int i=0;i<25;i++)printf(i?",%d":"%d",v[i]);
  for(int i=0;i<8;i++)printf(",%d",s->data[i]);printf(",%d,%d,%d,%d\n",currentMatrix.a,currentMatrix.b,currentMatrix.c,currentMatrix.d);
  if(!s->inUse)return;
 }abort();
}
int main(void){for(mode=0;mode<2;mode++)for(int a=0;a<(mode?4:2);a++)for(int t=0;t<(mode?4:2);t++){run(0,a,t);run(1,a,t);}}
"""
 with tempfile.TemporaryDirectory() as tmp:
  c=Path(tmp)/'trapping.c';exe=Path(tmp)/'trapping';c.write_text(h);ref.compile_c(['cc','-std=gnu11','-O0',str(c),'-o',str(exe)]);output=subprocess.check_output([str(exe)],text=True)
 cases=[];lookup={}
 for row in output.splitlines():
  values=list(map(int,row.split(',')));key=tuple(values[:4])
  if key not in lookup:
   case=dict(zip(['kind','actor','target','mode'],key));case['rows']=[];lookup[key]=case;cases.append(case)
  lookup[key]['rows'].append(values[4:])
 columns=['tick','active','x','y','x2','y2','invisible','phase','subpriority','blendControl','alpha','xScale','yScale','matrixWrites','affineIndex','affineCommand','affineDelay','affinePaused','affineEnded','sound','pan']+['data'+str(i) for i in range(8)]+['matrixA','matrixB','matrixC','matrixD']
 for c in cases:
  rows={r[0]:dict(zip(columns,r)) for r in c['rows']}
  first=rows[-1];last=c['rows'][-1]
  assert first['active']==1 and last[1]==0
  assert all(r['x']==first['x'] and r['y']==first['y'] for r in rows.values())
  if c['kind']==0:
   assert first['xScale']==first['yScale']==384 and first['matrixA']==first['matrixD']==170
   assert rows[71]['affineIndex']==1 and rows[71]['affinePaused']==1 and rows[71]['matrixWrites']==1
   assert all(rows[t]['xScale']==48 and rows[t]['invisible']==1 for t in range(71,82))
   assert all(rows[t]['matrixWrites']==0 for t in range(72,82))
   assert rows[82]['xScale']==80 and rows[82]['affinePaused']==0
   assert rows[87]['xScale']==240 and rows[87]['affineEnded']==0
   assert rows[88]['affineEnded']==1 and rows[88]['phase']==2 and rows[89]['phase']==3
   assert rows[90]['x2']==1 and rows[106]['phase']==4
   assert rows[154]['invisible']==1 and rows[154]['data0']==0
   assert last[0]==157 and rows[157]['data0']==-1 and rows[157]['blendControl']==rows[157]['alpha']==0
  else:
   player=c['target']%2==0;shift=5 if player else 0;expectedPan=(-63 if c['actor']%2 else -64) if player else 63
   assert first['y2']==(-144 if player else -96)
   assert first['subpriority']==[28,42,18,52][c['target']]
   assert [(t,r['sound'],r['pan']) for t,r in rows.items() if r['sound']]==[(9+shift,198,expectedPan),(41+shift,198,expectedPan),(72+shift,185,expectedPan)]
   assert last[0]==135+shift and rows[last[0]]['data2']==7
 return dict(revision=ref.COMMIT,species='PIKACHU',columns=columns,cases=cases)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.write_text(json.dumps(build(a.root),separators=(',',':'))+'\n')

"""Extract pinned healing callbacks and the original affine interpreter for source vectors."""
import argparse,importlib.util,json,re,subprocess,tempfile
from pathlib import Path
def function(s,n):
 m=re.search(r'^(?:static )?(?:void|bool8|u8|s16) '+n+r'\([^;]+?\)\n\{',s,re.M)
 if not m:raise ValueError(n)
 i=m.end();depth=1
 while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
 return s[m.start():i]
def build(reference,helper):
 spec=importlib.util.spec_from_file_location('reference_c',helper);ref=importlib.util.module_from_spec(spec);spec.loader.exec_module(ref);ref.verify_reference(reference)
 s=(reference/'src/sprite.c').read_text();e=(reference/'src/battle_anim_effects_2.c').read_text();m=(reference/'src/battle_anim_effects_1.c').read_text();mons=(reference/'src/battle_anim_mons.c').read_text();h=(reference/'include/sprite.h').read_text()
 names=['BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_loop','BeginAffineAnimLoop','ContinueAffineAnimLoop','JumpToTopOfAffineAnimLoop','AffineAnimCmd_jump','AffineAnimCmd_end','AffineAnimCmd_frame','GetSpriteMatrixNum','AffineAnimStateRestartAnim','AffineAnimStateStartAnim','ApplyAffineAnimFrameAbsolute','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame','ApplyAffineAnimFrame','StartSpriteAffineAnim']
 funcs=[function(s,n) for n in names]+[function(e,n) for n in ['AnimSoftBoiledEgg','AnimSoftBoiledEgg_Step1','AnimSoftBoiledEgg_Step2','AnimSoftBoiledEgg_Step3','AnimSoftBoiledEgg_Step3_Callback1','AnimSoftBoiledEgg_Step3_Callback2','AnimSoftBoiledEgg_Step4','AnimSoftBoiledEgg_Step4_Callback']]+[function(m,n) for n in ['AnimMilkBottle','AnimMilkBottle_Step1','AnimMilkBottle_Step2']]+[function(mons,n) for n in ['SetAnimSpriteInitialXOffset','InitSpritePosToAnimAttacker']]
 c=r'''
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef int16_t s16;typedef int32_t s32;typedef uint16_t u16;typedef uint8_t u8;typedef uint8_t bool8;
#define TRUE 1
#define FALSE 0
#define B_SIDE_PLAYER 0
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define ST_OAM_AFFINE_ON_MASK 1
#define SAFE_DIV(a,b) ((a)/(b))
#define REG_OFFSET_BLDCNT 0
#define REG_OFFSET_BLDALPHA 1
#define BLDCNT_TGT2_ALL 0x3F00
#define BLDCNT_EFFECT_BLEND 0x40
#define BLDALPHA_BLEND(a,b) ((a)|((b)<<8))
'''
 c+=h[h.index('struct AffineAnimFrameCmd'):h.index('\nenum\n{',h.index('struct AffineAnimState'))]
 c+=r'''
struct Sprite {s16 x,y,x2,y2,sAnchorX,sAnchorY,data[8];struct {int affineMode,matrixNum;u16 tileNum;}oam;int dead,invisible,anchored,affineAnimBeginning,affineAnimEnded,affineAnimPaused;const union AffineAnimCmd *const *affineAnims;void(*callback)(struct Sprite*);};
struct ObjAffineSrcData{s16 xScale,yScale;u16 rotation;};struct OamMatrix{int unused;};
static struct AffineAnimState sAffineAnimStates[1];static s16 gBattleAnimArgs[8];static int gBattleAnimAttacker,gBattleAnimTarget;static u16 registers[2];
static int GetBattlerSide(int b){return b&1;}
static s16 GetBattlerSpriteCoord(int b,int c){static const s16 x[]={32,200,90,152},y[]={80,40,88,32},pic[]={96,48,104,40};return c==0||c==2?x[b]:c==1?y[b]:pic[b];}
static s16 GetBattlerSpriteCoord2(int b,int c){return GetBattlerSpriteCoord(b,c);}
static void DestroyAnimSprite(struct Sprite*s){s->dead=1;}
static void SetGpuReg(int reg,u16 value){registers[reg]=value;}
static void ObjAffineSet(struct ObjAffineSrcData*a,struct OamMatrix*b,int c,int d){}
static void CopyOamMatrix(u8 a,struct OamMatrix*b){}
static void UpdateSpriteMatrixAnchorPos(struct Sprite*s,s32 a,s32 b){}
'''
 c+='\n'.join(f.split('\n{')[0]+';' for f in funcs)+'\n'
 c+='static void (*sAffineAnimCmdFuncs[])(u8,struct Sprite*)={AffineAnimCmd_loop,AffineAnimCmd_jump,AffineAnimCmd_end,AffineAnimCmd_frame};\n'
 for source,prefix in [(e,'sSoftBoiledEggAffine'),(m,'sMilkBottleAffine')]:
  for match in re.finditer(r'static const union AffineAnimCmd (?:\*const )?'+prefix+r'\w+\[\] =\s*\{.*?\};',source,re.S):c+=match[0]+'\n'
 c+='\n'.join(funcs)
 c+=r'''
static void animate(struct Sprite*s){if(s->affineAnimBeginning)BeginAffineAnim(s);else ContinueAffineAnim(s);}
static void emit(struct Sprite*s,int kind,int tick){struct AffineAnimState*a=sAffineAnimStates;printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",kind,gBattleAnimAttacker,gBattleAnimTarget,tick,s->x,s->y,s->x2,s->y2,s->oam.tileNum,s->invisible,s->dead,registers[0],registers[1],a->animNum,a->animCmdIndex,a->delayCounter,a->loopCounter,a->xScale,a->yScale,a->rotation,s->affineAnimEnded,gBattleAnimArgs[7],s->callback==DestroyAnimSprite);}
static void run(int kind,int actor,int target){struct Sprite s={0};memset(sAffineAnimStates,0,sizeof(sAffineAnimStates));memset(registers,0,sizeof(registers));memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));gBattleAnimAttacker=actor;gBattleAnimTarget=target;s.oam.affineMode=1;s.affineAnims=kind==2?sMilkBottleAffineAnimTable:sSoftBoiledEggAffineAnimTable;StartSpriteAffineAnim(&s,0);gBattleAnimArgs[0]=3;gBattleAnimArgs[1]=16;gBattleAnimArgs[2]=kind;
 if(kind==2)AnimMilkBottle(&s);else AnimSoftBoiledEgg(&s);animate(&s);emit(&s,kind,-1);
 for(int tick=0;tick<300;tick++){if(tick==200)gBattleAnimArgs[7]=-1;s.callback(&s);if(!s.dead)animate(&s);emit(&s,kind,tick);if(s.dead)return;}abort();}
int main(void){for(int actor=0;actor<4;actor++)for(int target=0;target<4;target++)for(int kind=0;kind<3;kind++)run(kind,actor,target);}
'''
 with tempfile.TemporaryDirectory() as d:
  p=Path(d);(p/'healing.c').write_text(c);ref.compile_c(['cc','-std=c99','-O0',str(p/'healing.c'),'-o',str(p/'healing')]);out=subprocess.check_output([str(p/'healing')],text=True)
 cases={}
 for line in out.splitlines():
  kind,actor,target,*row=map(int,line.split(','));cases.setdefault(f'{kind}:{actor}:{target}',[]).append(row)
 return dict(columns=['tick','x','y','x2','y2','tile','hidden','dead','bldcnt','bldalpha','affine','pc','delay','loop','xScale','yScale','rotation','ended','arg7','destroyHandoff'],cases=cases)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--helper',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();data=build(a.reference,a.helper);a.output.write_text(json.dumps(data,separators=(',',':'))+'\n');print('cases',len(data['cases']),'rows',sum(map(len,data['cases'].values())))

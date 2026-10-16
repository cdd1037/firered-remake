#!/usr/bin/env python3
"""Run the pinned stinger/linear callbacks in bounded C with integer BIOS adapters.

The game's callback and movement functions are extracted verbatim. Host callback
pointer storage and the external BIOS angle/matrix functions are the only adapters.
No product implementation or original repository build scripts are executed.
"""
import argparse, importlib.util, json, re, subprocess, tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
CONFIGS=[[20,0,-8,0,20],[10,-4,0,-4,20],[20,12,10,12,20],
         [10,-8,-8,-8,20],[18,0,0,0,20],[26,8,8,8,20],
         [10,-4,0,-4,1],[0,0,87,0,4],[0,0,88,0,4],[0,0,89,0,4],[-128,127,127,-128,4],[127,-128,-128,127,4],[20,0,-8,0,255]]
COLUMNS=['tick','x','y','x2','y2','arg0','arg1','arg2','arg3','rotation','matrixA','matrixB','matrixC','matrixD','active','callback','remaining','velocityX','velocityY','fractionX','fractionY']
def function(source,name):
 m=re.search(r'^(?:static )?(?:void|s16|u16|u8|bool8) '+name+r'\([^;]+?\)\n\{',source,re.M)
 if not m:raise ValueError('Missing source function '+name)
 i=m.end();depth=1
 while depth:depth+=(source[i]=='{')-(source[i]=='}');i+=1
 return source[m.start():i]
def build(reference,helper,angle_adapter):
 spec=importlib.util.spec_from_file_location('reference_c',helper);ref=importlib.util.module_from_spec(spec);spec.loader.exec_module(ref);ref.verify_reference(reference)
 mons=(reference/'src/battle_anim_mons.c').read_text();bug=(reference/'src/battle_anim_bug.c').read_text();header=(reference/'include/battle_anim.h').read_text()
 functions=[function(mons,n) for n in ['InitSpritePosToAnimAttacker','SetAnimSpriteInitialXOffset','InitAnimLinearTranslation','StartAnimLinearTranslation','AnimTranslateLinear','AnimTranslateLinear_WithFollowup','TrySetSpriteRotScale','ShouldRotScaleSpeciesBeFlipped']]+[function(bug,'AnimTranslateStinger')]
 harness=r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8; typedef uint16_t u16; typedef int16_t s16; typedef int32_t s32; typedef uint8_t bool8;
#define TRUE 1
#define FALSE 0
#define B_SIDE_PLAYER 0
#define B_POSITION_PLAYER_LEFT 0
#define B_POSITION_OPPONENT_LEFT 1
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
struct Sprite; typedef void (*SpriteCallback)(struct Sprite *);
struct Sprite {s16 x,y,x2,y2,data[8];bool8 inUse,affineAnimPaused;struct {int affineMode,matrixNum,shape,size;}oam;SpriteCallback callback,stored;};
struct ObjAffineSrcData {s16 xScale,yScale;u16 rotation;};
struct OamMatrix {s16 a,b,c,d;};
static struct OamMatrix gOamMatrices[1];static u16 sRotation;
static u8 gBattleAnimAttacker,gBattleAnimTarget;static s16 gBattleAnimArgs[8];
static const s16 sX[]={32,200,90,152},sY[]={88,40,96,32};
static u8 GetBattlerSide(u8 b){assert(b<4);return b&1;}
static u8 GetBattlerPosition(u8 b){assert(b<4);return b;}
static bool8 IsContest(void){return FALSE;}
static s16 GetBattlerSpriteCoord(u8 b,u8 c){assert(b<4&&c<4);return(c==0||c==2)?sX[b]:sY[b];}
static s16 GetBattlerSpriteCoord2(u8 b,u8 c){return GetBattlerSpriteCoord(b,c);}
static void StoreSpriteCallbackInData6(struct Sprite*s,SpriteCallback cb){s->stored=cb;}
static void SetCallbackToStoredInData6(struct Sprite*s){s->callback=s->stored;}
static void DestroyAnimSprite(struct Sprite*s){s->inUse=FALSE;}
static void CalcCenterToCornerVec(struct Sprite*s,int shape,int size,int mode){abort();}
static const s16 quarter[]={0,402,803,1205,1605,2005,2404,2801,3196,3589,3980,4369,4756,5139,5519,5896,6269,6639,7005,7366,7723,8075,8423,8765,9102,9434,9759,10079,10393,10701,11002,11297,11585,11866,12139,12406,12665,12916,13159,13395,13622,13842,14053,14255,14449,14634,14810,14978,15136,15286,15426,15557,15678,15790,15892,15985,16069,16142,16206,16260,16305,16339,16364,16379,16384};
static s16 sine(u8 a){int q=a>>6,i=a&63,v=quarter[(q&1)?64-i:i];return q<2?v:-v;}
static void ObjAffineSet(const struct ObjAffineSrcData*s,struct OamMatrix*d,int count,int stride){assert(count==1&&stride==2);sRotation=s->rotation;u8 a=s->rotation>>8;int sn=sine(a),cs=sine(a+64);d->a=(s->xScale*cs)>>14;d->b=-((s->xScale*sn)>>14);d->c=(s->yScale*sn)>>14;d->d=(s->yScale*cs)>>14;}
'''
 harness+='\n'.join(re.findall(r'^#define sTransl_.*$',header,re.M))+'\n'+angle_adapter.read_text()+'\n'
 harness+='\n'.join(f.split('\n{',1)[0]+';' for f in functions)+'\n'+'\n'.join(functions)+'\n'
 harness+='static const s16 configs[][5]={'+','.join('{'+','.join(map(str,c))+'}' for c in CONFIGS)+'};\n'
 harness+=r'''
static void emit(struct Sprite*s,int config,int tick){
 int callback=s->callback==StartAnimLinearTranslation?0:s->callback==AnimTranslateLinear_WithFollowup?1:s->callback==DestroyAnimSprite?2:-1;assert(callback>=0);
 struct OamMatrix*m=&gOamMatrices[0];
 int row[]={tick,s->x,s->y,s->x2,s->y2,gBattleAnimArgs[0],gBattleAnimArgs[1],gBattleAnimArgs[2],gBattleAnimArgs[3],sRotation,m->a,m->b,m->c,m->d,s->inUse,callback,s->data[0],(u16)s->data[1],(u16)s->data[2],(u16)s->data[3],(u16)s->data[4]};
 printf("%d:%d:%d",gBattleAnimAttacker,gBattleAnimTarget,config);for(unsigned i=0;i<sizeof(row)/sizeof(row[0]);i++)printf(",%d",row[i]);puts("");
}
static void run(int actor,int target,int config){
 struct Sprite s={0};memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));memcpy(gBattleAnimArgs,configs[config],sizeof(configs[config]));gBattleAnimAttacker=actor;gBattleAnimTarget=target;
 s.x=sX[target];s.y=sY[target];s.inUse=TRUE;s.oam.affineMode=1;AnimTranslateStinger(&s);assert(s.affineAnimPaused);emit(&s,config,-1);
 for(int tick=0;tick<260;tick++){s.callback(&s);emit(&s,config,tick);if(!s.inUse)return;}abort();
}
int main(void){for(int actor=0;actor<4;actor++)for(int relation=1;relation<=2;relation++)for(int config=0;config<sizeof(configs)/sizeof(configs[0]);config++)run(actor,actor^relation,config);}
'''
 with tempfile.TemporaryDirectory(prefix='needle-source-c-') as tmp:
  c=Path(tmp)/'needle.c';exe=Path(tmp)/'needle';c.write_text(harness);ref.compile_c(['cc','-std=gnu11','-O0',str(c),'-o',str(exe)]);output=subprocess.check_output([str(exe)],text=True)
 cases={}
 for line in output.splitlines():
  key,*row=line.split(',');values=list(map(int,row))
  if len(values)!=len(COLUMNS):raise ValueError('Unexpected source vector width')
  cases.setdefault(key,[]).append(values)
 for key,rows in cases.items():
  duration=CONFIGS[int(key.split(':')[2])][4]
  if len(rows)!=duration+3 or rows[0][0]!=-1 or rows[-1][0]!=duration+1 or rows[-1][14]!=0 or any(r[14]!=1 for r in rows[:-1]):raise ValueError('Callback lifecycle differs')
 return dict(columns=COLUMNS,configs=CONFIGS,cases=cases)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--helper',type=Path,default=ROOT/'tests/Reference/reference_c.py');p.add_argument('--angle-adapter',type=Path,default=Path(__file__).with_name('bios_angles_adapter.c'));p.add_argument('--output',type=Path,required=True);a=p.parse_args()
 if a.output.exists():p.error('Choose a new candidate output; refusing to overwrite a source vector fixture.')
 a.output.write_text(json.dumps(build(a.reference,a.helper,a.angle_adapter),separators=(',',':'))+'\n')

"""Bounded original-C Rock fragments and packed target translation vectors."""
import argparse,importlib.util,json,re,subprocess,tempfile
from pathlib import Path
def function(s,n):
 m=re.search(r'^(?:static )?(?:void|bool8) '+n+r'\([^;]+?\)\n\{',s,re.M)
 if not m:raise ValueError(n)
 i=m.end();depth=1
 while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
 return s[m.start():i]
def build(reference,helper):
 spec=importlib.util.spec_from_file_location('reference_c',helper);ref=importlib.util.module_from_spec(spec);spec.loader.exec_module(ref);ref.verify_reference(reference)
 mons=(reference/'src/battle_anim_mons.c').read_text();rock=(reference/'src/battle_anim_rock.c').read_text();header=(reference/'include/battle_anim.h').read_text()
 names=['InitSpriteDataForLinearTranslation','TranslateSpriteLinearFixedPoint','SetAnimSpriteInitialXOffset','InitSpritePosToAnimAttacker','InitAnimLinearTranslation','StartAnimLinearTranslation','AnimTranslateLinear','AnimTranslateLinear_WithFollowup','TranslateAnimSpriteToTargetMonLocation']
 funcs=[function(mons,n) for n in names]+[function(rock,n) for n in ['AnimRockFragment','AnimRockBlastRock']]
 c=r'''
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef int16_t s16;typedef int32_t s32;typedef uint16_t u16;typedef uint8_t u8;typedef uint8_t bool8;
#define TRUE 1
#define FALSE 0
#define B_SIDE_PLAYER 0
#define B_SIDE_OPPONENT 1
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
struct Sprite {s16 x,y,x2,y2,data[8];int frame,affine,animates,dead;void(*callback)(struct Sprite*);void(*follow)(struct Sprite*);};
static s16 gBattleAnimArgs[8];static int gBattleAnimAttacker,gBattleAnimTarget;
static int GetBattlerSide(int b){return b&1;}
static s16 GetBattlerSpriteCoord(int b,int c){static const s16 x[]={32,200,90,152},y[]={80,40,88,32},pic[]={96,48,104,40};return c==0||c==2?x[b]:c==1?y[b]:pic[b];}
static s16 GetBattlerSpriteCoord2(int b,int c){return GetBattlerSpriteCoord(b,c);}
static void StartSpriteAnim(struct Sprite*s,int n){s->frame=n;}
static void AnimateSprite(struct Sprite*s){s->animates++;}
static void StartSpriteAffineAnim(struct Sprite*s,int n){s->affine=n;}
static void StoreSpriteCallbackInData6(struct Sprite*s,void(*f)(struct Sprite*)){s->follow=f;}
static void SetCallbackToStoredInData6(struct Sprite*s){s->callback=s->follow;}
static void DestroyAnimSprite(struct Sprite*s){s->dead=1;}
static void DestroySpriteAndMatrix(struct Sprite*s){s->dead=1;}
'''
 c+='\n'.join(re.findall(r'^#define sTransl_.*$',header,re.M))+'\n'
 c+='\n'.join(f.split('\n{')[0]+';' for f in funcs)+'\n'+'\n'.join(funcs)
 configs=[[0,0,20,24,14,2],[5,0,-20,24,14,1],[0,5,20,-24,14,2],[-5,0,-20,-24,14,2],[0,-5,30,18,8,2],[0,0,30,-18,8,2],[0,0,-30,18,8,2],[0,0,-30,-18,8,2],[3,-4,129,-129,3,0],[3,-4,300,-300,7,1]]
 c+='\nstatic const s16 fragments[][6]={'+','.join('{'+','.join(map(str,a))+'}' for a in configs)+'};\n'
 c+=r'''
static void emit(struct Sprite*s,int kind,int cfg,int tick){printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",kind,gBattleAnimAttacker,gBattleAnimTarget,cfg,tick,s->x,s->y,s->x2,s->y2,s->data[0],s->data[1],s->data[2],s->data[3],s->data[4],gBattleAnimArgs[2],s->frame,s->affine,s->animates,s->callback==DestroyAnimSprite||s->callback==DestroySpriteAndMatrix,s->dead);}
static void run(int kind,int actor,int target,int cfg){
 struct Sprite s={0};memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));gBattleAnimAttacker=actor;gBattleAnimTarget=target;s.x=GetBattlerSpriteCoord(target,2);s.y=GetBattlerSpriteCoord(target,3);
 if(kind==0){memcpy(gBattleAnimArgs,fragments[cfg],sizeof(fragments[cfg]));AnimRockFragment(&s);}
 else {static const int modes[]={0,1,256,257};gBattleAnimArgs[0]=16;gBattleAnimArgs[1]=-3;gBattleAnimArgs[2]=13;gBattleAnimArgs[3]=7;gBattleAnimArgs[4]=25;gBattleAnimArgs[5]=modes[cfg];AnimRockBlastRock(&s);}
 emit(&s,kind,cfg,-1);
 for(int tick=0;tick<64;tick++){s.callback(&s);emit(&s,kind,cfg,tick);if(s.dead)return;}abort();
}
int main(void){for(int a=0;a<4;a++)for(int relation=1;relation<4;relation++)for(int kind=0;kind<2;kind++)for(int cfg=0;cfg<(kind==0?10:4);cfg++)run(kind,a,a^relation,cfg);}
'''
 with tempfile.TemporaryDirectory() as d:
  p=Path(d);(p/'rock.c').write_text(c);ref.compile_c(['cc','-std=c99','-O0',str(p/'rock.c'),'-o',str(p/'rock')]);out=subprocess.check_output([str(p/'rock')],text=True)
 cases={}
 for line in out.splitlines():
  kind,actor,target,cfg,*row=map(int,line.split(','));cases.setdefault(f'{kind}:{actor}:{target}:{cfg}',[]).append(row)
 assert len(cases)==168
 return dict(columns=['tick','x','y','x2','y2','remaining','d1','d2','d3','d4','arg2','frame','affine','animateCalls','handoff','dead'],fragmentArgs=configs,modes=[0,1,256,257],cases=cases)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--helper',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();data=build(a.reference,a.helper);a.output.write_text(json.dumps(data,separators=(',',':'))+'\n');print('cases',len(data['cases']),'rows',sum(map(len,data['cases'].values())))

"""Source-extracted music callback trajectories; palette and tile selection are observed."""
import argparse,importlib.util,json,re,subprocess,tempfile
from pathlib import Path
from generate_music_palette_vectors import function

def build(reference,helper):
 spec=importlib.util.spec_from_file_location("reference_c",helper);ref=importlib.util.module_from_spec(spec);spec.loader.exec_module(ref);ref.verify_reference(reference)
 e=(reference/'src/battle_anim_effects_1.c').read_text();m=(reference/'src/battle_anim_mons.c').read_text();t=(reference/'src/trig.c').read_text();h=(reference/'include/battle_anim.h').read_text()
 funcs=[function(e,n) for n in ['AnimWavyMusicNotes','AnimWavyMusicNotes_CalcVelocity','AnimWavyMusicNotes_Step','AnimBellyDrumHand','AnimSlowFlyingMusicNotes','AnimSlowFlyingMusicNotes_Step']]+[function(m,n) for n in ['SetSpriteCoordsToAnimAttackerCoords','WaitAnimForDuration','InitAnimLinearTranslation','AnimTranslateLinear']]
 c=r'''
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef int32_t s32;typedef int16_t s16;typedef int8_t s8;typedef uint16_t u16;typedef uint8_t u8;typedef uint8_t bool8;
#define TRUE 1
#define FALSE 0
#define DISPLAY_WIDTH 240
#define DISPLAY_HEIGHT 160
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define ST_OAM_HFLIP 8
#define Q_8_8(x) ((s16)((x)*256))
struct Sprite{s16 x,y,x2,y2,data[8];struct{int paletteNum,matrixNum;}oam;int dead,anim;void(*callback)(struct Sprite*);void(*follow)(struct Sprite*);};
static s16 gBattleAnimArgs[8];static int gBattleAnimAttacker,gBattleAnimTarget;
static const u16 sParticlesColorBlendTable[4][6]={{0},{1},{2},{3}};
static int IsContest(void){return 0;}
static int IndexOfSpritePaletteTag(int tag){return tag+4;}
static s16 GetBattlerSpriteCoord(int b,int c){static const s16 x[]={32,200,90,152},pic[]={96,48,104,40};return c==2?x[b]:pic[b];}
static void StartSpriteAnim(struct Sprite*s,int n){s->anim=n;}
static void DestroyAnimSprite(struct Sprite*s){s->dead=1;}
static void DestroySpriteAndMatrix(struct Sprite*s){s->dead=1;}
static void StoreSpriteCallbackInData6(struct Sprite*s,void(*f)(struct Sprite*)){s->follow=f;}
static void SetCallbackToStoredInData6(struct Sprite*s){s->callback=s->follow;}
'''
 c+=t[t.index('const s16 gSineTable[]'):t.index('};',t.index('const s16 gSineTable[]'))+2]+'\n'
 c+=t[t.index('s16 Sin('):t.index('//',t.index('s16 Sin('))] if False else ''
 def anyfunction(src,n):
  start=re.search(r'^(?:static )?(?:void|bool8|s16) '+n+r'\([^;]+?\)\n\{',src,re.M);i=start.end();d=1
  while d:d+=(src[i]=='{')-(src[i]=='}');i+=1
  return src[start.start():i]
 # bool8/s16 source functions have the same exact brace extraction.
 c+=anyfunction(t,'Sin')+'\n'
 c+='\n'.join(re.findall(r'^#define sTransl_.*$',h,re.M))+'\n'
 c+='\n'.join(re.findall(r'^#define s(?:MoveTimer|BlendTableIdx|BlendTimer|BlendCycleTime|X |Y |VelocX|VelocY).*$',e,re.M))+'\n'
 c+='\n'.join(f.split('\n{')[0]+';' for f in funcs)+'\n'+'\n'.join(funcs)
 c+=r'''
static void emit(struct Sprite*s,int kind,int cfg,int tick){printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",kind,gBattleAnimAttacker,gBattleAnimTarget,cfg,tick,s->x,s->y,s->x2,s->y2,s->oam.paletteNum,s->oam.matrixNum,s->anim,s->dead);for(int i=0;i<8;i++)printf(",%d",s->data[i]);puts("");}
static void run(int kind,int actor,int target,int cfg){struct Sprite s={0};gBattleAnimAttacker=actor;gBattleAnimTarget=target;memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));
if(kind==0){gBattleAnimArgs[0]=cfg;gBattleAnimArgs[1]=cfg%4;gBattleAnimArgs[2]=cfg%2?12:0;AnimWavyMusicNotes(&s);}
else if(kind==1){gBattleAnimArgs[0]=cfg%2;gBattleAnimArgs[1]=cfg%4;gBattleAnimArgs[2]=cfg%4;gBattleAnimArgs[3]=cfg<4?0:128;AnimSlowFlyingMusicNotes(&s);}
else{gBattleAnimArgs[0]=cfg;AnimBellyDrumHand(&s);}
emit(&s,kind,cfg,-1);for(int tick=0;tick<512;tick++){s.callback(&s);emit(&s,kind,cfg,tick);if(s.dead)return;}abort();}
int main(void){for(int a=0;a<4;a++)for(int b=0;b<4;b++)for(int kind=0;kind<3;kind++)for(int cfg=0;cfg<(kind==2?2:8);cfg++)if(kind!=0||a!=b)run(kind,a,b,cfg);
struct Sprite s={0};gBattleAnimAttacker=0;gBattleAnimTarget=1;memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));gBattleAnimArgs[2]=32767;AnimWavyMusicNotes(&s);s.sBlendTimer=32766;emit(&s,3,0,-1);for(int tick=0;tick<3;tick++){s.callback(&s);emit(&s,3,0,tick);}}
'''
 with tempfile.TemporaryDirectory() as d:
  p=Path(d);(p/'oracle.c').write_text(c);ref.compile_c(['cc','-std=c99','-O0',str(p/'oracle.c'),'-o',str(p/'oracle')]);out=subprocess.check_output([str(p/'oracle')],text=True)
 cases={}
 for line in out.splitlines():
  kind,a,b,cfg,*row=map(int,line.split(','));cases.setdefault(f'{kind}:{a}:{b}:{cfg}',[]).append(row)
 return dict(columns=['tick','x','y','x2','y2','paletteSlot','matrixNum','animation','dead','data[8]'],cases=cases)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--helper',type=Path,required=True);a=p.parse_args();d=build(a.reference,a.helper);a.output.write_text(json.dumps(d,separators=(',',':'))+'\n');print(len(d['cases']),sum(map(len,d['cases'].values())))

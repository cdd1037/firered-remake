"""Extract source ice callbacks and 4-bit translation into bounded numerical fixtures."""
import argparse,importlib.util,json,os,re,subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
spec=importlib.util.spec_from_file_location('reference_c',ROOT/'tests/Reference/reference_c.py');ref=importlib.util.module_from_spec(spec);spec.loader.exec_module(ref)
p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path,required=True);args=p.parse_args();R=args.reference;args.output.mkdir(parents=True,exist_ok=True)
work=tempfile.TemporaryDirectory();O=Path(work.name)
def compile_and_run(name,source):
 path=O/(name+'.c');exe=O/(name+('.exe' if os.name=='nt' else ''));path.write_text(source)
 ref.compile_c(['cc','-std=c99','-O0',str(path),'-o',str(exe)])
 return subprocess.check_output([str(exe)],text=True)
def fun(s,n):
 m=re.search(r'^(?:static )?(?:void|bool8|s16) '+n+r'\([^;]+?\)\n\{',s,re.M)
 if not m:raise ValueError(n)
 i=m.end();d=1
 while d:d+=(s[i]=='{')-(s[i]=='}');i+=1
 return s[m.start():i]
ice=(R/'src/battle_anim_ice.c').read_text();mons=(R/'src/battle_anim_mons.c').read_text();trig=(R/'src/trig.c').read_text()
names=['AnimSwirlingSnowball','AnimSwirlingSnowball_Step1','AnimSwirlingSnowball_Step2','AnimSwirlingSnowball_End','AnimMoveParticleBeyondTarget','AnimWiggleParticleTowardsTarget'];linear=['InitAnimFastLinearTranslation','InitAnimFastLinearTranslationWithSpeed','InitAnimFastLinearTranslationWithSpeedAndPos','AnimFastTranslateLinearWaitEnd','AnimFastTranslateLinear']
h=r'''
#include <stdio.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>
typedef int16_t s16;typedef int32_t s32;typedef uint16_t u16;typedef uint32_t u32;typedef uint8_t u8;typedef uint8_t bool8;
#define TRUE 1
#define FALSE 0
#define B_SIDE_PLAYER 0
#define DISPLAY_WIDTH 240
#define DISPLAY_HEIGHT 160
#define BATTLER_COORD_X_2 0
#define BATTLER_COORD_Y_PIC_OFFSET 1
#define Q_8_8(n) ((s16)((n)*256))
#define sTransl_Duration data[0]
#define sTransl_InitX data[1]
#define sTransl_DestX data[2]
#define sTransl_InitY data[3]
#define sTransl_DestY data[4]
struct Sprite {s16 x,y,x2,y2,data[8];void (*callback)(struct Sprite*);void (*follow)(struct Sprite*);int dead;};
static s16 gBattleAnimArgs[8];static int gBattleAnimAttacker,gBattleAnimTarget;static s16 positions[4][2];
static int GetBattlerSide(int b){return b&1;}
static int GetBattlerSpriteCoord(int b,int c){return positions[b][c];}
static void InitSpritePosToAnimAttacker(struct Sprite*s,int pic){s->x=positions[gBattleAnimAttacker][0]+(GetBattlerSide(gBattleAnimAttacker)?-gBattleAnimArgs[0]:gBattleAnimArgs[0]);s->y=positions[gBattleAnimAttacker][1]+gBattleAnimArgs[1];}
static void SetAverageBattlerPositions(int b,int pic,s16*x,s16*y){*x=(positions[b][0]+positions[b^2][0])/2;*y=(positions[b][1]+positions[b^2][1])/2;}
static void StoreSpriteCallbackInData6(struct Sprite*s,void(*f)(struct Sprite*)){s->follow=f;}
static void SetCallbackToStoredInData6(struct Sprite*s){s->callback=s->follow;}
static void DestroyAnimSprite(struct Sprite*s){s->dead=1;}
'''
h+=re.search(r'const s16 gSineTable\[\] =\s*\{.*?\};',trig,re.S)[0]+'\n'+fun(trig,'Sin')+'\n'+fun(trig,'Cos')+'\n'
for n in names+linear:h+=('static ' if n in names or n in ('InitAnimFastLinearTranslation','AnimFastTranslateLinearWaitEnd') else '')+('bool8' if n=='AnimFastTranslateLinear' else 'void')+' '+n+'(struct Sprite*);\n'
h+='\n'.join(fun(mons,n) for n in linear)+'\n'+'\n'.join(fun(ice,n) for n in names)
h+=r'''
static int phase(struct Sprite*s){if(s->callback==AnimWiggleParticleTowardsTarget)return 6;if(s->callback==AnimSwirlingSnowball)return 0;if(s->callback==InitAnimFastLinearTranslationWithSpeedAndPos)return 1;if(s->callback==AnimFastTranslateLinearWaitEnd)return 2;if(s->callback==AnimSwirlingSnowball_Step1)return 3;if(s->callback==AnimSwirlingSnowball_Step2)return 4;return 5;}
static void run(int actor,int grouped,int yoff,int beyond){struct Sprite s={0};gBattleAnimAttacker=actor;gBattleAnimTarget=actor^1;memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));gBattleAnimArgs[1]=yoff;gBattleAnimArgs[3]=yoff;gBattleAnimArgs[4]=72;gBattleAnimArgs[5]=beyond?6:grouped;gBattleAnimArgs[6]=beyond?5:0;gBattleAnimArgs[7]=beyond?grouped:0;s.callback=beyond?AnimMoveParticleBeyondTarget:AnimSwirlingSnowball;for(int t=0;t<512;t++){s.callback(&s);printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",actor,grouped,yoff,t,phase(&s),s.x,s.y,s.x2,s.y2,s.data[0],s.data[1],s.data[2],s.data[3],s.data[4],s.data[5],s.dead);if(s.dead)return;}exit(2);}
int main(void){positions[0][0]=32;positions[0][1]=96;positions[1][0]=200;positions[1][1]=48;positions[2][0]=90;positions[2][1]=104;positions[3][0]=152;positions[3][1]=40;for(int a=0;a<4;a++)for(int g=0;g<2;g++)for(int y=-10;y<=10;y+=20)for(int beyond=0;beyond<2;beyond++)run(a,g,y,beyond);}
'''
rows=[list(map(int,x.split(','))) for x in compile_and_run('snow',h).splitlines()]
cases=[]
for row in rows:
 if row[3]==0:cases.append(dict(actor=row[0],grouped=row[1],yOffset=row[2],beyond=row[4]==6,frames=[]))
 cases[-1]['frames'].append(row[3:])
(args.output/'swirling-snow-source-vectors.json').write_text(json.dumps({'columns':['tick','phase','x','y','x2','y2','d0','d1','d2','d3','d4','d5','dead'],'cases':cases},separators=(',',':'))+'\n')
print('cases',len({tuple(r[:3]+[r[4]==6]) for r in rows}),'frames',len(rows),'last',rows[-1])
source=h[:h.index('int main(void)')]+r'''
static void vector(int id,int dx,int dy,int speed){struct Sprite s={0};s.data[0]=speed;s.data[2]=dx;s.data[4]=dy;InitAnimFastLinearTranslationWithSpeed(&s);printf("%d,-1,%d,%d,%d,%d,%d\n",id,s.data[0],s.data[1],s.data[2],s.x2,s.y2);for(int n=0;n<600;n++){if(n==400){s.data[3]=s.data[4]=0;s.data[1]^=1;s.data[2]^=1;}if(n>=20)s.data[0]=1;AnimFastTranslateLinear(&s);printf("%d,%d,%d,%d,%d,%d,%d\n",id,n,s.data[0],s.data[1],s.data[2],s.x2,s.y2);}}
int main(void){vector(0,168,-48,72);vector(1,-168,48,72);vector(2,5000,-4000,72);vector(3,-5000,4000,72);vector(4,3,17,48);vector(5,1,-17,16);}
'''
rows=[list(map(int,x.split(','))) for x in compile_and_run('fast',source).splitlines()]
(args.output/'fast-linear-source-vectors.json').write_text(json.dumps(rows,separators=(',',':'))+'\n')
print('fast-linear rows',len(rows))
work.cleanup()

#!/usr/bin/env python3
"""Explicit native Weather Ball oracle; ordinary tests only read local vectors."""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile
from pathlib import Path
HERE=Path(__file__).resolve().parent

def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def build(root,reference,out):
 helper=module('weather_reference',root/'tests/Reference/reference_c.py');helper.verify_reference(reference)
 f=module('weather_native_sprite',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py')
 x=module('weather_extract',root/'tests/Reference/ConfusionKiss/source_extract.py')
 pins=json.loads((HERE/'pinned-inputs.json').read_text());s={}
 for path,digest in pins.items():
  data=(reference/path).read_bytes()
  if hashlib.sha256(data).hexdigest()!=digest:raise ValueError('Pinned input differs: '+path)
  s[path]=data.decode()
 names={'src/battle_anim_mons.c':['AnimWeatherBallUp','AnimWeatherBallUp_Step','AnimWeatherBallDown','InitAnimLinearTranslation','StartAnimLinearTranslation','AnimTranslateLinear','AnimTranslateLinear_WithFollowup'],'src/battle_anim_effects_3.c':['AnimTask_GetWeather']}
 functions=[f.function(s[p],name) for p,ns in names.items() for name in ns]
 constants=x.macros(list(s.values()),['ANIM_END','sTransl_Speed','sTransl_InitX','sTransl_DestX','sTransl_InitY','sTransl_DestY','ARG_RET_ID','ANIM_WEATHER_NONE','ANIM_WEATHER_SUN','ANIM_WEATHER_RAIN','ANIM_WEATHER_SANDSTORM','ANIM_WEATHER_HAIL','B_WEATHER_SUN','B_WEATHER_RAIN','B_WEATHER_SANDSTORM','B_WEATHER_HAIL'])
 extra=constants+r'''
#define B_SIDE_PLAYER 0
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
static s16 gBattleAnimArgs[8];static u8 gBattleAnimAttacker,gBattleAnimTarget;static u16 gWeatherMoveAnim;static int doubles,yoff,elevation,taskAlive;
static u8 GetBattlerSide(u8 b){return b&1;}
static u8 GetBattlerSpriteCoord(u8 id,u8 coord){static const u8 p[2][4][2]={{{72,80},{176,40},{48,40},{112,80}},{{32,80},{200,40},{90,88},{152,32}}};if(coord==2)return p[doubles][id][0];u8 y=p[doubles][id][1]+yoff-((id&1)?elevation:0);if(!(id&1))y+=8;if(y>104)y=104;return y;}
static void DestroyAnimVisualTask(u8 id){assert(id==0&&taskAlive);taskAlive=0;}
'''
 h=f.sprite_harness(reference,functions,extra)
 arrays={'src/battle_anim_status_effects.c':['sAnim_WeatherBallNormal'],'src/battle_anim_fire.c':['sAnim_BasicFire'],'src/battle_anim_water.c':['sAnim_WeatherBallWaterDown','sAffineAnim_WeatherBallWaterDown'],'src/battle_anim_rock.c':['sAnim_Rock_Big','sAffineAnim_BasicRock_0'],'src/battle_anim_ice.c':['sAffineAnim_WeatherBallIceDown']}
 for p,ns in arrays.items():
  for name in ns:h+=f.declaration(s[p],name)+'\n'
 dummy=re.search(r'static const union AnimCmd sDummyAnim = \{ ANIM_END \};',s['src/sprite.c'])
 if dummy is None:raise ValueError('Native dummy frame differs')
 h+=dummy[0]+r'''
static const union AnimCmd*const framesNormal[]={sAnim_WeatherBallNormal},*const framesFire[]={sAnim_BasicFire},*const framesWater[]={sAnim_WeatherBallWaterDown},*const framesRock[]={sAnim_Rock_Big},*const framesIce[]={&sDummyAnim};
static const union AffineAnimCmd*const affineWater[]={sAffineAnim_WeatherBallWaterDown},*const affineRock[]={sAffineAnim_BasicRock_0},*const affineIce[]={sAffineAnim_WeatherBallIceDown};
static void row(int frame){struct Sprite*s=&gSprites[0];struct AffineAnimState*a=&sAffineAnimStates[0];int cb=s->callback==AnimWeatherBallUp_Step?0:s->callback==StartAnimLinearTranslation?1:s->callback==AnimTranslateLinear_WithFollowup?2:s->callback==DestroyAnimSprite?3:-1;
 printf("{\"frame\":%d,\"alive\":%d,\"callback\":%d,\"sprite\":[%d,%d,%d,%d,%d,%d,%d],\"affine\":[%d,%d,%d,%d],\"data\":[",frame,s->inUse,cb,s->x,s->y,s->x2,s->y2,s->oam.tileNum,s->animEnded,s->animDelayCounter,a->xScale,a->yScale,a->rotation,s->affineAnimEnded);for(int i=0;i<8;i++)printf("%s%d",i?",":"",s->data[i]);puts("]}");}
int main(int argc,char**argv){assert(argc==15);int kind=atoi(argv[1]);doubles=atoi(argv[2]);gBattleAnimAttacker=atoi(argv[3]);gBattleAnimTarget=atoi(argv[4]);yoff=atoi(argv[5]);elevation=atoi(argv[6]);int seedX=atoi(argv[7]);for(int i=0;i<7;i++)gBattleAnimArgs[i]=atoi(argv[8+i]);
 if(kind==6){gWeatherMoveAnim=seedX;taskAlive=1;gBattleAnimArgs[7]=-30000;AnimTask_GetWeather(0);printf("{\"weather\":%d,\"alive\":%d,\"args\":[",seedX,taskAlive);for(int i=0;i<8;i++)printf("%s%d",i?",":"",gBattleAnimArgs[i]);puts("]}");return 0;}
 struct Sprite*s=&gSprites[0];s->inUse=TRUE;s->usingSheet=TRUE;s->animBeginning=TRUE;s->affineAnimBeginning=TRUE;s->x=GetBattlerSpriteCoord(gBattleAnimTarget,2);s->y=GetBattlerSpriteCoord(gBattleAnimTarget,3);
 s->callback=kind==0?AnimWeatherBallUp:AnimWeatherBallDown;s->anims=kind<2?framesNormal:kind==2?framesFire:kind==3?framesWater:kind==4?framesRock:framesIce;s->oam.affineMode=kind>=3;s->affineAnims=kind==3?affineWater:kind==4?affineRock:affineIce;AffineAnimStateReset(0);
 for(int frame=-1;frame<1000;frame++){AnimateSprites();if(frame==-1&&kind==0)s->data[2]=seedX;row(frame);if(!s->inUse)return 0;}abort();}
'''
 source=out/'weather-ball.c';source.write_text(h);exes=[]
 for opt in ['O0','O2']:
  exe=out/('weather-ball-'+opt);helper.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(source),'-o',str(exe)]);exes.append(exe)
 def run(args):
  values=[subprocess.check_output([str(e),*map(str,args)],text=True) for e in exes]
  if values[0]!=values[1]:raise ValueError('Optimization parity differs '+str(args))
  return [json.loads(line) for line in values[0].splitlines()]
 cases=[]
 def add(kind,d,actor,target,args=(-30,-100,25,1,0,0),y=7,e=0,seed=0,label='authored'):
  args=list(args)+[1900+i for i in range(len(args),7)];rows=run([kind,d,actor,target,y,e,seed,*args]);assert rows and not rows[-1]['alive']
  cases.append(dict(kind=kind,doubles=bool(d),actor=actor,target=target,yOffset=y,elevation=e,seedX=seed,arguments=args,branch=label,rows=rows))
 for d,actor,target in [(0,0,1),(0,1,0),(1,0,1),(1,1,0),(1,2,3),(1,3,2)]:
  for kind in range(6):add(kind,d,actor,target)
 for kind in range(1,6):
  add(kind,1,3,2,[-32768,32767,1,-32768,32767,-32768],y=31,e=64,label='word-wrapping-and-one-frame-duration')
  add(kind,1,2,3,[32767,-32768,37,32767,-32768,32767],y=31,e=64,label='opponent-offset-sign-and-retained-ignored-args')
 add(0,1,0,1,seed=32765,label='signed-x-accumulator-wrap')
 add(0,1,3,2,seed=-32765,y=31,e=64,label='signed-negative-x-accumulator-wrap')
 predicates=[run([6,0,0,1,0,0,weather,11,22,33,44,55,66,77])[0] for weather in range(256)]
 return dict(schema='firered-weather-ball-v1',sourceSha256=pins,sourceFunctions=names,optimizationParity=['O0','O2'],semantics={'frameMinusOne':'Native creation invokes callback then BeginAnim/BeginAffineAnim. Following rows execute original AnimateSprites, including the completion-to-destroy callback handoff.','hostSeams':'Canonical picture coordinates, fixture geometry, and callback pointer token transport only. No RNG. Native sprite frame/affine engines are extracted. BIOS matrices are covered by existing shared matrix tests.','dummyFrame':'Ice uses original immediate-END dummy animation. Generated static tile0 frame representation does not expose that unused native END bit; visual tile, motion, lifetime and native affine state are compared.','weather':'Every raw controller bit combination 0..255 executes the original sun-first predicate; unrelated argument words must survive.'},cases=cases,predicates=predicates)

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--reference',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);a=p.parse_args()
 if a.output.exists():raise FileExistsError(a.output)
 out=a.build_dir or Path(tempfile.mkdtemp(prefix='weather-ball-reference-'));out.mkdir(parents=True,exist_ok=True);r=build(a.root,a.reference or a.root/'content/raw/pokefirered/037335f4',out);a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(r,separators=(',',':'))+'\n');print(json.dumps(dict(cases=len(r['cases']),rows=sum(len(c['rows']) for c in r['cases']),predicates=len(r['predicates']))))

#!/usr/bin/env python3
"""Pinned Solar Beam / Guillotine callbacks. Explicit full-life cases; local outputs only."""
import argparse,hashlib,importlib.util,json,re,subprocess
from pathlib import Path
def module(n,p):
 s=importlib.util.spec_from_file_location(n,p);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
SOURCE_PINS={'battle_anim_effects_1.c': '80de93553f73982ca5b1abab6af952fe96e0b4763fefddf8aa1dc5071ad20682', 'battle_anim_effects_2.c': '50561e4882a0f8813f305af6f4147ed1c394657278e32aae6c733bef31994a04', 'battle_anim_mons.c': 'fcb002acc48b8cd6b8cf8b7eddcb3034d721ff5887eb6bdeedfe183b7b3908bb', 'sprite.c': '2a804302eb5a89d31c3ec2f33dc645a80a0c645113d4b33b9bd571064c0286ae'}
CASES={**{f'big-frame-{i}':[0,0,0,1,15,0,20,i] for i in range(7)},**{f'pincer-{actor}-{variant}':[1,1,actor,actor^1,variant,0,0,0] for actor in range(4) for variant in range(2)},**{f'task-{actor}':[2,1,actor,actor^1,0,0,0,0] for actor in range(4)},'big-short':[0,0,1,0,-7,8,1,6],'big-signed-min':[0,1,0,1,-32768,32767,1,0],'big-signed-max':[0,1,1,0,32767,-32768,1,6],'big-partner':[0,1,0,2,15,-7,20,3],'pincer-partner':[1,1,3,1,1,0,0,0],'task-partner':[2,1,2,0,0,0,0,0]}
def build(root):
 raw=root/'content/raw/pokefirered/037335f4';base=module('spbase',root/'tests/Game.Presentation.Tests/Reference/generate_orbit_spray_vectors.py');pg=module('sppower',root/'tests/Game.Presentation.Tests/Reference/generate_power_guard_vectors.py');h,meta=pg.build(root,raw);h=h.split('static const long long config0')[0]
 for name,digest in SOURCE_PINS.items():assert hashlib.sha256((raw/'src'/name).read_bytes()).hexdigest()==digest,name
 sources={f:(raw/'src'/f).read_text() for f in ['battle_anim_effects_1.c','battle_anim_effects_2.c','battle_anim_mons.c','sprite.c']}
 funcs=[base.function(sources[f],name) for f,names in [('battle_anim_effects_1.c',['AnimSolarBeamBigOrb','AnimSolarBeamSmallOrb','AnimSolarBeamSmallOrb_Step','AnimTask_CreateSmallSolarBeamOrbs']),('sprite.c',['StartSpriteAnim','SeekSpriteAnim']),('battle_anim_mons.c',['InitAnimLinearTranslation','StartAnimLinearTranslation','AnimTranslateLinear','AnimTranslateLinear_WithFollowup']),('battle_anim_effects_2.c',['AnimGuillotinePincer','AnimGuillotinePincer_Step1','AnimGuillotinePincer_Step2','AnimGuillotinePincer_Step3'])] for name in names]
 declarations=[]
 for file,prefixes in [('battle_anim_effects_1.c',['sSolarBeamBigOrb','sSolarBeamSmallOrb']),('battle_anim_effects_2.c',['sGuillotine'])]:
  for prefix in prefixes:declarations+=re.findall(r'static const union AnimCmd (?:\*const )?'+prefix+r'\w*\[\]\s*=\s*\{.*?\};',sources[file],re.S)
 for file,name in [('battle_anim_effects_1.c','gSolarBeamBigOrbSpriteTemplate'),('battle_anim_effects_1.c','gSolarBeamSmallOrbSpriteTemplate'),('battle_anim_effects_2.c','gGuillotineSpriteTemplate')]:declarations.append(re.search(r'const struct SpriteTemplate '+name+r'\s*=\s*\{.*?\};',sources[file],re.S)[0])
 tags=(raw/'include/constants/battle_anim.h').read_text()
 h+='\n'+'\n'.join(re.search(r'^#define '+name+r'\s+[^\n]+',tags,re.M)[0] for name in ['ANIM_SPRITES_START','ANIM_TAG_ORBS','ANIM_TAG_CUT'])+'\nstatic const struct OamData gOamData_AffineOff_ObjBlend_32x32={.size=2,.objMode=1,.priority=2};\n'
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'+'\n'.join(declarations+funcs)+'\n'+RUNNER
 spans=[]
 for snippet in funcs+declarations:
  file,text=next((f,s) for f,s in sources.items() if snippet in s);line=text[:text.index(snippet)].count('\n')+1;spans.append(dict(file='src/'+file,firstLine=line,lastLine=line+snippet.count('\n'),sha256=hashlib.sha256(snippet.encode()).hexdigest()))
 return h,dict(sourceRevision=meta['sourceRevision'],sourceSha256=meta['sourceSha256'],sourceSpans=spans,geometry=meta['geometry'],columns=['slot','x','y','x2','y2','tile','hflip','vflip','subpriority','animIndex','delay','ended','paused'])
RUNNER=r'''
static int kind,tick;
static int live(void){int n=0;for(int i=slots;i<64;i++)n+=gSprites[i].inUse;return n;}
static void emit(void){printf("[%d,%d,%d,[",tick,gAnimVisualTaskCount,gTasks[0].isActive);for(int j=0;j<4;j++)printf("%s%d",j?",":"",gBattleAnimArgs[j]);printf("],[");int comma=0;for(int i=slots;i<64;i++){struct Sprite*s=&gSprites[i];if(!s->inUse)continue;printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",comma++?",":"",i,s->x,s->y,s->x2,s->y2,s->oam.tileNum,(s->oam.matrixNum>>3)&1,(s->oam.matrixNum>>4)&1,s->subpriority,s->animCmdIndex,s->animDelayCounter,s->animEnded,s->animPaused);}puts("]]");}
int main(int argc,char**argv){assert(argc==9);kind=atoi(argv[1]);mode=atoi(argv[2]);gBattleAnimAttacker=atoi(argv[3]);gBattleAnimTarget=atoi(argv[4]);slots=mode?4:2;memset(gSprites,0,sizeof(gSprites));memset(gTasks,0,sizeof(gTasks));memset(sAffineAnimStates,0,sizeof(sAffineAnimStates));memset(&battleSpritesData,0,sizeof(battleSpritesData));gAnimVisualTaskCount=0;gOamMatrixAllocBitmap=0;for(int i=0;i<slots;i++){struct Sprite*s=&gSprites[i];s->inUse=TRUE;s->callback=SpriteCallbackDummy;s->anims=dummyFrames;s->affineAnims=dummyAffines;s->animPaused=TRUE;s->affineAnimPaused=TRUE;s->x=GetBattlerSpriteCoord(i,2);s->y=GetBattlerSpriteCoord(i,3);s->subpriority=GetBattlerSpriteSubpriority(i);}for(int i=0;i<4;i++)gBattleAnimArgs[i]=atoi(argv[i+5]);if(kind==2){gTasks[0].isActive=TRUE;gTasks[0].func=AnimTask_CreateSmallSolarBeamOrbs;gAnimVisualTaskCount=1;gTasks[0].func(0);}else{CreateSpriteAndAnimate(kind==0?&gSolarBeamBigOrbSpriteTemplate:&gGuillotineSpriteTemplate,GetBattlerSpriteCoord(gBattleAnimTarget,2),GetBattlerSpriteCoord(gBattleAnimTarget,3),GetBattlerSpriteSubpriority(kind==0?gBattleAnimTarget:gBattleAnimAttacker)-(kind==0?3:2));gAnimVisualTaskCount=1;}tick=-1;emit();for(tick=0;tick<1000&&(live()||gTasks[0].isActive);tick++){for(int i=0;i<64;i++)if(gSprites[i].inUse){gSprites[i].callback(&gSprites[i]);if(gSprites[i].inUse)AnimateSprite(&gSprites[i]);}if(gTasks[0].isActive)gTasks[0].func(0);emit();}assert(!live()&&!gTasks[0].isActive&&!gAnimVisualTaskCount);return 0;}
'''
def main():
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[3]);p.add_argument('--output-dir',type=Path,required=True);p.add_argument('--full',action='store_true');a=p.parse_args();a.root=a.root.resolve();a.output_dir.mkdir(parents=True,exist_ok=True)
 h,result=build(a.root);source=a.output_dir/'solar-pincer-native.c';source.write_text(h);ref=module('spref',a.root/'tests/Reference/reference_c.py')
 for opt in ['O0','O2']:ref.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(source),'-o',str(a.output_dir/('solar-pincer-'+opt))])
 configs=dict(CASES)
 if a.full:
  for d in [0,1]:
   for actor in range(4 if d else 2):
    for target in range(4 if d else 2):
     for anim in range(7):configs[f'full-big-{d}-{actor}-{target}-{anim}']=[0,d,actor,target,15,0,20,anim]
     for variant in range(2):configs[f'full-pincer-{d}-{actor}-{target}-{variant}']=[1,d,actor,target,variant,0,0,0]
     configs[f'full-task-{d}-{actor}-{target}']=[2,d,actor,target,0,0,0,0]
 cases={}
 for name,config in configs.items():
  outputs=[subprocess.check_output([str(a.output_dir/('solar-pincer-'+opt)),*map(str,config)]) for opt in ['O0','O2']];assert outputs[0]==outputs[1],name;rows=[json.loads(line) for line in outputs[0].splitlines()];assert rows[-1][1:3]==[0,0] and rows[-1][4]==[];cases[name]=dict(config=config,rows=rows)
 result.update(profile='full' if a.full else 'representative',representativeCases=list(CASES),cases=cases,optimizationParity=['O0','O2'],coverage=dict(caseCount=len(cases),rowCount=sum(len(x['rows']) for x in cases.values())))
 (a.output_dir/'solar-pincer-source-vectors.json').write_text(json.dumps(result,separators=(',',':'))+'\n');print(json.dumps(result['coverage']))
if __name__=='__main__':main()

#!/usr/bin/env python3
"""Extract original Assist/Sweet Scent callbacks and native signed-byte setpan.

Uses the existing bounded source extractor and native sprite engine harness.
Expected trajectories are local ignored output, never derived from C# playback.
"""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile
from pathlib import Path

def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def build(root,reference,out):
 helper=module('reference_c',root/'tests/Reference/reference_c.py');helper.verify_reference(reference)
 extract=module('source_extract',root/'tests/Reference/CallerPrelude/source_extract.py')
 engine=module('sprite_harness',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py')
 pins=json.loads((Path(__file__).parent/'pinned-inputs.json').read_text());s={p:(reference/p).read_text() for p in pins}
 for path,digest in pins.items():
  if hashlib.sha256((reference/path).read_bytes()).hexdigest()!=digest:raise ValueError('Pinned source changed: '+path)
 names={'src/battle_anim_effects_3.c':['AnimAssistPawprint','AnimSweetScentPetal','AnimSweetScentPetal_Step'],'src/battle_anim_mons.c':['InitAndRunAnimFastLinearTranslation','InitAnimFastLinearTranslation','AnimFastTranslateLinear','AnimFastTranslateLinearWaitEnd'],'src/sprite.c':['StartSpriteAnim'],'src/trig.c':['Sin','Cos']}
 functions=[extract.function(s[p],n) for p,ns in names.items() for n in ns]
 extra=extract.macros([s['include/global.h']],['Q_8_8'])+'\n'+'\n'.join(line for line in s['include/battle_anim.h'].splitlines() if line.startswith('#define sTransl_'))+'\n'+extract.array(s['src/trig.c'],'gSineTable')+r'''
#define B_SIDE_PLAYER 0
#define DISPLAY_WIDTH 240
static u8 gBattleAnimAttacker;static s16 gBattleAnimArgs[8];
static int GetBattlerSide(u8 id){return id&1;}
'''
 harness=engine.sprite_harness(reference,functions,extra)
 harness+='\n'.join(extract.array(s['src/battle_anim_effects_3.c'],n) for n in ['sSweetScentPetalAnimCmds1','sSweetScentPetalAnimCmds2','sSweetScentPetalAnimCmds3'])+r'''
static const union AnimCmd dummy[]={{.type=-1}};
static const union AnimCmd *const paw[]={dummy},*const petal[]={sSweetScentPetalAnimCmds1,sSweetScentPetalAnimCmds2,sSweetScentPetalAnimCmds3};
int main(int argc,char**argv){assert(argc==11);int kind=atoi(argv[1]);gBattleAnimAttacker=atoi(argv[2]);for(int i=0;i<8;i++)gBattleAnimArgs[i]=atoi(argv[i+3]);
 struct Sprite*s=&gSprites[0];memset(s,0,sizeof(*s));s->inUse=TRUE;s->usingSheet=TRUE;s->animBeginning=TRUE;s->anims=kind?petal:paw;s->callback=kind?AnimSweetScentPetal:AnimAssistPawprint;
 for(int frame=0;frame<400;frame++){AnimateSprites();printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",frame,s->inUse,s->x,s->y,s->x2,s->y2,s->oam.tileNum,s->hFlip,s->vFlip,s->animEnded,s->animDelayCounter,s->callback==DestroyAnimSprite);for(int i=0;i<8;i++)printf(",%d",gBattleAnimArgs[i]);puts("");if(!s->inUse)return 0;}abort();}
'''
 def compile_run(name,code,inputs):
  path=out/(name+'.c');path.write_text(code);exes=[]
  for opt in ['O0','O2']:
   exe=out/(name+'-'+opt);helper.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(path),'-o',str(exe)]);exes.append(exe)
  results=[]
  for args in inputs:
   data=[subprocess.check_output([str(exe),*map(str,args)],text=True) for exe in exes]
   if data[0]!=data[1]:raise ValueError('Native optimization parity failure: '+str(args))
   results.append([list(map(int,line.split(','))) for line in data[0].splitlines()])
  return results
 cases=[]
 def case(kind,actor,args,variant):cases.append(dict(kind=kind,actor=actor,arguments=args+[2001+i for i in range(8-len(args))],variant=variant))
 for actor in range(4):
  for args in [[112,-16,140,128,36],[208,128,-16,48,36],[-16,112,256,-16,36],[108,128,84,-16,36],[-16,56,256,56,36]]:case(0,actor,args,'authored-paw')
  for args in [[100,0,100],[70,1,64],[55,0],[0,2,-32768]]:case(1,actor,args,'authored-petal' if args[0] else 'finite-frame-unused-arg')
 for args in [[0,0,0,0,1],[-32768,32767,32767,-32768,1],[100,100,101,99,3],[0,0,-1,1,36]]:case(0,0,args,'fast-linear-width-boundary')
 for actor in [0,1]:
  for y in [-32768,32767]:case(1,actor,[y,1,32767],'signed16-y-wrap')
 trajectories=compile_run('petal-paw',harness,[[c['kind'],c['actor'],*c['arguments']] for c in cases])
 for c,rows in zip(cases,trajectories):
  assert rows[-1][1]==0;c['rows']=rows
 pan_names=['BattleAnimAdjustPanning','Cmd_setpan'];pan_functions=[extract.function(s['src/battle_anim.c'],name) for name in pan_names]
 pan=r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
typedef uint8_t u8;typedef int8_t s8;
#define B_SIDE_PLAYER 0
#define B_SIDE_OPPONENT 1
#define SOUND_PAN_ATTACKER -64
#define SOUND_PAN_TARGET 63
static u8 gBattleAnimAttacker,gBattleAnimTarget;static const u8*sBattleAnimScriptPtr;static int observed,controls;
static struct {struct {u8 statusAnimActive;} healthBoxesData[4];} storage,*gBattleSpritesDataPtr=&storage;
static int GetBattlerSide(u8 id){return id&1;}
static void SE12PanpotControl(s8 pan){observed=pan;controls++;}
'''+ '\n'.join(pan_functions)+r'''
int main(void){for(int actor=0;actor<4;actor++)for(int target=0;target<4;target++)for(int pan=-128;pan<=127;pan++){u8 script[]={0x1a,(u8)pan,0xff};sBattleAnimScriptPtr=script;gBattleAnimAttacker=actor;gBattleAnimTarget=target;controls=0;Cmd_setpan();assert(sBattleAnimScriptPtr==script+2&&controls==1);printf("%d,%d,%d,%d\n",actor,target,pan,observed);}return 0;}
'''
 pan_rows=compile_run('setpan',pan,[[]])[0]
 return dict(schema='firered-petal-paw-source-v1',sourceSha256=pins,optimizationParity=['O0','O2'],sourceFunctions=names,panSourceFunctions=pan_names,semantics='Frame0 invokes original initializer then the original frame engine; later frames use original AnimateSprites. Shared harness destructor marks the isolated slot dead without clearing it: compare lifetime always and positions/players only while alive. Paw dummy END is normalized by runtime as a one-frame loop; its rendered tile remains0. Portable storedCallback replaces only ARM pointer splitting. All sprite inputs and unused shared registers are explicit. setpan executes the unmodified source opcode and signed8 adjustment for all256 inputs and16 actor/target pairs; statusAnimActive is false for these move roots.',cases=cases,setpan=pan_rows)

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--reference',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);a=p.parse_args()
 if a.output.exists():raise FileExistsError('Refusing to overwrite reviewed fixture: '+str(a.output))
 out=a.build_dir or Path(tempfile.mkdtemp(prefix='petal-paw-reference-'));out.mkdir(exist_ok=True,parents=True)
 r=build(a.root.resolve(),a.reference or a.root/'content/raw/pokefirered/037335f4',out);a.output.parent.mkdir(exist_ok=True,parents=True);a.output.write_text(json.dumps(r,separators=(',',':'))+'\n');print(json.dumps(dict(cases=len(r['cases']),rows=sum(len(c['rows']) for c in r['cases']),setpanRows=len(r['setpan']))))

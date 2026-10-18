#!/usr/bin/env python3
"""Extract the pinned C round-shadow callbacks and original sprite/affine driver.
No managed implementation or third-party build script is executed.
"""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile,sys
from pathlib import Path
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[3]
NAMES=['FlyBallUp','FlyBallAttack','BounceBallShrink','BounceBallLand','DiveBall','DiveWaterSplash']
CONFIGS=[[0,0,0,13,336],[0,-37,23,0,256],[0,127,-127,255,336],[0,0,0,1,4096],
 [1,20,0,0,0],[1,1,0,0,0],[1,255,0,0,0],[2,0,0,0,0],[2,-37,23,0,0],[3,0,0,0,0],
 [4,0,0,13,336],[4,-37,23,0,256],[4,127,-20,255,336],[5,0,0,0,0],[5,1,0,0,0]]
COLUMNS=['alive','x','y','x2','y2','invisible','tile','a','b','c','d','affineMode','xScale','yScale','rotation','affineEnded','affineIndex','affineDelay']+['data'+str(i) for i in range(8)]
def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def build(root):
 base=module('round_shadow_native_base',root/'tests/Game.Presentation.Tests/Reference/generate_orbit_spray_vectors.py')
 with tempfile.TemporaryDirectory() as t:
  p=Path(t)/'base.c';base.build(root,'tasks',p);h=p.read_text().split('static const long long config0')[0]
 raw=root/'content/raw/pokefirered/037335f4';names={'battle_anim_flying':['AnimFlyBallUp','AnimFlyBallUp_Step','AnimFlyBallAttack','AnimFlyBallAttack_Step','AnimBounceBallShrink','AnimBounceBallLand','AnimDiveBall','AnimDiveBall_Step1','AnimDiveBall_Step2','AnimDiveWaterSplash'], 'battle_anim_mons':['InitSpritePosToAnimAttacker','SetAnimSpriteInitialXOffset','InitAnimLinearTranslation','AnimTranslateLinear','TrySetSpriteRotScale','TryResetSpriteAffineState']}
 src={name:(raw/('src/'+name+'.c')).read_text() for name in names};funcs=[base.function(src[file],name) for file,functions in names.items() for name in functions]
 declarations=[];flying=src['battle_anim_flying']
 for name in ['sAffineAnim_FlyBallUp','sAffineAnim_FlyBallAttack_0','sAffineAnim_FlyBallAttack_1','sAffineAnim_BounceBallShrink','sAffineAnim_BounceBallLand','sAffineAnim_DiveBall']:
  declarations.append(base.declaration(flying,name))
 for name in ['FlyBallUp','FlyBallAttack','BounceBallShrink','BounceBallLand','DiveBall']:
  declarations.append(re.search(r'^static const union AffineAnimCmd \*const sAffineAnims_'+name+r'\[\]\s*=\s*\{.*?\};',flying,re.M|re.S)[0])
 for name in NAMES:declarations.append(re.search(r'^const struct SpriteTemplate g'+name+r'SpriteTemplate\s*=\s*\{.*?\};',flying,re.M|re.S)[0])
 spans=[]
 for snippet in funcs+declarations:
  file,text=next((n,s) for n,s in src.items() if snippet in s);line=text[:text.index(snippet)].count('\n')+1
  spans.append(dict(file='src/'+file+'.c',firstLine=line,lastLine=line+snippet.count('\n'),sha256=hashlib.sha256(snippet.encode()).hexdigest(),declaration=snippet.split('\n')[0]))
 h+='\n#define ANIM_TAG_ROUND_SHADOW 1\n#define ANIM_TAG_SPLASH 2\n#define gDummySpriteAnimTable dummyFrames\n#define gDummySpriteAffineAnimTable dummyAffines\n'
 h+='static const struct OamData gOamData_AffineDouble_ObjNormal_64x64={.affineMode=3,.size=3},gOamData_AffineNormal_ObjNormal_64x64={.affineMode=1,.size=3};\n'
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'+'\n'.join(declarations)+'\n'+'\n'.join(funcs)+'\n'
 h+='static const struct SpriteTemplate *const templates[]={'+','.join('&g'+name+'SpriteTemplate' for name in NAMES)+'};\n'
 h+='static const int configs[][5]={'+','.join('{'+','.join(map(str,c))+'}' for c in CONFIGS)+'};\n'+RUNNER
 for snippet in funcs+declarations:assert snippet in h
 return h,dict(schema='firered-round-shadow-charge-source-v1',sourceRevision=base.module('round_shadow_reference',root/'tests/Reference/reference_c.py').COMMIT,sourceSha256=base.SOURCE_SHA256,sourceSpans=spans,configs=CONFIGS,effectColumns=COLUMNS,geometry=dict(yOffset=[5,11,7,13],elevation=[2,3,4,5],heights=[64,48,32,56]),conventions=dict(config='kind, source args0..3',kinds=NAMES,phase='tick -1 after synchronous CreateSpriteAndAnimate; subsequent rows after original AnimateSprites',source='New callbacks, helpers, templates, affine declarations and sprite/affine driver extracted verbatim; host static coordinates, pointer storage and BIOS boundary reused',visibility='Actor starts hidden on release kinds1/3 and remains hidden after setup kinds0/2/4; splash leaves actor flag untouched',widths='Native signed16 positions/data and unsigned16 linear accumulators; original callback and affine update ordering'))
RUNNER=r'''
static int tick;
static int matrices(void){int n=0;for(int i=slots;i<32;i++)n+=(gOamMatrixAllocBitmap>>i)&1;return n;}
static void emit(void){printf("{\"tick\":%d,\"actorHidden\":%d,\"count\":%d,\"matrices\":%d,\"effect\":[",tick,gSprites[gBattleAnimAttacker].invisible,gAnimVisualTaskCount,matrices());struct Sprite*s=&gSprites[slots];if(s->inUse){struct OamMatrix m=gOamMatrices[s->oam.matrixNum];struct AffineAnimState*a=&sAffineAnimStates[s->oam.matrixNum];printf("1,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",s->x,s->y,s->x2,s->y2,s->invisible,s->oam.tileNum,m.a,m.b,m.c,m.d,s->oam.affineMode,a->xScale,a->yScale,a->rotation,s->affineAnimEnded,a->animCmdIndex,a->delayCounter);for(int i=0;i<8;i++)printf(",%d",s->data[i]);}printf("],\"args\":[");for(int i=0;i<8;i++)printf("%s%d",i?",":"",gBattleAnimArgs[i]);puts("]}");}
int main(int argc,char**argv){assert(argc==5);mode=atoi(argv[1]);gBattleAnimAttacker=atoi(argv[2]);gBattleAnimTarget=atoi(argv[3]);const int*c=configs[atoi(argv[4])];int kind=c[0];slots=mode?4:2;memset(gSprites,0,sizeof(gSprites));memset(sAffineAnimStates,0,sizeof(sAffineAnimStates));gOamMatrixAllocBitmap=(1u<<slots)-1;gAnimVisualTaskCount=0;for(int i=0;i<32;i++)SetOamMatrix(i,256,0,0,256);for(int i=0;i<slots;i++){struct Sprite*s=&gSprites[i];s->inUse=TRUE;s->usingSheet=TRUE;s->animBeginning=TRUE;s->callback=SpriteCallbackDummy;s->anims=dummyFrames;s->affineAnims=dummyAffines;s->affineAnimPaused=TRUE;s->x=GetBattlerSpriteCoord(i,2);s->y=GetBattlerSpriteCoord(i,3)-(!(i&1)?8:0);s->subpriority=GetBattlerSpriteSubpriority(i);AffineAnimStateReset(i);}gSprites[gBattleAnimAttacker].invisible=kind==1||kind==3;memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));for(int i=0;i<4;i++)gBattleAnimArgs[i]=c[i+1];CreateSpriteAndAnimate(templates[kind],GetBattlerSpriteCoord(gBattleAnimTarget,2),GetBattlerSpriteCoord(gBattleAnimTarget,3),GetBattlerSpriteSubpriority(gBattleAnimTarget)-2);gAnimVisualTaskCount++;tick=-1;emit();for(tick=0;tick<3000;tick++){AnimateSprites();emit();if(!gAnimVisualTaskCount){assert(matrices()==0);return 0;}}abort();}
'''
def main():
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,default=ROOT);p.add_argument('--output-dir',type=Path,required=True);a=p.parse_args();a.output_dir.mkdir(parents=True,exist_ok=True);out=a.output_dir/'round-shadow-charge-source-vectors.json'
 if out.exists():p.error('Refusing to overwrite existing fixture candidate')
 source,result=build(a.root);c=a.output_dir/'round-shadow-charge.c';c.write_text(source);ref=module('round_shadow_reference_c',a.root/'tests/Reference/reference_c.py')
 for opt in ['O0','O2']:ref.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(c),'-o',str(a.output_dir/('round-shadow-charge-'+opt))])
 cases=[]
 for d in [0,1]:
  for actor in range(4 if d else 2):
   for target in ([actor^1,actor^2,actor^3] if d else [actor^1]):
    for ci in range(len(CONFIGS)):
     args=[str(d),str(actor),str(target),str(ci)];runs=[subprocess.check_output([str(a.output_dir/('round-shadow-charge-'+opt)),*args]) for opt in ['O0','O2']];assert runs[0]==runs[1]
     rows=[json.loads(line) for line in runs[0].splitlines()];assert rows[-1]['count']==0 and rows[-1]['matrices']==0 and not rows[-1]['effect']
     cases.append(dict(doubles=bool(d),actor=actor,target=target,config=ci,rows=rows))
 # Intern identical rows and trajectories. Every frame is still checked, while
 # stationary affine phases and actor-only setup trajectories are stored once.
 pool=[];lookup={};paths=[];path_lookup={};compact=[]
 for case in cases:
  indexes=[]
  for tick,row in enumerate(case.pop('rows')):
   assert row['tick']==tick-1
   assert row['args']==CONFIGS[case['config']][1:]+[0]*4
   values=[row['actorHidden'],row['count'],row['matrices']]+row['effect']
   key=tuple(values)
   if key not in lookup:lookup[key]=len(pool);pool.append(values)
   indexes.append(lookup[key])
  key=tuple(indexes)
  if key not in path_lookup:path_lookup[key]=len(paths);paths.append(indexes)
  compact.append([int(case['doubles']),case['actor'],case['target'],case['config'],path_lookup[key]])
 result.update(cases=compact,rowPool=pool,trajectories=paths,optimizationParity=['O0','O2'],coverage=dict(caseCount=len(cases),rowCount=sum(len(paths[c[4]]) for c in compact)),harnessSha256=hashlib.sha256(source.encode()).hexdigest())
 out.write_text(json.dumps(result,separators=(',',':'))+'\n');print(json.dumps(dict(**result['coverage'],sha256=hashlib.sha256(out.read_bytes()).hexdigest())))
if __name__=='__main__':main()

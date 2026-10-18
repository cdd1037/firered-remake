#!/usr/bin/env python3
"""Original pinned C callbacks/player; no managed or upstream build is executed."""
import argparse,hashlib,importlib.util,json,subprocess,tempfile,sys
from pathlib import Path
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[3]
SPRITE_COLUMNS=['x','y','x2','y2','invisible','a','b','c','d','affineMode']
EFFECT_COLUMNS=['alive','x','y','x2','y2','invisible','tile','a','b','c','d','affineMode','xScale','yScale','rotation','affineEnded','affineIndex','affineDelay']+['data'+str(i) for i in range(6)]
CONFIGS=[
 [0,32,0,16,16,0,7,40,0,0,0],
 [0,32,0,16,16,85,7,40,0,0,0],
 [0,32,0,16,16,170,7,40,0,0,0],
 [0,-37,23,-11,0,255,-7,1,0,0,0],
 [0,32767,-32768,-32768,16,128,255,3,0,0,0],
 [1,0,-24,0,0,10,0,0,0,0,0],
 [1,0,24,0,0,10,1,0,-24,0,0],
 [1,1,-17,13,1,7,0,0,9,-5,0],
 [1,0,32767,-32768,1,3,1,0,32767,-32768,0],
 [1,0,-128,127,0,11,0,0,-128,127,0],
 [2,0,0,0,0,0,0,0,0,0,0],
 [2,0,0,0,0,0,0,0,13,-9,1],
 [3,1,0,0,0,0,0,0,0,4,0],
 [3,1,0,0,0,0,0,0,13,-9,1],
]
def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
def build(root):
 base=module('charge_native_base',root/'tests/Game.Presentation.Tests/Reference/generate_orbit_spray_vectors.py')
 with tempfile.TemporaryDirectory() as t:
  p=Path(t)/'base.c';base.build(root,'tasks',p);h=p.read_text().split('static const long long config0')[0]
 raw=root/'content/raw/pokefirered/037335f4';paths={'battle_anim_effects_2':['AnimRazorWindTornado'],'battle_anim_effects_1':['AnimTask_SkullBashPosition','AnimTask_SkullBashPositionSet','AnimTask_SkullBashPositionReset'],'battle_anim_mon_movement':['SlideMonToOffsetAndBack','SlideMonToOffsetAndBack_End'],'battle_anim_mons':['TranslateSpriteInCircle','TranslateSpriteLinearByIdFixedPoint','InitSpriteDataForLinearTranslation','InitSpritePosToAnimAttacker','SetAnimSpriteInitialXOffset','SetBattlerSpriteYOffsetFromRotation']}
 sources={name:(raw/('src/'+name+'.c')).read_text() for name in paths};pins=dict(base.SOURCE_SHA256)
 pins['src/battle_anim_mon_movement.c']='ce72119ff5d658784180bae2e171fae2affa45077e9a1d5e2d55a2caf1ce628c'
 if hashlib.sha256((raw/'src/battle_anim_mon_movement.c').read_bytes()).hexdigest()!=pins['src/battle_anim_mon_movement.c']:raise ValueError('Pinned movement source mismatch')
 funcs=[base.function(sources[file],name) for file,names in paths.items() for name in names];decl=base.declaration(sources['battle_anim_effects_2'],'sRazorWindTornadoAffineAnimCmds')
 spans=[]
 for snippet in funcs+[decl]:
  name,source=next((n,s) for n,s in sources.items() if snippet in s);line=source[:source.index(snippet)].count('\n')+1
  spans.append(dict(file='src/'+name+'.c',firstLine=line,lastLine=line+snippet.count('\n'),sha256=hashlib.sha256(snippet.encode()).hexdigest(),declaration=snippet.split('\n')[0]))
 h+='\n#define sCirclePos data[0]\n#define sAmplitude data[1]\n#define sCircleSpeed data[2]\n#define sDuration data[3]\nstatic const u8 gBattlerSpriteIds[4]={0,1,2,3};\n'
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'+decl+'\n'+'\n'.join(funcs)+'\n'
 h+='static const union AffineAnimCmd *const razorAffine[]={sRazorWindTornadoAffineAnimCmds};\nstatic const struct SpriteTemplate razor={1,1,&oamAffine,dummyFrames,NULL,razorAffine,AnimRazorWindTornado},slideBack={0,0,&oamOff,dummyFrames,NULL,dummyAffines,SlideMonToOffsetAndBack};\n'
 h+='static const int configs[][11]={'+','.join('{'+','.join(map(str,c))+'}' for c in CONFIGS)+'};\n'+RUNNER
 for snippet in funcs+[decl]:assert snippet in h
 return h,dict(schema='firered-charge-pair-source-v1',sourceRevision=base.module('reference_c',root/'tests/Reference/reference_c.py').COMMIT,sourceSha256=pins,sourceSpans=spans,configs=CONFIGS,spriteColumns=SPRITE_COLUMNS,effectColumns=EFFECT_COLUMNS,geometry=dict(yOffset=[5,11,7,13],elevation=[2,3,4,5],heights=[64,48,32,56]),conventions=dict(config='kind, source args0..6, initial selected x2/y2, hidden-present flag',kinds=['tornado','slide-and-back','skull-set','skull-reset'],phase='tick -1 after CreateSpriteAndAnimate/task initializer; beforeTask follows AnimateSprites; afterTask follows RunTasks',source='New callbacks plus native source sprite/affine player and shared SetSpriteRotScale/Prepare/Reset extracted verbatim',host='Static source singles/doubles geometry, IsContest=false, pointer storage adapter, BIOS Q14 boundary. No upstream scripts or managed implementation. Sprite destruction uses exact source reset and zero effect row.',widths='Native signed16 data/positions/accumulators; -fwrapv preserves ARM wrap; portable helper handles installed Linux/Windows C compiler'))
RUNNER=r'''
static int tick,kind,currentConfig,selected;
static void sprite(const struct Sprite*s){struct OamMatrix m=gOamMatrices[s->oam.matrixNum];printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",s->x,s->y,s->x2,s->y2,s->invisible,m.a,m.b,m.c,m.d,s->oam.affineMode);}
static void effect(void){struct Sprite*s=&gSprites[slots];if(!s->inUse){printf("[]");return;}struct OamMatrix m=gOamMatrices[s->oam.matrixNum];struct AffineAnimState*a=&sAffineAnimStates[s->oam.matrixNum];printf("[1,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",s->x,s->y,s->x2,s->y2,s->invisible,s->oam.tileNum,m.a,m.b,m.c,m.d,s->oam.affineMode,a->xScale,a->yScale,a->rotation,s->affineAnimEnded,a->animCmdIndex,a->delayCounter);for(int i=0;i<6;i++)printf(",%d",s->data[i]);printf("]");}
static void prefix(void){printf("{\"tick\":%d,\"beforeTask\":",tick);sprite(&gSprites[selected]);}
static void finish(void){printf(",\"afterTask\":");sprite(&gSprites[selected]);printf(",\"effect\":");effect();printf(",\"count\":%d,\"taskAlive\":%d,\"args\":[",gAnimVisualTaskCount,gTasks[0].isActive);for(int i=0;i<8;i++)printf("%s%d",i?",":"",gBattleAnimArgs[i]);printf("],\"taskData\":[");for(int i=0;i<16;i++)printf("%s%d",i?",":"",gTasks[0].data[i]);puts("]}");}
static void setup(void){slots=mode?4:2;memset(gSprites,0,sizeof(gSprites));memset(gTasks,0,sizeof(gTasks));memset(sAffineAnimStates,0,sizeof(sAffineAnimStates));memset(&battleSpritesData,0,sizeof(battleSpritesData));gAnimVisualTaskCount=0;gOamMatrixAllocBitmap=(1u<<slots)-1;for(int i=0;i<32;i++)SetOamMatrix(i,256,0,0,256);for(int i=0;i<slots;i++){struct Sprite*s=&gSprites[i];s->inUse=TRUE;s->usingSheet=TRUE;s->animBeginning=TRUE;s->callback=SpriteCallbackDummy;s->anims=dummyFrames;s->affineAnims=dummyAffines;s->affineAnimPaused=TRUE;s->data[0]=i;s->x=GetBattlerSpriteCoord(i,2);s->y=GetBattlerSpriteCoord(i,3)-(!(i&1)?8:0);s->subpriority=GetBattlerSpriteSubpriority(i);battleSpritesData.healthBoxesData[i].matrixNum=i;AffineAnimStateReset(i);}}
int main(int argc,char**argv){assert(argc==5);mode=atoi(argv[1]);gBattleAnimAttacker=atoi(argv[2]);gBattleAnimTarget=atoi(argv[3]);currentConfig=atoi(argv[4]);const int*c=configs[currentConfig];kind=c[0];setup();memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));for(int i=0;i<7;i++)gBattleAnimArgs[i]=c[i+1];selected=kind==1&&c[1]!=0?gBattleAnimTarget:gBattleAnimAttacker;struct Sprite*s=&gSprites[selected];s->x2=c[8];s->y2=c[9];s->invisible=c[10];if(kind==3){PrepareBattlerSpriteForRotScale(selected,0);SetSpriteRotScale(selected,256,256,(selected&1)?0x600:-0x600);s->invisible=c[10];}tick=-1;if(kind<2){CreateSpriteAndAnimate(kind==0?&razor:&slideBack,GetBattlerSpriteCoord(gBattleAnimTarget,2),GetBattlerSpriteCoord(gBattleAnimTarget,3),GetBattlerSpriteSubpriority(gBattleAnimAttacker)-2);gAnimVisualTaskCount++;}else{gTasks[0].isActive=TRUE;gAnimVisualTaskCount++;AnimTask_SkullBashPosition(0);}prefix();finish();for(tick=0;tick<200;tick++){AnimateSprites();prefix();if(gTasks[0].isActive)gTasks[0].func(0);finish();if(!gAnimVisualTaskCount)return 0;}abort();}
'''
def main():
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,default=ROOT);p.add_argument('--output-dir',type=Path,required=True);a=p.parse_args();a.output_dir.mkdir(parents=True,exist_ok=True);out=a.output_dir/'charge-pair-source-vectors.json'
 if out.exists():p.error('Refusing to overwrite existing fixture candidate')
 source,result=build(a.root);c=a.output_dir/'charge-pair.c';c.write_text(source);ref=module('charge_reference_c',a.root/'tests/Reference/reference_c.py')
 for opt in ['O0','O2']:ref.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(c),'-o',str(a.output_dir/('charge-pair-'+opt))])
 cases=[]
 for d in [0,1]:
  for actor in range(4 if d else 2):
   for ci,config in enumerate(CONFIGS):
    targets=[actor^1]+([actor^2] if d and config[0]==0 else [])
    for target in targets:
     args=[str(d),str(actor),str(target),str(ci)];runs=[subprocess.check_output([str(a.output_dir/('charge-pair-'+opt)),*args]) for opt in ['O0','O2']];assert runs[0]==runs[1]
     rows=[json.loads(line) for line in runs[0].splitlines()];assert rows[-1]['count']==0 and rows[-1]['taskAlive']==0 and not rows[-1]['effect']
     cases.append(dict(doubles=bool(d),actor=actor,target=target,config=ci,rows=rows))
 result.update(cases=cases,optimizationParity=['O0','O2'],coverage=dict(caseCount=len(cases),rowCount=sum(len(c['rows']) for c in cases)))
 out.write_text(json.dumps(result,separators=(',',':'))+'\n');print(json.dumps(dict(**result['coverage'],sha256=hashlib.sha256(out.read_bytes()).hexdigest())))
if __name__=='__main__':main()

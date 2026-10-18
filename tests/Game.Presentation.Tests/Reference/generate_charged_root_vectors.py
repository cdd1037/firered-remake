#!/usr/bin/env python3
"""Pinned Sky Attack / Spit Up native callbacks with shared sprite and task players.
Default cases are explicit semantic and boundary cases, preserving every frame.
--full selects the larger source argument corpus for the same managed comparator.
Generated JSON and native harnesses are local test inputs, never tracked products.
"""
import argparse,hashlib,importlib.util,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
# kind (bird, orb, deform), doubles, actor, target, phase, duration, hidden.
DEFAULT_CASES={
 'bird-player':[0,0,0,1,0,0,0], 'bird-opponent':[0,0,1,0,0,0,0],
 'bird-doubles-left-opponent':[0,1,0,1,0,0,0], 'bird-doubles-right-opponent':[0,1,2,3,0,0,0],
 'bird-doubles-opponent-left':[0,1,1,0,0,0,0], 'bird-doubles-opponent-right':[0,1,3,2,0,0,0],
 'bird-player-partner':[0,1,0,2,0,0,0], 'bird-player-partner-reverse':[0,1,2,0,0,0,0],
 'bird-opponent-partner':[0,1,1,3,0,0,0], 'bird-opponent-partner-reverse':[0,1,3,1,0,0,0],
 'bird-cross-right':[0,1,0,3,0,0,0], 'bird-cross-left':[0,1,3,0,0,0,0],
 'bird-cross-upper':[0,1,1,2,0,0,0], 'bird-cross-lower':[0,1,2,1,0,0,0],
 # Every authored radial phase, including stronger branch interleaves.
 **{f'orb-authored-{a}':[1,0,0,1,a,12,0] for a in range(0,256,16)},
 'orb-phase255':[1,0,0,1,255,12,0], 'orb-duration0':[1,0,0,1,0,0,0],
 'orb-duration1':[1,0,1,0,64,1,0], 'orb-duration255':[1,1,2,3,128,255,0],
 'orb-hidden-rear':[1,1,3,1,224,12,1],
 'deform-player':[2,0,0,1,0,0,0], 'deform-opponent':[2,0,1,0,0,0,0],
 'deform-rear-player':[2,1,2,3,0,0,0], 'deform-rear-opponent':[2,1,3,2,0,0,0],
 'deform-hidden-player':[2,1,0,2,0,0,1], 'deform-hidden-opponent':[2,1,1,3,0,0,1],
}
COLUMNS=['slot','x','y','x2','y2','invisible','tile','hflip','vflip','subpriority','a','b','c','d','affineMode','objMode','priority','frameIndex','frameDelay','frameEnded','affineEnded','affineIndex','affineDelay','scaleX','scaleY','rotation']+['data'+str(i) for i in range(8)]
def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def build(root):
 reference=root/'content/raw/pokefirered/037335f4';pg=module('charged_root_native_power',root/'tests/Game.Presentation.Tests/Reference/generate_power_guard_vectors.py');base=module('charged_root_native_base',root/'tests/Game.Presentation.Tests/Reference/generate_orbit_spray_vectors.py')
 h,metadata=pg.build(root,reference);h=h.split('static const long long config0')[0]
 src={n:(reference/('src/'+n+'.c')).read_text() for n in ['battle_anim_flying','battle_anim_effects_2','battle_anim_effects_3','battle_anim_mons']}
 funcs=[base.function(src[file],name) for file,names in [('battle_anim_flying',['AnimSkyAttackBird','AnimSkyAttackBird_Step']),('battle_anim_effects_2',['AnimSpitUpOrb','AnimSpitUpOrb_Step']),('battle_anim_effects_3',['AnimTask_SpitUpDeformMon']),('battle_anim_mons',['TrySetSpriteRotScale'])] for name in names]
 decls=[base.declaration(src[file],name) for file,name in [('battle_anim_effects_2','sSpitUpOrbAffineAnimCmds'),('battle_anim_effects_3','sSpitUpDeformMonAffineAnimCmds')]]
 decls.append(re.search(r'static const union AffineAnimCmd \*const sSpitUpOrbAffineAnimTable\[\]\s*=\s*\{.*?\};',src['battle_anim_effects_2'],re.S)[0])
 decls += [re.search(r'const struct SpriteTemplate '+name+r'\s*=\s*\{.*?\};',src[file],re.S)[0] for file,name in [('battle_anim_effects_2','gSpitUpOrbSpriteTemplate'),('battle_anim_flying','gSkyAttackBirdSpriteTemplate')]]
 h+='\n#define ANIM_TAG_BIRD 8\n#define ANIM_TAG_RED_ORB_2 9\nstatic const struct OamData gOamData_AffineDouble_ObjNormal_8x8={.affineMode=3,.priority=2},gOamData_AffineDouble_ObjNormal_64x64={.affineMode=3,.priority=2,.size=3};\n'
 h+=(root/'tests/Game.Presentation.Tests/Reference/bios_angles_adapter.c').read_text()+'\n'
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'+'\n'.join(decls+funcs)+'\n'+RUNNER
 spans=[]
 for snippet in funcs+decls:
  file,text=next((n,s) for n,s in src.items() if snippet in s);line=text[:text.index(snippet)].count('\n')+1;spans.append(dict(file='src/'+file+'.c',firstLine=line,lastLine=line+snippet.count('\n'),sha256=hashlib.sha256(snippet.encode()).hexdigest(),declaration=snippet.split('\n')[0]));assert snippet in h
 return h,dict(sourceRevision=metadata['sourceRevision'],sourceSha256=metadata['sourceSha256'],sourceSpans=spans,columns=COLUMNS,geometry=metadata['geometry'],conventions=dict(config='kind,doubles,actor,target,phase,duration,hidden',tickMinusOne='After source synchronous creation and initial AnimateSprite or task initialization',order='AnimateSprites then ordinary task callback; complete post-task state retained',source='Original callbacks, sprite/frame/affine driver, task interpreter; external BIOS adapter and fixed geometry only',registers='Source Spit Up constructor receives the retained duration register; full script register persistence is separately tested'))
RUNNER=r'''
static int kind,tick;
static int live(void){int n=0;for(int i=slots;i<64;i++)n+=gSprites[i].inUse;return n;}
static void emitSprite(struct Sprite*s,int slot){if(!s->inUse){printf("[]");return;}struct OamMatrix m=(s->oam.affineMode&1)?gOamMatrices[s->oam.matrixNum]:(struct OamMatrix){256,0,0,256};struct AffineAnimState*a=&sAffineAnimStates[s->oam.matrixNum];int row[]={slot,s->x,s->y,s->x2,s->y2,s->invisible,s->oam.tileNum,(s->oam.matrixNum>>3)&1,(s->oam.matrixNum>>4)&1,s->subpriority,m.a,m.b,m.c,m.d,s->oam.affineMode,s->oam.objMode,s->oam.priority,s->animCmdIndex,s->animDelayCounter,s->animEnded,s->affineAnimEnded,a->animCmdIndex,a->delayCounter,a->xScale,a->yScale,a->rotation};if(s->oam.affineMode&1)row[7]=row[8]=0;printf("[");for(unsigned i=0;i<sizeof(row)/sizeof(row[0]);i++)printf("%s%d",i?",":"",row[i]);for(int i=0;i<8;i++)printf(",%d",s->data[i]);printf("]");}
static void emit(void){printf("[%d,%d,%d,%u,",tick,gAnimVisualTaskCount,gTasks[0].isActive,gOamMatrixAllocBitmap);emitSprite(&gSprites[slots],slots);printf(",");emitSprite(&gSprites[gBattleAnimAttacker],gBattleAnimAttacker);printf(",[");for(int i=0;i<13;i++)printf("%s%d",i?",":"",gTasks[0].data[i]);puts("]]");}
int main(int argc,char**argv){assert(argc==8);kind=atoi(argv[1]);mode=atoi(argv[2]);gBattleAnimAttacker=atoi(argv[3]);gBattleAnimTarget=atoi(argv[4]);slots=mode?4:2;memset(gSprites,0,sizeof(gSprites));memset(gTasks,0,sizeof(gTasks));memset(sAffineAnimStates,0,sizeof(sAffineAnimStates));memset(&battleSpritesData,0,sizeof(battleSpritesData));gOamMatrixAllocBitmap=(1u<<slots)-1;gAnimVisualTaskCount=0;for(int i=0;i<32;i++)SetOamMatrix(i,256,0,0,256);for(int i=0;i<slots;i++){struct Sprite*s=&gSprites[i];s->inUse=TRUE;s->usingSheet=TRUE;s->animBeginning=TRUE;s->callback=SpriteCallbackDummy;s->anims=dummyFrames;s->affineAnims=dummyAffines;s->oam.affineMode=1;s->oam.matrixNum=i;s->oam.paletteNum=i;s->affineAnimPaused=TRUE;s->data[0]=i;s->x=GetBattlerSpriteCoord(i,2);s->y=GetBattlerSpriteCoord(i,3);s->subpriority=GetBattlerSpriteSubpriority(i);battleSpritesData.healthBoxesData[i].matrixNum=i;AffineAnimStateReset(i);}
if(atoi(argv[7])){gSprites[gBattleAnimAttacker].invisible=TRUE;battleSpritesData.battlerData[gBattleAnimAttacker].invisible=TRUE;}
memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));gBattleAnimArgs[0]=atoi(argv[5]);gBattleAnimArgs[1]=atoi(argv[6]);if(kind<2){CreateSpriteAndAnimate(kind==0?&gSkyAttackBirdSpriteTemplate:&gSpitUpOrbSpriteTemplate,GetBattlerSpriteCoord(gBattleAnimTarget,2),GetBattlerSpriteCoord(gBattleAnimTarget,3),GetBattlerSpriteSubpriority(kind==0?gBattleAnimTarget:gBattleAnimAttacker)-2);gAnimVisualTaskCount++;}else{gTasks[0].isActive=TRUE;gTasks[0].func=AnimTask_SpitUpDeformMon;gAnimVisualTaskCount++;gTasks[0].func(0);}
tick=-1;emit();for(tick=0;tick<1000;tick++){AnimateSprites();if(gTasks[0].isActive)gTasks[0].func(0);emit();if(!gAnimVisualTaskCount){assert(live()==0);assert(gOamMatrixAllocBitmap==((1u<<slots)-1));return 0;}}abort();}
'''
def main():
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,default=ROOT);p.add_argument('--output-dir',type=Path,required=True);p.add_argument('--full',action='store_true');a=p.parse_args();a.output_dir.mkdir(parents=True,exist_ok=True);out=a.output_dir/'charged-root-source-vectors.json'
 if out.exists():p.error('Refusing to overwrite an existing candidate')
 source,result=build(a.root);c=a.output_dir/'charged-root-native.c';c.write_text(source);ref=module('charged_root_reference_c',a.root/'tests/Reference/reference_c.py')
 for opt in ['O0','O2']:ref.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(c),'-o',str(a.output_dir/('charged-root-'+opt))])
 configs=dict(DEFAULT_CASES)
 if a.full:
  for d in [0,1]:
   for actor in range(4 if d else 2):
    for target in ([actor^1,actor^2,actor^3] if d else [actor^1]):
     for angle in range(0,256,8):
      for duration in [0,1,12,255]:configs[f'full-orb-{d}-{actor}-{target}-{angle}-{duration}']=[1,d,actor,target,angle,duration,0]
 cases={}
 for name,config in configs.items():
  args=list(map(str,config));runs=[subprocess.check_output([str(a.output_dir/('charged-root-'+opt)),*args]) for opt in ['O0','O2']];assert runs[0]==runs[1],name
  rows=[json.loads(line) for line in runs[0].splitlines()];assert rows[0][0]==-1 and rows[-1][1:3]==[0,0] and not rows[-1][4],name
  assert [r[0] for r in rows]==list(range(-1,len(rows)-1)),name
  cases[name]=dict(config=config,rows=rows)
 result.update(profile='full' if a.full else 'representative',representativeCases=list(DEFAULT_CASES),cases=cases,optimizationParity=['O0','O2'],coverage=dict(caseCount=len(cases),rowCount=sum(len(c['rows']) for c in cases.values())),harnessSha256=hashlib.sha256(source.encode()).hexdigest())
 out.write_text(json.dumps(result,separators=(',',':'))+'\n');print(json.dumps(result['coverage']))
if __name__=='__main__':main()

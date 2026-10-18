#!/usr/bin/env python3
"""Pinned original-C Thrash task pair, including independent base and pos2 writes.
The maintained native task/player harness supplies only ABI/geometry/BIOS adapters.
"""
import argparse,hashlib,importlib.util,json,subprocess,tempfile,sys
from pathlib import Path
sys.dont_write_bytecode=True
CONFIGS=[[0,0,0,0,0],[0,0,3,-5,1],[19,-17,-3,7,0],[32767,-32768,0,0,0],[-32768,32767,32767,-32768,1]]
SPRITE_COLUMNS=['x','y','x2','y2','invisible','a','b','c','d','affineMode']
STATE_COLUMNS=['tick','visualCount','horizontalAlive','verticalAlive']+['horizontalData'+str(i) for i in range(1,13)]+['verticalData'+str(i) for i in range(1,16)]
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def build(root):
 base=module('locked_native_base',root/'tests/Game.Presentation.Tests/Reference/generate_orbit_spray_vectors.py')
 with tempfile.TemporaryDirectory() as tmp:
  c=Path(tmp)/'base.c';base.build(root,'tasks',c);h=c.read_text().split('static const long long config0')[0]
 source=(root/'content/raw/pokefirered/037335f4/src/battle_anim_effects_2.c').read_text()
 names=['AnimTask_ThrashMoveMonHorizontal','AnimTask_ThrashMoveMonHorizontal_Step','AnimTask_ThrashMoveMonVertical','AnimTask_ThrashMoveMonVertical_Step']
 funcs=[base.function(source,n) for n in names];decl=base.declaration(source,'sThrashMoveMonAffineAnimCmds');spans=[]
 for snippet in funcs+[decl]:
  start=source.index(snippet);line=source[:start].count('\n')+1;spans.append(dict(file='src/battle_anim_effects_2.c',firstLine=line,lastLine=line+snippet.count('\n'),sha256=hashlib.sha256(snippet.encode()).hexdigest(),declaration=snippet.split('\n')[0]))
 h=h.replace('static struct Task gTasks[1];','static struct Task gTasks[2];').replace('assert(n==0);gTasks[n].isActive=FALSE;', 'assert(n<2);gTasks[n].isActive=FALSE;')
 h+='\n'+'\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'+decl+'\n'+'\n'.join(funcs)+'\n'
 h+='static const int configs[][5]={'+','.join('{'+','.join(map(str,c))+'}' for c in CONFIGS)+'};\n'+RUNNER
 for snippet in funcs+[decl]:assert snippet in h
 return h,dict(sourceRevision='037335f4c725d7c9aecdac87066f2002b4bd7e14',sourceSha256=base.SOURCE_SHA256,sourceSpans=spans,configs=CONFIGS,spriteColumns=SPRITE_COLUMNS,stateColumns=STATE_COLUMNS,geometry=dict(yOffset=[5,11,7,13],elevation=[2,3,4,5],heights=[64,48,32,56]),conventions=dict(kinds=['horizontal','vertical','both'],config='x/y overrides when nonzero, initial x2/y2, hidden-present flag',phase='tick -1 after original synchronous initialization; P is before RunTasks, Q after; raw coordinates are separate from pos2',identity='Host sprite IDs only identify participants; no callback arithmetic on IDs',capacity='Original 64-sprite capacity retained; two task cells needed for simultaneous callbacks',program='Original PrepareAffineAnimInTaskData, RunAffineAnimFromTaskData, SetSpriteRotScale and foot anchoring are reused verbatim'))
RUNNER=r'''
static int kind,currentConfig,tick;
static void prefix(char type){printf("%d:%d:%d:%d,%c,%d",kind,mode,gBattleAnimAttacker,currentConfig,type,tick);}
static void sprite(char type){struct Sprite*s=&gSprites[gBattleAnimAttacker];struct OamMatrix m=gOamMatrices[s->oam.matrixNum];int row[]={s->x,s->y,s->x2,s->y2,s->invisible,m.a,m.b,m.c,m.d,s->oam.affineMode};prefix(type);for(unsigned i=0;i<sizeof(row)/sizeof(row[0]);i++)printf(",%d",row[i]);puts("");}
static void state(void){prefix('T');printf(",%d,%d,%d",gAnimVisualTaskCount,gTasks[0].isActive,gTasks[1].isActive);for(int i=1;i<=12;i++)printf(",%d",gTasks[0].data[i]);for(int i=1;i<=15;i++)printf(",%d",gTasks[1].data[i]);puts("");}
static void run(int k,int actor,int config){
 kind=k;currentConfig=config;tick=-1;slots=mode?4:2;gBattleAnimAttacker=actor;gBattleAnimTarget=actor^1;
 memset(gSprites,0,sizeof(gSprites));memset(gTasks,0,sizeof(gTasks));memset(sAffineAnimStates,0,sizeof(sAffineAnimStates));memset(&battleSpritesData,0,sizeof(battleSpritesData));gAnimVisualTaskCount=0;gOamMatrixAllocBitmap=(1u<<slots)-1;for(int i=0;i<32;i++)SetOamMatrix(i,256,0,0,256);
 for(int i=0;i<slots;i++){struct Sprite*s=&gSprites[i];s->inUse=TRUE;s->usingSheet=TRUE;s->animBeginning=TRUE;s->callback=SpriteCallbackDummy;s->anims=dummyFrames;s->affineAnims=dummyAffines;s->oam.affineMode=0;s->oam.matrixNum=0;s->affineAnimPaused=TRUE;s->data[0]=i;s->x=GetBattlerSpriteCoord(i,2);s->y=GetBattlerSpriteCoord(i,3)-(!(i&1)?8:0);s->subpriority=GetBattlerSpriteSubpriority(i);battleSpritesData.healthBoxesData[i].matrixNum=i;AffineAnimStateReset(i);}
 struct Sprite*s=&gSprites[actor];const int*c=configs[config];if(c[0])s->x=c[0];if(c[1])s->y=c[1];s->x2=c[2];s->y2=c[3];s->invisible=c[4];
 if(kind!=1){gTasks[0].isActive=TRUE;gAnimVisualTaskCount++;AnimTask_ThrashMoveMonHorizontal(0);}if(kind!=0){gTasks[1].isActive=TRUE;gAnimVisualTaskCount++;AnimTask_ThrashMoveMonVertical(1);}
 sprite('P');sprite('Q');state();
 for(tick=0;tick<200;tick++){AnimateSprites();sprite('P');for(int i=0;i<2;i++)if(gTasks[i].isActive)gTasks[i].func(i);sprite('Q');state();if(!gAnimVisualTaskCount)return;}abort();}
int main(void){for(mode=0;mode<2;mode++)for(int actor=0;actor<(mode?4:2);actor++)for(int k=0;k<3;k++)for(int c=0;c<5;c++)run(k,actor,c);}
'''

# Whole cases selected from source branches; never sample frames.
REPRESENTATIVE_CASES = (
 # Horizontal, vertical and simultaneous tasks: all initial-state and signed-coordinate boundaries
 '0:0:0:0','0:0:0:1','0:0:0:2','0:0:0:3','0:0:0:4',
 # Reverse side and affine foot anchoring
 '0:0:1:0',
 # Doubles hidden rear battler and signed initial offsets
 '0:1:2:4',
 # Doubles reverse side signed base positions
 '0:1:3:3',
 # Horizontal, vertical and simultaneous tasks: all initial-state and signed-coordinate boundaries
 '1:0:0:0','1:0:0:1','1:0:0:2','1:0:0:3','1:0:0:4',
 # Reverse side and affine foot anchoring
 '1:0:1:0',
 # Doubles hidden rear battler and signed initial offsets
 '1:1:2:4',
 # Doubles reverse side signed base positions
 '1:1:3:3',
 # Horizontal, vertical and simultaneous tasks: all initial-state and signed-coordinate boundaries
 '2:0:0:0','2:0:0:1','2:0:0:2','2:0:0:3','2:0:0:4',
 # Reverse side and affine foot anchoring
 '2:0:1:0',
 # Doubles hidden rear battler and signed initial offsets
 '2:1:2:4',
 # Doubles reverse side signed base positions
 '2:1:3:3',
)

def apply_profile(result, full=False):
 all_cases=result['cases']
 assert set(REPRESENTATIVE_CASES) <= all_cases.keys(), 'Missing explicit representative case'
 result['profile']='full' if full else 'representative'
 result['cases']=all_cases if full else {key:all_cases[key] for key in REPRESENTATIVE_CASES}
 coverage=result.setdefault('coverage',{})
 coverage.update(fullCaseCount=len(all_cases),caseCount=len(result['cases']),rowCount=sum(map(len,result['cases'].values())),representativeCaseCount=len(REPRESENTATIVE_CASES))
 return result

def main():
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output-dir',type=Path,required=True);p.add_argument('--full',action='store_true',help='Emit all source-C cases for the same managed comparator');a=p.parse_args();a.output_dir.mkdir(parents=True,exist_ok=True);out=a.output_dir/'locked_pair_vectors.json'
 if out.exists():p.error('Refusing to overwrite an existing candidate')
 source,result=build(a.root);c=a.output_dir/'locked_pair_source.c';exe=a.output_dir/'locked_pair_source';c.write_text(source);subprocess.run(['cc','-std=c11','-O0','-fwrapv',str(c),'-o',str(exe)],check=True);output=subprocess.check_output([str(exe)],text=True);subprocess.run(['cc','-std=c11','-O2','-fwrapv',str(c),'-o',str(exe)],check=True);optimized=subprocess.check_output([str(exe)],text=True);assert output==optimized, 'O0/O2 source-C row mismatch';(a.output_dir/'locked_pair_source.rows').write_text(output) if a.full else None;cases={}
 for line in output.splitlines():
  key,kind,tick,*values=line.split(',');row=cases.setdefault(key,{}).setdefault(int(tick),{});values=list(map(int,values))
  if kind=='T':assert len(values)+1==len(STATE_COLUMNS);row['state']=[int(tick)]+values
  else:assert len(values)==len(SPRITE_COLUMNS);row['beforeTask' if kind=='P' else 'afterTask']=values
 result['cases']={k:list(rows.values()) for k,rows in cases.items()};result['coverage']=dict(caseCount=len(cases),rowCount=sum(map(len,result['cases'].values())))
 assert len(cases)==90
 for rows in result['cases'].values():assert rows[-1]['state'][1:4]==[0,0,0]
 apply_profile(result,a.full)
 out.write_text(json.dumps(result,separators=(',',':'))+'\n');print(result['coverage']);print(hashlib.sha256(out.read_bytes()).hexdigest())
if __name__=='__main__':main()

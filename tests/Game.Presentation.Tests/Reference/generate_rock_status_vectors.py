#!/usr/bin/env python3
"""Pinned original-C rock/status callbacks in the existing native sprite harness.
Tick -1 is synchronous creation; sprite/OAM phase precedes RunTasks. No product
runtime, managed compiler, or upstream script supplies expected values.
"""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile,sys
from pathlib import Path
sys.dont_write_bytecode=True
KINDS=['RaiseSprite','RockTomb','RedX','SpinningSparkle','GrowAndGrayscale']
CONFIGS=[
 [[20,32,-48,50,2,0],[0,32,-38,25,5,0],[32,32,-28,40,3,0],[-20,32,-48,50,2,0],[20,32,-28,60,1,0],[0,32,-28,30,4,0],[0,0,0,1,0,1],[32767,-32768,32767,2,0,1],[-32768,32767,-32768,3,5,0]],
 [[20,12,64,114,0,1],[-20,12,64,98,0,1],[3,6,64,82,0,1],[-3,13,64,66,0,1],[0,0,0,1,5,0],[0,-10,1,2,4,0],[32767,-32768,127,10,3,1],[-32768,32767,32767,1,1,0]],
 [[s,t,0,0,0,p] for s,t,p in [(0,0,1),(1,1,0),(0,9,1),(1,10,1),(0,11,0),(1,50,1)]],
 [[20,-20,0,0,0,0],[24,-16,0,0,0,0],[-10,12,0,0,0,1],[32767,-32768,0,0,0,0],[-32768,32767,0,0,0,1]],[[0,0,0,0,0,0]]]
SPRITE_COLUMNS=['slot','x','y','x2','y2','invisible','tile','hflip','vflip','subpriority','a','b','c','d','affineMode','objMode','animNumber','frameIndex','frameDelay','frameEnded']+['data'+str(i) for i in range(8)]
STATE_COLUMNS=['tick','visualCount','activeTask','liveSprites','taskTimer']
EXTRA_HASH={'src/battle_anim_rock.c':'13d82c078ffcbe648c81f1fbeeead1a2e74be1d680d30dfdb8df992034f93021','src/battle_anim_psychic.c':'3db4956ce939f8e3cdbe205c376e6da61de837ec07ceb96c6c6c1ce44eb1d6f2','src/battle_anim_status_effects.c':'e574e88a1a235973010647d1ec11bead112b4be2d0044b45de8be2a80b6328b3'}
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def build(root,reference):
 base=module('rock_status_base',root/'tests/Game.Presentation.Tests/Reference/generate_orbit_spray_vectors.py')
 with tempfile.TemporaryDirectory() as tmp:
  c=Path(tmp)/'base.c';base.build(root,'tasks',c);h=c.read_text().split('static const long long config0')[0]
 for name,digest in EXTRA_HASH.items():assert hashlib.sha256((reference/name).read_bytes()).hexdigest()==digest,name
 src={n:(reference/('src/'+n+'.c')).read_text() for n in ['sprite','battle_anim_mons','battle_anim_rock','battle_anim_psychic','battle_anim_status_effects','battle_anim_effects_2']}
 names={'sprite':['StartSpriteAnim'],'battle_anim_mons':['InitSpritePosToAnimAttacker','SetAnimSpriteInitialXOffset','SetSpriteCoordsToAnimAttackerCoords','InitAnimLinearTranslation','StartAnimLinearTranslation','AnimTranslateLinear','AnimTranslateLinear_WithFollowup','RunStoredCallbackWhenAnimEnds','SetGreyscaleOrOriginalPalette'],'battle_anim_rock':['AnimRaiseSprite','AnimRockTomb','AnimRockTomb_Step'],'battle_anim_psychic':['AnimRedX','AnimRedX_Step'],'battle_anim_effects_2':['AnimTask_GrowAndGrayscale','AnimTask_GrowAndGrayscale_Step'],'battle_anim_status_effects':[]}
 names['battle_anim_mons'].append('AnimSpinningSparkle')
 funcs=[base.function(src[file],n) for file,ns in names.items() for n in ns]
 decls=[base.declaration(src['battle_anim_rock'],'sAnim_Rock_'+name) for name in ['Biggest','Bigger','Big','Small','Smaller','Smallest']]+[base.declaration(src['battle_anim_status_effects'],'sAnim_SpinningSparkle')]
 for file,name in [('battle_anim_rock','sAnims_BasicRock'),('battle_anim_status_effects','sAnims_SpinningSparkle')]:decls.append(re.search(r'static const union AnimCmd \*const '+name+r'\[\]\s*=\s*\{.*?\};',src[file],re.S)[0])
 spans=[]
 for snippet in funcs+decls:
  file=next(n for n,s in src.items() if snippet in s);start=src[file].index(snippet);line=src[file][:start].count('\n')+1
  spans.append(dict(file='src/'+file+'.c',firstLine=line,lastLine=line+snippet.count('\n'),sha256=hashlib.sha256(snippet.encode()).hexdigest(),declaration=snippet.split('\n')[0]))
 h+='\n#define ST_OAM_OBJ_BLEND 1\n#define PLTT_ID(n) ((n)*16)\n#define PLTT_SIZE_4BPP 32\n#define CpuCopy32(src,dst,n) memcpy((dst),(src),(n))\n'
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'+'\n'.join(decls)+'\n'+'\n'.join(funcs)+'\n'
 h+=r'''
static const struct SpriteTemplate raiseRock={1,1,&oamOff,sAnims_BasicRock,NULL,dummyAffines,AnimRaiseSprite},tombRock={1,1,&oamOff,sAnims_BasicRock,NULL,dummyAffines,AnimRockTomb},redX={1,1,&oamOff,dummyFrames,NULL,dummyAffines,AnimRedX},sparkle={1,1,&oamOff,sAnims_SpinningSparkle,NULL,dummyAffines,AnimSpinningSparkle};
'''
 for k,configs in enumerate(CONFIGS):h+='static const int config'+str(k)+'[][6]={'+','.join('{'+','.join(map(str,a))+'}' for a in configs)+'};\n'
 h+='static const int (*configs[])[6]={config0,config1,config2,config3,config4};\nstatic const int counts[]={'+','.join(str(len(c)) for c in CONFIGS)+'};\n'+RUNNER
 for snippet in funcs+decls:assert snippet in h
 return h,dict(sourceRevision='037335f4c725d7c9aecdac87066f2002b4bd7e14',sourceSha256=base.SOURCE_SHA256|EXTRA_HASH,sourceSpans=spans,kinds=KINDS,configs=CONFIGS,spriteColumns=SPRITE_COLUMNS,stateColumns=STATE_COLUMNS,geometry=dict(yOffset=[5,11,7,13],elevation=[2,3,4,5],heights=[64,48,32,56]),conventions=dict(tickMinusOne='After source synchronous callback and initial AnimateSprite',sprites='After AnimateSprites before RunTasks',afterTaskSprites='Post-task battler state',battlers='Present and visible, source affine-normal idle with identity matrix',palette='All512 entries initialized to (index*73+31)&32767; task output records target palette after RunTasks',pointerHandles='Stored callback pointer uses host-safe handle; function phase unchanged',configs='Five signed16 args then source creation participant0/1'))
RUNNER=r'''
static int currentKind,currentConfig,currentTick;
static int live(void){int n=0;for(int i=slots;i<64;i++)n+=gSprites[i].inUse;return n;}
static void prefix(char type){printf("%d:%d:%d:%d:%d,%c,%d",currentKind,mode,gBattleAnimAttacker,gBattleAnimTarget,currentConfig,type,currentTick);}
static void emitSprites(char type){for(int i=currentKind==4?gBattleAnimTarget:slots;i<(currentKind==4?gBattleAnimTarget+1:64);i++){struct Sprite*s=&gSprites[i];if(!s->inUse)continue;struct OamMatrix m=(s->oam.affineMode&1)?gOamMatrices[s->oam.matrixNum]:(struct OamMatrix){256,0,0,256};int row[]={i,s->x,s->y,s->x2,s->y2,s->invisible,s->oam.tileNum,(s->oam.matrixNum>>3)&1,(s->oam.matrixNum>>4)&1,s->subpriority,m.a,m.b,m.c,m.d,s->oam.affineMode,s->oam.objMode,s->animNum,s->animCmdIndex,s->animDelayCounter,s->animEnded};if(s->oam.affineMode&1)row[7]=row[8]=0;prefix(type);for(unsigned j=0;j<sizeof(row)/sizeof(row[0]);j++)printf(",%d",row[j]);for(int j=0;j<8;j++)printf(",%d",s->data[j]);puts("");}}
static void emitState(void){prefix('T');printf(",%d,%d,%d,%d\n",gAnimVisualTaskCount,gTasks[0].isActive,live(),gTasks[0].data[0]);if(currentKind==4){int bank=gBattleAnimTarget+16;for(int i=0;i<512;i++)if(i/16!=bank)assert(gPlttBufferFaded[i]==((i*73+31)&32767));prefix('L');for(int i=0;i<16;i++)printf(",%d",gPlttBufferFaded[bank*16+i]);puts("");}}
static void runCase(int kind,int actor,int target,int config){
 currentKind=kind;currentConfig=config;currentTick=-1;slots=mode?4:2;gBattleAnimAttacker=actor;gBattleAnimTarget=target;
 memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));memset(gSprites,0,sizeof(gSprites));memset(gTasks,0,sizeof(gTasks));memset(sAffineAnimStates,0,sizeof(sAffineAnimStates));memset(&battleSpritesData,0,sizeof(battleSpritesData));gOamMatrixAllocBitmap=(1u<<slots)-1;gAnimVisualTaskCount=0;for(int i=0;i<32;i++)SetOamMatrix(i,256,0,0,256);for(int i=0;i<512;i++)gPlttBufferUnfaded[i]=gPlttBufferFaded[i]=(i*73+31)&32767;
 for(int i=0;i<slots;i++){struct Sprite*s=&gSprites[i];s->inUse=TRUE;s->usingSheet=TRUE;s->animBeginning=TRUE;s->callback=SpriteCallbackDummy;s->anims=dummyFrames;s->affineAnims=dummyAffines;s->oam.affineMode=1;s->oam.matrixNum=i;s->oam.paletteNum=i;s->affineAnimPaused=TRUE;s->data[0]=i;s->x=GetBattlerSpriteCoord(i,2);s->y=GetBattlerSpriteCoord(i,3);s->subpriority=GetBattlerSpriteSubpriority(i);battleSpritesData.healthBoxesData[i].matrixNum=i;AffineAnimStateReset(i);}
 const int*c=configs[kind][config];for(int i=0;i<5;i++)gBattleAnimArgs[i]=c[i];
 if(kind<4){int selected=c[5]?target:actor;CreateSpriteAndAnimate((const struct SpriteTemplate*[]){&raiseRock,&tombRock,&redX,&sparkle}[kind],GetBattlerSpriteCoord(selected,2),GetBattlerSpriteCoord(selected,3),GetBattlerSpriteSubpriority(selected)-2);gAnimVisualTaskCount++;}
 else{gTasks[0].isActive=TRUE;gTasks[0].func=AnimTask_GrowAndGrayscale;gAnimVisualTaskCount++;gTasks[0].func(0);}
 emitSprites('P');if(kind==4)emitSprites('Q');emitState();
 for(currentTick=0;currentTick<1000;currentTick++){AnimateSprites();emitSprites('P');if(gTasks[0].isActive)gTasks[0].func(0);if(kind==4)emitSprites('Q');emitState();if(!gAnimVisualTaskCount){assert(live()==0);return;}}
 fprintf(stderr,"timeout kind=%d mode=%d actor=%d target=%d config=%d\n",kind,mode,actor,target,config);abort();}
int main(void){for(int k=0;k<5;k++)for(mode=0;mode<2;mode++)for(int a=0;a<(mode?4:2);a++)for(int t=0;t<(mode?4:2);t++)for(int c=0;c<counts[k];c++)runCase(k,a,t,c);}
'''

# Whole cases selected from source branches; never sample frames.
REPRESENTATIVE_CASES = (
 # Every configuration: all rock frames, zero drop/lifetime, blink thresholds and signed endpoints
 '0:0:0:1:0','0:0:0:1:1','0:0:0:1:2','0:0:0:1:3','0:0:0:1:4','0:0:0:1:5','0:0:0:1:6','0:0:0:1:7','0:0:0:1:8',
 # Every configuration: all rock frames, zero drop/lifetime, blink thresholds and signed endpoints
 '1:0:0:1:0','1:0:0:1:1','1:0:0:1:2','1:0:0:1:3','1:0:0:1:4','1:0:0:1:5','1:0:0:1:6','1:0:0:1:7',
 # Every configuration: all rock frames, zero drop/lifetime, blink thresholds and signed endpoints
 '2:0:0:1:0','2:0:0:1:1','2:0:0:1:2','2:0:0:1:3','2:0:0:1:4','2:0:0:1:5',
 # Every configuration: all rock frames, zero drop/lifetime, blink thresholds and signed endpoints
 '3:0:0:1:0','3:0:0:1:1','3:0:0:1:2','3:0:0:1:3','3:0:0:1:4',
 # Every configuration: all rock frames, zero drop/lifetime, blink thresholds and signed endpoints
 '4:0:0:1:0',
 # Reverse side: mirrored coordinates and palette/affine restoration
 '0:0:1:0:0','0:0:1:0:7','0:0:1:0:8',
 # Reverse side: mirrored coordinates and palette/affine restoration
 '1:0:1:0:6','1:0:1:0:7',
 # Reverse side: mirrored coordinates and palette/affine restoration
 '2:0:1:0:1',
 # Reverse side: mirrored coordinates and palette/affine restoration
 '3:0:1:0:3','3:0:1:0:4',
 # Reverse side: mirrored coordinates and palette/affine restoration
 '4:0:1:0:0',
 # Doubles rear coordinates, picture heights and target palette
 '0:1:2:3:0',
 # Doubles rear coordinates, picture heights and target palette
 '1:1:2:3:0',
 # Doubles rear coordinates, picture heights and target palette
 '2:1:2:3:0',
 # Doubles rear coordinates, picture heights and target palette
 '3:1:2:3:0',
 # Doubles rear coordinates, picture heights and target palette
 '4:1:2:3:0',
 # Doubles rear coordinates, picture heights and target palette
 '0:1:3:2:0',
 # Doubles rear coordinates, picture heights and target palette
 '1:1:3:2:0',
 # Doubles rear coordinates, picture heights and target palette
 '2:1:3:2:0',
 # Doubles rear coordinates, picture heights and target palette
 '3:1:3:2:0',
 # Doubles rear coordinates, picture heights and target palette
 '4:1:3:2:0',
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
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--reference',type=Path);p.add_argument('--output-dir',type=Path,required=True);p.add_argument('--full',action='store_true',help='Emit all source-C cases for the same managed comparator');a=p.parse_args();a.output_dir.mkdir(parents=True,exist_ok=True)
 if (a.output_dir/'rock_status_vectors.json').exists():p.error('Refusing to overwrite an existing candidate')
 h,result=build(a.root,a.reference or a.root/'content/raw/pokefirered/037335f4');c=a.output_dir/'rock_status_source.c';exe=a.output_dir/'rock_status_source';c.write_text(h);subprocess.run(['cc','-std=c11','-O0','-fwrapv',str(c),'-o',str(exe)],check=True)
 output=subprocess.check_output([str(exe)],text=True);subprocess.run(['cc','-std=c11','-O2','-fwrapv',str(c),'-o',str(exe)],check=True);optimized=subprocess.check_output([str(exe)],text=True);assert output==optimized, 'O0/O2 source-C row mismatch';(a.output_dir/'rock_status_source.rows').write_text(output) if a.full else None;cases={}
 for line in output.splitlines():
  key,typ,tick,*values=line.split(',');tick=int(tick);v=list(map(int,values));row=cases.setdefault(key,{}).setdefault(tick,dict(sprites=[],afterTaskSprites=[]))
  if typ=='T':assert len(v)+1==len(STATE_COLUMNS);row['state']=[tick]+v
  elif typ=='L':assert len(v)==16;row['palette']=v
  else:assert len(v)==len(SPRITE_COLUMNS);row['sprites' if typ=='P' else 'afterTaskSprites'].append(v)
 result['cases']={k:list(v.values()) for k,v in cases.items()};assert len(cases)==20*sum(map(len,CONFIGS))
 for key,rows in result['cases'].items():assert rows[0]['state'][0]==-1 and rows[-1]['state'][1:4]==[0,0,0],key
 result['coverage']=dict(caseCount=len(cases),rowCount=sum(map(len,result['cases'].values())))
 apply_profile(result,a.full)
 out=a.output_dir/'rock_status_vectors.json';out.write_text(json.dumps(result,separators=(',',':'))+'\n');print(result['coverage'])
if __name__=='__main__':main()

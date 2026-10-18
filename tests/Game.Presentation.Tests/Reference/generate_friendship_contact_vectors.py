#!/usr/bin/env python3
"""Extract pinned C callbacks into the maintained source sprite/task harness.

Tick -1 is synchronous creation. Later ticks run AnimateSprites before RunTasks.
No managed/product runtime or upstream scripts generate expected values.
"""
import argparse, hashlib, importlib.util, json, subprocess, tempfile, sys
sys.dont_write_bytecode=True
from pathlib import Path
KINDS=['MovementWaves','Lick','WeakFrustration','StrongFrustration','TraceMon']
CONFIGS=[[[0,0,0,0,0,0],[0,0,1,0,0,0],[0,1,2,0,0,0],[1,0,3,0,0,0],[1,1,2,0,0,0]],
 [[0,0,0,0,0,0],[-24,-16,0,0,0,0],[24,12,0,0,0,0],[127,-127,0,0,0,0],[32767,-32768,0,0,0,0],[-32768,32767,0,0,0,0]],
 [[0,-8,0,0,0,0],[20,-20,0,0,0,0],[-20,20,0,0,0,0],[32767,-32768,0,0,0,0],[-32768,32767,0,0,0,0]],[[0,0,0,0,0,0]],
 [[0,0,0,1,0,0],[1,0,1,2,0,0],[0,2,4,3,0,0],[1,1,10,3,0,0],[0,0,20,3,1,0],[1,0,5,5,2,0],[0,0,5,0,0,0]]]
SPRITE_COLUMNS=['slot','x','y','x2','y2','invisible','tile','hflip','vflip','subpriority','a','b','c','d','affineMode','priority','objMode','animNumber','frameIndex','frameDelay','frameEnded']+['data'+str(i) for i in range(8)]
STATE_COLUMNS=['tick','visualCount','activeTask','liveSprites']+['taskData'+str(i) for i in range(16) if i not in [13,14]]
EXTRA_HASH={'src/battle_anim_ghost.c':'622823b0ad91a22d98b29964caf03a35409d8f667379d45477ad7f660c31955e','src/battle_anim_effects_3.c':'bdf8ba956af35ca232d7e5101090e2ff31a35f0ae91286a21b216e7787ba6c1f','src/battle_anim_utility_funcs.c':'8dbf0d05319980658ac1f698b1fed5a9dba02d1bda5aba487fec42d9ae16d297'}
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def build(root,reference):
 base=module('orbit_base',root/'tests/Game.Presentation.Tests/Reference/generate_orbit_spray_vectors.py')
 with tempfile.TemporaryDirectory() as tmp:
  c=Path(tmp)/'base.c';base.build(root,'tasks',c);h=c.read_text().split('static const long long config0')[0]
 for name,digest in EXTRA_HASH.items():
  assert hashlib.sha256((reference/name).read_bytes()).hexdigest()==digest,name
 src={name:(reference/('src/'+name+'.c')).read_text() for name in ['sprite','battle_anim_mons','battle_anim_ghost','battle_anim_effects_2','battle_anim_effects_3','battle_anim_utility_funcs']}
 names={'sprite':['StartSpriteAnim'],'battle_anim_mons':['InitSpritePosToAnimTarget','InitSpritePosToAnimAttacker','SetAnimSpriteInitialXOffset','AnimTask_GetFrustrationPowerLevel','CloneBattlerSpriteWithBlend','DestroySpriteWithActiveSheet'],
 'battle_anim_ghost':['AnimLick','AnimLick_Step'],'battle_anim_effects_2':['AnimMovementWaves','AnimMovementWaves_Step'],
 'battle_anim_effects_3':['AnimTask_StrongFrustrationGrowAndShrink','AnimWeakFrustrationAngerMark','AnimTask_GetReturnPowerLevel'],
 'battle_anim_utility_funcs':['AnimTask_TraceMonBlended','AnimTask_TraceMonBlended_Step','AnimMonTrace']}
 funcs=[base.function(src[file],n) for file,ns in names.items() for n in ns]
 decls=[base.declaration(src[file],n) for file,n in [('battle_anim_effects_2','sMovementWavesAnimCmds1'),('battle_anim_effects_2','sMovementWavesAnimCmds2'),('battle_anim_effects_3','sStrongFrustrationAffineAnimCmds'),('battle_anim_ghost','sAnim_Lick')]]
 spans=[]
 for snippet in funcs+decls:
  file=next(n for n,s in src.items() if snippet in s);start=src[file].index(snippet);line=src[file][:start].count('\n')+1
  spans.append(dict(file='src/'+file+'.c',firstLine=line,lastLine=line+snippet.count('\n'),sha256=hashlib.sha256(snippet.encode()).hexdigest(),declaration=snippet.split('\n')[0]))
 h=h.replace('u8 affineMode,matrixNum,shape,size,paletteNum,objMode;', 'u8 affineMode,matrixNum,shape,size,paletteNum,objMode,priority;')
 h+='\n#define ARG_RET_ID 7\n#define SPRITE_NONE 255\n#define ST_OAM_OBJ_BLEND 1\nstatic u8 gAnimFriendship;\n'
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'+'\n'.join(decls)+'\n'+'\n'.join(funcs)+'\n'
 h+=r'''
static const union AnimCmd *const waveFrames[]={sMovementWavesAnimCmds1,sMovementWavesAnimCmds2},*const lickFrames[]={sAnim_Lick};
static const struct SpriteTemplate waves={1,1,&oamOff,waveFrames,NULL,dummyAffines,AnimMovementWaves},lick={1,1,&oamOff,lickFrames,NULL,dummyAffines,AnimLick},weak={1,1,&oamOff,dummyFrames,NULL,dummyAffines,AnimWeakFrustrationAngerMark};
'''
 for k,configs in enumerate(CONFIGS):h+='static const int config'+str(k)+'[][6]={'+','.join('{'+','.join(map(str,a))+'}' for a in configs)+'};\n'
 h+='static const int (*configs[])[6]={config0,config1,config2,config3,config4};\nstatic const int counts[]={'+','.join(str(len(c)) for c in CONFIGS)+'};\n'
 h+=RUNNER
 for snippet in funcs+decls:assert snippet in h,'Source extraction changed'
 return h,dict(sourceRevision='037335f4c725d7c9aecdac87066f2002b4bd7e14',sourceSha256=base.SOURCE_SHA256|EXTRA_HASH,sourceSpans=spans,kinds=KINDS,configs=CONFIGS,spriteColumns=SPRITE_COLUMNS,stateColumns=STATE_COLUMNS,
 geometry=dict(x=[[72,176,48,112],[32,200,90,152]],y=[[80,40,40,80],[80,40,88,32]],yOffset=[5,11,7,13],elevation=[2,3,4,5],heights=[64,48,32,56],subpriority=[30,40,20,50]),
 conventions=dict(tickMinusOne='After source synchronous initializer and AnimateSprite',sprites='After AnimateSprites before tasks (source OAM phase)',afterTaskSprites='Physical registry after tasks, not the same-frame OAM image',traceCount='Raw clones are not gAnimVisualTaskCount owners; task data5 owns them',allocation='0 free, 1 full, 2 one free sprite; static occupied slots never counted',traceBattlerState='All probe battlers present, visible, nonaffine with identity matrices; no hidden/affine clone parity claim',taskPointer='data13/data14 host pointer handles omitted',traceBattlerAnchor='Source battler sprites are initialized at Center(false); callback coordinates elsewhere use Center(true)'))
RUNNER=r'''
static int currentKind,currentConfig,currentTick,firstEffect;
static int live(void){int n=0;for(int i=firstEffect;i<64;i++)n+=gSprites[i].inUse;return n;}
static void prefix(char type){printf("%d:%d:%d:%d:%d,%c,%d",currentKind,mode,gBattleAnimAttacker,gBattleAnimTarget,currentConfig,type,currentTick);}
static void emitSprites(char type){for(int i=currentKind==3?gBattleAnimAttacker:firstEffect;i<(currentKind==3?gBattleAnimAttacker+1:64);i++){struct Sprite*s=&gSprites[i];if(!s->inUse)continue;struct OamMatrix m=(s->oam.affineMode&1)?gOamMatrices[s->oam.matrixNum]:(struct OamMatrix){256,0,0,256};int row[]={i,s->x,s->y,s->x2,s->y2,s->invisible,s->oam.tileNum,(s->oam.matrixNum>>3)&1,(s->oam.matrixNum>>4)&1,s->subpriority,m.a,m.b,m.c,m.d,s->oam.affineMode,s->oam.priority,s->oam.objMode,s->animNum,s->animCmdIndex,s->animDelayCounter,s->animEnded};if(s->oam.affineMode&1)row[7]=row[8]=0;prefix(type);for(unsigned j=0;j<sizeof(row)/sizeof(row[0]);j++)printf(",%d",row[j]);for(int j=0;j<8;j++)printf(",%d",s->data[j]);puts("");}}
static void emitState(void){if(currentKind==4)assert(live()==gTasks[0].data[5]);prefix('T');printf(",%d,%d,%d",gAnimVisualTaskCount,gTasks[0].isActive,live());for(int i=0;i<16;i++)if(i!=13&&i!=14)printf(",%d",gTasks[0].data[i]);puts("");}
static void reserve(int i){struct Sprite*s=&gSprites[i];s->inUse=TRUE;s->usingSheet=TRUE;s->animBeginning=TRUE;s->callback=SpriteCallbackDummy;s->anims=dummyFrames;s->affineAnims=dummyAffines;}
static void runCase(int kind,int actor,int target,int config){
 currentKind=kind;currentConfig=config;currentTick=-1;slots=mode?4:2;firstEffect=slots;gBattleAnimAttacker=actor;gBattleAnimTarget=target;
 memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));memset(gSprites,0,sizeof(gSprites));memset(gTasks,0,sizeof(gTasks));memset(sAffineAnimStates,0,sizeof(sAffineAnimStates));memset(&battleSpritesData,0,sizeof(battleSpritesData));gOamMatrixAllocBitmap=(1u<<slots)-1;gAnimVisualTaskCount=0;for(int i=0;i<32;i++)SetOamMatrix(i,256,0,0,256);
 for(int i=0;i<slots;i++){reserve(i);struct Sprite*s=&gSprites[i];s->oam.affineMode=kind==4?0:1;s->oam.matrixNum=kind==4?0:i;s->affineAnimPaused=TRUE;s->data[0]=i;s->x=GetBattlerSpriteCoord(i,2);s->y=GetBattlerSpriteCoord(i,3)-(kind==4&&!(i&1)?8:0);s->subpriority=GetBattlerSpriteSubpriority(i);battleSpritesData.healthBoxesData[i].matrixNum=i;AffineAnimStateReset(i);}
 const int*c=configs[kind][config];for(int i=0;i<4;i++)gBattleAnimArgs[i]=c[i];
 if(kind==4){firstEffect=c[4]==1?64:c[4]==2?63:slots;for(int i=slots;i<firstEffect;i++)reserve(i);}
 if(kind<3){CreateSpriteAndAnimate((const struct SpriteTemplate*[]){&waves,&lick,&weak}[kind],GetBattlerSpriteCoord(target,2),GetBattlerSpriteCoord(target,3),GetBattlerSpriteSubpriority(target)-2);gAnimVisualTaskCount++;}
 else{gTasks[0].isActive=TRUE;gTasks[0].func=kind==3?AnimTask_StrongFrustrationGrowAndShrink:AnimTask_TraceMonBlended;gAnimVisualTaskCount++;gTasks[0].func(0);}
 emitSprites('P');if(kind>=3)emitSprites('Q');emitState();if(!gAnimVisualTaskCount)return;
 for(currentTick=0;currentTick<1000;currentTick++){AnimateSprites();emitSprites('P');if(gTasks[0].isActive)gTasks[0].func(0);if(kind>=3)emitSprites('Q');emitState();if(!gAnimVisualTaskCount){assert(live()==0);return;}}
 fprintf(stderr,"timeout kind=%d mode=%d actor=%d target=%d config=%d\n",kind,mode,actor,target,config);abort();}
int main(void){for(int value=0;value<256;value++){gAnimFriendship=value;gAnimVisualTaskCount=1;gTasks[0].isActive=TRUE;AnimTask_GetReturnPowerLevel(0);int a=gBattleAnimArgs[7];assert(gAnimVisualTaskCount==0&&!gTasks[0].isActive);gAnimVisualTaskCount=1;gTasks[0].isActive=TRUE;AnimTask_GetFrustrationPowerLevel(0);assert(gAnimVisualTaskCount==0&&!gTasks[0].isActive);printf("F,%d,%d,%d\n",value,a,gBattleAnimArgs[7]);}for(int k=0;k<5;k++)for(mode=0;mode<2;mode++)for(int a=0;a<(mode?4:2);a++)for(int t=0;t<(mode?4:2);t++)for(int c=0;c<counts[k];c++)runCase(k,a,t,c);}
'''

# Whole cases selected from source branches; never sample frames.
REPRESENTATIVE_CASES = (
 # Every callback configuration: frame variants, signed extremes, trace delay/count and allocator boundaries
 '0:0:0:1:0','0:0:0:1:1','0:0:0:1:2','0:0:0:1:3','0:0:0:1:4',
 # Every callback configuration: frame variants, signed extremes, trace delay/count and allocator boundaries
 '1:0:0:1:0','1:0:0:1:1','1:0:0:1:2','1:0:0:1:3','1:0:0:1:4','1:0:0:1:5',
 # Every callback configuration: frame variants, signed extremes, trace delay/count and allocator boundaries
 '2:0:0:1:0','2:0:0:1:1','2:0:0:1:2','2:0:0:1:3','2:0:0:1:4',
 # Every callback configuration: frame variants, signed extremes, trace delay/count and allocator boundaries
 '3:0:0:1:0',
 # Every callback configuration: frame variants, signed extremes, trace delay/count and allocator boundaries
 '4:0:0:1:0','4:0:0:1:1','4:0:0:1:2','4:0:0:1:3','4:0:0:1:4','4:0:0:1:5','4:0:0:1:6',
 # Opposite side: mirrored offsets, picture anchors and trace origin
 '0:0:1:0:1','0:0:1:0:2','0:0:1:0:3','0:0:1:0:4',
 # Opposite side: mirrored offsets, picture anchors and trace origin
 '1:0:1:0:1','1:0:1:0:4','1:0:1:0:5',
 # Opposite side: mirrored offsets, picture anchors and trace origin
 '2:0:1:0:0','2:0:1:0:1','2:0:1:0:2','2:0:1:0:3','2:0:1:0:4',
 # Opposite side: mirrored offsets, picture anchors and trace origin
 '3:0:1:0:0',
 # Opposite side: mirrored offsets, picture anchors and trace origin
 '4:0:1:0:1','4:0:1:0:5',
 # Doubles rear geometry and trace no-free/one-free allocation
 '0:1:2:3:4',
 # Doubles rear geometry and trace no-free/one-free allocation
 '1:1:2:3:4',
 # Doubles rear geometry and trace no-free/one-free allocation
 '2:1:2:3:4',
 # Doubles rear geometry and trace no-free/one-free allocation
 '3:1:2:3:0',
 # Doubles rear geometry and trace no-free/one-free allocation
 '4:1:2:3:4',
 # Doubles rear geometry and trace no-free/one-free allocation
 '4:1:2:3:5',
 # Doubles rear geometry and trace no-free/one-free allocation
 '0:1:3:2:4',
 # Doubles rear geometry and trace no-free/one-free allocation
 '1:1:3:2:4',
 # Doubles rear geometry and trace no-free/one-free allocation
 '2:1:3:2:4',
 # Doubles rear geometry and trace no-free/one-free allocation
 '3:1:3:2:0',
 # Doubles rear geometry and trace no-free/one-free allocation
 '4:1:3:2:4',
 # Doubles rear geometry and trace no-free/one-free allocation
 '4:1:3:2:5',
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
 if (a.output_dir/'friendship_contact_vectors.json').exists():p.error('Refusing to overwrite an existing vector candidate')
 h,result=build(a.root,a.reference or a.root/'content/raw/pokefirered/037335f4');c=a.output_dir/'friendship_contact_source.c';exe=a.output_dir/'friendship_contact_source';c.write_text(h)
 subprocess.run(['cc','-std=c11','-O0','-fwrapv',str(c),'-o',str(exe)],check=True);output=subprocess.check_output([str(exe)],text=True);subprocess.run(['cc','-std=c11','-O2','-fwrapv',str(c),'-o',str(exe)],check=True);optimized=subprocess.check_output([str(exe)],text=True);assert output==optimized, 'O0/O2 source-C row mismatch';(a.output_dir/'friendship_contact_source.rows').write_text(output) if a.full else None
 cases={};friendship=[]
 for line in output.splitlines():
  if line.startswith('F,'):friendship.append(list(map(int,line.split(',')[1:])));continue
  key,typ,tick,*values=line.split(',');tick=int(tick);v=list(map(int,values));row=cases.setdefault(key,{}).setdefault(tick,dict(sprites=[],afterTaskSprites=[]))
  if typ=='T':assert len(v)+1==len(STATE_COLUMNS);row['state']=[tick]+v
  else:assert len(v)==len(SPRITE_COLUMNS);row['sprites' if typ=='P' else 'afterTaskSprites'].append(v)
 result['cases']={k:list(v.values()) for k,v in cases.items()};result['friendship']=friendship
 assert len(friendship)==256;assert len(cases)==20*sum(map(len,CONFIGS))
 for key,rows in result['cases'].items():assert rows[0]['state'][0]==-1 and rows[-1]['state'][1:4]==[0,0,0],key
 result['coverage']=dict(caseCount=len(cases),rowCount=sum(map(len,result['cases'].values())),friendshipCount=256)
 apply_profile(result,a.full)
 out=a.output_dir/'friendship_contact_vectors.json';out.write_text(json.dumps(result,separators=(',',':'))+'\n');print(result['coverage'])
if __name__=='__main__':main()

#!/usr/bin/env python3
"""Pinned original-C Twister, Brick Break, in-place shake and Fury predicates.
Uses the existing native sprite/task harness; no upstream script or managed code.
"""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile,sys
from pathlib import Path
sys.dont_write_bytecode=True
KINDS=['TwisterLeaf','TwisterRock','BrickWall','BrickShard','InPlaceShake']
TWISTER=[[120,70,5,70,30,1,0],[115,55,6,60,25,1,0],[115,60,7,60,30,1,0],[115,55,10,60,30,1,0],[100,50,4,50,26,1,0],[105,25,8,60,20,1,0],[115,40,10,48,30,1,0],[120,30,6,45,25,1,0],[115,35,10,60,30,1,0],[105,20,8,40,0,1,0],[20,255,15,32,0,1,0],[110,10,8,32,20,1,0],[1,0,0,0,0,0,0],[7,1,-1,-32768,8,0,0],[9,-32768,32767,32767,9,1,1],[6,32767,-32768,-32768,-32768,1,1]]
CONFIGS=[TWISTER,TWISTER,[[1,0,0,90,10,0,0],[0,0,0,1,0,1,0],[1,32767,-32768,1,1,0,0],[0,-32768,32767,2,3,1,0],[1,3,-4,4,2,1,0]],[[p,i,x,y,0,q,0] for p,i,x,y,q in [(p,i,-8 if i%2==0 else 8,-12 if i<2 else 12,1-p) for p in [0,1] for i in range(4)]]+[[0,0,32767,-32768,0,1,0],[1,3,-32768,32767,0,0,0]],[[p,x,y,n,d,0,h] for p in range(4) for h in [0,1] for x,y,n,d in [(3,0,12,1),(-2,4,3,0),(32767,-32768,2,1),(-32768,32767,1,0)]]]
SPRITE_COLUMNS=['slot','x','y','x2','y2','invisible','tile','hflip','vflip','subpriority','priority','objMode','frameIndex','frameDelay','frameEnded']+['data'+str(i) for i in range(8)]
STATE_COLUMNS=['tick','visualCount','activeTask','liveSprites']+['taskData'+str(i) for i in range(7)]
EXTRA_HASH={'src/battle_anim_rock.c':'13d82c078ffcbe648c81f1fbeeead1a2e74be1d680d30dfdb8df992034f93021','src/battle_anim_mon_movement.c':'ce72119ff5d658784180bae2e171fae2affa45077e9a1d5e2d55a2caf1ce628c'}
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def build(root,reference):
 base=module('cut_wall_base',root/'tests/Game.Presentation.Tests/Reference/generate_orbit_spray_vectors.py')
 with tempfile.TemporaryDirectory() as tmp:
  c=Path(tmp)/'base.c';base.build(root,'tasks',c);h=c.read_text().split('static const long long config0')[0]
 for n,d in EXTRA_HASH.items():assert hashlib.sha256((reference/n).read_bytes()).hexdigest()==d,n
 src={n:(reference/('src/'+n+'.c')).read_text() for n in ['battle_anim','battle_anim_mons','battle_anim_effects_1','battle_anim_effects_2','battle_anim_fight','battle_anim_rock','battle_anim_mon_movement']}
 names={'battle_anim':['IsBattlerSpriteVisible'],'battle_anim_mons':['GetAnimBattlerSpriteId','SetAverageBattlerPositions'],'battle_anim_effects_1':['AnimMoveTwisterParticle','AnimMoveTwisterParticle_Step'],'battle_anim_effects_2':['AnimTask_IsFuryCutterHitRight','AnimTask_GetFuryCutterHitCount'],'battle_anim_fight':['AnimBrickBreakWall','AnimBrickBreakWall_Step','AnimBrickBreakWallShard','AnimBrickBreakWallShard_Step'],'battle_anim_mon_movement':['AnimTask_ShakeMonInPlace','AnimTask_ShakeMonInPlace_Step']}
 funcs=[base.function(src[file],n) for file,ns in names.items() for n in ns]
 decls=[base.declaration(src['battle_anim_rock'],'sAnim_Rock_'+name) for name in ['Biggest','Bigger','Big','Small','Smaller','Smallest']]+[base.declaration(src['battle_anim_effects_1'],'sRazorLeafParticleAnimCmds'+str(i)) for i in [1,2]]
 for file,name in [('battle_anim_rock','sAnims_BasicRock'),('battle_anim_effects_1','sRazorLeafParticleAnimTable')]:decls.append(re.search(r'static const union AnimCmd \*const '+name+r'\[\]\s*=\s*\{.*?\};',src[file],re.S)[0])
 templates=[]
 for file,name in [('battle_anim_effects_1','gTwisterLeafSpriteTemplate'),('battle_anim_rock','gTwisterRockSpriteTemplate'),('battle_anim_fight','gBrickBreakWallSpriteTemplate'),('battle_anim_fight','gBrickBreakWallShardSpriteTemplate')]:templates.append(re.search(r'const struct SpriteTemplate '+name+r'\s*=\s*\{.*?\};',src[file],re.S)[0])
 spans=[]
 for snippet in funcs+decls+templates:
  file=next(n for n,s in src.items() if snippet in s);start=src[file].index(snippet);line=src[file][:start].count('\n')+1;spans.append(dict(file='src/'+file+'.c',firstLine=line,lastLine=line+snippet.count('\n'),sha256=hashlib.sha256(snippet.encode()).hexdigest(),declaration=snippet.split('\n')[0]))
 h=h.replace('static bool8 IsBattlerSpriteVisible(u8 b){assert(b<slots);return TRUE;}','static bool8 IsBattlerSpriteVisible(u8 b);').replace('static u8 GetAnimBattlerSpriteId(u8 which){return which?gBattleAnimTarget:gBattleAnimAttacker;}','static u8 GetAnimBattlerSpriteId(u8 which);').replace('struct BattleSpritesData {struct {u8 matrixNum;}healthBoxesData[4];};','struct BattleSpritesData {struct {u8 matrixNum;}healthBoxesData[4];struct {bool8 invisible;}battlerData[4];};').replace('u8 affineMode,matrixNum,shape,size,paletteNum,objMode;','u8 affineMode,matrixNum,shape,size,paletteNum,objMode;u8 priority:2;')
 h+=r'''
#define SPRITE_NONE 64
#define ANIM_ATK_PARTNER 2
#define BATTLE_PARTNER(n) ((n)^2)
#define ARG_RET_ID 7
#define ANIM_TAG_ROCKS 3
#define ANIM_TAG_BLUE_LIGHT_WALL 4
#define ANIM_TAG_TORN_METAL 5
#define gDummySpriteAnimTable dummyFrames
#define gDummySpriteAffineAnimTable dummyAffines
#define sAffineAnims_BasicRock dummyAffines
static bool8 IsDoubleBattle(void){return mode;}
static u8 gBattlerSpriteIds[]={0,1,2,3};
static bool8 IsBattlerSpritePresent(u8 b){return b<slots;}
static u8 GetBattlerSpriteBGPriority(u8 b){return (u8[]){1,0,0,1}[b];}
static struct {u8 furyCutterCounter;} disableStruct,*gAnimDisableStructPtr=&disableStruct;
static const struct OamData gOamData_AffineOff_ObjNormal_16x16={.size=1,.priority=2},gOamData_AffineOff_ObjNormal_32x32={.size=2,.priority=2},gOamData_AffineOff_ObjBlend_64x64={.size=3,.objMode=1,.priority=2};
'''
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'+'\n'.join(decls)+'\n'+'\n'.join(templates)+'\n'+'\n'.join(funcs)+'\n'
 for k,configs in enumerate(CONFIGS):h+='static const int config'+str(k)+'[][7]={'+','.join('{'+','.join(map(str,a))+'}' for a in configs)+'};\n'
 h+='static const int (*configs[])[7]={config0,config1,config2,config3,config4};\nstatic const int counts[]={'+','.join(str(len(c)) for c in CONFIGS)+'};\n'+RUNNER
 for snippet in funcs+decls+templates:assert snippet in h
 return h,dict(sourceRevision='037335f4c725d7c9aecdac87066f2002b4bd7e14',sourceSha256=base.SOURCE_SHA256|EXTRA_HASH,sourceSpans=spans,kinds=KINDS,configs=CONFIGS,spriteColumns=SPRITE_COLUMNS,stateColumns=STATE_COLUMNS,geometry=dict(yOffset=[5,11,7,13],elevation=[2,3,4,5],heights=[64,48,32,56]),conventions=dict(tickMinusOne='After source synchronous callback and initial AnimateSprite',sprites='After AnimateSprites before RunTasks',afterTaskSprites='Post-task battler state for shake',battlers='Present; hidden toggle applies to selected shake participant or Twister target partner',configs='Five signed16 arguments, creation participant0/1, hidden toggle; shake starts from x2=3/y2=-5',pointerTail='Twister rock uses exact &sAnims_BasicRock[4], no array-layout substitution',capacity='Native CreateSpriteAndAnimate and original MAX_SPRITES64 remain unchanged'))
RUNNER=r'''
static int currentKind,currentConfig,currentTick;
static int live(void){int n=0;for(int i=slots;i<64;i++)n+=gSprites[i].inUse;return n;}
static void prefix(char type){printf("%d:%d:%d:%d:%d,%c,%d",currentKind,mode,gBattleAnimAttacker,gBattleAnimTarget,currentConfig,type,currentTick);}
static int selected(int selector){return selector==0?gBattleAnimAttacker:selector==1?gBattleAnimTarget:selector==2?(gBattleAnimAttacker^2):(gBattleAnimTarget^2);}
static void emitSprites(char type){for(int i=currentKind==4?0:slots;i<(currentKind==4?slots:64);i++){struct Sprite*s=&gSprites[i];if(!s->inUse)continue;int row[]={i,s->x,s->y,s->x2,s->y2,s->invisible,s->oam.tileNum,(s->oam.matrixNum>>3)&1,(s->oam.matrixNum>>4)&1,s->subpriority,s->oam.priority,s->oam.objMode,s->animCmdIndex,s->animDelayCounter,s->animEnded};prefix(type);for(unsigned j=0;j<sizeof(row)/sizeof(row[0]);j++)printf(",%d",row[j]);for(int j=0;j<8;j++)printf(",%d",s->data[j]);puts("");}}
static void emitState(void){prefix('T');printf(",%d,%d,%d",gAnimVisualTaskCount,gTasks[0].isActive,live());for(int i=0;i<7;i++)printf(",%d",gTasks[0].data[i]);puts("");}
static void runCase(int kind,int actor,int target,int config){
 currentKind=kind;currentConfig=config;currentTick=-1;slots=mode?4:2;gBattleAnimAttacker=actor;gBattleAnimTarget=target;const int*c=configs[kind][config];
 memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));memset(gSprites,0,sizeof(gSprites));memset(gTasks,0,sizeof(gTasks));memset(sAffineAnimStates,0,sizeof(sAffineAnimStates));memset(&battleSpritesData,0,sizeof(battleSpritesData));gAnimVisualTaskCount=0;gOamMatrixAllocBitmap=0;
 for(int i=0;i<slots;i++){struct Sprite*s=&gSprites[i];s->inUse=TRUE;s->usingSheet=TRUE;s->animBeginning=TRUE;s->callback=SpriteCallbackDummy;s->anims=dummyFrames;s->affineAnims=dummyAffines;s->x=GetBattlerSpriteCoord(i,2);s->y=GetBattlerSpriteCoord(i,3);s->subpriority=GetBattlerSpriteSubpriority(i);if(kind==4){s->x2=3;s->y2=-5;}}
 int hidden=kind==4?selected(c[0]):gBattleAnimTarget^2;if(c[6]&&hidden<slots){gSprites[hidden].invisible=TRUE;battleSpritesData.battlerData[hidden].invisible=TRUE;}
 for(int i=0;i<5;i++)gBattleAnimArgs[i]=c[i];
 if(kind<4){int sel=c[5]?target:actor;CreateSpriteAndAnimate((const struct SpriteTemplate*[]){&gTwisterLeafSpriteTemplate,&gTwisterRockSpriteTemplate,&gBrickBreakWallSpriteTemplate,&gBrickBreakWallShardSpriteTemplate}[kind],GetBattlerSpriteCoord(sel,2),GetBattlerSpriteCoord(sel,3),GetBattlerSpriteSubpriority(sel)-2);gAnimVisualTaskCount++;}
 else{gTasks[0].isActive=TRUE;gTasks[0].func=AnimTask_ShakeMonInPlace;gAnimVisualTaskCount++;gTasks[0].func(0);}
 emitSprites('P');if(kind==4)emitSprites('Q');emitState();if(!gAnimVisualTaskCount)return;
 for(currentTick=0;currentTick<1000;currentTick++){AnimateSprites();emitSprites('P');if(gTasks[0].isActive)gTasks[0].func(0);if(kind==4)emitSprites('Q');emitState();if(!gAnimVisualTaskCount){assert(live()==0);return;}}
 fprintf(stderr,"timeout kind=%d mode=%d actor=%d target=%d config=%d\n",kind,mode,actor,target,config);abort();}
int main(void){for(int c=0;c<256;c++){disableStruct.furyCutterCounter=c;gTasks[0].isActive=TRUE;gAnimVisualTaskCount=1;AnimTask_IsFuryCutterHitRight(0);int right=gBattleAnimArgs[7];gTasks[0].isActive=TRUE;gAnimVisualTaskCount=1;AnimTask_GetFuryCutterHitCount(0);printf("F,%d,%d,%d\n",c,right,gBattleAnimArgs[7]);assert(!gTasks[0].isActive&&!gAnimVisualTaskCount);}for(int k=0;k<5;k++)for(mode=0;mode<2;mode++)for(int a=0;a<(mode?4:2);a++)for(int t=0;t<(mode?4:2);t++)for(int c=0;c<counts[k];c++)runCase(k,a,t,c);}
'''

# Whole cases selected from source branches; never sample frames.
REPRESENTATIVE_CASES = (
 # Twister: nominal, zero/positive/0xff rise, acceleration threshold and signed wrap
 '0:0:0:1:0','0:0:0:1:9','0:0:0:1:10','0:0:0:1:12','0:0:0:1:13','0:0:0:1:14','0:0:0:1:15',
 # Twister: opposite source side
 '0:0:1:0:0',
 # Twister: partner-average geometry, hidden partner and priority sides
 '0:1:2:3:0','0:1:2:3:14','0:1:2:3:15',
 # Twister: partner-average geometry, hidden partner and priority sides
 '0:1:3:2:0','0:1:3:2:14','0:1:3:2:15',
 # Twister: nominal, zero/positive/0xff rise, acceleration threshold and signed wrap
 '1:0:0:1:0','1:0:0:1:9','1:0:0:1:10','1:0:0:1:12','1:0:0:1:13','1:0:0:1:14','1:0:0:1:15',
 # Twister: opposite source side
 '1:0:1:0:0',
 # Twister: partner-average geometry, hidden partner and priority sides
 '1:1:2:3:0','1:1:2:3:14','1:1:2:3:15',
 # Twister: partner-average geometry, hidden partner and priority sides
 '1:1:3:2:0','1:1:3:2:14','1:1:3:2:15',
 # Brick wall: hold/retirement, shake/no-shake and signed offsets
 '2:0:0:1:0','2:0:0:1:1','2:0:0:1:2','2:0:0:1:3','2:0:0:1:4',
 # Brick wall: reverse side
 '2:0:1:0:0',
 # Brick wall: doubles target offsets
 '2:1:2:3:2',
 # Brick shards: all four tile/direction branches, both origins and signed extremes
 '3:0:0:1:0','3:0:0:1:1','3:0:0:1:2','3:0:0:1:3','3:0:0:1:4','3:0:0:1:5','3:0:0:1:6','3:0:0:1:7','3:0:0:1:8','3:0:0:1:9',
 # Brick shards: reverse side
 '3:0:1:0:0',
 # Brick shards: doubles target origin
 '3:1:3:2:7',
 # Shake: both battlers, odd/even terminal corrections, delay zero/one and signed-width edges
 '4:0:0:1:0','4:0:0:1:1','4:0:0:1:2','4:0:0:1:3','4:0:0:1:4','4:0:0:1:8','4:0:0:1:9','4:0:0:1:10','4:0:0:1:11','4:0:0:1:12','4:0:0:1:16','4:0:0:1:20','4:0:0:1:24','4:0:0:1:28',
 # Shake: both partner selectors present/hidden, odd/even and signed-width edges
 '4:1:2:3:16','4:1:2:3:17','4:1:2:3:18','4:1:2:3:19','4:1:2:3:20','4:1:2:3:24','4:1:2:3:25','4:1:2:3:26','4:1:2:3:27','4:1:2:3:28',
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
 if (a.output_dir/'cut_wall_vectors.json').exists():p.error('Refusing to overwrite an existing candidate')
 h,result=build(a.root,a.reference or a.root/'content/raw/pokefirered/037335f4');c=a.output_dir/'cut_wall_source.c';exe=a.output_dir/'cut_wall_source';c.write_text(h);subprocess.run(['cc','-std=c11','-O0','-fwrapv',str(c),'-o',str(exe)],check=True)
 output=subprocess.check_output([str(exe)],text=True);subprocess.run(['cc','-std=c11','-O2','-fwrapv',str(c),'-o',str(exe)],check=True);optimized=subprocess.check_output([str(exe)],text=True);assert output==optimized, 'O0/O2 source-C row mismatch';(a.output_dir/'cut_wall_source.rows').write_text(output) if a.full else None;cases={};predicates=[]
 for line in output.splitlines():
  if line.startswith('F,'):predicates.append(list(map(int,line.split(',')[1:])));continue
  key,typ,tick,*values=line.split(',');tick=int(tick);v=list(map(int,values));row=cases.setdefault(key,{}).setdefault(tick,dict(sprites=[],afterTaskSprites=[]))
  if typ=='T':assert len(v)+1==len(STATE_COLUMNS);row['state']=[tick]+v
  else:assert len(v)==len(SPRITE_COLUMNS);row['sprites' if typ=='P' else 'afterTaskSprites'].append(v)
 result['cases']={k:list(v.values()) for k,v in cases.items()};result['furyPredicates']=predicates;assert len(cases)==20*sum(map(len,CONFIGS));assert len(predicates)==256
 for key,rows in result['cases'].items():assert rows[0]['state'][0]==-1 and rows[-1]['state'][1:4]==[0,0,0],key
 result['coverage']=dict(caseCount=len(cases),rowCount=sum(map(len,result['cases'].values())),predicates=len(predicates))
 apply_profile(result,a.full)
 out=a.output_dir/'cut_wall_vectors.json';out.write_text(json.dumps(result,separators=(',',':'))+'\n');print(result['coverage'])
if __name__=='__main__':main()

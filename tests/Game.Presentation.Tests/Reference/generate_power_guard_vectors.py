#!/usr/bin/env python3
"""Pinned original-C power/guard callbacks with native sprite, affine and RNG players.
No upstream script or managed implementation supplies the expected observations.
"""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile,sys
from pathlib import Path
sys.dont_write_bytecode=True
KINDS=['ReversalOrb','Protect','ShadowBall','RandomHitSplat','FlailMovement']
# Three signed16 callback arguments, source creation participant, presentation seed.
CONFIGS=[[[26,p,0,0,0] for p in [0,42,84,126,168,210]]+[[d,p,0,q,0] for d,p,q in [(1,255,1),(2,63,1),(31,195,0),(255,64,0)]],
 [[24,0,d,0,0] for d in [2,3,19,20,21,90,340]]+[[32767,-32768,3,1,0],[-32768,32767,3,0,0]],
 [[a,b,c,p,0] for a,b,c,p in [(16,16,8,1),(1,1,1,1),(2,3,4,0),(17,1,3,1),(31,19,11,0)]],
 [[p,s,0,q,seed] for p,s,q in [(0,0,1),(0,1,0),(1,2,0),(1,3,1),(0,-1,1),(1,-1,0)] for seed in [0,1,0x12345678,0xffffffff]],
 [[p,h,0,0,0] for p in [0,1] for h in [0,1]]]
SPRITE_COLUMNS=['slot','x','y','x2','y2','invisible','tile','hflip','vflip','subpriority','a','b','c','d','affineMode','objMode','priority','frameIndex','frameDelay','frameEnded','affineEnded','affineIndex','affineDelay','scaleX','scaleY','rotation']+['data'+str(i) for i in range(8)]
STATE_COLUMNS=['tick','visualCount','activeTask','liveSprites','rngState','rngDraws','blendControl','blendAlpha']+['taskData'+str(i) for i in range(15)]+['arg'+str(i) for i in range(3)]
EXTRA_HASH={'src/battle_anim_effects_3.c':'bdf8ba956af35ca232d7e5101090e2ff31a35f0ae91286a21b216e7787ba6c1f', 'src/battle_anim_normal.c':'73478ff40f601740b58408caa3259f5abc29f90010f77017aecc69d71803a6c3', 'src/battle_anim_ghost.c':'622823b0ad91a22d98b29964caf03a35409d8f667379d45477ad7f660c31955e'}
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def build(root,reference):
 base=module('power_guard_base',root/'tests/Game.Presentation.Tests/Reference/generate_orbit_spray_vectors.py')
 with tempfile.TemporaryDirectory() as tmp:
  c=Path(tmp)/'base.c';base.build(root,'tasks',c);h=c.read_text().split('static const long long config0')[0]
 for n,d in EXTRA_HASH.items():assert hashlib.sha256((reference/n).read_bytes()).hexdigest()==d,n
 src={n:(reference/('src/'+n+'.c')).read_text() for n in ['sprite','battle_anim','battle_anim_mons','battle_anim_effects_1','battle_anim_effects_3','battle_anim_ghost','battle_anim_normal']}
 names={'battle_anim':['IsBattlerSpriteVisible'],'battle_anim_mons':['GetAnimBattlerSpriteId','InitSpritePosToAnimAttacker','InitSpritePosToAnimTarget','SetAnimSpriteInitialXOffset','SetSpriteCoordsToAnimAttackerCoords','DestroySpriteAndMatrix','RunStoredCallbackWhenAffineAnimEnds','DestroyAnimSpriteAndDisableBlend','SetBattlerSpriteYOffsetFromRotation'],
 'battle_anim_effects_3':['AnimReversalOrb','AnimReversalOrb_Step','AnimTask_FlailMovement','AnimTask_FlailMovement_Step'],
 'battle_anim_effects_1':['AnimProtect','AnimProtect_Step'],'battle_anim_ghost':['AnimShadowBall','AnimShadowBall_Step'],'battle_anim_normal':['AnimHitSplatRandom']}
 funcs=[base.function(src[file],n) for file,ns in names.items() for n in ns]
 decls=[base.declaration(src['battle_anim_normal'],'sAffineAnim_HitSplat_'+str(i)) for i in range(4)]+[base.declaration(src['battle_anim_ghost'],'sAffineAnim_ShadowBall')]
 for file,name in [('battle_anim_normal','sAffineAnims_HitSplat'),('battle_anim_ghost','sAffineAnims_ShadowBall')]:decls.append(re.search(r'static const union AffineAnimCmd \*const '+name+r'\[\]\s*=\s*\{.*?\};',src[file],re.S)[0])
 templates=[]
 for file,name in [('battle_anim_effects_3','gReversalOrbSpriteTemplate'),('battle_anim_effects_1','gProtectSpriteTemplate'),('battle_anim_ghost','gShadowBallSpriteTemplate'),('battle_anim_normal','gRandomPosHitSplatSpriteTemplate')]:templates.append(re.search(r'const struct SpriteTemplate '+name+r'\s*=\s*\{.*?\};',src[file],re.S)[0])
 spans=[]
 for snippet in funcs+decls+templates:
  file=next(n for n,s in src.items() if snippet in s);start=src[file].index(snippet);line=src[file][:start].count('\n')+1;spans.append(dict(file='src/'+file+'.c',firstLine=line,lastLine=line+snippet.count('\n'),sha256=hashlib.sha256(snippet.encode()).hexdigest(),declaration=snippet.split('\n')[0]))
 h=h.replace('static bool8 IsBattlerSpriteVisible(u8 b){assert(b<slots);return TRUE;}','static bool8 IsBattlerSpriteVisible(u8 b);')
 h=h.replace('static u8 GetAnimBattlerSpriteId(u8 which){return which?gBattleAnimTarget:gBattleAnimAttacker;}','static u8 GetAnimBattlerSpriteId(u8 which);')
 h=h.replace('struct BattleSpritesData {struct {u8 matrixNum;}healthBoxesData[4];};','struct BattleSpritesData {struct {u8 matrixNum;}healthBoxesData[4];struct {bool8 invisible;}battlerData[4];};')
 h=h.replace('u8 affineMode,matrixNum,shape,size,paletteNum,objMode;','u8 affineMode,matrixNum,shape,size,paletteNum,objMode;u8 priority:2;')
 h=h.replace('static u8 IndexOfSpritePaletteTag(u16 tag){','static u8 IndexOfSpritePaletteTag(u16 tag){if(tag==3)return 4;')
 h+=r'''
#define SPRITE_NONE 64
#define ANIM_ATK_PARTNER 2
#define BATTLE_PARTNER(n) ((n)^2)
static u8 gBattlerSpriteIds[]={0,1,2,3};
static bool8 IsBattlerSpritePresent(u8 b){return b<slots;}
#define ANIM_TAG_PROTECT 3
#define ANIM_TAG_BLUE_ORB 4
#define ANIM_TAG_SHADOW_BALL 5
#define ANIM_TAG_IMPACT 6
#define gDummySpriteAnimTable dummyFrames
#define gDummySpriteAffineAnimTable dummyAffines
#define REG_OFFSET_BLDCNT 0
#define REG_OFFSET_BLDALPHA 1
#define BLDCNT_TGT2_ALL 0x3f00
#define BLDCNT_EFFECT_BLEND 0x40
#define BLDALPHA_BLEND(a,b) ((a)|((b)<<8))
static u16 blendControl,blendAlpha;
static void SetGpuReg(u16 id,u16 value){if(id==0)blendControl=value;else if(id==1)blendAlpha=value;else abort();}
static u8 GetBattlerSpriteBGPriority(u8 b){return (u8[]){1,0,0,1}[b];}
static const struct OamData gOamData_AffineOff_ObjNormal_8x8={.priority=2},gOamData_AffineOff_ObjBlend_64x64={.size=3,.objMode=1,.priority=2},gOamData_AffineNormal_ObjNormal_32x32={.size=2,.affineMode=1,.priority=2},gOamData_AffineNormal_ObjBlend_32x32={.size=2,.affineMode=1,.objMode=1,.priority=2};
'''
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'+'\n'.join(decls)+'\n'+'\n'.join(templates)+'\n'+'\n'.join(funcs)+'\n'
 for k,configs in enumerate(CONFIGS):h+='static const long long config'+str(k)+'[][5]={'+','.join('{'+','.join(map(str,a))+'}' for a in configs)+'};\n'
 h+='static const long long (*configs[])[5]={config0,config1,config2,config3,config4};\nstatic const int counts[]={'+','.join(str(len(c)) for c in CONFIGS)+'};\n'+RUNNER
 for snippet in funcs+decls+templates:assert snippet in h
 return h,dict(sourceRevision='037335f4c725d7c9aecdac87066f2002b4bd7e14',sourceSha256=base.SOURCE_SHA256|EXTRA_HASH,sourceSpans=spans,kinds=KINDS,configs=CONFIGS,spriteColumns=SPRITE_COLUMNS,stateColumns=STATE_COLUMNS,geometry=dict(yOffset=[5,11,7,13],elevation=[2,3,4,5],heights=[64,48,32,56]),conventions=dict(tickMinusOne='After source synchronous callback and initial AnimateSprite',sprites='After AnimateSprites before RunTasks',afterTaskSprites='Post-task battler state',battlers='Present with source affine-normal idle identity matrix; Flail arg1 is a harness-only hidden-present toggle',palette='All512 entries initialized to (index*73+31)&32767; Protect owns OBJ palette4 only',pointerHandles='Stored callback uses host-safe handle; callback phases unchanged',configs='Three signed16 args, source creation participant0/1, explicit presentation RNG seed',priority='Initial source battler BG priorities [1,0,0,1], matching current shared renderer'))
RUNNER=r'''
static int currentKind,currentConfig,currentTick;
static int live(void){int n=0;for(int i=slots;i<64;i++)n+=gSprites[i].inUse;return n;}
static void prefix(char type){printf("%d:%d:%d:%d:%d,%c,%d",currentKind,mode,gBattleAnimAttacker,gBattleAnimTarget,currentConfig,type,currentTick);}
static int selected(void){return configs[4][currentConfig][0]?gBattleAnimTarget:gBattleAnimAttacker;}
static void emitSprites(char type){for(int i=currentKind==4?selected():slots;i<(currentKind==4?selected()+1:64);i++){struct Sprite*s=&gSprites[i];if(!s->inUse)continue;struct OamMatrix m=(s->oam.affineMode&1)?gOamMatrices[s->oam.matrixNum]:(struct OamMatrix){256,0,0,256};struct AffineAnimState*a=&sAffineAnimStates[s->oam.matrixNum];int row[]={i,s->x,s->y,s->x2,s->y2,s->invisible,s->oam.tileNum,(s->oam.matrixNum>>3)&1,(s->oam.matrixNum>>4)&1,s->subpriority,m.a,m.b,m.c,m.d,s->oam.affineMode,s->oam.objMode,s->oam.priority,s->animCmdIndex,s->animDelayCounter,s->animEnded,s->affineAnimEnded,a->animCmdIndex,a->delayCounter,a->xScale,a->yScale,a->rotation};if(s->oam.affineMode&1)row[7]=row[8]=0;prefix(type);for(unsigned j=0;j<sizeof(row)/sizeof(row[0]);j++)printf(",%d",row[j]);for(int j=0;j<8;j++)printf(",%d",s->data[j]);puts("");}}
static void emitState(void){prefix('T');printf(",%d,%d,%d,%u,%d,%d,%d",gAnimVisualTaskCount,gTasks[0].isActive,live(),gRngValue,rngDraws,blendControl,blendAlpha);for(int i=0;i<15;i++)printf(",%d",gTasks[0].data[i]);for(int i=0;i<3;i++)printf(",%d",gBattleAnimArgs[i]);puts("");if(currentKind==1){for(int i=0;i<512;i++)if(i/16!=20)assert(gPlttBufferFaded[i]==((i*73+31)&32767));prefix('L');for(int i=0;i<16;i++)printf(",%d",gPlttBufferFaded[320+i]);puts("");}}
static void runCase(int kind,int actor,int target,int config){
 currentKind=kind;currentConfig=config;currentTick=-1;slots=mode?4:2;gBattleAnimAttacker=actor;gBattleAnimTarget=target;const long long*c=configs[kind][config];
 memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));memset(gSprites,0,sizeof(gSprites));memset(gTasks,0,sizeof(gTasks));memset(sAffineAnimStates,0,sizeof(sAffineAnimStates));memset(&battleSpritesData,0,sizeof(battleSpritesData));gOamMatrixAllocBitmap=(1u<<slots)-1;gAnimVisualTaskCount=0;gRngValue=c[4];rngDraws=0;blendControl=blendAlpha=0;for(int i=0;i<32;i++)SetOamMatrix(i,256,0,0,256);for(int i=0;i<512;i++)gPlttBufferUnfaded[i]=gPlttBufferFaded[i]=(i*73+31)&32767;
 for(int i=0;i<slots;i++){struct Sprite*s=&gSprites[i];s->inUse=TRUE;s->usingSheet=TRUE;s->animBeginning=TRUE;s->callback=SpriteCallbackDummy;s->anims=dummyFrames;s->affineAnims=dummyAffines;s->oam.affineMode=1;s->oam.matrixNum=i;s->oam.paletteNum=i;s->affineAnimPaused=TRUE;s->data[0]=i;s->x=GetBattlerSpriteCoord(i,2);s->y=GetBattlerSpriteCoord(i,3);s->subpriority=GetBattlerSpriteSubpriority(i);battleSpritesData.healthBoxesData[i].matrixNum=i;AffineAnimStateReset(i);}
 if(kind==4&&c[1]){int b=c[0]?target:actor;gSprites[b].invisible=TRUE;battleSpritesData.battlerData[b].invisible=TRUE;}
 for(int i=0;i<3;i++)gBattleAnimArgs[i]=c[i];
 if(kind<4){int sel=c[3]?target:actor;CreateSpriteAndAnimate((const struct SpriteTemplate*[]){&gReversalOrbSpriteTemplate,&gProtectSpriteTemplate,&gShadowBallSpriteTemplate,&gRandomPosHitSplatSpriteTemplate}[kind],GetBattlerSpriteCoord(sel,2),GetBattlerSpriteCoord(sel,3),GetBattlerSpriteSubpriority(sel)-2);gAnimVisualTaskCount++;}
 else{gTasks[0].isActive=TRUE;gTasks[0].func=AnimTask_FlailMovement;gAnimVisualTaskCount++;gTasks[0].func(0);}
 emitSprites('P');if(kind==4)emitSprites('Q');emitState();
 for(currentTick=0;currentTick<1000;currentTick++){AnimateSprites();emitSprites('P');if(gTasks[0].isActive)gTasks[0].func(0);if(kind==4)emitSprites('Q');emitState();if(!gAnimVisualTaskCount){assert(live()==0);return;}}
 fprintf(stderr,"timeout kind=%d mode=%d actor=%d target=%d config=%d\n",kind,mode,actor,target,config);abort();}
int main(void){for(int k=0;k<5;k++)for(mode=0;mode<2;mode++)for(int a=0;a<(mode?4:2);a++)for(int t=0;t<(mode?4:2);t++)for(int c=0;c<counts[k];c++)runCase(k,a,t,c);}
'''

# Whole cases selected from source branches; never sample frames.
REPRESENTATIVE_CASES = (
 # All source control boundaries: orbit, Protect fade thresholds, three-phase travel, affine choices/RNG and hidden Flail
 '0:0:0:1:0','0:0:0:1:1','0:0:0:1:2','0:0:0:1:3','0:0:0:1:4','0:0:0:1:5','0:0:0:1:6','0:0:0:1:7','0:0:0:1:8','0:0:0:1:9',
 # All source control boundaries: orbit, Protect fade thresholds, three-phase travel, affine choices/RNG and hidden Flail
 '1:0:0:1:0','1:0:0:1:1','1:0:0:1:2','1:0:0:1:3','1:0:0:1:4','1:0:0:1:5','1:0:0:1:6','1:0:0:1:7','1:0:0:1:8',
 # All source control boundaries: orbit, Protect fade thresholds, three-phase travel, affine choices/RNG and hidden Flail
 '2:0:0:1:0','2:0:0:1:1','2:0:0:1:2','2:0:0:1:3','2:0:0:1:4',
 # All source control boundaries: orbit, Protect fade thresholds, three-phase travel, affine choices/RNG and hidden Flail
 '3:0:0:1:0','3:0:0:1:4','3:0:0:1:8','3:0:0:1:12','3:0:0:1:16','3:0:0:1:17','3:0:0:1:18','3:0:0:1:19','3:0:0:1:20','3:0:0:1:21','3:0:0:1:22','3:0:0:1:23',
 # All source control boundaries: orbit, Protect fade thresholds, three-phase travel, affine choices/RNG and hidden Flail
 '4:0:0:1:0','4:0:0:1:1','4:0:0:1:2','4:0:0:1:3',
 # Reverse side: source mirroring, BG priority and rotation foot offset
 '0:0:1:0:0','0:0:1:0:6','0:0:1:0:8',
 # Reverse side: source mirroring, BG priority and rotation foot offset
 '1:0:1:0:0','1:0:1:0:3','1:0:1:0:7','1:0:1:0:8',
 # Reverse side: source mirroring, BG priority and rotation foot offset
 '2:0:1:0:0','2:0:1:0:1',
 # Reverse side: source mirroring, BG priority and rotation foot offset
 '3:0:1:0:0','3:0:1:0:20',
 # Reverse side: source mirroring, BG priority and rotation foot offset
 '4:0:1:0:0','4:0:1:0:1','4:0:1:0:2','4:0:1:0:3',
 # Doubles rear geometry and source priorities
 '0:1:2:3:0',
 # Doubles rear geometry and source priorities
 '1:1:2:3:0',
 # Doubles rear geometry and source priorities
 '2:1:2:3:0',
 # Doubles rear geometry and source priorities
 '3:1:2:3:16',
 # Doubles rear geometry and source priorities
 '4:1:2:3:0',
 # Doubles rear geometry and source priorities
 '0:1:3:2:0',
 # Doubles rear geometry and source priorities
 '1:1:3:2:0',
 # Doubles rear geometry and source priorities
 '2:1:3:2:0',
 # Doubles rear geometry and source priorities
 '3:1:3:2:16',
 # Doubles rear geometry and source priorities
 '4:1:3:2:0',
 # Shadow Ball: equal-position and same-side interpolation endpoints
 '2:0:0:0:0',
 # Shadow Ball: equal-position and same-side interpolation endpoints
 '2:1:0:2:0',
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
 if (a.output_dir/'power_guard_vectors.json').exists():p.error('Refusing to overwrite an existing candidate')
 h,result=build(a.root,a.reference or a.root/'content/raw/pokefirered/037335f4');c=a.output_dir/'power_guard_source.c';exe=a.output_dir/'power_guard_source';c.write_text(h);subprocess.run(['cc','-std=c11','-O0','-fwrapv',str(c),'-o',str(exe)],check=True)
 output=subprocess.check_output([str(exe)],text=True);subprocess.run(['cc','-std=c11','-O2','-fwrapv',str(c),'-o',str(exe)],check=True);optimized=subprocess.check_output([str(exe)],text=True);assert output==optimized, 'O0/O2 source-C row mismatch';(a.output_dir/'power_guard_source.rows').write_text(output) if a.full else None;cases={}
 for line in output.splitlines():
  key,typ,tick,*values=line.split(',');tick=int(tick);v=list(map(int,values));row=cases.setdefault(key,{}).setdefault(tick,dict(sprites=[],afterTaskSprites=[]))
  if typ=='T':assert len(v)+1==len(STATE_COLUMNS);row['state']=[tick]+v
  elif typ=='L':assert len(v)==16;row['palette']=v
  else:assert len(v)==len(SPRITE_COLUMNS);row['sprites' if typ=='P' else 'afterTaskSprites'].append(v)
 result['cases']={k:list(v.values()) for k,v in cases.items()};assert len(cases)==20*sum(map(len,CONFIGS))
 for key,rows in result['cases'].items():assert rows[0]['state'][0]==-1 and rows[-1]['state'][1:4]==[0,0,0],key
 result['coverage']=dict(caseCount=len(cases),rowCount=sum(map(len,result['cases'].values())))
 apply_profile(result,a.full)
 out=a.output_dir/'power_guard_vectors.json';out.write_text(json.dumps(result,separators=(',',':'))+'\n');print(result['coverage'])
if __name__=='__main__':main()

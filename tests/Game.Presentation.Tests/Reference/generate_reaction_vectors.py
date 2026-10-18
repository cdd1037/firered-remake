#!/usr/bin/env python3
"""Extract pinned reaction callbacks into an authored C harness; never run upstream scripts.

Reuses the existing maintained source-player extraction, then replaces only its
host geometry, scenario runner, and observation adapters. Source callback bodies
are copied verbatim and audited as exact line spans with SHA-256 hashes.
"""
import argparse, hashlib, importlib.util, json, re, subprocess, sys, tempfile
sys.dont_write_bytecode = True
from pathlib import Path
ROOT = Path(__file__).resolve().parents[3]
KINDS = ['TriAttackTriangle','FacadeSquish','FacadeColorBlend','FacadeCombined','SmellingSaltsHand','SmellingSaltExclamation','SmellingSaltsSquish','LeafBlade']
CONFIG_COLUMNS = ['arg0','arg1','arg2','allocationMode','dimensionProfile']
CONFIGS = [
 [[16,0,0,0,0],[0,0,0,0,1],[-16,12,0,1,0]],
 [[0,1,0,0,0],[0,3,0,0,0],[1,3,0,1,1],[0,1,0,2,0],[0,0,0,0,0]],
 [[0,0,0,0,0],[0,1,0,0,0],[0,24,0,0,0],[1,72,0,0,1]],
 [[0,3,72,0,0],[1,3,72,1,1],[0,3,72,2,0]],
 [[b,f,n,0,d] for b,f,n,d in [(0,0,1,0),(0,1,2,1),(1,0,2,0),(1,1,2,1)]],
 [[b,delay,n,0,d] for b,delay,n,d in [(0,8,3,0),(1,8,3,0),(1,0,2,1),(1,1,1,1)]],
 [[0,2,0,0,0],[1,1,0,0,0],[1,2,0,0,1]],
 [[0,0,0,alloc,dim] for alloc,dim in [(0,0),(0,1),(1,0),(2,0),(3,0)]],
]
SPRITE_COLUMNS=['slot','role','x','y','x2','y2','invisible','tile','hflip','vflip','subpriority','a','b','c','d','affineMode','matrixSlot','affinePaused','affineEnded','affineIndex','affineDelay','xScale','yScale','rotation','callback','animNumber','frameIndex','frameDelay','frameEnded','frameBeginning']+['data'+str(i) for i in range(8)]
TASK_COLUMNS=['slot','active','callback']+['data'+str(i) for i in range(16)]
STATE_COLUMNS=['tick','visualCount','activeTasks','rawChildren','ownedSprites','matrices','createCalls','createSuccesses','destroyCalls','maxRawChildren','maxOwnedSprites','blendWrites','blendOffset','blendCoefficient','blendColor']
PALETTE_COLUMNS=['paletteSlot']+['faded'+str(i) for i in range(16)]+['unfaded'+str(i) for i in range(16)]
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def build(root):
 base=module('orbit_source_player',root/'tests/Game.Presentation.Tests/Reference/generate_orbit_spray_vectors.py')
 raw=root/'content/raw/pokefirered/037335f4'
 sources={p.relative_to(raw).as_posix():p.read_text() for p in (raw/'src').glob('*.c')}
 src1=sources['src/battle_anim_effects_1.c'];src3=sources['src/battle_anim_effects_3.c'];mons=sources['src/battle_anim_mons.c'];sprite=sources['src/sprite.c']
 # The fixed effects_3 hash is populated explicitly below, not inferred at run time.
 if hashlib.sha256((raw/'src/battle_anim_effects_3.c').read_bytes()).hexdigest()!=EFFECTS3_HASH:raise ValueError('Pinned effects_3 source mismatch')
 with tempfile.TemporaryDirectory() as tmp:
  target=Path(tmp)/'base.c';base.build(root,'tasks',target);h=target.read_text().split('static const long long config0')[0]
 h=h.replace('gTasks[1]','gTasks[2]').replace('assert(n==0);gTasks[n].isActive','assert(n<2);gTasks[n].isActive')
 h=h.replace('static const s16 X[2][4]', 'static int dimensionProfile;static const s16 profileYOffset[2][4]={{5,11,7,13},{5,11,7,0}};\nstatic const s16 X[2][4]')
 h=h.replace('(int[]){5,11,7,13}[b]','profileYOffset[dimensionProfile][b]').replace('(u16[]){5,11,7,13}[n]','profileYOffset[dimensionProfile][n]')
 h=h.replace('#define BATTLER_COORD_ATTR_HEIGHT 0','#define BATTLER_COORD_ATTR_HEIGHT 0\n#define BATTLER_COORD_ATTR_WIDTH 1\n#define BATTLER_COORD_ATTR_TOP 2\n#define BATTLER_COORD_ATTR_BOTTOM 3\n#define BATTLER_COORD_ATTR_LEFT 4\n#define BATTLER_COORD_ATTR_RIGHT 5')
 old='static s16 GetBattlerSpriteCoordAttr(u8 b,u8 attr){assert(attr==0);return (s16[]){64,48,32,56}[b];}'
 new='''static const s16 widths[2][4]={{64,48,32,56},{8,16,40,64}},heights[2][4]={{64,48,32,56},{8,64,16,64}};
static s16 GetBattlerSpriteCoordAttr(u8 b,u8 attr){int w=widths[dimensionProfile][b],h=heights[dimensionProfile][b];switch(attr){case BATTLER_COORD_ATTR_HEIGHT:return h;case BATTLER_COORD_ATTR_WIDTH:return w;case BATTLER_COORD_ATTR_LEFT:return GetBattlerSpriteCoord(b,2)-w/2;case BATTLER_COORD_ATTR_RIGHT:return GetBattlerSpriteCoord(b,2)+w/2;case BATTLER_COORD_ATTR_TOP:return GetBattlerSpriteCoord(b,3)-h/2;case BATTLER_COORD_ATTR_BOTTOM:return GetBattlerSpriteCoord(b,3)+h/2;}abort();}'''
 assert old in h;h=h.replace(old,new)
 # Clip/define only host scaffolding; never transform extracted function bodies.
 functions=[]
 for source,names in [(sprite,['StartSpriteAnim']), (mons,['SetAnimSpriteInitialXOffset','InitSpritePosToAnimAttacker','InitAnimLinearTranslation','StartAnimLinearTranslation','AnimTranslateLinear','AnimTranslateLinear_WithFollowup','InitAnimArcTranslation','TranslateAnimHorizontalArc']), (src1,['AnimTask_LeafBlade','AnimTask_LeafBlade_Step','LeafBladeGetPosFactor','AnimTask_LeafBlade_Step2','AnimTask_LeafBlade_Step2_Callback']), (src3,['AnimTriAttackTriangle','AnimTask_FacadeColorBlend','AnimTask_FacadeColorBlend_Step','AnimSmellingSaltsHand','AnimSmellingSaltsHand_Step','AnimSmellingSaltExclamation','AnimSmellingSaltExclamation_Step','AnimTask_SmellingSaltsSquish','AnimTask_SmellingSaltsSquish_Step'])]:
  functions += [base.function(source,n) for n in names]
 facade=src3[src3.index('#define IDX_ACTIVE_SPRITES 2  //'):src3.index('// Blends the mon sprite\'s color')]
 facade_names=['AnimTask_SquishAndSweatDroplets','AnimTask_SquishAndSweatDroplets_Step','CreateSweatDroplets','AnimFacadeSweatDrop']
 prototypes=functions+[base.function(src3,n) for n in facade_names]
 h+='\n'+ '\n'.join(f.split('\n{',1)[0]+';' for f in prototypes)+'\n'
 h+='''
#define RGB_BLACK RGB(0,0,0)
#define ANIM_TAG_TRI_ATTACK_TRIANGLE 3
#define ANIM_TAG_SWEAT_DROP 4
#define ANIM_TAG_TAG_HAND 5
#define ANIM_TAG_SMELLINGSALT_EFFECT 6
#define gOamData_AffineDouble_ObjNormal_64x64 oamDouble
#define gOamData_AffineOff_ObjNormal_8x8 oamOff
#define gOamData_AffineOff_ObjNormal_16x16 oamOff
#define gOamData_AffineOff_ObjNormal_32x32 oamOff
#define gDummySpriteAnimTable dummyFrames
#define gDummySpriteAffineAnimTable dummyAffines
'''
 def block(source,start,end):return source[source.index(start):source.index(end,source.index(start))]
 declarations=[block(src1,'static const union AnimCmd sLeafBladeAnimCmds1[]','static const union AffineAnimCmd sAromatherapyBigFlowerAffineAnimCmds'),block(src3,'static const union AnimCmd sTriAttackTriangleAnimCmds[]','static const union AnimCmd sEclipsingOrbAnimCmds[]'),block(src3,'static const union AffineAnimCmd sFacadeSquishAffineAnimCmds[]','const struct SpriteTemplate gRoarNoiseLineSpriteTemplate'),block(src3,'const struct SpriteTemplate gSmellingSaltsHandSpriteTemplate','const struct SpriteTemplate gHelpingHandClapSpriteTemplate')]
 # Facade section also contains the roar animation declaration before its template; trim to color table.
 declarations[2]=declarations[2][:declarations[2].index('};',declarations[2].index('static const u16 sFacadeBlendColors'))+2]
 declarations.append(base.declaration(sources['src/data.c'],'sAffineAnim_Battler_Normal'))
 h+='\n'.join(declarations)+'\n'
 h+='static const union AffineAnimCmd *const battlerAffines[]={sAffineAnim_Battler_Normal};\n'
 h+='''
static int createCalls,createSuccesses,destroyCalls,maxRawChildren,maxOwnedSprites;
static u8 ObservedCreateSprite(const struct SpriteTemplate*t,s16 x,s16 y,u8 priority){createCalls++;u8 n=CreateSprite(t,x,y,priority);createSuccesses+=n!=MAX_SPRITES;return n;}
static void ObservedDestroySprite(struct Sprite*s){destroyCalls++;DestroySprite(s);}
static void ObservedDestroyAnimSprite(struct Sprite*s){destroyCalls++;DestroyAnimSprite(s);}
static u8 ObservedCreateSpriteAndAnimate(const struct SpriteTemplate*t,s16 x,s16 y,u8 priority){createCalls++;u8 n=CreateSpriteAndAnimate(t,x,y,priority);createSuccesses+=n!=MAX_SPRITES;return n;}
#define CreateSprite ObservedCreateSprite
#define DestroySprite ObservedDestroySprite
#define DestroyAnimSprite ObservedDestroyAnimSprite
#define CreateSpriteAndAnimate ObservedCreateSpriteAndAnimate
'''
 h+='\n'.join(functions)+'\n'+facade+'\n'
 h+=HARNESS
 for k,cfg in enumerate(CONFIGS):h+='static const int reactionConfig'+str(k)+'[][5]={'+','.join('{'+','.join(map(str,c))+'}' for c in cfg)+'};\n'
 h+='static const int (*reactionConfigs[])[5]={'+','.join('reactionConfig'+str(k) for k in range(len(CONFIGS)))+'};\n'
 h+='static const int reactionCounts[]={'+','.join(str(len(c)) for c in CONFIGS)+'};\n'
 h+=RUNNER
 # Exact source functions that are present in emitted C, including reused player.
 spans=[]
 for filename,source in sources.items():
  for match in re.finditer(r'^(?:static )?(?:void|s8|s16|s32|u8|u16|bool8) (\w+)\([^;]+?\)\n\{',source,re.M):
   snippet=base.function(source,match.group(1))
   if snippet not in h:continue
   spans.append(span(filename,source,snippet))
 inherited=[]
 for filename,name in [('src/battle_anim_effects_2.c','sHiddenPowerOrbAffineAnimCmds'),('src/battle_anim_effects_2.c','sAffineAnims_StretchBattlerUp'),('src/battle_anim_effects_1.c','sMagicalLeafBlendColors'),('src/trig.c','gSineTable')]:inherited.append(base.declaration(sources[filename],name))
 for kind,name in [('AnimCmd','sDummyAnim'),('AffineAnimCmd','sDummyAffineAnim')]:inherited.append(re.search(r'^static const union '+kind+r' '+name+r' = .*?;',sprite,re.M)[0])
 for snippet in declarations+[facade]+inherited:
  candidates=[(name,source) for name,source in sources.items() if snippet in source];assert len(candidates)==1
  spans.append(span(*candidates[0],snippet))
 for snippet in functions+[facade]+declarations:assert snippet in h
 hashes=dict(base.SOURCE_SHA256);hashes['src/battle_anim_effects_3.c']=EFFECTS3_HASH;hashes['src/data.c']=DATA_HASH
 if hashlib.sha256((raw/'src/data.c').read_bytes()).hexdigest()!=DATA_HASH:raise ValueError('Pinned data.c mismatch')
 return h,dict(sourceRevision='037335f4c725d7c9aecdac87066f2002b4bd7e14',sourceSha256=hashes,sourceSpans=spans,kinds=KINDS,configColumns=CONFIG_COLUMNS,configs=CONFIGS,spriteColumns=SPRITE_COLUMNS,taskColumns=TASK_COLUMNS,stateColumns=STATE_COLUMNS,paletteColumns=PALETTE_COLUMNS,geometry={'x':[[72,176,48,112],[32,200,90,152]],'y':[[80,40,40,80],[80,40,88,32]],'yOffset':[5,11,7,13],'profileYOffset':[[5,11,7,13],[5,11,7,0]],'elevation':[2,3,4,5],'subpriority':[30,40,20,50],'widths':[[64,48,32,56],[8,16,40,64]],'heights':[[64,48,32,56],[8,64,16,64]]},conventions={'tickMinusOne':'After synchronous task or CreateSpriteAndAnimate initialization; no ordinary callback yet','sprites':'After AnimateSprites, before tasks','afterTaskSprites':'After active task callbacks in slot order; empty for sprite-only and palette-only kinds (no task-visible sprite changes)' ,'tasks':'After active task callbacks; all data fields included; affine pointer is stable handle (1,0)','rawChildren':'Leaf trails or facade sweat; controller counted separately in ownedSprites','roleCodes':{'battler':0,'leafController':1,'leafTrail':2,'sweat':3,'directAnimationSprite':4},'palette':'Both faded and unfaded, all 16 entries; initial color=(index*73+31)&32767','allocationModes':{'0':'ordinary first-free','1':'initial root/controller at slot32; released lower slots become available before first ordinary tick','2':'only final three slots free (partial allocation/failure)','3':'all sprite slots occupied; records source initializer failure behavior'},'sourceUndefinedCapacity':'Leaf initializer writes dummy slot64 after failed CreateSprite; harness stores that sentinel explicitly but never animates it','cancellation':'Source callbacks do not implement cancellation; not asserted as a source-C behavior'})

def span(filename,source,snippet):
 start=source.index(snippet);line=source[:start].count('\n')+1
 return {'file':filename,'firstLine':line,'lastLine':line+snippet.count('\n')-int(snippet.endswith('\n')),'byteOffset':len(source[:start].encode()),'byteLength':len(snippet.encode()),'sha256':hashlib.sha256(snippet.encode()).hexdigest(),'declaration':snippet.split('\n')[0]}

# Host observation layer. All callback logic is supplied by extracted C above.
HARNESS=r'''
static int currentKind,currentConfig,currentTick;
static int callbackId(SpriteCallback cb){if(cb==SpriteCallbackDummy)return 0;if(cb==AnimTriAttackTriangle)return 1;if(cb==StartAnimLinearTranslation)return 2;if(cb==AnimTranslateLinear_WithFollowup)return 3;if(cb==DestroyAnimSprite)return 4;if(cb==AnimFacadeSweatDrop)return 5;if(cb==AnimSmellingSaltsHand)return 6;if(cb==AnimSmellingSaltsHand_Step)return 7;if(cb==AnimSmellingSaltExclamation)return 8;if(cb==AnimSmellingSaltExclamation_Step)return 9;if(cb==AnimTask_LeafBlade_Step2_Callback)return 10;return -1;}
static int taskCallbackId(void(*cb)(u8)){if(cb==AnimTask_LeafBlade_Step)return 1;if(cb==AnimTask_SquishAndSweatDroplets_Step)return 2;if(cb==AnimTask_FacadeColorBlend_Step)return 3;if(cb==AnimTask_SmellingSaltsSquish_Step)return 4;return 0;}
static int role(int n){struct Sprite*s=&gSprites[n];if(n<slots)return 0;if(s->template==&gLeafBladeSpriteTemplate)return s->callback==AnimTask_LeafBlade_Step2_Callback?2:1;if(s->template==&gFacadeSweatDropSpriteTemplate)return 3;if(s->template==&gTriAttackTriangleSpriteTemplate||s->template==&gSmellingSaltsHandSpriteTemplate||s->template==&gSmellingSaltExclamationSpriteTemplate)return 4;return -1;}
static int live(int raw){int n=0;for(int i=slots;i<64;i++)if(gSprites[i].inUse){int r=role(i);n+=raw?(r==2||r==3):r>0;}return n;}
static int matrices(void){int n=0;for(int i=slots;i<32;i++)n+=(gOamMatrixAllocBitmap>>i)&1;return n;}
static void prefix(char type,int phase){printf("%d:%d:%d:%d:%d,%c,%d,%d",currentKind,mode,gBattleAnimAttacker,gBattleAnimTarget,currentConfig,type,currentTick,phase);}
static void recordSprites(int phase){for(int i=0;i<64;i++){struct Sprite*s=&gSprites[i];int r=role(i);if(!s->inUse||r<0)continue;if(r==0&&(!(currentKind==1||currentKind==3||currentKind==6)||i!=gTasks[0].data[15]))continue;int affine=s->oam.affineMode&1;struct OamMatrix m=affine?gOamMatrices[s->oam.matrixNum]:(struct OamMatrix){256,0,0,256};struct AffineAnimState*a=&sAffineAnimStates[s->oam.matrixNum];int row[]={i,r,s->x,s->y,s->x2,s->y2,s->invisible,s->oam.tileNum,affine?0:((s->oam.matrixNum>>3)&1),affine?0:((s->oam.matrixNum>>4)&1),s->subpriority,m.a,m.b,m.c,m.d,s->oam.affineMode,s->oam.matrixNum,s->affineAnimPaused,s->affineAnimEnded,affine?a->animCmdIndex:0,affine?a->delayCounter:0,affine?a->xScale:256,affine?a->yScale:256,affine?a->rotation:0,callbackId(s->callback),s->animNum,s->animCmdIndex,s->animDelayCounter,s->animEnded,s->animBeginning};prefix('S',phase);for(unsigned j=0;j<sizeof(row)/sizeof(row[0]);j++)printf(",%d",row[j]);for(int j=0;j<8;j++)printf(",%d",s->data[j]);puts("");}}
static void recordState(void){int raw=live(1),owned=live(0),active=0;if(currentKind==7)assert(raw==gTasks[0].data[12]);if(currentKind==1||currentKind==3)assert(raw==gTasks[0].data[2]);maxRawChildren=raw>maxRawChildren?raw:maxRawChildren;maxOwnedSprites=owned>maxOwnedSprites?owned:maxOwnedSprites;for(int i=0;i<2;i++)active+=gTasks[i].isActive;prefix('T',1);printf(",%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",gAnimVisualTaskCount,active,raw,owned,matrices(),createCalls,createSuccesses,destroyCalls,maxRawChildren,maxOwnedSprites,blendWrites,blendOffset0,blendCoefficient,blendColor);puts("");for(int t=0;t<2;t++){prefix('D',1);printf(",%d,%d,%d",t,gTasks[t].isActive,taskCallbackId(gTasks[t].func));for(int j=0;j<16;j++)printf(",%d",gTasks[t].data[j]);puts("");}if(currentKind==2||currentKind==3){int id=currentKind==3?1:0;int p=gTasks[id].data[2]/16-16;prefix('L',1);printf(",%d",p);for(int i=0;i<16;i++)printf(",%d",gPlttBufferFaded[OBJ_PLTT_ID(p)+i]);for(int i=0;i<16;i++)printf(",%d",gPlttBufferUnfaded[OBJ_PLTT_ID(p)+i]);puts("");for(int i=0;i<512;i++){assert(gPlttBufferUnfaded[i]==((i*73+31)&32767));if(i/16!=p+16)assert(gPlttBufferFaded[i]==gPlttBufferUnfaded[i]);}}}
static void initTask(int id,void(*fn)(u8)){gTasks[id].isActive=TRUE;gTasks[id].func=fn;fn(id);gAnimVisualTaskCount++;}
static void reserve(int n){gSprites[n].inUse=TRUE;gSprites[n].callback=SpriteCallbackDummy;gSprites[n].anims=dummyFrames;gSprites[n].affineAnims=dummyAffines;gSprites[n].usingSheet=TRUE;gSprites[n].animBeginning=TRUE;}
'''
RUNNER=r'''
static void runReaction(int kind,int actor,int target,int config){
 currentKind=kind;currentConfig=config;currentTick=-1;slots=mode?4:2;gBattleAnimAttacker=actor;gBattleAnimTarget=target;const int*c=reactionConfigs[kind][config];dimensionProfile=c[4];
 memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));memset(gSprites,0,sizeof(gSprites));memset(gTasks,0,sizeof(gTasks));memset(sAffineAnimStates,0,sizeof(sAffineAnimStates));memset(&battleSpritesData,0,sizeof(battleSpritesData));gOamMatrixAllocBitmap=(1u<<slots)-1;gAnimVisualTaskCount=0;for(int i=0;i<32;i++)SetOamMatrix(i,256,0,0,256);
 createCalls=createSuccesses=destroyCalls=maxRawChildren=maxOwnedSprites=0;
 for(int i=0;i<slots;i++){struct Sprite*s=&gSprites[i];reserve(i);s->affineAnims=battlerAffines;s->oam.affineMode=1;s->oam.matrixNum=i;s->oam.paletteNum=i;s->affineAnimPaused=TRUE;s->data[0]=i;s->x=GetBattlerSpriteCoord(i,2);s->y=GetBattlerSpriteCoord(i,3);s->subpriority=GetBattlerSpriteSubpriority(i);battleSpritesData.healthBoxesData[i].matrixNum=i;AffineAnimStateReset(i);}
 if(c[3]==1)for(int i=slots;i<32;i++)reserve(i);
 if(c[3]==2)for(int i=slots;i<61;i++)reserve(i);
 if(c[3]==3)for(int i=slots;i<64;i++)reserve(i);
 for(int i=0;i<512;i++)gPlttBufferUnfaded[i]=gPlttBufferFaded[i]=(i*73+31)&32767;
 for(int i=0;i<3;i++)gBattleAnimArgs[i]=c[i];blendWrites=0;blendOffset0=blendCoefficient=blendColor=-1;
 if(kind==0||kind==4||kind==5){const struct SpriteTemplate*t=kind==0?&gTriAttackTriangleSpriteTemplate:kind==4?&gSmellingSaltsHandSpriteTemplate:&gSmellingSaltExclamationSpriteTemplate;CreateSpriteAndAnimate(t,GetBattlerSpriteCoord(target,2),GetBattlerSpriteCoord(target,3),GetBattlerSpriteSubpriority(target)-2);gAnimVisualTaskCount++;}
 if(kind==1||kind==3)initTask(0,AnimTask_SquishAndSweatDroplets);
 if(kind==2)initTask(0,AnimTask_FacadeColorBlend);
 if(kind==3){gBattleAnimArgs[1]=c[2];initTask(1,AnimTask_FacadeColorBlend);}
 if(kind==6)initTask(0,AnimTask_SmellingSaltsSquish);
 if(kind==7)initTask(0,AnimTask_LeafBlade);
 if(c[3]==1)for(int i=slots;i<32;i++)memset(&gSprites[i],0,sizeof(gSprites[i]));
 recordSprites(0);if(kind==1||kind==3||kind==6||kind==7)recordSprites(1);recordState();if(!gAnimVisualTaskCount)return;
 for(currentTick=0;currentTick<1000;currentTick++){
  blendWrites=0;blendOffset0=blendCoefficient=blendColor=-1;
  AnimateSprites();recordSprites(0);for(int t=0;t<2;t++)if(gTasks[t].isActive)gTasks[t].func(t);if(kind==1||kind==3||kind==6||kind==7)recordSprites(1);recordState();
  if(!gAnimVisualTaskCount){assert(live(0)==0);assert(matrices()==0);return;}
 }fprintf(stderr,"did not finish kind=%d mode=%d actor=%d target=%d config=%d\n",kind,mode,actor,target,config);abort();
}
int main(void){for(int k=0;k<8;k++)for(mode=0;mode<2;mode++)for(int a=0;a<(mode?4:2);a++)for(int t=0;t<(mode?4:2);t++)for(int c=0;c<reactionCounts[k];c++)runReaction(k,a,t,c);return 0;}
'''
DATA_HASH='e1efcc8e6bde1c187fe51dafb0bdb8dbdc5ae3761c44f6488f4f42dbf19a71f9'
EFFECTS3_HASH='bdf8ba956af35ca232d7e5101090e2ff31a35f0ae91286a21b216e7787ba6c1f'


# Whole cases selected from source branches; never sample frames.
REPRESENTATIVE_CASES = (
 # Every admitted configuration, including cancellation boundaries, dimensions and allocator modes
 '0:0:0:1:0','0:0:0:1:1','0:0:0:1:2',
 # Every admitted configuration, including cancellation boundaries, dimensions and allocator modes
 '1:0:0:1:0','1:0:0:1:1','1:0:0:1:2','1:0:0:1:3',
 # Every admitted configuration, including cancellation boundaries, dimensions and allocator modes
 '2:0:0:1:0','2:0:0:1:1','2:0:0:1:2','2:0:0:1:3',
 # Every admitted configuration, including cancellation boundaries, dimensions and allocator modes
 '3:0:0:1:0','3:0:0:1:1','3:0:0:1:2',
 # Every admitted configuration, including cancellation boundaries, dimensions and allocator modes
 '4:0:0:1:0','4:0:0:1:1','4:0:0:1:2','4:0:0:1:3',
 # Every admitted configuration, including cancellation boundaries, dimensions and allocator modes
 '5:0:0:1:0','5:0:0:1:1','5:0:0:1:3',
 # Every admitted configuration, including cancellation boundaries, dimensions and allocator modes
 '6:0:0:1:0','6:0:0:1:1','6:0:0:1:2',
 # Every admitted configuration, including cancellation boundaries, dimensions and allocator modes
 '7:0:0:1:0','7:0:0:1:1','7:0:0:1:2','7:0:0:1:3',
 # Reverse side: mirrored hand/frame choices, squish, palette and leaf trajectory
 '0:0:1:0:0',
 # Reverse side: mirrored hand/frame choices, squish, palette and leaf trajectory
 '1:0:1:0:2',
 # Reverse side: mirrored hand/frame choices, squish, palette and leaf trajectory
 '3:0:1:0:1',
 # Reverse side: mirrored hand/frame choices, squish, palette and leaf trajectory
 '4:0:1:0:0','4:0:1:0:1',
 # Reverse side: mirrored hand/frame choices, squish, palette and leaf trajectory
 '5:0:1:0:3',
 # Reverse side: mirrored hand/frame choices, squish, palette and leaf trajectory
 '6:0:1:0:2',
 # Reverse side: mirrored hand/frame choices, squish, palette and leaf trajectory
 '7:0:1:0:0',
 # Doubles rear geometry with alternate dimensions and allocation pressure
 '0:1:2:3:1',
 # Doubles rear geometry with alternate dimensions and allocation pressure
 '1:1:2:3:3',
 # Doubles rear geometry with alternate dimensions and allocation pressure
 '2:1:2:3:3',
 # Doubles rear geometry with alternate dimensions and allocation pressure
 '3:1:2:3:2',
 # Doubles rear geometry with alternate dimensions and allocation pressure
 '4:1:2:3:3',
 # Doubles rear geometry with alternate dimensions and allocation pressure
 '7:1:2:3:1',
 # Leaf: rear player target geometry; attacker identity does not affect this callback
 '7:1:3:2:0',
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
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,default=ROOT);p.add_argument('--output-dir',type=Path,required=True);p.add_argument('--full',action='store_true',help='Emit all source-C cases for the same managed comparator');a=p.parse_args();a.output_dir.mkdir(parents=True,exist_ok=True)
 if (a.output_dir/'reaction_vectors.json').exists():p.error('Refusing to overwrite an existing candidate')
 h,result=build(a.root);c=a.output_dir/'reaction_source.c';exe=a.output_dir/'reaction_source';c.write_text(h)
 command=['cc','-std=c11','-O0','-fwrapv',str(c),'-o',str(exe)];subprocess.run(command,check=True)
 output=subprocess.check_output([str(exe)],text=True);subprocess.run(['cc','-std=c11','-O2','-fwrapv',str(c),'-o',str(exe)],check=True);optimized=subprocess.check_output([str(exe)],text=True);assert output==optimized, 'O0/O2 source-C row mismatch';(a.output_dir/'reaction_source.rows').write_text(output) if a.full else None
 cases={}
 for line in output.splitlines():
  key,typ,tick,phase,*values=line.split(',');tick=int(tick);phase=int(phase);v=list(map(int,values));row=cases.setdefault(key,{}).setdefault(tick,{'tick':tick,'sprites':[],'afterTaskSprites':[],'tasks':[],'palettes':[]})
  if typ=='S':assert len(v)==len(SPRITE_COLUMNS);row['sprites' if phase==0 else 'afterTaskSprites'].append(v)
  elif typ=='D':assert len(v)==len(TASK_COLUMNS);row['tasks'].append(v)
  elif typ=='L':assert len(v)==len(PALETTE_COLUMNS);row['palettes'].append(v)
  elif typ=='T':assert len(v)+1==len(STATE_COLUMNS);row['state']=[tick]+v
  else:raise ValueError(typ)
 result['cases']={key:list(rows.values()) for key,rows in cases.items()}
 # These source paths are intentionally rejected by the managed runtime.
 # Keep their every-frame evidence separate from the admitted parity cases.
 def diagnostic(key):
  kind,mode,actor,target,config=map(int,key.split(':'))
  return (kind,config) in {(1,4),(7,4),(5,2)}
 diagnostics={key:rows for key,rows in result['cases'].items() if diagnostic(key)}
 result['cases']={key:rows for key,rows in result['cases'].items() if not diagnostic(key)}
 assert len(result['cases'])==560 and len(diagnostics)==60
 apply_profile(result,a.full)
 result['diagnosticCases']=diagnostics if a.full else {key:diagnostics[key] for key in ['1:0:0:1:4','7:0:0:1:4','5:0:0:1:2']}
 result['coverage']['diagnosticCaseCount']=len(result['diagnosticCases'])
 result['build']={'command':['cc','-std=c11','-O0','-fwrapv','reaction_source.c','-o','reaction_source'],'compiler':subprocess.check_output(['cc','--version'],text=True).splitlines()[0],'harnessSha256':hashlib.sha256(h.encode()).hexdigest()}
 out=a.output_dir/'reaction_vectors.json';out.write_text(json.dumps(result,separators=(',',':'))+'\n')
 print(json.dumps({'cases':len(result['cases']),'rows':sum(len(v) for v in result['cases'].values()),'fixture':str(out),'sha256':hashlib.sha256(out.read_bytes()).hexdigest()}))
if __name__=='__main__':main()

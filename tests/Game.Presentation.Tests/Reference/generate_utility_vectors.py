#!/usr/bin/env python3
"""Complete utility callback/task cases from pinned C and shared native players.
Every chosen case retains every frame, including raw-child allocation failures.
"""
import argparse,hashlib,importlib.util,json,re,tempfile
from pathlib import Path
CONFIGS=[[[0],[1],[2]],[[20,0,0,24,30],[20,0,-24,24,30],[20,0,24,24,30],[-32768,32767,-32768,32767,1],[32767,-32768,32767,-32768,255]],[[0],[1],[2]],[[0]]]
SOURCE_SHA256={'src/battle_anim_effects_3.c': 'bdf8ba956af35ca232d7e5101090e2ff31a35f0ae91286a21b216e7787ba6c1f', 'src/battle_anim_psychic.c': '49a9c2a00ecb6791329fe6bb4f4e2612d4ba8f31a83dbfe39d1cafc40047c49a', 'src/battle_anim_ghost.c': '622823b0ad91a22d98b29964caf03a35409d8f667379d45477ad7f660c31955e'}
TEMPLATES=['gYawnCloudSpriteTemplate','gSpikesSpriteTemplate','sSkillSwapOrbSpriteTemplate','gGrudgeFlameSpriteTemplate']
COLUMNS=['slot','x','y','x2','y2','visible','tile','priority','subpriority','a','b','c','d','affine']
def module(n,p):
 s=importlib.util.spec_from_file_location(n,p);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def source(root):
 raw=root/'content/raw/pokefirered/037335f4'
 for name,digest in SOURCE_SHA256.items():
  if hashlib.sha256((raw/name).read_bytes()).hexdigest()!=digest:raise ValueError('Pinned utility source mismatch: '+name)
 base=module('utility_native_base',root/'tests/Game.Presentation.Tests/Reference/generate_orbit_spray_vectors.py')
 with tempfile.TemporaryDirectory() as d:
  p=Path(d)/'base.c';base.build(root,'tasks',p);h=p.read_text().split('static const long long config0')[0]
 h=h.replace('u8 affineMode,matrixNum,shape,size,paletteNum,objMode;','u8 affineMode,matrixNum,shape,size,paletteNum,objMode;u8 priority:2;')
 h=h.replace('struct Task gTasks[1]','struct Task gTasks[2]').replace('assert(n==0);gTasks[n].isActive=FALSE;','assert(n<2);gTasks[n].isActive=FALSE;')
 h=h.replace('static s16 GetBattlerSpriteCoordAttr(u8 b,u8 attr){assert(attr==0);return (s16[]){64,48,32,56}[b];}',r'''static s16 GetBattlerSpriteCoordAttr(u8 b,u8 attr){int w=(int[]){56,40,32,64}[b],h=(int[]){64,48,32,56}[b],x=GetBattlerSpriteCoord(b,2),y=GetBattlerSpriteCoord(b,3);switch(attr){case 0:return h;case 1:return w;case 2:return x-w/2;case 3:return x+w/2;case 4:return y-h/2;case 5:return y+h/2;default:abort();}}''')
 tags=(raw/'include/constants/battle_anim.h').read_text()
 for name in ['ANIM_SPRITES_START','ANIM_TAG_PINK_CLOUD','ANIM_TAG_SPIKES','ANIM_TAG_BLUEGREEN_ORB','ANIM_TAG_PURPLE_FLAME']:
  h+=re.search(r'^#define '+name+r'\s+[^\n]+',tags,re.M)[0]+'\n'
 h+=r'''
#include "gba/io_reg.h"
#define BATTLE_PARTNER(n) ((n)^2)
#define BATTLER_COORD_ATTR_WIDTH 1
#define BATTLER_COORD_ATTR_LEFT 2
#define BATTLER_COORD_ATTR_RIGHT 3
#define BATTLER_COORD_ATTR_TOP 4
#define BATTLER_COORD_ATTR_BOTTOM 5
#define gDummySpriteAnimTable dummyFrames
#define gDummySpriteAffineAnimTable dummyAffines
static u16 gpu[128];
static void SetGpuReg(int offset,u16 value){gpu[offset/2]=value;}
static int IsDoubleBattle(void){return mode;}
static u8 GetBattlerSpriteBGPriority(u8 b){return (u8[]){1,0,0,1}[b];}
static u8 GetBattlerYCoordWithElevation(u8 b){return GetBattlerSpriteCoord(b,1)-((b&1)?(int[]){2,3,4,5}[b]:0);}
static const struct OamData gOamData_AffineNormal_ObjNormal_32x32={.affineMode=1,.size=2,.priority=2},gOamData_AffineOff_ObjNormal_16x16={.size=1,.priority=2},gOamData_AffineNormal_ObjNormal_16x16={.affineMode=1,.size=1,.priority=2},gOamData_AffineOff_ObjBlend_16x32={.shape=2,.size=2,.objMode=1,.priority=2};
'''
 src={n:(raw/('src/'+n+'.c')).read_text() for n in ['battle_anim_mons','battle_anim_effects_3','battle_anim_psychic','battle_anim_ghost']}
 names={'battle_anim_mons':['InitSpritePosToAnimAttacker','SetAnimSpriteInitialXOffset','SetSpriteCoordsToAnimAttackerCoords','DestroySpriteAndMatrix','SetAverageBattlerPositions','InitAnimArcTranslation','TranslateAnimHorizontalArc','InitAnimLinearTranslation','AnimTranslateLinear','WaitAnimForDuration'],
 'battle_anim_effects_3':['InitYawnCloudPosition','UpdateYawnCloudPosition','AnimYawnCloud','AnimYawnCloud_Step','AnimSpikes','AnimSpikes_Step1','AnimSpikes_Step2'],
 'battle_anim_psychic':['AnimTask_SkillSwap','AnimTask_SkillSwap_Step','AnimSkillSwapOrb'],
 'battle_anim_ghost':['AnimTask_GrudgeFlames','AnimTask_GrudgeFlames_Step','AnimGrudgeFlame']}
 fs=[base.function(src[f],n) for f,ns in names.items() for n in ns];h+='\n'.join(f.split('\n{',1)[0]+';' for f in fs)+'\n'
 for file,prefix in [('battle_anim_effects_3','sYawnCloud'),('battle_anim_psychic','sAffineAnim_SkillSwap'),('battle_anim_psychic','sAffineAnims_SkillSwap'),('battle_anim_ghost','sAnim_Grudge'),('battle_anim_ghost','sAnims_Grudge')]:
  snippets=re.findall(r'static const union (?:AffineAnimCmd|AnimCmd) (?:\*const )?'+prefix+r'\w*\[\]\s*=\s*\{.*?\};',src[file],re.S);assert snippets,prefix;h+='\n'.join(snippets)+'\n'
 for i,name in enumerate(TEMPLATES):
  file='battle_anim_effects_3' if i<2 else 'battle_anim_psychic' if i==2 else 'battle_anim_ghost';h+=re.search(r'(?:static )?const struct SpriteTemplate '+name+r'\s*=\s*\{.*?\};',src[file],re.S)[0]+'\n'
 h+='\n'.join(fs)+'\n'
 for k,cs in enumerate(CONFIGS):h+='static const int config'+str(k)+'[][5]={'+','.join('{'+','.join(map(str,c))+'}' for c in cs)+'};\n'
 h+='static const int(*configs[])[5]={config0,config1,config2,config3};\nstatic const int counts[]={'+','.join(map(str,map(len,CONFIGS)))+'};\n'
 h+=r'''
static struct Sprite saved[64];static struct OamMatrix savedMatrices[32];
static int utility(struct Sprite*s){return s->inUse&&(s->template==&gYawnCloudSpriteTemplate||s->template==&gSpikesSpriteTemplate||s->template==&sSkillSwapOrbSpriteTemplate||s->template==&gGrudgeFlameSpriteTemplate);}
static int matrixCount(void){int n=0;for(int i=0;i<32;i++)n+=(gOamMatrixAllocBitmap>>i)&1;return n;}
static void capture(void){memcpy(saved,gSprites,sizeof saved);memcpy(savedMatrices,gOamMatrices,sizeof savedMatrices);}
static void emit(int kind,int cfg,int capacity,int tick){
 printf("%d:%d:%d:%d:%d:%d,%d,%d,%d,%d,%d,%d",kind,mode,gBattleAnimAttacker,gBattleAnimTarget,cfg,capacity,tick,gTasks[0].isActive,gTasks[1].isActive,gAnimVisualTaskCount,gpu[REG_OFFSET_BLDCNT/2],gpu[REG_OFFSET_BLDALPHA/2]);
 printf(",%d,%d",gBattleAnimArgs[2],matrixCount());
 for(int t=0;t<2;t++)for(int i=0;i<16;i++)printf(",%d",gTasks[t].data[i]);
 putchar('|');int first=1;for(int i=0;i<64;i++){struct Sprite*s=&saved[i];if(!utility(s))continue;struct OamMatrix m=(s->oam.affineMode&1)?savedMatrices[s->oam.matrixNum]:(struct OamMatrix){256,0,0,256};if(!first)putchar(';');first=0;printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",i,s->x,s->y,s->x2,s->y2,!s->invisible,s->oam.tileNum,s->oam.priority,s->subpriority,m.a,m.b,m.c,m.d,s->oam.affineMode&1);}putchar('\n');
}
static void run(int kind,int a,int t,int cfg,int capacity){
 slots=mode?4:2;gBattleAnimAttacker=a;gBattleAnimTarget=t;memset(gSprites,0,sizeof gSprites);memset(gTasks,0,sizeof gTasks);memset(sAffineAnimStates,0,sizeof sAffineAnimStates);memset(gBattleAnimArgs,0,sizeof gBattleAnimArgs);memset(gpu,0,sizeof gpu);gOamMatrixAllocBitmap=(1u<<slots)-1;gAnimVisualTaskCount=0;
 for(int i=0;i<32;i++)SetOamMatrix(i,256,0,0,256);
 for(int i=0;i<(capacity<0?slots:64-capacity);i++){struct Sprite*s=&gSprites[i];s->inUse=TRUE;s->callback=SpriteCallbackDummy;s->anims=dummyFrames;s->affineAnims=dummyAffines;s->animPaused=s->affineAnimPaused=TRUE;}
 for(int i=0;i<5;i++)gBattleAnimArgs[i]=configs[kind][cfg][i];
 if(kind<2){CreateSpriteAndAnimate(kind==0?&gYawnCloudSpriteTemplate:&gSpikesSpriteTemplate,GetBattlerSpriteCoord(t,2),GetBattlerSpriteCoord(t,3),28);gAnimVisualTaskCount++;}
 else{gTasks[0].isActive=TRUE;gAnimVisualTaskCount++;if(kind==2){if(gBattleAnimArgs[0]==2)gBattleAnimArgs[0]=1;AnimTask_SkillSwap(0);}else AnimTask_GrudgeFlames(0);}
 capture();emit(kind,cfg,capacity,-1);
 for(int tick=0;tick<500;tick++){
  AnimateSprites();capture();
  if(kind==2&&configs[kind][cfg][0]==2&&tick==16){gBattleAnimArgs[0]=0;gTasks[1].isActive=TRUE;gAnimVisualTaskCount++;AnimTask_SkillSwap(1);}
  for(int task=0;task<2;task++)if(gTasks[task].isActive)gTasks[task].func(task);
  emit(kind,cfg,capacity,tick);int count=0;for(int i=0;i<64;i++)count+=utility(&gSprites[i]);
  if(!gAnimVisualTaskCount&&!count){assert(gOamMatrixAllocBitmap==((1u<<slots)-1));return;}
 }abort();
}
int main(void){for(int k=0;k<4;k++)for(mode=0;mode<2;mode++)for(int a=0;a<(mode?4:2);a++)for(int t=0;t<(mode?4:2);t++)for(int c=0;c<counts[k];c++)run(k,a,t,c,-1);for(int k=2;k<4;k++)for(mode=0;mode<2;mode++)for(int c=0;c<counts[k];c++)for(int cap=0;cap<=2;cap++)run(k,0,1,c,cap);return 0;}
'''
 return h

def build(root,out,full=False):
 out.mkdir(parents=True,exist_ok=True);base=module('utility_compile',root/'tests/Game.Presentation.Tests/Reference/generate_cure_vectors.py');rows=base.compile_rows(root,source(root),'utility-native',out);cases={}
 for line in rows.splitlines():
  header,sprites=line.split('|');key,*state=header.split(',');cases.setdefault(key,[]).append(dict(state=list(map(int,state)),sprites=[list(map(int,s.split(','))) for s in sprites.split(';') if s]))
 all_count=len(cases);selected=[]
 for kind,configs in enumerate(CONFIGS):
  selected += [f'{kind}:0:0:1:{c}:-1' for c in range(len(configs))]
  selected += [f'{kind}:0:1:0:0:-1']+[f'{kind}:1:{a}:{t}:0:-1' for a in range(4) for t in range(4)]
  if kind>=2:selected += [f'{kind}:{mode}:0:1:{c}:{cap}' for mode in range(2) for c in range(len(configs)) for cap in range(3)]
 selected+=['2:1:3:2:2:-1','0:1:2:3:2:-1','1:1:3:0:2:-1'];assert len(selected)==len(set(selected))
 if not full:cases={k:cases[k] for k in selected}
 assert cases and all(v[0]['state'][0]==-1 and v[-1]['state'][3]==0 and not v[-1]['sprites'] for v in cases.values())
 return dict(templates=TEMPLATES,configs=CONFIGS,columns=COLUMNS,cases=cases,profile='full' if full else 'representative',coverage=dict(caseCount=len(cases),fullCaseCount=all_count,frames=sum(map(len,cases.values())),spriteRows=sum(len(f['sprites']) for v in cases.values() for f in v)),sourceHashes=SOURCE_SHA256)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output-dir',type=Path,required=True);p.add_argument('--full',action='store_true');a=p.parse_args();dest=a.output_dir/'utility-family-source-vectors.json'
 if dest.exists():p.error('Refusing to overwrite native vectors')
 result=build(a.root,a.output_dir,a.full);dest.write_text(json.dumps(result,separators=(',',':'))+'\n');print(result['coverage'])

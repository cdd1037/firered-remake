#!/usr/bin/env python3
"""Extract original Petal Dance/Ice Ball callbacks and every selected native frame."""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile
from pathlib import Path

def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def build(root,reference,build_dir):
 helper=module('reference_c',root/'tests/Reference/reference_c.py');helper.verify_reference(reference)
 f=module('fang_harness',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py')
 paths=['src/sprite.c','include/sprite.h','src/battle_anim_effects_1.c','src/battle_anim_ice.c','src/battle_anim_mons.c','include/battle_anim.h','src/trig.c','src/random.c','include/random.h','include/global.h','data/battle_anim_scripts.s']
 pins=json.loads((Path(__file__).parent/'pinned-inputs.json').read_text());s={p:(reference/p).read_text() for p in paths}
 for p in paths:
  if hashlib.sha256((reference/p).read_bytes()).hexdigest()!=pins[p]:raise ValueError('Pinned source changed: '+p)
 names={'src/battle_anim_effects_1.c':['AnimPetalDanceBigFlower','AnimPetalDanceBigFlower_Step','AnimPetalDanceSmallFlower','AnimPetalDanceSmallFlower_Step'],'src/battle_anim_ice.c':['InitIceBallAnim','AnimThrowIceBall','InitIceBallParticle','AnimIceBallParticle','AnimTask_GetRolloutCounter'],'src/battle_anim_mons.c':['SetAnimSpriteInitialXOffset','InitSpritePosToAnimAttacker','InitSpritePosToAnimTarget','InitAnimLinearTranslation','AnimTranslateLinear','InitAnimArcTranslation','TranslateAnimHorizontalArc','RunStoredCallbackWhenAnimEnds'],'src/sprite.c':['StartSpriteAnim','StartSpriteAffineAnim','AffineAnimStateStartAnim'],'src/trig.c':['Sin','Cos'],'src/random.c':['Random']}
 funcs=[f.function(s[p],n) for p,ns in names.items() for n in ns]
 sine=re.search(r'const s16 gSineTable\[\]\s*=\s*\{.*?\};',s['src/trig.c'],re.S)[0]
 macros='\n'.join(line for line in s['include/battle_anim.h'].splitlines() if re.match(r'#define sTransl_',line))+'\n'
 random_macro='\n'.join(re.search(r'^#define '+name+r'.*$',s[path],re.M)[0] for name,path in [('ISO_RANDOMIZE1','include/random.h'),('RAND_MULT','include/random.h'),('Q_8_8\\(','include/global.h')])
 extra=macros+random_macro+'\n'+sine+r'''
#define B_SIDE_PLAYER 0
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define ST_OAM_HFLIP 8
static int doubles;static u8 gBattleAnimAttacker,gBattleAnimTarget;static s16 gBattleAnimArgs[8];static u32 gRngValue;
static struct {u8 rolloutTimerStartValue,rolloutTimer;} disable,*gAnimDisableStructPtr=&disable;
static int taskActive=1;
static void DestroyAnimVisualTask(u8 id){assert(id==0);taskActive=0;}
static int GetBattlerSide(u8 id){return id&1;}
static int GetBattlerSpriteSubpriority(u8 id){static const int priorities[]={30,40,20,50};return priorities[id];}
static int GetBattlerSpriteCoord(u8 id,u8 coord){static const int one[4][2]={{72,80},{176,40},{48,40},{112,80}},two[4][2]={{32,80},{200,40},{90,88},{152,32}};assert(coord<4);return (doubles?two[id][coord&1]:one[id][coord&1])+(coord==3?8:0);}
static int GetBattlerSpriteCoord2(u8 id,u8 coord){return GetBattlerSpriteCoord(id,coord);}
'''
 h=f.sprite_harness(reference,funcs,extra).replace('struct {u8 affineMode;int tileNum;} oam;','struct {u8 affineMode;int tileNum,matrixNum;} oam;\n u8 subpriority;')
 h=h.replace('static void SetSpriteOamFlipBits(struct Sprite*s,u8 h,u8 v){s->hFlip=h;s->vFlip=v;}',f.function(s['src/sprite.c'],'SetSpriteOamFlipBits'))
 for p,ns in [('src/battle_anim_effects_1.c',['sPetalDanceBigFlowerAnimCmds','sPetalDanceSmallFlowerAnimCmds']),('src/battle_anim_ice.c',['sAnim_IceCrystalSmall','sAnim_IceBallChunk_0','sAnim_IceBallChunk_1']+['sAffineAnim_IceBallChunk_'+str(i) for i in range(5)])]:
  h+='\n'.join(f.declaration(s[p],n) for n in ns)+'\n'
 h+=r'''
static const union AnimCmd *const big[]={sPetalDanceBigFlowerAnimCmds},*const small[]={sPetalDanceSmallFlowerAnimCmds},*const chunk[]={sAnim_IceBallChunk_0,sAnim_IceBallChunk_1},*const shard[]={sAnim_IceCrystalSmall};
static const union AffineAnimCmd *const iceAffine[]={sAffineAnim_IceBallChunk_0,sAffineAnim_IceBallChunk_1,sAffineAnim_IceBallChunk_2,sAffineAnim_IceBallChunk_3,sAffineAnim_IceBallChunk_4};
int main(int argc,char**argv){assert(argc==15);int kind=atoi(argv[1]);doubles=atoi(argv[2]);gBattleAnimAttacker=atoi(argv[3]);gBattleAnimTarget=atoi(argv[4]);int count=atoi(argv[5]);gRngValue=strtoul(argv[6],0,10);disable.rolloutTimerStartValue=5;disable.rolloutTimer=5-count;for(int i=0;i<8;i++)gBattleAnimArgs[i]=atoi(argv[7+i]);
 if(kind==4){AnimTask_GetRolloutCounter(0);printf("%d",taskActive);for(int i=0;i<8;i++)printf(",%d",gBattleAnimArgs[i]);puts("");return 0;}
 struct Sprite*s=&gSprites[0];memset(s,0,sizeof(*s));AffineAnimStateReset(0);s->inUse=TRUE;s->usingSheet=TRUE;s->animBeginning=TRUE;s->affineAnimBeginning=TRUE;s->x=GetBattlerSpriteCoord(gBattleAnimTarget,2);s->y=GetBattlerSpriteCoord(gBattleAnimTarget,3);s->subpriority=2;
 if(kind==0){s->anims=big;s->callback=AnimPetalDanceBigFlower;}else if(kind==1){s->anims=small;s->callback=AnimPetalDanceSmallFlower;}else if(kind==2){s->anims=chunk;s->callback=InitIceBallAnim;s->oam.affineMode=3;s->affineAnims=iceAffine;}else{s->anims=shard;s->callback=InitIceBallParticle;}
 for(int tick=-1;tick<512;tick++){AnimateSprites();struct AffineAnimState*a=&sAffineAnimStates[0];printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%d\n",tick,s->inUse,s->x,s->y,s->x2,s->y2,s->oam.tileNum,(s->oam.matrixNum>>3)&1,s->subpriority,s->animEnded,a->xScale,a->yScale,s->affineAnimEnded,s->animDelayCounter,s->callback==DestroyAnimSprite,gRngValue,gBattleAnimArgs[2]);if(!s->inUse)return 0;}abort();}
'''
 source=build_dir/'petal-ice.c';source.write_text(h);executables=[]
 for opt in ['O0','O2']:
  exe=build_dir/('petal-ice-'+opt);helper.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(source),'-o',str(exe)]);executables.append(exe)
 def case(kind,d,actor,target,count,seed,args):return [kind,d,actor,target,count,seed,*args,*([2007,2008] if len(args)==6 else [])]
 cases=[]
 for d,actor,target in [(0,0,1),(0,1,0),(1,0,1),(1,1,0),(1,2,3),(1,3,2)]:
  cases.append(case(0,d,actor,target,1,1,[0,-24,8,140,0,0]))
  cases.append(case(1,d,actor,target,1,1,[16,-24,8,100,0,0]))
  cases.append(case(3,d,actor,target,1,0x12345678,[-12,-16,111,222,333,444]))
 for args in [[20,-16,14,80,0,0],[-20,-14,16,80,0,0],[0,0,-8,1,0,0]]:
  cases.append(case(1,1,2,0,1,1,args))
 for count in range(1,6):
  for actor in [0,1]:cases.append(case(2,0,actor,actor^1,count,1,[15,0,-12,-16,30,-40]))
 for actor,target in [(2,3),(3,2),(0,2),(2,0)]:cases.append(case(2,1,actor,target,5,1,[15,0,-12,-16,30,-40]))
 for seed in [0,1,2,3,0xffffffff]:cases.append(case(3,1,0,2,1,seed,[-12,-16,111,222,333,444]))
 for count,register in [(1,0),(5,0),(3,7)]:cases.append(case(4,0,0,1,count,1,[register,1001,1002,1003,1004,1005,1006,1007]))
 results=[]
 for c in cases:
  outputs=[subprocess.check_output([str(exe),*map(str,c)],text=True) for exe in executables]
  if outputs[0]!=outputs[1]:raise ValueError('Native optimization parity failure '+str(c))
  results.append(dict(kind=c[0],doubles=bool(c[1]),actor=c[2],target=c[3],counter=c[4],seed=c[5],arguments=c[6:],rows=[list(map(int,row.split(','))) for row in outputs[0].splitlines()]))
 return dict(schema='firered-petal-ice-source-v1',sourceSha256=pins,optimizationParity=['O0','O2'],semantics='Frame -1 is original creation callback followed by native AnimateSprite. Subsequent rows run native AnimateSprites. Coordinates are source static bases plus picture YOffset8 (player gets its native8). The BIOS matrix call is stubbed; native affine scale/END is observed. Task cases retain every untouched argument register. Seeded original Random is presentation-only.',cases=results)

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--reference',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);a=p.parse_args()
 if a.output.exists():raise FileExistsError(a.output)
 out=a.build_dir or Path(tempfile.mkdtemp(prefix='petal-ice-reference-'));out.mkdir(exist_ok=True,parents=True);r=build(a.root,a.reference or a.root/'content/raw/pokefirered/037335f4',out);a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(r,separators=(',',':'))+'\n');print(json.dumps(dict(cases=len(r['cases']),rows=sum(len(c['rows']) for c in r['cases']))))

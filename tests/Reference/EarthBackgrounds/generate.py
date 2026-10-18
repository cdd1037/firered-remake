#!/usr/bin/env python3
"""Pinned BG3 holders/predicate and affine rock: complete callback and native animation lives."""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile
from pathlib import Path

def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def build(root,reference,out,full=False):
 helper=module('reference_c',root/'tests/Reference/reference_c.py');helper.verify_reference(reference)
 f=module('native_sprite',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py');x=module('extract',root/'tests/Reference/ConfusionKiss/source_extract.py');here=Path(__file__).parent;pins=json.loads((here/'pinned-inputs.json').read_text());s={p:(reference/p).read_text() for p in pins}
 for p,h in pins.items():
  if hashlib.sha256((reference/p).read_bytes()).hexdigest()!=h:raise ValueError('Pinned source changed: '+p)
 if not re.search(r'^#define ARG_RET_ID 7$',s['include/constants/battle_anim.h'],re.M):raise ValueError('Source retained predicate register differs')
 names={'src/battle_anim_ground.c':['AnimTask_PositionFissureBgOnBattler','WaitForFissureCompletion'],'src/battle_anim_rock.c':['AnimTask_GetSeismicTossDamageLevel','AnimTask_MoveSeismicTossBg','AnimTask_SeismicTossBgAccelerateDownAtEnd','AnimRockScatter','AnimRockScatter_Step'],'src/trig.c':['Sin','Cos'],'src/sprite.c':['StartSpriteAnim']}
 funcs=[f.function(s[p],n) for p,ns in names.items() for n in ns]
 extra=r'''
#define Q_8_8(n) ((s16)((n)*256))
#define BIT_FLANK 2
#define ARG_RET_ID 7
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
struct Task {void(*func)(u8);bool8 isActive;u8 priority;s16 data[16];};
static struct Task gTasks[2];static s16 gBattleAnimArgs[8];static u8 gBattleAnimAttacker,gBattleAnimTarget;static s32 gAnimMoveDmg;static u16 gBattle_BG3_X,gBattle_BG3_Y;static int screen=1,doubles,yoff,elevation;
static s16 GetBattlerSpriteCoord(u8 id,u8 coord){static const s16 p[2][4][2]={{{72,80},{176,40},{48,40},{112,80}},{{32,80},{200,40},{90,88},{152,32}}};int value=p[doubles][id][coord&1];if(coord==3){value=(value+yoff-((id&1)?elevation:0))&255;if(!(id&1))value+=8;if(value>104)value=104;}return value;}
static u8 CreateTask(void(*fn)(u8),u8 priority){assert(!gTasks[1].isActive);gTasks[1].isActive=TRUE;gTasks[1].func=fn;gTasks[1].priority=priority;return 1;}
static void DestroyTask(u8 id){assert(id<2);gTasks[id].isActive=FALSE;}
static void DestroyAnimVisualTask(u8 id){DestroyTask(id);}
static void ToggleBg3Mode(bool8 largeScreenSize){screen=largeScreenSize?1:0;}
'''+x.array(s['src/trig.c'],'gSineTable')
 h=f.sprite_harness(reference,funcs,extra)
 for name in ['sAnim_Rock_'+n for n in ['Biggest','Bigger','Big','Small','Smaller','Smallest']]+['sAffineAnim_BasicRock_0','sAffineAnim_BasicRock_1']:h+=f.declaration(s['src/battle_anim_rock.c'],name)+'\n'
 h+=r'''
static const union AnimCmd*const frames[]={sAnim_Rock_Biggest,sAnim_Rock_Bigger,sAnim_Rock_Big,sAnim_Rock_Small,sAnim_Rock_Smaller,sAnim_Rock_Smallest};
static const union AffineAnimCmd*const affine[]={sAffineAnim_BasicRock_0,sAffineAnim_BasicRock_1};
static void row(int kind,int frame){struct Sprite*s=&gSprites[0];struct Task*t=&gTasks[kind==0?1:0];struct AffineAnimState*a=&sAffineAnimStates[0];printf("{\"frame\":%d,\"alive\":%d,\"parentAlive\":%d,\"priority\":%d,\"bg\":[%u,%u,%d],\"data\":[",frame,kind==4?s->inUse:t->isActive,gTasks[0].isActive,t->priority,gBattle_BG3_X,gBattle_BG3_Y,screen);for(int i=0;i<(kind==4?8:16);i++)printf("%s%d",i?",":"",kind==4?s->data[i]:t->data[i]);printf("],\"args\":[");for(int i=0;i<8;i++)printf("%s%d",i?",":"",gBattleAnimArgs[i]);printf("],\"sprite\":[%d,%d,%d,%d,%d,%d,%d],\"affine\":[%d,%d,%d,%d]}\n",s->x,s->y,s->x2,s->y2,s->oam.tileNum,s->animEnded,s->animDelayCounter,a->xScale,a->yScale,a->rotation,s->affineAnimEnded);}
int main(int argc,char**argv){assert(argc==19);int kind=atoi(argv[1]);doubles=atoi(argv[2]);gBattleAnimAttacker=atoi(argv[3]);gBattleAnimTarget=atoi(argv[4]);yoff=atoi(argv[5]);elevation=atoi(argv[6]);gAnimMoveDmg=atoi(argv[7]);int stop=atoi(argv[8]);gBattle_BG3_X=atoi(argv[9]);gBattle_BG3_Y=atoi(argv[10]);for(int i=0;i<8;i++)gBattleAnimArgs[i]=atoi(argv[11+i]);
 if(kind==4){struct Sprite*s=&gSprites[0];s->inUse=TRUE;s->usingSheet=TRUE;s->animBeginning=TRUE;s->affineAnimBeginning=TRUE;s->oam.affineMode=1;s->anims=frames;s->affineAnims=affine;s->callback=AnimRockScatter;AffineAnimStateReset(0);for(int frame=-1;frame<40;frame++){AnimateSprites();row(kind,frame);if(!s->inUse)return 0;}abort();}
 void(*fn)(u8)=kind==0?AnimTask_PositionFissureBgOnBattler:kind==1?AnimTask_GetSeismicTossDamageLevel:kind==2?AnimTask_MoveSeismicTossBg:AnimTask_SeismicTossBgAccelerateDownAtEnd;gTasks[0].isActive=TRUE;gTasks[0].func=fn;fn(0);
 for(int frame=0;frame<256;frame++){row(kind,frame);int id=kind==0?1:0;if(!gTasks[id].isActive)return 0;if(frame+1==stop)gBattleAnimArgs[7]=kind==0?gTasks[id].data[3]:0xFFF;gTasks[id].func(id);}abort();}
'''
 c=out/'earth-backgrounds.c';c.write_text(h);exes=[]
 for opt in ['O0','O2']:
  exe=out/('earth-backgrounds-'+opt);helper.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(c),'-o',str(exe)]);exes.append(exe)
 cases=[]
 def add(kind,d,a,t,args=(),y=0,e=0,damage=0,stop=17,bgx=65530,bgy=65520,label='authored'):
  args=list(args)+[2000+i for i in range(len(args),8)];argv=[kind,d,a,t,y,e,damage,stop,bgx,bgy,*args];outputs=[subprocess.check_output([str(exe),*map(str,argv)],text=True) for exe in exes]
  if outputs[0]!=outputs[1]:raise ValueError('Optimization mismatch '+str(argv))
  rows=[json.loads(line) for line in outputs[0].splitlines()]
  if rows[-1]['alive']:raise ValueError('Incomplete native life')
  cases.append(dict(kind=kind,doubles=bool(d),actor=a,target=t,yOffset=y,elevation=e,damage=damage,stop=stop,bg=[bgx,bgy],arguments=args,branch=label,rows=rows))
 layouts=[(0,0,1),(0,1,0),(1,2,3),(1,3,2)] if not full else [(d,a,t) for d in [0,1] for a in range(4 if d else 2) for t in range(4 if d else 2) if a!=t]
 for d,a,t in layouts:
  add(0,d,a,t,[1,5,-1]);add(2,d,a,t);add(3,d,a,t,stop=31)
  for args in [[-12,27,2,3],[8,28,3,4],[-4,30,2,3],[12,25,4,4],[-12,32,3,4],[8,31,2,2],[-4,28,2,3],[12,30,4,3]]:add(4,d,a,t,args)
 for damage in [0,1,32,33,65,66,100,2147483647]:add(1,0,0,1,damage=damage,label='damage-boundary')
 for selector in [0,2,3]:add(0,1,0,1,[selector,255,-32768],y=17,e=60,stop=1,label='partner-coordinate-byte-wrap-immediate-signal')
 add(3,0,1,0,stop=1,bgx=123,bgy=32768,label='signed-saved-scroll-and-immediate-stop')
 add(4,0,0,1,[32767,-32768,-32768,5],label='signed-data-position-and-product-boundaries')
 return dict(schema='firered-earth-backgrounds-source-v1',profile='full' if full else 'representative',sourceSha256=pins,optimizationParity=['O0','O2'],semantics='Pinned task and sprite callbacks plus original native sprite frame/affine scheduler. Creation row0 for tasks and row-1 for sprites. Source coordinate queries are explicit fixture inputs. BIOS matrix call is stubbed; native affine scale/rotation plus the shared matrix tests own matrix conversion. Every callback row includes retained args, task/sprite words, BG3 offsets/screen size, tile/END/affine state and raw holder destruction.',cases=cases)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--reference',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);p.add_argument('--full',action='store_true');a=p.parse_args()
 if a.output.exists():raise FileExistsError(a.output)
 out=a.build_dir or Path(tempfile.mkdtemp(prefix='earth-backgrounds-reference-'));out.mkdir(parents=True,exist_ok=True);r=build(a.root,a.reference or a.root/'content/raw/pokefirered/037335f4',out,a.full);a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(r,separators=(',',':'))+'\n');print(json.dumps(dict(cases=len(r['cases']),rows=sum(len(c['rows']) for c in r['cases']))))

#!/usr/bin/env python3
"""Original pinned Pain Split callbacks, shared frame interpreter and script timing.

Generated trajectories are local ignored data. This authored harness never runs
upstream scripts or derives expected output from managed implementation code.
"""
import argparse,hashlib,importlib.util,json,math,re,subprocess,tempfile
from pathlib import Path

def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def build(root,reference,out):
 helper=module('reference_c',root/'tests/Reference/reference_c.py');helper.verify_reference(reference)
 ex=module('source_extract',root/'tests/Reference/CallerPrelude/source_extract.py');engine=module('sprite_harness',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py')
 pins=json.loads((Path(__file__).parent/'pinned-inputs.json').read_text());s={p:(reference/p).read_text() for p in pins}
 for path,digest in pins.items():
  if hashlib.sha256((reference/path).read_bytes()).hexdigest()!=digest:raise ValueError('Pinned source changed: '+path)
 names={'src/battle_anim_effects_3.c':['AnimPainSplitProjectile','AnimTask_PainSplitMovement'],'src/battle_anim_mons.c':['GetBattlerSpriteFinal_Y','SetSpriteRotScale','ShouldRotScaleSpeciesBeFlipped','PrepareBattlerSpriteForRotScale','ResetSpriteRotScale','SetBattlerSpriteYOffsetFromYScale']}
 funcs=[ex.function(s[p],n) for p,ns in names.items() for n in ns]
 extra=r'''
#define ANIM_ATTACKER 0
#define ANIM_TARGET 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define B_SIDE_PLAYER 0
#define ST_OAM_OBJ_NORMAL 0
#define ST_OAM_AFFINE_DOUBLE 3
#define ST_OAM_AFFINE_NORMAL 1
#define MON_PIC_HEIGHT 64
#define DISPLAY_HEIGHT 160
#define IS_DOUBLE_BATTLE() doubles
static int doubles,yoff,elevation,hiddenMask;static u8 gBattleAnimAttacker,gBattleAnimTarget;static s16 gBattleAnimArgs[8];
static const struct {u8 x,y;} sBattlerCoords[2][4]={{{72,80},{176,40},{48,40},{112,80}},{{32,80},{200,40},{90,88},{152,32}}};
static int GetBattlerSide(u8 id){return id&1;}static u8 GetBattlerPosition(u8 id){return id;}
static u8 GetBattlerYDelta(u8 id,u16 species){return yoff;}static u8 GetBattlerElevation(u8 id,u16 species){return id&1?elevation:0;}
static u8 GetBattlerSpriteCoord(u8 id,u8 coord){return coord==2?sBattlerCoords[doubles][id].x:GetBattlerSpriteFinal_Y(id,1,TRUE);}
static u16 GetBattlerYDeltaFromSpriteId(u8 id){return yoff;}
static bool8 IsContest(void){return FALSE;}static bool8 IsBattlerSpriteVisible(u8 id){return !(hiddenMask&(1<<id));}
static u8 GetAnimBattlerSpriteId(u8 selector){return selector?gBattleAnimTarget:gBattleAnimAttacker;}
static void CalcCenterToCornerVec(struct Sprite*s,u8 shape,u8 size,u8 mode){}
static struct {struct {u8 matrixNum;} healthBoxesData[4];} battleSprites,*gBattleSpritesDataPtr=&battleSprites;
static struct OamMatrix gOamMatrices[32];static int lastX[4],lastY[4],lastRotation[4];
typedef void(*TaskFunc)(u8);static struct {s16 data[16];TaskFunc func;bool8 active;} gTasks[16];static int gAnimVisualTaskCount;
static void DestroyAnimVisualTask(u8 id){gTasks[id].active=FALSE;gAnimVisualTaskCount--;}
'''
 h=engine.sprite_harness(reference,funcs,extra)
 h=h.replace('#define MAX_SPRITES 1','#define MAX_SPRITES 64').replace('struct {u8 affineMode;int tileNum;} oam;','struct {u8 affineMode,matrixNum,objMode,shape,size;int tileNum;} oam; bool8 invisible;').replace('sAffineAnimStates[1]','sAffineAnimStates[32]')
 quarter=[int(math.sin(i*math.pi/128)*16384) for i in range(65)];quarter[0]=0;quarter[64]=16384
 bios='static const s16 quarter[]={'+','.join(map(str,quarter))+'};\n'+r'''
static s16 biosSine(u8 a){int q=a>>6,i=a&63,v=quarter[q&1?64-i:i];return q<2?v:-v;}
static void ObjAffineSet(const struct ObjAffineSrcData*s,struct OamMatrix*d,int n,int stride){assert(n==1&&stride==2);u8 a=s->rotation>>8;int sn=biosSine(a),cs=biosSine(a+64);*d=(struct OamMatrix){(s->xScale*cs)>>14,-((s->xScale*sn)>>14),(s->yScale*sn)>>14,(s->yScale*cs)>>14};}
'''
 h=re.sub(r'static void ObjAffineSet\([^\n]+',lambda m:bios.rstrip(),h,count=1)
 h=h.replace('static void DestroyAnimSprite(struct Sprite*s){s->inUse=FALSE;}','static int gAnimVisualTaskCount;\nstatic void DestroyAnimSprite(struct Sprite*s){s->inUse=FALSE;gAnimVisualTaskCount--;}')
 h=h.replace('static u8 GetSpriteMatrixNum(struct Sprite*s){return 0;}','static u8 GetSpriteMatrixNum(struct Sprite*s){return s->oam.matrixNum;}')
 h=h.replace('    src.xScale = xScale;','    lastX[spriteId]=xScale;lastY[spriteId]=yScale;lastRotation[spriteId]=rotation;\n    src.xScale = xScale;')
 h+=ex.array(s['src/battle_anim_effects_3.c'],'sPainSplitAnimCmds')+ex.array(s['src/battle_anim_effects_3.c'],'sPainSplitAnimCmdTable')+r'''
static void init(void){for(int i=0;i<4;i++){gSprites[i].data[0]=i;gSprites[i].oam.matrixNum=i;gSprites[i].invisible=!!(hiddenMask&(1<<i));gOamMatrices[i]=(struct OamMatrix){256,0,0,256};battleSprites.healthBoxesData[i].matrixNum=i;lastX[i]=lastY[i]=256;}}
static void makeSprite(int slot){struct Sprite*z=&gSprites[slot];memset(z,0,sizeof(*z));z->inUse=TRUE;z->usingSheet=TRUE;z->animBeginning=TRUE;z->anims=sPainSplitAnimCmdTable;z->callback=AnimPainSplitProjectile;z->x=GetBattlerSpriteCoord(gBattleAnimTarget,2);z->y=GetBattlerSpriteCoord(gBattleAnimTarget,3);z->callback(z);AnimateSprite(z);gAnimVisualTaskCount++;}
static void makeTask(int slot){memset(&gTasks[slot],0,sizeof(gTasks[slot]));gTasks[slot].active=TRUE;gTasks[slot].func=AnimTask_PainSplitMovement;AnimTask_PainSplitMovement(slot);gAnimVisualTaskCount++;}
'''
 primitive=h+r'''
int main(int argc,char**argv){assert(argc==17);int kind=atoi(argv[1]);doubles=atoi(argv[2]);gBattleAnimAttacker=atoi(argv[3]);gBattleAnimTarget=atoi(argv[4]);yoff=atoi(argv[5]);elevation=atoi(argv[6]);hiddenMask=atoi(argv[7]);for(int i=0;i<8;i++)gBattleAnimArgs[i]=atoi(argv[8+i]);int initialOffset=atoi(argv[16]);init();int selected=gBattleAnimArgs[0]?gBattleAnimTarget:gBattleAnimAttacker;if(kind){gSprites[selected].x2=initialOffset;gSprites[selected].y2=-initialOffset;makeTask(0);}else makeSprite(4);
 for(int frame=0;frame<100;frame++){
  if(frame){if(kind)AnimTask_PainSplitMovement(0);else AnimateSprites();}
  if(kind){struct Sprite*z=&gSprites[selected];struct OamMatrix*m=&gOamMatrices[selected];printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",frame,gTasks[0].active,selected,z->x2,z->y2,z->oam.affineMode,z->invisible,lastX[selected],lastY[selected],lastRotation[selected],m->a,m->b,m->c,m->d,gTasks[0].data[1],gTasks[0].data[2],gTasks[0].data[0]);}
  else{struct Sprite*z=&gSprites[4];printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",frame,z->inUse,z->x,z->y,z->x2,z->y2,z->oam.tileNum,z->animEnded,z->animDelayCounter,z->data[0],z->data[1],z->data[2],z->data[3],z->data[4]);}
  for(int i=0;i<8;i++)printf(",%d",gBattleAnimArgs[i]);puts("");if(kind?!gTasks[0].active:!gSprites[4].inUse)return 0;
 }abort();}
'''
 def run(name,code,inputs):
  path=out/(name+'.c');path.write_text(code);exes=[]
  for opt in ['O0','O2']:
   exe=out/(name+'-'+opt);helper.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(path),'-o',str(exe)]);exes.append(exe)
  results=[]
  for args in inputs:
   values=[subprocess.check_output([str(e),*map(str,args)],text=True) for e in exes]
   if values[0]!=values[1]:raise ValueError('Native optimization parity differs: '+str(args))
   results.append([list(map(int,r.split(','))) for r in values[0].splitlines()])
  return results
 cases=[]
 def case(kind,d,a,t,args,y=8,e=0,hidden=0,offset=0,label='authored'):
  cases.append(dict(kind=kind,doubles=bool(d),actor=a,target=t,yOffset=y,elevation=e,hiddenMask=hidden,initialOffset=offset,arguments=args+[2001+i for i in range(8-len(args))],label=label))
 for d,a,t in [(0,0,1),(0,1,0),(1,0,3),(1,1,2),(1,2,1),(1,3,0)]:
  for selector in [0,1]:
   for x in [-8,-24,8]:case(0,d,a,t,[x,-42,selector])
   for mode in range(3):case(1,d,a,t,[selector,mode])
 for a,t in [(0,1),(1,0),(2,3),(3,2)]:
  for y in [-32768,32767,-1,0,1]:case(0,1,a,t,[32767,y,a&1],y=20,e=48,label='signed16-offset-and-bounce')
  for mode in range(3):case(1,1,a,t,[1,mode],y=20,e=48,hidden=1<<t,offset=5,label='hidden-battler-existing-offset')
 for c,rows in zip(cases,run('pain-split',primitive,[[c['kind'],int(c['doubles']),c['actor'],c['target'],c['yOffset'],c['elevation'],c['hiddenMask'],*c['arguments'],c['initialOffset']] for c in cases])):c['rows']=rows
 schedule_names=['RunAnimScriptCommand','WaitAnimFrameCount','Cmd_loadspritegfx','Cmd_delay','Cmd_waitforvisualfinish'];schedule_funcs=[ex.function(s['src/battle_anim.c'],n) for n in schedule_names]
 source=s['data/battle_anim_scripts.s'];match=re.search(r'^Move_PAIN_SPLIT:\n(.*?)(?=^\w+:)',source,re.M|re.S);assert match
 commands=[]
 for line in match[1].splitlines():
  line=line.strip()
  if line:op,_,args=line.partition(' ');commands.append((op,[a.strip() for a in args.split(',')] if args else []))
 data=[];symbols={'ANIM_ATTACKER':0,'ANIM_TARGET':1}
 def word(n):return [n&255,(n>>8)&255]
 for op,args in commands:
  if op=='loadspritegfx':data.extend([0,0,0])
  elif op=='createsprite':assert args[0]=='gPainSplitProjectileSpriteTemplate';data.extend([2,*word(int(args[3])),*word(int(args[4])),symbols[args[5]]])
  elif op=='createvisualtask':assert args[0]=='AnimTask_PainSplitMovement';data.extend([3,symbols[args[2]],int(args[3])])
  elif op=='delay':data.extend([4,int(args[0])])
  elif op=='waitforvisualfinish':data.append(5)
  elif op=='playsewithpan':assert args==['SE_M_SWAGGER2','0'];data.append(6)
  elif op=='end':data.append(8)
  else:raise ValueError(op)
 schedule=h+r'''
static const u8*sBattleAnimScriptPtr;static s8 sAnimFramesToWait;static int gAnimScriptActive=1,frame;static void(*gAnimScriptCallback)(void);
#define T1_READ_16(p) ((u16)((p)[0]|((p)[1]<<8)))
#define GET_TRUE_SPRITE_INDEX(x) 0
static const int gBattleAnimPicTable[1],gBattleAnimPaletteTable[1];static void LoadCompressedSpriteSheetUsingHeap(const int*p){}static void LoadCompressedSpritePaletteUsingHeap(const int*p){}static void AddSpriteIndex(u16 i){}
'''+ '\n'.join(f[:f.index('{')].strip()+';' for f in schedule_funcs)+r'''
static void Sprite(void){sBattleAnimScriptPtr++;gBattleAnimArgs[0]=T1_READ_16(sBattleAnimScriptPtr);sBattleAnimScriptPtr+=2;gBattleAnimArgs[1]=T1_READ_16(sBattleAnimScriptPtr);sBattleAnimScriptPtr+=2;gBattleAnimArgs[2]=*sBattleAnimScriptPtr++;int id;for(id=4;id<64&&gSprites[id].inUse;id++);assert(id<64);makeSprite(id);printf("%d,0,%d,%d\n",frame,id,gBattleAnimArgs[2]);}
static void Task(void){sBattleAnimScriptPtr++;gBattleAnimArgs[0]=*sBattleAnimScriptPtr++;gBattleAnimArgs[1]=*sBattleAnimScriptPtr++;int id;for(id=0;id<16&&gTasks[id].active;id++);assert(id<16);makeTask(id);printf("%d,1,%d,%d\n",frame,gBattleAnimArgs[0],gBattleAnimArgs[1]);}
static void Sound(void){sBattleAnimScriptPtr++;printf("%d,2,187,0\n",frame);}
static void End(void){if(gAnimVisualTaskCount){sAnimFramesToWait=1;return;}gAnimScriptActive=0;printf("%d,3,0,0\n",frame);}
static void(*const sScriptCmdTable[9])(void)={[0]=Cmd_loadspritegfx,[2]=Sprite,[3]=Task,[4]=Cmd_delay,[5]=Cmd_waitforvisualfinish,[6]=Sound,[8]=End};
'''+ '\n'.join(schedule_funcs)+'\nstatic const u8 script[]={'+','.join(map(str,data))+'};'+r'''
int main(void){doubles=1;gBattleAnimAttacker=0;gBattleAnimTarget=1;yoff=8;init();sBattleAnimScriptPtr=script;gAnimScriptCallback=RunAnimScriptCommand;for(frame=0;gAnimScriptActive&&frame<600;frame++){gAnimScriptCallback();AnimateSprites();for(int i=0;i<16;i++)if(gTasks[i].active)gTasks[i].func(i);}assert(!gAnimScriptActive);return 0;}
'''
 schedule_rows=run('pain-split-schedule',schedule,[[]])[0]
 return dict(schema='firered-pain-split-source-v1',sourceSha256=pins,optimizationParity=['O0','O2'],sourceFunctions=names,scheduleFunctions=schedule_names,cases=cases,schedule=schedule_rows,semantics='Primitive frame0 is original initializer plus AnimateSprite for projectiles, initializer only for tasks. Later frames execute original callbacks and sprite frame interpreter. Original native rot-scale and Y-offset helpers use an independently generated Q1.14 BIOS sine boundary. Native matrix-call inputs are retained separately. Picture coordinates use original GetBattlerSpriteFinal_Y. Destructor retains inactive fields solely for inspection. Root timing extracts original load/delay/wait handlers; narrow pointer-free wrappers bind this root ABI. End gates actual visual count with no playing audio. Every expected trajectory comes from original C, never managed runtime.')

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--reference',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);a=p.parse_args()
 if a.output.exists():raise FileExistsError('Refusing to overwrite reviewed fixture: '+str(a.output))
 out=a.build_dir or Path(tempfile.mkdtemp(prefix='pain-split-reference-'));out.mkdir(exist_ok=True,parents=True);r=build(a.root.resolve(),a.reference or a.root/'content/raw/pokefirered/037335f4',out);a.output.parent.mkdir(exist_ok=True,parents=True);a.output.write_text(json.dumps(r,separators=(',',':'))+'\n');print(json.dumps(dict(cases=len(r['cases']),rows=sum(len(c['rows']) for c in r['cases']),schedule=r['schedule'])))

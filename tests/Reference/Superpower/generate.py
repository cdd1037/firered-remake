#!/usr/bin/env python3
"""Extract pinned Superpower callbacks and native frame/affine/linear players.
Generated trajectories are explicit local evidence; ordinary tests never run C.
"""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile
from pathlib import Path

def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def build(root,reference,out,importer=None):
 helper=module('reference_c',root/'tests/Reference/reference_c.py');helper.verify_reference(reference)
 ex=module('source_extract',root/'tests/Reference/CallerPrelude/source_extract.py');engine=module('sprite_harness',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py')
 pins=json.loads((Path(__file__).parent/'pinned-inputs.json').read_text());s={p:(reference/p).read_text() for p in pins if p.endswith(('.c','.h','.s'))}
 for path,digest in pins.items():
  if hashlib.sha256((reference/path).read_bytes()).hexdigest()!=digest:raise ValueError('Pinned source changed: '+path)
 names={'src/battle_anim_fight.c':['AnimSuperpowerOrb','AnimSuperpowerOrb_Step','AnimSuperpowerRock','AnimSuperpowerRock_Step1','AnimSuperpowerRock_Step2','AnimSuperpowerFireball'],'src/battle_anim_mons.c':['GetBattlerSpriteFinal_Y','InitAnimLinearTranslation','AnimTranslateLinear','AnimTranslateLinear_WithFollowup']}
 funcs=[ex.function(s[p],n) for p,ns in names.items() for n in ns]
 # The source carries a 32-bit integer through two pointer halves. On a 64-bit
 # oracle host, preserve the ARM integer exactly rather than execute pointer UB.
 funcs=[f.replace('void *var0;','u32 var0;').replace('(void *)(sprite->y << 8)','(u32)(sprite->y << 8)').replace('((intptr_t)var0)', '((s32)var0)').replace('(void *)(((s32)var0) >> 8)','(u32)(((s32)var0) >> 8)') for f in funcs]
 extra='\n'.join(line for line in s['include/battle_anim.h'].splitlines() if line.startswith('#define sTransl_'))+r'''
#define ANIM_ATTACKER 0
#define ANIM_TARGET 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define B_SIDE_PLAYER 0
#define MON_PIC_HEIGHT 64
#define DISPLAY_HEIGHT 160
#define IS_DOUBLE_BATTLE() doubles
#define ST_OAM_HFLIP 8
#define ST_OAM_VFLIP 16
#define REG_OFFSET_BLDCNT 0
static int doubles,yoff,elevation;static u8 gBattleAnimAttacker,gBattleAnimTarget,gBattlerAttacker;static s16 gBattleAnimArgs[8];static int blend=1,clearFrame=-1,frame;
static const struct {u8 x,y;} sBattlerCoords[2][4]={{{72,80},{176,40},{48,40},{112,80}},{{32,80},{200,40},{90,88},{152,32}}};
static int GetBattlerSide(u8 id){return id&1;}static u8 GetBattlerPosition(u8 id){return id;}
static u8 GetBattlerYDelta(u8 id,u16 species){return yoff;}static u8 GetBattlerElevation(u8 id,u16 species){return id&1?elevation:0;}
static u8 GetBattlerSpriteCoord(u8 id,u8 coord){return coord==2?sBattlerCoords[doubles][id].x:GetBattlerSpriteFinal_Y(id,1,TRUE);}
static u8 GetBattlerSpriteBGPriority(u8 id){return id==0||id==3?1:0;}
static bool8 IsContest(void){return FALSE;}
static void SetGpuReg(int reg,int value){assert(reg==0&&value==0);blend=0;clearFrame=frame;}
static void DestroySpriteAndMatrix(struct Sprite*s){DestroyAnimSprite(s);}
static void StorePointerInVars(s16*lo,s16*hi,u32 value){*lo=value;*hi=value>>16;}
static u32 LoadPointerFromVars(s16 lo,s16 hi){return (u16)lo|((u32)(u16)hi<<16);}
'''
 h=engine.sprite_harness(reference,funcs,extra)
 h=h.replace('struct {u8 affineMode;int tileNum;} oam;','struct {u8 affineMode,matrixNum,priority;int tileNum;} oam;')
 # Native dummy frame END retains explicit callback flip bits. The engine's
 # generic helper is observable for other recipes but these have dummy frames.
 h=h.replace('s->hFlip=h;s->vFlip=v;','s->hFlip=h;s->vFlip=v;s->oam.matrixNum=(h?8:0)|(v?16:0);')
 h+=ex.array(s['src/battle_anim_fight.c'],'sAffineAnim_SuperpowerOrb')+r'''
static const union AnimCmd dummy[]={{.type=-1}};static const union AnimCmd*const frames[]={dummy};static const union AffineAnimCmd*const affine[]={sAffineAnim_SuperpowerOrb};
static void init(int kind,int initial){struct Sprite*z=&gSprites[0];memset(z,0,sizeof(*z));AffineAnimStateReset(0);z->inUse=TRUE;z->usingSheet=TRUE;z->animBeginning=TRUE;z->anims=frames;z->oam.priority=2;z->x=GetBattlerSpriteCoord(initial,2);z->y=GetBattlerSpriteCoord(initial,3);if(kind==0){z->oam.affineMode=3;z->affineAnimBeginning=TRUE;z->affineAnims=affine;}z->callback=kind==0?AnimSuperpowerOrb:kind==1?AnimSuperpowerRock:AnimSuperpowerFireball;z->callback(z);AnimateSprite(z);}
int main(int argc,char**argv){assert(argc==16);int kind=atoi(argv[1]);doubles=atoi(argv[2]);gBattleAnimAttacker=gBattlerAttacker=atoi(argv[3]);gBattleAnimTarget=atoi(argv[4]);yoff=atoi(argv[5]);elevation=atoi(argv[6]);int initial=atoi(argv[7]);for(int i=0;i<8;i++)gBattleAnimArgs[i]=atoi(argv[8+i]);init(kind,initial);
 for(frame=0;frame<1800;frame++){if(frame)AnimateSprites();struct Sprite*z=&gSprites[0];struct AffineAnimState*a=&sAffineAnimStates[0];printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",frame,z->inUse,z->x,z->y,z->x2,z->y2,z->oam.tileNum,z->oam.priority,!!(z->oam.matrixNum&8),!!(z->oam.matrixNum&16),a->xScale,a->yScale,a->rotation,z->affineAnimEnded,blend,clearFrame);for(int i=0;i<8;i++)printf(",%d",z->data[i]);for(int i=0;i<8;i++)printf(",%d",gBattleAnimArgs[i]);puts("");if(!z->inUse)return 0;}abort();}
'''
 path=out/'superpower.c';path.write_text(h);exes=[]
 for opt in ['O0','O2']:
  exe=out/('superpower-'+opt);helper.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(path),'-o',str(exe)]);exes.append(exe)
 cases=[]
 def case(kind,d,a,t,args,y=8,e=0,initial=None,label='authored'):
  cases.append(dict(kind=kind,doubles=bool(d),actor=a,target=t,yOffset=y,elevation=e,initial=t if initial is None else initial,arguments=args+[2001+i for i in range(8-len(args))],label=label))
 for d,a,t in [(0,0,1),(0,1,0),(1,0,3),(1,1,2),(1,2,1),(1,3,0)]:
  for selector in [0,1]:
   case(0,d,a,t,[selector]);case(2,d,a,t,[selector])
  for args in [[200,96,1,120],[20,248,4,112],[130,160,2,104],[160,192,0,96],[60,288,3,88]]:case(1,d,a,t,args)
 for a,t in [(0,1),(1,0),(2,3),(3,2)]:
  for args in [[-32768,32767,0,1],[32767,1,4,0],[-8,32767,2,1],[248,1,3,1],[100,32767,1,2],[200,1,0,0]]:case(1,1,a,t,args,y=32,e=64,label='source-native-width')
  for kind in [0,2]:case(kind,1,a,t,[0],y=32,e=64,label='source-y-wrap-and-clamp')
 for c in cases:
  args=[c['kind'],int(c['doubles']),c['actor'],c['target'],c['yOffset'],c['elevation'],c['initial'],*c['arguments']]
  outputs=[subprocess.check_output([str(exe),*map(str,args)],text=True) for exe in exes]
  if outputs[0]!=outputs[1]:raise ValueError('Native optimization parity differs: '+str(args))
  c['rows']=[list(map(int,row.split(','))) for row in outputs[0].splitlines()]
 # The maintained importer alone parses the source script. A staged importer is
 # accepted only for explicit development before parent integration.
 canonical=root/'scripts/imports/battle/import-source-physical-animations.py'
 namespace={'__name__':'superpower_schedule_import','__file__':str(canonical)}
 exec(compile((importer or canonical).read_text(),str(importer or canonical),'exec'),namespace)
 pack,_=namespace['build'](reference);program=next(r['commands'] for r in json.loads(pack)['scripts'] if r['label']=='Move_SUPERPOWER')
 schedule_names=['RunAnimScriptCommand','WaitAnimFrameCount','Cmd_loadspritegfx','Cmd_delay']
 schedule_functions=[ex.function((reference/'src/battle_anim.c').read_text(),n) for n in schedule_names]
 data=[];template_ids={'gSuperpowerOrbSpriteTemplate':0,'gSuperpowerRockSpriteTemplate':1,'gSuperpowerFireballSpriteTemplate':2,'gShakeMonOrTerrainSpriteTemplate':-1}
 for c in program:
  op,args=c['op'],c['args']
  if op=='loadspritegfx':data.extend([0,0,0])
  elif op=='delay':data.extend([1,int(args[0])])
  elif op=='createsprite':data.extend([2,template_ids[args[0]]&255])
  elif op=='playsewithpan' or op=='createvisualtask' and args[0]=='SoundTask_PlaySE2WithPanning':data.append(3)
  elif op=='waitforvisualfinish':data.append(4);break
  else:data.append(5)
 schedule=r"""
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
typedef uint8_t u8;typedef uint16_t u16;typedef int8_t s8;typedef int16_t s16;
#define TRUE 1
#define FALSE 0
#define T1_READ_16(p) ((u16)((p)[0]|((p)[1]<<8)))
#define GET_TRUE_SPRITE_INDEX(x) 0
static const int gBattleAnimPicTable[1],gBattleAnimPaletteTable[1];static void LoadCompressedSpriteSheetUsingHeap(const int*p){}static void LoadCompressedSpritePaletteUsingHeap(const int*p){}static void AddSpriteIndex(u16 n){}
static const u8*sBattleAnimScriptPtr;static s8 sAnimFramesToWait;static int gAnimScriptActive=1,frame;static void(*gAnimScriptCallback)(void);
"""+'\n'.join(f[:f.index('{')].strip()+';' for f in schedule_functions)+r"""
static void Sprite(void){printf("%d,0,%d\n",frame,(s8)sBattleAnimScriptPtr[1]);sBattleAnimScriptPtr+=2;}
static void Sound(void){printf("%d,1,0\n",frame);sBattleAnimScriptPtr++;}
static void Wait(void){printf("%d,2,0\n",frame);gAnimScriptActive=0;}
static void Noop(void){sBattleAnimScriptPtr++;}
static void(*const sScriptCmdTable[])(void)={Cmd_loadspritegfx,Cmd_delay,Sprite,Sound,Wait,Noop};
"""+'\n'.join(schedule_functions)+'\nstatic const u8 script[]={'+','.join(map(str,data))+'};'+r"""
int main(void){sBattleAnimScriptPtr=script;gAnimScriptCallback=RunAnimScriptCommand;for(frame=0;gAnimScriptActive&&frame<1000;frame++)gAnimScriptCallback();assert(!gAnimScriptActive);return 0;}
"""
 path=out/'superpower-schedule.c';path.write_text(schedule);outputs=[]
 for opt in ['O0','O2']:
  exe=out/('superpower-schedule-'+opt);helper.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(path),'-o',str(exe)]);outputs.append(subprocess.check_output([str(exe)],text=True))
 if outputs[0]!=outputs[1]:raise ValueError('Native schedule optimization parity differs')
 schedule_rows=[list(map(int,line.split(','))) for line in outputs[0].splitlines()]
 return dict(schedule=schedule_rows,scheduleFunctions=schedule_names,scheduleSemantics='Original load/delay scheduler; non-yielding commands emit events or are no-ops. Stops at the final visual wait, after all authored births and sounds. Primitive vectors prove sprite callbacks separately; managed complete-root test independently verifies actual drain and restoration.',schema='firered-superpower-source-v1',sourceSha256=pins,optimizationParity=['O0','O2'],sourceFunctions=names,semantics='Frame0 invokes original initializer then original frame/affine engine. Later rows run original AnimateSprites. ARM32 split pointer fixed accumulator is represented as u32, and its intptr_t shift as s32; no host pointer arithmetic is used. Original callback branches and arithmetic are otherwise retained. Platform coordinates use original GetBattlerSpriteFinal_Y plus explicit dimensions. Stored callbacks are portable pointers. Native dummy END frame is rendered as tile0; managed shared dummy loop has equivalent pixels. Compare lifetime always, sprite state only while alive; destructor retains dead memory only for observation. No trajectories derive from managed code.',cases=cases)

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--reference',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);p.add_argument('--importer',type=Path);a=p.parse_args()
 if a.output.exists():raise FileExistsError('Refusing to overwrite reviewed fixture: '+str(a.output))
 out=a.build_dir or Path(tempfile.mkdtemp(prefix='superpower-reference-'));out.mkdir(exist_ok=True,parents=True);r=build(a.root.resolve(),a.reference or a.root/'content/raw/pokefirered/037335f4',out,a.importer);a.output.parent.mkdir(exist_ok=True,parents=True);a.output.write_text(json.dumps(r,separators=(',',':'))+'\n');print(json.dumps(dict(cases=len(r['cases']),rows=sum(len(c['rows']) for c in r['cases']))))

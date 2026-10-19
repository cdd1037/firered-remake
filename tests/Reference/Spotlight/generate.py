#!/usr/bin/env python3
"""Extract pinned spotlight/clapping/confetti callbacks and hardware palette engine.

Original C executes under both O0/O2. Shared bounded source extraction and sprite
harness are reused. No expected trajectory is produced by managed playback.
"""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile,types
from pathlib import Path

def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def build(root,reference,out,importer=None):
 helper=module('reference_c',root/'tests/Reference/reference_c.py');helper.verify_reference(reference)
 ex=module('source_extract',root/'tests/Reference/source_extract.py');engine=module('sprite_harness',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py')
 pins=json.loads((Path(__file__).parent/'pinned-inputs.json').read_text());s={p:(reference/p).read_text() for p in pins}
 for path,digest in pins.items():
  if hashlib.sha256((reference/path).read_bytes()).hexdigest()!=digest:raise ValueError('Pinned source changed: '+path)
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
 palette_names=['BeginHardwarePaletteFade','UpdateHardwarePaletteFade','UpdateBlendRegisters','TransferPlttBuffer']
 pfuncs=[ex.function(s['src/palette.c'],n) for n in palette_names]
 ph=s['include/palette.h'];start=ph.index('struct PaletteFadeControl\n');end=ph.index('\n};',start)+3
 pcode=r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8;typedef uint16_t u16;typedef uint32_t u32;typedef u8 bool8;
#define TRUE 1
#define FALSE 0
#define PLTT 0
#define PLTT_SIZE 1024
static u16 gPlttBufferFaded[512],registers[128];static bool8 sPlttBufferTransferPending;
static void DmaCopy16(int channel,void*src,void*dest,int size){assert(channel==3&&size==1024);}
static void SetGpuReg(u16 id,u16 value){registers[id/2]=value;}
'''+ph[start:end]+ '\nstatic struct PaletteFadeControl gPaletteFade;\n'+ex.enum_containing(s['src/palette.c'],'HARDWARE_FADE')+ex.macros([ph,s['include/gba/io_reg.h']],['gPaletteFade_blendCnt','gPaletteFade_delay','PALETTE_FADE_STATUS_DONE','PALETTE_FADE_STATUS_ACTIVE','PALETTE_FADE_STATUS_DELAY','REG_OFFSET_BLDCNT','REG_OFFSET_BLDY'])+'\n'.join(f[:f.index('{')].strip()+';' for f in pfuncs)+'\n'+'\n'.join(pfuncs)+r'''
static void row(int frame,int phase,int status){printf("%d,%d,%d,%d,%d,%d,%d,%d,%u,%d,%d,%d,%d\n",frame,phase,status,gPaletteFade.active,gPaletteFade.delayCounter,gPaletteFade.y,gPaletteFade.targetY,gPaletteFade.hardwareFadeFinishing,gPaletteFade_blendCnt,gPaletteFade.mode,gPaletteFade.shouldResetBlendRegisters,registers[REG_OFFSET_BLDCNT/2],registers[REG_OFFSET_BLDY/2]);}
int main(int argc,char**argv){assert(argc==6);BeginHardwarePaletteFade(atoi(argv[1]),atoi(argv[2]),atoi(argv[3]),atoi(argv[4]),atoi(argv[5]));row(0,0,-1);TransferPlttBuffer();row(0,2,-1);for(int frame=1;frame<2000;frame++){int status=UpdateHardwarePaletteFade();row(frame,1,status);TransferPlttBuffer();row(frame,2,status);if(!gPaletteFade.active)return 0;}abort();}
'''
 hardware=[]
 for values in [[248,3,0,10,0],[248,3,10,0,1],[248,0,0,0,0],[248,0,10,10,1],[184,0,0,16,0],[248,64,0,1,0],[248,255,1,0,1]]:hardware.append(dict(arguments=values))
 for c,rows in zip(hardware,run('hardware-fade',pcode,[c['arguments'] for c in hardware])):c['rows']=rows
 # Original callbacks and sprite frame/affine interpreter. The shared harness is
 # augmented only with fields and platform boundaries used by this family.
 names={'src/battle_anim_effects_3.c':['AnimSpotlight','AnimSpotlight_Step1','AnimSpotlight_Step2','AnimClappingHand','AnimClappingHand_Step','AnimClappingHand2','AnimFlatterConfetti','AnimFlatterConfetti_Step','AnimFlatterSpotlight','AnimFlatterSpotlight_Step','AnimTask_CreateSpotlight','AnimTask_RemoveSpotlight'],'src/battle_anim_mons.c':['SetAnimSpriteInitialXOffset','InitSpritePosToAnimTarget'],'src/sprite.c':['ChangeSpriteAffineAnim'],'src/random.c':['Random'],'src/battle_anim.c':['BattleAnimAdjustPanning']}
 funcs=[ex.function(s[p],n) for p,ns in names.items() for n in ns]
 macro_names=['REG_OFFSET_WINOUT','REG_OFFSET_WININ','REG_OFFSET_WIN0H','REG_OFFSET_WIN0V','REG_OFFSET_WIN1H','REG_OFFSET_WIN1V','REG_OFFSET_DISPCNT','DISPCNT_OBJWIN_ON','DISPCNT_WIN1_ON','WINOUT_WIN01_BG_ALL','WINOUT_WIN01_OBJ','WINOUT_WIN01_CLR','WINOUT_WINOBJ_BG_ALL','WINOUT_WINOBJ_OBJ','WINOUT_WINOBJ_CLR','WININ_WIN0_BG_ALL','WININ_WIN0_OBJ','WININ_WIN0_CLR','WININ_WIN1_BG_ALL','WININ_WIN1_OBJ','WININ_WIN1_CLR','WIN_RANGE','ST_OAM_OBJ_WINDOW','ST_OAM_HFLIP','ISO_RANDOMIZE1']
 extra=ex.macros([s['include/gba/io_reg.h'],s['include/gba/types.h'],s['include/random.h']],macro_names)+r'''
#define DISPLAY_WIDTH 240
#define DISPLAY_HEIGHT 160
#define B_SIDE_PLAYER 0
#define B_SIDE_OPPONENT 1
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define ANIM_ATTACKER 0
#define SOUND_PAN_ATTACKER -64
#define SOUND_PAN_TARGET 63
#define SE_M_ENCORE 215
static u8 gBattleAnimAttacker,gBattleAnimTarget;static s16 gBattleAnimArgs[8];static u32 gRngValue;static u16 regs[128],gBattle_WIN0H,gBattle_WIN0V,gBattle_WIN1H,gBattle_WIN1V;
static int coords[4][2]={{32,80},{200,40},{90,88},{152,32}},sounds,pan,taskDead;
static struct {struct {u8 statusAnimActive;} healthBoxesData[4];} storage,*gBattleSpritesDataPtr=&storage;
static int GetBattlerSide(u8 id){return id&1;}
static int GetBattlerSpriteCoord(u8 id,u8 coord){return coords[id][coord];}
static int GetBattlerSpriteCoord2(u8 id,u8 coord){return coords[id][coord];}
static bool8 IsContest(void){return FALSE;}
static void SetGpuReg(u16 id,u16 value){regs[id/2]=value;}
static u16 GetGpuReg(u16 id){return regs[id/2];}
static void SetGpuRegBits(u16 id,u16 value){regs[id/2]|=value;}
static void ClearGpuRegBits(u16 id,u16 value){regs[id/2]&=~value;}
static void DestroyAnimVisualTask(u8 id){taskDead++;}
static void PlaySE1WithPanning(u16 song,s8 value){assert(song==215);sounds++;pan=value;}
'''
 h=engine.sprite_harness(reference,funcs,extra)
 h=h.replace('bool8 inUse,animEnded','bool8 invisible;bool8 inUse,animEnded').replace('struct {u8 affineMode;int tileNum;} oam;','struct {u8 affineMode,objMode,matrixNum;int tileNum;} oam;')
 h=h.replace('#define SAFE_DIV(a,b) (assert((b)!=0),(a)/(b))','#define SAFE_DIV(a,b) ((b)?(a)/(b):0)')
 h+='\n'.join(ex.array(s['src/battle_anim_effects_3.c'],n) for n in ['sSpotlightAffineAnimCmds1','sSpotlightAffineAnimCmds2','sSpotlightAffineAnimTable'])+r'''
static const union AnimCmd dummy[]={{.type=-1}};static const union AnimCmd *const anims[]={dummy};
int main(int argc,char**argv){assert(argc==14);int kind=atoi(argv[1]);gBattleAnimAttacker=atoi(argv[2]);gBattleAnimTarget=atoi(argv[3]);gRngValue=strtoul(argv[4],0,10);for(int i=0;i<8;i++)gBattleAnimArgs[i]=atoi(argv[i+5]);int task=atoi(argv[13]);
 if(task){AnimTask_CreateSpotlight(0);printf("%d,%d,%d,%d,%d,%d,%d\n",taskDead,regs[REG_OFFSET_DISPCNT/2],regs[REG_OFFSET_WININ/2],regs[REG_OFFSET_WIN1H/2],regs[REG_OFFSET_WIN1V/2],gBattle_WIN1H,gBattle_WIN1V);AnimTask_RemoveSpotlight(0);printf("%d,%d,%d,%d,%d,%d,%d\n",taskDead,regs[REG_OFFSET_DISPCNT/2],regs[REG_OFFSET_WININ/2],regs[REG_OFFSET_WIN1H/2],regs[REG_OFFSET_WIN1V/2],gBattle_WIN1H,gBattle_WIN1V);return 0;}
 struct Sprite*s=&gSprites[0];memset(s,0,sizeof(*s));AffineAnimStateReset(0);s->inUse=TRUE;s->usingSheet=TRUE;s->animBeginning=TRUE;s->anims=anims;s->x=coords[gBattleAnimTarget][0];s->y=coords[gBattleAnimTarget][1]+((gBattleAnimTarget&1)?0:8);
 SpriteCallback callbacks[]={AnimSpotlight,AnimFlatterSpotlight,AnimClappingHand,AnimClappingHand2,AnimFlatterConfetti};s->callback=callbacks[kind];if(kind<2){s->oam.affineMode=3;s->affineAnimBeginning=TRUE;s->affineAnims=sSpotlightAffineAnimTable;}
 for(int frame=0;frame<600;frame++){AnimateSprites();struct AffineAnimState*a=&sAffineAnimStates[0];printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%d,%d\n",frame,s->inUse,s->x,s->y,s->x2,s->y2,s->oam.tileNum,s->oam.matrixNum==ST_OAM_HFLIP,s->invisible,s->oam.objMode,a->xScale,a->yScale,a->rotation,s->affineAnimEnded,sounds,gRngValue,regs[REG_OFFSET_DISPCNT/2],regs[REG_OFFSET_WINOUT/2]);if(!s->inUse)return 0;}abort();}
'''
 cases=[]
 for actor in range(4):
  for kind,args in [(0,[0,-8]),(1,[0,-8,80]),(1,[7,12,1]),(2,[-2,0,0,0,9]),(2,[2,0,1,0,9]),(3,[-2,0,0,0,9]),(3,[2,0,1,0,9]),(2,[10,-9,0,1,1]),(4,[0]),(4,[1])]:cases.append(dict(kind=kind,actor=actor,target=actor^1,seed=0x12345678,arguments=args+[1000+i for i in range(8-len(args))]))
 for seed in [0,1,0xffffffff]:
  for selector in [0,1]:cases.append(dict(kind=4,actor=0,target=1,seed=seed,arguments=[selector]+[0]*7))
 for c,rows in zip(cases,run('spotlight',h,[[c['kind'],c['actor'],c['target'],c['seed'],*c['arguments'],0] for c in cases])):c['rows']=rows
 burst=[];seed=0x12345678
 for i in range(38):
  args=[4,0,1,seed,i&1,*([0]*7),0];outputs=[subprocess.check_output([str(out/('spotlight-'+opt)),*map(str,args)],text=True) for opt in ['O0','O2']]
  if outputs[0]!=outputs[1]:raise ValueError('Confetti burst native parity differs')
  rows=[list(map(int,line.split(','))) for line in outputs[0].splitlines()];burst.append(dict(seed=seed,selector=i&1,rows=rows));seed=rows[0][15]
 tasks=run('spotlight',h,[[0,0,1,0,*([0]*8),1]])[0]
 # Birth scheduling runs original load/delay/wait and hardware task callbacks.
 # Other callbacks cannot gate this prefix: after the sole initial hardware wait,
 # source scripts use only authored delays before their final sprite births.
 importer_path=root/'scripts/imports/battle/import-source-elemental-animations.py';imp=types.ModuleType('spotlight_schedule_importer');imp.__file__=str(importer_path);exec(compile((importer or importer_path).read_text(),str(importer or importer_path),'exec'),imp.__dict__)
 roots,subs=imp.discover_scripts(reference);roots={r['moveId']:r for r in roots};schedules=[]
 schedule_names=['WaitAnimFrameCount','RunAnimScriptCommand','Cmd_loadspritegfx','Cmd_delay','Cmd_waitforvisualfinish']
 schedule_funcs=[ex.function(s['src/battle_anim.c'],n) for n in schedule_names]+[ex.function(s['src/battle_anim_utility_funcs.c'],n) for n in ['AnimTask_HardwarePaletteFade','AnimTask_HardwarePaletteFade_Step']]
 for move in [227,260]:
  commands=[]
  for c in roots[move]['commands']:
   commands.extend([x for x in subs[c['args'][0]] if x['op']!='return'] if c['op']=='call' else [c])
  last=max(i for i,c in enumerate(commands) if c['op']=='createsprite');commands=commands[:last+1];data=[];templates=[]
  for c in commands:
   op=c['op']
   if op=='loadspritegfx':data.extend([0,0,0])
   elif op=='delay':data.extend([4,int(c['args'][0])])
   elif op=='waitforvisualfinish':data.append(5)
   elif op=='createsprite':data.append(2);templates.append(c['args'][0])
   elif op=='createvisualtask' and c['args'][0]=='AnimTask_HardwarePaletteFade':data.extend([6,*map(int,c['args'][2:])])
   else:data.append(3)
  data.append(8)
  prefix=pcode[:pcode.index('static void row(')]+r'''
typedef int8_t s8;typedef int16_t s16;
static const u8*sBattleAnimScriptPtr;static s8 sAnimFramesToWait;static int gAnimScriptActive=1,gAnimVisualTaskCount,frame,birth;static void(*gAnimScriptCallback)(void);static s16 gBattleAnimArgs[8];static struct {void(*func)(u8);}gTasks[1];
#define T1_READ_16(p) ((u16)((p)[0]|((p)[1]<<8)))
#define GET_TRUE_SPRITE_INDEX(x) 0
static const int gBattleAnimPicTable[1],gBattleAnimPaletteTable[1];static void LoadCompressedSpriteSheetUsingHeap(const int*p){}static void LoadCompressedSpritePaletteUsingHeap(const int*p){}static void AddSpriteIndex(u16 i){}
static void DestroyAnimVisualTask(u8 id){assert(id==0);gAnimVisualTaskCount--;gTasks[0].func=0;}
'''
  prefix+='\n'.join(f[:f.index('{')].strip()+';' for f in schedule_funcs)+r'''
static void Create(void){birth++;printf("%d\n",frame);sBattleAnimScriptPtr++;}static void Ignore(void){sBattleAnimScriptPtr++;}static void End(void){gAnimScriptActive=0;sAnimFramesToWait=1;}
static void Hardware(void){sBattleAnimScriptPtr++;for(int i=0;i<5;i++)gBattleAnimArgs[i]=*sBattleAnimScriptPtr++;gAnimVisualTaskCount++;AnimTask_HardwarePaletteFade(0);}
static void(*const sScriptCmdTable[9])(void)={[0]=Cmd_loadspritegfx,[2]=Create,[3]=Ignore,[4]=Cmd_delay,[5]=Cmd_waitforvisualfinish,[6]=Hardware,[8]=End};
'''+'\n'.join(schedule_funcs)+'\nstatic const u8 script[]={'+','.join(map(str,data))+'};'+r'''
int main(void){sBattleAnimScriptPtr=script;gAnimScriptCallback=RunAnimScriptCommand;for(frame=0;gAnimScriptActive&&frame<600;frame++){gAnimScriptCallback();UpdateHardwarePaletteFade();if(gTasks[0].func)gTasks[0].func(0);TransferPlttBuffer();}assert(!gAnimScriptActive);return 0;}
'''
  frames=run('schedule-'+str(move),prefix,[[]])[0];assert len(frames)==len(templates);schedules.append(dict(move=move,births=[dict(frame=f[0],template=t) for f,t in zip(frames,templates)]))
 return dict(schema='firered-spotlight-source-v1',sourceSha256=pins,optimizationParity=['O0','O2'],sourceFunctions=names,hardwareFunctions=palette_names,scheduleFunctions=schedule_names,schedules=schedules,confettiBurst=burst,cases=cases,windowTasks=tasks,hardware=hardware,hardwareColumns=['frame','phase','status','active','delayCounter','y','targetY','finishing','blendControl','mode','reset','registerBlendControl','registerY'],columns=['frame','active','x','y','x2','y2','tile','hflip','invisible','objMode','xScale','yScale','rotation','affineEnded','soundCount','rng','dispcnt','winout'],semantics='Frame0 runs initializer and original AnimateSprite. Source dummy frame END is visually normalized only by runtime. Original affine/frame engines execute unchanged; SAFE_DIV uses source global.h UBFIX zero handling for authored zero-x scale. Harness fields/platform getters and isolated destructor are explicit boundaries. Hardware phase0 is Begin; phase1 Update; phase2 TransferPlttBuffer/UpdateBlendRegisters. All generated expected values come only from extracted original C.')

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--reference',type=Path);p.add_argument('--importer',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);a=p.parse_args()
 if a.output.exists():raise FileExistsError('Refusing to overwrite reviewed fixture: '+str(a.output))
 out=a.build_dir or Path(tempfile.mkdtemp(prefix='spotlight-reference-'));out.mkdir(exist_ok=True,parents=True);r=build(a.root.resolve(),a.reference or a.root/'content/raw/pokefirered/037335f4',out,a.importer);a.output.parent.mkdir(exist_ok=True,parents=True);a.output.write_text(json.dumps(r,separators=(',',':'))+'\n');print(json.dumps(dict(cases=len(r['cases']),rows=sum(len(c['rows']) for c in r['cases']),hardwareCases=len(r['hardware']),hardwareRows=sum(len(c['rows']) for c in r['hardware']))))

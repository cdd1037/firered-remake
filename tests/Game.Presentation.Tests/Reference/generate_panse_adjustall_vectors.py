#!/usr/bin/env python3
"""Extract pinned panse_adjustall, signed8 adjustment, sweep task and scheduler.

Only extracted C is compiled. The audio hooks record exact source calls without
playing sound. Full current-pan signed16 state is preserved separately from the
signed8 argument received by SE12PanpotControl. Source adjustments are exhaustive.
"""
import argparse, importlib.util, json, re, subprocess, tempfile, sys
from pathlib import Path
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[3]
PAN_CASES=[
 ['wish',63,-64,-3,0,0],['delay1',63,-64,-3,1,0],['delay2',63,-64,-3,2,0],['delay255',63,-64,-3,255,0],
 ['extrema_decrease',127,-128,-128,0,0],['extrema_increase',-128,127,127,0,0],
 ['extrema_unit',-128,127,1,0,0],['zero_increment',127,-128,0,2,0],
 ['equal_positive',127,127,0,0,0],['equal_negative',-128,-128,0,0,0],
 ['status_override',63,-64,-3,0,1],
]
def load(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def build(reference,helper,extractor,keep_c=None):
 ref=load('pan_reference',helper);ref.verify_reference(reference);ext=load('pan_extract',extractor)
 def function(source,name):
  try:return ext.function(source,name)
  except ValueError:
   # Same brace extraction as the shared helper; two signatures here extend its
   # fixed return-type/nonempty-parameter regex (s8 and empty arguments).
   m=re.search(r'^(?:static )?(?:void|u8|s8) '+name+r'\([^;]*?\)\n\{',source,re.M)
   if not m:raise ValueError('Missing source function '+name)
   end=m.end();depth=1
   while depth:depth+=(source[end]=='{')-(source[end]=='}');end+=1
   return source[m.start():end]
 anim=(reference/'src/battle_anim.c').read_text();task=(reference/'src/task.c').read_text()
 script=(reference/'data/battle_anim_scripts.s').read_text().split('Move_WISH:',1)[1].split('Move_STOCKPILE:',1)[0]
 assert 'panse_adjustall SE_M_REFLECT, SOUND_PAN_TARGET, SOUND_PAN_ATTACKER, -3, 0' in script
 song=int(re.search(r'^#define SE_M_REFLECT\s+(\d+)',(reference/'include/constants/songs.h').read_text(),re.M)[1]);assert song==200
 funcs=[function(anim,n) for n in ['BattleAnimAdjustPanning2','Task_PanFromInitialToTarget','Cmd_panse_adjustall']]
 funcs += [function(task,n) for n in ['ResetTasks','CreateTask','InsertTask','DestroyTask','RunTasks','FindFirstActiveTask','TaskDummy']]
 aliases=anim[anim.index('#define tInitialPan'):anim.index('static void Cmd_panse(void)',anim.index('#define tInitialPan'))]
 c=r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8;typedef int8_t s8;typedef uint16_t u16;typedef int16_t s16;typedef uint32_t u32;typedef int32_t s32;typedef u8 bool8;typedef u32 bool32;
#define TRUE 1
#define FALSE 0
#define B_SIDE_PLAYER 0
#define SOUND_PAN_ATTACKER -64
#define SOUND_PAN_TARGET 63
#define NUM_TASKS 16
#define NUM_TASK_DATA 16
#define HEAD_SENTINEL 0xFE
#define TAIL_SENTINEL 0xFF
#define REVISION 0
#define T1_READ_16(ptr) ((ptr)[0] | ((ptr)[1]<<8))
typedef void (*TaskFunc)(u8);
struct Task {TaskFunc func;bool8 isActive;u8 prev,next,priority;s16 data[16];};
static struct Task gTasks[16];
static struct {struct {bool8 statusAnimActive;}healthBoxesData[4];}battleSpritesData,*gBattleSpritesDataPtr=&battleSpritesData;
static u8 gBattleAnimAttacker,gAnimSoundTaskCount;static const u8*sBattleAnimScriptPtr;
static int caseId,tick,eventId,updates;static u16 expectedSong;
static u8 GetBattlerSide(u8 b){assert(b<4);return b&1;}
static void PlaySE12WithPanning(u16 song,s8 pan){assert(song==expectedSong);printf("P,%d,%d,%d,%u,%d\n",caseId,eventId++,tick,song,pan);}
static void SE12PanpotControl(s8 pan){printf("U,%d,%d,%d,%d,%d,%d,%d,%d\n",caseId,eventId++,tick,pan,gTasks[0].data[4],gTasks[0].data[8],gTasks[0].isActive,gAnimSoundTaskCount);updates++;}
'''+aliases+'\n'
 c+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'+'\n'.join(funcs)+'\n'
 c+=r'''
static void Reset(int actor,int status){ResetTasks();gBattleAnimAttacker=actor;memset(&battleSpritesData,0,sizeof(battleSpritesData));gBattleSpritesDataPtr->healthBoxesData[actor].statusAnimActive=status;gAnimSoundTaskCount=0;eventId=updates=0;}
static void State(void){printf("S,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",caseId,eventId++,tick,gTasks[0].isActive,gAnimSoundTaskCount,gTasks[0].data[0],gTasks[0].data[1],gTasks[0].data[2],gTasks[0].data[3],gTasks[0].data[4],gTasks[0].data[8]);}
'''
 c+='static const int cases[][5]={'+','.join('{'+','.join(map(str,case[1:]))+'}' for case in PAN_CASES)+'};\n'
 c+=f'static const u16 song={song};\n'
 c+=r'''
int main(void){
 for(int actor=0;actor<4;actor++)for(int status=0;status<2;status++){
  Reset(actor,status);printf("A,%d,%d",actor,status);
  for(int pan=-128;pan<=127;pan++)printf(",%d",BattleAnimAdjustPanning2((s8)pan));puts("");
 }
 for(int actor=0;actor<2;actor++)for(unsigned scenario=0;scenario<sizeof(cases)/sizeof(cases[0]);scenario++){
  const int*p=cases[scenario];Reset(actor,p[4]);expectedSong=song;u8 script[]={0x27,song&255,song>>8,(u8)p[0],(u8)p[1],(u8)p[2],(u8)p[3]};sBattleAnimScriptPtr=script;
  printf("K,%d,%d,%u\n",caseId,actor,scenario);tick=0;Cmd_panse_adjustall();assert(sBattleAnimScriptPtr==script+7);assert(gTasks[0].isActive&&gAnimSoundTaskCount==1);State();
  // Exactly the same source frame: command creates task, then RunTasks runs.
  for(tick=0;gTasks[0].isActive;tick++){assert(tick<140000);RunTasks();State();}
  assert(gAnimSoundTaskCount==0);printf("Z,%d,%d,%d,%d,%d\n",caseId,tick,updates,gTasks[0].data[4],gAnimSoundTaskCount);caseId++;
 }
}
'''
 if keep_c:keep_c.write_text(c)
 with tempfile.TemporaryDirectory() as tmp:
  source=Path(tmp)/'pan.c';exe=Path(tmp)/'pan';source.write_text(c);ref.compile_c(['cc','-std=gnu11','-O0',str(source),'-o',str(exe)]);out=subprocess.check_output([str(exe)],text=True)
 cases={};adjust=[]
 for line in out.splitlines():
  kind,*data=line.split(',');data=list(map(int,data))
  if kind=='A':adjust.append(data);continue
  key,*data=data
  if kind=='K':cases[key]={'config':data,'plays':[],'updates':[],'states':[]}
  else:
   field={'P':'plays','U':'updates','S':'states','Z':'end'}[kind]
   if kind=='Z':cases[key][field]=data
   else:cases[key][field].append(data)
 validate(cases,adjust,song)
 # Keep every pan emission, initial state, first waiting interval and terminal
 # state; long delays otherwise have redundant no-emission state snapshots.
 for case in cases.values():
  first=case['updates'][0][1]
  case['states']=[r for index,r in enumerate(case['states']) if index==0 or r[1]<=min(first+1,3) or r[1]==case['end'][0]-1]
  if len(case['updates'])>512:
   count=len(case['updates']);case['updates']=[r for index,r in enumerate(case['updates']) if index<16 or index>=count-16 or index in [125,126,127,128,254,255,256,257,16382,16383,16384]]
 return {'sourceCommit':ref.COMMIT,'songId':song,'inputCases':PAN_CASES,'inputCaseColumns':['name','initialPan','targetPan','increment','framesToWait','statusActive'],
 'configColumns':['attackerPosition','inputCaseIndex'],'adjustmentColumns':['attackerPosition','statusActive','resultsForEverySigned8InputFromMinus128Through127'],
 'playColumns':['event','tick','songId','signed8Pan'],'updateColumns':['event','tick','signed8AudioPan','signed16CurrentPan','frameCounter','active','soundCount'],
 'stateColumns':['event','tick','active','soundCount','initial','target','increment','framesToWait','current','counter'],
 'endColumns':['nextTick','updates','signed16CurrentPan','soundCount'],'adjustments':adjust,'cases':cases,
 'notes':['Exact extracted command adjusts initial,target,increment independently; input and output are signed8. Opponent negation of-128 remains-128. There is no[-64,63] clamp.',
 'Source statusAnimActive overrides every operand to actor endpoint; this includes the increment. Exhaustive adjustment maps cover all four actor positions and both status states.',
 'Command plays initial pan before incrementing soundCount. It creates a priority1 task; extracted RunTasks executes the first update in the same frame when delay0.',
 'Counter uses postincrement >= framesToWait and resets0 on update. DelayN emits on ticks N,2N+1,3N+2,..., separated by N+1 frames.',
 'Task current pan is signed16. Audio API receives signed8. Terminal emission clamps its local audio pan to target but source task data current keeps the unclamped sum.',
 'Wish player emits60,57,...,-63,-64; opponent emits-60,-57,...,63,64. Both have43 updates, last at tick42.',
 'Zero increment destroys on first eligible update and emits target. Source status override and signed8 minimum can make direction non-progressing until signed16 wrap; fixtures preserve exact source outcomes rather than normalizing increment.',
 'All source states and emissions are validated before compaction. Cases with more than512 updates retain first/last16 emissions and selected signed-width boundaries; end.updates preserves total count. Other cases retain every emission.',
 'Audio resource hooks are read-only logs; no sound engine, network, managed compiler or upstream script is executed.']}

def signed8(x):return ((x+128)&255)-128
def signed16(x):return ((x+32768)&65535)-32768

def validate(cases,adjust,song):
 assert len(adjust)==8 and len(cases)==22
 for actor,status,*values in adjust:
  assert values==[(-64 if actor%2==0 else 63) if status else signed8(-x) if actor%2 else x for x in range(-128,128)]
 for case in cases.values():
  actor,scenario=case['config'];name,start,target,inc,delay,status=PAN_CASES[scenario]
  def adj(x):return (-64 if actor%2==0 else 63) if status else signed8(-x) if actor%2 else x
  start,target,inc=map(adj,[start,target,inc]);assert case['plays']==[[0,0,song,start]]
  assert case['states'][0]==[1,0,1,1,start,target,inc,delay,start,0]
  current=start;counter=0;update_index=0
  for tick,r in enumerate(case['states'][1:]):
   update=counter>=delay;counter+=1;active=1
   if update:
    counter=0;current=signed16(current+inc)
    destroy=inc==0 or (current>=target if start<target else current<=target)
    pan=target if destroy else current;active=int(not destroy)
    u=case['updates'][update_index];assert u[1:]==[tick,signed8(pan),current,0,active,active],(name,actor,u,pan,current)
    assert u[0]<r[0];update_index+=1
   assert r[1:]==[tick,active,active,start,target,inc,delay,current,counter]
  assert update_index==len(case['updates'])==case['end'][1] and case['end'][2:]==[current,0]
  assert [r[1] for r in case['updates']]==[delay+i*(delay+1) for i in range(update_index)]
  assert case['end'][0]==update_index*(delay+1)
  if name.startswith('wish') or name.startswith('delay'):
   assert update_index==43 and case['updates'][-1][2]==(-64 if actor==0 else 64)
   assert case['end'][2]==(-66 if actor==0 else 66)

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--helper',type=Path,default=ROOT/'tests/Reference/reference_c.py');p.add_argument('--extractor',type=Path,default=ROOT/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py');p.add_argument('--output',type=Path,default=Path(__file__).with_name('panse-adjustall-source-vectors.json'));p.add_argument('--keep-c',type=Path);a=p.parse_args();result=build(a.reference,a.helper,a.extractor,a.keep_c);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');print(str(a.output)+': '+str(len(result['cases']))+' cases, '+str(a.output.stat().st_size)+' bytes')
 for c in result['cases'].values():print(c['config'],result['inputCases'][c['config'][1]][0],c['end'])

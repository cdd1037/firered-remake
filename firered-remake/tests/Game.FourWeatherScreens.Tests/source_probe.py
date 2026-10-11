"""Pinned original C commands/field owner/screen blocks; no upstream scripts run."""
from pathlib import Path
import hashlib,itertools,json,subprocess,sys
source,out=map(Path,sys.argv[1:]);out.mkdir(parents=True,exist_ok=True)
commit='037335f4c725d7c9aecdac87066f2002b4bd7e14'
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=source,text=True).strip()==commit
paths=['src/battle_script_commands.c','src/battle_util.c','src/pokemon.c','include/constants/battle.h','include/constants/battle_string_ids.h','include/constants/battle_anim.h','include/constants/moves.h','include/constants/pokemon.h']
texts={};pins={}
for path in paths:
 data=(source/path).read_bytes();assert data==subprocess.check_output(['git','show',commit+':'+path],cwd=source),path
 texts[path]=data.decode();pins[path]=hashlib.sha256(data).hexdigest()
def function(text,name):
 a=text.index(name+'\n{');e=text.index('{',a)+1;depth=1
 while depth:depth+=(text[e]=='{')-(text[e]=='}');e+=1
 return text[a:e]
cmd,util,pkm=[texts['src/'+f] for f in ['battle_script_commands.c','battle_util.c','pokemon.c']]
names=['reflect','lightscreen','mist','safeguard','rain','sandstorm','sunny','hail']
commands='\n'.join(function(cmd,'static void Cmd_set'+name+'(void)') for name in names)
owner=function(util,'u8 DoFieldEndTurnEffects(void)')
enums=util[util.rfind('enum\n{',0,util.index('u8 DoFieldEndTurnEffects(void)\n{')):util.index('u8 DoFieldEndTurnEffects(void)\n{')]
count=function(pkm,'u8 CountAliveMonsInBattle(u8 caseId)')
blocks=[]
for marker in ['// Apply Reflect','// Apply Lightscreen']:
 a=pkm.index('        if ',pkm.index(marker));e=pkm.index('        // Moves hitting both',a);blocks.append(pkm[a:e])
headers='\n'.join('#include "'+str(source/p)+'"' for p in paths if p.startswith('include/'))
harness=r'''#include <stdint.h>
#include <stdio.h>
#include <string.h>
typedef uint8_t u8;typedef uint16_t u16;typedef int32_t s32;
'''+headers+r'''
#define BATTLE_ALIVE_EXCEPT_ACTIVE 0
#define BATTLE_ALIVE_ATK_SIDE 1
#define BATTLE_ALIVE_DEF_SIDE 2
#define GET_BATTLER_SIDE(battler) ((battler)&1)
#define GetBattlerSide(battler) ((battler)&1)
#define MULTISTRING_CHOOSER 5
#define FALSE 0
#define MAX_BATTLERS_COUNT 4
#define PREPARE_MOVE_BUFFER(a,b) ((void)(a),(void)(b))
unsigned gBitTable[4]={1,2,4,8};
int gBattlerAttacker,gBattlerTarget,gActiveBattler,gAbsentBattlerFlags,gBattleWeather,gBattleTypeFlags,gMoveResultFlags,gCritMultiplier,gBattlersCount=4;
int gSideStatuses[2],gBattleCommunication[8],gBattleTextBuff1[8],gBattlerByTurnOrder[4];
struct {int reflectTimer,reflectBattlerId,lightscreenTimer,lightscreenBattlerId,mistTimer,mistBattlerId,safeguardTimer,safeguardBattlerId;}gSideTimers[2];
struct {int weatherDuration,wishCounter[4];}gWishFutureKnock;
struct {int hp;}gBattleMons[4];
struct {int animArg1;}gBattleScripting;
struct Counters {u8 turnCountersTracker,turnSideTracker;} counters,*gBattleStruct=&counters;
u8 memory[256],*gBattlescriptCurrInstr;
u8 sideEnd[1],safeEnd[1],wish[1],rain[1],weatherEnd[1],weatherDamage[1],sunEnd[1],sun[1];
#define BattleScript_SideStatusWoreOff sideEnd
#define BattleScript_SafeguardEnds safeEnd
#define BattleScript_WishComesTrue wish
#define BattleScript_RainContinuesOrEnds rain
#define BattleScript_SandStormHailEnds weatherEnd
#define BattleScript_DamagingWeatherContinues weatherDamage
#define BattleScript_SunlightFaded sunEnd
#define BattleScript_SunlightContinues sun
void BattleTurnPassed(void){}
void (*gBattleMainFunc)(void)=BattleTurnPassed;
int callbacks;
void BattleScriptExecute(const u8 *script){(void)script;callbacks++;}
int GetWhoStrikesFirst(int a,int b,int ignore){(void)a;(void)b;(void)ignore;return 0;}
void SwapTurnOrder(int a,int b){int x=gBattlerByTurnOrder[a];gBattlerByTurnOrder[a]=gBattlerByTurnOrder[b];gBattlerByTurnOrder[b]=x;}
'''+count+'\n'+commands+'\n'+enums+owner+r'''
int main(void){int mode,a,b,c,d,e,f;while(scanf("%d%d%d%d%d%d%d",&mode,&a,&b,&c,&d,&e,&f)==7){
memset(gSideTimers,0,sizeof gSideTimers);memset(gSideStatuses,0,sizeof gSideStatuses);memset(&gWishFutureKnock,0,sizeof gWishFutureKnock);memset(&counters,0,sizeof counters);
gBattlerAttacker=a;gBattlerTarget=a^1;gActiveBattler=0;gAbsentBattlerFlags=e;gBattleTypeFlags=BATTLE_TYPE_DOUBLE;gMoveResultFlags=0;gBattlescriptCurrInstr=memory;callbacks=0;
for(int i=0;i<4;i++)gBattleMons[i].hp=100;
if(mode==0){int side=a&1;gBattleWeather=c;gWishFutureKnock.weatherDuration=d;
gSideTimers[side].reflectTimer=gSideTimers[side].lightscreenTimer=gSideTimers[side].mistTimer=gSideTimers[side].safeguardTimer=f;
if(f)gSideStatuses[side]=SIDE_STATUS_REFLECT|SIDE_STATUS_LIGHTSCREEN|SIDE_STATUS_MIST|SIDE_STATUS_SAFEGUARD;
switch(b){case 0:Cmd_setreflect();break;case 1:Cmd_setlightscreen();break;case 2:Cmd_setmist();break;case 3:Cmd_setsafeguard();break;case 4:Cmd_setrain();break;case 5:Cmd_setsandstorm();break;case 6:Cmd_setsunny();break;case 7:Cmd_sethail();break;}
printf("%d %d %d %d %d %d %d\n",gBattleWeather,gWishFutureKnock.weatherDuration,gSideTimers[side].reflectTimer,gSideTimers[side].lightscreenTimer,gSideTimers[side].mistTimer,gSideTimers[side].safeguardTimer,gMoveResultFlags!=0);
}else if(mode==1){gBattleWeather=c;gWishFutureKnock.weatherDuration=d;for(int i=0;i<2;i++){int value=i?b:a;gSideTimers[i].reflectTimer=gSideTimers[i].lightscreenTimer=gSideTimers[i].mistTimer=gSideTimers[i].safeguardTimer=value;if(value)gSideStatuses[i]=SIDE_STATUS_REFLECT|SIDE_STATUS_LIGHTSCREEN|SIDE_STATUS_MIST|SIDE_STATUS_SAFEGUARD;}
int guard=0;while(counters.turnCountersTracker<ENDTURN_FIELD_COUNT&&guard++<50)DoFieldEndTurnEffects();
printf("%d %d %d %d %d %d\n",gBattleWeather,gWishFutureKnock.weatherDuration,gSideTimers[0].reflectTimer,gSideTimers[1].reflectTimer,gSideTimers[0].mistTimer,gSideTimers[1].safeguardTimer);
}else{int damage=a,sideStatus=SIDE_STATUS_REFLECT|SIDE_STATUS_LIGHTSCREEN;gCritMultiplier=b;gBattleTypeFlags=c;gBattlerTarget=d;gAbsentBattlerFlags=e;
if(f==0){
'''+blocks[0]+r'''
}else{
'''+blocks[1]+r'''
}
printf("%d %d\n",damage,CountAliveMonsInBattle(BATTLE_ALIVE_DEF_SIDE));
}
}return 0;}
'''
(out/'four-field-oracle.c').write_text(harness)
subprocess.run(['cc','-std=c11','-Wall','-Wextra','-Werror',str(out/'four-field-oracle.c'),'-o',str(out/'four-field-oracle')],check=True)
settings=[(0,slot,effect,w,3,0,t) for slot,effect,w,t in itertools.product(range(4),range(8),(0,1,5,8,24,32,96,128),(0,3))]
timers=[(1,a,b,w,d,0,0) for a,b,w,d in itertools.product((0,1,5),(0,1,5),(0,1,5,8,24,32,96,128),(1,5))]
screens=[(2,d,crit,double,target,absence,effect) for d,crit,double,target,absence,effect in itertools.product((1,2,3,5,17,100),(1,2),(0,1),(0,1),(0,4,8),(0,1))]
rows=settings+timers+screens
result=subprocess.run([str(out/'four-field-oracle')],input=''.join(' '.join(map(str,row))+'\n' for row in rows),capture_output=True,text=True,check=True)
data=[{'input':row,'output':list(map(int,line.split()))} for row,line in zip(rows,result.stdout.splitlines(),strict=True)]
(out/'four-field-vectors.json').write_text(json.dumps({'sourceCommit':commit,'pins':pins,'functions':names+['DoFieldEndTurnEffects','CountAliveMonsInBattle'],'screenBlocks':'unchanged Reflect/Light Screen blocks from CalculateBaseDamage','vectors':data},indent=2)+'\n')
print(f'{len(settings)} setting commands; {len(timers)} complete field-owner timer vectors; {len(screens)} source screen/count vectors')

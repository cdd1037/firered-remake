#!/usr/bin/env python3
"""Compile unmodified pinned C command bodies; host stubs mark the base-damage boundary."""
import argparse, hashlib, json, re, subprocess, tempfile
from pathlib import Path
PIN='037335f4c725d7c9aecdac87066f2002b4bd7e14'
p=argparse.ArgumentParser();p.add_argument('--source',type=Path,required=True);p.add_argument('--report',type=Path,required=True);a=p.parse_args()
run=lambda args,**kw: subprocess.check_output(args,text=True,**kw)
assert run(['git','rev-parse','HEAD'],cwd=a.source).strip()==PIN
paths=['src/battle_script_commands.c','src/battle_util.c','src/battle_main.c','data/battle_scripts_1.s']
sources={f:(a.source/f).read_text() for f in paths}
for f,s in sources.items():assert s==run(['git','show',f'{PIN}:{f}'],cwd=a.source),f
s=sources[paths[0]];scripts=sources[paths[3]]
def body(name):
 m=re.search(r'^static void '+name+r'\(void\)\n',s,re.M);assert m,name
 depth=0
 for i in range(s.index('{',m.end()),len(s)):
  if s[i]=='{':depth+=1
  elif s[i]=='}':
   depth-=1
   if depth==0:return s[m.start():i+1]
functions={n:body(n) for n in ['Cmd_stockpile','Cmd_stockpiletobasedamage','Cmd_stockpiletohpheal','Cmd_setcharge']}
assert 'statStages' not in ''.join(functions.values())
spit=scripts.split('BattleScript_EffectSpitUp::',1)[1].split('BattleScript_EffectSwallow::',1)[0]
assert all(x not in spit for x in ['critcalc','damagecalc','adjustnormaldamage'])
assert [spit.index(x) for x in ['attackstring','ppreduce','accuracycheck','stockpiletobasedamage','typecalc','adjustsetdamage']]==sorted(spit.index(x) for x in ['attackstring','ppreduce','accuracycheck','stockpiletobasedamage','typecalc','adjustsetdamage'])
miss=scripts.split('BattleScript_PrintMoveMissed::',1)[1].split('\n\n',1)[0];assert 'ppreduce' in miss
charge_tick=sources[paths[1]].split('            case ENDTURN_CHARGE:',1)[1].split('            case ENDTURN_TAUNT:',1)[0]
charge_damage=re.search(r'    if \(gStatuses3\[gBattlerAttacker\] & STATUS3_CHARGED_UP && gBattleMoves\[gCurrentMove\].type == TYPE_ELECTRIC\)\n        gBattleMoveDamage \*= 2;',s).group()
assert 'chargeTimer && --gDisableStructs[gActiveBattler].chargeTimer == 0' in charge_tick
prefix=r'''
#include <stdint.h>
#include <stdio.h>
#include <string.h>
typedef uint8_t u8; typedef int32_t s32;
#define T1_READ_PTR(x) ((void)(x), failure)
#define GET_BATTLER_SIDE(x) ((x)&1)
#define PREPARE_BYTE_NUMBER_BUFFER(a,b,c) ((void)(a),(void)(b),(void)(c));
enum { MULTISTRING_CHOOSER, MISS_TYPE, B_MSG_CANT_STOCKPILE=9, B_MSG_STOCKPILED=10, B_MSG_PROTECTED=11, B_MSG_SWALLOW_FAILED=12, B_MSG_SWALLOW_FULL_HP=13, MOVE_RESULT_MISSED=1, STATUS3_CHARGED_UP=512, TYPE_ELECTRIC=13 };
struct Mon { int hp,maxHP; } gBattleMons[4];
struct Disable { int stockpileCounter,chargeTimer,chargeTimerStartValue; } gDisableStructs[4];
struct { int helpingHand; } gProtectStructs[4];
struct { int animTurn; } gBattleScripting;
struct { int type; } gBattleMoves[2];
u8 script[8], failure[1],*gBattleTextBuff1;
const u8 *gBattlescriptCurrInstr;
int gBattlerAttacker,gBattlerTarget,gActiveBattler,gCurrentMove,gSideStatuses[2],gStatuses3[4],gBattleCommunication[16],gMoveResultFlags,gBattleMoveDamage,baseCalls;
static int CalculateBaseDamage(struct Mon *a,struct Mon *b,int move,int side,int power,int type,int actor,int target){(void)a;(void)b;(void)move;(void)side;(void)actor;(void)target;if(power||type)return -999;baseCalls++;return 13;}
static void Reset(void){memset(gDisableStructs,0,sizeof(gDisableStructs));memset(gProtectStructs,0,sizeof(gProtectStructs));memset(gBattleCommunication,0,sizeof(gBattleCommunication));memset(gStatuses3,0,sizeof(gStatuses3));gBattleMons[0]=(struct Mon){1,101};gBattlerTarget=1;gBattlescriptCurrInstr=script;gBattleMoveDamage=777;gMoveResultFlags=0;baseCalls=0;}
'''
main=r'''
int main(void){int n=0;puts("[");
for(int count=0;count<4;count++)for(int kind=0;kind<5;kind++){Reset();gDisableStructs[0].stockpileCounter=count;if(kind==0)Cmd_stockpile();else if(kind<3){gBattleCommunication[MISS_TYPE]=kind==2?B_MSG_PROTECTED:0;gProtectStructs[0].helpingHand=1;Cmd_stockpiletobasedamage();}else{if(kind==4)gBattleMons[0].hp=101;Cmd_stockpiletohpheal();}printf("%s{\"kind\":%d,\"count\":%d,\"after\":%d,\"damage\":%d,\"target\":%d,\"failed\":%d,\"flag\":%d,\"baseCalls\":%d}",n++?",\n":"",kind,count,gDisableStructs[0].stockpileCounter,gBattleMoveDamage,gBattlerTarget,gBattlescriptCurrInstr==failure,gMoveResultFlags,baseCalls);}
Reset();Cmd_setcharge();for(int i=0;i<3;i++){gBattleMoveDamage=17;gBattleMoves[0].type=TYPE_ELECTRIC;ChargeDamage();printf(",\n{\"kind\":5,\"step\":%d,\"timer\":%d,\"start\":%d,\"damage\":%d,\"charged\":%d}",i,gDisableStructs[0].chargeTimer,gDisableStructs[0].chargeTimerStartValue,gBattleMoveDamage,!!(gStatuses3[0]&STATUS3_CHARGED_UP));ChargeTick();}puts("\n]");return 0;}
'''
tick=charge_tick[:charge_tick.index('gBattleStruct->turnEffectsTracker++')]
unit=prefix+'\n'+'\n'.join(functions.values())+'\nstatic void ChargeTick(void){'+tick+'}\nstatic void ChargeDamage(void){'+charge_damage+'}\n'+main
with tempfile.TemporaryDirectory(prefix='stockpile-source-') as td:
 td=Path(td);(td/'probe.c').write_text(unit)
 subprocess.run(['cc','-std=c11','-Wall','-Wextra','-Werror','-O2',str(td/'probe.c'),'-o',str(td/'probe')],check=True)
 rows=json.loads(run([str(td/'probe')]))
assert len(rows)==23
for r in rows:
 if r['kind']==0: assert r['after']==min(3,r['count']+1) and r['flag']==(r['count']==3)
 elif r['kind'] in (1,2):
  assert r['after']==0 and r['failed']==(r['count']==0)
  assert r['damage']==(13*r['count']*15//10 if r['kind']==1 and r['count'] else 777)
  assert r['baseCalls']==int(r['kind']==1 and r['count']>0)
 elif r['kind'] in (3,4):
  assert r['after']==0 and r['failed']==int(r['count']==0 or r['kind']==4)
  assert r['damage']==(-(101//(1<<(3-r['count']))) if r['kind']==3 and r['count'] else 777)
 else: assert (r['timer'],r['start'],r['damage'],r['charged'])==[(2,2,34,1),(1,2,34,1),(0,2,17,0)][r['step']]
report={'passed':True,'sourceCommit':PIN,'compiler':run(['cc','--version']).splitlines()[0],'sourceFiles':{f:hashlib.sha256(s.encode()).hexdigest() for f,s in sources.items()},'extractedFunctions':{n:hashlib.sha256(s.encode()).hexdigest() for n,s in functions.items()},'vectors':rows,'scope':'Four original command bodies, original Charge damage and end-turn fragments; CalculateBaseDamage is a host boundary returning 13, not a recreated original damage formula. Script audit verifies PP/accuracy/type/adjust order, no stat boosts or crit/random damage commands.'}
a.report.write_text(json.dumps(report,indent=2)+'\n');print(json.dumps({'passed':True,'vectors':len(rows)}))

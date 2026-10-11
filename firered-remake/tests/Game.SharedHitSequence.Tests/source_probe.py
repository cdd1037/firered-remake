#!/usr/bin/env python3
"""Pinned original C commands plus script ordering; never execute upstream tools."""
import hashlib, json, pathlib, subprocess, sys
ref=pathlib.Path(sys.argv[1]); out=pathlib.Path(sys.argv[2]);out.mkdir(parents=True,exist_ok=False)
commit=subprocess.check_output(['git','-C',str(ref),'rev-parse','HEAD'],text=True).strip()
assert commit=='037335f4c725d7c9aecdac87066f2002b4bd7e14'
src=(ref/'src/battle_script_commands.c').read_text(); scripts=(ref/'data/battle_scripts_1.s').read_text(); util=(ref/'src/battle_util.c').read_text(); main=(ref/'src/battle_main.c').read_text()
def function(name):
 start=src.index('static void '+name+'(void)\n{');end=src.index('\n}\n',start)+3
 return src[start:end]
fury=function('Cmd_furycuttercalc');fake=function('Cmd_jumpifnotfirstturn')
code='''#include <stdio.h>
#include <stdint.h>
typedef int32_t s32; typedef uint8_t u8;
#define MOVE_RESULT_NO_EFFECT 1
#define T1_READ_PTR(x) (failure)
struct { u8 furyCutterCounter; u8 isFirstTurn; } gDisableStructs[4];
struct { unsigned power; } gBattleMoves[1];
unsigned gMoveResultFlags,gBattlerAttacker,gCurrentMove,gDynamicBasePower;
const u8 storage[16]={0},failure[16]={0},BattleScript_MoveMissedPause[16]={0};const u8 *gBattlescriptCurrInstr;
'''+fury+fake+'''
int main(void){for(unsigned b=0;b<4;b++)for(unsigned c=0;c<6;c++)for(unsigned fail=0;fail<2;fail++){
 gBattlerAttacker=b;gDisableStructs[b].furyCutterCounter=c;gMoveResultFlags=fail;gBattleMoves[0].power=10;gDynamicBasePower=0;gBattlescriptCurrInstr=storage;Cmd_furycuttercalc();
 printf("F %u %u %u %u %u %d\\n",b,c,fail,gDisableStructs[b].furyCutterCounter,gDynamicBasePower,gBattlescriptCurrInstr==BattleScript_MoveMissedPause);
 }for(unsigned c=0;c<3;c++){gDisableStructs[gBattlerAttacker].isFirstTurn=c;gBattlescriptCurrInstr=storage;Cmd_jumpifnotfirstturn();printf("O %u %d\\n",c,gBattlescriptCurrInstr==storage+5);}return 0;}
'''
(out/'oracle.c').write_text(code)
answers=[]
for name,flags in [('oracle-o2',['-O2']),('oracle-ubsan',['-O1','-fsanitize=undefined','-fno-sanitize-recover=all'])]:
 subprocess.run(['cc','-std=c11','-Wall','-Wextra','-Werror',*flags,str(out/'oracle.c'),'-o',str(out/name)],check=True)
 answers.append(subprocess.check_output([str(out/name)],text=True))
assert answers[0]==answers[1]
rows=[]
for line in answers[0].splitlines():
 p=line.split();rows.append(dict(kind=p[0],values=list(map(int,p[1:]))))
assert len(rows)==51
for row in rows:
 if row['kind']=='F':
  b,c,fail,after,power,jump=row['values'];assert (after,power,jump)==((0,0,1) if fail else (min(5,c+1),10<<min(c,4),0))
 else:
  c,passed=row['values'];assert passed==int(c!=0)
checks={}
def check(name,ok):assert ok,name;checks[name]=True
rage=scripts[scripts.index('BattleScript_EffectRage::'):scripts.index('BattleScript_EffectMimic::')]
check('rage_accuracy_primary_before_pp',rage.index('accuracycheck')<rage.index('seteffectprimary')<rage.index('BattleScript_HitFromAtkString'))
check('rage_miss_clears_status','clearstatusfromeffect BS_ATTACKER' in rage)
check('rage_substitute_primary_guard',src.index('status2 & STATUS2_SUBSTITUTE && affectsUser')<src.index('case MOVE_EFFECT_RAGE:'))
check('rage_enemy_actual_damage',all(x in src[src.index('case MOVEEND_RAGE:'):src.index('case MOVEEND_DEFROST:')] for x in ['GetBattlerSide(gBattlerAttacker) != GetBattlerSide(gBattlerTarget)','TARGET_TURN_DAMAGED','gBattleMoves[gCurrentMove].power != 0']))
triple=scripts[scripts.index('BattleScript_EffectTripleKick::'):scripts.index('BattleScript_EffectThief::')]
check('triple_each_hit_accuracy_and_power',triple.index('accuracycheck')<triple.index('addbyte sTRIPLE_KICK_POWER, 10')<triple.index('critcalc')<triple.index('adjustnormaldamage')<triple.index('jumpifmovehadnoeffect'))
check('triple_stops_sleep_ko_endure',all(x in triple for x in ['jumpifhasnohp BS_ATTACKER','jumpifhasnohp BS_TARGET','MOVE_SLEEP_TALK','MOVE_RESULT_FOE_ENDURED','bicbyte gMoveResultFlags, MOVE_RESULT_MISSED']))
check('triple_per_hit_moveend','moveendto MOVEEND_NEXT_TARGET' in triple and 'moveendfrom MOVEEND_UPDATE_LAST_MOVES' in triple)
check('paralysis_script_cancels_chain','cancelmultiturnmoves BS_ATTACKER' in scripts[scripts.index('BattleScript_MoveUsedIsParalyzed::'):scripts.index('BattleScript_MoveUsedFlinched::')])
check('cancel_multiturn_resets_fury','furyCutterCounter = 0' in util[util.index('void CancelMultiTurnMoves('):util.index('bool8 WasUnableToUseMove')])
check('chosen_move_clears_rage','gChosenMoveByBattler[i] != MOVE_RAGE' in util)
check('initial_entry_decrements_firstturn','TurnValuesCleanUp(FALSE);' in main[main.index('static void TryDoEventsBeforeFirstTurn(void)\n{'):main.index('static void HandleEndTurn_ContinueBattle(void)\n{')])
files=['src/battle_script_commands.c','data/battle_scripts_1.s','src/battle_util.c','src/battle_main.c','src/data/battle_moves.h']
(out/'vectors.json').write_text(json.dumps(rows,indent=2)+'\n')
(out/'source-report.json').write_text(json.dumps(dict(commit=commit,sources={p:hashlib.sha256((ref/p).read_bytes()).hexdigest() for p in files},vectors=len(rows),checks=checks,optimizedAndUbsanAgree=True),indent=2)+'\n')
print(json.dumps(dict(vectors=len(rows),scriptChecks=len(checks),optimizedAndUbsanAgree=True)))

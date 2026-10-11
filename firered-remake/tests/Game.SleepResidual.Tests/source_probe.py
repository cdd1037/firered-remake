#!/usr/bin/env python3
"""Extract byte-identical pinned FireRed C, compile a small host harness, parse JSON.

Run this locally authored script, never a script from the reference repository.
"""
from pathlib import Path
import hashlib
import itertools
import json
import subprocess
import sys

PIN = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
ref = Path(sys.argv[1]).resolve()
out = Path(sys.argv[2]).resolve()
if out.exists():
    raise FileExistsError("Use a fresh evidence directory; source vectors are immutable")
out.mkdir(parents=True)
assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ref, text=True).strip() == PIN
files = ['src/battle_util.c', 'src/battle_script_commands.c', 'src/battle_main.c',
         'data/battle_scripts_1.s', 'src/data/battle_moves.h', 'include/battle.h',
         'include/battle_controllers.h', 'src/battle_controller_player.c']
headers = ['battle.h', 'pokemon.h', 'abilities.h', 'battle_script_commands.h',
           'battle_string_ids.h', 'battle_move_effects.h', 'moves.h']
files += ['include/constants/' + p for p in headers]
pins = {}
texts = {}
for name in files:
    original = subprocess.check_output(['git', 'show', PIN + ':' + name], cwd=ref)
    assert (ref / name).read_bytes() == original, 'Modified reference: ' + name
    pins[name] = hashlib.sha256(original).hexdigest()
    texts[name] = original.decode()
    if name.startswith('include/constants/'):
        target = out / 'pinned' / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(original)

cmd = texts['src/battle_script_commands.c']
util = texts['src/battle_util.c']
main = texts['src/battle_main.c']
scripts = texts['data/battle_scripts_1.s']
extractions = []

def record(name, file, fragment):
    whole = texts[file]
    assert fragment in whole
    offset = whole.index(fragment)
    extractions.append(dict(name=name, file=file,
                            startLine=whole[:offset].count('\n') + 1,
                            lines=fragment.count('\n') + 1,
                            sha256=hashlib.sha256(fragment.encode()).hexdigest()))
    return fragment

def function(file, signature):
    text = texts[file]
    start = text.index(signature + '\n{')
    pos = text.index('{', start)
    depth = 1
    end = pos + 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return record(signature, file, text[start:end])

def span(file, start, end, name):
    text = texts[file]
    frag = text[text.index(start):text.index(end, text.index(start))]
    return record(name, file, frag)

functions = [function('src/battle_script_commands.c', 'static void ' + n + '(void)') for n in ['Cmd_cursetarget','Cmd_setyawn','Cmd_datahpupdate']]
functions += [function('src/battle_util.c', 'void CancelMultiTurnMoves(u8 battler)'), function('src/battle_script_commands.c','bool8 UproarWakeUpCheck(u8 battlerId)'), function('src/battle_main.c','void SwitchInClearSetData(void)')]
enum=span('src/battle_util.c','enum\n{\n    ENDTURN_INGRAIN,','\nu8 DoBattlerEndTurnEffects','end-turn enum')
nightmare=span('src/battle_util.c','            case ENDTURN_NIGHTMARES:','            case ENDTURN_CURSE:','Nightmare residual')
curse=span('src/battle_util.c','            case ENDTURN_CURSE:','            case ENDTURN_WRAP:','Curse residual')
yawn=span('src/battle_util.c','            case ENDTURN_YAWN:','            case ENDTURN_BATTLER_COUNT:','Yawn residual')
absent=span('src/battle_util.c','        if (gAbsentBattlerFlags & gBitTable[gActiveBattler])','        else\n','end-turn absent gate')
regions={}
for name,a,b in [('Nightmare','BattleScript_EffectNightmare::','BattleScript_EffectMinimize::'),('Curse','BattleScript_EffectCurse::','BattleScript_EffectProtect::'),('Yawn','BattleScript_EffectYawn::','BattleScript_PrintBankAbilityMadeIneffective::')]:
    regions[name]=span('data/battle_scripts_1.s',a,b,name+' script')
regions['CurseSelectedTarget']=span('src/battle_controller_player.c', '        if (moveInfo->moves[gMoveSelectionCursor[gActiveBattler]] == MOVE_CURSE)', '        if (moveTarget & MOVE_TARGET_USER)', 'non-Ghost Curse selection target')
assert 'accuracycheck' not in regions['Nightmare']
assert regions['Yawn'].index('ABILITY_VITAL_SPIRIT') < regions['Yawn'].index('STATUS2_SUBSTITUTE') < regions['Yawn'].index('SIDE_STATUS_SAFEGUARD') < regions['Yawn'].index('accuracycheck') < regions['Yawn'].index('jumpifcantmakeasleep') < regions['Yawn'].index('setyawn')
assert regions['Curse'].index('STAT_SPEED') < regions['Curse'].index('STAT_ATK') < regions['Curse'].index('STAT_DEF')
# Authenticate exact no-accuracy branch and primary effect gates, including absence of a miss-result check.
regions['NoAccuracy']=span('src/battle_script_commands.c','    if (move == NO_ACC_CALC || move == NO_ACC_CALC_CHECK_LOCK_ON)','    else\n    {\n        u8 type, moveAcc','NO_ACC_CALC_CHECK_LOCK_ON branch')
regions['PrimaryEffectGates']=span('src/battle_script_commands.c','void SetMoveEffect(bool8 primary, u8 certain)','    if (gBattleCommunication[MOVE_EFFECT_BYTE] <= PRIMARY_STATUS_MOVE_EFFECT)','primary effect gates')
assert 'MOVE_RESULT' not in regions['PrimaryEffectGates']
regions['NightmareSet']=span('src/battle_script_commands.c','            case MOVE_EFFECT_NIGHTMARE:','            case MOVE_EFFECT_ALL_STATS_UP:','Nightmare primary set')
(out/'source-excerpts.txt').write_text('\n\n'.join(k+'\n'+v for k,v in regions.items()))
(out/'source-script-order.json').write_text(json.dumps({'sourceCommit':PIN,'regions':regions},indent=2)+'\n')
head = r'''#include <stdio.h>
#include <stdint.h>
#include <string.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32; typedef int32_t s32;
'''
head += '\n'.join('#include "pinned/include/constants/' + h + '"' for h in headers)
head += r'''
#define GetBattlerSide(b) ((b)&1)
#define IS_BATTLER_OF_TYPE(b,t) (gBattleMons[b].type1==(t)||gBattleMons[b].type2==(t))
#define IS_TYPE_PHYSICAL(t) ((t)<TYPE_MYSTERY)
#define F_DYNAMIC_TYPE_1 (1 << 6)
#define DYNAMIC_TYPE_MASK (F_DYNAMIC_TYPE_1 - 1)
#define BUFFER_A 0
#define REQUEST_HP_BATTLE 42
#define T1_READ_PTR(p) ((void)(p), failScript)
struct Mon {u16 hp,maxHP;u8 type1,type2,ability,statStages[NUM_BATTLE_STATS];u32 status1,status2;} gBattleMons[4];
struct DisableStruct {int substituteHP,battlerWithSureHit,perishSongTimer,perishSongTimerStartValue,battlerPreventingEscape,isFirstTurn,rolloutTimer,furyCutterCounter;} gDisableStructs[4];
struct Battle {int dynamicMoveType,turnEffectsTracker,turnEffectsBattlerId;u8 wrappedBy[4],lastTakenMove[8],lastTakenMoveFrom[32];u16 choicedMove[4];} bs,*gBattleStruct=&bs;
struct Damage {int dmg,physicalDmg,specialDmg,physicalBattlerId,specialBattlerId;} gSpecialStatuses[4],gProtectStructs[4];
struct {int animArg1,animArg2,battler;} gBattleScripting;
struct {int effect,type;} gBattleMoves[400];
struct Flags {int flags[4];} flagData;
struct Resources {struct Flags *flags;} resources={&flagData},*gBattleResources=&resources;
int gActionSelectionCursor[4],gMoveSelectionCursor[4],gLastMoves[4],gLastLandedMoves[4],gLastHitByType[4],gLastResultingMoves[4],gLastPrintedMoves[4],gLastHitBy[4];
int gBattlerAttacker,gBattlerTarget,gActiveBattler,gCurrentMove,gBattlersCount=4,gBattleMoveDamage,gHpDealt,gMoveResultFlags,gBattleControllerExecFlags,effect,gEffectBattler,randomValue,randomCalls;
u32 gStatuses3[4],gHitMarker,gAbsentBattlerFlags,gBitTable[4]={1,2,4,8},gTakenDmg[4];
int gTakenDmgByBattler[4],gBattleCommunication[8];
u8 script[32],failScript[8],subScript[8],ingrainScript[8],seedScript[8];
const u8 *gBattlescriptCurrInstr=script,*BattleScript_SubstituteFade=subScript,*BattleScript_IngrainTurnHeal=ingrainScript,*BattleScript_LeechSeedTurnDrain=seedScript,*executed;
const u8 *BattleScript_NightmareTurnDmg=seedScript,*BattleScript_CurseTurnDmg=seedScript,*BattleScript_YawnMakesAsleep=seedScript;
#define REQUEST_STATUS_BATTLE 40
#define TRUE 1
#define FALSE 0
typedef int bool8;
static unsigned Random(void){randomCalls++;return randomValue;}

static int GetBattlerForBattleScript(int b){return b==BS_ATTACKER?gBattlerAttacker:gBattlerTarget;}
static void BattleScriptPushCursor(void){}
static void BattleScriptExecute(const u8 *s){executed=s;}
static void BtlController_EmitSetMonData(int a,int b,int c,int d,void *p){(void)a;(void)b;(void)c;(void)d;(void)p;}
static void MarkBattlerForControllerExec(int b){(void)b;}
'''
body='\n\n'.join(functions)+'\n'+enum
body+='\nstatic void SourceResidual(void){switch(gBattleStruct->turnEffectsTracker){\n'+nightmare+curse+yawn+'\n}}\n'
body+='\nstatic void SourceAbsentGate(void){\n'+absent+'\n}\n'
run=r'''
static void Reset(void){
 memset(gBattleMons,0,sizeof gBattleMons);memset(gDisableStructs,0,sizeof gDisableStructs);memset(&bs,0,sizeof bs);
 memset(gSpecialStatuses,0,sizeof gSpecialStatuses);memset(gProtectStructs,0,sizeof gProtectStructs);memset(gStatuses3,0,sizeof gStatuses3);
 gBattlerAttacker=gBattlerTarget=gActiveBattler=gBattleMoveDamage=gHpDealt=effect=randomValue=randomCalls=gHitMarker=gAbsentBattlerFlags=0;gBattlescriptCurrInstr=script;
 for(int i=0;i<4;i++){gBattleMons[i].hp=31;gBattleMons[i].maxHP=100;}
}
static void Hp(void){script[1]=BS_ATTACKER;gBattlescriptCurrInstr=script;Cmd_datahpupdate();}
int main(void){int v[16];while(scanf("%d",&v[0])==1){for(int i=1;i<16;i++)if(scanf("%d",&v[i])!=1)return 2;Reset();
 if(v[0]==0){
  gBattlerAttacker=v[1];gBattlerTarget=v[2];gBattleMons[v[1]].maxHP=v[3];gBattleMons[v[1]].hp=v[4];gBattleMons[v[2]].status2=v[5]?STATUS2_CURSED:0;
  Cmd_cursetarget();int failed=gBattlescriptCurrInstr==failScript,damage=gBattleMoveDamage;
  if(!failed){gHitMarker=HITMARKER_IGNORE_SUBSTITUTE;Hp();}
  printf("%d %d %d %d\n",failed,damage,gBattleMons[v[1]].hp,!!(gBattleMons[v[2]].status2&STATUS2_CURSED));
 }else if(v[0]==1){
  gBattlerTarget=v[1];gStatuses3[v[1]]=STATUS3_YAWN_TURN(v[2]);gBattleMons[v[1]].status1=v[3];Cmd_setyawn();
  printf("%d %d\n",gBattlescriptCurrInstr==failScript,(gStatuses3[v[1]]&STATUS3_YAWN)>>11);
 }else if(v[0]==2){
  gActiveBattler=gBattlerAttacker=v[1];gBattleMons[v[1]].maxHP=v[2];gBattleMons[v[1]].hp=v[3];gBattleMons[v[1]].status2=v[4]?STATUS2_NIGHTMARE:0;gBattleMons[v[1]].status1=v[5];gAbsentBattlerFlags=v[6];
  bs.turnEffectsTracker=ENDTURN_NIGHTMARES;SourceAbsentGate();if(!bs.turnEffectsBattlerId)SourceResidual();int damage=gBattleMoveDamage;
  if(effect){gHitMarker=HITMARKER_IGNORE_SUBSTITUTE|HITMARKER_PASSIVE_DAMAGE|HITMARKER_SKIP_DMG_TRACK;Hp();}
  printf("%d %d %d %d\n",effect,damage,gBattleMons[v[1]].hp,!!(gBattleMons[v[1]].status2&STATUS2_NIGHTMARE));
 }else if(v[0]==3){
  gActiveBattler=gBattlerAttacker=v[1];gBattleMons[v[1]].maxHP=v[2];gBattleMons[v[1]].hp=v[3];gBattleMons[v[1]].status2=v[4]?STATUS2_CURSED:0;gAbsentBattlerFlags=v[5];
  bs.turnEffectsTracker=ENDTURN_CURSE;SourceAbsentGate();if(!bs.turnEffectsBattlerId)SourceResidual();int damage=gBattleMoveDamage;
  if(effect){gHitMarker=HITMARKER_IGNORE_SUBSTITUTE|HITMARKER_PASSIVE_DAMAGE|HITMARKER_SKIP_DMG_TRACK;Hp();}
  printf("%d %d %d\n",effect,damage,gBattleMons[v[1]].hp);
 }else if(v[0]==4){
  gActiveBattler=gBattlerAttacker=v[1];gStatuses3[v[1]]=STATUS3_YAWN_TURN(v[2])|STATUS3_UNDERGROUND;gBattleMons[v[1]].status1=v[3];gBattleMons[v[1]].ability=v[4];gAbsentBattlerFlags=v[5];randomValue=v[6];gBattleMons[3].status2=v[7]?STATUS2_UPROAR:0;gDisableStructs[v[1]].rolloutTimer=4;gBattleMons[v[1]].status2|=STATUS2_MULTIPLETURNS;
  bs.turnEffectsTracker=ENDTURN_YAWN;SourceAbsentGate();if(!bs.turnEffectsBattlerId)SourceResidual();
  printf("%d %d %u %d %d %d\n",effect,(gStatuses3[v[1]]&STATUS3_YAWN)>>11,gBattleMons[v[1]].status1,randomCalls,gDisableStructs[v[1]].rolloutTimer,!!(gStatuses3[v[1]]&STATUS3_UNDERGROUND));
 }else if(v[0]==5){
  gActiveBattler=v[1];gCurrentMove=1;gBattleMoves[1].effect=v[2]?EFFECT_BATON_PASS:0;gStatuses3[v[1]]=STATUS3_YAWN_TURN(2);gBattleMons[v[1]].status2=STATUS2_CURSED|STATUS2_NIGHTMARE;
  SwitchInClearSetData();printf("%d %d %d\n",!!(gBattleMons[v[1]].status2&STATUS2_CURSED),!!(gBattleMons[v[1]].status2&STATUS2_NIGHTMARE),(gStatuses3[v[1]]&STATUS3_YAWN)>>11);
 }else return 3;
 }return 0;}
'''
(out / 'oracle.c').write_text(head + '\n' + body + run)
compile_result = subprocess.run(['cc', '-std=c11', '-Wall', '-Wextra', '-Werror', '-Wno-sign-compare', '-O0', str(out / 'oracle.c'), '-o', str(out / 'oracle')], text=True, capture_output=True)
(out / 'compile.log').write_text(compile_result.stdout + compile_result.stderr)
assert compile_result.returncode == 0, compile_result.stderr

groups={}
def add(group, values):groups.setdefault(group,[]).append(values+[0]*(16-len(values)))
for a,b in [(0,3),(2,1),(1,2),(3,0)]:
 for maxhp,hp in [(1,1),(7,7),(99,49),(100,100),(100,1)]:
  for duplicate in [0,1]:add('curseApplication',[0,a,b,maxhp,hp,duplicate])
for a,count,status in itertools.product(range(4),[0,1,2],[0,3,8]):add('yawnApplication',[1,a,count,status])
for a,maxhp,hp in [(0,1,1),(1,7,7),(2,99,80),(3,100,1),(3,100,0)]:
 for flag,status,absentFlag in itertools.product([0,1],[0,3],[0,1<<a]):add('nightmareResidual',[2,a,maxhp,hp,flag,status,absentFlag])
 for flag,absentFlag in itertools.product([0,1],[0,1<<a]):add('curseResidual',[3,a,maxhp,hp,flag,absentFlag])
for a in range(4):
 for count,status,ability,absentFlag,random,uproar in [(0,0,0,0,0,0),(2,0,0,0,0,0),(1,0,0,0,0,0),(1,0,0,0,3,0),(1,8,0,0,1,0),(1,0,72,0,1,0),(1,0,15,0,1,0),(1,0,0,1<<a,1,0),(1,0,0,0,1,1),(1,0,43,0,2,1)]:add('yawnResidual',[4,a,count,status,ability,absentFlag,random,uproar])
for a,baton in itertools.product(range(4),[0,1]):add('switchTransfer',[5,a,baton])
inputs=[r for rows in groups.values() for r in rows]
result=subprocess.run([str(out/'oracle')],input=''.join(' '.join(map(str,r))+'\n' for r in inputs),text=True,capture_output=True,check=True)
output=[list(map(int,line.split())) for line in result.stdout.splitlines()];assert len(output)==len(inputs)
offset=0;vectors={}
for name,rows in groups.items():
 vectors[name]={'rows':[{'input':r,'output':o} for r,o in zip(rows,output[offset:offset+len(rows)],strict=True)]};offset+=len(rows)
payload={'sourceCommit':PIN,'sourceFiles':pins,'extractions':extractions,'groups':vectors,'harnessNotes':'Verbatim pinned C commands and residual cases; inert controller callbacks, inspected script-order HP updates. C flags are sampled before faint cleanup; full C# turns verify faint/reward/replacement owners.'}
(out/'source-vectors.json').write_text(json.dumps(payload,indent=2)+'\n');json.loads((out/'source-vectors.json').read_text());print(json.dumps({'totalVectors':len(inputs),'jsonParsed':True}))

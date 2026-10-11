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
         'include/battle_controllers.h']
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

functions = [function('src/battle_script_commands.c', 'static void ' + n + '(void)')
             for n in ['Cmd_setseeded', 'Cmd_trysetroots', 'Cmd_datahpupdate', 'Cmd_manipulatedamage']]
functions.append(function('src/battle_main.c', 'void SwitchInClearSetData(void)'))
enum = span('src/battle_util.c', 'enum\n{\n    ENDTURN_INGRAIN,', '\nu8 DoBattlerEndTurnEffects', 'end-turn enum')
ingrain = span('src/battle_util.c', '            case ENDTURN_INGRAIN:', '            case ENDTURN_ABILITIES:', 'Ingrain residual case')
leech = span('src/battle_util.c', '            case ENDTURN_LEECH_SEED:', '            case ENDTURN_POISON:', 'Leech Seed residual case')
absent = span('src/battle_util.c', '        if (gAbsentBattlerFlags & gBitTable[gActiveBattler])', '        else\n', 'end-turn absent gate')

script_regions = [
    ('LeechSeedApplication', 'BattleScript_EffectLeechSeed::', 'BattleScript_EffectSplash::'),
    ('IngrainApplication', 'BattleScript_EffectIngrain::', 'BattleScript_EffectSuperpower::'),
    ('LeechSeedResidual', 'BattleScript_LeechSeedTurnDrain::', 'BattleScript_BideStoringEnergy::'),
    ('IngrainResidual', 'BattleScript_IngrainTurnHeal::', 'BattleScript_PrintMonIsRooted::'),
    ('BatonPass', 'BattleScript_EffectBatonPass::', 'BattleScript_EffectRapidSpin::'),
    ('Roar', 'BattleScript_EffectRoar::', 'BattleScript_EffectMultiHit::'),
    ('NightmareFutureGap', 'BattleScript_EffectNightmare::', 'BattleScript_EffectMinimize::'),
    ('YawnFutureGap', 'BattleScript_EffectYawn::', 'BattleScript_PrintBankAbilityMadeIneffective::'),
    ('GhostCurseFutureGap', 'BattleScript_GhostCurse::', 'BattleScript_EffectProtect::'),
]
evidence = {}
for name, start, end in script_regions:
    evidence[name] = span('data/battle_scripts_1.s', start, end, name)
assert 'tryfaintmon BS_ATTACKER\n\ttryfaintmon BS_TARGET' in evidence['LeechSeedResidual']
assert evidence['LeechSeedResidual'].index('datahpupdate BS_ATTACKER') < evidence['LeechSeedResidual'].index('copyword gBattleMoveDamage, gHpDealt') < evidence['LeechSeedResidual'].index('jumpifability BS_ATTACKER, ABILITY_LIQUID_OOZE') < evidence['LeechSeedResidual'].index('datahpupdate BS_TARGET')
assert 'accuracycheck' not in evidence['IngrainApplication']
evidence['NightmareResidualFutureGap'] = span('src/battle_util.c', '            case ENDTURN_NIGHTMARES:', '            case ENDTURN_CURSE:', 'Nightmare residual future gap')
evidence['YawnResidualFutureGap'] = span('src/battle_util.c', '            case ENDTURN_YAWN:', '            case ENDTURN_BATTLER_COUNT:', 'Yawn residual future gap')
evidence['UproarFutureGap'] = function('src/battle_script_commands.c', 'bool8 UproarWakeUpCheck(u8 battlerId)')
evidence['SeedData'] = span('src/data/battle_moves.h', '    [MOVE_LEECH_SEED] =', '    [MOVE_GROWTH] =', 'Leech Seed move data')
evidence['RootData'] = span('src/data/battle_moves.h', '    [MOVE_INGRAIN] =', '    [MOVE_SUPERPOWER] =', 'Ingrain move data')
(out / 'source-excerpts.txt').write_text('\n\n'.join('=== ' + name + ' ===\n' + value for name, value in evidence.items()))
(out / 'source-script-order.json').write_text(json.dumps({
    'sourceCommit': PIN,
    'regions': {name: [line.strip() for line in value.splitlines() if line.startswith('\t')]
                for name, value in evidence.items() if name in {x[0] for x in script_regions}},
}, indent=2) + '\n')
json.loads((out / 'source-script-order.json').read_text())

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
struct DisableStruct {int substituteHP,battlerWithSureHit,perishSongTimer,perishSongTimerStartValue,battlerPreventingEscape,isFirstTurn;} gDisableStructs[4];
struct Battle {int dynamicMoveType,turnEffectsTracker,turnEffectsBattlerId;u8 wrappedBy[4],lastTakenMove[8],lastTakenMoveFrom[32];u16 choicedMove[4];} bs,*gBattleStruct=&bs;
struct Damage {int dmg,physicalDmg,specialDmg,physicalBattlerId,specialBattlerId;} gSpecialStatuses[4],gProtectStructs[4];
struct {int animArg1,animArg2;} gBattleScripting;
struct {int effect,type;} gBattleMoves[400];
struct Flags {int flags[4];} flagData;
struct Resources {struct Flags *flags;} resources={&flagData},*gBattleResources=&resources;
int gActionSelectionCursor[4],gMoveSelectionCursor[4],gLastMoves[4],gLastLandedMoves[4],gLastHitByType[4],gLastResultingMoves[4],gLastPrintedMoves[4],gLastHitBy[4];
int gBattlerAttacker,gBattlerTarget,gActiveBattler,gCurrentMove,gBattlersCount=4,gBattleMoveDamage,gHpDealt,gMoveResultFlags,gBattleControllerExecFlags,effect;
u32 gStatuses3[4],gHitMarker,gAbsentBattlerFlags,gBitTable[4]={1,2,4,8},gTakenDmg[4];
int gTakenDmgByBattler[4],gBattleCommunication[8];
u8 script[32],failScript[8],subScript[8],ingrainScript[8],seedScript[8];
const u8 *gBattlescriptCurrInstr=script,*BattleScript_SubstituteFade=subScript,*BattleScript_IngrainTurnHeal=ingrainScript,*BattleScript_LeechSeedTurnDrain=seedScript,*executed;
static int GetBattlerForBattleScript(int b){return b==BS_ATTACKER?gBattlerAttacker:gBattlerTarget;}
static void BattleScriptPushCursor(void){}
static void BattleScriptExecute(const u8 *s){executed=s;}
static void BtlController_EmitSetMonData(int a,int b,int c,int d,void *p){(void)a;(void)b;(void)c;(void)d;(void)p;}
static void MarkBattlerForControllerExec(int b){(void)b;}
'''
body = '\n\n'.join(functions) + '\n' + enum
body += '\nstatic void SourceResidual(void){switch(gBattleStruct->turnEffectsTracker){\n' + ingrain + leech + '\n}}\n'
body += '\nstatic void SourceAbsentGate(void){\n' + absent + '\n}\n'
run = r'''
static void Reset(void){
 memset(gBattleMons,0,sizeof gBattleMons);memset(gDisableStructs,0,sizeof gDisableStructs);memset(&bs,0,sizeof bs);
 memset(gSpecialStatuses,0,sizeof gSpecialStatuses);memset(gProtectStructs,0,sizeof gProtectStructs);
 memset(gStatuses3,0,sizeof gStatuses3);memset(gBattleCommunication,0,sizeof gBattleCommunication);
 memset(gTakenDmg,0,sizeof gTakenDmg);memset(gTakenDmgByBattler,0,sizeof gTakenDmgByBattler);
 for(int i=0;i<4;i++){gBattleMons[i].hp=31;gBattleMons[i].maxHP=100;}
 gBattlerAttacker=gBattlerTarget=gActiveBattler=gCurrentMove=gBattleMoveDamage=gHpDealt=gMoveResultFlags=gBattleControllerExecFlags=effect=0;
 gHitMarker=gAbsentBattlerFlags=0;gBattlescriptCurrInstr=script;executed=0;
}
static void Hp(int who){script[1]=who;gBattlescriptCurrInstr=script;Cmd_datahpupdate();}
int main(void){int v[16];while(scanf("%d",&v[0])==1){for(int i=1;i<16;i++)if(scanf("%d",&v[i])!=1)return 2;Reset();
 if(v[0]==0){
  gBattlerAttacker=v[1];gBattlerTarget=v[2];gBattleMons[v[2]].type1=v[3];gBattleMons[v[2]].type2=v[4];gMoveResultFlags=v[5];gStatuses3[v[2]]=v[6];
  Cmd_setseeded();printf("%u %d %d\n",gStatuses3[v[2]],gMoveResultFlags,gBattleCommunication[MULTISTRING_CHOOSER]);
 }else if(v[0]==1){
  gBattlerAttacker=v[1];gStatuses3[v[1]]=v[2];Cmd_trysetroots();printf("%u %d\n",gStatuses3[v[1]],gBattlescriptCurrInstr==failScript);
 }else if(v[0]==2){
  int donor=v[1],receiver=v[2],actual=0,nominal=0,receiverDamage=0;
  gActiveBattler=gBattlerAttacker=donor;gBattleMons[donor].maxHP=v[3];gBattleMons[donor].hp=v[4];gBattleMons[receiver].maxHP=v[5];gBattleMons[receiver].hp=v[6];
  gBattleMons[donor].ability=v[7]?ABILITY_LIQUID_OOZE:0;gAbsentBattlerFlags=v[8];gStatuses3[donor]=v[9]?(STATUS3_LEECHSEED|receiver):0;
  gBattleMons[donor].status2=v[10]?STATUS2_SUBSTITUTE:0;gBattleMons[receiver].status2=v[11]?STATUS2_SUBSTITUTE:0;gDisableStructs[donor].substituteHP=gDisableStructs[receiver].substituteHP=20;
  gBattleStruct->turnEffectsTracker=ENDTURN_LEECH_SEED;SourceAbsentGate();if(!bs.turnEffectsBattlerId)SourceResidual();
  if(effect){nominal=gBattleMoveDamage;gHitMarker=HITMARKER_IGNORE_SUBSTITUTE|HITMARKER_PASSIVE_DAMAGE|HITMARKER_SKIP_DMG_TRACK;Hp(BS_ATTACKER);actual=gHpDealt;gBattleMoveDamage=gHpDealt;
   if(gBattleMons[gBattlerAttacker].ability!=ABILITY_LIQUID_OOZE){script[1]=DMG_CHANGE_SIGN;gBattlescriptCurrInstr=script;Cmd_manipulatedamage();}
   receiverDamage=gBattleMoveDamage;gHitMarker|=HITMARKER_IGNORE_SUBSTITUTE|HITMARKER_PASSIVE_DAMAGE;Hp(BS_TARGET);
  }
  printf("%d %d %d %d %d %d %d %d %d %d %d %d %d %d %d\n",effect,gBattlerTarget,nominal,actual,receiverDamage,gBattleMons[donor].hp,gBattleMons[receiver].hp,gHpDealt,effect&&gBattleMons[donor].hp==0,effect&&gBattleMons[receiver].hp==0,gDisableStructs[donor].substituteHP,gDisableStructs[receiver].substituteHP,gBattleMons[0].hp,gBattleMons[1].hp,bs.turnEffectsBattlerId);
 }else if(v[0]==3){
  gActiveBattler=gBattlerAttacker=v[1];gBattleMons[v[1]].maxHP=v[2];gBattleMons[v[1]].hp=v[3];gStatuses3[v[1]]=v[4]?STATUS3_ROOTED:0;gAbsentBattlerFlags=v[5];gBattleMons[v[1]].status2=v[6]?STATUS2_SUBSTITUTE:0;gDisableStructs[v[1]].substituteHP=20;
  bs.turnEffectsTracker=ENDTURN_INGRAIN;SourceAbsentGate();if(!bs.turnEffectsBattlerId)SourceResidual();int damage=gBattleMoveDamage;
  if(effect){gHitMarker|=HITMARKER_IGNORE_SUBSTITUTE;Hp(BS_ATTACKER);}
  printf("%d %d %d %d %d\n",effect,damage,gBattleMons[v[1]].hp,gDisableStructs[v[1]].substituteHP,bs.turnEffectsBattlerId);
 }else if(v[0]==4){
  gActiveBattler=v[1];gCurrentMove=1;gBattleMoves[1].effect=v[2]?EFFECT_BATON_PASS:0;gStatuses3[v[1]]=v[3];gBattleMons[v[1]].status2=v[4];gBattleMons[v[1]].statStages[0]=v[5];gDisableStructs[v[1]].substituteHP=17;
  SwitchInClearSetData();printf("%u %u %d %d\n",gStatuses3[v[1]],gBattleMons[v[1]].status2,gBattleMons[v[1]].statStages[0],gDisableStructs[v[1]].substituteHP);
 }else return 3;
 }return 0;}
'''
(out / 'oracle.c').write_text(head + '\n' + body + run)
compile_result = subprocess.run(['cc', '-std=c11', '-Wall', '-Wextra', '-Werror', '-Wno-sign-compare', '-O0', str(out / 'oracle.c'), '-o', str(out / 'oracle')], text=True, capture_output=True)
(out / 'compile.log').write_text(compile_result.stdout + compile_result.stderr)
assert compile_result.returncode == 0, compile_result.stderr

groups = {}
def add(group, values):
    assert len(values) <= 16
    groups.setdefault(group, []).append(values + [0] * (16 - len(values)))

# Numeric inputs are fixed source header constants; output keeps the full bitfield.
for a,b in itertools.permutations(range(4),2):
    for t1,t2,flags,status in itertools.product((0,12),(0,12),(0,1,8,32),(0,4,0x400)):
        add('seedApplication',[0,a,b,t1,t2,flags,status])
for a,roots in itertools.product(range(4),(0,0x400,4,0x404)):
    add('ingrainApplication',[1,a,roots])
for d,r in itertools.permutations(range(4),2):
    for maxhp,hp,rhp,ooze in itertools.product((1,7,8,15,16,17,99,100,255,511),(0,1,3,17),(0,1,3,99,100),(0,1)):
        if hp<=maxhp:add('leechResidual',[2,d,r,maxhp,hp,100,rhp,ooze,0,1,0,0])
    for absent,hp,rhp,seeded,sub in itertools.product((0,1<<d,1<<r,(1<<d)|(1<<r)),(0,3,100),(0,3,100),(0,1),(0,1)):
        add('leechResidualSlots',[2,d,r,100,hp,100,rhp,1,absent,seeded,sub,sub])
for a,maxhp in itertools.product(range(4),(1,7,8,15,16,17,99,100,255,511)):
    for hp,rooted,absent,sub in itertools.product(sorted({0,1,maxhp-1,maxhp}),(0,1),(0,1<<a),(0,1)):
        add('ingrainResidual',[3,a,maxhp,hp,rooted,absent,sub])
for a,baton,receiver in itertools.product(range(4),(0,1),range(4)):
    # Seeds + source, rooted, Yawn; Curse + Nightmare. The complete function decides which persist.
    add('switchTransfer',[4,a,baton,receiver|4|0x400|0x1800,0x10000000|0x08000000,10])

inputs = [row for rows in groups.values() for row in rows]
result = subprocess.run([str(out / 'oracle')], input=''.join(' '.join(map(str,row))+'\n' for row in inputs), text=True, capture_output=True, check=True)
output = [list(map(int,line.split())) for line in result.stdout.splitlines()]
assert len(output)==len(inputs)
columns = {
 'seedApplication': ['status3After','moveResultFlagsAfter','messageChooser'],
 'ingrainApplication':['status3After','failed'],
 'leechResidual':['effect','receiverSlotAfter','nominalDrain','donorHpDealt','receiverSignedDamage','donorHpAfter','receiverHpAfter','finalHpDealt','donorFaints','receiverFaints','donorSubHpAfter','receiverSubHpAfter','slot0HpAfter','slot1HpAfter','donorAbsentSkipped'],
 'ingrainResidual':['effect','signedDamage','hpAfter','subHpAfter','absentSkipped'],
 'switchTransfer':['status3After','status2After','statStageAfter','subHpAfter'],
}
columns['leechResidualSlots']=columns['leechResidual']
input_columns = {
 'seedApplication':['mode','attackerSlot','targetSlot','type1','type2','moveResultFlagsBefore','status3Before'],
 'ingrainApplication':['mode','attackerSlot','status3Before'],
 'leechResidual':['mode','donorSlot','sourceReceiverSlot','donorMaxHp','donorHp','receiverMaxHp','receiverHp','liquidOoze','absentFlags','seeded','donorSubstitute','receiverSubstitute'],
 'ingrainResidual':['mode','slot','maxHp','hp','rooted','absentFlags','substitute'],
 'switchTransfer':['mode','slot','batonPass','status3Before','status2Before','statStageBefore'],
}
input_columns['leechResidualSlots']=input_columns['leechResidual']
offset=0
vectors={}
for name,rows in groups.items():
    vectors[name]=dict(inputColumns=input_columns[name], outputColumns=columns[name],
                       rows=[{'input':r[:len(input_columns[name])],'output':o} for r,o in zip(rows,output[offset:offset+len(rows)],strict=True)])
    offset+=len(rows)
payload={'sourceCommit':PIN,'sourceFiles':pins,'extractions':extractions,'groups':vectors,
         'harnessNotes':'Unchanged complete C functions and residual cases. Controller/render callbacks are inert; host driver follows inspected assembly command order for Leech donor/copy/sign/receiver HP operations. Faint flags are observations at the source-ordered tryfaint positions, not a full faint/replacement engine.'}
(out / 'source-vectors.json').write_text(json.dumps(payload,indent=2)+'\n')
parsed=json.loads((out / 'source-vectors.json').read_text())
assert parsed['sourceCommit']==PIN and sum(len(g['rows']) for g in parsed['groups'].values())==len(inputs)
assert len(parsed['sourceFiles'])==len(files)
summary={'sourceCommit':PIN,'counts':{n:len(g['rows']) for n,g in parsed['groups'].items()},'totalVectors':len(inputs),'jsonParsed':True}
(out / 'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
print(json.dumps(summary))

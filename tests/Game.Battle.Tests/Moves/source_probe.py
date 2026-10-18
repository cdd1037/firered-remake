from pathlib import Path as _ReferencePath
import sys as _reference_sys
_reference_sys.path.insert(0, str(next(p for p in _ReferencePath(__file__).resolve().parents if (p / 'Reference/reference_c.py').is_file()) / 'Reference'))
from reference_c import compile_c, verify_reference
import hashlib,json,pathlib,subprocess,sys
import atexit
import tempfile
fixtures_only = "--fixtures-only" in sys.argv
arguments = [argument for argument in sys.argv[1:] if argument != "--fixtures-only"]
if len(arguments) != 2:
    raise SystemExit("Usage: source_probe.py PINNED_REFERENCE OUTPUT_DIRECTORY [--fixtures-only]")
ref, fixture_output = (pathlib.Path(argument).resolve() for argument in arguments)
fixture_output.mkdir(parents=True, exist_ok=True)
if fixtures_only:
    temporary_build = tempfile.TemporaryDirectory(prefix="firered-moves-reference-")
    atexit.register(temporary_build.cleanup)
    out = pathlib.Path(temporary_build.name)
else:
    out = fixture_output

pin='037335f4c725d7c9aecdac87066f2002b4bd7e14'
verify_reference(ref)
src=(ref/'src/battle_script_commands.c').read_text()
trainer=(ref/'src/data/trainer_parties.h').read_text().split('sParty_TwinsEliAnne[] = {',1)[1].split('};',1)[0]
assert trainer.count('.lvl = 22')==2 and trainer.count('.iv = 0')==2 and 'SPECIES_CLEFAIRY' in trainer and 'SPECIES_JIGGLYPUFF' in trainer
util=(ref/'src/battle_util.c').read_text()
assert '//CancelMultiTurnMoves(gBattlerAttacker);' in util
assert 'else if (--gDisableStructs[gActiveBattler].disableTimer == 0)' in util
assert 'CancelMultiTurnMoves(gEffectBattler);' in src

def extract(name):
 start=src.index('static void '+name+'(void)\n{');brace=src.index('{',start);n=1;i=brace+1
 while n:
  n += (src[i]=='{')-(src[i]=='}');i+=1
 return src[start:i]
functions='\n\n'.join(extract(n) for n in ['Cmd_setmultihitcounter','Cmd_disablelastusedattack','Cmd_rolloutdamagecalculation'])
head='''#include <stdio.h>
#include <stdint.h>
typedef int32_t s32; typedef uint8_t u8;
#define MAX_MON_MOVES 4
#define MOVE_NONE 0
#define MOVE_RESULT_NO_EFFECT 1
#define STATUS2_MULTIPLETURNS 1
#define STATUS2_DEFENSE_CURL 2
#define T1_READ_PTR(x) (x)
#define PREPARE_MOVE_BUFFER(a,b) ((void)0);
static uint32_t seed;static int draws;
static uint16_t Random(void){seed=seed*1103515245u+24691u;draws++;return seed>>16;}
struct Mon{int moves[4],pp[4],status2;}gBattleMons[4];
struct Disable{int disabledMove,disableTimer,disableTimerStartValue,rolloutTimer,rolloutTimerStartValue;}gDisableStructs[4];
struct Move{int power;}gBattleMoves[355];
static unsigned char script[100],*gBattlescriptCurrInstr=script,*BattleScript_MoveMissedPause=script;
static int gBattlerTarget=1,gBattlerAttacker=0,gLastMoves[4],gMoveResultFlags,gDynamicBasePower,gCurrentMove=205,gLockedMoves[4],gMultiHitCounter;
static void CancelMultiTurnMoves(int i){gBattleMons[i].status2&=~STATUS2_MULTIPLETURNS;gDisableStructs[i].rolloutTimer=0;}
'''
main='''
int main(void){
 printf("{\\\"rollout\\\":[");gBattleMoves[205].power=30;
 for(int i=0;i<5;i++){gBattlescriptCurrInstr=script;Cmd_rolloutdamagecalculation();printf("%s{\\\"power\\\":%d,\\\"remaining\\\":%d}",i?",":"",gDynamicBasePower,gDisableStructs[0].rolloutTimer);}
 printf("],\\\"disable\\\":[");gBattleMons[1].moves[0]=1;gBattleMons[1].pp[0]=35;gLastMoves[1]=1;
 for(int s=0;s<48;s++){seed=s;draws=0;Random();gDisableStructs[1].disabledMove=0;gBattlescriptCurrInstr=script;Cmd_disablelastusedattack();printf("%s{\\\"seed\\\":%d,\\\"turns\\\":%d,\\\"finalSeed\\\":%u}",s?",":"",s,gDisableStructs[1].disableTimer,seed);}
 printf("],\\\"multihit\\\":[");
 for(int s=0;s<48;s++){seed=s;draws=0;Random();gBattlescriptCurrInstr=script;script[1]=0;Cmd_setmultihitcounter();int hits=gMultiHitCounter;for(int i=0;i<hits;i++){Random();Random();}Random();printf("%s{\\\"seed\\\":%d,\\\"hits\\\":%d,\\\"finalSeed\\\":%u,\\\"draws\\\":%d}",s?",":"",s,hits,seed,draws);}
 printf("]}\\n");return 0;
}
'''
# The independently assembled harness executes verbatim pinned command bodies. Accuracy/critical/damage/secondary RNG calls reflect the source multi-hit script.
(out/'oracle.c').write_text(head+functions+main)
compile_c(['cc','-std=c99','-Wall','-Wextra','-O0',str(out/'oracle.c'),'-o',str(out/'oracle')])
v=json.loads(subprocess.check_output([str(out/'oracle')],text=True));v['sourceCommit']=pin;v['rolloutFiveTurnSeed']=31
v['sourceFiles']={p:hashlib.sha256((ref/p).read_bytes()).hexdigest() for p in ['src/battle_script_commands.c','src/battle_util.c','src/battle_main.c','src/pokemon.c','data/battle_scripts_1.s','src/data/trainer_parties.h','src/data/pokemon/level_up_learnsets.h']}
(out/'source-vectors.json').write_text(json.dumps(v,indent=2)+'\n')
print(json.dumps({'pin':pin,'rollout':len(v['rollout']),'disable':len(v['disable']),'multihit':len(v['multihit'])}))

# Only independent numeric output is published in fixture preparation mode.
if fixtures_only:
    for name in ['source-vectors.json']:
        target = fixture_output / name
        candidate = (out / name).read_bytes()
        if target.exists() and target.read_bytes() != candidate:
            raise SystemExit("Existing reference differs; review a new candidate directory: " + str(target))
        target.write_bytes(candidate)

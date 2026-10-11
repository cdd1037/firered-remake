"""Compile no upstream scripts: extract two pinned target branches into a bounded C harness."""
import hashlib
import json
import pathlib
import subprocess
import sys

reference = pathlib.Path(sys.argv[1])
output = pathlib.Path(sys.argv[2])
pinned = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
assert subprocess.check_output(['git', '-C', str(reference), 'rev-parse', 'HEAD'], text=True).strip() == pinned
paths = ['src/battle_main.c', 'src/battle_util.c', 'src/battle_script_commands.c', 'src/pokemon.c',
         'include/constants/battle.h', 'include/battle_util.h', 'data/battle_scripts_1.s']
texts = {}
hashes = {}
for path in paths:
    actual = (reference / path).read_bytes()
    assert actual == subprocess.check_output(['git', '-C', str(reference), 'show', 'HEAD:' + path]), path
    texts[path] = actual.decode()
    hashes[path] = hashlib.sha256(actual).hexdigest()

def function(text, signature):
    start = text.index(signature + '\n{')
    pos = text.index('{', start) + 1
    depth = 1
    while depth:
        depth += (text[pos] == '{') - (text[pos] == '}')
        pos += 1
    return text[start:pos]

main = texts['src/battle_main.c']
start = main.index('    // choose target\n', main.index('static void HandleAction_UseMove(void)\n{'))
end = main.index('    gBattlescriptCurrInstr = gBattleScriptsForMoveEffects', start)
learned = main[start:end]
called = function(texts['src/battle_util.c'], 'u8 GetMoveTarget(u16 move, u8 setTarget)')
prefix = r'''
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32;
#define MOVE_TARGET_SELECTED 0
#define MOVE_TARGET_DEPENDS 1
#define MOVE_TARGET_USER_OR_SELECTED 2
#define MOVE_TARGET_RANDOM 4
#define MOVE_TARGET_BOTH 8
#define MOVE_TARGET_USER 16
#define MOVE_TARGET_FOES_AND_ALLY 32
#define MOVE_TARGET_OPPONENTS_FIELD 64
#define NO_TARGET_OVERRIDE 0
#define B_SIDE_PLAYER 0
#define BIT_SIDE 1
#define BIT_FLANK 2
#define B_POSITION_PLAYER_LEFT 0
#define B_POSITION_OPPONENT_LEFT 1
#define B_POSITION_PLAYER_RIGHT 2
#define B_POSITION_OPPONENT_RIGHT 3
#define BATTLE_TYPE_DOUBLE 1
#define ABILITY_LIGHTNING_ROD 1
#define TYPE_ELECTRIC 1
#define ABILITYEFFECT_COUNT_OTHER_SIDE 1
static const u8 gBitTable[4] = {1,2,4,8};
static const u8 gBattlerByTurnOrder[4] = {3,2,1,0};
static u8 gBattlersCount=4, gBattleTypeFlags=BATTLE_TYPE_DOUBLE;
static u8 gBattlerAttacker, gBattlerTarget, gActiveBattler, gAbsentBattlerFlags;
static u16 gChosenMove=0, gCurrentMove=0;
static struct {u16 hp; u8 ability;} gBattleMons[4];
static struct {u8 followmeTimer,followmeTarget;} gSideTimers[2];
static struct {u8 target,type,power;} gBattleMoves[1];
static struct {u8 lightningRodRedirected;} gSpecialStatuses[4];
static struct {u8 moveTarget[4];} battleStruct, *gBattleStruct=&battleStruct;
static u8 GetBattlerSide(u8 id) {return id&1;}
static u8 GetBattlerPosition(u8 id) {return id;}
static u8 GetBattlerAtPosition(u8 id) {return id;}
static u8 GetBattlerTurnOrderNum(u8 id) {return 3-id;}
static void RecordAbilityBattle(u8 id, u8 ability) {(void)id; (void)ability;}
static u8 AbilityBattleEffects(u8 effect,u8 battler,u8 ability,u8 special,u16 move) {
 (void)effect; (void)special; (void)move;
 u8 count=0;for(u8 i=0;i<4;i++)if(GetBattlerSide(i)!=GetBattlerSide(battler)&&gBattleMons[i].ability==ability)count++;
 return count;
}
static void configure(void) {
 gBattlerAttacker=actor;gAbsentBattlerFlags=absent;
 for(int i=0;i<4;i++){gBattleMons[i].hp=(hp&(1<<i))!=0;gBattleMons[i].ability=(lightning&(1<<i))!=0;gSpecialStatuses[i].lightningRodRedirected=0;}
 for(int i=0;i<2;i++){gSideTimers[i].followmeTimer=0;gSideTimers[i].followmeTarget=0;}
 gSideTimers[(actor&1)^1].followmeTimer=followTimer;gSideTimers[(actor&1)^1].followmeTarget=followTarget;
 gBattleMoves[0].target=targetMode;gBattleMoves[0].type=electric;gBattleMoves[0].power=targetMode==MOVE_TARGET_USER?0:40;
 gBattleStruct->moveTarget[actor]=selected;
}
'''
wrappers = '\nstatic int learned(void) {\n configure(); u8 side; u32 var=4;\n' + learned + '\n redirected=gSpecialStatuses[gBattlerTarget].lightningRodRedirected;return gBattlerTarget;\n}\n'
wrappers += called + '\nstatic int caller(void) {configure();int target=GetMoveTarget(0,NO_TARGET_OVERRIDE);redirected=gSpecialStatuses[target].lightningRodRedirected;return target;}\n'
harness = pathlib.Path(__file__).with_name('source_target_oracle.c').read_text()
assert harness.count('/* SOURCE_FUNCTIONS */') == 1
output.mkdir(parents=True, exist_ok=True)
source = harness.replace('/* SOURCE_FUNCTIONS */', prefix + wrappers)
(output/'source-target-extracted.c').write_text(source)
subprocess.run(['cc', '-std=c11', '-O2', '-Wall', '-Wextra', '-Werror', str(output/'source-target-extracted.c'), '-o', str(output/'source-target-oracle')], check=True)
with (output/'source-target-vectors.csv').open('w') as target:
    subprocess.run([str(output/'source-target-oracle')], stdout=target, check=True)
(output/'source-provenance.json').write_text(json.dumps({'pinnedReference':pinned, 'files':hashes,
    'targetOracle':'Exact extracted learned branch and GetMoveTarget; identity-position, reversed-order, ability-count and RNG harness stubs only.',
    'damageOracle':'Independent arithmetic transcription, neutral stages/types and no weather/items; actual species stats supplied by .NET tests.',
    'sourceTargetVectorsSha256':hashlib.sha256((output/'source-target-vectors.csv').read_bytes()).hexdigest()}, indent=2)+'\n')

#!/usr/bin/env python3
"""Extract and compile original trapping checks; do not execute reference-repo scripts."""
import hashlib, json, pathlib, re, subprocess, sys
root, output = map(pathlib.Path, sys.argv[1:])
output.mkdir(parents=True, exist_ok=True)
expected = {
    'src/battle_main.c': 'c290d325761d68c059f02dc21cc6178d4bbefa0d06ffddc8879341c090d37928',
    'src/battle_util.c': '87cf810eb2d58361d1beb1537b3bd71c9160a77c2a17d703399f6ed373bb5720',
    'data/battle_scripts_1.s': '27e9c39f3147b05e5ba2ed36ca0c89fcdc9b26718778083bd362c582de800034',
    'include/constants/abilities.h': '1c1b93de5d5ff65d8b3066e9b40229b5d30e02842e587e3851a1d708ece1292b',
    'include/constants/pokemon.h': '9cabd203100ae7246bc595b7295e7e04145f6292abfab83c3bd0597a2cfbcfb5',
    'include/constants/items.h': 'af17276346eaecc5a10ac60f14b22c33332ec8c6fb0ec82895bdc3649b678138',
    'include/constants/hold_effects.h': 'e763fdbbef19ba6999a7151094cf26734323414237ebb1afcf2f453f65c2f661',
    'include/constants/battle.h': 'b9d0493af4629d450050637e9aace63b684b282f841bed2a0d97b1d71e9d55e3',
    'include/battle_util.h': '38d93e081d1886494489c0fc44bf7d77c0753b096a29f2f327a6b2909b112370',
    'src/data/pokemon/tmhm_learnsets.h': '92bf603f90469ccae0398902db943770e2cb12cb910883b42a552cff07cff31f',
}
files = {}
for name, digest in expected.items():
    data = (root / name).read_bytes()
    assert hashlib.sha256(data).hexdigest() == digest, name
    files[name] = data.decode()
main = files['src/battle_main.c']
start = main.index('u8 IsRunningFromBattleImpossible(void)\n{')
run = main[start:main.index('\nvoid UpdatePartyOwnerOnSwitch_NonMulti', start)]
util = files['src/battle_util.c']
start = util.index('case ABILITYEFFECT_CHECK_OTHER_SIDE:')
side = util[util.index('\n', start) + 1:util.index('            break;', start)]
switch = re.search(r'else if (\(\(i = ABILITY_ON_OPPOSING_FIELD[\s\S]*?)\n                    \{', main).group(1)
script = files['data/battle_scripts_1.s']
roar = script[script.index('BattleScript_EffectRoar::'):script.index('BattleScript_EffectMultiHit::')]
commands = [line.strip().split()[0] for line in roar.splitlines()[1:] if line.strip()]
assert commands == ['attackcanceler', 'attackstring', 'ppreduce', 'jumpifability', 'jumpifstatus3', 'accuracycheck', 'accuracycheck', 'forcerandomswitch'], commands
assert 'ABILITY_SUCTION_CUPS' in roar and 'ABILITY_SHADOW_TAG' not in roar
teleport = script[script.index('BattleScript_EffectTeleport::'):script.index('BattleScript_EffectBeatUp::')]
assert teleport.index('ppreduce') < teleport.index('getifcantrunfrombattle')
assert 'STATUS3_ROOTED' in roar and roar.index('ABILITY_SUCTION_CUPS') < roar.index('STATUS3_ROOTED')
constants = ['ABILITY_RUN_AWAY', 'ABILITY_SHADOW_TAG', 'ABILITY_ARENA_TRAP', 'ABILITY_MAGNET_PULL', 'ABILITY_LEVITATE',
             'TYPE_FLYING', 'TYPE_STEEL', 'ITEM_ENIGMA_BERRY', 'HOLD_EFFECT_CAN_ALWAYS_RUN', 'BATTLE_TYPE_LINK',
             'BATTLE_TYPE_FIRST_BATTLE', 'STATUS2_ESCAPE_PREVENTION', 'STATUS2_WRAPPED', 'STATUS3_ROOTED',
             'BATTLE_RUN_SUCCESS', 'BATTLE_RUN_FAILURE', 'BATTLE_RUN_FORBIDDEN', 'ABILITYEFFECT_CHECK_FIELD_EXCEPT_BATTLER']
headers = '\n'.join(files[p] for p in ['include/constants/abilities.h', 'include/constants/pokemon.h', 'include/constants/items.h',
                'include/constants/hold_effects.h', 'include/constants/battle.h', 'include/battle_util.h'])
defs = []
for name in constants:
    match = re.search(r'^#define\s+' + name + r'\s+([^\n]+)', headers, re.M)
    assert match, name
    defs.append('#define ' + name + ' ' + match.group(1))
source = r'''
#include <stdint.h>
#include <stdio.h>
typedef uint8_t u8; typedef int32_t s32;
#define MULTISTRING_CHOOSER 0
''' + '\n'.join(defs) + r'''
struct BattleMon { int item, ability, type, hp; unsigned status2; } gBattleMons[2];
struct Enigma { int holdEffect; } gEnigmaBerries[2];
struct Scripting { int battler; } gBattleScripting;
unsigned gStatuses3[2], gBattleTypeFlags; int gActiveBattler, gPotentialItemEffectBattler, gBattlersCount=2, gLastUsedAbility, gBattleCommunication[3];
int GetBattlerSide(int i) { return i & 1; }
int ItemId_GetHoldEffect(int item) { return item == 1 ? HOLD_EFFECT_CAN_ALWAYS_RUN : 0; }
#define IS_BATTLER_OF_TYPE(b,t) (gBattleMons[b].type == (t))
int AbilityBattleEffects(int mode,int battler,int ability,int x,int y) {
  int side,i,effect=0;
''' + side + r'''
  return effect;
}
#define ABILITY_ON_OPPOSING_FIELD(b,a) AbilityBattleEffects(12,b,a,0,0)
''' + run + r'''
int main(void) {
  int a,b,t,h,item,link;
  if(scanf("%d %d %d %d %d %d",&a,&b,&t,&h,&item,&link)!=6) return 2;
  gBattleMons[0]=(struct BattleMon){.item=item,.ability=a,.type=t,.hp=1};
  gBattleMons[1]=(struct BattleMon){.ability=b,.type=0,.hp=h};
  gBattleTypeFlags=link ? BATTLE_TYPE_LINK : 0;
  int i, blocked=0;
  if ''' + switch + r''' blocked=1;
  printf("%d %d\n",IsRunningFromBattleImpossible(),blocked);
  return 0;
}
'''
(output / 'source-oracle.c').write_text(source)
subprocess.run(['cc', '-std=c99', '-O0', str(output / 'source-oracle.c'), '-o', str(output / 'source-oracle')], check=True)
(output / 'source-authentication.json').write_text(json.dumps({'sourceCommit': '037335f4c725d7c9aecdac87066f2002b4bd7e14', 'sha256': expected, 'roarCommands': commands, 'compiledOriginalRunAndSwitchChecks': True}, indent=2))
print('Authenticated source run/switch extraction and Suction Cups script order.')

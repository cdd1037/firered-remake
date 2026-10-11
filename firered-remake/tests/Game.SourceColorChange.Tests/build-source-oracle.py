#!/usr/bin/env python3
"""Build only the unchanged pinned Color Change branch; never run upstream scripts."""
import hashlib, json, pathlib, subprocess, sys
if len(sys.argv) != 3:
    raise SystemExit('usage: build-source-oracle.py PINNED_PRET_ROOT OUTPUT_DIRECTORY')
root, out = map(pathlib.Path, sys.argv[1:]); out.mkdir(parents=True, exist_ok=True)
data = (root / 'src/battle_util.c').read_bytes()
expected = '87cf810eb2d58361d1beb1537b3bd71c9160a77c2a17d703399f6ed373bb5720'
if hashlib.sha256(data).hexdigest() != expected: raise SystemExit('Pinned battle_util.c hash mismatch')
text = data.decode(); start = text.index('            case ABILITY_COLOR_CHANGE:', text.index('case ABILITYEFFECT_ON_DAMAGE:'))
end = text.index('            case ABILITY_ROUGH_SKIN:', start)
branch = text[start:end]
source = r'''
#include <stdio.h>
#define ABILITY_COLOR_CHANGE 16
#define MOVE_RESULT_NO_EFFECT 1
#define MOVE_STRUGGLE 165
#define TARGET_TURN_DAMAGED damaged
#define IS_BATTLER_OF_TYPE(b,t) (gBattleMons[b].type1 == (t) || gBattleMons[b].type2 == (t))
#define SET_BATTLER_TYPE(b,t) (gBattleMons[b].type1 = gBattleMons[b].type2 = (t))
#define PREPARE_TYPE_BUFFER(b,t) ((void)0)
#define BattleScriptPushCursor() ((void)0)
#define BattleScript_ColorChangeActivates 1
struct { int power; } gBattleMoves[166];
struct { int hp, type1, type2; } gBattleMons[1];
int main(void) {
 int moveType, noEffect, move, power, damaged, hp, type1, type2;
 while (scanf("%d %d %d %d %d %d %d %d", &moveType, &type1, &type2, &noEffect, &move, &power, &damaged, &hp) == 8) {
  int battler=0, effect=0, gMoveResultFlags=noEffect, gBattlescriptCurrInstr=0;
  gBattleMons[0].type1=type1; gBattleMons[0].type2=type2; gBattleMons[0].hp=hp; gBattleMoves[move].power=power;
  switch (ABILITY_COLOR_CHANGE) {
''' + branch + r'''
  }
  printf("%d %d %d\n", effect, gBattleMons[0].type1, gBattleMons[0].type2);
  if (effect && !gBattlescriptCurrInstr) return 3;
 }
 return 0;
}
'''
p = out / 'original-color-change-oracle.c'; p.write_text(source)
subprocess.run(['cc', '-std=c11', '-O2', '-Wall', '-Wextra', '-Werror', str(p), '-o', str(out/'original-color-change-oracle')], check=True)
print(json.dumps({'sourceCommit':'037335f4c725d7c9aecdac87066f2002b4bd7e14', 'sourceSha256':expected, 'branchStartLine':text[:start].count('\n')+1, 'branchEndLine':text[:end].count('\n'), 'oracleSha256':hashlib.sha256(p.read_bytes()).hexdigest()}, indent=2))

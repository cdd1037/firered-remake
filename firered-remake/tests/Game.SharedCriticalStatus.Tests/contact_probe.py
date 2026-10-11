#!/usr/bin/env python3
"""Bounded original Static command witness, plus exact Toxic lifecycle source anchors."""
import argparse, hashlib, json, subprocess, tempfile
from pathlib import Path
from source_probe import PIN, run
p=argparse.ArgumentParser();p.add_argument('--source',type=Path,required=True);p.add_argument('--report',type=Path,required=True);a=p.parse_args()
files=['src/battle_util.c']+[f'include/constants/{n}.h' for n in ['battle','battle_move_effects','battle_script_commands','pokemon','moves','abilities','battle_string_ids']]
sources={n:(a.source/n).read_text() for n in files}
assert run(['git','rev-parse','HEAD'],cwd=a.source).strip()==PIN
for n,v in sources.items():assert v==run(['git','show',f'{PIN}:{n}'],cwd=a.source),n
src=sources['src/battle_util.c']
arm=src[src.index('            case ABILITY_STATIC:'):src.index('            case ABILITY_FLAME_BODY:')]
sync=src[src.index('        case ABILITYEFFECT_SYNCHRONIZE:'):src.index('        case ABILITYEFFECT_INTIMIDATE1:')]
assert sync.count('gBattleStruct->synchronizeMoveEffect = MOVE_EFFECT_POISON;')==2
poison=src[src.index('if ((gBattleMons[gActiveBattler].status1 & STATUS1_TOXIC_POISON)'):]
poison=poison[:poison.index('break;')]
assert 'gBattleMons[gActiveBattler].status1 += STATUS1_TOXIC_TURN(1);' in poison
assert 'BtlController_EmitSetMonData' not in poison
prefix='''#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include "constants/battle.h"
#include "constants/battle_move_effects.h"
#include "constants/battle_script_commands.h"
#include "constants/pokemon.h"
#include "constants/moves.h"
#include "constants/abilities.h"
typedef uint8_t u8;typedef uint16_t u16;typedef uint32_t u32;
static struct { u16 hp; } gBattleMons[4];
static struct { unsigned confusionSelfDmg; } gProtectStructs[4];
static struct { unsigned flags; } gBattleMoves[1];
static u8 gBattlerAttacker,gBattleCommunication[16],gLastUsedAbility,move;
static u32 gMoveResultFlags,gHitMarker;
static unsigned receipt,draws,randomValue,effect;
#define TARGET_TURN_DAMAGED (receipt != 0)
static const u8 script[1]={0}, *BattleScript_ApplySecondaryEffect=script,*gBattlescriptCurrInstr;
static u16 Random(void) { draws++;return randomValue; }
static void BattleScriptPushCursor(void) {}
static void RunStatic(void) {switch(gLastUsedAbility){
'''
suffix='''}}
int main(void){for(unsigned mask=0;mask<32;mask++)for(unsigned roll=0;roll<3;roll++){
 gLastUsedAbility=ABILITY_STATIC;gBattlerAttacker=2;move=0;effect=draws=gHitMarker=0;randomValue=roll;
 gMoveResultFlags=(mask&1)?MOVE_RESULT_DOESNT_AFFECT_FOE:0;gBattleMons[2].hp=(mask&2)?0:100;
 gProtectStructs[2].confusionSelfDmg=(mask&4)!=0;receipt=(mask&8)?0:7;gBattleMoves[0].flags=(mask&16)?0:FLAG_MAKES_CONTACT;
 RunStatic();printf("{\\\"mask\\\":%u,\\\"roll\\\":%u,\\\"draws\\\":%u,\\\"effect\\\":%u}\\n",mask,roll,draws,effect);
}return 0;}
'''
# Keep the literal C strings readable instead of relying on a shell escape layer.
with tempfile.TemporaryDirectory(prefix='firered-critical-contact-') as t:
 t=Path(t);(t/'probe.c').write_text(prefix+arm+suffix);outputs=[]
 for name,flags in [('optimized',['-O2']),('ubsan',['-O1','-fsanitize=undefined','-fno-sanitize-recover=all'])]:
  run(['cc','-std=c11','-Wall','-Wextra','-Werror',*flags,'-I',str(a.source/'include'),str(t/'probe.c'),'-o',str(t/name)]);outputs.append(run([str(t/name)]))
assert outputs[0]==outputs[1]
rows=[json.loads(x) for x in outputs[0].splitlines()];assert len(rows)==96
for r in rows:assert r['draws']==int(r['mask']==0) and r['effect']==int(r['mask']==0 and r['roll']==0),r
report={'passed':True,'sourceCommit':PIN,'sourceFiles':{n:hashlib.sha256(v.encode()).hexdigest() for n,v in sources.items()},'staticSlice':hashlib.sha256(arm.encode()).hexdigest(),'synchronizeSlice':hashlib.sha256(sync.encode()).hexdigest(),'toxicResidualSlice':hashlib.sha256(poison.encode()).hexdigest(),'optimizedAndUbsanEqual':True,'vectors':rows}
a.report.write_text(json.dumps(report,separators=(',',':'))+'\n');json.loads(a.report.read_text());print(json.dumps({'passed':True,'vectors':len(rows)}))

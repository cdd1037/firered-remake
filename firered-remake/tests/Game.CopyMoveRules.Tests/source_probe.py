#!/usr/bin/env python3
"""Read-only checks of pinned source; never compiles or executes reference code."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

if len(sys.argv) != 3:
    raise SystemExit("source_probe.py REFERENCE_ROOT NEW_RESULT_JSON")
root, output = Path(sys.argv[1]), Path(sys.argv[2])
if output.exists():
    raise SystemExit("refusing to overwrite evidence")
pin = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
paths = ["src/battle_script_commands.c", "data/battle_scripts_1.s", "src/battle_controller_player.c",
         "src/battle_controller_opponent.c", "include/battle.h", "src/battle_message.c"]
texts, hashes, checks = {}, {}, []
for path in paths:
    data = (root / path).read_bytes()
    pinned = subprocess.check_output(["git", "show", pin + ":" + path], cwd=root)
    if data != pinned:
        raise AssertionError("reference changed: " + path)
    hashes[path] = hashlib.sha256(data).hexdigest()
    texts[path] = data.decode()

def check(label, ok):
    if not ok:
        raise AssertionError(label)
    checks.append(label)

def command(name):
    return re.search(r"^static void " + name + r"\(void\)\n\{.*?^\}", texts[paths[0]], re.M | re.S).group()

def script(name):
    return re.search(r"^" + name + r"::\n.*?(?=^\w.*::|\Z)", texts[paths[1]], re.M | re.S).group()

mimic, sketch = command("Cmd_mimicattackcopy"), command("Cmd_copymovepermanently")
for label, body in [("Mimic", mimic), ("Sketch", sketch)]:
    check(label + " writes unavailable before tests", body.index("gChosenMove = MOVE_UNAVAILABLE;") < body.index("if ("))
    check(label + " excludes transformed attacker", "gBattleMons[gBattlerAttacker].status2 & STATUS2_TRANSFORMED" in body)
    check(label + " writes current PP slot", "moves[gCurrMovePos] =" in body and "pp[gCurrMovePos] =" in body)
check("Mimic uses last-moves register only", "gLastMoves[gBattlerTarget]" in mimic and "gLastPrintedMoves" not in mimic)
check("Sketch uses last-printed register only", "gLastPrintedMoves[gBattlerTarget]" in sketch and "gLastMoves[" not in sketch)
table = re.search(r"sMovesForbiddenToCopy\[\] =\n\{(.*?)MIMIC_FORBIDDEN_END", texts[paths[0]], re.S).group(1)
check("Mimic exact exclusion table prefix", re.findall(r"MOVE_\w+", table) == ["MOVE_METRONOME", "MOVE_STRUGGLE", "MOVE_SKETCH", "MOVE_MIMIC"])
check("Sketch permits Mimic and Metronome", "MOVE_MIMIC" not in sketch and "MOVE_METRONOME" not in sketch)
check("Mimic sets source slot mask", "mimickedMoves |= gBitTable[gCurrMovePos]" in mimic)
check("Mimic minimum-of-five PP", ".pp < 5" in mimic and "pp[gCurrMovePos] = 5" in mimic)
check("Sketch base PP and unchanged bonuses", "pp[gCurrMovePos] = gBattleMoves[gLastPrintedMoves[gBattlerTarget]].pp" in sketch and "movePpData.ppBonuses = gBattleMons[gBattlerAttacker].ppBonuses" in sketch)
check("Sketch transmits every live slot", "for (i = 0; i < MAX_MON_MOVES; i++)" in sketch and "movePpData.moves[i] = gBattleMons[gBattlerAttacker].moves[i]" in sketch and "REQUEST_MOVES_PP_BATTLE" in sketch)
check("Sketch leaves Mimic flags set", "mimickedMoves" not in sketch)
for path in paths[2:4]:
    writes = re.findall(r"case REQUEST_MOVES_PP_BATTLE:(.*?)break;", texts[path], re.S)
    check(path + " writes all party slots and bonuses", any("SetMonData" in block and "MON_DATA_MOVE1 + i" in block and "MON_DATA_PP1 + i" in block and "MON_DATA_PP_BONUSES" in block and "mimickedMoves" not in block for block in writes))
for label, body in [("Mimic", script("BattleScript_EffectMimic")), ("Sketch", script("BattleScript_EffectSketch"))]:
    check(label + " script PP precedes substitute", body.index("ppreduce") < body.index("STATUS2_SUBSTITUTE"))
check("Mimic source no-calculation Lock-On gate", "NO_ACC_CALC_CHECK_LOCK_ON" in script("BattleScript_EffectMimic"))
check("Sketch has no accuracy command", "accuracycheck" not in script("BattleScript_EffectSketch"))
check("Transform clears Mimic mask", "mimickedMoves = 0" in command("Cmd_transformdataexecution"))
check("PP write suppression uses permanent predicate", "if (MOVE_IS_PERMANENT(gBattlerAttacker, gCurrMovePos))" in command("Cmd_ppreduce"))
check("Permanent predicate excludes transformation and Mimic", "STATUS2_TRANSFORMED" in texts[paths[4]][:1500] and "mimickedMoves & gBitTable[moveSlot]" in texts[paths[4]][:1500])
check("Original success text symbols", 'sText_PkmnSketchedMove[] = _("{B_ATK_NAME_WITH_PREFIX} SKETCHED\\n{B_BUFF1}!")' in texts[paths[5]] and 'sText_PkmnLearnedMove2[] = _("{B_ATK_NAME_WITH_PREFIX} learned\\n{B_BUFF1}!")' in texts[paths[5]])
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps({"passed": True, "sourceCommit": pin, "checks": checks, "count": len(checks), "sha256": hashes}, indent=2) + "\n")
print(f"PASS {len(checks)} source contracts; {len(hashes)} pinned source files")

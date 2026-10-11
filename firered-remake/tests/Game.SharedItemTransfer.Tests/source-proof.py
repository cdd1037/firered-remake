#!/usr/bin/env python3
"""Compile small verbatim pinned FireRed command slices with authored host adapters.

Usage: python3 source-proof.py PRET_CHECKOUT NEW_OUTPUT_DIRECTORY
No upstream build script runs. Golden rows are authored independently; this script
never writes or updates them. This is command/state evidence, not full battle QA.
"""
import hashlib
import json
import pathlib
import subprocess
import sys

PIN = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
HERE = pathlib.Path(__file__).resolve().parent
ref, out = map(pathlib.Path, sys.argv[1:])
assert subprocess.check_output(["git", "-C", str(ref), "rev-parse", "HEAD"], text=True).strip() == PIN
out.mkdir(parents=True, exist_ok=False)
sources, slices = {}, []


def read(name):
    data = (ref / name).read_bytes()
    committed = subprocess.check_output(["git", "-C", str(ref), "show", f"{PIN}:{name}"])
    assert data == committed, f"dirty source: {name}"
    sources[name] = hashlib.sha256(data).hexdigest()
    return data.decode()


def block(name, start, end, *, last=False):
    text = read(name)
    begin = text.rindex(start) if last else text.index(start)
    finish = text.index(end, begin)
    result = text[begin:finish]
    slices.append(dict(file=name, startLine=text.count("\n", 0, begin) + 1,
                       endLine=text.count("\n", 0, finish), sha256=hashlib.sha256(result.encode()).hexdigest()))
    return result


commands = "src/battle_script_commands.c"
headers = ("global", "battle", "battle_script_commands", "items", "moves", "abilities",
           "hold_effects", "trainers", "opponents", "pokemon", "battle_string_ids")
# Copy authenticated constants into the output, avoiding build-time source changes.
for name in headers:
    path = f"include/constants/{name}.h"
    target = out / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(read(path))
includes = "".join(f'#include "constants/{name}.h"\n' for name in headers)
mail = block("include/mail.h", "#define IS_ITEM_MAIL", "#define FIRST_MAIL_IDX")
controller_enum = block("include/battle_controllers.h", "enum\n{", "};") + "};\n"
scaffold = (HERE / "source-command-harness.h").read_text()


def controller(side):
    name = f"src/battle_controller_{side}.c"
    body = block(name, "    case REQUEST_HELDITEM_BATTLE:", "    case REQUEST_MOVES_PP_BATTLE:", last=True)
    return f"static void Source{side.title()}Controller(int request, int monId) {{ switch(request) {{\n{body} default: assert(0); }} }}\n"


guard = block(commands, "    if (gBattleMons[gEffectBattler].hp == 0", "    if (gBattleCommunication[MOVE_EFFECT_BYTE] <= PRIMARY_STATUS_MOVE_EFFECT)")
steal = block(commands, "            case MOVE_EFFECT_STEAL_ITEM:", "            case MOVE_EFFECT_PREVENT_ESCAPE:")
knock = block(commands, "            case MOVE_EFFECT_KNOCK_OFF:", "            case MOVE_EFFECT_SP_ATK_TWO_DOWN:")
effect = "static void SourceEffect(int effect) { int side; u8 affectsUser = 0; gEffectBattler = gBattlerTarget; gBattleCommunication[MOVE_EFFECT_BYTE] = effect;\n" + guard + "switch(effect) {\n" + steal + knock + "default: assert(0); } }\n"
trick = block(commands, "static void Cmd_tryswapitems(void)\n{", "// Role Play")
recycle = block(commands, "static void Cmd_tryrecycleitem(void)\n{", "static void Cmd_settypetoterrain(void)\n{")
remove = block(commands, "static void Cmd_removeitem(void)\n{", "static void Cmd_atknameinbuff1(void)\n{")
ending = block(commands, "        case MOVEEND_CHOICE_MOVE:", "        case MOVEEND_ITEM_EFFECTS_ALL:")
moveend = "static void SourceMoveEnd(void) { s32 i; u16 *choicedMoveAtk = &gBattleStruct->choicedMove[gBattlerAttacker]; u8 holdEffectAtk = gBattleMons[gBattlerAttacker].item == ITEM_CHOICE_BAND ? HOLD_EFFECT_CHOICE_BAND : 0; for (int state = MOVEEND_CHOICE_MOVE; state <= MOVEEND_CHANGED_ITEMS; state++) { switch(state) {\n" + ending + "} } }\n"
switchmask = block(commands, "    // check knocked off item", "    if (gBattleMoves[gCurrentMove].effect == EFFECT_BATON_PASS)")
switchfunc = "static void SourceSwitchMask(void) { s32 i;\n" + switchmask + "}\n"

# Structural assertions document bounds the command probe does not emulate.
scripts = read("data/battle_scripts_1.s")
assert "BattleScript_EffectThief::\n\tsetmoveeffect MOVE_EFFECT_STEAL_ITEM\n\tgoto BattleScript_EffectHit" in scripts
assert "BattleScript_EffectKnockOff::\n\tsetmoveeffect MOVE_EFFECT_KNOCK_OFF\n\tgoto BattleScript_EffectHit" in scripts
assert "\tjumpifstatus2 BS_TARGET, STATUS2_SUBSTITUTE, BattleScript_ButItFailed\n\taccuracycheck BattleScript_ButItFailed, ACC_CURR_MOVE\n\ttryswapitems" in scripts
moves = read("src/data/battle_moves.h")
for name in ("THIEF", "COVET"):
    entry = moves.split(f"[MOVE_{name}] =", 1)[1].split("},", 1)[0]
    assert ".effect = EFFECT_THIEF" in entry and ".secondaryEffectChance = 100" in entry
assert "FLAG_MAKES_CONTACT" not in moves.split("[MOVE_COVET] =", 1)[1].split("},", 1)[0]
main = read("src/battle_main.c")
switchclear = block("src/battle_main.c", "void SwitchInClearSetData(void)\n{", "void FaintClearSetData(void)\n{")
assert "usedHeldItems" not in switchclear and "changedItems" not in switchclear and "knockedOffMons" not in switchclear
assert "*((u8 *)gBattleStruct->usedHeldItems + i) = ITEM_NONE;" in main
assert "dataPtr = (u8 *)&gWishFutureKnock;" in main
battle_structs = read("include/battle.h")
assert "u16 usedHeldItems[MAX_BATTLERS_COUNT];" in battle_structs and "u8 knockedOffMons[2];" in battle_structs

code = includes + mail + controller_enum + scaffold + controller("player") + controller("opponent") + effect + trick + recycle + remove + moveend + switchfunc
(out / "source-commands.c").write_text(code)
golden = json.loads((HERE / "source-golden.json").read_text())
assert golden["rows"], "empty golden"
answers = []
for name, flags in (("optimized", ["-O2"]), ("ubsan", ["-O1", "-fsanitize=undefined", "-fno-sanitize-recover=all"])):
    subprocess.run(["cc", "-std=c11", "-Wall", "-Wextra", "-Werror", *flags,
                    "-I", str(out / "include"), str(out / "source-commands.c"), "-o", str(out / name)], check=True)
    output = subprocess.check_output([str(out / name)], text=True)
    (out / f"{name}.jsonl").write_text(output)
    rows = [json.loads(line) for line in output.splitlines()]
    assert rows == golden["rows"], f"{name} differs from authored golden; retain output and inspect"
    answers.append(rows)
assert answers[0] == answers[1]
report = dict(commit=PIN, sourceFiles=sources, extractedSlices=slices,
              authoredHarnessSha256=hashlib.sha256(scaffold.encode()).hexdigest(),
              authoredGoldenSha256=hashlib.sha256((HERE / "source-golden.json").read_bytes()).hexdigest(),
              rows=len(answers[0]), optimizedAndUbsanAgree=True,
              scope="Exact steal/knock command cases, HP/substitute guard, Trick/Recycle/remove commands, choice+changed-items phases, player/opponent item controller cases and switch knocked-item mask. Authored host state, immediate controller completion and fixtures. No full script VM, damage/accuracy, presentation, save, switch lifecycle or excluded battle-mode claims.")
(out / "source-report.json").write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps(dict(rows=report["rows"], optimizedAndUbsanAgree=True, sourceReport=str(out / "source-report.json"))))

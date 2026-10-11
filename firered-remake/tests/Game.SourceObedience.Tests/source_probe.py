#!/usr/bin/env python3
"""Compile unchanged pinned FireRed obedience functions against narrow test stubs.

Usage: python3 source_probe.py PATH_TO_PINNED_PRET OUTPUT_DIRECTORY

Uses only git show and the system C compiler. It never runs repository scripts,
the game, a ROM, or the battle-script interpreter. JSON separates function output
from static evidence about later scripts, RNG draws, and move-history flags.
"""

import argparse
import hashlib
import json
import pathlib
import re
import subprocess


COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
HEADERS = [
    "constants/battle.h", "constants/abilities.h", "constants/moves.h",
    "constants/species.h", "constants/pokemon.h", "constants/global.h",
    "constants/flags.h", "constants/trainers.h", "constants/opponents.h",
    "constants/battle_script_commands.h", "constants/battle_string_ids.h",
]
SOURCES = ["src/battle_util.c", "src/pokemon.c", "src/battle_script_commands.c",
           "data/battle_scripts_1.s"] + ["include/" + name for name in HEADERS]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def extract(source, signature):
    """Extract the original bytes, balancing braces outside strings/comments."""
    match = re.search(rb"(?m)^" + re.escape(signature.encode()) + rb"\s*\n\{", source)
    assert match, signature
    position = source.index(b"{", match.start())
    depth, state = 0, "code"
    while position < len(source):
        char, pair = source[position:position + 1], source[position:position + 2]
        if state == "line":
            if char == b"\n":
                state = "code"
        elif state == "comment":
            if pair == b"*/":
                state = "code"
                position += 1
        elif state in ("string", "char"):
            if char == b"\\":
                position += 1
            elif char == (b'"' if state == "string" else b"'"):
                state = "code"
        elif pair == b"//":
            state = "line"
            position += 1
        elif pair == b"/*":
            state = "comment"
            position += 1
        elif char == b'"':
            state = "string"
        elif char == b"'":
            state = "char"
        elif char == b"{":
            depth += 1
        elif char == b"}":
            depth -= 1
            if depth == 0:
                return source[match.start():position + 1]
        position += 1
    raise AssertionError("Unbalanced function: " + signature)


def name_bytes(name):
    # The probe only tests source byte equality; these are fixture bytes, not a
    # claim that ASCII is the game's displayed-name character encoding.
    assert len(name) <= 7
    return list(name.encode("ascii")) + [255] * (8 - len(name))


def vectors():
    rows = []

    def add(group, name, **changes):
        inputs = dict(
            battleTypeFlags=0, attackerSide=0, level=50, badgeFlags=0,
            species=25, modernFateful=1, otId=0x12345679, playerId=0x12345678,
            otName=name_bytes("RED"), playerName=name_bytes("RED"),
            currentMove=1, chosenMove=1, calledMove=777, currMovePos=0,
            chosenMovePos=0, moves=[1, 10, 33, 45], status1=0, status2=0,
            otherStatus2=[0, 0, 0], ability=0, battlersCount=2, target=1,
            limitedMask=0, targetResult=1, baseDamage=37, damage=-123,
            hitMarker=0, multiString=99, draws=[255, 255, 255, 3],
        )
        inputs.update(changes)
        assert all(0 <= draw <= 65535 for draw in inputs["draws"])
        assert len(inputs["draws"]) <= 16
        rows.append(dict(id=len(rows), group=group, name=name, input=inputs))

    # Each strict first-check boundary is bracketed, with level == cap also
    # checked. Badge flags use the ordinary bit for each of badges 1 through 8.
    for badges, cap in [(0, 10), (2, 30), (8, 50), (32, 70)]:
        level = cap + 1
        boundary = (cap * 256 + level + cap - 1) // (level + cap)
        add("badge", f"cap_{cap}_level_equal", badgeFlags=badges, level=cap, draws=[])
        add("badge", f"cap_{cap}_roll_below", badgeFlags=badges, level=level,
            draws=[boundary - 1])
        add("badge", f"cap_{cap}_roll_equal_or_above", badgeFlags=badges, level=level,
            draws=[boundary, 255, 255, 0])
    add("badge", "odd_badges_do_not_raise_cap", badgeFlags=0x55, level=11)
    add("badge", "highest_even_badge_wins", badgeFlags=0x2A, level=70, draws=[])
    add("badge", "eighth_badge_bypass", badgeFlags=128, level=100, draws=[])
    add("bypass", "link", battleTypeFlags=2, draws=[])
    add("bypass", "pokedude", battleTypeFlags=65536, draws=[])
    add("bypass", "opponent", attackerSide=1, draws=[])
    add("ownership", "same_id_and_name_owned", otId=0x12345678, draws=[])
    add("ownership", "high_id_bits_differ", otId=0xABCD5678)
    add("ownership", "same_id_other_name_foreign", otId=0x12345678,
        otName=name_bytes("BLUE"))
    add("ownership", "ot_prefix_is_owned_source_quirk", otId=0x12345678,
        otName=name_bytes("RE"), draws=[])
    add("ownership", "longer_ot_name_is_foreign", otId=0x12345678,
        otName=name_bytes("REDD"))
    add("fateful", "illegal_mew_ignores_owner_and_badge8", species=151,
        modernFateful=0, otId=0x12345678, badgeFlags=128, draws=[0, 0, 0])
    add("fateful", "illegal_deoxys_ignores_owner_and_badge8", species=410,
        modernFateful=0, otId=0x12345678, badgeFlags=128, draws=[0, 0, 75])
    add("fateful", "legal_mew_owned", species=151, otId=0x12345678, draws=[])
    add("fateful", "ordinary_species_ignores_fateful_bit", modernFateful=0,
        otId=0x12345678, draws=[])
    add("fateful", "illegal_mew_link_bypasses", species=151, modernFateful=0,
        battleTypeFlags=2, draws=[])
    add("asleep", "sleeping_snore_ignores_after_one_draw", currentMove=173,
        chosenMove=173, status1=3, draws=[255])
    add("asleep", "sleeping_sleep_talk_ignores_after_one_draw", currentMove=214,
        chosenMove=214, status1=3, draws=[255])
    add("asleep", "sleeping_snore_can_obey_first_check", currentMove=173,
        chosenMove=173, status1=3, draws=[0])
    add("alternate", "select_other_slot", draws=[255, 0, 1])
    add("alternate", "second_check_strict_boundary_below", draws=[255, 42, 1])
    add("alternate", "second_check_strict_boundary_equal", draws=[255, 43, 0])
    add("alternate", "rejection_sampling_skips_all_masked_slots", limitedMask=0xA,
        draws=[255, 0, 0, 1, 3, 2])
    add("alternate", "chosen_slot_is_in_initial_mask", currMovePos=2,
        chosenMovePos=2, limitedMask=0xB, draws=[255, 0, 65535])
    add("alternate", "all_moves_masked_loaf_low_two_bits", limitedMask=14,
        draws=[255, 0, 65534])
    add("alternate", "target_helper_selects_self", targetResult=0, draws=[255, 0, 1])
    add("alternate", "existing_hitmarker_preserved", hitMarker=256, draws=[255, 0, 1])
    add("focus_punch", "second_roll_cannot_select_alternate", currentMove=264,
        chosenMove=264, draws=[255, 0, 255, 65534])
    add("focus_punch", "can_nap_after_second_roll", currentMove=264,
        chosenMove=264, draws=[255, 0, 0])
    add("rage", "disobedience_clears_only_rage", currentMove=99, chosenMove=99,
        status2=(1 << 23) | 3)
    add("rage", "obedience_keeps_rage", currentMove=99, chosenMove=99,
        status2=(1 << 23) | 3, draws=[0])
    add("draw_masks", "high_byte_ignored", draws=[0xABFF, 0xCD00, 0xEF02])
    add("draw_masks", "first_high_byte_only_obeys", draws=[0xFF00])
    for roll in [0, 39, 40, 79, 80, 255]:
        add("outcome", f"delta40_third_roll_{roll}",
            draws=[255, 255, roll] + ([2] if roll >= 80 else []))
    for label, changes in [
        ("poison", dict(status1=8)),
        ("already_asleep", dict(status1=3)),
        ("vital_spirit", dict(ability=72)),
        ("insomnia", dict(ability=15)),
        ("self_uproar", dict(status2=16)),
        ("opponent_uproar", dict(otherStatus2=[16, 0, 0])),
        ("fourth_battler_uproar", dict(battlersCount=4, otherStatus2=[0, 0, 16])),
    ]:
        add("blocked_sleep", label + "_falls_through_to_self_hit", draws=[255, 255, 0],
            **changes)
    add("blocked_sleep", "uproar_outside_battlers_count_ignored",
        otherStatus2=[0, 0, 16], draws=[255, 255, 0])
    add("blocked_sleep", "toxic_counter_without_status_is_not_any_status",
        status1=256, draws=[255, 255, 0])
    add("self_hit", "passes_base_damage_arguments_and_preserves_marker",
        baseDamage=61, hitMarker=256, draws=[255, 255, 40])
    for chooser in range(4):
        add("loaf", f"message_{chooser}", draws=[255, 255, 255, 0xAB00 + chooser])
    return rows


PREAMBLE = r'''
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8;
typedef uint16_t u16;
typedef uint32_t u32;
typedef int32_t s32;
typedef u8 bool8;
typedef u32 bool32;
#include "constants/battle.h"
#include "constants/abilities.h"
#include "constants/moves.h"
#include "constants/species.h"
#include "constants/pokemon.h"
#include "constants/global.h"
#include "constants/flags.h"
#include "constants/battle_script_commands.h"
#include "constants/battle_string_ids.h"
#ifndef TRUE
#define TRUE 1
#define FALSE 0
#endif
#define EOS 255
#define NO_TARGET_OVERRIDE 0
#define MOVE_LIMITATIONS_ALL 255
struct BattlePokemon { u32 otId; u8 otName[8]; u8 level; u16 moves[4];
    u32 status1, status2; u8 ability; };
struct Pokemon { u16 species; u8 modernFateful; };
struct SaveBlock2 { u8 playerTrainerId[4], playerName[8]; };
struct Input {
    u32 battleTypeFlags, attackerSide, level, badgeFlags, species, modernFateful;
    u32 otId, playerId; u8 otName[8], playerName[8];
    u32 currentMove, chosenMove, calledMove, currMovePos, chosenMovePos;
    u16 moves[4]; u32 status1, status2, otherStatus2[3], ability, battlersCount;
    u32 target, limitedMask, targetResult; s32 baseDamage, damage;
    u32 hitMarker, multiString; u16 draws[16]; u32 drawCount;
};
static struct BattlePokemon gBattleMons[4];
static struct Pokemon gPlayerParty[6];
static struct SaveBlock2 saveBlock, *gSaveBlock2Ptr = &saveBlock;
static u32 gBattleTypeFlags, gHitMarker;
static u8 gBattlerAttacker, gBattlerTarget, gBattlersCount;
static u8 gBattlerPartyIndexes[4], gCurrMovePos, gChosenMovePos;
static u8 gBattleCommunication[8];
static u16 gCurrentMove, gChosenMove, gCalledMove;
static s32 gBattleMoveDamage;
static const u32 gBitTable[4] = {1, 2, 4, 8};
static const u8 BattleScript_IgnoresWhileAsleep[] = "ignore_asleep";
static const u8 BattleScript_MoveUsedLoafingAround[] = "loaf";
static const u8 BattleScript_IgnoresAndUsesRandomMove[] = "alternate";
static const u8 BattleScript_IgnoresAndFallsAsleep[] = "nap";
static const u8 BattleScript_IgnoresAndHitsItself[] = "self_hit";
static const u8 *gBattlescriptCurrInstr;
static const struct Input *input;
static u32 drawIndex, limitationsCalls, initialMask, checkFlags, targetCalls;
static u32 targetMove, targetOverride, damageCalls, damageMove, damagePower;
static u32 damageSelfPair, damageArgumentsZero;
static u16 Random(void) {
    if (drawIndex >= input->drawCount) {
        fprintf(stderr, "Draw tape exhausted at index %u\n", drawIndex);
        exit(2);
    }
    return input->draws[drawIndex++];
}
static u8 GetBattlerSide(u8 battler) {
    return battler == 0 ? input->attackerSide : (battler & 1);
}
static u32 GetMonData(struct Pokemon *mon, int field, void *unused) {
    (void)unused;
    if (field == MON_DATA_SPECIES) return mon->species;
    if (field == MON_DATA_MODERN_FATEFUL_ENCOUNTER) return mon->modernFateful;
    fprintf(stderr, "Unexpected GetMonData field %d\n", field); exit(3);
}
static bool8 FlagGet(u16 flag) {
    if (flag < FLAG_BADGE01_GET || flag > FLAG_BADGE08_GET) {
        fprintf(stderr, "Unexpected flag %u\n", flag); exit(4);
    }
    return (input->badgeFlags >> (flag - FLAG_BADGE01_GET)) & 1;
}
static u8 CheckMoveLimitations(u8 battler, u8 mask, u8 flags) {
    if (battler != 0) exit(5);
    limitationsCalls++; initialMask = mask; checkFlags = flags;
    return mask | input->limitedMask;
}
static u8 GetMoveTarget(u16 move, u8 override) {
    targetCalls++; targetMove = move; targetOverride = override;
    return input->targetResult;
}
static s32 CalculateBaseDamage(struct BattlePokemon *attacker,
    struct BattlePokemon *defender, u16 move, u16 sideStatus,
    u16 power, u8 type, u8 attackerId, u8 defenderId) {
    damageCalls++; damageMove = move; damagePower = power;
    damageSelfPair = attacker == &gBattleMons[0] && defender == &gBattleMons[0]
        && attackerId == 0 && defenderId == 0;
    damageArgumentsZero = sideStatus == 0 && type == 0;
    return input->baseDamage;
}
'''


DRIVER = r'''
int main(void) {
    for (u32 row = 0; row < sizeof(cases) / sizeof(cases[0]); row++) {
        input = &cases[row];
        memset(gBattleMons, 0, sizeof(gBattleMons));
        memset(gPlayerParty, 0, sizeof(gPlayerParty));
        memset(gBattlerPartyIndexes, 0, sizeof(gBattlerPartyIndexes));
        memset(gBattleCommunication, 0, sizeof(gBattleCommunication));
        gBattleTypeFlags = input->battleTypeFlags;
        gHitMarker = input->hitMarker;
        gBattlerAttacker = 0; gBattlerTarget = input->target;
        gBattlersCount = input->battlersCount;
        gBattleMons[0].otId = input->otId;
        memcpy(gBattleMons[0].otName, input->otName, 8);
        memcpy(saveBlock.playerName, input->playerName, 8);
        for (int i = 0; i < 4; i++)
            saveBlock.playerTrainerId[i] = (input->playerId >> (8 * i)) & 255;
        gBattleMons[0].level = input->level;
        memcpy(gBattleMons[0].moves, input->moves, sizeof(input->moves));
        gBattleMons[0].status1 = input->status1;
        gBattleMons[0].status2 = input->status2;
        gBattleMons[0].ability = input->ability;
        for (int i = 1; i < 4; i++) gBattleMons[i].status2 = input->otherStatus2[i - 1];
        gPlayerParty[0].species = input->species;
        gPlayerParty[0].modernFateful = input->modernFateful;
        gCurrentMove = input->currentMove; gChosenMove = input->chosenMove;
        gCalledMove = input->calledMove;
        gCurrMovePos = input->currMovePos; gChosenMovePos = input->chosenMovePos;
        gBattleMoveDamage = input->damage;
        gBattleCommunication[MULTISTRING_CHOOSER] = input->multiString;
        gBattlescriptCurrInstr = (const u8 *)"unchanged";
        drawIndex = limitationsCalls = initialMask = checkFlags = targetCalls = 0;
        targetMove = targetOverride = damageCalls = damageMove = damagePower = 0;
        damageSelfPair = damageArgumentsZero = 0;
        bool8 foreign = IsOtherTrainer(gBattleMons[0].otId, gBattleMons[0].otName);
        bool32 fateful = IsBattlerModernFatefulEncounter(0);
        u8 result = IsMonDisobedient();
        printf("{\"id\":%u,\"result\":%u,\"script\":\"%s\",", row, result,
            (const char *)gBattlescriptCurrInstr);
        printf("\"foreign\":%u,\"fatefulGate\":%u,\"drawCount\":%u,\"consumedDraws\":[",
            foreign, fateful, drawIndex);
        for (u32 i = 0; i < drawIndex; i++) printf("%s%u", i ? "," : "", input->draws[i]);
        printf("],\"currentMove\":%u,\"chosenMove\":%u,\"calledMove\":%u,",
            gCurrentMove, gChosenMove, gCalledMove);
        printf("\"currMovePos\":%u,\"chosenMovePos\":%u,\"target\":%u,",
            gCurrMovePos, gChosenMovePos, gBattlerTarget);
        printf("\"status1\":%u,\"status2\":%u,\"hitMarker\":%u,\"multiString\":%u,",
            gBattleMons[0].status1, gBattleMons[0].status2, gHitMarker,
            gBattleCommunication[MULTISTRING_CHOOSER]);
        printf("\"damage\":%d,\"limitationsCalls\":%u,\"initialMask\":%u,\"checkFlags\":%u,",
            gBattleMoveDamage, limitationsCalls, initialMask, checkFlags);
        printf("\"targetCalls\":%u,\"targetMove\":%u,\"targetOverride\":%u,",
            targetCalls, targetMove, targetOverride);
        printf("\"damageCalls\":%u,\"damageMove\":%u,\"damagePower\":%u,",
            damageCalls, damageMove, damagePower);
        printf("\"damageSelfPair\":%u,\"damageArgumentsZero\":%u}\n",
            damageSelfPair, damageArgumentsZero);
    }
    return 0;
}
'''


def script_evidence(source):
    """Inspect later flow; deliberately do not pretend the C probe executes it."""
    scripts = source["data/battle_scripts_1.s"].decode()
    commands = source["src/battle_script_commands.c"]
    evidence = []

    def record(name, path, block, tokens, explanation):
        cursor = 0
        for token in tokens:
            position = block.find(token, cursor)
            assert position >= 0, (name, token)
            cursor = position + len(token)
        evidence.append(dict(name=name, source=path, sha256=sha(block.encode()),
                             checkedTokens=tokens, interpretation=explanation))

    def segment(start, end):
        return scripts[scripts.index(start + "::"):scripts.index(end + "::")]

    record("sleep_duration_is_a_later_draw", "src/battle_script_commands.c",
           extract(commands, "void SetMoveEffect(bool8 primary, u8 certain)").decode(),
           ["STATUS1_SLEEP_TURN((Random() & 3) + 2)"],
           "Nap selects a script in IsMonDisobedient. Its later successful sleep effect consumes one more 16-bit draw and sleeps for (draw & 3) + 2 turns, i.e. 2 through 5.")
    record("nap_script_sets_sleep_later", "data/battle_scripts_1.s",
           segment("BattleScript_IgnoresAndFallsAsleep", "BattleScript_IgnoresAndHitsItself"),
           ["setmoveeffect MOVE_EFFECT_SLEEP | MOVE_EFFECT_AFFECTS_USER", "seteffectprimary", "moveendto MOVEEND_NEXT_TARGET"],
           "The oracle nap output keeps status1 unchanged; the later battle script applies sleep.")
    record("alternate_changes_current_and_chosen_later", "src/battle_script_commands.c",
           extract(commands, "static void Cmd_jumptocalledmove(void)").decode(),
           ["gChosenMove = gCurrentMove = gCalledMove;"],
           "BattleScript_IgnoresAndUsesRandomMove executes jumptocalledmove 0, which changes both chosen and current move to the alternate. The obedience function itself changes only called move and positions.")
    record("alternate_script_restarts_called_move", "data/battle_scripts_1.s",
           segment("BattleScript_IgnoresAndUsesRandomMove", "BattleScript_MoveUsedLoafingAround"),
           ["jumptocalledmove 0"],
           "Later move execution, including its ordinary accuracy/damage/effect draws, is outside this probe. GetMoveTarget is a deterministic stub; any source targeting RNG is also outside the probe.")
    record("targeting_can_draw_inside_obedience_helper", "src/battle_util.c",
           extract(source["src/battle_util.c"], "u8 GetMoveTarget(u16 move, u8 setTarget)").decode(),
           ["case MOVE_TARGET_SELECTED:", "gSideTimers[side].followmeTimer",
            "targetBattler = Random() % gBattlersCount;", "case MOVE_TARGET_RANDOM:",
            "if (Random() & 1)"],
           "Source selected-target moves reject random battler indices until a valid opponent is chosen, including in singles. Follow Me can skip targeting draws; doubles random-target moves can draw one bit. These helper-internal draws are excluded by the stub, distinct from the later script draws.")
    record("self_hit_script_calls_confusion_damage", "data/battle_scripts_1.s",
           segment("BattleScript_IgnoresAndHitsItself", "BattleScript_SubstituteFade"),
           ["goto BattleScript_DoSelfConfusionDmg"],
           "The disobedience self-hit script transfers to the ordinary self-confusion damage continuation.")
    record("self_hit_runs_normal_damage2_later", "data/battle_scripts_1.s",
           segment("BattleScript_DoSelfConfusionDmg", "BattleScript_MoveUsedIsConfusedRet"),
           ["cancelmultiturnmoves BS_ATTACKER", "adjustnormaldamage2", "HITMARKER_IGNORE_SUBSTITUTE | HITMARKER_PASSIVE_DAMAGE", "datahpupdate BS_ATTACKER"],
           "Self-hit's base damage is a stub result. Later script cancels multiturn moves, adjusts damage, ignores Substitute and applies HP damage to attacker; none of those later mutations are emitted as function output.")
    record("self_hit_variance_draw", "src/battle_script_commands.c",
           extract(commands, "static inline void ApplyRandomDmgMultiplier(void)").decode(),
           ["u16 rand = Random();", "u16 randPercent = 100 - (rand % 16);"],
           "The later normaldamage2 adjustment consumes one variance draw, including when base damage is zero.")
    record("self_hit_focus_band_optional_draw", "src/battle_script_commands.c",
           extract(commands, "static void Cmd_adjustnormaldamage2(void)").decode(),
           ["ApplyRandomDmgMultiplier();", "holdEffect == HOLD_EFFECT_FOCUS_BAND && (Random() % 100) < param"],
           "A Focus Band hold effect adds a later draw after variance. This probe does not emulate damage, held items, or Focus Band.")
    record("attack_canceller_obeys_and_missed_flags", "src/battle_script_commands.c",
           extract(commands, "static void Cmd_attackcanceler(void)").decode(),
           ["if (!(gHitMarker & HITMARKER_OBEYS)", "i = IsMonDisobedient();",
            "case 0:", "case 2:", "gHitMarker |= HITMARKER_OBEYS;", "default:",
            "gMoveResultFlags |= MOVE_RESULT_MISSED;", "gHitMarker |= HITMARKER_OBEYS;"],
           "For a fresh check, return 0 continues and gains OBEYS; return 2 gains OBEYS then returns; return 1 sets MISSED without OBEYS. Already OBEYS or MULTIPLETURNS skips this check. No wrapper flags are synthesized in oracle output.")
    moveend = extract(commands, "static void Cmd_moveend(void)").decode()
    history = moveend[moveend.index("case MOVEEND_UPDATE_LAST_MOVES:"):moveend.index("case MOVEEND_NEXT_TARGET:")]
    record("history_gates_on_obeys", "src/battle_script_commands.c", history,
           ["if (gHitMarker & HITMARKER_OBEYS)", "gLastMoves[gBattlerAttacker] = gChosenMove;",
            "gLastResultingMoves[gBattlerAttacker] = gCurrentMove;",
            "gLastMoves[gBattlerAttacker] = MOVE_UNAVAILABLE;",
            "gLastResultingMoves[gBattlerAttacker] = MOVE_UNAVAILABLE;"],
           "Where normal moveend guards allow update, OBEYS records chosen/current moves; otherwise both histories become unavailable. Alternate return 2 records its later called move; self-hit return 2 keeps original chosen/current move. Mirror Move additionally requires distinct attacker/target, so self-hit cannot populate it.")
    return evidence


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=pathlib.Path)
    parser.add_argument("output", type=pathlib.Path)
    parser.add_argument("--cc", default="cc")
    arguments = parser.parse_args()
    output = arguments.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    source = {name: subprocess.check_output(
        ["git", "-C", str(arguments.source), "show", COMMIT + ":" + name]) for name in SOURCES}
    extracted = [
        ("src/pokemon.c", "bool8 IsOtherTrainer(u32 otId, u8 *otName)"),
        ("src/battle_util.c", "static bool32 IsBattlerModernFatefulEncounter(u8 battlerId)"),
        ("src/battle_util.c", "u8 IsMonDisobedient(void)"),
    ]
    bodies = [extract(source[path], signature) for path, signature in extracted]
    for name in HEADERS:
        target = output / "include" / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(source["include/" + name])
    rows = vectors()

    def c_literal(value):
        if isinstance(value, list):
            return "{" + ",".join(str(item) for item in value) + "}"
        return str(value)

    cases = ["static const struct Input cases[] = {"]
    for row in rows:
        values = dict(row["input"], drawCount=len(row["input"]["draws"]))
        cases.append("    {" + ", ".join("." + key + "=" + c_literal(value)
                                          for key, value in values.items()) + "},")
    cases.append("};\n")
    c_source = PREAMBLE.encode() + b"\n\n" + b"\n\n".join(bodies)
    c_source += ("\n\n" + "\n".join(cases) + DRIVER).encode()
    c_path, binary_path = output / "source-obedience-probe.c", output / "source-obedience-probe"
    c_path.write_bytes(c_source)
    # Original IsOtherTrainer compares u32 against integer-promoted ID bytes.
    # Preserve that function verbatim and silence only its signedness warning.
    compile_command = [arguments.cc, "-std=c99", "-O0", "-Wall", "-Wextra", "-Werror",
                       "-Wno-sign-compare",
                       "-I", str(output / "include"), str(c_path), "-o", str(binary_path)]
    subprocess.run(compile_command, check=True)
    result = subprocess.check_output([str(binary_path)], text=True)
    outputs = [json.loads(line) for line in result.splitlines()]
    assert len(outputs) == len(rows)
    for row, result in zip(rows, outputs):
        assert result.pop("id") == row["id"]
        row["output"] = result
    by_name = {row["name"]: row["output"] for row in rows}
    assert by_name["select_other_slot"]["script"] == "alternate"
    assert by_name["select_other_slot"]["calledMove"] == 10
    assert by_name["second_check_strict_boundary_below"]["script"] == "alternate"
    assert by_name["second_check_strict_boundary_equal"]["script"] == "nap"
    assert by_name["rejection_sampling_skips_all_masked_slots"]["drawCount"] == 6
    assert by_name["ot_prefix_is_owned_source_quirk"]["foreign"] == 0
    assert by_name["illegal_mew_ignores_owner_and_badge8"]["script"] == "nap"
    assert by_name["opponent_uproar_falls_through_to_self_hit"]["script"] == "self_hit"
    assert by_name["second_roll_cannot_select_alternate"]["limitationsCalls"] == 0
    assert by_name["sleeping_snore_ignores_after_one_draw"]["drawCount"] == 1
    evidence = script_evidence(source)
    report = dict(
        schemaVersion=1,
        sourceCommit=COMMIT,
        sourceHashes={name: sha(data) for name, data in source.items()},
        extractedFunctions=[dict(source=path, signature=signature, sha256=sha(body))
                            for (path, signature), body in zip(extracted, bodies)],
        compileCommand=compile_command,
        harnessSha256=sha(c_source),
        boundaries=[
            "The three named functions are extracted unchanged from git show at the pinned commit.",
            "Globals are reduced structs; scalar constants come from pinned original headers.",
            "Random returns supplied uint16 draws and fails immediately if the bounded tape is exhausted.",
            "CheckMoveLimitations returns the union of the function's initial mask and fixture limitedMask. Actual PP, Disable, Taunt, Torment, Imprison, and Choice Band evaluation is outside this probe.",
            "GetMoveTarget records arguments and returns fixture targetResult without consuming RNG; source target-selection RNG is outside this probe.",
            "CalculateBaseDamage records all relevant arguments and returns fixture baseDamage without RNG; damage arithmetic and later variance are outside this probe.",
            "Battle scripts and source repository scripts are never executed. Script/history evidence is inspected and reported separately from function output.",
            "Name fixtures are byte comparisons with EOS 255, not a game-character encoding conversion.",
        ],
        vectorCount=len(rows),
        vectors=rows,
        scriptAndHistoryEvidence=evidence,
    )
    report_path = output / "source-obedience-vectors.json"
    report_path.write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(dict(sourceCommit=COMMIT, vectorCount=len(rows),
                          scriptEvidenceCount=len(evidence), report=str(report_path))))


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Run unchanged FireRed Focus Band C with controlled scalar inputs, not a ROM.

Usage: python3 source_probe.py PATH_TO_PINNED_PRET OUTPUT_DIRECTORY
Only Git reads and the system C compiler are invoked; repository scripts are not.
"""
import argparse
import collections
import datetime
import hashlib
import itertools
import json
import pathlib
import re
import subprocess

COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
HERE = pathlib.Path(__file__).resolve().parent
FIELDS = "id op preserve target hp damage item substitute endure sticky falseSwipe attackerLevel targetLevel sturdy sureHit flags lastItem draw0 draw1 enigmaEffect enigmaParam moveAccuracy attacker statusCount".split()
OUTPUTS = "id damage flags lastItem item focus0 focus1 focus2 focus3 itemRecords abilityRecords drawCount draw0 draw1 recordBattler recordEffect destination potentialItemBattler activeBattler dmgMultiplier critMultiplier moveEffectByte missType hitMarker multiString specialStatusZeroMask lastAbility hp".split()
OPS = {"normal": 0, "normal2": 1, "set": 2, "ohko": 3, "moveCleanup": 4, "specialClear": 5, "observe": 6, "hpUpdate": 7}
SOURCES = [
    "src/battle_script_commands.c", "src/battle_main.c", "src/battle_util.c", "include/battle.h",
    "src/data/items.json", "include/constants/items.h", "include/constants/hold_effects.h",
    "include/constants/battle.h", "include/constants/battle_move_effects.h", "include/constants/abilities.h",
    "include/constants/battle_script_commands.h", "include/constants/battle_string_ids.h",
    "include/constants/moves.h", "include/constants/pokemon.h", "data/battle_scripts_1.s",
]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def extract(text, signature):
    """Brace extraction without changing a single source byte, honoring C strings/comments."""
    match = re.search(rb"(?m)^" + re.escape(signature.encode()) + rb"\s*\n\{", text)
    assert match, signature
    opening = text.index(b"{", match.start())
    pos, depth, state = opening, 0, "code"
    while pos < len(text):
        c, pair = text[pos:pos+1], text[pos:pos+2]
        if state == "line":
            if c == b"\n": state = "code"
        elif state == "comment":
            if pair == b"*/": state = "code"; pos += 1
        elif state in ("string", "char"):
            if c == b"\\": pos += 1
            elif c == (b'"' if state == "string" else b"'"): state = "code"
        elif pair == b"//": state = "line"; pos += 1
        elif pair == b"/*": state = "comment"; pos += 1
        elif c == b'"': state = "string"
        elif c == b"'": state = "char"
        elif c == b"{": depth += 1
        elif c == b"}":
            depth -= 1
            if depth == 0: return text[match.start():pos+1]
        pos += 1
    raise AssertionError("Unbalanced function: " + signature)


def script_checks(source):
    script = source["data/battle_scripts_1.s"].decode()
    commands = source["src/battle_script_commands.c"]
    main = source["src/battle_main.c"]
    evidence = []

    def segment(start, end):
        a = re.search(r"(?m)^" + re.escape(start) + r"::?\s*$", script)
        b = re.search(r"(?m)^" + re.escape(end) + r"::?\s*$", script)
        assert a and b and a.start() < b.start(), (start, end)
        return script[a.start():b.start()]

    def check(name, block, ordered, forbidden=()):
        pos = 0
        for token in ordered:
            index = block.find(token, pos)
            assert index >= 0, (name, token)
            pos = index + len(token)
        for token in forbidden: assert token not in block, (name, "forbidden", token)
        evidence.append({"name": name, "ordered": ordered, "forbidden": list(forbidden), "sha256": sha(block.encode()), "source": block})

    check("ordinary accuracy gate precedes adjustment; immunity has no intervening jump",
          segment("BattleScript_HitFromAccCheck", "BattleScript_HitFromAtkAnimation"),
          ["accuracycheck", "critcalc", "damagecalc", "typecalc", "adjustnormaldamage"], ["jumpifmovehadnoeffect"])
    check("multihit immunity branches before damage random and Focus",
          segment("BattleScript_DoMultiHit", "BattleScript_MultiHitNoMoreHits"),
          ["movevaluescleanup", "critcalc", "damagecalc", "typecalc", "jumpifmovehadnoeffect", "adjustnormaldamage", "datahpupdate", "MOVE_RESULT_FOE_ENDURED", "decrementmultihit"])
    multi = segment("BattleScript_DoMultiHit", "BattleScript_MultiHitNoMoreHits")
    interval = multi[multi.index("\tdatahpupdate"):multi.index("\tdecrementmultihit")]
    check("multihit HP update retains Endure until loop stop; resultmessage is later",
          interval, ["datahpupdate", "critmessage", "waitmessage", "printstring", "waitmessage", "addbyte", "moveendto", "MOVE_RESULT_FOE_ENDURED"],
          ["resultmessage", "movevaluescleanup", "setbyte gMoveResultFlags", "bicbyte gMoveResultFlags"])
    for name in ["Cmd_datahpupdate", "Cmd_critmessage", "Cmd_waitmessage", "Cmd_printstring", "Cmd_addbyte", "Cmd_moveend"]:
        body = extract(commands, "static void " + name + "(void)")
        assert not re.search(rb"\bgMoveResultFlags\s*(?:[|&^+*/-]?=(?!=)|\+\+|--)", body), name
        evidence.append({"name": name + " has no direct move-result flag assignment", "sha256": sha(body), "boundary": "Direct function-body inspection; unrelated callbacks are not executed by this assertion."})
    check("Explosion adjusts before accuracy, including a subsequent miss",
          segment("BattleScript_ExplosionLoop", "BattleScript_ExplosionMissed"),
          ["movevaluescleanup", "critcalc", "damagecalc", "typecalc", "adjustnormaldamage", "accuracycheck"])
    check("Super Fang skips all adjustment commands",
          segment("BattleScript_EffectSuperFang", "BattleScript_EffectDragonRage"),
          ["accuracycheck", "typecalc", "damagetohalftargethp", "goto BattleScript_HitFromAtkAnimation"],
          ["adjustnormaldamage", "adjustsetdamage", "tryKO"])
    check("Super Fang shared continuation also skips adjustment",
          segment("BattleScript_HitFromAtkAnimation", "BattleScript_MoveEnd"), ["datahpupdate", "resultmessage"],
          ["adjustnormaldamage", "adjustsetdamage", "tryKO"])
    check("Future Sight and Doom Desire accuracy precedes normal2",
          segment("BattleScript_MonTookFutureAttack", "BattleScript_FutureAttackMiss"),
          ["accuracycheck BattleScript_FutureAttackMiss, MOVE_FUTURE_SIGHT", "accuracycheck BattleScript_FutureAttackMiss, MOVE_DOOM_DESIRE", "adjustnormaldamage2", "datahpupdate", "end2"], ["critcalc", "typecalc"])
    check("OHKO type immunity gates before tryKO",
          segment("BattleScript_EffectOHKO", "BattleScript_KOFail"),
          ["accuracycheck", "typecalc", "jumpifmovehadnoeffect", "tryKO"])
    check("Triple Kick type immunity branches after adjustment",
          segment("BattleScript_DoTripleKickAttack", "BattleScript_TripleKickNoMoreHits"),
          ["accuracycheck", "typecalc", "adjustnormaldamage", "jumpifmovehadnoeffect", "datahpupdate", "MOVE_RESULT_FOE_ENDURED"])
    check("Beat Up result per member and no Endure loop break",
          segment("BattleScript_BeatUpLoop", "BattleScript_BeatUpEnd"),
          ["movevaluescleanup", "trydobeatup", "critcalc", "adjustnormaldamage", "datahpupdate", "resultmessage", "goto BattleScript_BeatUpLoop"], ["MOVE_RESULT_FOE_ENDURED", "typecalc"])
    check("Confusion normal2 precedes ignore-substitute flag",
          segment("BattleScript_DoSelfConfusionDmg", "BattleScript_MoveUsedIsConfusedRet"),
          ["adjustnormaldamage2", "HITMARKER_IGNORE_SUBSTITUTE", "datahpupdate"])
    finished = extract(main, "static void HandleAction_ActionFinished(void)")
    assert b"SpecialStatusesClear();" in finished
    for signature in ["static void BattleTurnPassed(void)", "static void TurnValuesCleanUp(bool8 var0)", "static void HandleAction_UseMove(void)", "static void HandleAction_NothingIsFainted(void)"]:
        # Argument naming varies by source; retrieve the declaration's exact text.
        name = signature.split("(")[0].split()[-1]
        sig = re.search(rb"(?m)^(?:static )?void " + name.encode() + rb"\([^\n;]*\)\n\{", main)
        assert sig, name
        body = extract(main, sig.group().split(b"\n")[0].decode())
        assert b"SpecialStatusesClear(" not in body, name
        evidence.append({"name": name + " does not call SpecialStatusesClear", "sha256": sha(body)})
    evidence.append({"name": "ActionFinished clears all SpecialStatus slots", "sha256": sha(finished), "boundary": "Source control-flow evidence; observe fixtures do not execute turn callbacks."})
    return evidence


def build_vectors():
    vectors = []

    def add(group, name, **kw):
        row = dict(zip(FIELDS, [len(vectors), 0, 0, 1, 20, 20, 196, 0, 0, 0, 0, 50, 50, 0, 0, 0, 777, 0, 9, 39, 10, 30, 0, 4]))
        if "mode" in kw: kw["op"] = OPS[kw.pop("mode")]
        row.update(kw)
        assert set(row) == set(FIELDS)
        vectors.append({"group": group, "name": name, "input": row})
        return vectors[-1]

    # 2,560 rows: complete status-bit cross product at representative HP/damage
    # and all requested Focus proc boundaries; dynamic Enigma uses two boundaries.
    for mode in ["normal", "normal2", "set"]:
        for hp, damage in [(1, 1), (20, 1), (20, 20), (20, 1000)]:
            for substitute, endure, falseSwipe, sticky in itertools.product(range(2), repeat=4):
                for item, proc in [(0, 10)] + [(196, p) for p in [0, 9, 10, 99, 65535]] + [(175, p) for p in [9, 10]]:
                    for variance in ([0, 15] if mode != "set" else [0]):
                        draws = [variance, proc] if mode != "set" else [proc, 54321]
                        add("scalar", "status_cross_product", mode=mode, hp=hp, damage=damage, substitute=substitute, endure=endure, falseSwipe=falseSwipe, sticky=sticky, item=item, draw0=draws[0], draw1=draws[1])
    for mode in ["normal", "normal2", "set"]:
        for hp, damage in [(1, 0), (1, 2), (2, 0), (2, 1), (2, 2), (2, 3), (20, 0), (20, 19), (20, 21), (20, -1), (20, -1000)]:
            for item, proc in [(0, 10), (196, 9), (196, 10)]:
                for variance in ([0, 15] if mode != "set" else [0]):
                    add("arithmetic", "zero_one_nonlethal_threshold_signed", mode=mode, hp=hp, damage=damage, item=item, draw0=variance if mode != "set" else proc, draw1=proc if mode != "set" else 54321)
    for mode in ["normal", "normal2", "set"]:
        for param, proc in itertools.product([0, 1, 10, 100, 255], [0, 9, 10, 99, 65535]):
            add("enigma", "dynamic_focus_parameter", mode=mode, item=175, enigmaParam=param, draw0=0 if mode != "set" else proc, draw1=proc)
        add("enigma", "dynamic_nonfocus_effect", mode=mode, item=175, enigmaEffect=0)
    # 864 rows: strict OHKO chance boundary, level/sure-hit and pre-Sturdy roll.
    for item, proc in [(0, 10), (196, 9), (196, 10)]:
        for sticky, endure, substitute, sturdy in itertools.product(range(2), repeat=4):
            for attackerLevel, sureHit in itertools.product([49, 50, 51], range(3)):
                chance = 30 + attackerLevel - 50
                for accuracyRoll in [chance - 2, chance - 1]:
                    add("ohko", "chance_sturdy_surehit_cross_product", mode="ohko", item=item, sticky=sticky, endure=endure, substitute=substitute, sturdy=sturdy, attackerLevel=attackerLevel, sureHit=sureHit, draw0=proc if item else accuracyRoll, draw1=accuracyRoll, damage=321)
    for mode in ["normal", "normal2", "set", "ohko"]:
        for flags in [1, 2, 4, 8, 32, 128, 255]:
            add("flags", "existing_result_flags_are_or_preserved", mode=mode, flags=flags, damage=1000, draw0=9 if mode in ["set", "ohko"] else 0, draw1=0)
    for hp in [1, 2]:
        for endure, substitute, sticky, proc in itertools.product(range(2), range(2), range(2), [9, 10]):
            add("ohko_hp", "hp1_hp2_substitute_no_guard", mode="ohko", hp=hp, endure=endure, substitute=substitute, sticky=sticky, draw0=proc, draw1=0)

    def seq(name, steps):
        for n, step in enumerate(steps):
            defaults = {"preserve": int(n != 0), "sticky": -1 if n else 0, "flags": 0, "lastItem": -1 if n else 777}
            defaults.update(step)
            add("sequence", name + "/" + str(n), **defaults)

    seq("nonlethal_then_failed_lethal", [dict(damage=1), dict(damage=1000, draw1=10)])
    seq("substitute_then_failed_lethal", [dict(substitute=1, damage=1000), dict(damage=1000, draw1=10)])
    seq("hp1_then_repeated_failed_hits", [dict(hp=1, damage=1), dict(hp=1, damage=1, draw1=10), dict(hp=1, damage=1, draw1=99)])
    seq("move_cleanup_retains_sticky", [dict(damage=1), dict(mode="moveCleanup", flags=255), dict(damage=1000, draw1=10)])
    seq("action_finished_clear_removes_sticky", [dict(damage=1), dict(mode="specialClear", flags=-1), dict(damage=1000, draw1=10)])
    seq("future_normal2_then_no_reset_then_first_action", [dict(mode="normal2", damage=1), dict(mode="observe", flags=0), dict(damage=1000, draw1=10), dict(mode="specialClear"), dict(damage=1000, draw1=10)])
    seq("sticky_after_item_removed", [dict(damage=1), dict(item=0, damage=1000)])
    seq("sticky_after_item_changed", [dict(damage=1), dict(item=42, damage=1000)])
    seq("independent_slots_and_all_slot_clear", [dict(target=1, damage=1), dict(target=2, damage=1000, draw1=10), dict(target=3, damage=1), dict(mode="specialClear", target=2), dict(target=1, damage=1000, draw1=10), dict(target=3, damage=1000, draw1=10)])
    seq("endure_band_then_original_hp_update", [dict(endure=1, damage=1000), dict(mode="hpUpdate", hp=20, damage=19, endure=1, flags=-1)])
    seq("focus_then_original_hp_update", [dict(damage=1000), dict(mode="hpUpdate", hp=20, damage=19, flags=-1)])
    # Direct named checkpoints make regressions inspectable without a model.
    for name, kw in [
        ("zero_still_draws_twice", dict(damage=0)),
        ("failed_roll_does_not_clear_sticky", dict(damage=1000, sticky=1, draw1=10)),
        ("normal2_ignores_false_swipe", dict(mode="normal2", item=0, falseSwipe=1, damage=1000)),
        ("normal_honors_false_swipe", dict(item=0, falseSwipe=1, damage=1000)),
        ("focus_false_swipe_reports_hung_on", dict(falseSwipe=1, damage=1000)),
        ("endure_wins_over_focus", dict(endure=1, damage=1000)),
        ("sturdy_still_rolls_focus_first", dict(mode="ohko", sturdy=1, draw0=9, draw1=0, damage=321)),
        ("ohko_substitute_still_clamps", dict(mode="ohko", substitute=1, draw0=9, draw1=0)),
        ("ohko_chance_miss_still_primes", dict(mode="ohko", draw0=9, draw1=29, damage=321)),
        ("ohko_surehit_higher_skips_chance", dict(mode="ohko", sureHit=1, attackerLevel=51, draw0=10, draw1=99)),
        ("ohko_surehit_lower_still_draws_and_misses", dict(mode="ohko", sureHit=1, attackerLevel=49, draw0=9, draw1=0, damage=321)),
    ]:
        add("checkpoint", name, **kw)
    return vectors


def assertions(vectors):
    named = {v["name"]: v for v in vectors if v["group"] in ["checkpoint", "sequence"]}
    checks = []

    def expect(name, **expected):
        observed = named[name]["output"]
        for key, value in expected.items(): assert observed[key] == value, (name, key, value, observed[key])
        checks.append({"name": name, "expected": expected, "id": named[name]["input"]["id"]})

    expect("zero_still_draws_twice", damage=0, drawCount=2, focus1=1, itemRecords=1, flags=0)
    expect("failed_roll_does_not_clear_sticky", damage=19, focus1=1, itemRecords=0, flags=128, lastItem=196)
    expect("normal2_ignores_false_swipe", damage=1000, flags=0, drawCount=1)
    expect("normal_honors_false_swipe", damage=19, flags=0, drawCount=1)
    expect("focus_false_swipe_reports_hung_on", damage=19, flags=128)
    expect("endure_wins_over_focus", damage=19, flags=64, focus1=1, itemRecords=1, lastItem=777)
    expect("sturdy_still_rolls_focus_first", damage=321, flags=1, focus1=1, itemRecords=1, abilityRecords=1, drawCount=1, destination=10, lastAbility=5)
    expect("ohko_substitute_still_clamps", damage=19, flags=128, drawCount=2)
    expect("ohko_chance_miss_still_primes", damage=321, flags=1, focus1=1, drawCount=2, destination=11, multiString=0)
    expect("ohko_surehit_higher_skips_chance", damage=20, flags=16, drawCount=1, destination=5)
    expect("ohko_surehit_lower_still_draws_and_misses", damage=321, flags=1, focus1=1, drawCount=2, destination=11, multiString=1)
    expect("nonlethal_then_failed_lethal/1", damage=19, flags=128, itemRecords=0)
    expect("substitute_then_failed_lethal/0", damage=1000, flags=0, focus1=1)
    expect("substitute_then_failed_lethal/1", damage=19, flags=128, itemRecords=0)
    expect("hp1_then_repeated_failed_hits/2", damage=0, flags=128, itemRecords=0)
    expect("move_cleanup_retains_sticky/1", focus1=1, flags=0, dmgMultiplier=1, critMultiplier=1, moveEffectByte=0, missType=0, hitMarker=1 << 31)
    expect("move_cleanup_retains_sticky/2", damage=19, flags=128)
    expect("action_finished_clear_removes_sticky/1", focus1=0, specialStatusZeroMask=15, activeBattler=4)
    expect("action_finished_clear_removes_sticky/2", damage=1000, flags=0)
    expect("future_normal2_then_no_reset_then_first_action/2", damage=19, flags=128)
    expect("future_normal2_then_no_reset_then_first_action/4", damage=1000, flags=0)
    expect("sticky_after_item_removed/1", damage=19, flags=128, lastItem=0, drawCount=1, itemRecords=0)
    expect("sticky_after_item_changed/1", damage=19, flags=128, lastItem=42, drawCount=1, itemRecords=0)
    expect("independent_slots_and_all_slot_clear/1", damage=1000, focus1=1, focus2=0, flags=0)
    expect("independent_slots_and_all_slot_clear/3", focus0=0, focus1=0, focus2=0, focus3=0, specialStatusZeroMask=15)
    expect("endure_band_then_original_hp_update/0", damage=19, hp=20, flags=64, focus1=1, itemRecords=1)
    expect("endure_band_then_original_hp_update/1", damage=19, hp=1, flags=64, focus1=1, drawCount=0)
    expect("focus_then_original_hp_update/1", hp=1, flags=128, focus1=1, drawCount=0)
    for v in vectors:
        i, o = v["input"], v["output"]
        assert o["id"] == i["id"] and o["item"] == i["item"]
        assert 0 <= o["drawCount"] <= 2
        assert o["consumedDraws"] == [i["draw0"], i["draw1"]][:o["drawCount"]]
        if o["itemRecords"]:
            assert o["recordBattler"] == i["target"] and o["recordEffect"] == 39
    return checks


def run(root, out):
    out.mkdir(parents=True, exist_ok=True)
    assert subprocess.check_output(["git", "-C", str(root), "rev-parse", "HEAD"], text=True).strip() == COMMIT
    source = {}
    for name in SOURCES:
        source[name] = (root / name).read_bytes()
        assert source[name] == subprocess.check_output(["git", "-C", str(root), "show", COMMIT + ":" + name]), name
    item = next(x for x in json.loads(source["src/data/items.json"])["items"] if x["itemId"] == "ITEM_FOCUS_BAND")
    assert item["holdEffect"] == "HOLD_EFFECT_FOCUS_BAND" and item["holdEffectParam"] == 10
    assert re.search(rb"(?m)^#define ITEM_FOCUS_BAND\s+196\s*$", source["include/constants/items.h"])
    assert re.search(rb"(?m)^#define HOLD_EFFECT_FOCUS_BAND\s+39\s*$", source["include/constants/hold_effects.h"])
    script_evidence = script_checks(source)
    symbols = [("src/battle_script_commands.c", sig) for sig in [
        "static inline void ApplyRandomDmgMultiplier(void)", "static void Cmd_adjustnormaldamage(void)",
        "static void Cmd_adjustnormaldamage2(void)", "static void Cmd_adjustsetdamage(void)",
        "static void Cmd_tryKO(void)", "static void MoveValuesCleanUp(void)", "static void Cmd_datahpupdate(void)",
    ]] + [("src/battle_main.c", "static void SpecialStatusesClear(void)")]
    extracted = [(name, sig, extract(source[name], sig)) for name, sig in symbols]
    constants = "ITEM_NONE ITEM_FOCUS_BAND ITEM_ENIGMA_BERRY HOLD_EFFECT_FOCUS_BAND ABILITY_STURDY EFFECT_FALSE_SWIPE STATUS2_SUBSTITUTE STATUS3_ALWAYS_HITS MOVE_RESULT_MISSED MOVE_RESULT_SUPER_EFFECTIVE MOVE_RESULT_NOT_VERY_EFFECTIVE MOVE_RESULT_DOESNT_AFFECT_FOE MOVE_RESULT_ONE_HIT_KO MOVE_RESULT_FAILED MOVE_RESULT_FOE_ENDURED MOVE_RESULT_FOE_HUNG_ON MOVE_RESULT_NO_EFFECT HITMARKER_DESTINYBOND HITMARKER_SYNCHRONISE_EFFECT HITMARKER_IGNORE_SUBSTITUTE HITMARKER_PASSIVE_DAMAGE HITMARKER_SKIP_DMG_TRACK MOVE_EFFECT_BYTE MISS_TYPE MULTISTRING_CHOOSER B_MSG_KO_MISS B_MSG_KO_UNAFFECTED F_DYNAMIC_TYPE_1 DYNAMIC_TYPE_MASK TYPE_MYSTERY MOVE_PAIN_SPLIT BS_TARGET".split()
    definitions = []
    for symbol in constants + ["IS_TYPE_PHYSICAL"]:
        matches = [m.group() for data in source.values() for m in re.finditer(rb"(?m)^#define " + symbol.encode() + rb"(?:\s|\()[^\n]*", data)]
        assert len(matches) == 1, (symbol, matches)
        definitions.append(matches[0])
    special = extract(source["include/battle.h"], "struct SpecialStatus") + b";"
    prefix = (HERE / "source-prefix.h").read_bytes().replace(b"/* SOURCE_CONSTANTS_AND_SPECIAL_STATUS */", b"\n".join(definitions) + b"\n" + special)
    bodies = b"\n\n".join(body for _, _, body in extracted)
    generated = prefix + b"\n" + bodies + b"\n" + (HERE / "source-main.c").read_bytes()
    (out / "extracted-functions.c").write_bytes(bodies + b"\n")
    (out / "focus-oracle.c").write_bytes(generated)
    compiler = ["cc", "-std=c99", "-O2", "-Wall", "-Wextra", "-Werror", "-Wno-sign-compare", str(out / "focus-oracle.c"), "-o", str(out / "focus-oracle")]
    subprocess.run(compiler, check=True)
    vectors = build_vectors()
    encoded = "".join(" ".join(str(v["input"][key]) for key in FIELDS) + "\n" for v in vectors)
    (out / "input-vectors.txt").write_text(encoded)
    result = subprocess.check_output([str(out / "focus-oracle")], input=encoded, text=True)
    compile_validation = []
    for label, flags in [("debug", ["-O0"]), ("ubsan", ["-O1", "-fsanitize=undefined", "-fno-sanitize-recover=all"])]:
        executable = out / ("focus-oracle-" + label)
        args = ["cc", "-std=c99", "-Wall", "-Wextra", "-Werror", "-Wno-sign-compare", *flags, str(out / "focus-oracle.c"), "-o", str(executable)]
        subprocess.run(args, check=True)
        probe = subprocess.run([str(executable)], input=encoded, text=True, capture_output=True, check=True)
        assert probe.stdout == result and not probe.stderr, label
        compile_validation.append({"name": label, "arguments": args, "resultSha256": sha(probe.stdout.encode()), "binarySha256": sha(executable.read_bytes()), "matchesOptimized": True, "diagnostics": ""})
    lines = result.splitlines()
    assert len(lines) == len(vectors)
    for v, line in zip(vectors, lines):
        values = [int(x) for x in line.split(",")]
        assert len(values) == len(OUTPUTS), values
        v["output"] = dict(zip(OUTPUTS, values))
        v["output"]["consumedDraws"] = [v["output"]["draw0"], v["output"]["draw1"]][:v["output"]["drawCount"]]
    checkpoints = assertions(vectors)
    invalid_item = encoded.splitlines()[0].split()
    invalid_item[FIELDS.index("item")] = "65536"
    for invalid in ["x\n", "0\n", encoded.splitlines()[0] + " 9\n", " ".join(invalid_item) + "\n"]:
        proc = subprocess.run([str(out / "focus-oracle")], input=invalid, text=True, capture_output=True)
        assert proc.returncode == 2 and not proc.stdout
    jsonl = "".join(json.dumps(v, separators=(",", ":")) + "\n" for v in vectors)
    (out / "source-vectors.jsonl").write_text(jsonl)
    (out / "source-vectors.csv").write_text(",".join(OUTPUTS) + "\n" + result)
    (out / "script-evidence.json").write_text(json.dumps(script_evidence, indent=2) + "\n")
    (out / "checkpoints.json").write_text(json.dumps(checkpoints, indent=2) + "\n")
    (out / "endure-lifecycle.json").write_text(json.dumps({"rows": [v for v in vectors if v["name"].startswith("endure_band_then_original_hp_update/")], "evidence": [e for e in script_evidence if "Endure" in e["name"] or e["name"].startswith("Cmd_datahpupdate")], "interpretation": "Unchanged source adjustment sets Endure (64) while recording the successful Focus proc, then unchanged source HP application leaves HP 1 and Endure (64). MultiHit checks Endure before resultmessage; Beat Up performs resultmessage per member and has no Endure loop break. The full interpreter and unrelated moveend callbacks are not executed."}, indent=2) + "\n")
    schema = {
        "version": 1, "format": "One JSON object per line with group, name, input and output; source-vectors.csv is the flat result view.",
        "inputWireFields": FIELDS, "outputWireFields": OUTPUTS, "operations": OPS,
        "sequence": "Run rows in order. preserve=0 resets fixture; preserve=1 retains all registers not explicitly assigned. sticky=-1, flags=-1, lastItem=-1 retain those registers. Other input fields are fixture assignments before every command.",
        "random": "draw0 and draw1 are exact supplied uint16 outputs, not PRNG seeds. Only output.consumedDraws were consumed, in that order. No RNG is added by the harness.",
        "destinations": {"0": "unchanged", "1": "adjust advance", "2": "datahpupdate advance", "5": "OHKO success advance", "10": "SturdyPreventsOHKO", "11": "KO failure operand", "12": "SubstituteFade"},
        "sureHit": {"0": "status absent", "1": "status set and battlerWithSureHit matches attacker", "2": "status set but attacker mismatch"},
        "compareCSharp": {"minimumFields": ["damage", "flags", "lastItem", "item", "focus0", "focus1", "focus2", "focus3", "itemRecords", "abilityRecords", "drawCount", "consumedDraws", "destination", "hp"], "flagValues": {"missed": 1, "oneHitKo": 16, "endured": 64, "hungOn": 128}, "sourceHoldEffect": 39, "sourceItemId": 196, "sourceHoldEffectParam": 10},
        "specialClear": "specialStatusZeroMask is one bit per wholly zeroed SpecialStatus struct. Reset decorates every struct; the source clear produces 15 with statusCount=4.",
        "hpUpdate": "Only named sequences use unchanged Cmd_datahpupdate, with no substitute and no controller blocking. Controller output and script stack calls are inert stubs. HP application is actual source C.",
        "boundaries": ["Not a ROM execution or an emulator.", "All selected function bodies and SpecialStatus declaration are exact pinned source bytes.", "Enigma configuration is a controlled input, not proof of C# Enigma item admission.", "Observe opcode 6 is fixture-only and cannot establish turn callback behavior; source script assertions establish the separate control-flow boundary.", "Per-command itemRecords records proc success, not displayed survival messages. Resultmessage display and entire script execution are not executed.", "Items besides none, Focus Band and dynamic Enigma are modeled with no hold effect; item 42 only tests current-item reporting after sticky success.", "C99 host compiler is used with exact-width scalar types. This does not emulate ARM ABI, controller or memory timing."]
    }
    (out / "schema.json").write_text(json.dumps(schema, indent=2) + "\n")
    outputs = ["focus-oracle.c", "focus-oracle", "extracted-functions.c", "input-vectors.txt", "source-vectors.jsonl", "source-vectors.csv", "script-evidence.json", "checkpoints.json", "schema.json", "endure-lifecycle.json"]
    metadata = {
        "generatedAtUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "sourceCommit": COMMIT,
        "sourceFiles": {name: sha(data) for name, data in source.items()},
        "symbols": [{"file": name, "signature": sig, "sha256": sha(body), "bytes": len(body)} for name, sig, body in extracted],
        "specialStatusDeclarationSha256": sha(special), "extractedFunctionsSha256": sha(bodies),
        "harnessFiles": {name: sha((HERE / name).read_bytes()) for name in ["source_probe.py", "source-prefix.h", "source-main.c"]},
        "outputFiles": {name: sha((out / name).read_bytes()) for name in outputs},
        "compiler": subprocess.check_output(["cc", "--version"], text=True).splitlines()[0], "compilerArguments": compiler,
        "buildValidation": compile_validation,
        "vectors": len(vectors), "groups": dict(collections.Counter(v["group"] for v in vectors)),
        "namedCheckpoints": len(checkpoints), "scriptAssertions": len(script_evidence), "malformedInputChecks": 4,
        "focusBand": {"item": 196, "effect": 39, "parameter": 10},
        "scope": "Unmodified, byte-verified original C command extraction plus separate static source script/control-flow assertions; not ROM execution."
    }
    (out / "source-probe.json").write_text(json.dumps(metadata, indent=2) + "\n")
    print(json.dumps({"vectors": len(vectors), "checkpoints": len(checkpoints), "scriptAssertions": len(script_evidence), "extractedSha256": sha(bodies), "out": str(out)}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=pathlib.Path)
    parser.add_argument("out", type=pathlib.Path)
    args = parser.parse_args()
    run(args.source.resolve(), args.out.resolve())

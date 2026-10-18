#!/usr/bin/env python3
"""One-time independent reference evaluator, never invoked by normal .NET tests.

Requires locally downloaded, hash-verified pinned public text. Does not import the
C# implementation, execute upstream code/scripts, download a ROM, or use .NET
test output. Golden changes require a source/profile review, not a rerun to make
failing tests pass. Python arbitrary-precision integers and math.isqrt form the
independent arithmetic implementation.
"""
import hashlib
import json
import math
import pathlib
import sys

COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
SOURCES = {
    "src_data_pokemon_experience_tables.h": "9a236761640676e251b76b7950b86e5881ee23dfe920ca27bbe47b4541542731",
    "src_battle_script_commands.c": "3b7341db8cd58c54fb0b54b47e6eef89c5c05e8a60ab4e9b45bdc907845c19b3",
}


def growth(curve, n):
    if n == 1:
        return 1  # literal table entries, deliberately not the formulas at n=1
    cube = n ** 3
    if curve == 0:
        return cube
    if curve == 1:
        if n < 51:
            return cube * (100 - n) // 50
        if n < 69:
            return cube * (150 - n) // 100
        if n < 99:
            coefficient = (1911 - 10 * n) // 3
            return cube * coefficient // 500
        return cube * (160 - n) // 100
    if curve == 2:
        if n < 16:
            coefficient = (n + 1) // 3 + 24
        elif n < 37:
            coefficient = n + 14
        else:
            coefficient = n // 2 + 32
        return cube * coefficient // 50
    if curve == 3:
        return (6 * cube // 5) - (15 * n ** 2) + 100 * n - 140
    if curve == 4:
        return cube * 4 // 5
    if curve == 5:
        return cube * 5 // 4
    raise ValueError(curve)


def capture(identifier, hp, current, rate, ball, status, seed, cursor=0):
    multiplier = {1: 0, 2: 20, 3: 15, 4: 10}[ball]
    adjusted = rate * multiplier // 10
    hp_adjusted = adjusted * (hp * 3 - current * 2) // (hp * 3)
    value = hp_adjusted
    if status in [1, 2]:
        value *= 2
    elif status in [3, 4, 5, 6]:
        value = value * 15 // 10
    threshold = None
    start_seed = seed
    start_cursor = cursor
    passed = 0
    draws = []
    if value > 254:
        animation, branch = 4, "GuaranteedCatchValue"
    else:
        # Fixed original-layout Master Ball bonus is 0; original __udivsi3 /0 ->0.
        threshold = 0 if value == 0 else 1048560 // math.isqrt(math.isqrt(16711680 // value))
        while passed < 4:
            seed = (1103515245 * seed + 24691) % (2 ** 32)
            raw = seed // (2 ** 16)
            cursor += 1
            draws.append({"raw": raw, "seed": seed})
            if raw >= threshold:
                break
            passed += 1
        animation = 4 if ball == 1 else passed
        branch = "MasterBallOverride" if ball == 1 else "ShakeChecks"
    return {
        "id": identifier,
        "input": {"maximumHp": hp, "currentHp": current, "catchRate": rate, "ball": ball, "status": status},
        "seed": start_seed, "cursor": start_cursor,
        "expected": {
            "caught": animation == 4, "checksPassed": passed, "animationCode": animation,
            "branch": branch, "seed": seed, "cursor": cursor, "draws": draws,
            "calculation": {"ballMultiplierTenths": multiplier, "ballAdjustedCatchRate": adjusted,
                            "hpAdjustedCatchValue": hp_adjusted, "catchValue": value, "shakeThreshold": threshold},
        },
    }


def main():
    if len(sys.argv) != 3:
        raise SystemExit("Usage: reference_vectors.py VERIFIED_REFERENCE_DIRECTORY NEW_OUTPUT_DIRECTORY")
    source, output = map(pathlib.Path, sys.argv[1:])
    source_paths = {
        "src_data_pokemon_experience_tables.h": "src/data/pokemon/experience_tables.h",
        "src_battle_script_commands.c": "src/battle_script_commands.c",
    }
    for name, expected in SOURCES.items():
        input_path = source / source_paths[name]
        if not input_path.is_file():
            input_path = source / name  # Preserve the older verified flat-text input layout.
        if hashlib.sha256(input_path.read_bytes()).hexdigest() != expected:
            raise SystemExit("Pinned source mismatch: " + name)
    output.mkdir(parents=True, exist_ok=True)
    curves = [{"id": curve, "experienceByLevel": [growth(curve, n) for n in range(1, 101)]} for curve in range(6)]
    # Additional manually derived anchors, checked before producing any fixture.
    assert [row["experienceByLevel"][-1] for row in curves] == [1000000, 600000, 1640000, 1059860, 800000, 1250000]
    assert growth(3, 2) == 9 and growth(3, 50) == 117360
    assert growth(1, 69) == 267406 and growth(2, 37) == 50653
    vectors = []
    found = set()
    for seed in range(10000):
        row = capture("shake-count-seed-" + str(seed), 100, 100, 45, 4, 0, seed, 50)
        count = row["expected"]["checksPassed"]
        if count not in found:
            vectors.append(row)
            found.add(count)
        if len(found) == 5:
            break
    assert found == {0, 1, 2, 3, 4}
    for ball in [1, 2, 3, 4]:
        for status in range(7):
            vectors.append(capture(f"ball-{ball}-status-{status}", 100, 47, 45, ball, status, 0x12345678, 3))
    vectors += [
        capture("floor-before-hp-before-status", 100, 80, 3, 3, 4, 2),
        capture("zero-catch-value-first-raw-zero", 100, 100, 1, 4, 0, 0),
        capture("zero-catch-value-first-raw-high", 100, 100, 2, 4, 0, 0xFFFFFFFF),
        capture("master-lowest-hp", 1, 1, 1, 1, 0, 0),
        capture("master-highest-inputs", 65535, 65535, 255, 1, 1, 0xFFFFFFFF),
        capture("hp-upper-bound", 65535, 1, 255, 2, 1, 0),
        capture("catch-value-254", 2, 1, 255, 3, 0, 0),
        capture("catch-value-255-guaranteed", 1, 1, 255, 2, 3, 0, 2**64-1),
        capture("rare-full-hp", 500, 500, 3, 4, 0, 0xFFFFFFFF),
        capture("rare-one-hp-asleep", 500, 1, 3, 2, 1, 0x12345678),
    ]
    inverse = pow(1103515245, -1, 2**32)
    for raw in [32766, 32767, 32768]:
        seed = ((raw << 16) - 24691) * inverse % (2**32)
        vectors.append(capture("strict-threshold-raw-" + str(raw), 100, 100, 45, 4, 0, seed))
    for raw in [65534, 65535]:
        seed = ((raw << 16) - 24691) * inverse % (2**32)
        vectors.append(capture("254-value-raw-" + str(raw), 2, 1, 255, 3, 0, seed))
    for name, value in [
        ("growth-v1.json", {"sourceCommit": COMMIT, "sourceSha256": SOURCES["src_data_pokemon_experience_tables.h"], "curves": curves}),
        ("capture-v1.json", {"sourceCommit": COMMIT, "sourceSha256": SOURCES["src_battle_script_commands.c"], "vectors": vectors}),
    ]:
        candidate = json.dumps(value, indent=2) + "\n"
        target = output / name
        if target.exists() and target.read_text() != candidate:
            raise SystemExit("Existing reference differs; review a new candidate directory: " + str(target))
        target.write_text(candidate)
    print(f"Created 600 growth entries and {len(vectors)} capture vectors; no C# implementation was invoked.")


if __name__ == "__main__":
    main()

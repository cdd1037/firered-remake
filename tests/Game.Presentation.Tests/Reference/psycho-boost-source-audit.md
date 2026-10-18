# Psycho Boost source callback evidence

The local pinned source is pret/pokefirered commit 037335f4c725d7c9aecdac87066f2002b4bd7e14. The maintained generator extracts original AnimPsychoBoost, its affine programs, the original sprite frame/affine player including ChangeSpriteAffineAnim, BattleAnimAdjustPanning, and the two psychic palette tasks. It uses tests/Reference/reference_c.py for local native compilation. It executes no reference-repository scripts and needs no ROM or download.

The fixture contains 1,350 orb rows across six single/double actor layouts, 64 retained-affine rows across four initial signed-scale/rotation states, and 98 palette rows across twelve old/new mode and stop-tick cases. The source oracle output agrees at O0, O2 and Linux O2/UBSan. The source tick -1 is after synchronous initialization. The first affine ends at tick 197 (304/304); ChangeSpriteAffineAnim begins the second program at tick 198 (284/328), preserving state; its last scales are 4/664 at tick 213. The orb becomes invisible at tick 222 and is destroyed at tick 223. The callback-only Teleport cue uses the original actor-relative panning, including the opposite-side +63 clamp.

FadeScreenToWhite rotates both the faded and unfaded bank-2 entries 1 through 11 every fourth task update, before checking the termination argument. SetPsychicBackground only rotates faded entries with the same cadence. The fixture covers termination on and off cadence, untouched entries 0 and 12 through 15, and a later faded-bank overwrite derived from the retained unfaded values.

## Shared affine safety interpretation

The retained-state constructor uses the same shared affine interpreter and preserves signed-16-bit scale and unsigned-16-bit rotation. Relative frames discard the low rotation byte exactly as sprite.c does. Absolute first frames still replace the retained values.

For zero scale on either axis, matrix conversion deliberately adopts the source UBFIX SAFE_DIV branch, returning zero on that axis. The original non-UBFIX macro performs an undefined division by zero. This is an explicit reviewed safety choice for Mimic's initial invisible zero-scale frame, not a claim that the undefined non-UBFIX expression has a portable native result. Every nonzero-axis division remains unchanged. Psycho Boost never reaches zero scale; its extracted native oracle keeps ordinary division and validates its nonzero path. The copy-family fixture supplies Mimic's zero-scale lifecycle evidence; the shared managed affine test also covers each zero axis.

## Materials, metadata and verification

The addition reuses the existing indexed 64x64 Circle of Light tiles and Electric Orbs palette; no new material file is required. The importer records the callback-only Teleport sound and rejects a changed callback sound or a reset-affine substitution. The pinned gameplay metadata is EFFECT_OVERHEAT, power 140, Psychic, accuracy 90, PP 5 and a certain self Special Attack reduction of two stages through the existing generic effect family.

Reproduction from the repository root:

```
python tests/Game.Presentation.Tests/Reference/generate_psycho_boost_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/psycho-boost-source-vectors.json --check
python scripts/checks/test-psycho-boost-import.py
```

The generator refuses to overwrite an existing fixture unless checking it. Seven managed test methods cover the native rows, source palette modes, retained affine state, full-root composition across ten actor/target layouts, cancellation, dependency rejection and affine-width/zero-axis validation. The publication worker did not run managed builds. Native callback evidence does not constitute Godot-window, actual audible-output or complete gameplay-route acceptance.

# Shared copy state and ability exchange

This focused suite reuses its existing source-backed creature, four-slot frame,
action, full-turn, replacement, and JSON continuation helpers. It exercises
Transform/Mimic/Sketch plus shared Role Play and Skill Swap. Controlled move and
acquired-ability fixtures are battle-register tests, not campaign learnset claims.

Run from the repository root with official SDK 10 configured by `scripts/env.sh`:

```
bash tests/Game.SharedCopyState.Tests/run.sh CONTENT_ROOT PINNED_PRET_ROOT NEW_OUTPUT
```

The source root must contain pret/pokefirered commit
`037335f4c725d7c9aecdac87066f2002b4bd7e14`. The content root supplies the
existing authenticated source-mechanics catalog. The project depends explicitly
on Game.Domain and Game.Content; the runner also executes the existing
Game.CopyMoveRules.Tests/source_probe.py contract check.

The original singles-baseline.json is unchanged. ability-singles-baseline.json
contains 60 complete-state/events/RNG/switch result hashes captured from fixed
`516c492a2c78862e1100983edba22d867b618b5f` before changing product code. Capture
mode refuses to overwrite an existing baseline. These are compatibility checks,
not source-oracle replacements.

ability_probe.py verifies every input against pinned Git blobs, extracts unchanged
Cmd_trycopyability and Cmd_tryswapabilities, and compiles them with the checked-in
ability-prefix.h and ability-main.c harness. Its 648 result vectors cover every
actor and both commands, None/Wonder Guard, equal abilities, Trace/Forecast,
Intimidate, weather, and ordinary abilities. Optimized and UBSan results must match.
The source scripts also authenticate PP-before-accuracy and ordinary move-end paths.

The C# suite checks the source command results through actual shared actions,
then verifies full turns, ally/opponent targets, Protect/Substitute/Lock-On,
all move-end immunity cures, persistent party identity, switch-out/return,
no copied entry activation, authentic Trace copying Wonder Guard, and actual
Forecast re-evaluation. Castform visual forms remain distinct from battle types
when Forecast is lost or Color Change is acquired. Malformed ability names reject
atomically. All suites and oracle files are required and must be nonempty.

# Shared conditional hit verification

This one focused suite covers Facade, Smelling Salt, Revenge and Brick Break at
fixed product `516c492a2c78862e1100983edba22d867b618b5f`, against FireRed source
`037335f4c725d7c9aecdac87066f2002b4bd7e14`.

Run from the selected-source candidate (authenticated content stays read-only):

```sh
bash tests/Game.SharedConditionalHit.Tests/run.sh \
  /workspace/scratch/18c02ed4ed0d/firered-restored-20261011/firered-remake \
  /workspace/scratch/18c02ed4ed0d/firered-restored-20261011/audio-reference-build/pokefirered \
  /tmp/conditional-hit-final
```

The wrapper uses the content project's official `scripts/env.sh`, SDK 10.0.401,
its offline NuGet configuration, locked restore and the shared build lock. It
builds only this small project and its Domain/Content dependencies. There is no
Godot, broad regression, whole-game or audible-audio acceptance claim.

## Frozen baseline

Before any product edit, this same runner (baseline section only) captured 192
complete called-action results, including both serialized states, all events,
invocation history and every random trace. The capture guard rejects a product
which already contains `HasConditionalHitRecipe`, refuses to overwrite output,
and records the fixed product commit. The immutable baseline SHA-256 is
`3d20a56e6e8ab296ad4aaa40e05b5581cc429ff1c71e0e01d425116250cc017d`.

All 96 historical results are unchanged. Of 96 canonical results, 94 are
unchanged. The two exact, deliberate corrections are Revenge on either side
with `DamagedByOpponentThisTurn` set but no actual current-turn category receipt:
only damage, target HP and the resulting damage receipt change. The test restores
only those six duplicated/state/event scalar fields and requires the original
full result hash. PP, move caller identity, RNG, history and event order must match.
Historical regional fallbacks and their Protect event shape remain unchanged.

## Actual four-position owner

The focused suite runs 41 full turns through `SourceFourActionTurns`, replaying
every continuation from serialized/deserialized JSON. It covers all four actor
positions for each move, exact selected targets, PP and per-source mirror history;
Facade's healthy, poison, toxic, paralysis and Gen III burn behavior; Smelling
Salt's paralysis cure, Substitute break without cure, Ghost immunity, Endure at
one HP, Focus Band and Static contact; Brick Break's accuracy/Protect gates,
PP-before-removal, screen-before-damage, Ghost immunity and ally-target opposing
side removal; and Revenge after real physical/special damage from different
positions, including same-category replacement and Substitute exclusion. A
second actual turn proves old-turn and poison residual loss produce no receipt.
Additional action checks cover KO status handling, zero-loss critical text and
all four moves against Wonder Guard. No prepared moves or four-slot callers were
added by this slice.

Test setup explicitly assigns authentic move definitions to valid synthetic
battle bodies; this is a mechanics fixture, not proof of campaign learnset access.
The small host setup/Put/turn/replay helpers reuse the existing
`Game.SharedRecoil.Tests/Program.cs` pattern. The historical loader chain is also
reused. No new test-host framework or mirrored battle owner is introduced.

## Independent original-source checks

`source_probe.py` verifies the source Git pin, extracts the original
`Cmd_doubledamagedealtifdamaged` and `Cmd_removelightscreenreflect` bodies, and
compiles them with the explicitly authored `source-prefix.h` and `source-main.c`
using `cc -std=c99 -Wall -Wextra -Werror -fsanitize=undefined`.
It checks 512 Revenge category/source cases and 64 Brick Break side/timer cases.
It separately asserts the pinned Facade/Smelling Salt prefixes and all Brick
Break command positions, including the second `typecalc2`. These source files,
the complete immutable JSON baseline, package lock and host source are explicit
fixture dependencies in `fixture-dependencies.json`.

Successful output contains 8 groups, 1,381 assertions and 41 completed full turns.
The generated complete after-results/turn traces remain in the requested output
folder; source binaries, build products and logs are not committed dependencies.

# Shared weather and airborne hit recipes

Gust, Twister, Thunder and Sky Uppercut now use the existing shared hit recipe in
canonical singles and the four-position kernel. No new damage engine or target
loop is introduced. Historical singles remain on their prior effect owners.
The source pin is `037335f4c725d7c9aecdac87066f2002b4bd7e14`; the candidate base is
`b01522c721121548c43c841371be2702b4b86833`.

The shared accuracy helper retains Protect → matching Lock-On → semi-state →
weather order. Rain Thunder bypasses the accuracy draw; sun Thunder uses 50
before source stage, ability and item modifiers. Cloud Nine/Air Lock suppress
these weather effects. Fly/Bounce permit all four moves; only Gust/Twister gain
the twofold damage modifier. Dig/Dive reject them unless matching Lock-On applies.
Twister re-enters the existing BOTH-target owner, resets the per-target multiplier,
checks both opposing Pressure holders on the first PP command and never spends PP
twice. Every reached ordinary hit keeps its secondary RNG draw, even a zero effect
byte, immunity, Substitute or lethal damage; the existing secondary helpers own
Thunder paralysis and Twister flinch. Effective Endure/Focus Band zero-loss hits
retain critical text before result/secondary commands.

`singles-baseline-b01522c.json` contains 96 complete immutable pre-edit results
and hashes captured by the original b01522c product assembly before modification.
The fixture uses authentic Clefairy Metronome with both historical and canonical
profiles, four called moves, six conditions and two seeds. Diagnostic charging
state is labeled in the runner; every supplied move is natural or verified
against the pinned source TM/HM learnset. Baselines are never rewritten to accept
implementation changes.

`source-prefix.h` and `source-main.c` are authored host fixtures. The Python probe
extracts and compiles the original `JumpIfMoveAffectedByProtect`,
`AccuracyCalcHelper`, `Cmd_accuracycheck` and source accuracy table without changing
their bodies. It verifies all 192 saved C vectors and source file hashes. The product checks
176 accuracy outcomes and RNG cursors; the other 16 vectors have Bright Powder,
which remains outside this base's four-slot item admission and rejects atomically. Its script
checks retain the four prefixes and BOTH-target cleanup. It runs no upstream
scripts and does not claim a full original-ROM execution.

Run using the official configured .NET 10 toolchain:

```sh
FIRERED_TOOLCHAIN_ROOT=/path/to/official/toolchain bash tests/Game.WeatherHit.Tests/run.sh CONTENT_ROOT PINNED_SOURCE NEW_OUTPUT
```

The focused runner checks exact singles baselines, all actor positions, original C
accuracy vectors, target-local PP/damage/secondary state, source weather
suppression, complete singles and four-action spread turns, reward ownership,
JSON continuation and atomic stale rejection. Assets, campaign reachability,
GUI and physical audio are outside this mechanics-only slice.

Final isolated verification: 9 groups / 3,594 checks / 0 failures, with four complete
natural singles turns, one complete nonlethal spread turn and one terminal spread
turn whose two KOs are rewarded exactly once. Official SDK 10.0.401 / hosted
runtime 10.0.12; locked restore/build, one MSBuild node, no shared compiler, no GUI.
The retained full baseline is 737,060 bytes, not a generated replacement golden.

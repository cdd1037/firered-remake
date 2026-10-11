# Shared Recharge and Rampage commands

This bounded migration shares Hyper Beam, Frenzy Plant, Blast Burn, Hydro Cannon,
Thrash, Outrage and Petal Dance between canonical singles and the real four-slot
owner. It starts from `88543cb9ee09fd1a56a5567bc1aaa7bc0aeea900` and does not
admit Uproar, Bide, callers or Transform to the four-slot kernel.

## Ownership and behavior

`SourceBattleRecipes.RechargeRampage.cs` owns the action recipe. Existing charge
scalar/PP adapters reuse the original accuracy, damage, type, PP and contact
commands. `SourceEndTurnContext.Rampage.cs` owns one residual algorithm, with
storage-only singles and frame adapters. `SourceTurnLock` retains the true PP
slot, learned identity and selected position. The existing target resolver still
performs one random-flank draw on each doubles Rampage action, including forced
continuations; no called-move rejection loop is introduced.

- Effective Recharge hits set the original timer to 2 and skip the secondary
  chance draw. Ready decrements it. Sleep/freeze/Truant can precede its canceller;
  expiry still occurs at Ready. Consuming recharge costs no PP or attack RNG.
- Successful Rampage continuations skip `ppreduce`; accuracy misses execute it,
  including Pressure and zero-PP clamping. Protect cancels before the PP skip.
- The first effective hit draws the duration after damage and the secondary
  chance command. The shared residual decrements 2–3 turns, ends with a 2–5 turn
  confusion draw, and respects existing confusion and Own Tempo.
- Truant/flinch/Disable/infatuation/confusion self-hit cancel immediately.
  An already-sleeping action retains its lock until the pre-residual cleanup;
  an already-frozen action retains it and still reaches the Thrash countdown.
  Inflicting sleep/freeze cancels through the existing status command.
  Type immunity marks the retained Rampage for residual interruption.
- One source correction is intentional: paralysis cancels Rampage immediately
  because `BattleScript_MoveUsedIsParalyzed` executes `cancelmultiturnmoves`.
  The C canceller's commented-out call is not the end of this script. The old
  implementation incorrectly deferred this to an inability marker.

## Evidence

`singles-baseline.json` was frozen before any production edit: seven moves ×
three seeds × five complete canonical singles turns. It hashes the complete
serialized result records, including every state, ordered event and RNG trace.
It is immutable; capture refuses when the new recipe exists. SHA-256:
`ce0d660d6963d1172fe2dec3b0ecba5142a506b015408babd68739abe0a24dac`.

The compact executable checks all four positions and both sides, exact PP owner,
Pressure, zero-PP continuation, misses/Protect/immunity, cancellation ordering,
four concurrent full turn lifecycles, fatigue, JSON continuation replay,
unsupported atomic rejection, and a genuine KO → replacement → next-turn flow.
It compares action/target/residual results with the original-C vectors.

`source_probe.py` authenticates every used source file against pinned commit
`037335f4c725d7c9aecdac87066f2002b4bd7e14`, extracts C unchanged and compiles both
optimized and UBSan variants. There are 35 vectors, from 11 exact functions or
case fragments. The host C/H files contain declarations, presentation stubs,
bounded effect dispatch and fixtures only. They do not call the remake or
reimplement the extracted commands. The action fixture explicitly supplies the
accuracy/critical/damage draw positions, with script order asserted; it is not
a full damage or ROM emulator. Six residual vectors execute the actual
`ENDTURN_THRASH` body and are compared to the shared singles residual owner.
Eight target vectors execute the actual action-time random-target body and are
compared at all four slots. `source-vectors.json` stores hashes of exact upstream
inputs/fragments and authenticated move metadata. SHA-256:
`cb4baf14b84ed19297ca7a2000e8c5b25c30f090aeb61d0d0021dea96866a66f`.

Source anchors: `battle_scripts_1.s:585` (Rampage), `:3713` (paralysis script),
`battle_script_commands.c:1122` (PP), `:2405` (confusion), `:2600` (recharge),
`:2719` (Thrash duration), `:2774` (chance/CERTAIN), `:6849` (initial Thrash effect),
`battle_util.c:203` (cancel), `:216` (inability), `:953` (Thrash residual),
`battle_main.c:2930` (sleep cleanup), `:3642` (recharge timer), `:3989`/`:4102`
(locked current move and action-time target).

## Reproduce

From a checkout with the official local .NET 10 toolchain configured:

```
bash tests/Game.SharedRechargeRampage.Tests/run.sh CONTENT_ROOT PINNED_POKEFIRERED_ROOT NEW_OUTPUT_DIRECTORY
```

The runner sources `scripts/env.sh`, uses an offline-only NuGet configuration,
serial MSBuild (`-m:1 -nr:false -p:UseSharedCompilation=false`), and holds the
shared `/tmp/firered-remake-build.lock` only for compilation. Runtime evidence
is written to the new output directory; the test requires actual hosted runtime major 10.
This is a focused Domain/Content check, not a historical whole-suite, GUI,
campaign doubles, or physical audio acceptance run.

## Combination with concurrent families

The baseline does not contain the concurrent Substitute/sleep/target-mark
migration. Preserve those gates and dispatch additions when applying this
patch. In its new `FrameEndTurnContext.SleepResidual.cs`, extend
`YawnCancelMultiTurn` to include `WildBattleCalledLockKind.Rampage` alongside
Charge/Rollout and emit `"called-" + locked.Kind.ToString().ToLowerInvariant()`.
The integrated runner always enables `--combined`; direct executable calls can
use that flag after composition. Its additional group exercises Yawn cancellation and
recharge expiry retaining an independent Lock-On mark. The normal baseline run
does not claim these combined checks passed. The recharge timer preservation
change is already included here. Keep the parent's Focus Band resultmessage
hooks in `SourceBattleRecipes.ActionCancellers.cs` and SinglesRecipeBridge;
this patch does not replace them.

The main composition is accepted separately in `evidence/shared-dynamic-freeze-rampage-20261011/README.md` (14 groups, 863 checks).

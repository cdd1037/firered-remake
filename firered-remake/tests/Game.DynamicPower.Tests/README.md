# Shared HP, friendship and weight power

This focused suite targets immutable product baseline
`88543cb9ee09fd1a56a5567bc1aaa7bc0aeea900` and original FireRed source
`037335f4c725d7c9aecdac87066f2002b4bd7e14`.

Exactly seven authenticated moves enter the shared recipe: Return, Frustration,
Flail, Reversal, Eruption, Water Spout and Low Kick. Actual four-position actions,
complete turns and canonical singles call the same `SourceRecipeContext` owner.
Historical profiles retain their existing execution paths.

## Reproduce

From this candidate checkout:

```sh
bash tests/Game.DynamicPower.Tests/run.sh CONTENT_ROOT PINNED_SOURCE_ROOT NEW_OUTPUT
```

The content root must contain the existing complete authenticated 386-species and
354-move catalog resources. They are read in place. The runner sources the official
`CONTENT_ROOT/scripts/env.sh`, uses the local offline package configuration, locked
restore, `/tmp/firered-remake-build.lock`, `-m:1 -nr:false`, and
`-p:UseSharedCompilation=false`. Output must be new. Build and execution logs stay
in the supplied output directory; candidate binaries stay in this checkout.

The source oracle can be verified separately:

```sh
python3 tests/Game.DynamicPower.Tests/source_probe.py --source PINNED_SOURCE_ROOT
```

It authenticates pinned source bytes against Git before extracting unchanged
commands, tables and `GetScaledHPFraction`. Its optimized and UBSan runs must
agree. The committed immutable JSON contains 135 grouped vectors: 147,820 HP
command executions (both move IDs in each HP family), all 512 friendship inputs,
all 65,536 u16 weights, and source cache/critical/variance observations. Damage
calculation is stubbed only at its dependency boundary to observe selected power;
this C harness does not claim full damage or battle conformance. The C# action
suite separately checks damage with a small independent integer transcription and
real authenticated stats/types.

## Frozen compatibility evidence

`singles-baseline-88543cb.json` was captured before production edits, using the
unchanged baseline assembly. Its 504 complete result hashes cover both historical
and canonical profiles, both actors, every family member, three seeds and six HP,
friendship, immunity, protection and held-item states. 474 results remain exact;
30 canonical Low Kick results intentionally move the PP command before accuracy.
All 252 historical results remain exact.

`accuracy-baseline-88543cb.json` was captured from a second unchanged, selected-
source extraction of the same Git commit, with candidate recipes absent. It
contains another 144 full result hashes for Flail, Reversal and Low Kick with
native Hustle, ordinary accuracy, actual called Lock-On, and actual called
Fly/Dive/Dig preparation, on both sides. The normalized hash removes only PP
command cursor fields and checks that plain Low Kick has no other delta. The
additional source corrections come from using the already shared accuracy
command instead of the old singles generic fallback: Hustle lowers physical
accuracy, sure-hit skips its draw, and the three semi-invulnerable states block
ordinary hits. Original hashes are retained, not rewritten to match the candidate.
Of these 144 additional outcomes, 32 remain exact and 112 expose those source
corrections (including Low Kick PP timing where applicable). The separate immutable
`accuracy-hustle-baseline-88543cb.json` adds six source-native Hustle cases with an
accuracy roll above 80 on each side; all six change from the old incorrect hit to
the source miss. Across the frozen corpora, 506 of 654 outcomes are exact and all
148 changes are explicit. Accuracy items remain excluded by existing admission.

All capture switches reject a candidate with `HasDynamicPowerRecipe`; they also
refuse to overwrite an existing baseline. Source hash data and old/new result
hashes are retained in the generated verification result.

## Actual execution coverage

- All seven moves, all four actor positions and every valid selected target;
  genuine chosen move slot 1, untouched neighboring PP, one spread PP cost,
  complete raw LCG draws, bounds/cursors and contact's additional Cute Charm draw.
- Every Flail HP power transition and neighboring integer, friendship endpoints
  and truncation boundaries, low/full Eruption HP, both actor sides and exact
  independent damage arithmetic.
- All six Low Kick tiers using Horsea, Goldeen, Seaking, Clamperl, Wailmer and
  Wailord; a real voluntary switch proves weight uses the incoming live species.
- A faster real Seismic Toss changes HP before Flail/Eruption/Water Spout execute.
  Complete turn continuation is JSON-round-tripped and compared exactly.
- Both spread target passes, per-action power reset, real spread KO/reward pause,
  surviving partner actions, victim action cancellation, and no PP/hit replay.
- Protect, Pressure, Ghost immunity, Water Absorb, Flash Fire, Endure, accuracy
  failure, sleep cancellation, stale entries/revisions, invalid slots, RNG overflow
  and forged move metadata.
- An explicitly isolated recipe-context probe changes live HP after the shared
  preparation command and checks cached Eruption power. This is a command-boundary
  test, not a claim that admitted held items can currently heal HP between passes.

Ordinary donor moves are checked against source level-up learnsets. Clefairy's
Return/Frustration are the two explicit TM exceptions, present in the pinned
`src/data/pokemon/tmhm_learnsets.h` Clefairy entry. Called-action fixtures retain a
real learned Metronome slot rather than fabricating a learned execution move.

No full historical suite, GUI, campaign encounter, new AI, new held-item admission,
four-slot Transform/callers or four-slot Fire-thaw admission is claimed here.

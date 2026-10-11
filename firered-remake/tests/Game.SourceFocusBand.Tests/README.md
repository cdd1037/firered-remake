# Source Focus Band regression and bounded differential

Run `bash tests/Game.SourceFocusBand.Tests/run.sh REAL_PRODUCT_ROOT PINNED_PRET_ROOT NEW_EVIDENCE_DIRECTORY` using the product's official .NET 10 SDK. The suite has no external NuGet dependencies. Builds use the shared short build lock.

The source oracle recompiles eight unchanged functions from pinned pret FireRed commit `037335f4c725d7c9aecdac87066f2002b4bd7e14`, verifies 15 source files against Git blobs, and produces 3,770 deterministic command vectors plus script-order and Endure-lifecycle evidence. Optimized, unoptimized, and UBSan C outputs must agree. The C structs/controller destinations outside the extracted functions are bounded host stubs, not an emulator or ROM.

The C# differential has a narrower, explicit boundary: independent normal/normal2/set rows with ordinary Focus Band or no effect, excluding dynamic Enigma. It independently applies the original integer damage-random multiplier before calling the production post-random FocusRoll/Adjust primitive. It compares adjusted damage, sticky bit, and newly added Endure/HungOn flags. This does not claim that the pure helper consumes RNG, reproduces all original registers, runs OHKO, or proves script reachability. The separate exact-C oracle retains complete observations for those fields.

The gameplay fixtures use source-authenticated species, level-up/TM/tutor move entitlement, StartSourceWild, and actual ResolveMove turns. Live volatile/HP registers are controlled inputs. Seed searches are bounded to 0..128 and stop once the specified outcome is seen. Selected receipts include full actual RNG traces and typed event lists. They cover action-finish cleanup, command reachability/order, sticky multi-hit behavior, Endure precedence, delayed Future Sight state serialization and next-action retention, OHKO, confusion/substitute, and exclusions. Twineedle has no ordinary type immunity, and its only authentic immune target needs unadmitted Wonder Guard; the suite verifies that admission rejection and a real two-hit Twineedle turn, while immunity ordering remains an explicitly separate static-source assertion. Mechanical DamageAdjusted receipts are not item-consumption or displayed item-message events.

`result.json` records counts and failures; `scenario-receipts.json` records observed seeds, traces, and events. A passing source oracle alone is not a passing gameplay suite. See the current result files for the exact completed coverage.

Controlled live registers include lowered accuracy/raised evasion, substitute HP, confusion, both incoming Focus bits, and matching Lock-On target state. Switch cleanup uses a controlled incoming Focus bit; a separate genuine three-turn Future Sight fixture proves that delayed gameplay can produce that state. OHKO tests cover a real Endure action and a sufficient-level matching sure-hit register with a retained Focus draw but no OHKO accuracy draw.

The old ResolvePotion API still rejects a canonical full catalog before execution; the suite confirms its failure is atomic and does not claim canonical potion support. The actual source switch owner is used to verify that a completed non-move action clears incoming slot protection before the opponent attacks a one-HP reserve.

## Main integration verification (2026-10-11)

The combined main candidate passed 18 groups / 71,301 checks, with 202 accumulated move turns, one source switch and 31 selected scenario receipts. The exact original-C oracle produced 3,770 vectors, of which 2,123 ordinary-item rows are compared against the bounded product scalar helper. See [the compact integration result](../../evidence/recovery-20261011/focus-canceller-combined/result.json) and its `focus/` result, scenario receipts and source proof.

The integration also runs shared action cancellers, actual M06 reward/replacement continuation and the unchanged 64-phase legacy single-battle golden. Four-slot held damage remains gated. The main comparison confirms delayed Future Sight and confusion self-damage reach the same canonical Focus adjustment after their own damage-random calculation.

The author runner recreates C vectors from the pinned source, so generated C executables and bulk vector output are not delivery dependencies. Both small authored oracle hosts (`source-prefix.h`, `source-main.c`) must accompany `source_probe.py`. They are included in the explicit required-test-input manifest.

## Four-slot extension

`bash tests/Game.SourceFocusBand.Tests/run-four.sh CONTENT_ROOT PRET_ROOT NEW_OUTPUT`
uses the same held-damage primitive with position-local sticky state. It covers all
four actor positions in an actual Fury Swipes action, per-hit random draws even
after success, one PP cost, no item consumption, action-end cleanup, next-action
failure and atomic publication failure. A complete turn verifies poison bypass,
one EXP settlement and deferred replacement. A controlled legal Future Sight
queue verifies normal2 adjustment and retention through Ready/turn setup, followed
by the real switch action's global SpecialStatusesClear boundary. Casting Future
Sight remains separately gated. Item/switch actions clear special status at the
same completed-action boundary; turn setup does not.

The source primitive and earlier exact-C vectors are unchanged. The first new
probe attempt used unadmitted Splash/Gust helper actions and stopped at capability
validation; the corrected fixture uses actual level-up Barrier/Tackle. Compact
results preserve that fixture correction. Mechanical HungOn receipts still do not
claim the dedicated item result text, native rendering, or campaign acquisition.

## Successful-hit result message

Four-slot execution now emits a separate SourceHeldItemSurvivalEvent at the
source resultmessage command, after committed HP update. A pre-hit mechanical
DamageAdjusted event remains silent, including Explosion adjustment followed
by a miss. Multi-hit produces its result after all hits, and delayed Future Sight
uses its own result boundary. Endure retains precedence. The adapter retains
target, move and item, and source text resolves through the existing battle-text
catalog. Dedicated B_ANIM_FOCUS_BAND is still unimplemented. A subsequent canonical
singles increment wires successful result text and shared confusion self-hit
results. Run `run-messages.sh CONTENT_ROOT PRET_ROOT NEW_OUTPUT`, or
`./dev test-focus-messages NEW_NAME`, for the seven affected groups. The four-slot
mode additionally checks confusion at all positions. See
[focused integration evidence](../../evidence/focus-result-messages-20261011/README.md).

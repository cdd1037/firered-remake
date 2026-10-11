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

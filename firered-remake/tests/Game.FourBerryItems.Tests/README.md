# Shared campaign berry phases

Run `bash tests/Game.FourBerryItems.Tests/run.sh CONTENT_ROOT PINNED_PRET_ROOT NEW_EVIDENCE_DIRECTORY`.
The runner sources the selected toolchain environment, uses the shared build lock and an empty
NuGet feed, and requires the exact local pret commit `037335f4c725d7c9aecdac87066f2002b4bd7e14`.
No reference-repository script is executed.

The independent C oracle compiles unchanged normal/move-end case bodies for seven active berry
effects. Its 35 fixed cases cover status cures, Toxic counters, confusion, half-HP floor, cap,
fainted HP and phase differences. Six directly relevant source files are authenticated against
pinned git blobs. Twelve existing campaign item records are checked, including the five
`HOLD_EFFECT_NONE` Pickup berries. This is a semantic slice, not a whole-ROM execution.

Integration uses actual four-action turns and authenticated natural moves, including the real
Twins definition with reserve Meowth, slot-zero/slot-two voluntary entry differences, Baton Pass,
actual KO entry, immunity ordering, residual threshold crossing, two opponent residual KOs,
persistent party item synchronization, position used-item history and JSON continuation.
Unsupported held items (including Lum, Leppa, Sitrus and Enigma) still fail atomically. A predecessor
negative fixture now uses unsupported Sitrus instead of newly implemented Oran.

The rules share berry predicates and HP arithmetic with historical singles. Original-profile
compatibility remains unchanged, including its existing HP and berry capability predicates.
Four-slot move-end scans all numeric positions after immunity, without an added HP/absence filter.
Normal items are HP guarded; only Items1 restores Oran. Initial switch-in does not consume these
berries. The fainted-action callback runs status items on position zero between Trace and Forecast.
This preserves the source's unusual voluntary-switch asymmetry and the already approved deferred
KO replacement boundary. Recycle, other item effects and broader move capabilities remain gated.

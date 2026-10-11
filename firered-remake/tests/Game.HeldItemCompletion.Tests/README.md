# Held-item completion checks

One executable contains rule vectors and real singles/four-position turns. `source_probe.py` verifies the pinned Git blobs, extracts and compiles the original item phase cases, flavor table, PP calculation and held-stat arithmetic, and generates private comparison vectors. It does not run an upstream script. Generated original C and imported tables remain ignored under `generated/`.

Prerequisites: pinned source `037335f4c725d7c9aecdac87066f2002b4bd7e14`, authenticated mechanics/move content, the C15 item pack rebuilt with `python3 scripts/source-battle-items/import.py PINNED_SOURCE PROJECT_ROOT`, official `scripts/env.sh`, and the local-only recovery NuGet config. No alternate SDK/runtime is used.

Run from the project:

```sh
bash tests/Game.HeldItemCompletion.Tests/run.sh PINNED_SOURCE
```

Optional second and third arguments identify an existing mechanics content root and separate C15 item-pack root. The test binds the separate authenticated pack only when the mechanics root has no pack. An existing corrupt pack fails loading.

The suite covers 599 original item-phase/PP/nature/random-stat vectors, 1,348 original type/species stat vectors, 15 groups and 35 real turn executions. Original callback vectors distinguish Normal, Normal(moveTurn), MoveEnd and SwitchIn. Stat vectors include wrong species, Raichu, all source types, integer truncation and u16 assignment boundaries. Some direct rule vectors deliberately use synthetic register values; they are not campaign progression claims.

Actual transactions cover Transform/Leppa with PP Ups, flavor confusion, White Herb after initial and mid-turn Intimidate and Baton Pass, Recycle, Knock Off, Leftovers, generic evasion and critical holders, and Light Ball special versus physical attacks. Mimic PP ownership and Lum's confusion-only party-status write also have focused checks.

`BaselineParity.cs` is an alternative host in this same project (`-p:Parity=true`). Build it against the exact f95 snapshot via `-p:SourceRoot=ABSOLUTE_BASELINE_ROOT`, then against this candidate, using separate output directories. Parse its final `HELD_BASELINE_PARITY_END` marker and compare the entire first-line JSON, including states, events, RNG and continuation receipts. The retained 106-result baseline/candidate comparison is byte-identical. The baseline host is intentionally limited to previously admitted, unaffected holders; newly admitted items and explicit canonical corrections are tested separately.

Smoke Ball's authenticated canonical ordinary wild run route is covered through all six trap bypasses, item precedence over Run Away, the real after-faint No branch, permanent identity and held-item retention, event adapter round trips, and exact source flee text. The baseline host additionally records six ordinary no-item/Run Away run receipts for byte-exact comparison. Amulet Coin prize settlement, dynamic e-Reader Enigma data, link/Tower special modes, native UI and audible-output acceptance are outside this harness. Historical regional profiles remain gated by their existing item admission and branches.

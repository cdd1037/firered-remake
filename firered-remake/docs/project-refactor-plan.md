# First-clear refactor and stop boundary

Historical milestone completed at `8940b9b`: final source synchronization, stage10 full backup and bounded cleanup finished, followed by the requested pause. The user's 2026-10-12 23:42 UTC instruction to continue has now resumed postgame development; [National Dex](national-dex-campaign.md) is the next bounded node. The original milestone scope and measurements below remain historical records.

The earlier 2026-10-12 instruction was to finish the first main story, then perform a bounded project-level refactor and simplification, synchronize source and the complete Drive package, clean reproducible temporary material, and pause for further instructions. It did not itself authorize postgame development; the later explicit continue instruction is recorded above. The pause was a development stopping condition, not a gameplay restriction on a completed save.

## First-story exit reached

The actual eight-badge route through Victory Road is retained. The inherited natural League test party won Lorelei, lost to Bruno, legitimately captured a Golbat and won Lorelei again, but exhausted its recovery resources. Its complete history stops at generation 41923 without claiming a natural first clear.

The separate, explicitly controlled reasonable-level party and supplies fixture actually defeated the unchanged original Bruno, Agatha, Lance and Champion through public commands. EXP and money settled normally; the Hall file was written at generation 42922 and loaded by a separate process. Original credits and ResetRequested completed at generation 42943. The 1,020-command chain contains zero synthetic victory receipts. This closes the bounded product-development exit while preserving the natural generation-41923 file unchanged. It does not claim that the inherited natural party cleared the League. See [the controlled report](../tests/Game.PokemonLeagueControlledRoute.Tests/evidence/REPORT.md) and [remaining scope](first-clear-remaining-scope.md).

Clearly distinguish source/logic checks, headless view checks and unavailable native visual/audio comparison. Remaining visual fidelity and regional service gaps stay explicit.

## Integrated bounded changes

1. `Game.Tooling.SaveUpgradeProfiles` now owns 37 exact predecessor/target/migration entries and seven optional-root admissions. The atomic staging/publication block and migration algorithms are unchanged. Seven late-profile launchers share a single helper with their original argument, environment, save-folder and exit behavior; `launch-current.sh` selects the complete first-League profile and delegates its one required import check.
2. `CampaignSession.OccupiedFieldMaps` retains all 218 map keys but overlays actor occupancy only on the current map and direct neighbors, which are the maps the movement operation can read. The bounded candidate measured 88.57–92.46% lower allocation across five locations, with exact transitions/events/RNG preserved in 35 cases. These are allocation measurements, not elapsed-time claims or a new cache.
3. Department-store elevator coordinates use `GetSourceCell` directly in two call sites, avoiding implicit compact-map expansion. Current elevator maps are rich maps, so no large current speedup is claimed. All ten elevator entries and compact/rich equivalence were checked.
4. Remove only the completed one-off `package-victory-road-rules.py` and `package-victory-road-escape-rope.py` patch assemblers. Permanent importers, `prepare-victory-road-hooks.py`, the first-Sevii preparation contract and all pinned reconstruction inputs remain. Unique source, save and execution evidence is preserved.

Candidate characterization remains in [the compact proof index](../evidence/project-refactor-20261012/candidate-proof-index.json): 557 registry assertions, 1,303 routing/publication assertions and 11 actual CLI comparisons; 154 launcher before/after pairs; 4,993 cell checks; 305 movement checks. These are earlier candidate results, not newly repeated integration passes.

The final normal Godot and Tooling builds passed with zero warnings/errors (117.29 and 10.01 seconds). The final registry host passed all 557 checks. The final Application smoke used one public East move from the real generation-40778 Indigo Center checkpoint under both pre-refactor and final DLLs. All 18 checks per cohort passed with identical complete states, outcomes, events, both RNG streams and strict-save bytes. The final normal Godot cohort authenticated the unchanged controlled battle observation and passed all 17 Hall/save/Continue checks across six headless Draw phases; its newly written save is byte-identical to the original true Hall file. No historical route or backup runtime was replayed. See [field comparison](../evidence/project-refactor-20261012/final-field-smoke/comparison-summary.json), [normal builds](../evidence/pokemon-league-combination-20261012/final-refactor-build.log) and [final view result](../tests/Game.PokemonLeagueCompletionView.Tests/evidence/controlled-20261012T191519/controlled-run-result.json).

Each accepted change needs a normal build and a few affected checks. Do not rerun historical routes, every battle matrix, or backup restore environments. No arbitrary file/line-count target and no wholesale battle or catalog rewrite.

## Final delivery

Record the resulting source commit, required author inputs, reconstruction contracts, first-clear checkpoint and unresolved feature list. GitHub receives the normal private source increment. The complete Drive package is checked for structural and byte integrity under the current backup policy; it does not trigger another reconstructed runtime/replay exercise. Delete only identified rebuildable caches or confirmed duplicate copies, preserving unique sources, checkpoints and evidence. Then stop development pending the user's next instruction.

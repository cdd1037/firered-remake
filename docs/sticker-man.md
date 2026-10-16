# Four Island Sticker Man

The original Four Island House2 is now part of the existing Four Island map package: town door (25,26), original indoor exit (4,7), original MAN actor (4,4), source scripts/texts and shared presentation atlas. Runtime uses generated resources only.

Pinned pret 037335f4 `data/scripts/trainer_card.inc` controls category choice, cancellation, first conversation, award, repeats and upgrades. The native `StickerManGetBragFlags` query exposes the shared counters through catalog-defined milestone metadata. Rewards are the original `VAR_HOF_BRAG_STATE` and `VAR_EGG_BRAG_STATE` values 1–4; no item or money rewards are invented.

- Hall of Fame tiers: 1–39, 40–99, 100–199, 200+.
- Hatched egg tiers: 1–99, 100–199, 200–299, 300+.
- The native query truncates Hall of Fame counts to ushort and saturates egg counts at 65535, as in source.
- Link wins remain unavailable and zero. No multiplayer victory or sticker is fabricated.
- Trainer Card back rendering remains deferred. Award storage and original acknowledgement dialogue are implemented; this does not claim visible stickers on the card back.

## Lifetime hatch statistic and old saves

Pinned `src/field_control_avatar.c` increments the stat when `ShouldEggHatch()` succeeds, before scheduling the hatch script; `src/overworld.c` saturates it at 0xFFFFFF. This implementation records it in the shared native hatch transaction inside that locked, unsaveable script sequence.

New games initialize `GAME_STAT_HATCHED_EGGS` at zero. The shared gift/day-care hatch transaction increments once, independently of later receipt pruning, animation, nickname choice and save/Continue. Retained hatch receipts are only a validation lower bound, never a reconstruction of lifetime totals: release and NPC trades can prune old breeding receipts.

A save missing this counter is rejected with `save_hatched_egg_count_missing_migration_required`. The original generation-57604 representative checkpoint has no trustworthy lifetime total and remains unchanged; it is no longer claimed to be a current valid Continue baseline. No counter was inferred from its party, boxes or surviving receipts.

Application regression fixtures explicitly use a synthetic test-only total of 1000 after checksum decoding, then run normal current-state validation. That controlled fixture is not a migrated player checkpoint or evidence of its historical total. The raw-checkpoint rejection test remains separate. Users with an independently known lifetime count may explicitly convert to a new destination:

```sh
dotnet run --project src/Game.Tooling -- set-known-hatch-count SOURCE NEW_DESTINATION KNOWN_LIFETIME_COUNT
```

The count must be an explicit integer 0..16777215. The converter refuses overwriting a destination or replacing an already-present counter, validates the complete resulting save, and preserves original bytes, generation and RNG. Its operator-provided total is asserted input, not independently proven history; no automatic estimate is offered.

## Verification scope

Boundary and repeat tests use isolated controlled counter transitions rather than fabricated league/hatching histories. Physical door, interaction, storage and Continue tests use the clearly marked controlled new-schema test fixture. Shared hatch tests exercise the walking/script hatch transaction for gift and bred eggs, animation, nickname choice and serialization. Real speaker audio and Trainer Card back visuals are outside this slice.

## Checked 2026-10-16

Toolchain: .NET SDK 10.0.401, Microsoft.NETCore.App 10.0.12. All builds used the repository's serial command:

```sh
source scripts/env.sh
dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~StickerManTests|FullyQualifiedName~EggHatchStatisticTests|FullyQualifiedName~GiftEggOwnershipTests|FullyQualifiedName~LoreleiHouseTests|FullyQualifiedName~HallOfFameSnapshotTests|FullyQualifiedName~WaterPathSizeRecordTests|FullyQualifiedName~GeneratedOnlyCampaignTests|FullyQualifiedName~CurrentCampaignFixtureTests' -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/Game.Domain.Tests/Game.Domain.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~FieldMilestoneCollectionTests' -m:1 -nr:false -p:UseSharedCompilation=false
python scripts/checks/test-sticker-man-import.py
python scripts/checks/test-story-menus.py
```

Final results: build 0 warnings / 0 errors; 112 application cases, 2 domain cases, 6 Python cases passed, no skipped cases. Earlier build attempts exposed a temporarily missing initializer while work was in progress and an MSTest collection-assertion analyzer error; both were corrected before the final run. No full-suite, native-window visual, or real audio pass is claimed.

CLI conversion checks used explicit synthetic counts 1000 and 0, writing only temporary test files. Both passed complete save validation. Missing and negative count, destination overwrite, and a source with a counter already present were rejected. Source bytes, generation, RNG and every payload field other than the intended added statistic were compared and preserved. Those successes establish converter behavior, not the legacy player's historical count.

Final review also checks all Application tests because the required save statistic affects their common fixture. The first complete run found one old raw-checkpoint test still assuming automatic compatibility (531 passed, 1 failed). That test now checks preserved files and an explicit migration-required outcome rather than silently patching the player save.

Default launch does not select a checkpoint; the launch script passes one only when explicitly supplied. Player Continue/load now classifies missing hatch totals as `MigrationRequired` and displays “Save needs a known hatch count. / Files kept. Convert explicitly.” Explicit startup checkpoints remain guarded by the existing startup exception handler (diagnostic plus exit code 21). Conversion CLI failures now return a concise diagnostic and exit code 2 instead of an unhandled exception. Process hash tests use their own EngineeringState and do not depend on the old player checkpoint.

Source fetch inputs were consolidated into the existing shared `fetch-story.lock.json`; no per-Sticker-Man lock remains. Offline materialization passed for all 22 shared inputs.

Actual headless Godot startup with the unchanged old checkpoint was also exercised using isolated XDG directories and Dummy audio. It returned the expected controlled exit code 21 and `save_hatched_egg_count_missing_migration_required`, through the startup error handler. This is a refusal-path check, not a playable visual/audio acceptance run. Invalid-count conversion CLI was rechecked after error handling and returned code 2 with usage guidance, without an unhandled exception.

Full Application rerun after final review: **533 passed, 0 failed, 0 skipped** (1m54s). Save-feedback presentation checks: **4 passed**. Full Process assembly: **1 passed**. Final serial solution rebuild: **0 warnings / 0 errors**. Commands used the same `--no-build --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` switches; Application and Process ran without a filter, Presentation used `--filter FullyQualifiedName~SaveFeedbackTests`. The previously reported 112 focused cases are a subset of the complete Application run, not additional distinct tests.

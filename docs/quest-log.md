# Quest Log: source-backed recording work in progress

## Status and activation

The Quest Log is now connected to accepted campaign commands and normal Continue, with read-only input/tick/draw playback and successful-save recording boundaries. The integrated full solution build passed at 15:41 UTC; full application regression verification passed 749/749 at 15:51 UTC. It is **not complete source scene parity**. The specific remaining differences below must not be represented as source-complete playback.

The existing `StoryState.QuestLogState == 0` save/script restriction remains intentional. This design does not run the game in its original mutable script replay mode. Recorded fields are presentation data, and playback has no campaign command executor, RNG or reward ownership. It cannot grant items, repeat battle settlements, alter party/storage, move the actual player or modify flags. The actual restored session remains untouched throughout playback and skipping.

## Pinned source and reconstruction

Source revision: pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`.

- `src/quest_log.c`: recording, movement/gfx actions, Continue playback, A next scene, B final saved scene, caption durations and original field restoration.
- `src/quest_log_events.c`: event sizes and text variants, first-visit arrival flags, disabled maps, stopped-recording start gates, departures and special-encounter exclusions.
- `src/quest_log_battle.c`: source trainer classes, winning species and ending-HP flavor text.
- `src/field_specials.c`: inside/outside departure pairs.
- `src/event_object_movement.c` and the existing application ObjectSlots implementation: actual 16-slot active object pool.
- `include/global.h`, `include/constants/global.h`, `include/constants/quest_log.h`: four scene slots, 128-u16 script per scene, 32 event-pointer playback limit.
- `src/data/text/quest_log.h`: original English captions, fetched from the pinned official repository; not reconstructed from memory.
- Source map JSON, region-map sections and object graphic constants supply labels and dynamic graphic identities.

Regenerate with:

```sh
python scripts/imports/presentation/import-quest-log.py
python scripts/imports/presentation/import-quest-log.py --check
```

The importer also accepts `--raw`, `--world`, and `--out`. `generate(raw, world_path)` only reads and returns data. `--check` compares canonical output without modifying generated resources. The normal runtime reads only `content/generated/data/quest-log.json` through `PretQuestLogSource`. Source references and reconstruction recipes stay in tools/docs, not in saves.

## Bounded representation

`CampaignState.QuestLog` is optional. New games and old saves without recordings have no fabricated scenes. Four recent scenes are retained. Each scene starts with the actual active object-slot projection, not nearest-N arbitrary actors. Actions contain display coordinates, facing/visibility, changed graphics, bounded spawn presentation data or a source event identifier with the caption's bounded parameters. No complete campaign, command stream, state hash, receipt history or per-command world snapshot is stored.

The action stream uses a 256-byte **logical source-equivalent budget**, reserving two bytes for scene end, with source event costs imported from `sQuestLogEventCmdSizes`. Movement costs eight logical bytes; projected graphic/spawn additions reserve additional space. At most 32 actions are retained, and event arguments, identifiers and object counts are bounded. JSON property names and stable string identities mean this is not a claim that the serialized JSON occupies exactly 256 bytes. This is not an original GBA save format.

The source stops movement recording when the scene buffer is full until a qualifying event starts another scene. This behavior is not an indefinitely growing movement history. Script `QuestLog_CutRecording` is respected. A newly restored session does not append movement to a prior saved scene. The public `StopQuestLogRecording()` is the intended successful-save boundary hook.

## Current implementation

- Accepted-command observer wrapper; rejected/faulted commands never publish recordings. The wrapper is the live Execute callback; it preserves accepted no-op state identity when no recording changes.
- Source-backed arrival flags, disabled maps, ordinary direct departure pairs, party/held item changes, used items, shop-exit buying/selling aggregation, PC item deposits/withdrawals, Pokemon deposits/withdrawals, box move/shift/group move variants, selected field moves, opening rival and ordinary single-trainer class-specific wins, wild wins/catches and source static-species exclusions.
- Original text expansion and region names, including source event text durations (95/127/191/255 frames).
- Bounded map/actor projection with interpolated movement, A next scene and B skip to the saved-location caption. A final saved-location projection is transient, not another durable scene.
- Separate rendering that never calls LoadMap/Synchronize with recorded state and does not replace the actual campaign session or field poses.
- Original indexed Quest Log description-window tiles, source palette and text/header placement are imported and rendered; no invented sepia overlay.
- Save structure validation and a generated-only source catalog.

## Remaining source parity obligations

1. **Event closure:** verify all single-player used-item/TM/HM and held-item PC variants, story-item events, double-trainer results and exact last-victor/flavor data. NPC in-game trades must not be equated with the source's link-trade event without evidence.
2. **Repeated events:** original wild aggregation, repeated-event counts, departure suppression and Game Corner played-slots tracking are not yet fully ported. The new `CampaignQuestLogShopCapture` follows source `shop.c` with at most two transient buy/sell entries until shop exit (quantity capped at 999, money at 999999), exact quantity/multiple-transaction text variants, and no durable shopping history. Three independent shop tests passed in the refreshed application batch.
3. **Departure state:** direct pairs are extracted, but original nested entrance retention, Rocket Hideout/Game Corner transitions, exact Gym badge-dependent text and special-exit distinctions require the full source state-machine cases.
4. **Scene actions:** initial/delta actors, visibility, new actors and graphics are represented, but source multi-step action timing, simultaneous NPC movement, waits/emotes, avatar modes, input actions and movement-path replay are not yet complete. Current simple interpolation is not evidence of full source action parity.
5. **Field appearance:** replay uses map layers and recorded actors but does not yet reconstruct every historical map edit/layout, border/connected-map effect, weather/palette. The description-window art and palette are now reconstructed from pinned source assets.
6. **Native integration:** transaction, Continue, successful-save stop, input ownership and play-time exclusion are connected. Verify New Game, missing/old recording, repeat Continue, full buffer, skip/current-scene/last-scene, quit, backup save and restored final map in the running client.
7. **Native verification:** no Quest Log native window, capture or audio verification has been performed by this worker. Existing test passes do not establish this.

## Evidence so far

Focused serial .NET command (SDK/runtime from the existing `scripts/env.sh`):

```sh
source scripts/env.sh
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --filter FullyQualifiedName~CampaignQuestLogTests
dotnet test tests/Game.Content.Tests/Game.Content.Tests.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --filter FullyQualifiedName~QuestLogContentTests
```

At 2026-10-16 14:50 UTC: Application 8 passed / 0 failed / 0 skipped; Content 1 passed / 0 failed / 0 skipped. An initial constant-vs-constant content assertion was rejected by the MSTest analyzer; it was removed and the content run passed. Subsequent source-gap changes need a fresh compiler/test pass. Source reconstruction `--check` passed after the importer interface update. At 2026-10-16 15:19 UTC, the refreshed batch passed Application 11/11, Content 1/1, ResourceImport 2/2, and the Godot project built with zero warnings/errors. This includes cumulative graphics, actor-slot removal and recorded-duration interpolation regressions. No activation or native playback was tested.

At 2026-10-16 15:36 UTC, the full solution built with zero warnings/errors and the Quest application filter passed 14/14 (including shop aggregation). Combined Quest/Opening/Intro Content 7/7, Opening Presentation 5/5 and ResourceImport 4/4 also passed. The new Waterfall semantic detector compiled; its independent recorder vector remains to be added.

At 2026-10-16 15:51 UTC, the integrated recorder passed the complete Application suite (749/749), focused Quest Content (1/1), and ResourceImport (2/2). An initial integrated run found exactly one failure in accepted no-op state identity; the wrapper was corrected to preserve the original decision/state reference when its recording is unchanged. The successful rerun includes a live accepted party reorder, idempotent command retry, save/restore, pure skip, and successful-save recording-boundary test.

Actual regression commands:

```sh
source scripts/env.sh
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-build --no-restore -m:1 -nr:false
dotnet test tests/Game.Content.Tests/Game.Content.Tests.csproj --no-build --no-restore -m:1 -nr:false --filter FullyQualifiedName~QuestLogContentTests
dotnet test tests/Game.ResourceImport.Tests/Game.ResourceImport.Tests.csproj --no-build --no-restore -m:1 -nr:false --filter FullyQualifiedName~QuestLogImportTests
```

The source-only shop batch boundary correction, original window assets and noncombat field-event detectors were included in the subsequent full solution builds at 15:54 and 16:01 UTC.

At 16:02 UTC a clearly labelled fresh-game native Continue fixture was produced through public movement/turn commands, with no lifetime-count substitution. It contains one scene and 25 actions. Codec roundtrip and both pure full playback and skip preserved the entire serialized restored state, including Pallet Town (6,8), money 3000 and RNG cursor 1. This is fixture/projection evidence, not native visual verification. The isolated profile and command report are under `/workspace/shared/firered-quest-visual`; setup is documented there in `README.txt`.

At 16:03 UTC the combined 16:01 build passed focused Application 15/15, Content 1/1 and ResourceImport 2/2 (`/tmp/firered-quest-final-focused.log`). Commands were `dotnet test` for the three corresponding test projects with `--no-build --filter FullyQualifiedName~CampaignQuestLog` (Application) or `--filter FullyQualifiedName~QuestLog` (Content/ResourceImport), and `--verbosity minimal`. This includes the corrected shop batch boundary and independent original window import vectors.

At 16:04:41 UTC the parent completed and closed one bounded native Continue sample using the isolated fresh-game fixture. The client log confirmed `hasContinue: true` and `playerContinue` generation 48 at 34235 ms, with no error. The observed screenshot showed the expected Pallet Town house/field return. The transient recap overlay was missed between observations, so its appearance was **not visually verified**; the full/skip state invariants above remain automated projection evidence. Native log: `/workspace/shared/firered-quest-visual/native-quest-1603.log`; sampled assembly MVID: `089f67bd-4992-4894-898f-539681759e37`.

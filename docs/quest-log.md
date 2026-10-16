# Quest Log: source-backed recording work in progress

## Status and activation

The Quest Log is now connected to accepted campaign commands and normal Continue, with read-only input/tick/draw playback and successful-save recording boundaries. The version2 full solution build passed at16:39 UTC with zero warnings/errors; focused Quest Application36/36, Content1/1 and Import2/2 passed. The earlier full Application regression passed749/749 at15:51 UTC. It is **not complete source scene parity**. The specific remaining differences below must not be represented as source-complete playback.

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

`CampaignState.QuestLog` is optional. New games and old saves without recordings have no fabricated scenes. Format version2 has a single overlapping timeline interpreter. Earlier experimental recordings with a missing/unsupported version fail clearly with `save_quest_log_version_unsupported`; no automatic campaign rewriting or legacy interpreter is retained. Four recent scenes are retained. Each scene starts with the actual active object-slot projection, not nearest-N arbitrary actors. Actions contain display coordinates, facing/visibility, changed graphics, bounded spawn presentation data or a source event identifier with the caption's bounded parameters. No complete campaign, command stream, state hash, receipt history or per-command world snapshot is stored.

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
2. **Repeated events:** general repeated-event counts and departure suppression are not yet fully ported. Game Corner played-slots tracking now follows the original reel-start marker: one transient live-recorder boolean is set only on an accepted increase in PlayedSpins and consumed by an eligible departure; no durable field is added. The new `CampaignQuestLogShopCapture` follows source `shop.c` with at most two transient buy/sell entries until shop exit (quantity capped at 999, money at 999999), exact quantity/multiple-transaction text variants, and no durable shopping history. Three independent shop tests passed in the refreshed application batch.
3. **Departure state:** direct pairs are extracted, but original nested entrance retention, Rocket Hideout/Game Corner transitions, exact Gym badge-dependent text and special-exit distinctions require the full source state-machine cases.
4. **Scene actions:** the one timeline now retains multi-step scripted paths/source action labels and parallel actor start times; captions advance independently, with source-duration waits and ordinary speed profiles. Remaining gaps include idle host-input delays, exact script-event timing provenance, emote/effect drawing, forced-movement nonuniform pixel schedules and avatar modes. This is not full source action parity.
5. **Field appearance:** historical layout identifiers, bounded sparse metatile changes, map borders and connected source maps now have a pure Quest-local renderer using existing cache owners and patch atlases. Current-save palette transformation no longer leaks into historical actor art. Source historical weather/palette, avatar graphics and music remain incomplete. Signed connected-border CUT edits now use the same pure direct-connection resolver as the field implementation; their historical neighbor layers receive recorded tile patches. The follow-on passed the combined full solution build and focused Quest tests (37/37) at16:52 UTC. The description-window art and palette are reconstructed from pinned source assets.
6. **Native integration:** transaction, Continue, successful-save stop, input ownership and play-time exclusion are connected. Verify New Game, missing/old recording, repeat Continue, full buffer, skip/current-scene/last-scene, quit, backup save and restored final map in the running client.
7. **Native verification:** the parent verified the version2 Continue overlay, recorded actor pose, source saved-location window, skip and final field return in one bounded native sample (details below). Full natural playback, every historical map variant and historical audio remain unverified.

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

At 16:12 UTC the in-place source wild aggregation passed focused Application 23/23, including eight new vectors (`/tmp/firered-quest-wild-tests.log`). Two species and saturating 255 defeat/catch counts update one existing event, preserve the logical byte/action budget, and select the original singular/plural mixed variants. An intervening semantic event, map change, save/restore or cut ends this aggregation; movement overflow does not. No additional durable fields were introduced.

At 16:14 UTC the refreshed Application project/tests built successfully and focused Quest tests passed 25/25 (`/tmp/firered-quest-champion-tests.log`). Champion playback now emits all three original caption phases from one bounded event and a transient substep cursor; scene skip omits remaining phases safely. A regression also verifies graphic changes for an actor that first appeared after the scene began. Exact double-battle last-victor provenance remains outstanding: the source updates last opponent species and `gBattlerAttacker` at each opponent faint, while the current application event lacks that attacker provenance.

## Version2 appearance and timeline verification

The source QuestLogScene footprint is0x668 bytes, but our sparse visual projection is a different design: header8 +16 object slots at20 logical bytes + layout2 +210 edit tuples at5 logical bytes +256 action bytes =1636 logical bytes. This allocation is informed by the original fixed footprint, not a claim that those were original fields or that JSON occupies that many bytes. Only a layout identifier and sparse current-map metatile edits are copied. No campaign flags, variable arrays, command history or per-command world snapshots are added. Tile/layout deltas share the existing action budget. An oversized initial projection is not recorded; a delta that cannot fit closes the scene before any partial patch is published.

The version2 full build command was `dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --verbosity minimal`; it passed with zero warnings/errors in43.39 seconds (`/tmp/firered-quest-v2-build.log`). The no-build Quest test filters then passed Application36/36, Content1/1 and ResourceImport2/2 (`/tmp/firered-quest-v2-tests.log`). New vectors cover version rejection, simultaneous caption/player/NPC tracks, source step durations, initial sparse tiles, tile removal, overflow without partial rooms, and catalog rejection of unknown layouts.

The isolated fresh-game public-command fixture was regenerated as format2/generation153: four bounded home-departure scenes,124 total actions, final Pallet Town(6,8) facing North, money3000 and RNG cursor1. Full playback and skip again left the entire restored serialized campaign unchanged. Its roughly32-second timeline provides a longer native observation window. This remains a labelled synthetic fresh-game fixture, not a completed playthrough or visual proof. Probe log: `/tmp/firered-quest-v2-fixture.log`.

At16:44:04 UTC the parent closed a second bounded native sample using format2/generation153. The actual Continue flow displayed “Previously on your quest…” with countdown3 and a recorded actor pose. Backspace skipped to the readable original “And QUEST saved the game at this location: PALLET TOWN.” window; Enter then returned to the actual saved Pallet Town(6,8), facing North. No errors were observed. This verifies the overlay, source-window text, skip and final field return; it does not verify every historical map variant or a full natural, unskipped recap. Native log: `/workspace/shared/firered-quest-visual/native-quest-v2-1642.log`; sampled assembly MVID: `fd73d865-5d92-42d0-8021-1f190cfe99b9`.

At16:51:58 UTC the combined full solution build passed with zero warnings/errors including the signed connected-border Quest projection. `dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~CampaignQuestLog --verbosity minimal` then passed37/37 (`/tmp/firered-quest-border-tests.log`). The additional regression accepts a source-resolved north-border tile, preserves that immutable projection through skip, and rejects coordinates beyond the direct source border. Neighbor rendering uses the existing CUT art patcher and recorded tile values; no live campaign flags are copied.

At17:04:03 UTC the candidate90 full solution build passed with zero warnings/errors, including Game Corner departure recording. The same focused no-build Quest command passed38/38 (`/tmp/firered-quest-gamecorner-tests.log`). The new vector verifies no Game Corner departure caption without playing and the exact original CELADON CITY caption after playing. Source provenance: `slot_machine.c:1008` sets the marker when reels start; `quest_log_events.c:2020–2034` suppresses an unplayed departure and clears the marker on eligible departures. The marker is transient, resets naturally with a new session/Continue, and adds no save payload. Nested entrance retention and general duplicate-departure suppression remain outstanding.

# Quest Log

## Status and safety

Quest Log records accepted campaign events and runs automatically after normal Continue. Playback is a read-only projection: it owns no campaign command executor, RNG, inventory, party, flags or rewards. Full playback and skipping return to the actual restored session. Successful saves stop the live recording; new games and saves without a recording invent no scenes. The original script guard `QuestLogState == 0` remains intact.

This is a usable player-facing feature, **not complete source scene parity**. Remaining differences are listed below.

## Source and import

Pinned source: pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`.

- `quest_log.c` and `quest_log_events.c`: scene/action budgets, cursor/repeat rules, recording gates, playback controls, caption timing and departures.
- `quest_log_battle.c`, `field_specials.c`, `event_object_movement.c`: battle wording, entrance/exit semantics and movement timing.
- `src/data/text/quest_log.h`, map JSON, region-map sections and graphic constants: original English captions, map names and graphics.
- `include/global.h` and quest-log constants: four scenes, 128-u16 script, 16 object slots and 32 logical-command pointers.

```sh
python scripts/imports/presentation/import-quest-log.py
python scripts/imports/presentation/import-quest-log.py --check
```

The importer accepts `--raw`, `--world`, `--out`; `generate(raw, world_path)` is read-only. The normal runtime reads only `content/generated/data/quest-log.json`. Provenance and conversion recipes stay outside saves/runtime data.

## Bounded recording

`CampaignState.QuestLog` is optional, format 2, with one overlapping timeline interpreter. Missing/unsupported experimental versions fail with `save_quest_log_version_unsupported`; no legacy interpreter or automatic campaign rewriting is retained.

- Four recent scenes; actual active object slots, not nearest-N actors.
- A 256-byte logical source-equivalent script budget includes the initial facing/zero-input pair (16 bytes) and scene end (2 bytes). The initial pose is not duplicated. Source event sizes are imported. Movement/input costs eight bytes; projection graphic/spawn additions consume corresponding space.
- The 32-pointer limit counts logical commands, including initial pair and scene end. Repeated payloads share their group's pointer. A separate 128-payload bound follows from the 128-u16 script maximum; there is no 32-expanded-payload substitute.
- Same event ID at an uninterrupted action cursor shares a four-byte header. Original plus four repeats means **five** retained payloads. A sixth replaces the oldest only after the original additional-capacity check. Pokémon Center duplicates at the same cursor are suppressed. Grouping does not compare text or timestamps.
- Successful field-control takeovers record bounded nonvisual input markers. Actual Start opening shares existing menu/field eligibility, uses an accepted command, and sets `FLAG_OPENED_START_MENU`. Playback never executes recorded inputs.
- Departure events retain original `LocationId`. One transient last-location scalar suppresses duplicate stopped-scene starts; active recording is not blanket-deduplicated. Non-departure starts reset it. Continue/skip reconstruct it from the first event of the final scene, matching the source's skip-through-read behavior.
- Movement/input overflow closes recording while retaining the single transient wild-aggregate pointer. A subsequent wild result may update that existing bounded record even after an ordinary non-disabled map change, without adding a stopped scene’s appearance/movement; save/Continue, explicit cuts, disabled-map entry and failed-caption finish clear the pointer. No retained event history is added.
- Rejected/faulted commands publish nothing. Save/Continue, explicit recording cut and capacity boundaries prevent an indefinitely growing log. Failed caption allocation retries only under the source scene-start gates.

No campaign snapshots, command history, retained hashes or general event ledger are stored. String identities and JSON framing mean these logical budgets are not claims about exact JSON byte size or compatibility with a GBA save.

## Event and presentation behavior

Source-backed events include arrivals, nested departure latches, Gym badge variants, party/held-item changes, item use, bounded shop-exit aggregation, PC items and Pokémon movement, successful battle medicine/PP and Poké Doll use, Powder Jar and wake-only field Poké Flute, selected field moves, trainer/Champion text and wild win/catch aggregation. Static encounter species exclusions and disabled maps come from source data. Game Corner uses the original transient played-slots condition. The original entrance variable and pending-departure flag preserve Rocket Hideout/Game Corner and alternate-exit semantics.

Field medicine recording follows the accepted source callback and actual target/payment, including Berry Pouch items. Leppa Berry and Max Ether now share authenticated selected-PP restoration: imported callback/effect validation, source PP amount, matching item container, and original return view. Full-PP refusal consumes nothing and creates no caption. Sacred Ash names the first actually revived party member. Held-item captions require explicit give/take/swap menu context, preserving party/bag/PC and completed-mail origins; incidental battle consumption, theft, Storage Move Items and mail removal create none. PP boosts and Rare Candy also use their imported callbacks. Evolution stones name the original recipient through the typed evolution proposal; Repel records only at its paid sound-completion callback. The broad command-prefix/bag-decrement fallback has been removed: unrelated consumption creates no caption. These capabilities are declared consistently in the generated item catalog, without a map/species shortcut.

Ordinary double-trainer wins use a single battle-local last-opponent-faint receipt, overwritten at each opponent faint and discarded with the battle. Direct, self-KO, residual, Future Sight and known switch-entry contexts retain the original script attacker. Completion resolves that slot or the source living-left/right fallback, with combined active HP flavor. No campaign event history is added.

Ordinary accepted player movement shares `CampaignFieldMovementTiming` with live field rendering: walk/run 16/8 frames, rock-stair walk/run 23/11, Surf 8, bicycle 6 (stairs and destination cracked ice 8), existing Cycling Road 4/16/turn rules and accepted ledge jumps 32. Source nonuniform bicycle/stair pixel schedules are shared too. The command-only, default-omitted `RunHeld` input uses imported map running permission and shoes/tile eligibility; it changes no movement legality or RNG and adds no durable state. Existing bounded source-action labels retain the selected motion.

Captions run independently of player/NPC tracks with original 95/127/191/255-frame durations and Champion phases. Scripted movement retains bounded source labels and durations. A transient clock counts actually observed controllable-field loop steps, not wall time or stamp gaps. Shared input/modal eligibility excludes menus, Help, recap and field effects. Clock observation changes no campaign state, revision, RNG or save file; ordinary locks pause it.

Historical rendering uses recorded map/layout identity, actors and sparse tile edits, including supported connected-border coordinates. Ordinary map transitions now remain in one logical scene through an explicit read-only map projection marker; captions continue while the local map/actors/layout/edits switch and stale interpolation clears. Imported disabled locations still cut recording. No map or gameplay scripts execute during replay.

Every scene has one total visual allocation across all segments: 256 reserved action bytes plus each projection’s header 8, actual actors×20, optional layout 2 and sparse edits×5, bounded at **1,636 logical bytes**. Each transition also uses one eight-byte action marker and one logical command pointer. This is our accounting policy, not serialized JSON bytes or an original GBA field/limit. Layout/tile changes remain atomic; either action or total visual overflow closes before publishing the unrepresentable transition. The final saved-location projection is transient and uses the actual saved field appearance.

Walking, bicycle and Surf player graphics now retain source identity and gender in the existing bounded actor graphics field. Initial scenes and the final saved-location projection use their respective avatar modes. Mode changes use the existing conservative position-plus-graphics allocation (16 logical bytes). Cycle assets and Surf sheet placement are shared with live field rendering; replay uses its own transient frame clock for the two-frame Surf blob. Show Mon retains walking graphics, with riding graphics from mount through dismount completion. Source-action jump displacement and arcs now share the live sampler; original bob-phase restoration and complete mount/dismount effect ownership remain separate. See [shared jump presentation](shared-field-jump-presentation.md).

Original indexed description-window art and palette are imported. Enter/A advances; Backspace/B skips to the saved-location caption and then returns. Playback excludes normal gameplay input and play-time accumulation.

## Verification evidence

Toolchain comes from `./dev env`. Latest verified commands:

```sh
dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --verbosity minimal
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~CampaignQuestLog --verbosity minimal
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-build --no-restore --verbosity minimal
```

- Current full solution: zero warnings/errors; `/tmp/animation129-quest159-build.log`. Corrected test-project refresh: `/tmp/animation129-corrected-refresh.log`. Application focused: **469 passed**, including **Quest 159**, zero failed/skipped; `/tmp/animation129-quest159-application-corrected.log`. The latest 30 vectors cover stopped wild aggregation across ordinary map changes and shared ordinary movement timing/unchanged gameplay, including the source destination-cracked-ice bicycle exception. Earlier coverage includes item/battle receipts, avatar modes, eleven ordinary-map/total-budget/timeline vectors, and strict modal timestamp validation.
- Full Application: **1,202 passed**, zero failed/skipped, 2m53s, on the same corrected Quest 159 output; `/tmp/firered-quest159-full-application.log`. This covers command/catalog/live-movement integration, public campaign travel and save-validation regressions. The Cut-tree precedence fixture uses authored Route 2 rather than dictionary-first selection, preserving current save validation.
- Battle provenance: 5 passed after constructing residual fixtures through the real action lifecycle; `/tmp/firered-quest-opponent-faint-corrected-tests.log`. Test-project refresh: zero warnings/errors, `/tmp/firered-quest-opponent-faint-refresh.log`. Direct KO, self-KO, residual and Liquid Ooze contexts plus explicit unknown-attacker handling are covered.
- Full Battle regression: **1,709 passed**, zero failed/skipped, 13 seconds; `/tmp/firered-quest-full-battle-regression.log`.
- Shared selected-PP/current-item Content: 7 passed; CurrentItems ResourceImport: 1 passed; independent import checks: 8 passed; read-only regeneration passed. Logs: `/tmp/firered-quest-berry-content.log`, `/tmp/firered-quest-berry-import.log`, `/tmp/firered-quest-berry-import-check.log`, `/tmp/firered-quest-berry-regeneration.log`.
- Content: 1 passed; ResourceImport: 2 passed; independent importer vectors and read-only regeneration passed. Logs: `/tmp/firered-quest-nested-content.log`, `/tmp/firered-quest-nested-import.log`.

Tests cover bounded rings/budgets, source variants, actor changes, overlapping tracks, sparse appearance overflow, malformed recordings, save/Continue boundaries, accepted/rejected commands, idle clock, repeats and complete serialized-state/RNG/reward invariance during playback/skip.

**Bounded native evidence:** at 2026-10-16 16:44 UTC, actual Continue displayed the “Previously on your quest…” header/countdown and recorded actor. Backspace showed the original saved-location window; Enter returned to Pallet Town (6,8), North, with no observed errors. Log: `/workspace/shared/firered-quest-visual/native-quest-v2-1642.log`; assembly MVID `fd73d865-5d92-42d0-8021-1f190cfe99b9`. This verifies that sampled overlay/skip/return flow, not every historical variant or natural unskipped recap.

**Avatar native sample:** at 2026-10-16 19:49 UTC, Continue displayed the bicycle under “Previously…” count 2, then returned to the actual saved cycling Pallet field. Log: `/workspace/shared/firered-quest-avatar-visual/native-avatar-1948.log`; Godot MVID `f58648a0-46c0-4857-99cd-fbbcee56dc5b`. The short Surf scene elapsed between observations; native Surf and explicit skip were not verified. This separately labelled fixture supplies Bicycle reward setup and recorder-seeded Surf poses, without claiming a Koga battle/playthrough; bicycle activation/movement use public commands. Codec and full/skip serialized-state invariance passed (`/tmp/firered-quest-avatar-fixture.log`). Its README documents the controlled setup and native route.

**Current multi-map codec fixture:** `/workspace/shared/firered-quest-map-visual` was regenerated against the corrected 122/Quest 129 assembly from a fresh game using only public movement/ack/turn commands. One scene retains two actual house/Pallet transitions in eight actions and 440 logical visual bytes; generation 30, final Pallet (6,8) North, money 3000/RNG 3510449587 cursor 1. Current codec validation, full/skip serialized-state invariance and the post-transition marker boundary passed (`/tmp/firered-quest-map-fixture-corrected.log`). The bounded native sample below subsequently verified the recorded map sequence.

**Multi-map native sample, 2026-10-17 03:43–03:51 UTC:** ordinary Continue replayed Pallet → house 1F → Pallet with scene count 1 at supported 0.03× time scale, then naturally reached the saved-location caption. Enter returned to Pallet (6,8), North. A separate normal-speed Backspace skip and Right+Enter return preserved the saved position/direction; no gameplay input leakage, disk-save mutation or runtime error was observed. Both processes closed and before/after Debug-file manifests matched. Godot MVID `ba754bfc-63d6-4b32-ad1f-5074bc2c8203`; report and captures: `/workspace/shared/firered-quest-map-native-20261017-0342/REPORT.md`. Audio used Dummy. Normal-speed transition fidelity, entirely input-free final-caption return and exhaustive recap coverage remain unverified.

## Final caption and locked exit phase

The transient playback now preserves the source final-caption timer and exit task: natural caption dismissal occurs on callback 128; the following 52 callbacks blank the header, erase the top/bottom bars one pixel per update, hold the restored field, and finally release controls. A/B dismissal enters the same locked exit; B skip suppresses the saved-location caption, as `END_MODE_FINISH` does. Repeated input during that phase cannot advance gameplay. Save data and recording schemas are unchanged.

`tests/Reference/generate_quest_exit_vectors.py` extracts the pinned `Task_WaitAtEndOfQuestLog`, `Task_EndQuestLog`, and `RestoreScreenAfterPlayback` bodies and produces 180 numerical task observations. Platform rendering calls are stubbed only to observe caption/header/bar/control state. This gate covers window geometry and timing. Shared weighted grayscale and original-color restoration now use the same exit progress; see [palette presentation](quest-log-palette-presentation.md). Weather-specific palette-bank darkening and warp fades remain open.

## Remaining source parity, in priority order

1. Exact warp-entry/exit fades, connected-edge pixel continuity and special forced-transition presentation remain incomplete. Ordinary map changes retain bounded same-scene visual transitions; replay executes no historical gameplay scripts. Existing transition records contain destination appearance, not warp cause, departure phase or door callback. They also represent seamless connected edges, so adding a fade to every map change would be incorrect.
2. The 20 regular/special/in-place jump displacement and arc bindings are shared with live rendering. Remaining special/scripted poses, facing-reversal animation, landing/shadow effects and source bob-phase restoration are not fully reproduced. Ordinary walk/run/Surf/bicycle timing is shared and covered; avatar transition allocation remains conservative. Ordinary locks pause timing. Escalator-out’s empty action remains excluded unless ordinary single-player reachability is established.
3. Fainted-replacement Spikes KO lacks the source mutable attacker register in the existing replacement cursor. Its receipt preserves opponent species with an unknown attacker, and no double-win victor caption is invented.
4. Shared historical grayscale and exit color restoration are implemented; weather-specific palette darkening, audio and native full natural recap/map-variant coverage remain. Source-selected actor textures prevent actual-save regional palettes leaking into past scenes. Current automated invariance and bounded native samples do not establish all New Game/Continue/backup/full-buffer/quit combinations. Cosmetic history should not expand the bounded recording architecture.

Reachable used-item callbacks, TM/HM learn/replace and admitted story-item boundaries have source-backed adapters. Inaccessible colored-flute/link-only events and in-game NPC trades are not treated as source link-trade events without evidence.

Final exit-phase automated gate, 2026-10-17: all 165 Quest cases passed, including six new exit rows, within the 203-case combined Application gate (`/tmp/nonmove-quest-box-corrected.log`). Full solution compiled with zero warnings/errors; the 180-frame source oracle reproduced exactly and layout checks passed 28/28. Native observations above predate this exit-phase correction.

**Final-exit native sample, 2026-10-17 13:20–13:24 UTC:** on checkpoint4243e8b, normal-speed uninterrupted capture observed the saved caption, retracting bars and entirely input-free return to saved Pallet facing North. Separate B skip suppressed the saved caption; immediately supplied Right caused no gameplay command or direction leakage. Saves remained byte-identical and logs had no errors. MVID `03912d91-61d2-4449-882c-dffefbca55c4`; evidence `/workspace/shared/firered-quest-exit-native-20261017/REPORT.md`. One all-black screenshot near final release has normal adjacent frames and no established cause; retain it as an unresolved observation. Coarse screenshots do not prove exact128/52 timing or pixel parity. Dummy audio; processes closed before subsequent compilation.

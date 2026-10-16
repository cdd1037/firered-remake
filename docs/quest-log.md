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
- Rejected/faulted commands publish nothing. Save/Continue, explicit recording cut and capacity boundaries prevent an indefinitely growing log. Failed caption allocation retries only under the source scene-start gates.

No campaign snapshots, command history, retained hashes or general event ledger are stored. String identities and JSON framing mean these logical budgets are not claims about exact JSON byte size or compatibility with a GBA save.

## Event and presentation behavior

Source-backed events include arrivals, nested departure latches, Gym badge variants, party/held-item changes, item use, bounded shop-exit aggregation, PC items and Pokémon movement, selected field moves, trainer/Champion text and wild win/catch aggregation. Static encounter species exclusions and disabled maps come from source data. Game Corner uses the original transient played-slots condition. The original entrance variable and pending-departure flag preserve Rocket Hideout/Game Corner and alternate-exit semantics.

Captions run independently of player/NPC tracks with original 95/127/191/255-frame durations and Champion phases. Scripted movement retains bounded source labels and durations. A transient clock counts actually observed controllable-field loop steps, not wall time or stamp gaps. Shared input/modal eligibility excludes menus, Help, recap and field effects. Clock observation changes no campaign state, revision, RNG or save file; ordinary locks pause it.

Historical rendering uses recorded map/layout identity, actors and sparse tile edits, including supported connected-border coordinates. Layout/tile deltas are atomic and share the action budget; an oversized projection closes/skips the scene before publishing a partial room. Its allocation is our design informed by the original fixed footprint: header 8 + 16 objects×20 + layout 2 + 210 edits×5 + action bytes 256 = 1636 logical bytes. These are not claims about original recorded fields. The final saved-location scene is transient and uses the actual saved field appearance.

Original indexed description-window art and palette are imported. Enter/A advances; Backspace/B skips to the saved-location caption and then returns. Playback excludes normal gameplay input and play-time accumulation.

## Verification evidence

Toolchain comes from `./dev env`. Latest verified commands:

```sh
dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --verbosity minimal
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-build --filter FullyQualifiedName~CampaignQuestLog --logger 'console;verbosity=normal'
```

- Latest combined103 build: zero warnings/errors, 44.07 seconds; `/tmp/animation-103-full-build.log`.
- Quest Application: 59 passed, zero failed/skipped, including logical-pointer limits and departure reconstruction; `/tmp/animation-103-corrected-quest.log`. The matching test-project refresh passed zero warnings/errors; `/tmp/animation-103-refresh-app-build.log`.
- Content: 1 passed; ResourceImport: 2 passed; independent importer vectors and read-only regeneration passed. Logs: `/tmp/firered-quest-nested-content.log`, `/tmp/firered-quest-nested-import.log`.
- A final unified Application regression remains pending. The latest broad run was 890 passed/3 invalid animation-test-row failures; corrected affected filters subsequently passed. This is not a full-green claim.

Tests cover bounded rings/budgets, source variants, actor changes, overlapping tracks, sparse appearance overflow, malformed recordings, save/Continue boundaries, accepted/rejected commands, idle clock, repeats and complete serialized-state/RNG/reward invariance during playback/skip.

**Bounded native evidence:** at 2026-10-16 16:44 UTC, actual Continue displayed the “Previously on your quest…” header/countdown and recorded actor. Backspace showed the original saved-location window; Enter returned to Pallet Town (6,8), North, with no observed errors. Log: `/workspace/shared/firered-quest-visual/native-quest-v2-1642.log`; assembly MVID `fd73d865-5d92-42d0-8021-1f190cfe99b9`. This verifies that sampled overlay/skip/return flow, not every historical variant or natural unskipped recap.

**Controlled fixture:** `/workspace/shared/firered-quest-visual` contains an explicitly synthetic fresh-game/public-command profile, not a claimed playthrough. Latest regeneration used copied DLL references through `dotnet run --project /tmp/firered-quest-probe --no-restore -p:UseSharedCompilation=false`; log `/tmp/firered-quest-cursor-fixture.log`. Format 2/generation 153, four scenes/120 actions, final Pallet (6,8) North, money 3000, RNG seed 3510449587/cursor 1. Codec round-trip, full playback and skip preserve the complete restored state. The pointer/departure batch requires regenerating this fixture after its matching build.

## Remaining source parity

1. Complete used-item/TM/HM, held-item PC and story-item event coverage; exact double-trainer/last-victor and residual/self-inflicted KO wording. In-game NPC trades must not be treated as link-trade events without source evidence.
2. Exact action-cursor/timing provenance for instantaneous scripts, automatic movement and avatar transitions; source emote/effect rendering and nonuniform pixel schedules. Sparse appearance deltas conservatively separate repeat groups. Escalator-out's empty action is not implemented; ordinary locks do not create one. Do not expand excluded communication scope merely because source code exists.
3. Exact full-buffer stopped-state versus retained wild-aggregate-pointer interaction, map-transition scene partitioning and forced-transition playback. The projection starts map-specific scenes rather than executing original historical scripts.
4. Historical weather, palettes/avatar modes and audio. Recorded actors use source-selected textures to avoid leaking actual-save regional palette state; full historical palette/audio reconstruction remains incomplete.
5. Native full natural recap and comprehensive New Game/Continue/backup/full-buffer/quit and historical-map variants. Current automated invariance evidence and the bounded native sample do not establish all of these.

# Non-localized single-player completion audit

Audit date: 2026-10-16, static inspection principally 14:14–14:17 UTC. Code snapshot inspected: `720b31f65633325810ae759f9cccc780687406be`, with concurrent development in progress. This is an independent static audit, not a build, playthrough, audio-output test, or certification of current work in progress. Only this document was written by this audit.

## Current reconciliation — 2026-10-19

The current move-identity inventory covers all 354 identities through 352 dedicated scripts and two authenticated called-leaf forwarding paths. Named general result/weather/healing dispatch and the shared Substitute controller have their separate evidence; identity coverage is not a claim that every runtime branch has been visually watched. See the [battle matrix](battle-system-completion-matrix.md) and [audit repairs](full-project-audit-repairs.md).

The subsequent ordinary-service review found and repaired the reachable Pewter profile woman: canonical menu, shared Mail/profile Easy Chat editor, source message timing and validated saved profile words. See [profile service](easy-chat-profile-service.md). Trainer Tower retains one authenticated settlement receipt with explicit legacy conversion, rather than unbounded history. These are actual repairs, not additional playthrough evidence.

Concrete remaining presentation work includes the remaining special-door programs/art and forced-transition/preview/weather branches, connected-edge pixel continuity, weather-specific palette behavior, broader special pose/facing/landing/shadow details, and evolution initial terrain entry/restoration pixel boundaries. Authenticated plain warps and connected-edge no-fade selection share live/recorded presentation authority. The bounded animated-door implementation uses source callbacks and tileset-derived ordinary small-door art plus the normal S.S. Anne 1×2 style for ordinary same-section NONE/SUNNY walking routes, with independent departure/arrival phases and exact receipt-derived door anchors; its source/managed checks and outstanding native acceptance are reported separately. The exact same-command first-arrival caption now retains observed transition timing and becomes eligible at release; other script/caption tails remain bounded out. Existing caption timers also respect authenticated ordinary control locks, with description clearing at map reconstruction. Ordinary nonanimated doors now retain the accepted entry-step direction and share the live/recap step–fade–arrival controller. Manual elevator returns now publish shared resolved-warp receipts and use source 1×2 sliding-door materials; their source-disabled Quest recording is preserved. The separate scripted Tower lobby exit, ladder, stair, forced and weather-specific transitions remain separate. See [ordinary field transitions](plain-field-transition-presentation.md). Evolution's confirmed shared RGB555 palette/alpha difference and fixed-time intro cue ordering are corrected through shared arithmetic and actual printer/audio completion; initial terrain entry/restoration pixels remain unverified. Older Quest records retain their immediate unknown transition; only newly completed supported observations carry bounded actual switch/end timing and optional door geometry.

Acceptance remains separate: real non-Dummy speaker output, current-binary window-level samples for the new profile and other documented scenes, fresh exported-build persistence, and broader natural legal routes/branches. Chinese, external communication/e-Reader/ROM interchange/cloud save and the unspecified trade-evolution replacement remain deferred. The bounded review found no further concrete missing ordinary service; this is not proof of exhaustive story/acquisition closure.

## Earlier reconciliation — 2026-10-17, checkpoint 269b664

Sections 1–6 below preserve the original inspection and must not be read as a current missing-feature list. Current implementation and evidence are maintained in [current scope](first-clear-remaining-scope.md) and the linked feature documents. This reconciliation inspected those documents and their current owners; it is not a new exhaustive source audit or playthrough.

- **Implemented since the historical audit:** bounded Quest recording/playback/skip/return; Castform's general form transition; saved box-name delivery text; shared naming templates for opening, Pokémon and PC boxes; historical grayscale/source-color restoration; and shared live/recap jump displacement and arcs. Naming retains direct keyboard/IME input. See [naming](shared-naming-presentation.md), [Quest palette](quest-log-palette-presentation.md), [jump poses](shared-field-jump-presentation.md), and section 11 below.
- **Other historical omissions now have implementations:** noncombat party moves, source opening/title/Oak presentation, evolution/hatch, NPC trade scenes, Summary, Hall/credits and Itemfinder. Their dedicated documents retain timing/rendering limits. The old `fullTitleIntroImplemented=false` diagnostic alone does not establish that the current title implementation is absent.
- **Actual implementation work still open:** 162 dedicated move roots (alignment resumed by the user after this reconciliation); Quest warp/door/forced-transition phases and connected-edge pixel continuity; weather-specific palette behavior; broader special pose, facing-reversal and landing/shadow effects. Existing Quest records cannot distinguish warp causes from seamless map changes. Future warp work needs a genuine shared transition cause/phase authority before recording or rendering it. Evolution also documents remaining host RGBA versus source RGB555 blending and bounded introduction/restoration timing. These are presentation gaps, not demonstrated missing ordinary story or acquisition routes.
- **Acceptance still open, rather than absent mechanics:** native Castform transition, full naming IME/Help combinations, broader evolution/hatch/Hall/credits and UI branch samples, connected-border recap pixels, natural full-route coverage, and a fresh exported-build persistence check. Recent native PC/capture naming, recap gray/color restoration, rival Back, Mail and Tutor cancellation samples are bounded successes; they do not certify all branches or later binaries.
- **Separate gates/deferred scope:** real non-Dummy audio output remains unverified; Chinese, external network/e-Reader/ROM interchange/cloud-save features and the unspecified trade-evolution service remain separate. No unavailable species or replacement NPC is implied.

No additional small ordinary single-player functional omission was established within the recent Castform/Quest, native-UI and interaction-branch pass. Stop that pass here rather than manufacture a new feature or infer completion from test counts. Remaining presentation work requires its own bounded source-backed scope; broader verification is not proof of a missing implementation.

## Conclusion

**The inspected game cannot yet be called fully complete except for Chinese.** Real presentation and auxiliary-function omissions remain. Conversely, this audit did not establish a missing ordinary playable region or a missing main-story rule from map counts or `unsupported` strings. The broad content is composed; proving every legal interaction still needs a source-to-runtime closure audit and bounded executable cases.

Chinese, player communication/wireless games, external e-Reader, original-ROM save interchange, cloud saves, and the still-undecided trade-evolution replacement service remain outside this completion pass. Existing approved event-island access and rule improvements stay in scope as implemented decisions. Do not add unavailable species or other-generation systems to make a data count look complete.

## 1. Observed implementation gaps

| Priority | Finding | Direct implementation evidence | Source/closure criterion |
|---|---|---|---|
| P1 functional auxiliary | Quest Log recording/playback is absent. This is B-class single-player auxiliary content, **not a blocked story progression path**. | `CampaignSession.cs` rejects nonzero `Script.QuestLogState` with `save_quest_log_state_not_supported`; `CampaignHealingCenter.cs` accepts `SetUsedPkmnCenterQuestLogEvent` only at state zero; other scripts similarly no-op recording cuts. | Pinned `src/main_menu.c:481` invokes `TryStartQuestLogPlayback` on Continue; `src/quest_log.c` owns actual scenes. Implement bounded saved recording/replay/skip/return, or explicitly preserve this as an outstanding exclusion. Merely removing guards would not implement it. |
| P1 presentation | Only 20 dedicated move animations are dispatched. | Both overloads of `CampaignMoveAnimationPresentation.AtAttackText` in `CampaignMoveAnimationDispatch.cs` enumerate 20 IDs and return `script-outside-slice` for all other moves. | Pinned `data/battle_anim_scripts.s` plus sprite/task callbacks. Audit complete script/callback/asset closure and representative execution, rather than visually replaying all 354 moves. Ordinary result text/HP changes are not dedicated animations. |
| P1 presentation/interaction | Full title/Oak introduction and source name keyboard are missing. | `CampaignPlayerEntryView.cs` reports `fullTitleIntroImplemented=false`; drawing uses a generic frame, gender/name/confirmation prompts, and direct text entry. Capture/egg naming also uses shared LineEdit. | Pinned `src/main_menu.c`, `src/title_screen.c`, `src/oak_speech.c`, `src/naming_screen.c`. Preserve new-game initialization, cancel/back, gender, rival naming and existing-save protection while adding presentation. |
| P1 presentation | Evolution is still an explicit simplified adapter. | `CampaignEvolutionView.cs` states original background, morph animation, fanfare and timing are not reproduced; proposal renders a portrait on black and asks A to continue/B to stop. | Pinned `src/evolution_scene.c`; verify level/stone variants, cancellation rules, completion and learning order, sound, and stable input ownership. Rule completion and scene completion are separate. |
| P1 presentation | NPC trades use a before/after portrait sequence. | `CampaignNpcTradeView.cs` explicitly omits cable, ball travel, background animation and source timing; draws one portrait in four text phases. | Pinned `src/trade_scene.c`; nine NPC exchange transactions may remain correct while their shared scene is unfinished. Exercise one representative plus source-data parity for all nine. |
| P2 presentation | Egg hatch background remains missing. | `CampaignEggView.cs:169` explicitly identifies absent trade/shadow tilemap background; shell, cracks, shards and hatch sprite do have implementations. | Pinned `src/daycare.c` (egg-hatch callbacks). Complete the absent layers and compare selected timing frames; do not replace the implemented hatch rules or call the whole hatch feature absent. |
| P2 presentation | Summary uses generic frame/text layout. | `CampaignFieldMenuView.cs:338` draws three pages inside the standard frame; `CampaignOwnedSummaryView.cs` draws generic MOVE DETAILS rows. | Pinned `src/pokemon_summary_screen.c` and its graphics. Preserve actual metadata, page navigation and move reorder transactions while completing art/controls/transitions. |
| P2 presentation | Hall of Fame/credits remain simplified. | `CampaignPokemonLeagueView.cs:332` constructs Hall background from two tile selections; credits MON is a stationary 64-pixel sprite; map composition is direct layers plus dark overlay and simplified camera arithmetic. | Pinned `src/hall_of_fame.c`, `src/credits.c`. Existing completion/save/return logic must not be mistaken for full scene parity. Check recording, saved Hall revisit and repeat championship separately. |
| P2 presentation | Itemfinder uses font glyphs instead of source effect sprites. | `CampaignItemfinderView.cs.DrawItemfinder` displays `★` or directional Unicode arrows at a fixed position. | Pinned item-use/field-effect source. Preserve underfoot versus nearby response and pickup rules, replace only presentation, check cancellation/input lock and sound. |
| P2 presentation verification | Pokédex habitat/area decoration, transitions; capture/Safari scene details; Fame Checker scrolling/control timing; slot palettes still require targeted comparison. | Concrete UI owners exist (`CampaignPokedexHabitatView.cs`, `CampaignPokedexAreaView.cs`, `CampaignCaptureView.cs`, Safari/Fame/slot views); current scope document identifies residual differences. This audit inspected habitat navigation and rendering but did not independently enumerate every missing pixel/timing instruction. | Keep these as **partly implemented, comparison needed**, not fabricated lists of absent mechanics. Source and render checkpoints should establish exactly which components need work. |
| P0 acceptance | Actual audio output is not established. | README/engineering contract retain Dummy-only evidence and an open real-output gate. The parent subsequently checked the cloud desktop at 14:18:53 UTC: no `/dev/snd`, audio tools, or Pulse runtime; the read diagnostic `/workspace/shared/firered-audio-environment.txt` confirms a real-output environment blocker. This audit did not run an output device. | Actual non-Dummy device output, heard/recorded representative BGM/cry/SFX/fanfare transitions and mono/stereo behavior. PCM existence alone cannot close this gate. |

### Spinda is not a demonstrated ordinary single-player blocker

Pinned `src/pokemon.c:5339` genuinely applies PID-derived front spots. No implementation was found in the current C# source. However, direct searches found no `SPECIES_SPINDA` in default wild encounters, normal trainer parties, trainer-tower sets, NPC trades, evolution entries or map scripts. Current generated admission lists contain 196 owned, 220 opponent and 204 tower identities, and **none contains Spinda**. Canonical/machine/tutor/mail identity data is not a reachable encounter. Keep PID spots as broader presentation debt unless a real in-scope route/opponent is demonstrated; do not add Spinda or label ordinary single-player incomplete solely for this reason.

### Follow-up finding: noncombat party moves (2026-10-16 15:36 UTC)

A later source-to-menu check found five genuine functional omissions: Dig, Teleport,
Sweet Scent, Soft-Boiled and Milk Drink outside battle. `CampaignFieldMenuView.FieldPartyActions`
currently exposes seven HM actions, but not these five; existing battle-move handlers
are not field implementations. These are now assigned for completion, not closed.

The pinned source supplies shared mechanisms, which the remake should reuse rather than
create per-map exceptions:

- `src/fldeff_dig.c`: same map eligibility and escape task as Escape Rope, without consuming an item.
- `src/fldeff_teleport.c`: source Teleport/Fly map-type eligibility and the saved healing destination.
- `src/fldeff_sweetscent.c`: field presentation followed by `SweetScentWildEncounter`; encounter eligibility and modifiers must be traced separately from walking encounters.
- `src/fldeff_softboiled.c`: donor HP must exceed one fifth of its maximum; transfer costs that amount and heals a different living, injured party member, capped at its maximum. Trace the party-menu table for the shared Milk Drink action.

This follow-up does not revise the historical findings above into claims about current
completion. Public command tests, menu wiring and representative presentation remain required.

## 2. Static world and interaction inventory actually checked

Compared `content/raw/pokefirered/037335f4/data/maps/*/map.json`, `content/generated/maps/world.json` and `content/generated/data/story.json` directly, using local Python read-only inspection. Raw map files total 425; the runtime index contains 395 distinct non-variant source maps. The 30 absent raw maps consist of:

- 15 Pokémon Center 2F maps
- Trade Center, Union Room, Battle Colosseum 2P/4P, Record Corner
- Seven Island House Room2 (e-Reader)
- Saffron City Connection
- Four prototype Sevii islands and four unused houses (Routes 6/19/23 and Seven Island)

The documentation's 395/414 denominator excludes 11 unused/prototype/other raw entries; it is not a claim that raw contains only 414 files. Neither 30 nor 19 is a count of missing ordinary playable areas.

For every mounted map, collected every non-null/non-`0x0` script reference in the original object, coordinate and background events. Results:

- 1,512 object references
- 216 coordinate references
- 519 background references
- All 2,247 references have an entry in generated story Programs

Following explicit Goto/Call/conditional/flag/QuestLogJump edges from those roots visited 3,430 programs. Unsupported instructions in that conservative graph belonged to cable-club/Mystery Gift operations. This graph includes branches that are not legal ordinary single-player paths and **does not include a complete map-header/native-effect model**. It proves symbol presence, not execution, physical reachability, callback handling or correct conditional dialogue.

The global IR retains 165 unsupported diagnostics, including 135 `.2byte`/`.4byte` data rows. Counting them as 165 broken gameplay commands would be false. Generic trainer, hole and link helper scripts also require semantic handling: for example, `CampaignFourIslandField.cs:189` handles the actual Icefall `warphole` effect. Never remove or ignore unsupported diagnostics globally to obtain a zero count.

`PretCurrentGame.cs` composes the world, shared canonical/items/machines/trainer systems, ordinary regions, first and later Sevii, trainer tower, NPC trades, breeding, gifts, tutors and optional areas. `CampaignSession.cs` dispatches effect handlers before its generic fallback, which throws on an unhandled effect. This architecture means a compiled root can still fail in a specific legal state; a complete callback audit must use actual dispatch, not a search for method names.

## 3. Findings that must not be misreported as new implementation gaps

- At initial inspection, `battle-system-completion-matrix.md` still said Battle Scene editing was unavailable (parent reports correcting it during this audit). Current `CampaignOptionsView.cs` and `CampaignOptionEditing.cs` expose/edit it. Correct the stale statement; do not reimplement Options.
- `department-store.json` contains historical `deferredServices`/`complete:false` entries. `PretCampaignRegionalInteriors.DeferredRegionalServiceReason` explicitly bypasses these for integrated TM sales, Counter tutor, ordinary stocks and rooftop services. Historical resource prose is not current runtime availability.
- “Unsupported move” and “PC unavailable” UI error strings are not proof those failures occur with current admitted data. Trace the command and catalog first.
- Existing static species coverage does not prove every acquisition, evolution, breeding or tutor route. Conversely, no missing reachable species was established by this audit.
- The old representative save lacks lifetime egg count. README explicitly says it is not a current continuation baseline without known-value conversion; a fresh game is valid. Do not fabricate historical counts to make a save open.
- Natural full-route evidence is incomplete and the representative championship derives from an explicitly prepared party branch. This is an acceptance-evidence limitation, not proof that the championship rules are absent.

## 4. Source-to-runtime verification plan

The output should be a machine-readable ledger plus concise human report. One row per source obligation, not one row per map or per test file. Suggested columns: source map/root/event identity, ordinary/external/unused class, legal precondition partition, generated root, Application handler/command, expected durable delta, presentation owner, executable case, evidence status, reason for exclusion. Keep provenance in tools/audit artifacts, not as new runtime hash gates.

1. **Build the true denominator.** Start with pinned map object/coord/bg events, map headers and conditional tables; then add source-native entrypoints (menus, items, evolution, hatch, tutorial, battle modes, tower and Quest Log). Account for every omitted raw map explicitly. Expand calls, fallthrough, native effects and source-data tables, preserving FireRed edition branches.
2. **Trace mounted runtime wiring.** Check world-index identity, entrances/exits, actor visibility/movement, physical interaction geometry and script trigger projection. Follow story instructions into the actual `CampaignSession` dispatcher. A handler returning success without the corresponding source behavior must remain an explained divergence or gap.
3. **Partition legal state instead of replaying every NPC blindly.** Use before/after quest flags, badge/National Dex gates, item absent/present, bag or party/PC full, repeat reward, decline/cancel, win/loss/flee/capture, map exit/re-entry and save/reload. Execute public campaign commands from disclosed valid fixtures; assert no faults, correct text/effects, no reward duplication and recoverable modal state.
4. **Give every special shared mechanism at least one executable case.** Include hidden/renewable items, trainer sight/rematch, NPC trade, fossil/gift, TM/HM/tutors, egg and multi-level learning, evolution restrictions, held mail/storage/multi-move, fishing/surf/Safari, push/current/spinner/ice puzzles, roamer, tower four modes, diploma/card/Hall. Data parity checks cover all rows; behavioral cases cover distinct semantics.
5. **Check global journey seams with short segments.** Three starter/rival branches; Bill invitation accept/refuse/revisit; alternate Kanto gates; first League defeat/clear/save/return; National Dex and Ruby/Sapphire gates; rematch/Cerulean Cave/roamer; repeatable late-game play. Existing prepared checkpoints must be labelled. This does not require a full replay for this audit, and cannot be called a natural full replay.
6. **Audit all animation scripts structurally.** Require every in-scope script/callback/template/palette/task to resolve to an implementation. Group callbacks by primitive semantics and use reference vectors/timing samples plus representative native scenes (single/double, substitute, missed/failed/immune, multi-hit/spread, called moves). Never infer all visuals complete from one generic fallback, but do not spend the acceptance budget manually watching every move.
7. **Run native UI scenario coverage.** New/Continue/save overwrite, keyboard/controller/mouse ownership, name editing, settings, PC/party/bag/dex/help, capture/evolution/hatch/trade, Hall/credits and one map traversal per new movement mechanic. Record screenshots at source-derived checkpoints and short motion/audio captures. Check focus, back/cancel, transitions and no input leakage.
8. **Re-run maintained tests and export only after integration.** Serial build on this Linux environment per README; current MSTest projects and explicit audio suite; generated-only loading; fresh-profile exported executable startup, save/quit/Continue. Capture actual engine and runtime versions and test totals. A skipped/empty suite or Dummy audio is not a pass.

## 5. Exit conditions

The non-localized completion statement is justified only when:

- Every ordinary source obligation is implemented and reachable, or has an explicitly approved remaining exclusion; no unexplained callback failure or silent placeholder remains.
- All concrete omissions in section 1 are closed or explicitly excluded by the user. A prior decision to do visuals later is scheduling, not evidence they are already done.
- Source/data coverage, rules/transactions, native presentation, audio output and exported persistence each have their own evidence; no percentage is inferred from map/test/line counts.
- Chinese and deferred external functionality remain disclosed, without widening the game to make 386 species or multiplayer stars obtainable in one ordinary save.

This audit itself ran **no builds, tests, native windows, uploads or asset changes**. It supports prioritization and the verification method; it does not certify those exit conditions.

## 6. Bounded mounted-script callback follow-up — 2026-10-16

Static inspection: **18:59–19:03 UTC**, working tree based on `88cfbae74bda9c5cea74d20b3147155e1e86be06`, with concurrent animation and Quest Log changes. This follow-up establishes **no confirmed new single-player functional gap** within the inspected paths. It does not update the historical findings above into a current all-game pass.

### Closure actually inspected

- Matched the 395 mounted, distinct source maps to pinned original map events. All **2,247** script references resolve in generated story Programs: 1,512 object, 216 coordinate and 519 background references.
- Added actual `<map name>_MapScripts` headers and their conditional tables. Header rows comprise 113 transition, 46 resume, 42 frame-table, 18 warp-table, 40 load and 2 return-to-field entries. Runtime binding retains all six categories in `PretCampaignCatalog.cs:164–169`; entry queues and field-pump conditions are implemented in `CampaignSession.cs:711–731, 754–764`.
- A conservative traversal following operands that name generated programs reaches **3,991 programs and 180 special names**. These totals include external branches and referenced data tables; they are not counts of proven legal paths or successfully executed callbacks. Unsupported IR encountered in that graph belongs to shop-stock `.2byte` data or cable-club/Mystery Gift operations, rather than a demonstrated ordinary executable path.
- Inspected source/dispatch context for Four Island daycare deposit, withdrawal and egg callbacks; the Water Labyrinth Togepi gift; Daisy grooming; Magikarp/Heracross record services; Selphy requests; and representative Sevii map-edit, movement and warp callbacks. No concrete unsupported or silently skipped functional route was established.

### False positives resolved against runtime

- `UpdateLoreleiDollCollection` and `StickerManGetBragFlags` need not appear as literals in Application dispatch. Their source callbacks are bound through `StatFlagUnlocks` and `FieldMilestones`: `CampaignStatFlagUnlocks.cs:7–16`, `CampaignFieldMilestones.cs:7–18`, `PretCurrentGame.cs:505` and `PretFourIslandRegion.cs`.
- Daisy's source actor starts with a wandering movement type, but the transition sets `MOVEMENT_TYPE_FACE_RIGHT` before the initial dialogue branch that uses `face_original_direction` (`data/maps/PalletTown_RivalsHouse/scripts.inc:7–34`). The generic movement restriction alone is not a reachable failure.
- Cape Brink's jump-in-place commands have a dedicated tutor handler (`CampaignUltimateTutor.cs:86–109`) before generic movement dispatch. The generic fallback alone is not evidence of a missing tutor path.
- Missing-name candidates in wireless/link, e-Reader and Mystery Gift branches remain outside the requested single-player audit. Already assigned Quest Log gaps and deferred trade-evolution service placement were not counted as new findings.

### Limits

No failing reproduction route is justified by this inspection. Symbol closure and selected handler inspection do **not** prove physical reachability, every legal precondition partition, correct durable deltas, callback continuation, repeat/cancel/full-capacity behavior, save/reload behavior or presentation parity. Those require the bounded executable cases described in section 4. This follow-up ran **no builds, tests, game commands, native windows, downloads or uploads** and changed only this document after the inspection. It leaves whole-game acceptance and actual audio-output gates unchanged.


## 7. Bounded Quest Log native follow-up (2026-10-17)

The 03:43–03:51 UTC native sample used the existing fresh-game public-command fixture through ordinary Continue. At supported 0.03× time scale, Pallet → house 1F → Pallet remained one scene and naturally reached the saved-location caption; Enter returned to the saved Pallet (6,8), North. A separate normal-speed Backspace skip and Right+Enter dismissal showed no direction leakage, save-file mutation or runtime error. Both game processes were closed and the tested Debug files were unchanged. Godot MVID: `ba754bfc-63d6-4b32-ad1f-5074bc2c8203`; evidence: `/workspace/shared/firered-quest-map-native-20261017-0342/REPORT.md` and its captures/runtime logs.

This closes only the sampled multi-map/skip/return observation. Audio used Dummy; fully input-free final-caption return, normal-speed transition fidelity, native pixel parity and whole-game acceptance remain unverified. No third native run occurred.

## 8. Quest exit and named-box dialogue (2026-10-17)

The transient Quest playback now follows the final-caption/exit task timings: 128 natural wait callbacks, then 52 locked exit callbacks, with source one-pixel bar removal and B-skip caption suppression. A 180-row extracted-C oracle checks caption/header/bar/control state; 30/60/120 Hz tests preserve saved-state serialization. Palette/weather restoration and warp fades remain open. Ordinary/Safari delivery and shared gift text now read saved box names through the existing storage customization authority, including rollover names. Controlled delivery fixtures are not full acquisition playthroughs.

Full solution build: zero warnings/errors, 71.35s (`/tmp/nonmove-quest-box-build.log`). Application refresh: zero warnings/errors, 5.78s. Combined Quest/capture/Safari/gift/box tests: **203/203**, including **165 Quest** and **10 box dialogue** cases (`/tmp/nonmove-quest-box-corrected.log`). The initial 171/175 run found four noncanonical stored-Pokémon fixture projections; using the existing Receive operation corrected only those fixtures. Layout **28/28**; Quest oracle reproduced byte-for-byte. No move root admission change (166/354).

## 9. Bounded native inventory branches (2026-10-17)

Before the above build, native UI checks passed female TM Case color/disc/popup, empty TM Case cleanup/reopen, PC multi-selection with holes and held-exit refusal, party reorder, and storage reentry. These used explicitly controlled fixtures. MVID `d3337854-f2f4-4c40-be98-673ed1e182fc`; before/after DLL SHA256 unchanged. Both runs closed before compiler release at 13:15:06 UTC. Evidence: `/workspace/shared/firered-ui-branches-20261017/ACCEPTANCE.md`. Dummy audio; rival-name Back and Mail/Tutor selectors, exact pixel/frame parity and whole-game acceptance remain unverified. These runs predate the Quest exit change.

## 10. Quest automatic return native sample (2026-10-17)

Checkpoint4243e8b received a second bounded fixed-DLL window. Normal-speed uninterrupted capture observed natural saved-caption expiry, bar retraction and input-free return; B skip suppressed the saved caption and swallowed an immediately supplied Right. Saves remained unchanged; no runtime error. MVID `03912d91-61d2-4449-882c-dffefbca55c4`; `/workspace/shared/firered-quest-exit-native-20261017/REPORT.md`. One all-black capture near release remains unexplained, with normal adjacent samples; no pixel-perfect claim. Exact callback timing remains automated-source evidence. This window did not cover renamed-box UI, rival naming, Mail or Tutor.

## 11. Castform general form transition

The existing event-time body projection now drives the source named general transition. Forecast/overlay receipts authorize immutable before/after materials; the shared renderer implements BG mosaic and the peak frame/palette/Y swap. The result retains its final display through the message ACK, preserving singles/doubles cursor phases. Scene Off, substitute bypass, hidden actors/partners and transformed palette ownership follow their distinct source paths. No dedicated move count changed. The battle completion matrix records source vectors and the final managed gate; native Castform transition and audible acceptance remain unverified.

## Current bounded transition reconciliation (2026-10-19)

Ordinary stable plain/animated/nonanimated warps, normal 1×2 S.S. Anne doors, four manual elevator exits and the Trainer Tower scripted lobby arrival now share authenticated transition presentation. The latter also corrects shared OnTransition→OnLoad ordering and old script-context reset. No extra gameplay ACK or guessed Quest timing is added. The consolidated [remaining transition boundaries](plain-field-transition-presentation.md#finite-remaining-transition-boundaries) distinguishes actual ordinary-controller limits (stairs/ladder/gait/preview/weather) from separately implemented forced mechanisms, untested story routes, unsupported historical recap evidence and native pixel/audio acceptance. These categories are finite follow-up scope, not an all-game fidelity claim or a request for a new generated coverage ledger.


Fresh-game Oak escort acceptance now covers both source coordinate triggers through starter-choice readiness and strict save/Continue. This closed shared facing/hidden-origin/music-boundary defects; it does not establish Fuji/Selphy/ship/Champion public-route acceptance or native escort/audio parity. Source evidence and the bounded tests are maintained in the [transition document](plain-field-transition-presentation.md#fresh-game-oak-escort-acceptance).

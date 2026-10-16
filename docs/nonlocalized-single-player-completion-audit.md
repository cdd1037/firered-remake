# Non-localized single-player completion audit

Audit date: 2026-10-16, static inspection principally 14:14–14:17 UTC. Code snapshot inspected: `720b31f65633325810ae759f9cccc780687406be`, with concurrent development in progress. This is an independent static audit, not a build, playthrough, audio-output test, or certification of current work in progress. Only this document was written by this audit.

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

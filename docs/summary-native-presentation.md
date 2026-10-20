# Native Summary presentation

The shared owned Summary uses original Info, Skills, Moves, Move Details and Egg tilemaps from pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`. Field, PC, battle and Route 5/Four Island Day-Care share the renderer, not duplicate interaction rules. Application commands retain owned identity, revision, service and battle authority; inspection and animation never advance gameplay or RNG.

## Resources and geometry

The offline importer reads retained indexed PNGs, palettes, tilemaps, page-progress declarations and move/ability descriptions. Runtime reads only generated page textures, cursor frames, HP/EXP strips, shiny star, Summary-palette markings and description/motion dictionaries under `content/generated/ui/summary-screen`. It does not read original C, source recipes or version metadata. Type badges reuse the Pokédex atlas, capture-ball overlays the capture atlas, and portraits/eggs existing presentation assets. Marking tiles use their distinct source Summary palette. No font or audio replacement is introduced.

Offline helpers resolve shiny background banks, tile H/V flips, RGB555 conversion, transparency and cursor indices. Info/Skills/Moves/Egg retain the full left portrait; move details use the small owned icon and source power, accuracy, effect and right-hand rows. Live PP, stats, OT, item, ability, EXP and next-level requirement come from the owned instance/catalogs. National numbers use the species mapping, not internal IDs. Facing flags cover real species only: parsing requires the actual multiline species record, includes Bulbasaur and excludes `SPECIES_NONE`.

Full portraits and icons retain source centers `(60,65)` and `(24,32)`. A negative-width Godot texture rectangle flips UVs without moving its origin; normal and mirrored draws therefore use the same left edge. Shared type-badge tile-memory addressing and independent source-pixel checks are documented in [Pokédex](pokedex-habitats-and-area.md).

Header hit regions are previous `x104–128`, next/details `x128–208`, back `x208–240`, all `y0–16`; these do not overlap memo/ability text. Details rows use `x120–240`, `y18 + 28 × row`. The header switch/place button is clickable. Keyboard, mouse, wheel, hover and shoulder routing share the same selection and transition guards. Two-stage move-switch cancellation retains its identity guard.

Trainer memo prose preserves source low-16-bit OT ID/name matching, GBA origin, trade, fateful and hatched branches, including English `ApparentlyMet` for ordinary traded hatchlings. Kanto/Sevii section names are source-derived; no translated text is introduced here.

## Visible visits and presentation time

A visible visit is identified by actual screen ownership, not elapsed time between draws. Render stalls must not restart the entry fade or portrait. Help suspends the current visit; drawing another screen ends it. An owner change begins a new visit. A Pokémon change restarts only its portrait and discards any stale outgoing page. Each outgoing page retains its own selection/content snapshot.

Process and input observation advance page state independently of drawing. Timestamps retain fractional milliseconds until one source-frame quantizer; truncating to milliseconds first repeats the 120-pixel step and can skip 180. Exact 1/60-second ticks produce the source `60/120/180/240`-pixel slide steps, including from a fractional starting timestamp. The quantizer's floating-point tolerance also governs entry/exit and portrait boundaries; a real 16 ms interval remains below one 60 Hz frame.

Entry/exit use the source 16-step palette duration. Entry inhibits navigation, matching `Task_InputHandler_Info`'s fade/cry states. Exit closes only after its fade and ignores input while pending. Keyboard, mouse, wheel and shoulders all share entry, in-flight slide and exit guards. Process delta stops under Help; load/new-game/context replacement clears pending callbacks rather than later closing a different modal. Two translated pages restore identity drawing transforms after composition.

The short slide ignores additional keys. Original same-direction input can retain one pending page request; that queue is not implemented. Whole-page composition also approximates the source BG/OBJ priorities. Neither behavior is claimed frame-perfect. Portrait bounces and egg shakes retain source delta arrays, delays, HP/egg-cycle branches, the two-cycle limit and status/faint suppression.

## Inspection and move-selection modes

- Field/PC normal Summary permits two-stage move reordering through the existing owned-move command.
- Wild, trainer and double-battle PARTY use source `SHIFT`/`SEND OUT`, `SUMMARY`, `CANCEL`; Eggs and one-member parties retain `SUMMARY`, `CANCEL`. Active or fainted members remain inspectable. Battle Summary supports party/page/detail navigation and the source exit fade, but is read-only. Switching remains a separate authoritative command (`party_menu.c:5906–5914`, `pokemon_summary_screen.c:3589–3609`).
- Learning/replacement, deletion, new-move and Reminder candidate selection use explicit shared modes. Wild/trainer/double rewards, TM/HM, tutors, Reminder, Rare Candy and evolution show selected type, current/max PP, power, accuracy and source effect. A new move occupies row five with base PP; choosing it follows cancellation/stop-learning, never replacement slot 4. Reminder candidates scroll through the same five-row layout. Deletion has no new-move row.
- Revision, modal context and owned identity stamps reject stale drafts. Application commands still own HM refusal, payments, consumption, learning, rewards and switches. Fixed selection modes and details do not gain shoulder paging; see [input routing](button-mode-help.md).

An accepted `play_time_tick` may rebase an otherwise-current battle-party, learning/replacement or SELECT draft because only `PlayTime` changed. It never revives an already-stale guard. Real command, identity, occupant and battle-revision changes still require a fresh selection.

## Day-Care normal Summary

Route 5 and Four Island deposit-party callbacks open `PSS_MODE_NORMAL`, including Moves→A details, normal reorder/cancel, pointer rows and bounded page/party browsing. Info includes Eggs; Skills/Moves skip them. Source `party_menu.c:3019–3021,3095–3106` selects these actions; `pokemon_summary_screen.c:1004–1010,3589–3618` clears `lockMovesFlag` and permits swapping outside battle/link modes. A service-specific read-only rule would not match that source.

Only the authenticated deposit-party callback admits the shared owned-move transaction. Its actor/source/`waitstate` guards preserve script frame, wait correlation, service object, party identities and RNG. The global stable-field gate is unchanged; dialogue, withdrawal/resident selection, other services and battle do not inherit permission. Full move slots, revision/identity and ownership validation remain intact. Only deposit/cancel resumes the script; normal deposit PP restoration still applies.

`DayCareSummaryTests` covers public entry, reorder/deposit/Continue, cancel, stale/invalid targets, Eggs, clock-only revision, resident refusal and callback forgery. Fixtures use actual walkable source floor and `EnterMap`→`Pump` initialization with the existing owned party, not fabricated badges, wins or creatures. Application coverage does not certify every Day-Care native input route.

## SELECT in the live FIGHT picker

This is the separate `battle_move_swap` transaction, not battle Summary. Physical SELECT (Space/controller Back) starts the source two-cursor draft: destination 0 unless source is 0, then destination 1. A/SELECT confirms; B cancels and restores the original cursor without a command. Mouse grid selection chooses/confirms the destination; right-click cancels. The same controller serves ordinary wild/trainer, both doubles positions and the first-rival tutorial. Source `Switch / which?` and tiles 30/31 (source), 28/29 (destination) replace the normal PP/type panel while selecting.

Requests carry battle ID/revision, occupant token, actor slot, owned instance and command-session revision. Application accepts only settled move-selection phases and preserves presentation queues, the partner's selected doubles action, RNG, turn, event sequence and unrelated state. Domain swaps full slots including PP/PP Ups and independently maintains persistent moves. Transform uses a bounded live permutation without changing donor snapshot/original party; Mimic follows the temporary/persistent mapping symmetrically, as explained in [original-behavior improvements](original-behavior-improvements.md).

First-rival validation permits only its two originally allowed move IDs in either order, with unchanged PP Ups/max-PP restrictions. AI, next-turn resolution and rewards find moves by identity rather than assuming slot 0 attacks; the untouched pre-battle ownership snapshot remains reward origin. Source: `battle_controller_player.c:565–580,631–719,1422–1430`, `battle_controller_oak_old_man.c:338–343`, `battle_main.c:3124,3145–3161`.

## Verification and limits

```sh
python3 scripts/imports/presentation/import-summary-screen.py
python3 scripts/imports/presentation/import-summary-screen.py --check
python3 scripts/checks/test-summary-screen.py
```

Source checks cover exact regeneration, canvases/shiny banks, detail layering, cursor frames, descriptions, HP/EXP strips and missing-input failure. Content checks cover memo/motion boundaries, species facing and generated-only loading. `SummaryPageTransitionTests` covers visits/re-entry, stalls, owners, source offsets, pre-draw observation, identity/context replacement, details, Help and reset. `SummaryInteractionPresentationTests`, `BattleMoveOrderApplicationTests`, `BattleMoveOrderPresentationTests`, `SourceBattleMoveOrderTests` and `FirstBattleMoveOrderTests` cover mode/slot authority and unchanged state on inspection/cancel.

Current bounded native inspection, battle reorder/learning-page and final exact-slide/Help/identity tests are centralized in [validation](validation.md). They do not establish all species/pages, every consumer/physical controller/language, a replacement commit on every route, natural full-game completion, BG/OBJ hardware equivalence or audible output. Source same-direction page queuing remains the deliberate limitation above.

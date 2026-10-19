# Native Summary presentation

The shared owned Summary renderer now uses the original Info, Skills, Moves,
Move Details and Egg tilemaps from pret/pokefirered
`037335f4c725d7c9aecdac87066f2002b4bd7e14`. Field and PC entry points keep their
existing identity/revision guards and Application-owned move swapping. The
Route 5 and Four Island Day-Care party callbacks use the same interactive
normal Summary, including outside-battle move reordering.

## Resource boundary

`python3 scripts/imports/presentation/import-summary-screen.py` reads retained
original indexed PNGs, palettes, tilemaps, Summary page-progress declarations,
and move/ability descriptions. It emits only generated page textures, cursor
frames, HP/EXP strips, shiny star, Summary-palette markings and description/motion dictionaries under
`content/generated/ui/summary-screen`. The running view never reads original C,
raw assets, source recipes or source version metadata. Type badges reuse the
existing Pokedex menu atlas; capture-ball overlays reuse the shared capture atlas;
Pokemon portraits and egg images reuse existing presentation assets. Markings use
the shared original marking tiles with the distinct source Summary palette. No font or audio replacement is introduced.

The original Summary background palette includes the shiny bank replacements.
Tile H/V flips, GBA RGB555 conversion, background transparency and cursor palette
indices are handled by the same offline tile/image helpers as other native UI.
Info/Skills/Moves/Egg keep the full-size left portrait; move details switch to the
small owned icon and original power, accuracy, effect and right-hand rows. Live
PP, stats, OT, held item, ability, EXP and next-level requirement come from the
owned instance and shared catalogs. National numbers use the existing species
mapping rather than treating internal source species IDs as dex numbers.

## Controls and transitions

Direct keyboard behavior is unchanged. Header click targets replace the old
footer targets so they no longer overlap trainer memo/ability text: previous at
x104–128, next/details at x128–208, and back at x208–240, all y0–16.
Details rows use x120–240, y18 + 28 × row. Scroll/hover selection and two-stage
move-switch cancellation retain their identity guards. The header switch/place
button is also clickable. The same renderer performs presentation-only page
slides at the original 60-pixel-per-frame step, independent of game rules/RNG.
Entry/exit fades use the source 16-step palette fade duration. Exit closes only
after its fade and ignores further input while pending. Its clock is driven by
process delta so Help pauses it; load/new-game/context replacement clears pending
callbacks rather than closing a later modal. Portrait bounces and egg
shakes use source delta arrays, delays, HP/egg-cycle branches, two-cycle limit
and status/faint suppression.

Trainer memo strings and all Kanto/Sevii section names are generated from source.
The shared prose helper preserves the original low-16-bit OT ID/name decision,
GBA origin gate, trade, fateful and hatched branches, including the English
source’s ApparentlyMet wording for ordinary traded hatchlings.

## Verification

- `python3 scripts/imports/presentation/import-summary-screen.py --check`
- `python3 scripts/checks/test-summary-screen.py` (nine nonempty tests)

Checks cover exact regeneration, all page canvases, shiny palette variation,
move-detail background layering, two native cursor frames, source description
bindings, HP/EXP sprite strips and missing-input failure. Eight additional content tests cover memo branches and
source motion boundaries, species facing and isolated generated-only loading. Compiler and native
window acceptance are separate. These checks do not establish frame-perfect
emulator parity. Species-facing flags are also imported and applied to full portraits and icons.
Exact frame-by-frame layer-priority equivalence still requires native/emulator
comparison; existing egg-origin/hatch text semantics are
preserved. No translated text is introduced.

## Native review corrections

A real-window check caught a mirrored portrait displaced into the right pane.
Godot's negative-width texture draw mirrors its UVs without requiring an added
width in the destination X. Both portrait and icon now retain their fixed native
left edge; a shared placement helper and regression cover the source centres
(60,65) and (24,32). The same review exposed incorrect shared type-badge crops;
those belong to the Pokédex atlas importer and are corrected/tested there.

A content test also exposed a source-parser record boundary error: inline
SPECIES_NONE consumed the first multiline species record. The importer now
requires the actual multiline species body and verifies Bulbasaur is present,
NONE is absent, and all 386 real species have facing flags. These corrections
require a fresh runtime build/window before visual acceptance.

## Bounded native-window check

2026-10-16: actual Godot window, assembly MVID `76757e8d-242d-42bd-bebf-6502d88d21f7`, synthetic held-party checkpoint. Verified Mewtwo info, skills and moves pages: portrait stays in the left pane; PSYCHIC and WATER badges are correct; fields remain readable. An earlier run exposed the mirrored destination offset and tile-index crop defects; both were corrected and the window was restarted before this check. This does not establish all species/pages or original frame-by-frame parity. The source-backed generated-art checks cover every badge rather than only this sample.

## Battle inspection and move-selection modes

Ordinary wild, trainer and double-battle party selectors now open the source
SHIFT / SEND OUT, SUMMARY, CANCEL action window. Eggs and one-member parties
retain SUMMARY / CANCEL. Selecting an active or fainted member remains possible
for inspection; switching still submits the existing authoritative command.
Battle Summary uses the same Info/Skills/Moves/details renderer as field/PC,
including party browsing, keyboard, mouse and wheel navigation, and source exit
fade. It is explicitly read-only: no move-order command is available there.
This follows `party_menu.c` 5906–5914 and `pokemon_summary_screen.c` 3589–3609.

The shared move-details renderer now has explicit read-only, reorder, replacement,
deletion and new-move-selection modes. Wild/trainer/double rewards, TM/HM, tutors,
Reminder, Rare Candy and evolution replacement choices display selected move type,
current/max PP, power, accuracy and generated source effect description. The new
move occupies the fifth row with its base PP and complete details. Confirming
that row follows the existing cancellation/stop-learning path; it is never
submitted as replacement slot 4. Reminder candidates scroll through the same
five-row detail presentation, and the Deleter uses deletion selection without a
new-move row. Existing field/PC swapping retains its two-stage draft behavior.

All cursor/page/draft work remains presentation-only. Revision, modal context and
owned identity stamps prevent a stale selection from committing after the target
changes. The existing application commands still own HM refusal, payments,
consumption, learning, rewards and battle switches. No source files are read at
runtime and no rules/RNG are advanced by inspection.

2026-10-16 verification: full serial solution build passed with zero warnings and
errors. Seven `SummaryInteractionPresentationTests` and four focused Application
learning/switch tests passed with no skipped cases. These cover mode restrictions,
fifth-row behavior, candidate scrolling identity, native move-row hit regions,
stale selection and unchanged authority/RNG/EXP/known moves on inspection or
learning cancellation. Actual-window acceptance for these new entry points remains
separate. SELECT reordering in the battle FIGHT menu is a distinct transaction;
read-only battle Summary does not implement or substitute for it.

### SELECT in the live FIGHT picker

The shared `battle_move_swap` menu transaction is distinct from Summary inspection. Physical SELECT (Space or controller Back) starts a two-cursor draft, with the source's destination0/source0-destination1 default. A/SELECT confirms; B restores the original cursor without a command. Existing mouse grid selection chooses/confirms the destination and right-click cancels. The same controller serves ordinary wild/trainer, both player positions in doubles and the first-rival tutorial. The source's `Switch / which?` prompt and tiles30/31 (source) and28/29 (destination) replace the normal PP/type panel while picking.

The typed request carries battle ID/revision, occupant token, actor slot and owned instance identity, in addition to the command session's revision. Application adapters only accept settled move-selection phases. They retain presentation event queues, the partner's selected doubles action, RNG, turn, event sequence and all unrelated state. The Domain transaction swaps complete move-slot values, including PP and PP Ups, and independently maintains persistent moves; Transform uses a bounded live permutation while preserving the donor snapshot and original party. Mimic uses the existing temporary/persistent mapping symmetrically; see the deliberate original-behavior fix in `original-behavior-improvements.md`.

First-rival validation accepts only its same two originally allowed move IDs in either order, with unchanged PP Ups and max-PP restrictions. AI, next-turn resolution and final reward checks match those moves by identity rather than assuming slot0 is always the attack. The untouched pre-battle ownership snapshot remains the reward origin.

Source: `battle_controller_player.c:565–580,631–719,1422–1430`, `battle_controller_oak_old_man.c:338–343`, and `battle_main.c:3124,3145–3161`. Targeted coverage: `BattleMoveOrderApplicationTests`, `BattleMoveOrderPresentationTests`, `SourceBattleMoveOrderTests`, `FirstBattleMoveOrderTests`; native visual review is a separate gate.

### Play-time-only revision handling and verification

A native review caught an integration defect: a one-second `play_time_tick` advanced the command-session revision and made a newly opened battle-party Summary draft appear stale. The existing accepted clock-only rebasing hook now also preserves matching battle-party, learning/replacement and SELECT drafts. It never rebases a guard that was already stale; real command, identity, occupant and battle-revision changes still require a fresh selection. The Application regression verifies that an accepted clock tick changes only `PlayTime` and leaves the battle selection context untouched.

The refreshed full solution built with normal dependencies, zero warnings/errors (2026-10-16 16:51:58 UTC). Focused Summary/reorder/clock tests: Application7/7, Presentation14/14. Domain focused/golden tests59/59; full maintained Battle1,704/1,704 and Domain277/277, zero skips. Cursor extraction checks2/2. A fresh, explicitly controlled Route1 FIELD fixture was separately strict-codec/restoration validated and exercised through ordinary capture and encounter/KO to the Lv20 RAZOR LEAF learning pause; it is not an organically played save or full-route coverage. Native review after the clock correction is tracked separately.

### Bounded native review after the clock fix (2026-10-16 16:52–16:58 UTC)

The parent reviewed the refreshed native Godot build (MVID `7e167d0f-47b0-4756-a4d8-3a51c793ecb8`) using the fresh controlled Route1 fixture:

- Battle PARTY → SUMMARY remained valid after waiting more than a second. INFO → MOVES → read-only details displayed type, power, accuracy and description; Back returned through the expected screens.
- Physical SELECT/Space opened the two-cursor FIGHT draft. After roughly15 seconds, Enter swapped TACKLE0/GROWL1. The accepted `battle_move_swap` spent no turn or PP. The subsequently selected TACKLE at its new slot1 performed the ordinary KO.
- Bulbasaur reached Lv20 and the RAZOR LEAF learning choice. The shared five-row Summary displayed the new fifth row with GRASS, power55, accuracy95, PP25 and its description.
- No native errors were observed. The reviewer closed the controlled test without committing a move replacement. This is representative native inspection/reorder/learning-page evidence, not a complete single-player route, all battle layouts, or a native replacement-commit claim.

Review log: `/workspace/shared/firered-summary-visual/native-summary-1652.log`. The previous bounded review exposed the one-second clock defect and is retained separately as `native-summary-1645.log`; it is not counted as a pass.


### Normal Summary from Day-Care party callbacks

Route 5 and Four Island share the ordinary Summary interaction, including Moves
→ A for details, two-stage reordering/cancellation, pointer rows, and bounded
page/party browsing. Skills/Moves skip Eggs; Info includes them. LR uses the
existing active-Summary dispatcher. No extra input dispatcher or service-specific
Summary controller is introduced.

Source: `party_menu.c:3019–3021,3095–3106` selects the Day-Care actions and opens
`PSS_MODE_NORMAL`; `pokemon_summary_screen.c:1004–1010` clears `lockMovesFlag`,
and `3589–3618` permits move swapping outside battle/link modes. The previous
read-only Day-Care restriction was therefore not source behavior.

The existing Route 5 and Four Island actor/source/`waitstate` guards admit only
the deposit party callback to the shared owned-move transaction. It retains the
script frame, wait correlation, service object, party identities and RNG. The
global stable-field gate is unchanged; dialogue, withdrawal/resident selection,
other services and battle do not inherit this permission. The transaction keeps
full move slots, identity/revision checks and ownership validation. Deposit and
cancel alone resume the source script; normal deposit PP restoration still applies.

`DayCareSummaryTests` contains public service-entry, reorder/deposit/Continue,
cancel, stale/invalid target, Egg, clock-only revision, resident-menu rejection
and authenticated-callback forgery regressions. Fixtures relocate the existing
owned checkpoint party without adding badges, wins or synthetic creatures.
Build, execution and native-window acceptance must be recorded separately; source
and test additions alone do not claim these gates passed.


Verification on 2026-10-19: full solution build passed with zero warnings/errors
(94.35 seconds); the final DayCareSummary/BattleSummaryLearning Application run
passed 13/13 (14 seconds), including all 11 new Day-Care cases. Shared Summary
and FriendshipContact Presentation regressions passed 33/33 (6 seconds).
The initial Four Island fixture incorrectly stood on the counter, then omitted
its regional movement-direction initialization. Both failed runs were retained;
the corrected fixture now uses actual walkable source floor and the existing
EnterMap→Pump initialization, preserving strict-save, actor and unchanged-RNG
assertions. No production admission was relaxed. These tests exercise service
transactions and shared presentation rules; this binary's actual Day-Care
keyboard/mouse window routing and speaker output have not been visually/audibly
accepted.

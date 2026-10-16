# Native Summary presentation

The shared owned Summary renderer now uses the original Info, Skills, Moves,
Move Details and Egg tilemaps from pret/pokefirered
`037335f4c725d7c9aecdac87066f2002b4bd7e14`. Field and PC entry points keep their
existing identity/revision guards and Application-owned move swapping. The
Day-Care read-only entry uses the same skin without exposing move mutation.

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

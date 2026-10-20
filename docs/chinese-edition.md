# Simplified Chinese edition

The local Chinese text pack uses Xzonn/PretFRLG commit
`a04f63173ca5fcf9c2db18ee223cf057c7f232c0`. Only display declarations are
imported. Gameplay scripts, formulas and original encoded generation inputs
remain from the fixed FireRed rules source. Original title and replacement
pictures are retained.

## Build the local pack

With the pinned source inputs already available locally:

```
python scripts/imports/presentation/import-chinese-text.py
python scripts/imports/presentation/import-cjk-font.py --text-pack content/generated/localization/zh-Hans --out content/generated/localization/zh-Hans/font --include-naming-characters
python dev run --language zh-Hans
```

The English default is `python dev run`. A missing explicitly selected Chinese
pack is an error. The text importer produces the existing 32 source groups and
7 authored groups; absent overrides retain their generated English values.
Raw inputs, generated packs and font images are local build inputs/outputs,
not tracked artifacts. Chinese glyphs use Fusion Pixel 2026.10.16, native
12-pixel normal and 10-pixel small zh-Hans monospaced faces. The original
Latin atlas and title/replacement pictures are retained.

Obtain the official `fusion-pixel-font-{10,12}px-monospaced-otf-v2026.10.16.zip`
archives from [the fixed release](https://github.com/TakWolf/fusion-pixel-font/releases/tag/2026.10.16).
Place the two `fusion-pixel-{10,12}px-monospaced-zh_hans.otf` files, `OFL.txt`
and `LICENSES/` in `content/raw/fonts/fusion-pixel/2026.10.16/`. The importer
copies the licenses beside the generated masks so distributed packs retain
font notices. FontTools and Pillow are import-time dependencies only.

The initial edition deliberately leaves small-face 靓 and 黾 missing, as
approved. Display uses the original visible `?` marker and a diagnostic;
there is no secondary font substitution. Naming requires support in both
faces and rejects unsupported input before submission. The supported naming
repertoire is smaller than the provisional Noto font; for example 𠮷 and 䶵
are unavailable. Existing literal names remain unchanged in saved data.

Reviewed corrections are small keyed files under
`scripts/recipes/localization/zh-Hans`. Authored application wording lives in
`src/Game.Content/Text/zh-Hans`, matching the existing English group IDs.
Do not duplicate an entire English source catalog to add a translation.

## Layout compared with the translation source

The fixed translation uses bitmap Chinese glyphs: `src/chinese_text.c:40–80`
advances normal Han by 12 pixels and small Han by 10, with 12×15 and 10×13
render boxes. `tools/generate_font.py:43–55` selects SimSun 12 and DinkieBitmap
10. Its normal dialogue is a 208×32 window with a 15-pixel line step; the
printer follows explicit newline/scroll/page commands and clips at the edge.
It does not automatically wrap prose to the available width. Translated source
newlines are therefore retained by this project's measured layout.

The translation's `naming_screen.c` is unchanged from the fixed rules source:
Latin keys, one-byte edits and 8-pixel input slots. Free Unicode naming and
long-name presentation in this project are additional capabilities, not claims
about the original translation. Fusion now matches the 12/10-pixel Han
advances using its native pixel outlines, without horizontal scaling. Ink
width is independent of advance so the one-pixel shadow is not clipped for
the few glyphs that overhang their advance.
The Help `CIRCLE_DOT` symbol uses its original extended glyph in both faces
(normal advance 10, small advance 8), independently of the Chinese font.

## Identity and controls

Source labels and IDs remain authoritative. Translation may reorder named
values within a control-delimited phrase and reflow ordinary lines. Command
operands, scroll/page boundaries and terminators retain their source semantics.
Bound player text is literal and is never parsed again as source controls.
The source scanner selects the fixed preprocessor branch, including the
Daisy FONT branch, rather than concatenating mutually exclusive declarations.

Player, rival and OT names are literal Unicode strings. Trainer names allow
7 text elements, custom Pokémon nicknames 10 and box names 8. A shared scalar
limit bounds pathological combining sequences. No hidden English player name
is created. Original NPC/species encoding needed for source generation stays
in that generation authority. Mail stores its author text, full trainer ID
and authenticated origin rather than a second pseudo-encoded name.

This changes saves to schema 3. Schema 1/2 files cannot be continued directly;
they are not silently renamed, guessed or overwritten. Existing historical
checkpoints remain separate. Rebuild controlled test saves through current
public commands; any deliberate one-off conversion must retain the original
and have the actual missing state available.

## Verification scope

The assembled pack has 11,540 overrides across the existing 32 source and
7 authored groups, with 880 resolved source aliases. Every imported source
control signature and the complete current-game catalog were validated. The
source collector (20 cases), catalog mapping (19), and glyph importer (15)
checks pass. Focused identity/save/mail/name tests cover Unicode, exact OT
identity, explicit mail origins and rejection of old save schemas.

Chinese messages use measured two-row visual pages while retaining their
original semantic page and acknowledgement ownership. Fixed summary windows
use bounded measured wrapping, and compact labels share fitted/scrolling
rendering. The final affected layout run passed 79 cases. The normal and small supplemental
fonts expose 11,026 and 11,024 glyphs respectively; immutable masks are shared and
colored atlas pages are allocated on demand. The local generated font files
occupy 2,501,303 bytes including required license notices. Unsupported name glyphs are rejected explicitly before
submission, rather than silently saving question marks.

Cloud-window acceptance on 2026-10-20 with the provisional font exercised original title art, translated
opening guides/dialogue, and literal Chinese player/rival naming. Direct input,
delete, cancel, confirmation and a supplementary Han character were checked;
an unsupported emoji was explicitly rejected. The fresh new-game producer
retained the exact Unicode names. A five-row rival-name frame overflow found
in that run was corrected through the shared frame geometry. The selected Fusion
font was subsequently checked in the actual cloud window through the opening
control guide, prose pages and Oak dialogue, then the small-font Help screen
and its return to the unchanged field position; the title art remains unchanged.
One launch logged an X11 driver BadValue during startup; the subsequent
Continue/Help launch had no errors, exceptions or exit-leak warnings.

Source fallback auditing distinguishes unused Japanese declarations, Braille,
credits, units and neutral symbols from reachable untranslated prose. These
checks and the representative window are not whole-game visual acceptance.
Long-message, custom-name, save and control semantics are covered by focused
automated tests; not every later-game screen was visited in Chinese.

Chinese IME input and actual audio-device playback require separate genuine
acceptance. Dummy audio and typed/pasted Unicode are not evidence of either.

## Godot dialogue-control migration

The first product consumer is Oak's opening speech. `SourceDialogText` uses
`RichTextLabel` with the existing pixel `FontFile`, plain-text shaping, native
line ranges and `visible_characters`. It does not measure or draw glyphs itself.
A small visual-page cursor allows confirmation between engine-produced two-line
pages and releases the semantic acknowledgement only once after the last page.
`SourceTextProgress` retains the established source-frame cadence and emits a
Unicode-scalar reveal count, including line breaks; it does not lay out text.

Oak no longer uses the manual `SourceMessageCursor`/`ItemMessageBox` text path.
The scene explicitly hides the child control outside the selected speech branch,
including portrait fades, the return animation and menus. Its existing pixel
font owns foreground/shadow colors; this slice does not introduce a second
font or palette cache. The field and ordinary battle consumers described below
now share this control; Help and other specialized UI still retain their old
layout implementation.

The affected build passed with no warnings/errors, and 18 focused opening,
printer and page-state cases passed. An isolated Godot 4.7.2 prototype checked
10/12-pixel metrics, stable layout under character reveal, literal text,
visual-page boundaries and unique semantic completion (245 assertions across
these scenarios, not 245 independent cases). Actual cloud-window acceptance
then exercised the product opening through the gender menu and an external
long-text override: all three visual pages remained readable before the next
semantic message; consecutive confirmation sped up the next message without
skipping it. A final review also removed an unnecessary same-message binding cache: an
explicit page reset must clear its acknowledgement latch even when returning
to retained text during Nidoran send-out. Both initial runs exited normally
without errors or leak warnings. A final native retry entered Nidoran send-out,
immediately returned to the retained message, and successfully confirmed it
again into the next message; the process exited normally. The prototype's battle-text layout check is not product battle-control acceptance.


### Field and ordinary battle surfaces

Map dialogue now binds the same `SourceDialogText`, including the egg-hatch
message consumer of the field printer. `SourceTextProgress` retains the selected
slow/mid/fast source cadence, EOS wait and held-button speed-up. A visual page
advance does not increment the dialogue's semantic page or send a command;
existing script/choice/automatic-print completion owners remain responsible for
acknowledgements. Trainer Tower speech identity is retained separately. Braille
keeps its specialized renderer and clears the ordinary text binding.

`SourceDialogSurface` hosts the control in one retained transparent `SubViewport`
and composites its texture at the original parent draw position. Thus later
menus, screens and fades continue to cover text, rather than accidentally
rendering underneath a direct child control. Viewport size follows the native
control, filtering is nearest, and every submitted composite explicitly requests
one cleared render. A rendered regression caught a blank first frame with
`WhenVisible`; explicit `Once` scheduling fixes it. No viewport or texture is
created per frame. Palette-only font changes retain page/reveal state and reuse
the existing `SourceGameFont` variants; no second glyph/layout cache is introduced.

Ordinary single/double battle message windows, tutorial Oak dialogue and the
single-battle move-learning introduction use the surface. They use the shared
progressive printer at saved 8/4/1 source speed, with logical held A/B acceleration
and source-string continuation metadata (see [battle text](battle-text-progress.md)).
Battle event/source page indices, authoritative ACKs and animation/cry/HP gates
remain owned by their existing consumers. Notices, party/medicine, capture,
trainer-shift choices and stats have specialized retained paths and are not
claimed migrated.

The serial solution build passed with zero warnings/errors. The focused field
printer/page-cursor run passed 16 cases, including all three text speeds,
continuation latch retention, EOS readiness and resetting identical text for a
new semantic owner. The battle-focused regression run passed 42 cases.

Actual Godot 4.7.2/.NET 10.0.12 X11 field acceptance used a fresh new game, not the
restored legacy checkpoint (which was correctly rejected as unsupported). The
NES interaction printed both original semantic pages and returned control to the
field. A separate external test pack expanded only its first message to three
visual pages: consecutive confirmation revealed the next page without skipping
it, all visual continuations produced no campaign ACK, then exactly one ACK
advanced to the original second message and one closed it. A subsequent turn
command was accepted. The external pack is not a shipped translation change.
Audio was Dummy; this does not establish actual audio or IME acceptance.

The rendered surface regression checks first-frame ink, clearing previous-page
pixels during reveal reset, changed next-page pixels, Clear/rebind, resize and
parent overlay ordering. Run after building the Godot project, from a graphical
desktop terminal (not headless):

```sh
source scripts/env.sh
"$GODOT" --path src/Game.Godot --audio-driver Dummy --script "$PWD/scripts/checks/test-field-dialog-surface.gd"
```

The test exits nonzero on an assertion failure. Its fallback font tests surface
lifecycle rather than the Chinese font itself; the product field checks above
used the actual Fusion Pixel pack. These checks are not a full-game UI pass;
late-game field branches and the egg-hatch scene have not been replayed in this
migration slice. The restored archive lacks many unrelated presentation oracle
fixtures, so an attempted broader presentation suite is not a full-suite pass.

The maintained battle-control probe links the production `SourceGameFont`,
`SourceDialogText` and `SourceDialogSurface` into a temporary Godot project. It
requires the built Debug Content/Domain assemblies, generated source font and
local `zh-Hans` font pack; it neither tracks assets nor modifies a player save.
Run from a graphical desktop terminal after building the project:

```sh
python3 scripts/checks/test-battle-dialog-text.py
```

Use `--output <directory>` to retain build/runtime logs and `render.png` in a
chosen directory. `--headless` checks layout only and explicitly skips pixels;
it is not rendering acceptance. The launcher uses `dev env` for the official
toolchain, disables audio with Dummy, and fails on missing inputs, exceptions,
nonzero exit, missing completion evidence or failed assertions.

On Godot 4.7.2 / CLR 10.0.12 the actual Fusion Pixel/source bitmap font probe
passed instant reveal, explicit three-page continuation, same-text reset,
literal Unicode text, Chinese native wrapping and continuation bounds. X11
rendering additionally passed nonempty bitmap glyph pixels, changed next-page
pixels, white palette replacement without resetting reveal/page state,
exact-width text layout, Clear and parent overlay checks. The exact-width check
is a layout/ink check, not a per-glyph shadow-overhang oracle. These control tests
do not claim to exercise campaign command rejection or duplicate-ACK behavior.

The broader Presentation run completed with **1,897 passed, 415 failed and no
skips (2,312 total)**. Of the failures, 409 require missing oracle fixtures;
the other six are existing local content/test mismatches outside this migration:

- Claw `GlobalObjectShake` timing expects `(0,4)` but receives `(0,0)`.
- Claw terrain-operand acceptance differs from the test expectation.
- `impact` and `small-ember` each contain duplicate indexed/legacy manifest assets.
- `SpritePaletteGroup.Resolve` returns a cached mutable palette alias.
- `StorageMoveHelp` expects the old source-keyboard wording rather than the
  current visible-characters wording.

The relevant tracked inputs are unchanged from HEAD; the historical baseline
of generated assets is not provable from this archive. This is not a full-suite
pass, and these unrelated failures were not changed in the dialogue slice.

A fresh-game X11 walkthrough on the same official Godot/CLR build naturally
reached Oak's lab, chose Bulbasaur and completed the rival tutorial battle in
five Tackle turns. Native white battle messages and gray Oak tutorial pages
were legible; HP/animation gates, fainting, 69 EXP, level 6, both stat overlay
pages, prize text and return to native field dialogue all progressed normally.
No runtime errors or exceptions were found in the walkthrough log. This used
Dummy audio and the bedroom entry, not the opening/naming sequence. It does not
establish ordinary wild/double/capture battle, late-game or audible-cry coverage.
The final combined focused regression selection passed **55/55 with no skips**.

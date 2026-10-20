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
font or palette cache. Field, battle, Help and other UI consumers have not yet
migrated to this control, so their old layout implementation remains in use.

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

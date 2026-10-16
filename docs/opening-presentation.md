# Opening and identity presentation

Ordinary startup now runs the copyright/Game Freak/grass/Gengar–Nidorino presentation before the title. See [intro movie](intro-movie.md) for its source stages and fidelity limits. Movie A/Start/Select skips after copyright; the title reveal accepts A/B/Start to finish the reveal, with a separate A/Start press required to enter. Mouse clicks follow the same guarded A action. Title idle restarts the movie after 2700 presentation frames. No gameplay RNG or save state is advanced by these scenes.

The title uses source tile layers, a Charizard scanline sweep and grayscale fade, flash/window reveal and logo reveal. Its run state uses the source's local seed 30840, LCG, 18-frame flame spawn cadence, ten-frame compact flame sheet, 60-visible/30-hidden press-start cadence and 540-frame slash wait. The copyright colors remain visible during press-start blink. Title confirmation plays the normal Charizard cry and presents a 90-frame hold followed by the white fade. Flame animation continues during that hold; an already-active slash finishes, while new slashes and idle restart are disabled. The host renderer uses alpha blending rather than the original hardware's palette/scanline registers; DMA task setup and palette completion frames are not claimed cycle-identical.

New Game presents the three controls guide pages and three Pikachu introduction pages, with source text, control icons, backdrop and compact body/ear/eye animation tracks. A advances; B goes back within each three-page section and does nothing on its first page. The host also accepts left/right for page navigation. The final Pikachu page plays the exit theme before Oak. Oak dialogue is paged and uses the shared source Unicode character-printer with saved text speed and a user-triggered speed-up. The Oak/player/rival/Nidoran portraits, platform and separate name confirmations remain source art. Nidoran stays in front of Oak and follows the source ball delay, emerge scale/sine flight, normal cry, return shrink and trade-ball cue, with cry-finish gating and the shared Pokéball color tint; particles share the battle capture implementation and atlas. Rival naming offers NEW NAME and the source GREEN/GARY/KAZ/TORU list after the 30-frame portrait slide. Rejecting or cancelling a rival name returns to those choices. Final confirmation runs the source's five 20-frame player-shrink steps, white silhouette, warp cue and fade before creating the campaign.

Continue retains its verified save re-read. Introduction/naming never writes a save or bypasses overwrite confirmation. Keyboard and pointer same-frame, drag/release and double-click guards remain in place.

Naming keeps the focused native LineEdit, seven-character validation, selection, caret, direct typing, Enter submission and Escape/back ownership. The optional pointer/controller keyboard offers source upper/lower/symbol rows, page switching, deletion and confirmation. D-pad navigation wraps across letters and action buttons; A activates, B deletes and Start selects OK. Tab switches between direct editing and grid navigation; typing a printable character resumes direct editing. Its hit targets/layout are adapted for the host rather than replacing modern text entry.

## Resources and reconstruction

Run from the project root:

    python scripts/imports/presentation/import-opening-presentation.py
    python scripts/imports/presentation/import-opening-presentation.py --check

Use `--raw <pinned-source-root> --out <generated-package-directory>` for isolated reconstruction/checks. `generate(raw)` returns bytes without writing output.

The importer reads local pinned pret `037335f4c725d7c9aecdac87066f2002b4bd7e14` inputs: naming/intro text, Oak/title source animation commands and PNG/palette/tilemaps. Palette grayscale conversion follows `palette.c:TintPalette_GrayScale2`. Downloaded bytes are checked against the pinned Git blob hash; binary connector text normalization is reversed only when that hash matches. No upstream scripts execute. Source and converted Pokémon assets stay local and ignored.

Runtime reads only `content/generated/ui/opening` and the separate generated movie package. Opening JSON contains text, keyboard characters and compact frame/duration tracks. The remaining assets are shared RGBA layers and sprite sheets, not a baked movie. Runtime data contains no raw source paths, hashes or provenance inventory.

## Verification and remaining fidelity

Offline regeneration currently verifies 26 files. Content tests cover exact dialogue/characters, substitutions, guides, animation durations, PNG presence, generated-only loading and malformed keyboard refusal. Presentation tests cover flame determinism/chunk independence, bounded sprite lifetime, blink/slash/restart boundaries, compact animation boundaries, reveal/exit scale steps, Nidoran flight endpoints and shared Pokéball burst boundaries. Capture regressions guard the shared particle extraction. Integrated build/test results belong in the current task evidence, not an inferred UI/audio acceptance claim.

Still outstanding: real user-window walkthrough/audio output, exact Nidoran/background palette-fade matching, remaining trainer portrait crossfades, original naming-screen decoration/page transitions, and pixel/frame comparison of host palette/window effects. The source player-name path goes directly to native naming; its initial player-name suggestion array is not a visible menu in that route. The movie has its own explicit approximation/audio limitations. `fullTitleIntroImplemented=false` intentionally remains until these are resolved; the implementation is no longer described as a still title, but is not certified pixel-perfect or fully finished.

### 2026-10-16 native sampling and regression evidence

The shared serial batch (`source scripts/env.sh`; `dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false`) passed with zero warnings/errors. Its focused content, presentation and importer runs passed; log: `/tmp/firered-quest-opening-batch.log`. The later animation batch rebuilt the solution and passed the opening content tests and opening/capture presentation regressions; an unrelated Self-Destruct regression in the full presentation suite was preserved and sent to its owner rather than hidden.

A real Godot window from build 55 (MVID `a3abb8e0-6f34-43d9-92bf-8ac6e4488faf`) was launched with a fresh XDG profile and Dummy audio. The parent observed the animated star trail, transition to title, and entry into the controls guide. Log: `/workspace/shared/firered-opening-visual/native-opening-1542.log`. This was bounded sampling, not inspection of every fight frame or the full naming/new-game route; no speaker/audio-output acceptance is claimed.

That sample found a real controls-page clipping bug: source LF characters reached Godot's single-line DrawString and were visually concatenated. `OpeningGuideText` now explicitly draws each source row, retaining LF/blank-row structure, with the source controls/Pikachu line spacing. The rebuilt native window (MVID `9268cf7a-854c-410c-b37e-51b685d091b8`, log `/workspace/shared/firered-opening-visual/native-opening-1550.log`) verified all three D-pad/A/B control rows readable and in bounds, plus correct first explanatory-page wrapping. It did not verify the entire Oak/naming route.

After the later compiler release, title-exit clock/deactivation regression coverage was added to `OpeningTitlePresentationTests`. The next integrated build passed and its full presentation suite passed 169/169, including this regression.


A later native sample exposed the Game Freak name as an opaque white rectangle. The pinned `gbagfx/main.c` passes `!hasPalette` to `ConvertToTiles4Bpp`; `gfx.c` converts grayscale nibbles using `15 - value`. The shared `gba_4bpp_indices` helper now applies that convention in the intro, opening and NPC trade importers. This fixes the Game Freak name, title border and wireless signal. Independent tests read raw grayscale pixels and reconstruct expected palette/alpha values (including all 32 wireless signal frames); all three pass, and all three reject the old convention under an in-memory mutation check. All three packages regenerate exactly. The regenerated Game Freak strip and title composite were inspected as images; no new full native or speaker acceptance is inferred. The new ResourceImport wrapper needs its next compiled test run; the underlying Python tests already passed directly.

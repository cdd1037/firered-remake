# Opening and identity presentation

Ordinary startup runs the copyright/Game Freak/grass/Gengar–Nidorino presentation before the title. See [intro movie](intro-movie.md) for its source stages and fidelity limits. Movie A/Start/Select skips after copyright; the title reveal accepts A/B/Start to finish the reveal, with a separate A/Start press required to enter. Mouse clicks follow the same guarded A action. Title idle restarts the movie after 2700 presentation frames. No gameplay RNG or save state is advanced by these scenes.

The title uses source tile layers, a Charizard scanline sweep and grayscale fade, flash/window reveal and logo reveal. Its run state uses the source's local seed 30840, LCG, 18-frame flame spawn cadence, ten-frame compact flame sheet, 60-visible/30-hidden press-start cadence and 540-frame slash wait. The copyright colors remain visible during press-start blink. Title confirmation plays the normal Charizard cry and presents a 90-frame hold followed by the white fade. Flame animation continues during that hold; an already-active slash finishes, while new slashes and idle restart are disabled. The host renderer uses alpha blending rather than the original hardware's palette/scanline registers; DMA task setup and palette completion frames are not claimed cycle-identical.

New Game presents the three controls guide pages and three Pikachu introduction pages, with source text, control icons, backdrop and compact body/ear/eye animation tracks. A advances; B goes back within each three-page section and does nothing on its first page. The host also accepts left/right for page navigation. The final Pikachu page plays the exit theme before Oak. Oak dialogue is paged and uses the shared source Unicode character-printer with saved text speed and a user-triggered speed-up. The Oak/player/rival/Nidoran portraits, platform and separate name confirmations remain source art. Nidoran stays in front of Oak and follows the source ball delay, emerge scale/sine flight, normal cry, return shrink and trade-ball cue, with cry-finish gating and the shared Pokéball color tint; particles share the battle capture implementation and atlas. Rival naming offers NEW NAME and the source GREEN/GARY/KAZ/TORU list after the 30-frame portrait slide. Rejecting or cancelling a rival name returns to those choices. Final confirmation runs the source's five 20-frame player-shrink steps, white silhouette, warp cue and fade before creating the campaign.

Oak A/B held acceleration, same-message continuation and the remaining timing boundaries are documented in [opening checks and limits](opening-presentation.md#verification-and-limits). Unlike the guide pages, B advances Oak speech rather than going backward.

Continue retains its verified save re-read. Introduction/naming never writes a save or bypasses overwrite confirmation. Keyboard and pointer same-frame, drag/release and double-click guards remain in place.

Naming keeps the focused native LineEdit, seven-character validation, selection, caret, direct typing, Enter submission and Escape/back ownership. The optional pointer/controller keyboard offers source upper/lower/symbol rows, page switching, deletion and confirmation. D-pad navigation wraps across letters and action buttons; A activates, B deletes and Start selects OK. Tab switches between direct editing and grid navigation; typing a printable character resumes direct editing. The backdrop, upper/lower/symbol page colors and borders, page/back/OK button sprites, cursor, input arrow, underscores and animated player/rival icons now come from the original naming assets. The source's uneven letter-column spacing is imported. Page swaps use the native sine table, a 40-pixel excursion, priority reversal at frame 16, 32 movement frames and a teardown frame; the page button label slides off/on independently. Repeated grid activation is ignored during a swap, direct typing/IME and Escape remain available, and reopening resets the animation. The native editor is aligned to the source name box; its selection/caret and the host control-help banner remain deliberate host adaptations.

Oak-to-gender, chosen-player reveal, player-to-rival, and rival-to-player transitions now use the source trainer alpha-task cadence: outgoing one-step and incoming two-step coefficients with a two-frame delay, plus the source platform visibility threshold. The speech printer and A/B navigation wait for that transition; exiting the introduction clears pending transition work. These host alpha layers are not a claim of hardware palette equivalence.

Trainer art and platform sprites also retain their shared source horizontal offset. The first rival-name question slides both 60 pixels right to leave room for the choices; selecting a preset does not recenter them. Returning from either the player or rival naming editor restores that same +60 position. Confirmation, the remembered-rival speech, outgoing fade, and rival NO/B/cancel retries retain it. Only the incoming trainer is centered, after the outgoing trainer is invisible. These are the boundaries in pinned `oak_speech.c`'s `MoveRivalDisplayNameOptions`, `RepeatNameQuestion`, `ReturnFromNamingScreen`, `FadeInRivalPic` and `ReshowPlayersPic`; there is no invented return slide.

## Resources and reconstruction

Run from the project root:

    python scripts/imports/presentation/import-opening-presentation.py
    python scripts/imports/presentation/import-opening-presentation.py --check

Use `--raw <pinned-source-root> --out <generated-package-directory>` for isolated reconstruction/checks. `generate(raw)` returns bytes without writing output.

The importer reads local pinned pret `037335f4c725d7c9aecdac87066f2002b4bd7e14` inputs: naming/intro text, Oak/title source animation commands and PNG/palette/tilemaps. Palette grayscale conversion follows `palette.c:TintPalette_GrayScale2`. Downloaded bytes are checked against the pinned Git blob hash; binary connector text normalization is reversed only when that hash matches. No upstream scripts execute. Source and converted Pokémon assets stay local and ignored.

Runtime reads only `content/generated/ui/opening` and the separate generated movie package. Opening JSON contains text, keyboard characters and compact frame/duration tracks. The remaining assets are shared RGBA layers and sprite sheets, not a baked movie. Runtime data contains no raw source paths, hashes or provenance inventory.

## Shared naming control and drawing contracts

Player/rival, capture/Safari, gift, hatch, Name Rater, party nickname and PC box naming share the editor, keyboard layers and coordinate layout. Synchronizing an unchanged target retains its editor and animation; closing releases focus and hides it. The editor bounds are `(94,47,104,18)` logical pixels. Both `normal` and `read_only` styles are overridden, and final size is assigned after theme/tree setup so Godot's default minimum does not cover the keyboard. The background remains transparent over the source white panel and underscores. Text, selection, caret and IME belong to `LineEdit`; there is no duplicate manual name draw. The separate input arrow and moving keyboard layers are intentional.

Guide text draws source LF/blank rows separately, with the source controls/Pikachu line spacing, rather than concatenating lines through a single-line draw API. Grayscale source PNGs use pinned gbagfx's `15 - value` nibble conversion: white is transparent index 0, black is palette 15. This shared convention applies to the Game Freak name, title border and NPC trade wireless signal.

## Verification and limits

Run the regeneration/check above and these independent source checks:

```sh
python3 scripts/checks/test-opening-naming-import.py
python3 scripts/checks/test-opening-grayscale-import.py
```

The naming check reconstructs every background/keyboard pixel from source tile words and palettes, rejects malformed tiles and checks sprite extents. The grayscale check reconstructs palette/alpha from raw pixels, including all wireless frames. Content and presentation tests cover exact dialogue/substitutions, generated-only loading, malformed keyboards, deterministic flame/compact-animation clocks, reveal/shrink/flight endpoints, shared capture particles, page swaps and trainer offset/fade/platform boundaries. They do not substitute for rendered or audible acceptance.

After a normal product build, inspect the actual compiled editor with an isolated profile:

```sh
source scripts/env.sh
probe=$(mktemp -d)
FIRERED_PROJECT_ROOT="$PWD" FIRERED_PROFILE=current FIRERED_PLAYER_ENTRY=1 FIRERED_AUDIO=0 \
  XDG_DATA_HOME="$probe/data" XDG_CONFIG_HOME="$probe/config" XDG_CACHE_HOME="$probe/cache" \
  "$GODOT" --headless --path src/Game.Godot \
  --script "$PWD/scripts/checks/test-opening-name-editor.gd"
```

This checks bounds, minimum size, focus, selection and the seven-character cap, not pixels or sound. Current scoped native naming/portrait checks and public-input opening routes, including Chinese and the female ending, are recorded in [validation](../validation.md). They are controlled renderer/input tests, not a manual real-time complete playthrough; they write no player save.

Still outstanding: whole-screen naming-entry/return fades, exact Nidoran/background palette fades, portrait palette/window details and hardware pixel/frame equivalence. The source player-name path opens native naming directly; its initial suggestion array is not a visible menu on that route. The [movie](intro-movie.md) has separate approximations. `fullTitleIntroImplemented=false` remains intentional: bounded native checks do not certify every branch, complete-game fidelity or actual speaker output.

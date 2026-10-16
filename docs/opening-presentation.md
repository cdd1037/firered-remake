# Opening and identity presentation

The ordinary player entry now displays the original FireRed title tile composition before reading the selected menu route. New Game uses source Oak dialogue pages, the Oak/player/rival portraits, Nidoran♀ demonstration image, original background/platform and separate name confirmations. The final Oak message completes before creating the new campaign. Continue retains its verified save re-read; introduction and naming do not write a save or bypass existing overwrite confirmation.

Naming keeps the focused native LineEdit, seven-character validation, selection, caret, direct typing, Enter submission and Escape/back ownership. An additional pointer/controller keyboard offers the original upper/lower/symbol character rows, page switching, deletion and confirmation. D-pad navigation wraps across letters and action buttons; A inserts/activates, B deletes, and Start selects OK. Keyboard Tab switches between direct editing and arrow-key grid navigation; typing a printable character resumes direct editing. Its hit targets/layout are adapted for this host; it is not a pixel-identical replacement of the original naming screen. Mouse drag/release, double-click and same-frame entry transition guards are retained. Existing saved options still apply before entry/new-game transitions.

## Resources and reconstruction

Run from the project root:

    python scripts/imports/presentation/import-opening-presentation.py
    python scripts/imports/presentation/import-opening-presentation.py --check

Use `--raw <pinned-source-root> --out <generated-package-directory>` for isolated reconstruction/checks. `generate(raw)` returns bytes without writing output.

The importer reads only local pinned pret `037335f4c725d7c9aecdac87066f2002b4bd7e14` inputs: `src/naming_screen.c`, `data/text/new_game_intro.inc`, Oak speech PNG/palettes/tilemap, title PNG/palettes/tilemaps, and Nidoran♀ front art/palette. `src/oak_speech.c` and `src/title_screen.c` are sequencing/composition references. New downloaded source bytes were checked against the pinned Git tree (binary connector text normalization was reversed only when the resulting Git blob hash matched). It never runs upstream scripts. Source and converted Pokémon assets remain local, ignored content.

Runtime reads only `content/generated/ui/opening`. The generated JSON contains text and naming characters, with no source paths, hashes or provenance inventories. The rest is RGBA PNG; no raw C/ASM or import recipe is consulted by the game.

## Evidence and remaining fidelity

Deterministic offline regeneration checks all nine generated files. Content and cursor-model tests exercise page boundaries, exact source names/characters, substitutions, PNG presence, generated-only loading and malformed keyboard refusal. The first targeted run passed 2 content tests and 1 resource-import test. Shared PhysicalMoveAnimation/Itemfinder tests also passed 15/15 during the same compiler slot. Later controller wiring and its two cursor tests require the next integrated build. These checks are not an actual user-window route or audio test.

Remaining: the pre-title battle movie, title flame/scanline/slash/blink timing, controls/Pikachu guide pages, default-name suggestion menus, original naming-screen decorative art and page animation, per-character Oak text timing, ball/cry synchronization, portrait blend/slide and player-shrink transitions. The current Nidoran demonstration is a still image and title is a still source tile composition. The diagnostic deliberately retains `fullTitleIntroImplemented=false`. No complete intro/audio or pixel-perfect acceptance is claimed.

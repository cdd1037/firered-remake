# Bounded native window checks — 2026-10-16

Godot 4.7.2 stable Mono native Linux window, 1364×1024 desktop, 240×160 game viewport at 4×. Audio driver Dummy: no audible-output claim. Existing built product used without rebuilding or editing product code.

Only labelled, strictly valid synthetic-Hatch-1000 fixture branches were loaded; original checkpoint untouched. No in-game save was requested. Screens were inspected through native desktop screenshots during the run.

## Trainer Card and photograph

Fixture: `/workspace/shared/firered-card-visual/trainer-card-photo-tint-1.json`.
Log: `/workspace/shared/firered-card-visual/card-native-1330.log`.

- Tab → RED opens readable front with ID, money, Pokédex, badges and explicit checkpoint-time annotation.
- Enter flips to reverse, including six silhouette photos and honest `HOF debut time unavailable` text. Backspace flips to front. No visible clipping in inspected stable screens.
- Printer interaction displayed the 50 price and money changed 950→900 after Yes; countdown and subsequent photograph dialogue completed back to field. Tint selection and transient flash were not captured in stable screenshots, so this run does not independently verify their appearance.

## Pokédex

Same fixture/log. Contents → Grassland 1/17 → caught Rattata detail → Enter area all operated. Detail had sprite, footprint, height/weight and description; area screen had highlighted Kanto routes and size silhouettes. Text and sprites remained readable with no observed clipping. No claim about all habitats, unseen-species gating or pixel parity.

## PC rectangle

Fixture: `/workspace/shared/firered-pc-visual/TEST-SYNTHETIC-HATCH-1000-near-viridian-pc.json`.
Log: `/workspace/shared/firered-pc-visual/pc-native-1335.log`.

- Public interaction → Bill's PC → Move Pokémon.
- Initial current box was internal 13 (HELD); navigated with Page Up/Down to internal box0.
- Space enabled MULTI; Return+Right+Down chord selected and grabbed the top-left 2×2 (Spearow, Diglett, Metapod, Weedle sprites). Rectangle and ENTER: PLACE visibly rendered.
- Backspace while held refused with `You're holding a POKéMON!` and retained the rectangle.
- Acknowledged refusal; Page Down twice carried rectangle to empty BOX3. Enter placed four sprites in the expected top-left 2×2, removed large rectangle and restored HOLD ENTER state.
- Backspace after placement returned to Move Pokémon menu normally. Closed native process without saving. No visible clipping in inspected storage/dialogue screens.

These are bounded native-input/visual checks, not a full playthrough, whole-game audio verification, exhaustive persistence proof or all-screen fidelity claim.

# Capture ball presentation

Ordinary wild battles and Safari result pages share a read-only capture-ball timeline. The existing committed `CaptureOutcome` and animation code select the result: failure codes 0–3 show that many shakes; code 4 shows three shakes and a successful click. Presentation never rolls RNG, consumes an item, creates a Pokémon, or changes ownership. Early acknowledgment is blocked until playback completes; the existing application acknowledgment then proceeds normally.

## Native resources

`scripts/imports/presentation/import-capture-balls.py` reads the locally retained pret/pokefirered revision `037335f4c725d7c9aecdac87066f2002b4bd7e14`. Its inputs are the twelve `graphics/interface/ball/*.png` files, `ball_open.png`, `src/data/graphics/interface_pokeballs.h`, `src/battle_anim_special.c`, `src/trig.c`, the `particles.png`/`circle_impact.png` battle animation assets, and the Red/Leaf back-pic PNGs, palettes and throw sequences. Source conversion uses PNG palette indices, RGB555 expansion, and transparent index zero.

The runtime loads only `content/generated/battle/capture-balls`:

- `balls.png`: one 192×48 atlas, twelve palettes × three 16×16 native frames
- `particles.png`: eight native 8×8 particle tiles
- `balls.json`: immutable ball columns, native sine table, per-ball fade colors and trainer throw frames
- `trainers.png`: a 128×320 atlas of both native five-frame trainer throws

`LoadBallGfx` overwrites tile 8 onward with `ball_open` except for Dive, Luxury and Premier. The importer applies the replacement to indices before using each ball's own palette. It does not replace those three balls' custom open frames with the Poké Ball art. Summary screens can reuse the closed atlas tile.

## Playback

The basis is `AnimTask_ThrowBall` and `SpriteCB_ThrowBall_*` in `battle_anim_special.c`, with integer arc translation from `battle_anim_mons.c`, frame/rotation sequences from `pokeball.c`, and the actual sine table. The timeline includes opening, 28 shrinking updates, closing, four diminishing bounces, subpixel shake reversals and pauses, breakout or click, click brightness, caught-intro cue, and final fade. Per-ball native burst shapes follow the source callback families; successful captures use the three native Master Ball particle stars with their original arc offsets and blinking. Target tint uses the imported RGB555 ball colors and integer channel blending. Sounds are dispatched from timeline cues; actual audible output remains a separate verification gate.

This is a Godot callback adapter, not a GBA VBlank/OAM scheduler. The breakout uses the native `data.c` emerge scale (0x28 plus twelve 0x12 updates) and allows 34 frames for the independent palette restoration, rather than claiming cycle-exact affine/palette-task scheduling. Safari uses the native Red/Leaf five-frame throw, per-frame horizontal offsets, twenty-frame release delay and gender-specific ball release coordinate. Global selected-background-palette white flashes are not reproduced by this slice. Old Man's separately paced tutorial adapter remains unchanged.

## Checks

```
python3 scripts/imports/presentation/import-capture-balls.py --check
python3 scripts/checks/test-capture-balls.py
```

`--raw` and `--out` select explicit roots; `--check` performs no writes and fails on missing or differing generated output. Tests independently reconstruct all 36 atlas frames from source indices/palettes, verify all overlay exceptions, and check rejection of a corrupted output. `CaptureBallPresentationTests` covers immutable sampling, all five outcome codes, twelve ball identities, the fixed-point throw and shrink, and success cue timing. `CaptureBallImportTests` registers the native pixel check in the resource-import suite. These checks do not replace a native window capture/playback review.

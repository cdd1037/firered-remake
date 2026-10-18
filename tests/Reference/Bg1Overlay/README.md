# Bounded BG1 overlay reference vectors

Reference work for pret/pokefirered commit
`037335f4c725d7c9aecdac87066f2002b4bd7e14`. No product/runtime/importer code supplies
expected numerical values. The C templates are authored glue; named callbacks,
blend tables and coordinate arrays are extracted verbatim from pinned C. Only
these bounded harnesses are compiled, never the upstream project or its scripts.

## Reproduction

From the repository root (Python 3, Pillow and GNU-compatible `cc` required):

```sh
python tests/Reference/Bg1Overlay/check_vectors.py --repository "$PWD" --output /tmp/firered-overlay-oracle-check
```

Choose a fresh output directory outside the repository. The checker copies
only this authored reference package there, generates all four fixture families,
verifies O0/O2 equality and additional structural/phase assertions, and compares
candidate bytes to the committed presentation fixtures. Generated C, executables,
and candidate fixtures remain in that external directory.

Fixtures are `tests/Game.Presentation.Tests/Fixtures/overlay-*-source-vectors.json`:
- task: 99 cases / 12,684 rows
- mist: 180 cases / 6,524 rows
- glare: 37 scenarios / 3,286 frames / 13,429 coordinate probes
- material: 76 samples / 532 RGB555 blends / 15 viewport hashes / two frame-zero sprites

`verification.json` records pinned input/extracted-body hashes and original
regeneration evidence. Native compiler version is evidence, not a product requirement.
Ordinary presentation tests select reachable FireRed inputs; deliberately seeded
contest/overflow/helper-divergence probes remain source evidence and are labeled.

## Callback timing

Do not treat initializer rows as ordinary subsequent callbacks.
`Cmd_createvisualtask` invokes the task initializer synchronously;
`CreateSpriteAndAnimate` invokes the template sprite callback synchronously.
The main battle phase order is `AnimateSprites`, `BuildOamBuffer`, `RunTasks`.
`BattleMainCB1` executes the animation script before that callback-2 phase.
Consequently task callback ordinal 1 can be in the same global frame as task
creation, and Mist sprite callback ordinal 1 can be in the same global frame
as its synchronous wrapper invocation. Ordinals remain unambiguous independent
of the product's global tick labels.

### Overlay rows

Each case identifies `name`, `contest`, ordered `attacker`/`target` position IDs,
and task ID 3. There are all 16 ordered pairs in both contest-predicate settings
for each family, plus three deliberately seeded cleanup-edge probes.
FireRed's `IsContest` normally returns false; true cases are branch probes,
not a claim that contest gameplay is reachable in FireRed.

Rows:

- `frame`: task callback ordinal, with 0 for initialization.
- `phase`: 0 = initializer; 1 = subsequent step; 2 = manually seeded edge state.
- `alive`: whether `DestroyAnimVisualTask` has occurred.
- `bg1`: raw **u16** software X/Y. Thus subtracting 1 from X=0 yields 65535.
- `registers`: raw `[BLDCNT, BLDALPHA, BG1HOFS, BG1VOFS]`. The first two
  reflect only `SetGpuReg` writes. The scroll registers stay at their explicit
  initializer writes; the unrelated VBlank software-scroll flush is not simulated.
- `attributes`: source enum order `[screenSize, areaOverflow, mosaic,
  charBaseBlock, priority, palettesMode, screenBaseBlock]`. Initial attributes
  are 7 to expose untouched fields. These are controlled attribute-store
  observations, not a bit-packed hardware BGxCNT register.
- `data`: all 16 signed task slots after the call.
- `events`: ordered fixed-width records `[op,a,b,c,d]`.

Event meanings:

| op | arguments |
| -- | -- |
| 1 | register offset, u16 value |
| 2 | BG ID, attribute ID, value |
| 4 | asset ID, symbolic destination map 1 |
| 5 | BG ID, tilemap asset ID |
| 6 | BG ID, compressed-gfx asset ID, tile offset |
| 7 | compressed-palette asset ID, palette entry offset, byte size |
| 8 | palette bank, relocation arg0, relocation arg1 |
| 9 | BG ID, uncompressed-tile asset ID, byte size, tile offset |
| 10 | uncompressed-palette asset ID, palette entry offset, byte size |
| 11 | BG ID passed to InitBattleAnimBg |
| 12 | task ID destroyed |

Asset IDs: 1 ScaryFaceContest map; 2 ScaryFacePlayer map; 3
ScaryFaceOpponent map; 4 ScaryFace graphics; 5 ScaryFace palette;
6 WeatherFogHorizontal tiles; 7 Fog map; 8 DefaultWeather palette.
`GetBattleAnimBg1Data` is extracted exactly: bank 8, BG 1, tile offset 512.
Its memory addresses are controlled buffers. Memory-loading helpers record
arguments, not LZ decompression or VRAM rendering. Material expectations below
cover independent indexed-source reads.

Scary Face state-entry ordinals: 28 / 49 / 77 / 78 (destroy).
Haze: 76 / 157 / 193 / 194. Mist fog: 19 / 100 / 120 / 121.
Cleanup case 3 falls through to case 4 in one callback. Haze/Mist reset software
scroll during cleanup. Mist fade-in/hold uses `EVA + EVB = 17`; fade-out uses 16.

### Mist Ball sprite rows

The source script's six arguments are parsed from `battle_anim_scripts.s`, not
copied into fixture expectations. All 16 ordered battler pairs use both source
single/double coordinate layouts. Normal controls give `GetBattlerSpriteCoord`
and `GetBattlerSpriteCoord2` the same selected-species geometry; eight
`divergent-coordinate-helper-probe` cases intentionally differ and are marked
`probe=true` and `divergent_coord2=true`. They test helper choice only.

`position` is `[x,y,x2,y2]`, `data` has eight signed slots, `args` has all eight
mutable shared arguments, and `visual_count` is the source count after the call.
The source `DestroyAnimSprite` body is included. Pointer storage helpers are
host-safe controlled stubs: `(data[6],data[7])=(1,0)` denotes DestroyAnimSprite,
not a real GBA address. Do not compare that token to host/native pointers.

Callback IDs: 0 wrapper, 1 target-location initializer, 2 linear-start,
3 linear-with-followup, 4 destroy. `callback_before` and `callback_after`
show both phases. `invocation=-1,phase=0` is the allocation state;
`invocation=0,phase=1` is the synchronously invoked wrapper.
Later ordinary calls are phase 2. Phase 3 is an intentionally seeded accumulator
wrap probe. For duration 30: call 1 initializes with no movement, call 2 performs
movement 1, call 31 performs movement 30, call 32 installs destroy, call 33 destroys.

Mist event records: op1 coordinate-helper-1 read `[battler,type,value]`;
op2 coordinate-helper-2 read; op3 stores destroy token 1; op4 installs it;
op5 frees the OAM matrix; op6 destroys the sprite. No events means no controlled
external read/write in that callback. Wide-delta, signed-offset-wrap, equal-X,
and accumulator-wrap cases are separate numerical stress probes. Inputs still
pass through source u8 coordinate returns and signed s16 argument/position slots.

### Glare

See `glare/README.md` for the richer per-phase row schema. It includes source
`CreateSprite`, `AnimateSprites`, `RunTasks`, and `BattleMainCB2` as well as the
four requested Glare bodies. Raw child creation does not invoke child callback
synchronously. First child callback is birth+1; destruction is birth+37.
Normal task destruction is ordinal 89, full saturation 53, one-free-slot 81.
Initializer overwrites `tIsContest=FALSE`; forced-true cases are isolated probes.

### Independent material pixels

This Python oracle reads PNG indices/palettes and little-endian map entries
directly, without importer/runtime calls. `sample_fields` and `blend_fields`
are embedded in JSON. Sampling wraps a 256x256 text map, honors both tile flips,
retains literal palette-bank bits, and preserves transparent index 0.
RGB555 conversion truncates each source channel to five bits. For owned bank8,
Scary Face uses its PNG palette and fog uses the source JASC weather palette.
Other palette banks have explicitly listed controlled colors; they are never
silently replaced by the overlay palette. The second target is controlled
RGB555 `(31,5,27)`.

Viewport hashes serialize 240x160 pixels row-major: `bank_index_sha256` hashes
2 bytes per pixel `(bank,index)`, and `rgb555_sha256` hashes unsigned little-endian
16-bit colors, substituting the controlled second target for transparent pixels.
Blend vectors apply GBA five-bit arithmetic: clamp each coefficient to 16,
sum channel products, shift right 4, saturate each resulting channel at 31.
Transparent pixels preserve the second target. This is an independent hardware
arithmetic oracle, not an extracted software renderer.

Scary Face map banks 0/1 have nontransparent entries below the normal 160px
viewport, beginning at entry 670. Full-map and scroll probes deliberately keep
them visible. Sprite samples are frame-zero source indices/colors only; Glare
priority, OAM sorting, and full scene composition are outside these material rows.

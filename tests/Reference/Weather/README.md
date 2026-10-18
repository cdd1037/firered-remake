# Bounded weather animation source oracles

Pinned input: pret/pokefirered commit `037335f4c725d7c9aecdac87066f2002b4bd7e14`. These authored generators extract selected C functions/tables and compile only the extracted closure plus explicit platform stubs. They do not invoke upstream build scripts, product importers, runtime code, managed builds, or canonical generation. `pinned-inputs.json` fixes every consumed source and image/map hash. JSON is compact and deterministic; it contains no absolute paths, host compiler versions, wall-clock times, or native function addresses.

## Fixture integration names

- `bg-vectors.json` → `Fixtures/weather-bg-source-vectors.json`: `firered-sandstorm-bg-source-v1`, 32 cases / 2,640 register/task rows.
- `engine-vectors.json` → `Fixtures/weather-engine-source-vectors.json`: `firered-weather-sprite-engine-source-v1`, nine cases / 1,759 phase/sprite rows.
- `motion-vectors.json` → `Fixtures/weather-motion-source-vectors.json`: `firered-weather-motion-palette-abi-source-v1`, 56 crescent cases / 2,264 rows; 32 heat target cases / 4,160 rows; 32 palette cases / 248 rows; 154 native OAM ABI cases.
- `material-vectors.json` → `Fixtures/weather-material-source-vectors.json`: `firered-weather-material-source-v1`, 13 BG samples, ten BG render hashes, 40 source sprite-frame/flip variants, 152 independent source-fragment render hashes. Each fragment case retains the full 240×160 RGB555 hash and `rgb555FieldSha256` for the first 112 rows (240×112), matching the admitted battle field.
- `pan-vectors.json` → `Fixtures/weather-pan-source-vectors.json`: `firered-panse-adjustnone-source-v1`, seven cases / 335 sound-task rows.
- `schedule-vectors.json` → `Fixtures/weather-schedule-source-vectors.json`: `firered-weather-script-schedule-source-v1`, eight authored-root/actor cases / 1,032 scheduling rows, exact creation/destruction arrays and peak allocated/source-visible counts. The source wait callback makes delay N advance to the next instruction N+2 frames later. Graphics load contributes three frames. Frame 0 is the first RunAnimScriptCommand callback.

`source_extract.py`, the six `generate_*.py` files, four `*-*.c.in` templates, `pinned-inputs.json`, `validate.py`, and `check_vectors.py` are authored maintenance inputs. Generated `.c` files preserve the exact selected bodies/tables plus harness glue for inspection; executables are disposable, not repository deliverables. `verification.json` records structural assertions and fixture hashes.

## Regeneration

Run in an external copy of this directory. Inputs may be the pinned raw reference plus a separate pinned supplement directory containing missing PNG/map inputs.

```
python -B generate_bg.py REFERENCE
python -B generate_engine.py REFERENCE
python -B generate_motion.py REFERENCE
python -B generate_material.py REFERENCE SUPPLEMENT
python -B generate_pan.py REFERENCE
python -B generate_schedule.py REFERENCE
python -B validate.py
```

Each C generator independently compiles `cc -std=c11 -O0 -fwrapv` and `-O2 -fwrapv`, runs both, and rejects any observation mismatch. Palette bitfield access additionally uses `-fno-strict-aliasing`, matching the explicitly modeled halfword color representation. Ordinary managed tests only read reviewed fixtures; they must never generate or overwrite expected results.

## Exact timing and ownership

- Sand BG initialization is frame zero. Fade-in ends28, hold ends129, fade-out ends157, map clear is158, register/scroll cleanup and task destruction is159. Unlike fog, source cases3 and4 are separate callbacks. X is unsigned16 wrapping ±6 and Y unsigned16 wrapping -1. Direction flips only when caller flag is nonzero and actor is opponent-side.
- Crescent invocation0 is immediate initialization. Seven script lifetimes from that initializer are39,44,35,39,45,32,35. Crossing the bound installs `DestroyAnimSprite`; actual destruction occurs the following callback. Opponent Heat Wave negates the caller's shared argument1 and sets horizontal object-flip bits. Sandstorm caller flag0 never reverses.
- Rain task initializer itself consumes callback1 and can allocate its first drop. `[0,3,120]` destroys on frame119. Every allocation attempt consumes exactly two source RNG reads, including a full64-cell scene and an injected failed allocation. `Random()` itself, source `CreateSprite`, source task insertion/run/destruction, and source `BattleMainCB2`/`AnimateSprites` execute in the native harness. BuildOamBuffer is an observation boundary, preceding RunTasks. Two-task equal-priority creation order and a changed priority are explicit probes. Raw drops never own global animation visual count.
- A rain drop installs its movement callback at birth+1. Source frame-animation end is seen after callback at birth+19; DestroySprite runs at birth+20 with data0=19. It moves only13 steps, final x2/y2=13/52. Initial and every source phase are retained.
- Sunny Day source `CreateSpriteAndAnimate` calls the initializer and affine/frame engine immediately. Initial affine scale is80/80. Shared linear translation advances60 times, reaching139/79, not an invented exact destination. Callback61 installs destroy and still runs affine animation; callback62 destroys without another affine tick. Last scale is202/202; rotation25088. The fixture contains exact inverse-scale/rotation inputs to `ObjAffineSet`; BIOS matrix output and affine pixel accuracy belong to the separate existing BIOS/affine oracle. No replacement sine algorithm is claimed here.
- Source palette closures include battle palette mask unpacking, visibility selection, StartBlendAnimSpriteColor's eager first callback, per-palette BlendPalette, and background/particle blend wrappers. Sun fade delay1 has13 later callbacks; Rain delay2 has14; Heat's6→6 red blend destroys immediately.
- Pan adjustnone preserves signed8 start/target/increment as written, with no actor-side adjustment. Sunny Day's -64→63,+1,wait0 sweep destroys at callback127. Macro extraction is deliberately bounded to that source block: later sound commands reuse `tFramesToWait` with a different data index.

## Pixels and ABI

The source Makefile's PNG→gbapal rule and absence of special overrides select indexed PNG palettes. Sand BG explicitly loads Flying Dirt's palette; even where its colors match the background PNG, that source ownership is retained. Tile/map indices, flip bits, palettebank8 and pixel-zero transparency are independent of product converters. Fragment hashes consume native `AddSubspritesToOamBuffer` outputs, then sample source PNG tile indices with 1D OBJ addressing, object flips,9-bit X/8-bit Y wrap and transparent index0. Both source/default capacity64 and hardware128 edge probes are included; null-table fallback and tile-number1024 wrap are explicit. Hashes are precomposition source-layer pixels; the fixture does not pretend that a set mosaic bit has a nonunit mosaic size or that an OBJ blend bit alone specifies a second layer.

## Capacity proof and limit

Pinned `sprite.c` has only two `gOamLimit` assignments: zero-initialized storage and `ResetSpriteData` setting 64. There is no battle-specific 128 assignment in the pinned local source. The initializer-separated motion harness gives destruction invocations 39, 44, 35, 39, 45, 32, 35. The extracted script scheduling harness proves the actual authored delays: `Cmd_delay` installs `WaitAnimFrameCount`; N decrements, one callback handoff and the next script invocation produce N+2-frame instruction spacing. With frame 0 at the first script callback, Sandstorm births are [21,33,45,57,69,81,93] and destruction frames [59,76,79,95,113,112,127]. Heat Wave births are [23,35,47,59,71,83,95] and destruction frames [61,78,81,97,115,114,129]. Creation-frame AnimateSprites is included. For every actor, peak allocated and source-visible crescent counts are both four: eight OAM pieces. Stable doubles field uses four battlers, two player healthboxes of four OAM each (two body sprites plus two healthbar fragments), and two opponent healthboxes of five OAM each (two body sprites plus three healthbar fragments): 22 base OAM. Weather therefore needs at most 30, below source 64. Sources: `battle_interface.c` CreateBattlerHealthboxSprites and the player/opponent healthbar tables. Runtime validation conservatively permits all seven allocations alive: 22 + 2×7 + retained fragments must remain ≤64. This bounded stable-field proof excludes arbitrary concurrent/debug sprite injection and transient party-summary bars; hardware 128 is not substituted for source 64.

## Explicit boundaries

Controlled battler geometry/visibility and palette allocations are harness inputs. CreateSpriteAt models the initialization fields consumed by this closure; storage allocation and tile/palette loaders are not rerun. Callback pointers stored in source data6/7 are represented by stable token1 rather than process addresses. DestroySprite retains fields after clearing inUse for observation. These observations establish extracted callback/register/ABI/pixel behavior, not full-game playthrough, audible output, or native Godot/UI verification.

## Managed callback-vector coverage

`WeatherSourceOracleTests.cs` provides nine test methods and 11,424 row/probe comparisons: sand registers 1,280; crescent initializers/callbacks 2,208; heat offsets 4,160; rain task/sprite phases 1,696; sunlight motion/affine inputs 252; sun/rain palettes 232; immediate heat palette writes eight; all native palette helper rows 248; and pan sweeps 1,340. Raw helper palette slots 0/3 are tested as helper probes, not claimed to be available in the managed animation pool. The test file is staged for the owning integrator; no managed build was run by this source-oracle worker.

The scheduling correction is test/documentation-only: the managed wait callback already matches source. The earlier peak-five estimate incorrectly treated delay 10 as ten frames; source scheduling establishes twelve. Its command fixtures preserve exact source root ordering, with unrelated task/audio bodies explicitly stubbed and their state covered separately. The source-visible count means the invisible flag is unset, not that opaque pixels intersect the screen; `fieldIntersectCount` is separately identified as a geometric diagnostic.

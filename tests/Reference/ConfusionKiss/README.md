# Confuse Ray, Lovely Kiss, Sweet Kiss and Foresight source motion

## Local evidence profiles

Generated comparison JSON is local, ignored data, not a Git deliverable. Git keeps the native extraction code, explicit semantic case selection and managed comparisons. Tests never compile C or regenerate expectations implicitly. Missing input is an error with its preparation command.

- Default `representative`: 32 sprite + 8 task + 4 shared-heart + 3 integrated cases.
- Opt-in `full`: 149 sprite + 35 task + 18 shared-heart + 10 integrated cases.
- Selection: Every sprite recipe and callback; side/layout/ally and alternate geometry; RNG states 0/1/0xffffffff/0x80000000; pan -64/0/63, delayed/reversed pan, fade delays -2/1/3 and full shared-RNG/integrated phase ordering, including the double right-side opponent bounce that reaches plain PlaySE.

Every frame and field of each selected case is retained. Full runs use the same unmodified original-C extraction and O0/O2 parity checks. Case selection is explicit in `generate_motion.py`; it is not every-Nth sampling. The JSON root `profile` identifies which cardinality/coverage guards the same managed comparator enforces.

Prepare the normal local input from the repository root:

```sh
python tests/Reference/ConfusionKiss/generate_motion.py content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/confusion-kiss-motion-source-vectors.json
```

For a fresh external full-corpus directory:

```sh
python tests/Reference/ConfusionKiss/generate_motion.py content/raw/pokefirered/037335f4 --full --output /tmp/firered-full/confusion-kiss-motion-source-vectors.json
FIRERED_SOURCE_VECTOR_DIR=/tmp/firered-full dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --filter FullyQualifiedName~ConfusionKissAnimationTests
```

Prepare any companion inputs used by that class in the same override directory before running it. An override never silently falls back to local fixtures. Full generation is explicit maintenance work, not ordinary-test setup.


This bounded portable-C oracle extracts callbacks and shared frame, affine, translation, RNG, panning and palette-fade functions from pinned FireRed `037335f4c725d7c9aecdac87066f2002b4bd7e14`. It does not import or execute upstream build scripts. Every input is hash-pinned and every extracted original function has a source span and original-body hash in the fixture.

Regenerate from the repository root after integration:

```sh
python tests/Reference/ConfusionKiss/generate_motion.py content/raw/pokefirered/037335f4 --build-dir /tmp/confusion-kiss-reference --output tests/Game.Presentation.Tests/Fixtures/confusion-kiss-motion-source-vectors.json
```

For an external staged directory, supply `--reference-helper /path/to/FireRed-main/tests/Reference/reference_c.py`. The helper is the existing authored portable compiler entry point, not upstream code. Without `--build-dir`, all temporary C and executable files go to a fresh system-temporary directory; the verification report is also written there. `--output` selects the maintained numerical fixture, and defaults to `tests/Game.Presentation.Tests/Fixtures/confusion-kiss-motion-source-vectors.json`. No generated C, executable, or verification report is written beside the generator. Compilation uses C11, `-fwrapv`, `-fno-strict-aliasing`, and both O0 and O2. Each case must produce byte-identical output at both optimization levels. Ordinary managed tests only read the local ignored fixture.

The fixture includes 212 configurations and 16,673 rows:

- `cases`: 149 sprite configurations, 11,661 rows. Kind IDs 0–6 are Confuse Ray bounce/spiral, Devil, PinkHeart, Angel, RedHeartBurst, and Foresight magnifying glass.
- `taskCases`: 35 configurations, 3,234 rows. Kind IDs 7–8 are custom-pan adjustment and tagged-palette blend cycling.
- `groupCases`: 18 four-heart configurations, 1,008 rows. These create PinkHearts in the exact Lovely Kiss script order and use original `AnimateSprites` slot order to expose consecutive RNG draws.
- `integratedCases`: 10 configurations, 770 rows. These run the bounce sprite, global palette fade, then the source custom-pan task each frame, matching `BattleMainCB2` (`battle_main.c:1447–1453`) and exposing the actual pan value consumed by each `PlaySE12WithPanning` call and preserving separate `PlaySE` events. `customPanBeforeSprites` is the consumed value; `customPan` is the post-task observed value. The immediate task initializer still runs before sprite construction.

Sprite frame 0 follows original `CreateSpriteAndAnimate`: initializer callback, then frame/affine engine. Subsequent rows follow original `AnimateSprites`. Task frame 0 follows the original immediate initializer; subsequent task rows update the original global palette fade before invoking the task callback. The fixture records all terminal rows.

Creation coordinates are selected target `X_2,Y_PIC_OFFSET` from original `Cmd_createsprite`, including callbacks which retain those inputs. Subpriority is target priority minus the source script offset (2, or 3 for hearts); Devil then performs its original target-minus-1 override. All four battle positions, singles/doubles, selected allies and alternate opposing slots are covered. Source dimensions are explicit and include two width/height sets.

Foresight's species/personality-to-coordinate-table lookup is the single adapted arithmetic helper boundary: explicit dimensions replace that lookup, while its original coordinate-attribute switch is retained verbatim. All seven sprite callback families and the two task families remain verbatim, as do shared frame/affine/motion/RNG/pan/palette functions. GPU/audio invocations are instrumented at their external-call boundaries. Sprite allocation and BIOS matrix writes are bounded stubs, documented in fixture semantics.

`hFlip/vFlip` are raw Sprite base flags. The effective non-affine rendered flips are original OAM `matrixNum` bits 3 and 4. Foresight writes `matrixNum = ST_OAM_HFLIP` directly on opponent-side targets; its dummy frame animation preserves that direct bit. Affine `matrixNum` is a matrix index, not a flip field.

PinkHeart consumes exactly one original LCG draw at frame 24 and uses `Random() % 180`. The four-heart case consumes four draws in ascending sprite-slot order. Seed zero is preserved, rather than replaced. Draw events retain the full unsigned prior/new 32-bit states.

The custom-pan task changes its global variable without emitting audio or an audio-engine pan command. Bounce step 1 invokes `PlaySE12WithPanning` with that global value; step 2 invokes `PlaySE` with no new pan. These event kinds are intentionally distinct.

The palette-tag lookup returns slot 4 for `ANIM_TAG_YELLOW_BALL` (10013), corresponding to palette bank 20 and mask `1 << 20`. Original palette arithmetic runs against deterministic 15-bit input colors; fade event timing, bank colors, and terminal restoration are retained. The generator selects the lexical task macro scope, since other source sections reuse names such as `tDelay` with different meanings.

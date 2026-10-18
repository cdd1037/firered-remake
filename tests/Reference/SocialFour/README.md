# Taunt, Follow Me, Attract and Torment source oracle

This bounded portable-C oracle extracts pinned FireRed `037335f4c725d7c9aecdac87066f2002b4bd7e14` callbacks, frame/affine engines, task-affine engine, linear motion, sine, panning, sprite allocation/reset, and script sprite creation. It never imports or executes upstream build scripts. Every input is hash-pinned; every original function and data array/struct has an original-source span and SHA-256 recorded in the fixture.

## Regenerate

From the repository root after integration:

```sh
python tests/Reference/SocialFour/generate_motion.py content/raw/pokefirered/037335f4 \
  --build-dir /tmp/social-four-reference \
  --output tests/Game.Presentation.Tests/Fixtures/social-four-source-vectors.json
```

For an external stage, add `--reference-helper /path/to/FireRed-main/tests/Reference/reference_c.py`. This is the authored portable compiler helper, not an upstream script. Without `--build-dir`, generated C, executables and the verification report go to a fresh system-temporary directory. No generated executable, C file or report is written beside the generator. `--output` selects the local numerical fixture. Generated comparison data is ignored and is not source-controlled; only the generator, pinned input identities, tests and explicit representative selection belong in Git. Both C11 O0 and O2 builds use `-fwrapv -fno-strict-aliasing`; every case must produce byte-identical output.

The full on-demand profile has 174 configurations and 20,158 rows:

- `cases`: 126 configurations / 14,530 rows for ThoughtBubble, TauntFinger, FollowMeFinger, RedHeartProjectile and RedHeartRising.
- `taskCases`: 26 configurations / 4,223 rows for HeartsBackground and TormentAttacker, including capacity 0/1/3/5, live preexisting matching-template sprites and maximum-six adoption.
- `directCases`: 18 configurations / 317 rows for all four bubble/finger frame animations with both raw base h-flips, a raw base v-flip, and both Follow Me affine programs.
- `integratedCases`: four configurations / 1,088 rows for the eight source rising-heart recipes with HeartsBackground, in native sprite/fade/task order.

## Inputs and observation

The first row has an `inputs` object emitted by C: widths, heights, y-offsets, elevations, subpriorities and all four native coordinate getters. Case metadata is copied from these observed values, preventing a geometry/metadata mismatch. Species/personality lookup is replaced with explicit dimensions/y-offsets and zero elevation. Original `GetBattlerSpriteCoord`, `GetBattlerSpriteCoord2`, `GetBattlerSpriteFinal_Y`, `GetBattlerSpriteCoordAttr` arithmetic and `GetBattlerSpriteSubpriority` execute. This includes source u8 coordinate conversion, player +8, cap104, width/height head positioning, Follow Me top clamp10, and priorities30/40/20/50.

Original `Cmd_createsprite` reads bounded bytecode records through a portable template-token reader. It preserves shared arguments, target creation coordinates, signed subpriority arithmetic and clamp3, callback/animation initialization order, and visual-count increment. Sprite frame0 is after its initializer and animation engine. Task frame0 is after its immediate initializer. Subsequent task/integrated frames run original `AnimateSprites`, the inactive palette-fade boundary, then the task callback, matching `BattleMainCB2`. Palette fading itself is inactive in this slice.

Sprite rows record raw base `hFlip/vFlip`, raw `oamMatrixNum`, frame/affine command positions, delay/loop state, geometry, callback, data words and arguments. For nonaffine sprites, rendered flips come from matrixNum bits3/4. For affine sprites, matrixNum is a matrix index. A terminal `destroyedSnapshot` retains the pre-reset observer state; the real source slot has already been reset through original `DestroySprite` and `ResetSprite`.

## Portable representation boundaries

- Native callback pointer packing into two 16-bit data words is represented by explicit deterministic handles. Switching to a stored callback never invokes it eagerly. Handles and their names are in fixture semantics.
- Task affine pointers use deterministic handle1. Its native byte arithmetic has one explicit pointer cast to support typed host byte pointers instead of GNU void-pointer arithmetic.
- `AffineAnimCmd` has an explicit host alignment member and `sizeof == 8` assertion to preserve the ARM command stride used by the native task-affine engine.
- Native `JumpToTopOfAffineAnimLoop` reads command[-1] before checking index0. Follow Me's looping array is provided through an identical command copy with one inert END prefix. The original loop function is unchanged. At index0 either probe result reaches the same final decrement, so the sentinel affects no source state while preventing an out-of-bounds host read. The original array and its hash remain present.
- Source `CreateSprite` scans slots unchanged. The allocation boundary initializes available tag-backed sprites, with deterministic free matrix indices. Graphics resources are available; non-sheet tile allocation/free paths are outside this bounded harness.
- BIOS `ObjAffineSet` is instrumented. Its zero-rotation diagonal values are exact, so original Torment scale/foot-offset equations execute. Nonzero rotation coefficients are deliberately not modeled; Follow Me scale/rotation state and timing are source-engine values, not BIOS matrix verification.
- GPU, graphics loading and audio calls are observed at external-call boundaries. No assets are decompressed or executed.

## Source ownership and timing

Torment increments the visual count for the task only. Every run attempts six raw sprite allocations and emits six sounds, at frames1/26/51/66/81/96, even if all slots are occupied. Children receive `SpriteCallbackDummy` without running the ThoughtBubble initializer. At frame119 the task scans all global sprite slots ascending by template pointer, adopts at most six, writes its task/counter references and starts frame anim2. Prior rendered tile/flip remain unchanged at119; the new animation begins on the sprite pass at120. Adopted children finish at129 and use `DestroySprite`, not `DestroyAnimSprite`. With no child to adopt the task ends at120.

Live supplied matching-template slots participate in that scan before later slots, irrespective of producer identity. The full-capacity plus two-preexisting case adopts those two and the first four new children, leaving the last two raw children alive; the six-preexisting case leaves all six newly created children alive. Dead slots are truly reset and cannot leave artificial stale template matches. The fixture is a bounded observer of one task and supplied live raw children; it does not claim to simulate a complete concurrent battle.

HeartsBackground uses source BG1 palette8/tilesOffset512, priority3, screen size0, charbase1 and scroll0. Fade-in ends64, hold ends205, fade-out ends269, BG1 clears270, and blend/priority reset plus task destruction occurs271. The BG2 sentinel state never changes. Follow Me ends180; TauntFinger ends46; ThoughtBubble with source hold45 ends64; projectile ends96.

## Additional validation

All 174 configurations were checked under AddressSanitizer and UndefinedBehaviorSanitizer after the explicit affine-array prefix was added. Leak checking was disabled because the execution environment uses ptrace; the harness performs no dynamic allocation.

A separate development-only Python transcription review checked the staged bounded C# arithmetic/state scheduling against all 20,158 C rows. It covered sprite motion, direct frame/affine tables, Torment geometry/ownership/restarts, background phases and eight-heart integration, and recorded the reviewed C# source hashes. That external review is not a managed-runtime test or a replacement for the repository's managed fixture coverage; its script and reports are not reference publication inputs.

## Ordinary versus full source comparison

The default generator runs 48 explicit branch representatives and writes plain local JSON: 18 sprite cases / 1,635 rows, 10 task cases / 1,433 rows, all 18 direct table cases / 317 rows, and two integrated cases / 544 rows. All 3,929 frames are retained. It never samples every Nth frame or advances a callback only at selected checkpoints.

Representative choices are literal case identities in `generate_motion.py`, with their required identities independently checked by `SocialFourAnimationTests`:

- Thought bubble: both selected sides, source 45-frame hold, one-frame hold, both two-frame short-hold selectors, and alternate dimensions.
- Taunt: both sides, both selectors, both double-battle partner positions, and alternate geometry.
- Follow Me: both sides and both top-clamp selectors; both original affine programs remain fully covered independently.
- Heart projectile: both sides plus a same-side partner destination. Rising hearts retain clamped and unclamped subpriorities; both integrated cases contain all eight authored recipes and the complete BG1 lifecycle.
- Torment: ordinary and alternate foot geometry, capacity 0/1/5/full, foreign-only adoption, five total adopted children, the six-child scan cap with two foreign children, and six foreign children leaving newly created raw children unadopted. Every sound frame and every teardown frame is retained.
- All four bubble/finger frame selectors with both base H flips, base V flip, and both Follow Me affine tables are kept. These short distinct table tests are not replaced by hashes.

Default output removes unasserted raw diagnostic fields and duplicate row-zero input records; the observed C input metadata and source provenance remain once per fixture. Retained fields are the actual test expectations: positions, lifetime, rendered tiles/flags/priorities, shared arguments, relevant frame/affine player state, task phase/counters, battler matrix/offsets, blend/BG1 fields, and observed Torment sound/allocation events. The test also independently checks ownership, RNG absence, extended argument preservation, counters, cleanup and cancellation. Callback names, unused data words and hardware instrumentation are not claimed to be equal to managed internals.

To re-run every original combination, retain full raw diagnostics only in a temporary directory, then point the same managed comparison tests at it:

```sh
python tests/Reference/SocialFour/generate_motion.py content/raw/pokefirered/037335f4 \
  --full --build-dir /tmp/social-four-reference-full \
  --output /tmp/move-source-full/social-four-source-vectors.json
FIRERED_SOURCE_VECTOR_DIR=/tmp/move-source-full dotnet test \
  tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj \
  --filter FullyQualifiedName~SocialFourAnimationTests
```

Without `--output`, `--full` writes beside its temporary compiler artifacts, never into the ordinary fixture directory. Full generation requires byte-identical O0/O2 output for all 174 configurations and preserves every original field and frame. Full-profile tests require the original group counts and run the same numerical assertions; hashes in generation diagnostics identify inputs/artifacts and never replace comparisons. Ordinary tests read only the local representative data, require exactly the reviewed case identities and contiguous nonempty frames, and do not invoke Python or C. Missing local data is an error with the generation command, not a skip or an implicit compiler invocation.

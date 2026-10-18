# Present extracted-source oracle

Explicit generation from the repository root, after the execution owner reviews
this harness:

```
python tests/Reference/Present/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/present-source-vectors.json
```

The harness pins local pret/pokefirered revision
037335f4c725d7c9aecdac87066f2002b4bd7e14. Expected trajectories come from original
C bodies and original frame/affine tables, independently of managed runtime code.
No upstream scripts run. Output is refused if the fixture already exists. Normal
managed tests must only read the fixture, never regenerate it.

## Scope and boundaries

Extracted unchanged: AnimPresent, InitItemBagData, MoveAlongLinearPath,
AnimItemSteal_Step1/2, AnimPresentHealParticle, AnimTask_IsHealingMove, both position
initializers, SetAnimSpriteInitialXOffset, battler side/position helpers, source
sine and coordinate tables, native creation/frame/affine drivers, destruction
wrappers and all animation/template/OAM declarations used by these primitives.
The bag and all nine authored heal argument lists are parsed from the pinned
Move_PRESENT / PresentHeal scripts, including the duplicate -8 heal birth.

Authored platform boundaries: allocation, picture-offset fixture, lookup into the
original coordinate table, task resource destruction, frame-image copying and
BIOS affine output. Unsupported anchors and image-copy paths abort. BIOS matrix
output is intentionally stubbed; original xScale/yScale/rotation and affine
program state are observed. The source initializer requests raw Y, so the chosen
nonzero picture offset affects the creation origin only, then is discarded by
the original callback. The returned fixture records yOffset so managed tests can
also supply a nonzero body offset and catch accidental picture-relative births.
Native memory is retained after destruction for terminal inspection.

The generator compiles the same extracted C with O0 and O2 and requires identical
output for every case. This is native parity evidence only. Managed comparison,
root playback, resource integration, cancellation, UI and audio are separate gates.

## Corpus

168 cases, each emitted through death or immediate task destruction:

- Six layouts: single 0→1 and 1→0; double 0→3 and 1→2 (nonpartners), 2→0 and
  3→1 (partners). These cover every actor position and both side directions.
- Eighteen bags: original argument list and two nonzero signed birth offsets in
  each layout. All 60 movement callbacks, settle delay, affine restart and death
  are observed, including the final retained packed interpolation register.
- Ninety-six heal particles: all nine authored births per layout, zero/positive/
  negative and signed16-extreme velocities, and signed16-extreme position offsets.
- Fifty-four immediate predicates: each layout uses nine signed32 damage values,
  including INT32_MIN, INT32_MAX, ±65536, -1, 0 and 1. All eight native argument
  registers are captured; only argument7 may change.

Generator validation rejects empty/noncontiguous/undrained cases, missing settle
restart, missing heal END, an incomplete corpus and O0/O2 disagreement. The total
row count is reported only after actual execution; no capture has been claimed
by merely preparing this generator.

## Fixture / managed comparator contract

Schema: firered-present-source-v1. Kind0 is the bag, kind1 the heal particle,
kind2 the signed damage predicate. Case arguments contain all eight native
registers, with sentinel values in unused positions. Native task data has16 words;
sprite data has8. The managed owner retains16 argument words: compare the first8,
and assert the unused final8 are unchanged from their seeded values.

Frame0 is native CreateSpriteAndAnimate: creation callback followed by
AnimateSprite. A primitive managed constructor must likewise initialize and
advance its frame/affine programs once. Each later native row is one original
AnimateSprites pass: callback first, and frame/affine advance only while alive.
Do not advance either program after the death callback. The predicate emits only
frame0, after it writes argument7 and destroys its visual task.

Every row contains:

- frame: contiguous zero-based tick
- alive: sprite/task ownership still active
- visualCount:1 before destruction,0 afterward
- args:8 signed16 animation argument registers
- data:8 signed16 sprite registers, or16 zero-initialized task registers
- callback:0 travel,1 settle,2 heal,3 immediate predicate
- sprite: null for predicate; otherwise the following14 integers:
  x, y, x2, y2, tile, visible, oamPriority, subpriority, animEnded,
  animDelayCounter, affineXScale, affineYScale, affineRotation, affineAnimEnded
- affine: null for predicate; otherwise affine animation number, command index,
  delay counter, loop counter, affineAnimBeginning
- frameState: null for predicate; otherwise frame animation number, command
  index, loop counter, animBeginning

The same column names and callback mapping are embedded in the fixture header.
Heal particles have no affine owner; their raw affine inspection slot remains
zero, so compare the affine fields only for kind0. The frame and data arrays are
still exact for kind1. Preserve terminal x/y/y2/data/frame state for comparison,
even though a destroyed sprite is no longer drawn.

Recommended focused test assertions, in addition to the full row comparison:

1. Assert schema,168 nonempty cases, six layouts and both partner/nonpartner
   travel paths before looping; do not let a missing fixture or empty suite pass.
2. At bag birth compare raw attacker coordinates with mirrored signed x offset,
   and raw Y plus argument1. data[5]/data[6] are packed source bytes; data[7]
   begins at60<<8. Partner data[3] is1; nonpartner data[3] is3.
3. Compare the full trajectory and both affine programs at every tick. Source
   linear motion completes after evaluating currentTime59, so it does not snap
   x/y to the endpoint. Final movement leaves packed currentTime59 unchanged.
4. Capture the exact settle callback and affine-program1 restart from native
   rows, then require death and visual-count drainage. Do not substitute guessed
   frame-duration arithmetic for the frame and affine interpreter.
5. Heal birth immediately increments data[0] and writes y2=velocity. Frame END
   is observed before the later callback destroys the sprite; compare the death
   callback's final y2 as well. Test both positive and negative velocity wrapping.
6. Task expected result is gAnimMoveDmg<=0. Use the captured s32 values, retain
   all other native arguments and assert there is no remaining task owner.

Full Move_PRESENT script scheduling, damage explosion particles and the shared
HealingEffect2 closure are deliberately outside this primitive oracle. Their
existing shared implementations and full-root tests remain separate authorities.

## Executed native evidence

The prepared generator was explicitly run against the canonical local source.
O0/O2 outputs matched for168 cases and3,798 rows:2,016 bag rows,1,728 heal
rows and54 immediate predicate rows. This is actual native execution evidence;
managed comparison and full-root tests have not been executed by this worker.
The generated vectors, emitted C and binaries remain external to the repository.

Executed command:

```
PYTHONDONTWRITEBYTECODE=1 python /workspace/shared/present217-stage/oracle/tests/Reference/Present/generate.py --root /workspace/scratch/18c02ed4ed0d/FireRed-main --output /workspace/shared/present217-stage/oracle-evidence/vectors/present-source-vectors.json --build-dir /workspace/shared/present217-stage/oracle-evidence/native
```

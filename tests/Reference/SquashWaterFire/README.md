# Eruption / Water Spout extracted-source oracle

This explicit generator extracts original C from locally pinned FireRed revision
`037335f4c725d7c9aecdac87066f2002b4bd7e14`. It executes no upstream scripts and reads
no managed product code or product-generated expected values. Every selected case
retains initialization and every following frame through root and child closure.

```
python tests/Reference/SquashWaterFire/generate.py --root . --output /external/squash-water-fire-source-vectors.json --build-dir /external/squash-native
python tests/Reference/SquashWaterFire/generate.py --root . --full --output /external/full/squash-water-fire-source-vectors.json --build-dir /external/squash-native-full
```

`SquashWaterFireNativeTests` reads `FIRERED_SOURCE_VECTOR_DIR` when set; a missing
file in that directory fails rather than falling back to the smaller fixture.
Ordinary managed tests never compile C, download sources, or regenerate expected
output. Generated C, binaries and numerical traces remain local/external and are
not tracked. The generator refuses to overwrite an existing output file.

## Independent implementation boundary

The extracted bodies include the Eruption launch task, all seven launch-rock
trajectories, falling rocks, both Water Spout tasks, droplets, splash hits, the strict
HP-quarter helper, shared squash/foot-anchor/rot-scale helpers, and the native
sprite-animation and affine-animation engines. The source's dummy animation
commands, speed array, sine table, hit affine programs and templates are extracted.
The six authored falling-rock operand lists are read from the pinned move script.

The source `CreateSprite` chooses actual lowest-free raw sprite slots, while
`InitSpriteAffineAnim`, `AllocOamMatrix`, `FreeOamMatrix`, and affine state reset are
extracted. The small `CreateSpriteAt` resource boundary initializes the chosen cell
with zero-relative sheet/palette bases. It does not choose an allocation outcome.
Actual battler cells and matrices are reserved. All corpus allocations succeed;
capacity-failure behavior is not claimed by this oracle.

Explicit battle coordinates, picture Y offsets/elevations, visibility, party HP and
maximum HP replace species/party lookup boundaries. The BIOS zero-angle matrix
boundary is exact. The only callback-engine body instrumentation records the
current sprite slot around dispatch, without changing native callback data or
scheduling. Inactive sprite memory is retained solely for the final falling-rock
observation; reallocation resets it and inactive sprites are never dispatched.
The host union has an alignment member plus a static assertion preserving the
source ARM eight-byte affine-command stride.

`CreateTask`, `InsertTask`, `DestroyTask`, `RunTasks`, and task initialization are
extracted. The isolated primitive task uses priority5, and rain inserts the
original priority 80 target/partner shake tasks while the parent remains active.
All callbacks run in source sprite-slot order followed by source linked task
priority order. This is not a proof for arbitrary tasks that destroy themselves
before inserting another task into a reused task cell.

## Native arithmetic and scheduling proofs

- Battler preparation selects affine DOUBLE. The 64x64 battler's native
  center-to-corner Y is therefore -64 when Eruption computes its launch origin.
- `ISO_RANDOMIZE2(task->data[7])` promotes signed16 data to signed32. Compilation
  explicitly uses `-fwrapv`; the expression and signed remainder are unmodified.
  Player-side rain X starts 185,116,97,124; opponent-side starts 65,-34,35,-44.
- Water Spout HP tiers use strict `<` comparisons after integer `maxHp/4`.
  Maximum HP 1,2,3 therefore returns tier 3 for every admitted HP, including zero.
- Alternate launch orbs execute the original immediate callback before their
  parent's count increment; all birth-frame registers and coordinates are kept.
- For double-battle actor0/tier3/power150, frame 8 creates the first splash in slot 6
  from rain slot 4. Because slot 6 is later in the scan, its blink delay is already1
  and its native scale 128 yields matrix 512 in the same frame.
- At frame 18 a splash is born in slot 4 from rain slot 7. It has delay 0, scale 256,
  and identity matrix on its birth frame, then starts its affine program when the
  next sprite scan reaches it.
- At frame 8 the target and partner shake tasks are inserted at priority 80. Source
  `RunTasks` reaches both later-priority tasks that same frame; each has delay 1,
  target bindings 1 and 3, max-time 12, and amplitude 18. At frame 9 both offsets are 9.
- That rain root ends at frame 88; its shake children remain active through 168.
  The trace and managed comparator continue after root destruction until those
  children finish. Single battles and hidden partners exercise absent-child paths.

## Coverage and verified native result

The representative corpus contains 229 cases and 21,357 rows. It includes all six
single/double actor layouts; all four tiers for launch and rain; HP values on both
sides of strict quarter boundaries; `maxHp<4`, non-divisible 101, and 65535; every
source falling-rock recipe; delay 0/1, landing equal/under/over-eight boundaries and
all tile variants; shifted live bases, picture offsets/elevations, hidden partners,
move-power quantization, raw slot reuse, every birth/death and both splash scan
handoffs. No birth disappears before the row observation.

The optional full actor-target corpus contains 349 cases and 33,423 rows. Both
corpora were compiled and executed at O0 and O2 with byte-identical JSON under
Debian GCC 14.2.0 using `cc -std=c11 -O0/-O2 -fwrapv -fno-strict-aliasing`.
Source-file hashes and exact extracted-function hashes are emitted in the fixture.
Managed frame comparison is a separate gate; native optimization parity alone
does not imply it has passed. Full authored-root playback and cancellation are
also separate tests rather than inferred from these isolated callback traces.

# Water Sport native callback reference

This generator extracts the five Water Sport task/particle callbacks from the
local pinned FireRed source, together with the original horizontal arc/linear
helpers, Random arithmetic, task scheduling, ascending sprite traversal and
lowest-free-slot allocation. It never executes reference-repository scripts and
does not use product output as expected data.

Explicit host boundaries are battle-coordinate lookup, FireRed's false contest
predicate, static dummy-orb frame animation and raw sprite allocation/destruction
storage. The original source traversal and callback bodies execute unchanged.
Frame 0 is task initialization; every subsequent frame runs AnimateSprites then
RunTasks. Task-created raw orbs do not get an eager callback in their birth frame.
At the first arc's completion, the source adds x2/y2 to the base position but
retains those offsets for that handoff frame; the second arc replaces them on
its next step.

Run explicitly from the project root:

    python tests/Reference/WaterSport/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/water-sport-source-vectors.json

The output must not exist. It is ignored generated evidence, not a source file.
The representative profile has 18 cases and 3,774 complete frames: all six battle
positions with seed 0 and 0xffffffff, picture offsets/elevation, one-free-cell
allocation pressure, and full saturation followed by an explicitly released
cell. Failure consumes neither RNG nor the owner's live-child count. The
saturated source task deliberately waits until a real droplet finishes; the
harness releases a cell at frame 35 rather than inventing a termination rule.

Both O0 and O2 outputs must agree byte-for-byte before JSON is emitted. To add
seeds 1 and 0x12345678 at every layout:

    python tests/Reference/WaterSport/generate.py --root . --full --output /path/to/full/water-sport-source-vectors.json

Set FIRERED_SOURCE_VECTOR_DIR to that external directory and run the same
WaterSportNativeTests comparator. A missing requested corpus fails without
fallback. Normal managed tests only read generated data; they never invoke C.
The comparator checks all sixteen task registers, all eight arc registers,
position/offsets, callback phase, each successful allocation identity, raw-slot
reuse, visual RNG state, task ownership and final drain at every frame.

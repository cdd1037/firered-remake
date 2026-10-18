# Memento reference vectors

The explicit generator extracts Memento's four public task callbacks, both steps,
DoMementoShadowEffect, source priority helper, and the native scanline setup/seed/
swap/stop functions from the pinned local FireRed source. It executes only the
reviewed authored C host; no upstream build scripts or product code are run.

```
python tests/Reference/Memento/generate.py content/raw/pokefirered/037335f4 --repo . --output tests/Game.Presentation.Tests/Fixtures/memento-source-vectors.json
```

Output must not already exist. The destination is ignored, and ordinary managed
tests fail with this command when missing. Never commit generated vectors, native
binaries, task traces, or reports. `--build-dir` may choose an external directory.
`reference_c.compile_c` selects installed Linux cc or Windows Visual Studio C.

Fifteen complete task cases cover both BG ranks, every single/double position,
visible and hidden partners, exact source picture height/offset/elevation, negative
rows, all target setup phases, alpha endpoints, WIN0 collapse/widen, and deferred
stop. Every one of 1,099 rows compares all task registers, both 960-word banks,
window/register values, and each available 161-sample raster. Five direct helper
cases cover zero-height both-bank rewrites, negative top rows, signed division,
source-buffer selection, and untouched retained tails. O0 and O2 must agree.

The host models immediate SetGpuReg transport for isolated callback vectors. The
runtime's root tests retain its existing explicit VBlank/queued-write model.
Neither source callback order nor these tests establish CPU/VCOUNT cycle timing
for a line-zero queued register flush. Signed shifts by eight are replaced only
by bounded multiplication for C portability; the original extracted hashes and
line ranges remain recorded. Negative right shifts follow the arithmetic shift
of the ARM target and the two supported host compilers.

Separate managed tests compose complete roots, cancel every phase, transfer a
shared scene to a later controller, retain detached snapshots, restore visible
OBJ priorities at Cmd_end, and check independent vertical-row/window pixels.

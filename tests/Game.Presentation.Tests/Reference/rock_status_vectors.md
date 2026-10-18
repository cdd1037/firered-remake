# Rock/status callback oracle

Pinned revision: `037335f4c725d7c9aecdac87066f2002b4bd7e14`.

Run `generate_rock_status_vectors.py --root <repository> --output-dir <new directory>`.
The generator extracts callbacks and frame arrays verbatim from cached pinned C,
then compiles the authored harness with `cc -std=c11 -O0 -fwrapv`. It never runs an
upstream script or the product runtime. It reuses the existing orbit/spray harness
for source sprite allocation, frame playback, task-affine platform adapters and
static single/double battle geometry. Source-file and extracted-span SHA-256
values are retained in the fixture. Two independent native runs were byte-identical.

The full profile has 580 configurations and 24,880 contiguous callback rows. These cover every
actor/target position in single and double battles (including same-side/equal
positions), every new callback, each rock frame, actual source argument shapes,
zero red-X lifetime, zero rock drop, signed16 coordinate extremes, endpoint wrap,
source fall/hold counters, deferred stored callback retirement, and the complete
81-callback GrowAndGrayscale lifetime. Tick -1 is synchronous creation. Ordinary
sprite/OAM state is captured before tasks; Grow also records post-task state.

Grow probes use present, visible battlers with source affine-normal idle state.
Its task changes matrix, double-size and object blend, but does not adjust the
foot offset. Original source grayscale conversion and restoration run against all
512 palette entries. The consumer compares the selected palette with the shared
product palette implementation and proves other source banks were untouched.
Source sprite ID is only an adapter lookup here; none of these callbacks performs
observable arithmetic on the raw battler sprite ID. Hidden participants are
conservatively rejected by the invoked runtime task until presence is represented.

Stored callback pointers use the maintained host-safe callback handle. BIOS affine
matrix and fixed geometry are platform adapters. Frame indices/delays, sprite
coordinates, flags, counters, task retirement and palette observations are source
outputs. The fixture is not a snapshot of the managed implementation.

## Local representative and full evidence

Generated comparison data is local and ignored by Git, including this family's
plain JSON. Git retains the generator, explicit case selection, and managed
numerical comparator. Missing data fails with the exact generation command;
normal tests never skip a missing corpus or implicitly invoke a C compiler.

Default generation writes `profile: representative`: 48 complete cases /
2,270 callback rows. `--full` writes `profile: full`: 580 admitted cases /
24,880 rows. Both use the same fixture filename and the same
managed assertions for every numeric observation. Selection is by whole source
branch/boundary cases; every tick from -1 through retirement is retained. The
managed test independently requires the explicit key set and profile-specific
case/row counts, in addition to its unchanged frame, state and lifetime assertions.
The small exhaustive 256-byte predicate tables, where present, remain exhaustive.

From the repository root, generate into a new directory and run the same comparator:

```sh
vectors=$(mktemp -d)
python tests/Game.Presentation.Tests/Reference/generate_rock_status_vectors.py --root . --output-dir "$vectors" --full
FIRERED_SOURCE_VECTOR_DIR="$vectors" dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --filter 'FullyQualifiedName~RockStatusSourceVectorTests'
```

Omit `--full` to generate representative evidence. To supply normal tests, review
and copy the resulting `rock_status_vectors.json` into
`tests/Game.Presentation.Tests/Fixtures/`. The output is not staged in Git.
Both generation profiles compile the unchanged extracted C under `-O0` and `-O2`
and require byte-identical numeric row streams before emitting JSON. Full mode
retains its native row stream for audit; the default does not write that large
full-corpus sidecar. Source file/span hashes remain provenance, never substitutes
for numerical comparison. Generated harness output and full corpora stay outside
Git. The corpus redesign itself was verified with native C and Python; managed
validation belongs to the integrating run, not to those native checks.

### Explicit representative selection

- Every configuration: all rock frames, zero drop/lifetime, blink thresholds and signed endpoints.
  `0:0:0:1:0`, `0:0:0:1:1`, `0:0:0:1:2`, `0:0:0:1:3`, `0:0:0:1:4`, `0:0:0:1:5`, `0:0:0:1:6`, `0:0:0:1:7`, `0:0:0:1:8`, `1:0:0:1:0`, `1:0:0:1:1`, `1:0:0:1:2`, `1:0:0:1:3`, `1:0:0:1:4`, `1:0:0:1:5`, `1:0:0:1:6`, `1:0:0:1:7`, `2:0:0:1:0`, `2:0:0:1:1`, `2:0:0:1:2`, `2:0:0:1:3`, `2:0:0:1:4`, `2:0:0:1:5`, `3:0:0:1:0`, `3:0:0:1:1`, `3:0:0:1:2`, `3:0:0:1:3`, `3:0:0:1:4`, `4:0:0:1:0`.
- Reverse side: mirrored coordinates and palette/affine restoration.
  `0:0:1:0:0`, `0:0:1:0:7`, `0:0:1:0:8`, `1:0:1:0:6`, `1:0:1:0:7`, `2:0:1:0:1`, `3:0:1:0:3`, `3:0:1:0:4`, `4:0:1:0:0`.
- Doubles rear coordinates, picture heights and target palette.
  `0:1:2:3:0`, `1:1:2:3:0`, `2:1:2:3:0`, `3:1:2:3:0`, `4:1:2:3:0`, `0:1:3:2:0`, `1:1:3:2:0`, `2:1:3:2:0`, `3:1:3:2:0`, `4:1:3:2:0`.

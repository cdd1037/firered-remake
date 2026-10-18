# Friendship/contact source vectors

`generate_friendship_contact_vectors.py` reuses the maintained orbital/spray C extraction scaffolding, adds pinned source callback bodies verbatim, and compiles an authored bounded harness with `cc -std=c11 -O0 -fwrapv`. No upstream scripts or managed implementation generate the expected numbers. Exact file hashes and new callback line spans are recorded in the fixture.

Full profile: 480 source cases, 17,420 callback rows and all 256 friendship bytes for both predicates. Cases span all actor/target positions in singles and doubles, both MovementWaves directions/participants, zero and repeated cycles, Lick offsets and visibility pulses, WeakFrustration signed offsets/accelerating descent, signed16 coordinate overflow in both Lick and WeakFrustration, the native StrongFrustration task-affine loop and cleanup, and both trace selectors with zero/bounded lifetimes, zero/multiple attempts, full capacity and a one-free-slot registry.

Tick -1 is source synchronous initialization. On each ordinary tick, AnimateSprites runs before the pre-task/OAM snapshot, then the task callback runs before the raw registry/state snapshot. Trace raw clones do not increase gAnimVisualTaskCount; task data5 owns their lifetime. A raw clone created after OAM has no same-frame rendered entry. The consumer compares native raw registry ownership after tasks and visible geometry at the next actual sprite phase. Trace base battler inputs are explicit unshifted Center(false) coordinates; ordinary animation-position callbacks retain the source's player-side animation offset through Center(true). Trace probes initialize all participating battlers as present, visible and nonaffine with identity matrices. The consumer compares matrix and affine fields only in that supported state. These vectors do not claim hidden-participant or affine-clone parity. Invoked hidden or affine participant rejection belongs to the separate whole-root validation tests.

Source outcomes include:
- Return level: 0 through 60, 1 at 61–91, 2 at 92–200, 3 at 201–255. The strict source comparisons leave 60 at initialized zero.
- Frustration level: 0 through 30, 1 at 31–100, 2 at 101–200, 3 at 201–255.
- MovementWaves one cycle retires at tick 32; two cycles at 65.
- Lick retires at tick 50; its ended-frame XOR produces one-tick visible pulses.
- WeakFrustration retires at tick 36; StrongFrustration task-affine restoration finishes at tick 45.
- Trace selector 0 writes OAM priority 2; selector 1 writes priority 1.

Generation used the local pinned reference cache, including the separately cached ghost source:

```
python3 tests/Game.Presentation.Tests/Reference/generate_friendship_contact_vectors.py --root /path/to/FireRed-main --reference /path/to/pinned/reference --output-dir /tmp/new-friendship-vectors
```



The accompanying two C# tests were staged for the parent-owned managed build; these C observations alone do not claim that managed runtime tests, native UI playback or audible output passed.

## Local representative and full evidence

Generated comparison data is local and ignored by Git, including this family's
plain JSON. Git retains the generator, explicit case selection, and managed
numerical comparator. Missing data fails with the exact generation command;
normal tests never skip a missing corpus or implicitly invoke a C compiler.

Default generation writes `profile: representative`: 51 complete cases /
1,979 callback rows. `--full` writes `profile: full`: 480 admitted cases /
17,420 rows. Both use the same fixture filename and the same
managed assertions for every numeric observation. Selection is by whole source
branch/boundary cases; every tick from -1 through retirement is retained. The
managed test independently requires the explicit key set and profile-specific
case/row counts, in addition to its unchanged frame, state and lifetime assertions.
The small exhaustive 256-byte predicate tables, where present, remain exhaustive.

From the repository root, generate into a new directory and run the same comparator:

```sh
vectors=$(mktemp -d)
python tests/Game.Presentation.Tests/Reference/generate_friendship_contact_vectors.py --root . --output-dir "$vectors" --full
FIRERED_SOURCE_VECTOR_DIR="$vectors" dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --filter 'FullyQualifiedName~FriendshipContactSourceVectorTests'
```

Omit `--full` to generate representative evidence. To supply normal tests, review
and copy the resulting `friendship_contact_vectors.json` into
`tests/Game.Presentation.Tests/Fixtures/`. The output is not staged in Git.
Both generation profiles compile the unchanged extracted C under `-O0` and `-O2`
and require byte-identical numeric row streams before emitting JSON. Full mode
retains its native row stream for audit; the default does not write that large
full-corpus sidecar. Source file/span hashes remain provenance, never substitutes
for numerical comparison. Generated harness output and full corpora stay outside
Git. The corpus redesign itself was verified with native C and Python; managed
validation belongs to the integrating run, not to those native checks.

### Explicit representative selection

- Every callback configuration: frame variants, signed extremes, trace delay/count and allocator boundaries.
  `0:0:0:1:0`, `0:0:0:1:1`, `0:0:0:1:2`, `0:0:0:1:3`, `0:0:0:1:4`, `1:0:0:1:0`, `1:0:0:1:1`, `1:0:0:1:2`, `1:0:0:1:3`, `1:0:0:1:4`, `1:0:0:1:5`, `2:0:0:1:0`, `2:0:0:1:1`, `2:0:0:1:2`, `2:0:0:1:3`, `2:0:0:1:4`, `3:0:0:1:0`, `4:0:0:1:0`, `4:0:0:1:1`, `4:0:0:1:2`, `4:0:0:1:3`, `4:0:0:1:4`, `4:0:0:1:5`, `4:0:0:1:6`.
- Opposite side: mirrored offsets, picture anchors and trace origin.
  `0:0:1:0:1`, `0:0:1:0:2`, `0:0:1:0:3`, `0:0:1:0:4`, `1:0:1:0:1`, `1:0:1:0:4`, `1:0:1:0:5`, `2:0:1:0:0`, `2:0:1:0:1`, `2:0:1:0:2`, `2:0:1:0:3`, `2:0:1:0:4`, `3:0:1:0:0`, `4:0:1:0:1`, `4:0:1:0:5`.
- Doubles rear geometry and trace no-free/one-free allocation.
  `0:1:2:3:4`, `1:1:2:3:4`, `2:1:2:3:4`, `3:1:2:3:0`, `4:1:2:3:4`, `4:1:2:3:5`, `0:1:3:2:4`, `1:1:3:2:4`, `2:1:3:2:4`, `3:1:3:2:0`, `4:1:3:2:4`, `4:1:3:2:5`.

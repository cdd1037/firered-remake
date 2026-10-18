# Visibility and status animation reference

## Local evidence profiles

Generated comparison JSON is local, ignored data, not a Git deliverable. Git keeps the native extraction code, explicit semantic case selection and managed comparisons. Tests never compile C or regenerate expectations implicitly. Missing input is an error with its preparation command.

- Default `representative`: 97 cases / 2,280 rows.
- Opt-in `full`: 318 cases / 10,388 rows.
- Selection: All eleven kinds; logical/sprite invisibility truth table; ten terrains and eight selectors; capacity 0/1/2/3 and every missing-battler probe. The full-only negative-delay case now contains all 4,097 consecutive frames, replacing its former 21-row sample.

Every frame and field of each selected case is retained. Full runs use the same unmodified original-C extraction and O0/O2 parity checks. Case selection is explicit in `generate_motion.py`; it is not every-Nth sampling. The JSON root `profile` identifies which cardinality/coverage guards the same managed comparator enforces.

Prepare the normal local input from the repository root:

```sh
python tests/Reference/VisibilityFour/generate_motion.py content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/visibility-four-source-vectors.json
```

For a fresh external full-corpus directory:

```sh
python tests/Reference/VisibilityFour/generate_motion.py content/raw/pokefirered/037335f4 --full --output /tmp/firered-full/visibility-four-source-vectors.json
FIRERED_SOURCE_VECTOR_DIR=/tmp/firered-full dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --filter FullyQualifiedName~VisibilityFourAnimationTests
```

Prepare any companion inputs used by that class in the same override directory before running it. An override never silently falls back to local fixtures. Full generation is explicit maintenance work, not ordinary-test setup.


This portable oracle extracts original pinned FireRed C functions and data for Faint Attack, Camouflage, Calm Mind and Odor Sleuth. No upstream build scripts are executed. Normal managed tests consume the committed JSON fixture.

Regenerate explicitly from the repository root:

```sh
python tests/Reference/VisibilityFour/generate_motion.py content/raw/pokefirered/037335f4
```

The default compiler workspace is a temporary directory. Optional `--build-dir` retains derived C, native executables and verification output outside the source tree. `--output` and `--reference-helper` support an external stage. The harness uses the shared portable compiler helper, verifies pinned source inputs, preserves extracted source line spans and hashes, and requires O0/O2 byte-identical output before writing vectors.

The boundary inputs are explicit party HP/position, native logical and sprite invisible flags, raw signed16 coordinates, terrain0..9 and occupied sprite cells. The original predicate is Present AND (!logicalInvisible OR !spriteInvisible). The native sprite callback phase precedes the OAM flag latch and task callbacks. Runtime launch presence is the event-time body lifecycle, not reconstructed from committed final HP. Clone numeric IDs are compared by ascending free-cell rank where native fixed reservations differ from runtime presence; allocation failure cases use identical actual occupied capacity. No phantom battler cells are inserted into runtime.

Covered mechanisms: visible/invisible selectors; conditional affine preparation; fade delay narrowing and alpha registers; uncounted logical-hide signal and restoration; all-other visibility; terrain-specific palette colors and exclusion masks; raw clone allocation, cleanup, movement, blink and counters; shrinking ring shared frame and affine players. The source clone copies its entire Sprite; its initial OAM Y remains the copied value even while hidden. Managed tests separately assert this copied OAM value, owner/material identity and complete authored-root phase order.

The full u8 delay255 case retains and compares every native frame; the representative profile omits that complete long case. No profile thins its retained traces. Ring source command union alignment is explicitly preserved at the ARM ABI's8-byte affine command stride. Coordinate species lookup is replaced only by explicit form y-offset inputs; original coordinate switches remain. Sprite allocation instrumentation replaces the platform allocator boundary, with original first-free scan and original sprite callbacks preserved.

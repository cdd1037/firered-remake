# Water, roots and poison-cloud source motion

## Local evidence profiles

Generated comparison JSON is local, ignored data, not a Git deliverable. Git keeps the native extraction code, explicit semantic case selection and managed comparisons. Tests never compile C or regenerate expectations implicitly. Missing input is an error with its preparation command.

- Default `representative`: 36 cases / 2,807 rows (35 managed cases / 2,656 rows; one source-only BASE-Y probe).
- Opt-in `full`: 113 cases / 8,813 rows (110 managed cases / 8,360 rows; three source-only BASE-Y probes).
- Selection: Every callback/recipe on player singles and opposing doubles; ally targeting, hidden partner, coordinate flags and the Y-clamp source-helper boundary.

Every frame and field of each selected case is retained. Full runs use the same unmodified original-C extraction and O0/O2 parity checks. Case selection is explicit in `generate_motion.py`; it is not every-Nth sampling. The JSON root `profile` identifies which cardinality/coverage guards the same managed comparator enforces.

Prepare the normal local input from the repository root:

```sh
python tests/Reference/ElementalFour/generate_motion.py content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/elemental-four-motion-source-vectors.json
```

For a fresh external full-corpus directory:

```sh
python tests/Reference/ElementalFour/generate_motion.py content/raw/pokefirered/037335f4 --full --output /tmp/firered-full/elemental-four-motion-source-vectors.json
FIRERED_SOURCE_VECTOR_DIR=/tmp/firered-full dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --filter FullyQualifiedName~WaterRootAnimationTests
```

Prepare any companion inputs used by that class in the same override directory before running it. An override never silently falls back to local fixtures. Full generation is explicit maintenance work, not ordinary-test setup.


This oracle extracts the original callbacks and shared linear/frame/affine machinery from pinned FireRed 037335f4c725d7c9aecdac87066f2002b4bd7e14. It never executes upstream build scripts. Source spans and their identities are recorded in the fixture; input identities are checked before extraction.

Regenerate explicitly from the repository root:

```sh
python tests/Reference/ElementalFour/generate_motion.py content/raw/pokefirered/037335f4 --build-dir /tmp/elemental-four-reference --output /tmp/elemental-four-motion-source-vectors.json
```

The generator compiles its authored bounded harness at O0 and O2 and requires byte-identical output. The full corpus has 113 configurations and 8,813 callback/frame rows. Ordinary tests only read it. The motion test compares 110 reachable configurations and 8,360 rows; three deliberate BASE-Y shifts are retained as source arithmetic evidence because the current runtime does not expose a variable BASE coordinate.

Coverage includes four battle positions, singles and doubles, selected allies, hidden/visible Safeguard partners, both Hydro Cannon coordinate selector bytes, shared argument mutations, Poison Gas's three phases and captured BG priority, Ingrain flicker and sine motion, frame/affine endings, and the separate stored-callback destruction handoff.

The fixture supplies explicit battler subpriorities. Managed tests compare source relative changes against the scene's retained subpriority inputs. The harness stubs BIOS matrix output; affine scale, rotation and timing come from the extracted original engine, while the existing managed BIOS/matrix tests own coefficient correctness. The fixture's semantics section records each bounded stub and its limits.

The `hFlip` and `vFlip` observations are the raw Sprite base flags. They are not the rendered animation flips: original `SetSpriteOamFlipBits` (`sprite.c:1236–1241`) XORs the animation command flags with them into OAM `matrixNum` bits 3 and 4. The fixture also records raw `oamMatrixNum`; the managed comparison reads those bits only for non-affine sprites. Original observations remain unchanged.

# Battler-copy native oracle

## Local evidence profiles

Generated comparison JSON is local, ignored data, not a Git deliverable. Git keeps the native extraction code, explicit semantic case selection and managed comparisons. Tests never compile C or regenerate expectations implicitly. Missing input is an error with its preparation command.

- Default `representative`: 14 cases / 897 rows.
- Opt-in `full`: 40 cases / 2,726 rows.
- Selection: Each callback in player singles and opposing doubles; free capacity 0/1/2, zero hold, hidden target, signed-speed wrap and self-target orb.

Every frame and field of each selected case is retained. Full runs use the same unmodified original-C extraction and O0/O2 parity checks. Case selection is explicit in `generate.py`; it is not every-Nth sampling. The JSON root `profile` identifies which cardinality/coverage guards the same managed comparator enforces.

Prepare the normal local input from the repository root:

```sh
python tests/Reference/BattlerCopy/generate.py content/raw/pokefirered/037335f4 --repo . --output tests/Game.Presentation.Tests/Fixtures/battler-copy-source-vectors.json
```

For a fresh external full-corpus directory:

```sh
python tests/Reference/BattlerCopy/generate.py content/raw/pokefirered/037335f4 --repo . --full --output /tmp/firered-full/battler-copy-source-vectors.json
FIRERED_SOURCE_VECTOR_DIR=/tmp/firered-full dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --filter FullyQualifiedName~BattlerCopyAnimationTests
```

Prepare any companion inputs used by that class in the same override directory before running it. An override never silently falls back to local fixtures. Full generation is explicit maintenance work, not ordinary-test setup.


`generate.py` extracts unmodified pinned C callbacks for Night Shade, Double Team, Mimic shrink and Mimic orb. It also extracts the native frame/affine players and fast-linear translation, rather than implementing a second numerical oracle. The bounded harness stores explicit GPU registers, palette banks, source body coordinates and allocation state; its stubs are described in the fixture metadata.

Run explicitly from repository root:

```
python tests/Reference/BattlerCopy/generate.py content/raw/pokefirered/037335f4 --repo . --output /tmp/battler-copy-source-vectors.json --build-dir /tmp/battler-copy-native
```

Generation refuses to overwrite the output. It verifies pinned source bytes and requires identical O0/O2 output. Current fixture: 40 cases, 2726 rows. Native task frame zero is its initializer before the first task step; the orb frame zero includes its immediate native initializer and native animation players. Full script scheduling/composition is covered separately by managed root tests.

Mimic's initial zero affine scale uses the project's explicit UBFIX-compatible SAFE_DIV zero-to-zero boundary. This is a deliberate safe source variant, not a claim that retail divide-by-zero behavior is identical. All nonzero conversion, retained ChangeSpriteAffineAnim state, callback handoffs, signed16 accumulation, and source fast-linear sign-bit arithmetic run from extracted source.

Double Team's runtime projection is intentionally bounded to its one-owner source closure: no concurrent battler palette, affine, visibility, translation or form mutation is admitted. It allocates/frees the source BENT_SPOON palette identity, but uses a detached black11 blend over copied original RGB555 body pixels instead of reconstructing unavailable original palette indices. Native source only copies unfaded entries1..15, blends the bank once, and never reads or mutates those entries later. Applying black11 directly is exactly equivalent for every opaque pixel; index0 remains transparent. The managed native-vector test verifies all15 visible palette entries, and a dependency-negative test rejects an added palette-changing companion.

No upstream scripts, ROMs, network downloads, graphics uploads or native rendering/audio claims are involved.

Source anchors: `battle_anim_ghost.c:335–395` mutates the existing actor for Night Shade; `battle_anim_effects_1.c:5209–5288` owns Double Team palette/copies/plane gating; `battle_anim_effects_1.c:2830–2921` owns Mimic shrink/restore/orb; its recipe at `628–662` uses the existing Orbs material. `battle_anim_scripts.s:2837`, `7431`, `7725` retain the authored roots. BG_GHOST and Orbs were already available locally; no new graphics conversion input was introduced.

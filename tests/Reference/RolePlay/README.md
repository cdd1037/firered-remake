# Additional-mon Role Play source oracle

## Local evidence profiles

Generated comparison JSON is local, ignored data, not a Git deliverable. Git keeps the native extraction code, explicit semantic case selection and managed comparisons. Tests never compile C or regenerate expectations implicitly. Missing input is an error with its preparation command.

- Default `representative`: 9 cases / 369 rows.
- Opt-in `full`: 84 cases / 3,444 rows.
- Selection: All six species/transformation/Unown-letter combinations, reversed doubles side, selected ally and cross-position geometry.

Every frame and field of each selected case is retained. Full runs use the same unmodified original-C extraction and O0/O2 parity checks. Case selection is explicit in `generate.py`; it is not every-Nth sampling. The JSON root `profile` identifies which cardinality/coverage guards the same managed comparator enforces.

Prepare the normal local input from the repository root:

```sh
python tests/Reference/RolePlay/generate.py content/raw/pokefirered/037335f4 --repo . --output tests/Game.Presentation.Tests/Fixtures/role-play-source-vectors.json
```

For a fresh external full-corpus directory:

```sh
python tests/Reference/RolePlay/generate.py content/raw/pokefirered/037335f4 --repo . --full --output /tmp/firered-full/role-play-source-vectors.json
FIRERED_SOURCE_VECTOR_DIR=/tmp/firered-full dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --filter FullyQualifiedName~RolePlayAnimationTests
```

Prepare any companion inputs used by that class in the same override directory before running it. An override never silently falls back to local fixtures. Full generation is explicit maintenance work, not ordinary-test setup.


The initializer, both phase callbacks, additional-mon creation helper, native Unown decompression selector, source BG-priority selector, rot-scale/reset helpers and task blend teardown are extracted unchanged from local pinned FireRed source. Authored hardware seams provide six party entries, marker picture tables, a free sprite/palette/matrix and the source default BG priorities. They never execute upstream scripts or fetch inputs.

Explicit regeneration:

    python tests/Reference/RolePlay/generate.py content/raw/pokefirered/037335f4 --repo . --output /tmp/role-play-candidate.json

The generator refuses an existing output and requires byte-identical C `-O0`/`-O2` results. The fixture has 84 cases / 3,444 rows, with task teardown at callback40. Both actor sides, singles/doubles, opposite and same-side targeting, Transform, Unown forms and Deoxys first-frame loading are exercised. The native helper's DMA is asserted to copy exactly 0x800 bytes. Selected Unown geometry deliberately comes from the base species while its bitmap selector can resolve another table entry.

OAM is captured before each task step. The ninth contraction destroys the sprite at callback39, but the already-built eighth-contraction OAM remains visible that tick. Callback40 disables blending and destroys the task. Affine inputs have zero rotation, so the BIOS seam copies the source signed Q8.8 scales without floating-point code.

The source ignores allocation-failure sentinels in this path. The reviewed runtime therefore validates scene OAM/matrix bounds before side effects rather than reproducing invalid native memory access. The oracle does not claim a source-defined failure result.

Deoxys normal-frame source pixels and palettes are checked separately by test-role-play-animation-import.py. Spinda spot drawing changes only indices1–3 to5–7, so the final all-white silhouette has identical transparency. No normal- or shiny-color approximation is needed for this task.

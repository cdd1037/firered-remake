# Extrasensory callback reference

Run explicitly from the repository, with the pinned local source already present:

```sh
python tests/Reference/Extrasensory/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/extrasensory-source-vectors.json
```

Expected JSON is local and ignored. `--full --output /tmp/extrasensory-source-vectors.json` expands battle positions; `FIRERED_SOURCE_VECTOR_DIR=/tmp` selects it through the same comparator. Neither normal tests nor imports compile upstream code.

The maintained generator extracts the two exact task initializers/steps, native raw clone and participant lookup, matrix allocation/free, source height correction, angle-zero matrix setter, and doubled center-to-corner calculation. The four native compilations compare O0/O2 output for two explicitly separate corpora:

- Legal source: unchanged source callbacks. A preflight asserts that every selected distortion index is below 320. Cases include clamped top rows (stage 1 ends at index 318), byte-wrapped target Y, retained nonzero banks and write-buffer 1, all clone selectors, picture Y offset, hidden direct versus hidden partner lookup, absent source, and full object/matrix allocation exits.
- Approved periodic boundary: only the distortion sine load is replaced by the documented shared periodic sampling policy. These expectations do not claim an original out-of-bounds result. Stage 1's inclusive final row samples index 320; the explicit policy returns periodic index 64. All legal indices remain direct table reads. Shared `PeriodicSineTests` separately prove the source table's 256-period extension and integer boundary behavior.

Every callback frame is retained, including initialization, stop request, VBlank stop consumption, raw object destruction and the following matrix/task release. Both 960-word banks use lossless deltas. Clone observations include retained X offset, overwritten height-corrected Y offset, shared body identity, native OAM subpriority/priority, forced visible blended mode, double-size canvas and explicit matrix. Source handles are opaque; the comparator observes selected identity and lifetime rather than inventing a matrix-ID allocator in the renderer.

The C fixture models only stop/flip between callbacks. Actual queued row-zero register precedence, terminal transfer and complete source frame ordering remain covered by the shared scanline engine and complete-root compositor tests. The BIOS matrix stub accepts angle zero only, where the exact result is the two supplied scales. No floating-point or out-of-bounds read is used.

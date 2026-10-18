# Helping Hand, Imprison and Teeter Dance source vectors

Regenerate with the already-local, pinned pret checkout:

```
python tests/Reference/SupportStatus/generate_motion.py content/raw/pokefirered/037335f4
```

An optional `--build-dir` keeps compiler artifacts/evidence in a chosen external directory. The default is a new temporary directory. `--output` selects the plain local numerical fixture. Generated comparison data is ignored and is not source-controlled; only the generator, pinned input identities, tests and explicit representative selection belong in Git. `--reference-helper` can select the maintained `tests/Reference/reference_c.py` helper when staging outside the repository. No upstream tools, scripts, downloads or ROM are executed. Pinned input hashes are checked before extraction.

The generator extracts original callback/task bodies, coordinate functions, sprite allocator and sprite frame/affine engine from commit 037335f4c725d7c9aecdac87066f2002b4bd7e14. Every extracted span records source lines and a SHA256. Both O0 and O2 must produce byte-identical rows. C compilation uses the portable repository helper; normal managed tests consume the local representative fixture without compiling C.

Coverage includes every actor position in singles/doubles; both hands; all authored fast-note argument triples and selectors6/7; task phase boundaries; exact base coordinates, secondary offsets, shared argument mutations, raw OAM flags, tile offsets, frame/affine state; five Imprison allocation attempts under 0..5 available cells; source form dimensions32x32,48x48,32x48,64x48,32x40; and hidden-present Imprison actors. Integrated cases pair Helping Hand movement with both claps and Teeter movement with the five scheduled static-coordinate note leaves.

The host harness replaces species-dependent lookup only with explicit source picture dimensions and offsets. Original coordinate branches and radius math execute. Native battler x and x2 remain separate in every row. A bounded runtime Teeter leaf closure may expose their sum as its rendered offset because those leaves read static GetBattlerSpriteCoord, never task-mutated gSprites.x. This is not a general base-position equivalence.

All four native battler sprite slots remain allocated even for hidden-present probes. The shared scene currently reserves visible bodies. Hidden-actor tests therefore compare monotonic free-cell ranks with ample capacity, rather than inventing a phantom runtime occupant or claiming raw numeric-ID equality. Imprison uses IDs only as retained destruction handles; it neither branches on their numeric value nor reads another child/battler through them. Separate congested-scene tests compare exact allocator failure and cleanup with explicit real occupants.

Hardware register writes and external allocation/destruction calls are recorded. The source allocator and source ResetSprite run unchanged; CreateSpriteAt is the bounded sheet-backed initialization boundary. Template-pointer bytecode tokens map losslessly to host pointers; native Cmd_createsprite still sets arguments, priority and immediate initializer/player order. The observer preserves pre-reset destroyed snapshots, while the actual source slot resets normally. No assets or actual audio are executed.

Dummy frame END commands leave clap raw OAM flip/tile writes intact. Fast notes are affine-double: source BeginAnim and AnimCmd_frame skip OAM flip writes, so selectors6/7 have frame-control VFlip but no effective rendered flip. The fixture records both control flags and OAM matrix index. Affine command union alignment is made explicitly32-bit to retain the source ARM8-byte command stride; commands and control flow are unchanged.

The shared kernel invocation/caller contract is outside this presentation oracle. Complete resolver-backed managed tests cover these three authored animation scripts, shared sounds, BG/palette reuse, cancellation, ownership, and termination. The portable C fixture alone is not a managed-build or audiovisual acceptance claim.

## Ordinary versus full source comparison

Default generation runs 35 explicit branch representatives and keeps every frame: eight sprite cases / 440 rows, 10 task cases / 928 rows, four integrated cases / 448 rows, eight direct cases / 528 rows, and five hidden-form cases / 400 rows. The 2,744 rows are plain local JSON, not committed goldens or gzip.

The family-local literal selection and the independent expected identities in `SupportStatusAnimationTests` preserve:

- Both clap hands and sides, all four battler positions across selected cases, and both source geometry sets.
- Ordinary note timing, selector2's distinct 41-frame command, selectors6/7, signed velocities and both travel directions. The integrated cases retain all five authored argument triples at their real spawn frames.
- Both Helping Hand and Teeter movement directions, independent raw base-X and X2 counters, and both integrated movement/leaf schedules.
- Imprison ordinary geometry on both sides, capacity 0/1/4/5/full, five attempts even with failures, destruction and blend teardown. Capacity4 versus5 preserves the final-attempt/storage boundary.
- Every special form shape (32x32, 48x48, 32x48, 64x48, 32x40) in hidden-present actors, distributing representatives across all four actor slots. Hidden tests retain only the documented monotonic free-cell rank equivalence; congested visible scenes still compare exact numeric slots.
- All eight direct frame selectors and the complete 66-frame affine-loop observation window, including control VFlip with unchanged OAM matrix index.

The local representative file projects only fields used by the numerical comparisons. It removes duplicate C input records, unused engine-state diagnostics and irrelevant hardware events. It keeps exact positions, lifetime, rendered fields, arguments, applicable frame/affine state, movement phases, independent battler positions, allocation-attempt counters, blending and child identity. Source provenance appears once. Runtime ownership, no-RNG behavior, untouched extended arguments, full-root composition, cancellation and fail-closed dependency checks remain independent managed assertions.

Full native generation retains all 228 original configurations and 16,604 rows, including every raw diagnostic field, and checks O0/O2 byte parity per configuration:

```sh
python tests/Reference/SupportStatus/generate_motion.py content/raw/pokefirered/037335f4 \
  --full --build-dir /tmp/support-status-reference-full \
  --output /tmp/move-source-full/support-status-source-vectors.json
FIRERED_SOURCE_VECTOR_DIR=/tmp/move-source-full dotnet test \
  tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj \
  --filter FullyQualifiedName~SupportStatusAnimationTests
```

Without `--output`, `--full` writes beside its temporary compiler artifacts rather than the ordinary fixture directory. Both profiles use the same managed frame-by-frame comparator. The representative profile requires exactly the reviewed identities; the full profile requires all original group counts. Both reject empty rows, duplicate case IDs and missing/noncontiguous frames. Ordinary tests neither compile C nor silently generate or skip missing data; a missing file fails with the exact regeneration command. Full traces are temporary debugging/verification output, not another permanent source-controlled fixture. Input and artifact hashes are provenance only, never semantic equality evidence.

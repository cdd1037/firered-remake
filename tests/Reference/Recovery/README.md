# Recovery and Aurora source evidence

## Local preparation and evidence profiles

Generated JSON is ignored local comparison data; it is not stored in Git. Source extractors, semantic case definitions, pinned inputs and comparators remain versioned. `check_vectors.py` is the explicit family preparation entrypoint. It stages all native compilation outside the repository, validates the result, then copies only JSON to the requested destination. It can prepare absent inputs without comparing to a previous generated file. `--fixtures DIR` optionally checks reproducibility against an existing corpus of the same profile.

```sh
# Prepare every input for this family, including small companion fixtures.
python tests/Reference/Recovery/check_vectors.py --reference content/raw/pokefirered/037335f4 --prepare tests/Game.Presentation.Tests/Fixtures
# Full original-C corpus stays outside the repository.
python tests/Reference/Recovery/check_vectors.py --reference content/raw/pokefirered/037335f4 --full --prepare /tmp/firered-full
# Independently validate an already prepared corpus, without regenerating it.
python tests/Reference/Recovery/validate.py --fixtures /tmp/firered-full
```

`--output /tmp/fresh-stage` optionally retains a named fresh external staging directory; otherwise a unique system-temporary directory is used. Add `--supplement PATH` for missing pinned image/map inputs in the overlay/weather families. Ordinary managed tests only read prepared data, and fail with a concrete preparation command if it is absent. Set `FIRERED_SOURCE_VECTOR_DIR=/tmp/firered-full` to run the same managed comparisons against the full corpus; prepare all families read by the chosen test filter in that directory.

- Task representative: eight cases / 816 rows. All four task kinds, visibility masks 3/15, both preexisting-fade values and both morning-sun sides remain.
- Task full: 28 cases / 2,632 rows. Every retained case preserves every frame and all ten palette banks.
- Motion, schedule and material remain complete. RecoveryVisualSourceOracleTests uses the same task comparator for both profiles; RecoveryVisualAnimationTests reads complete schedule/material companions.

Default reduced fixtures have root `profile: representative`; `--full` writes `profile: full`. The family-specific selectors retain complete traces, never every-Nth frames. The native full cases and all original numerical fields remain available on demand. Counts in historical detailed descriptions below refer to the full corpus unless labeled representative.


Pinned authority: pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`.
This bounded package covers Rest156, Morning Sun234, Moonlight236 and Aurora Beam62. It executes extracted C callback/player bodies in a local harness, never an upstream build script or ROM. Product code and importers are not imported by the source generators. Every input is SHA256 pinned; newly acquired official assets also retain Git blob identities in `acquisition.json`.

## Reproduction

Run `python3 tests/Reference/Recovery/check_vectors.py --reference content/raw/pokefirered/037335f4 --fixtures tests/Game.Presentation.Tests/Fixtures --output /tmp/recovery-reproduction-new` from the repository root. A C11 compiler and Pillow are required. The output must be a new external directory. The generator executes every native case at both `-O0` and `-O2` and rejects byte differences. Each selected case preserves every source row and palette value.

- Motion: 48 cases, 1,950 rows. Singles and doubles, both actor sides, all four double positions, Aurora signal before initialization/at callback1/at callback8, and fifteen overlapping Green Star families. Exact source frame/affine players, RNG, translation, raw-child initialization, hidden tiles, callbacks and destruction are retained.
- Tasks: 28 cases, 2,632 rows. BG1 register/priority/scroll and sound writes; all Moonlight direct BG entries and global palette-fade phases; visible battler masks3/15 and already-active global fade; alternating alpha coefficients; palette rotation. Full ten-bank palette snapshots remain in every row.
- Schedule: three roots, 297 rows, using the extracted `Cmd_loadspritegfx`, `Cmd_delay`, `WaitAnimFrameCount` and `RunAnimScriptCommand`. Non-yielding instructions are flattened to no-ops and creation frames are recorded. Rest births are3/25/47. Morning Sun births are32+7n for n0..14. Aurora's train begins after its independently covered BG fade, with relative births3n for n0..39. The schedule excludes later visual waits and HealingEffect.
- Material: five indexed sheets, all48 tile/flip frame combinations and15 distinct BG palette/index/flip samples. Direct PNG palettes, source map words, tile nibble packing, zero padding and palette index0 are observed independently of producers.

## Input and harness boundaries

Coordinates are canonical field positions with player picture offset8 and no opponent elevation. Two battler slots are occupied in singles and four in doubles. `CreateSpriteAt` models the consumed initialized fields and distinct affine slots; its source allocation scan, `CreateSpriteAndAnimate`, callback order and players are extracted. Green Star raw children begin at animation0 tile0; `StartSpriteAnim` changes their pending program, applied by their first later player pass. Their first two hidden tiles become8 and12 on that pass.

BIOS affine calls record inputs; matrix coefficient accuracy is covered by the existing shared affine vectors. BG loads record literal asset identities and offsets; the independent material fixture supplies their bytes. Palette transfer uses local memory and completes between frames. The source health-box status-animation flags are false, contest mode is false, and explicit visibility masks drive palette selection. Host-width pointer storage is replaced only for the source uint32 palette mask packed in two16-bit task words. No pointer arithmetic is used as animation authority.

## Lifetime, palette and capacity proof

Rest letters finish after61 motion callbacks. A Green Star parent plus its two raw children is fully destroyed after38 callbacks; the runtime's40-callback reservation is conservative. The fifteen-family native case reaches18 allocated cells and14 visible objects. Source `delay5` consumes seven callbacks, including restoration of the script callback. The bound reserves22 persistent field OAM entries, all live family cells, and conservatively all four HealingEffect allocations:44<64, before separately accounting for retained scene fragments. The runtime rejects unreviewed companions and unbounded branches; source affine capacity32 is checked separately with four reserved battler matrices. Aurora duration17 finishes after17 subsequent callbacks, within duration+2; its three-frame creation spacing bounds simultaneous rings. No use of hardware128 OAM as source64 sprite capacity is claimed.

Morning Sun's immediate BG initializer is followed by157 task callbacks, with four panned sound events. AlphaFadeIn takes64 callbacks for0/16 to16/0 at authored delay1. Aurora palette rotation takes130 callbacks and rotates only entries1..8 every third callback.

Moonlight writes fixed RGB555 colors into BG banks1..3 entries1..15 only. It never reads a baseline color; index0 remains the preceding black fade until the later whole-bank restoration. Both ordinary and already-active global-fade probes finish the task at callback75. The source scanner signals all Moon/Sparkle templates; admitted move roots cannot retain counted Moon sprites across controllers. Mixed `monbg` is rejected before side effects. Normal Moonlight completion intentionally retains BLDCNT and16/0 alpha, as the source script does; cancellation clears them. Sparse palette snapshots are detached and later normal fades retire their entries.

The renderer callbacks do not authenticate recovery receipts. Application dispatch separately requires committed healing ownership and, for Rest, the authenticated sleep/status result before the recovery boundary. Source battle script status text/icon precede Rest's attack animation.

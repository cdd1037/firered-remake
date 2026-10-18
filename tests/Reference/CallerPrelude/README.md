# Metronome and Sleep Talk caller-prelude vectors

## Local evidence profiles

Generated comparison JSON is local, ignored data, not a Git deliverable. Git keeps the native extraction code, explicit semantic case selection and managed comparisons. Tests never compile C or regenerate expectations implicitly. Missing input is an error with its preparation command.

- Default `representative`: 18 cases / 1,198 rows.
- Opt-in `full`: 56 cases / 4,348 rows.
- Selection: All three callbacks, all letter velocities, geometry/target selectors, partner head order, four unsigned-X edge probes and both signed-Y wrap directions.

Every frame and field of each selected case is retained. Full runs use the same unmodified original-C extraction and O0/O2 parity checks. Case selection is explicit in `generate_motion.py`; it is not every-Nth sampling. The JSON root `profile` identifies which cardinality/coverage guards the same managed comparator enforces.

Prepare the normal local input from the repository root:

```sh
python tests/Reference/CallerPrelude/generate_motion.py content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/caller-prelude-source-vectors.json
```

For a fresh external full-corpus directory:

```sh
python tests/Reference/CallerPrelude/generate_motion.py content/raw/pokefirered/037335f4 --full --output /tmp/firered-full/caller-prelude-source-vectors.json
FIRERED_SOURCE_VECTOR_DIR=/tmp/firered-full dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --filter FullyQualifiedName~CallerPreludeAnimationTests
```

Prepare any companion inputs used by that class in the same override directory before running it. An override never silently falls back to local fixtures. Full generation is explicit maintenance work, not ordinary-test setup.

The class also needs its two-root schedule: `python tests/Reference/CallerPrelude/generate_schedule.py content/raw/pokefirered/037335f4 --output /tmp/firered-full/caller-prelude-schedule-source-vectors.json`.


The bounded oracle uses already-local pret revision
`037335f4c725d7c9aecdac87066f2002b4bd7e14`. It never executes upstream
scripts, downloads a file, or uses a ROM. Ordinary tests consume the numerical
fixtures and do not run a C compiler.

Explicit regeneration writes a new candidate, never overwrites reviewed vectors:

```
python tests/Reference/CallerPrelude/generate_motion.py content/raw/pokefirered/037335f4 --output /tmp/caller-prelude-motion-candidate.json
python tests/Reference/CallerPrelude/generate_schedule.py content/raw/pokefirered/037335f4 --output /tmp/caller-prelude-schedule-candidate.json
```

Both generators accept `--build-dir` and `--reference-helper`; the latter defaults
to the maintained portable `tests/Reference/reference_c.py`. Linux `cc` and
Windows Visual Studio C compiler are supported through that helper. Both O0 and
O2 output must agree byte for byte. Input hashes are checked before extraction;
the motion fixture records extracted function/data spans, hashes and adaptations.

The 56 callback cases / 4,348 observed rows cover singles and all four double
positions; attacker/target/self/partner orientation; source width/height/y-offset
changes; both head selectors; every Sleep Talk vertical speed; ignored arg1;
signed16 vertical accumulator wrap; C truncating division; unsigned X edge
checks, including initial callback destruction. Existing frame and affine
players are compared against the original native state each live frame.

The original `Cmd_createsprite` retains target-based initial position and its
independent subpriority selector. Original `CreateSpriteAndAnimate` calls the
initializer immediately before frame/affine initialization. Original ascending
sprite allocation, `AnimateSprites`, callback handoffs and `DestroySprite` reset
remain in the harness. Native callback pointer splitting uses deterministic IDs
instead of truncating host addresses. A guarded Metronome affine array preserves
an inert native command[-1] loop probe without out-of-bounds host reads. The
original command union receives explicit32-bit alignment to preserve the ARM
8-byte stride. Species table lookup alone becomes explicit coordinate inputs;
the original coordinate switches, final-Y and head-attribute equations execute.

The BIOS matrix boundary does not synthesize nonzero-rotation coefficients;
this oracle records the full native scale/rotation program state. Existing shared
BIOS matrix tests cover coefficient conversion. Rendering tests compose the
resolved existing assets using those same players, and import checks compare
every pixel/palette/index byte with the pinned inputs and prior material owner.

The schedule oracle executes the original load and delay callbacks, records
sprite births, and stops at the final visual wait. Other non-yielding commands
are explicit no-ops. Metronome emits its bubble at6 and finger at14. Sleep Talk
emits letters at25,33,41,63,71,79,101,109,117. Complete managed root tests then
cover their actual lifetime, shared sounds/SwayMon, all positions, cancellation,
scene cleanup and starting a later leaf controller.

Source distinctions are deliberate:

- Metronome reuses Follow Me's exact affine tables and shared finger state
  machine, but starts beside the head, uses affine-double OAM, and omits the
  Follow Me sway phase. Its callback lifetime is103 frames after creation.
- ThoughtBubble is the existing counted callback with hold100, not Torment's
  raw child. It finishes119 frames after creation.
- Sleep Talk uses `AnimLetterZ`, not Rest's `AnimSleepLetterZ`. It starts at the
  actor center regardless of the command's target subpriority, ignores arg1,
  moves in its initial callback and ends only after unsigned X leaves0..240.
- Application caller receipt boundaries are separate from this renderer oracle.
  Mirror Move119 and Nature Power267 remain leaf aliases, not prelude roots.

These vectors are not managed-build, native-window or audible-output acceptance.

## Capacity and retained scene owners

This uses the existing stable-battle bound from Weather/Recovery. Pinned
`battle_interface.c:540-612` creates two body sprites per healthbox and assigns
` sHealthBar_SubspriteTable` at599. Its player/opponent tables at185-271 contain
two/three healthbar fragments. Four battlers plus two player healthboxes of4
pieces plus two opponent healthboxes of5 pieces give22 stable OAM entries.
Pinned `sprite.c:297` sets the software OAM limit64; the hardware128 limit is not
substituted. Four battler matrices plus all new affine births and every retained
affine scene sprite must remain at most32.

The prelude guard conservatively counts every authored birth simultaneously:
Metronome2 (one affine), Sleep Talk9 (nine affine), without taking credit for
early destruction or explicit waits. It adds every retained scene object's
actual subsprite count (at least1) and retained affine use, regardless of owner.
These roots allocate no raw children; only SwayMon is admitted as a non-emitting
task companion. Loops/branching and other emitting companions are rejected.
The stable-field bound excludes transient party-summary UI bars. Source root
playback occurs in the ordinary battle-event animation gate, not that UI flow.

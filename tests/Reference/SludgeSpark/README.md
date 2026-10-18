# Sludge and Spark visual callback evidence

Pinned input: `037335f4c725d7c9aecdac87066f2002b4bd7e14`. Regenerate explicitly:

```sh
python tests/Reference/SludgeSpark/generate.py --root . \
  --output tests/Game.Presentation.Tests/Fixtures/sludge-spark-source-vectors.json \
  --build-dir /tmp/sludge-spark-reference
```

The generator refuses to replace an existing expected file. Fixtures are local,
ignored numerical test data; the generator, harness and source pins are authored
maintenance inputs. No upstream scripts are executed.

The 894 complete cases / 11,122 rows are divided into two independent assertions:

- 656 legal-source cases. Sludge uses original nonzero mode, so its invalid frame
  lookup is never executed. Sludge Bomb impact particles retain all signed16
  fixed-point words, division/truncation, acceleration, timer and affine timing.
  Orbiting Spark uses nonzero flash divisors. Stationary Spark executes the
  original direct matrix callback and lifetime helper, including the original
  defender-partner selection test against attacker-partner visibility.
- 238 approved-policy cases. Separate compiled harnesses change only Sludge's
  requested animation 2 to its sole legal animation 0, and guard Spark's modulo
  with divisor != 0. Those rows prove the approved safe behavior and are not
  represented as original retail trajectories. Script arguments are unchanged.

Each case agrees between O0/O2. The harness extracts original sprite frame and
sprite affine players, arc/signed-linear helpers and callbacks. Frame 0 is the
source CreateSpriteAndAnimate callback followed by AnimateSprite. Later frames
run callbacks before animation programs; destroyed sprites skip those programs.
Source lifetimes include their original pre/postdecrement conventions.

Geometry is explicit standard singles/doubles with zero picture offset/elevation
and player pictureY +8; source partner visibility is an explicit mask. The BIOS
ObjAffineSet boundary writes zero coefficients while preserving the original
source scale/rotation state. Stationary Spark writes its own original four
matrix coefficients and those are compared directly. Allocation initializes an
available native sprite cell and reserves the actual battler cells. No gameplay
kernel or RNG is run by this oracle.

Managed tests compare every source/policy row, separately exercise canonical
resource resolution and real-pixel shared composition for all actor/target slot
layouts, verify every cancellation tick, reject malformed recipes/arguments and
excessive object/affine closures, and compare the approved Sludge frame selection
to original legal-mode movement/lifetime.

Importer checks use discovery and recipe extraction, never a broad all-producer
regeneration for each root. All 182 generated elemental material outputs remain
byte-identical; the new roots reuse poison-bubble and spark-2.

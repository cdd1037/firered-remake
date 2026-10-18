# Power/guard callback oracle

Pinned revision: `037335f4c725d7c9aecdac87066f2002b4bd7e14`.

Run `generate_power_guard_vectors.py --root <repository> --output-dir <new directory>`.
It extracts original C callbacks, affine arrays and SpriteTemplate declarations
verbatim, then compiles the authored native harness with `cc -std=c11 -O0 -fwrapv`.
No upstream script or managed runtime supplies the expected values. The shared
orbit/spray harness supplies source sprite allocation, frame/affine playback,
Random, static battle geometry and the BIOS affine platform adapter. File hashes
and exact extracted-span hashes are embedded. Two native runs are byte-identical.

The full profile has 1,040 configurations and 42,180 contiguous rows covering all actor/target positions
in single/double battles, including equal/partner slots. Tick -1 is synchronous
CreateSpriteAndAnimate/task initialization. Ordinary OAM observations follow
AnimateSprites and precede task execution. Flail records post-task state as well.

The observed callbacks are AnimReversalOrb/Step, AnimProtect/Step,
AnimShadowBall/Step, AnimHitSplatRandom, and AnimTask_FlailMovement/Step. Coverage
includes every authored Reversal phase, expanding/contracting signed16 radius
wrap; Protect's exact leftward palette rotation across indices 1..7, alpha ramp,
hold, signed16 x drift wrap and delayed destruction; all Shadow Ball phases and
its shared affine player; every impact affine selector, optional random selector
and two positional Random calls under four seeds; and full Flail reflection,
decaying rotation limit, rotation-based foot correction and restoration.

Protect durations 0 and 1 do not reach the bounded alpha tail. They are rejected
by callback validation and have explicit negative tests; the authored root uses90.
The duration2 vector is the smallest terminating source case. Native lifetimes:
Reversal26 retires at tick50 because initialization performs the first step;
Protect90 at110; ShadowBall16/16/8 at40; FlailMovement at41.

Flail's actor/target selector0/1 permits known hidden-but-present battlers. The
probe extracts GetAnimBattlerSpriteId and IsBattlerSpriteVisible verbatim, gives
present source sprites stable IDs, and sets both source invisibility fields for
hidden cases. Rotation still advances and resets while invisibility remains.
No callback performs arithmetic on the actual sprite ID. Partner selectors2/3
have separate visibility-sentinel rules and remain outside this adapter's ABI.

Protect's native palette probe initializes all512 entries deterministically and
asserts that every bank except OBJ4 remains untouched. GPU adapters observe exact
BLDCNT/BLDALPHA writes. Runtime comparisons use the existing blend abstraction;
disabled blending has canonical default target masks. Palette registration and
actual artwork conversion are separately checked by the narrow importer suite.

Stored callback pointers use the established host-safe callback handle. Palette
allocation, static BG priorities and BIOS ObjAffineSet are explicit platform
adapters. Source observations remain independent of the managed implementation.

## Flail-only resource publication

This checkpoint publishes only Flail's complete root, with the already-owned
impact material. The three other callback recipes in the numeric consumer are
explicit probe inputs. They are compared against independent original-C output,
without resolving or advertising their missing-art move roots. The full four-root
artwork stage remains separate until the fixed-source download is authorized.

## Local representative and full evidence

Generated comparison data is local and ignored by Git, including this family's
plain JSON. Git retains the generator, explicit case selection, and managed
numerical comparator. Missing data fails with the exact generation command;
normal tests never skip a missing corpus or implicitly invoke a C compiler.

Default generation writes `profile: representative`: 67 complete cases /
2,777 callback rows. `--full` writes `profile: full`: 1040 admitted cases /
42,180 rows. Both use the same fixture filename and the same
managed assertions for every numeric observation. Selection is by whole source
branch/boundary cases; every tick from -1 through retirement is retained. The
managed test independently requires the explicit key set and profile-specific
case/row counts, in addition to its unchanged frame, state and lifetime assertions.
The small exhaustive 256-byte predicate tables, where present, remain exhaustive.

From the repository root, generate into a new directory and run the same comparator:

```sh
vectors=$(mktemp -d)
python tests/Game.Presentation.Tests/Reference/generate_power_guard_vectors.py --root . --output-dir "$vectors" --full
FIRERED_SOURCE_VECTOR_DIR="$vectors" dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --filter 'FullyQualifiedName~PowerGuardSourceVectorTests'
```

Omit `--full` to generate representative evidence. To supply normal tests, review
and copy the resulting `power_guard_vectors.json` into
`tests/Game.Presentation.Tests/Fixtures/`. The output is not staged in Git.
Both generation profiles compile the unchanged extracted C under `-O0` and `-O2`
and require byte-identical numeric row streams before emitting JSON. Full mode
retains its native row stream for audit; the default does not write that large
full-corpus sidecar. Source file/span hashes remain provenance, never substitutes
for numerical comparison. Generated harness output and full corpora stay outside
Git. The corpus redesign itself was verified with native C and Python; managed
validation belongs to the integrating run, not to those native checks.

### Explicit representative selection

- All source control boundaries: orbit, Protect fade thresholds, three-phase travel, affine choices/RNG and hidden Flail.
  `0:0:0:1:0`, `0:0:0:1:1`, `0:0:0:1:2`, `0:0:0:1:3`, `0:0:0:1:4`, `0:0:0:1:5`, `0:0:0:1:6`, `0:0:0:1:7`, `0:0:0:1:8`, `0:0:0:1:9`, `1:0:0:1:0`, `1:0:0:1:1`, `1:0:0:1:2`, `1:0:0:1:3`, `1:0:0:1:4`, `1:0:0:1:5`, `1:0:0:1:6`, `1:0:0:1:7`, `1:0:0:1:8`, `2:0:0:1:0`, `2:0:0:1:1`, `2:0:0:1:2`, `2:0:0:1:3`, `2:0:0:1:4`, `3:0:0:1:0`, `3:0:0:1:4`, `3:0:0:1:8`, `3:0:0:1:12`, `3:0:0:1:16`, `3:0:0:1:17`, `3:0:0:1:18`, `3:0:0:1:19`, `3:0:0:1:20`, `3:0:0:1:21`, `3:0:0:1:22`, `3:0:0:1:23`, `4:0:0:1:0`, `4:0:0:1:1`, `4:0:0:1:2`, `4:0:0:1:3`.
- Reverse side: source mirroring, BG priority and rotation foot offset.
  `0:0:1:0:0`, `0:0:1:0:6`, `0:0:1:0:8`, `1:0:1:0:0`, `1:0:1:0:3`, `1:0:1:0:7`, `1:0:1:0:8`, `2:0:1:0:0`, `2:0:1:0:1`, `3:0:1:0:0`, `3:0:1:0:20`, `4:0:1:0:0`, `4:0:1:0:1`, `4:0:1:0:2`, `4:0:1:0:3`.
- Doubles rear geometry and source priorities.
  `0:1:2:3:0`, `1:1:2:3:0`, `2:1:2:3:0`, `3:1:2:3:16`, `4:1:2:3:0`, `0:1:3:2:0`, `1:1:3:2:0`, `2:1:3:2:0`, `3:1:3:2:16`, `4:1:3:2:0`.
- Shadow Ball: equal-position and same-side interpolation endpoints.
  `2:0:0:0:0`, `2:1:0:2:0`.

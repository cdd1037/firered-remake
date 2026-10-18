# Cut, wall and shared-particle source oracle

Pinned revision: 037335f4c725d7c9aecdac87066f2002b4bd7e14. The maintained generator extracts the original C callbacks, template declarations, frame programs, `GetAnimBattlerSpriteId`, `IsBattlerSpriteVisible`, and `SetAverageBattlerPositions`. Every extracted span and file digest is embedded in the fixture. It uses the existing original sprite/frame/task harness; no upstream scripts or managed implementation generate expected values.

Full profile: 1,580 configurations and 66,092 observed rows, plus both Fury Cutter predicates for all 256 source counter bytes. The runtime invocation validator admits only the producer-owned range 1..5. The full profile includes all singles/doubles actor/target combinations, every root Twister parameter tuple, both creation participants, hidden partner geometry, wall wait/shake stages, all four wall shards, signed16 coordinate and motion extremes, and selectors 0..3 with visible/hidden/absent partners.

Tick -1 is synchronous creation and initial animation. At ordinary ticks the sprite callback/frame phase precedes tasks. Sprite positions, offsets, priority, blend mode, frame state, all eight callback words, task count/delay, and ownership/lifetime are compared. Shake starts with nonzero offsets to prove additive restoration. Its x/y offsets and doubled task words retain signed16 wrap and signed division, including extremes where source restoration itself does not return to the starting offset.

Twister uses the existing sine and frame players. Its double-battle anchor averages both canonical target-side positions regardless of visibility. The target partner shake separately skips hidden/absent partners. The original rock template's pointer tail starts at `sAnims_BasicRock[4]`, exposing tiles 64 and 80; it is never clamped to the first rock. The template's unused affine-table pointer is mapped to the harness's dummy table because affine mode is off throughout; no callback or frame operation reads that table.

Host adapters supply only static battle coordinates/layout, identity source sprite-ID lookup, loaded resource lookup, and platform hooks already owned by the base harness. No callback performs arithmetic on source sprite identity. Source capacity remains MAX_SPRITES=64, with original native allocation unchanged. A separate runtime test proves sixty effect cells in fully visible doubles and failure at the sixty-first allocation.

Material closure: no new graphics. Leaf and rocks retain their existing elemental material; Cut and Impact retain shared hit material; Blue Light Wall retains canonical walls indexed bytes/palette; Torn Metal retains existing physical RGB555 bytes using its registered Blue Light Wall palette alias. Strict resource conflict detection remains active.

Source receipt boundaries are tested separately in Application. Fury consumes the postincrement prepared counter, including saturated five. Brick Break branches on authenticated opposing-side screen removal and retains its shattered animation against damage immunity. Ordinary Twister uses the existing first-eligible spread invocation.

## Local representative and full evidence

Generated comparison data is local and ignored by Git, including this family's
plain JSON. Git retains the generator, explicit case selection, and managed
numerical comparator. Missing data fails with the exact generation command;
normal tests never skip a missing corpus or implicitly invoke a C compiler.

Default generation writes `profile: representative`: 71 complete cases /
2,243 callback rows. `--full` writes `profile: full`: 1580 admitted cases /
66,092 rows. Both use the same fixture filename and the same
managed assertions for every numeric observation. Selection is by whole source
branch/boundary cases; every tick from -1 through retirement is retained. The
managed test independently requires the explicit key set and profile-specific
case/row counts, in addition to its unchanged frame, state and lifetime assertions.
The small exhaustive 256-byte predicate tables, where present, remain exhaustive.

From the repository root, generate into a new directory and run the same comparator:

```sh
vectors=$(mktemp -d)
python tests/Game.Presentation.Tests/Reference/generate_cut_wall_vectors.py --root . --output-dir "$vectors" --full
FIRERED_SOURCE_VECTOR_DIR="$vectors" dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --filter 'FullyQualifiedName~CutWallSourceVectorTests'
```

Omit `--full` to generate representative evidence. To supply normal tests, review
and copy the resulting `cut_wall_vectors.json` into
`tests/Game.Presentation.Tests/Fixtures/`. The output is not staged in Git.
Both generation profiles compile the unchanged extracted C under `-O0` and `-O2`
and require byte-identical numeric row streams before emitting JSON. Full mode
retains its native row stream for audit; the default does not write that large
full-corpus sidecar. Source file/span hashes remain provenance, never substitutes
for numerical comparison. Generated harness output and full corpora stay outside
Git. The corpus redesign itself was verified with native C and Python; managed
validation belongs to the integrating run, not to those native checks.

### Explicit representative selection

- Twister: nominal, zero/positive/0xff rise, acceleration threshold and signed wrap.
  `0:0:0:1:0`, `0:0:0:1:9`, `0:0:0:1:10`, `0:0:0:1:12`, `0:0:0:1:13`, `0:0:0:1:14`, `0:0:0:1:15`, `1:0:0:1:0`, `1:0:0:1:9`, `1:0:0:1:10`, `1:0:0:1:12`, `1:0:0:1:13`, `1:0:0:1:14`, `1:0:0:1:15`.
- Twister: opposite source side.
  `0:0:1:0:0`, `1:0:1:0:0`.
- Twister: partner-average geometry, hidden partner and priority sides.
  `0:1:2:3:0`, `0:1:2:3:14`, `0:1:2:3:15`, `0:1:3:2:0`, `0:1:3:2:14`, `0:1:3:2:15`, `1:1:2:3:0`, `1:1:2:3:14`, `1:1:2:3:15`, `1:1:3:2:0`, `1:1:3:2:14`, `1:1:3:2:15`.
- Brick wall: hold/retirement, shake/no-shake and signed offsets.
  `2:0:0:1:0`, `2:0:0:1:1`, `2:0:0:1:2`, `2:0:0:1:3`, `2:0:0:1:4`.
- Brick wall: reverse side.
  `2:0:1:0:0`.
- Brick wall: doubles target offsets.
  `2:1:2:3:2`.
- Brick shards: all four tile/direction branches, both origins and signed extremes.
  `3:0:0:1:0`, `3:0:0:1:1`, `3:0:0:1:2`, `3:0:0:1:3`, `3:0:0:1:4`, `3:0:0:1:5`, `3:0:0:1:6`, `3:0:0:1:7`, `3:0:0:1:8`, `3:0:0:1:9`.
- Brick shards: reverse side.
  `3:0:1:0:0`.
- Brick shards: doubles target origin.
  `3:1:3:2:7`.
- Shake: both battlers, odd/even terminal corrections, delay zero/one and signed-width edges.
  `4:0:0:1:0`, `4:0:0:1:1`, `4:0:0:1:2`, `4:0:0:1:3`, `4:0:0:1:4`, `4:0:0:1:8`, `4:0:0:1:9`, `4:0:0:1:10`, `4:0:0:1:11`, `4:0:0:1:12`, `4:0:0:1:16`, `4:0:0:1:20`, `4:0:0:1:24`, `4:0:0:1:28`.
- Shake: both partner selectors present/hidden, odd/even and signed-width edges.
  `4:1:2:3:16`, `4:1:2:3:17`, `4:1:2:3:18`, `4:1:2:3:19`, `4:1:2:3:20`, `4:1:2:3:24`, `4:1:2:3:25`, `4:1:2:3:26`, `4:1:2:3:27`, `4:1:2:3:28`.

# Thrash and Bide source closure

Pinned pret/pokefirered revision 037335f4c725d7c9aecdac87066f2002b4bd7e14.

- Thrash37: battle_anim_scripts.s2588–2607 invokes the two source tasks simultaneously, existing random fist/foot children, hit splats, ShakeMonInPlace and sound. No new material.
- Bide117: battle_anim_scripts.s7512–7551 retains the exact setup/release branches. Existing palette fades, ShakeMon2, monbg, slides and hit splats cover both branches. MoveTurn is committed input; setup0, release1. No new material.
- Original task implementations effects_2.c2307 onward and their exact sThrashMoveMonAffineAnimCmds are extracted verbatim. The shared original task-affine interpreter, integer BIOS adapter, foot anchoring, and sprite animator come from the maintained original-C harness.
- Raw sprite x/y and x2/y2 are independent signed16 values. Vertical motion writes x/y while horizontal affine foot anchoring writes y2. The shared raw-position delta is consumed by OAM latch/sort and monbg base-position sums; coordinate lookup helpers stay source-static. Cancellation restores each register family independently. Teeter Dance now uses this same raw-base seam, removing its obsolete single-callback closure restriction.
- Native oracle90 cases /7,890 callback rows: horizontal, vertical and simultaneous pair; singles/doubles; every present actor; default/offset/extreme signed16 bases and pos2; known present logical=false/sprite-invisible cases. Native horizontal preparation conditionally clears the sprite flag through IsBattlerSpriteVisible, while vertical-only retains it.
- tick-1 is synchronous initialization. Each subsequent row observes original AnimateSprites, pre-RunTasks OAM state, then both source task callbacks in source order. Consumer compares exact raw position, independent pos2, matrix, native sprite visibility, affine double-size mode, task data/cursor/loop values, counted ownership and retirement. Horizontal retires tick87, vertical83.
- Host adaptations: two task cells rather than one, original64 sprite capacity unchanged; participant sprite identity is used only as an index. No slot-ID arithmetic, deleted callback statements, frame clamping or imported-script execution.

- Staged physical importer closes exactly37/117, preserving all83 previous roots and all prior asset/recipe/indexed bytes. Original new task program only. Focused import checker5 cases; maintained old root inventory, task-program set, and random-fist shared-dependency assertions updated in3 targeted cases.

## Receipt boundary

- battle_scripts_1.s585: Rampage accuracy runs before attackstring; retained successful multiple-turn invocation skips ppreduce. The actual shared kernel emits a lone SourceAttackStringEvent followed by its unique owned HP/Substitute hit; no MoveUsed or PpCommand. Cue owns AfterRetainedAttackText and ACKs once, with AttackSequence set and PpSequence null. Thrash metadata is MOVE_TARGET_RANDOM, not selected.
- Bide setup battle_scripts_1.s575: attackstring, ppreduce, animation, setbide. The committed setup has PP then called-bide0→2 and owns MoveTurn0 at normal attack text.
- Storing battle_scripts_1.s3246 has text only and no animation. Release3251–3274 has no attackstring/ppreduce and sets animTurn1 before animation and damage. It is proven by called-bide1→0/released plus exactly one matching committed HP/Substitute result and owns BeforeHitResult with Attack/PpSequence both null; it does not ACK that current result.
- No-energy, protected, immune, sleeping and malformed prefix/unique-hit cases fail closed. Both moves make contact; actual queued Rough Skin suffix-KO cases preserve the earlier visual owner even after attacker faint cleanup.
- The kernel fixture uses natural Nidoking/Thrash and Pineco/Bide learners. Bide energy is acquired from a real Rattata Tackle (or Gastly Night Shade for immunity), followed by the actual storing and release actions. Test-only body alterations synchronize the bound party record.

## Local representative and full evidence

Generated comparison data is local and ignored by Git, including this family's
plain JSON. Git retains the generator, explicit case selection, and managed
numerical comparator. Missing data fails with the exact generation command;
normal tests never skip a missing corpus or implicitly invoke a C compiler.

Default generation writes `profile: representative`: 24 complete cases /
2,104 callback rows. `--full` writes `profile: full`: 90 admitted cases /
7,890 rows. Both use the same fixture filename and the same
managed assertions for every numeric observation. Selection is by whole source
branch/boundary cases; every tick from -1 through retirement is retained. The
managed test independently requires the explicit key set and profile-specific
case/row counts, in addition to its unchanged frame, state and lifetime assertions.
The small exhaustive 256-byte predicate tables, where present, remain exhaustive.

From the repository root, generate into a new directory and run the same comparator:

```sh
vectors=$(mktemp -d)
python tests/Game.Presentation.Tests/Reference/generate_locked_pair_vectors.py --root . --output-dir "$vectors" --full
FIRERED_SOURCE_VECTOR_DIR="$vectors" dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --filter 'FullyQualifiedName~LockedPairSourceVectorTests'
```

Omit `--full` to generate representative evidence. To supply normal tests, review
and copy the resulting `locked_pair_vectors.json` into
`tests/Game.Presentation.Tests/Fixtures/`. The output is not staged in Git.
Both generation profiles compile the unchanged extracted C under `-O0` and `-O2`
and require byte-identical numeric row streams before emitting JSON. Full mode
retains its native row stream for audit; the default does not write that large
full-corpus sidecar. Source file/span hashes remain provenance, never substitutes
for numerical comparison. Generated harness output and full corpora stay outside
Git. The corpus redesign itself was verified with native C and Python; managed
validation belongs to the integrating run, not to those native checks.

### Explicit representative selection

- Horizontal, vertical and simultaneous tasks: all initial-state and signed-coordinate boundaries.
  `0:0:0:0`, `0:0:0:1`, `0:0:0:2`, `0:0:0:3`, `0:0:0:4`, `1:0:0:0`, `1:0:0:1`, `1:0:0:2`, `1:0:0:3`, `1:0:0:4`, `2:0:0:0`, `2:0:0:1`, `2:0:0:2`, `2:0:0:3`, `2:0:0:4`.
- Reverse side and affine foot anchoring.
  `0:0:1:0`, `1:0:1:0`, `2:0:1:0`.
- Doubles hidden rear battler and signed initial offsets.
  `0:1:2:4`, `1:1:2:4`, `2:1:2:4`.
- Doubles reverse side signed base positions.
  `0:1:3:3`, `1:1:3:3`, `2:1:3:3`.

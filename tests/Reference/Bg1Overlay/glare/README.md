# Exact-source Glare callback oracle

This staging directory contains the authored extraction harness and generated numerical observations for FireRed `AnimTask_GlareEyeDots`, its step callback, `GetGlareEyeDotCoords`, and `AnimGlareEyeDot`. No canonical repository files, upstream scripts, or managed code were executed or edited for this harness.

## Reproduce

From any directory, using the repository's authored `tests/Reference/reference_c.py` compiler helper:

```sh
PYTHONDONTWRITEBYTECODE=1 python /tmp/firered-bg1-overlay-stage/oracle/glare/generate_glare_vectors.py \
  --repository /workspace/scratch/18c02ed4ed0d/FireRed-main \
  --output /tmp/firered-bg1-overlay-stage/oracle/glare/glare-source-vectors.reproduced.json
```

The output path must be new and remain within this directory. Source files are verified against SHA-256 pins before extraction. The script compiles its generated C with `cc -std=c11 -O0 -fwrapv` and again with `-O2`, requires byte-identical observations, and checks lifecycle invariants before writing JSON. Tested compiler: Debian GCC 14.2.0. The generated C is inspectable in `glare-harness.generated.c`; authored platform stubs and observers are in `harness_prefix.c` and `harness_suffix.c`.

## Coverage and observed results

- 37 scenarios and 3,286 frame rows: all 16 ordered actor/target IDs 0..3 in each single/double coordinate table; full saturation; one free slot; a failed first sibling with successful second sibling; contest state reset by init; forced contest state branch.
- Source `sBattlerCoords` is extracted verbatim. Battler ID maps identically to position. Controlled heights are `[63,50,41,28]`; controlled picture Y offsets are `[3,-5,7,-9]`, added to the extracted table Y coordinates. These are declared fixture inputs, not species data or product-derived values.
- All 26 allocation attempts occur at task frames 4,8,...,52. Normal execution creates 26 children, peaks at 20 live children, and destroys the task at frame 89.
- Complete saturation still attempts both members of all 13 pairs and destroys the task at frame 53. With 63 occupied slots, successes occur at frames 4 and 44; the task ends at frame 81. Failed first-sibling allocation leaves the successful second sibling at `(+3,+3)`.
- Child callback and OAM observation first occur one frame after creation. The child expires at birth+37 on its 37th callback. The parent's counter reaches zero during sprite callbacks; the parent destroys itself in that same frame's later task phase. Raw children leave the global visual task count unchanged.
- 13,429 direct coordinate helper observations cover 13 endpoint pairs, full s16 endpoints, negative coordinates/displacements, exact endpoints, fractional truncation, max values 0/1/2/3/12/255, and bounded u8 pair numbers. Example: `(87,68)→(176,35)` gives `(175,35)` for pair 11 and exact `(176,35)` for pair 12; `(0,0)→(-1,-2)` gives `(-1,-1)` for pair 1.
- Init explicitly sets `tIsContest=FALSE`, including a seeded TRUE probe. The forced TRUE state case only tests the otherwise unreachable `(-3,+3)/(+3,-3)` branch; it is not reachable FireRed behavior. Same-side/self actor-target pairs similarly exercise helper inputs without claiming native move-script targeting.

## Frame and row interpretation

Frame 0 is the immediate source initializer followed by the global visual-count increment, matching `Cmd_createvisualtask`; task step calls begin at frame 1. There is no synthetic child callback during allocation. The verbatim `BattleMainCB2` establishes each subsequent frame's order: `AnimateSprites` → `BuildOamBuffer` → `RunTasks` (unrelated text/palette functions are inert).

Each frame records task data and global count after the task phase, source child callback events during the sprite phase, child slots observed at the OAM boundary, both allocation results, live child slots after tasks, and whether the task was destroyed during the task phase. The `semantics` object in JSON defines all positional row fields and task data indices. In particular:

- `children` and `oamChildren`: `[slot, instance, x, y, x2, y2, timer, parentTask, parentCounterIndex, subpriority]`.
- `callbacks`: `[slot, instance, timerBefore, timerAfterOrAtDestroyEntry, parentCountBefore, parentCountAfter, destroyed]`. For a destroyed child the timer is captured at entry to `DestroySprite`, before the stub zeroes the slot.
- `allocations`: `[attempt, pairNum, siblingIndex, x, y, subpriority, resultSlotOr64, instanceOrZero]`. Results of 64 are source allocation failures.
- `initReads`: `[kind, battler, selector, value]`, with kind 0=side, 1=coordinate, 2=attribute. C operand evaluation ordering is compiler-specific; values and target identities are authoritative, not cross-compiler read ordering.

## Exact-source boundary and limitations

The Glare macro/function block (effects_3.c:3882–4043), sprite template, source coordinate table, `CreateSprite`, `AnimateSprites`, task traversal, `DestroyAnimVisualTask`, and `BattleMainCB2` are extracted verbatim. Preprocessor aliases add observation wrappers without editing the extracted algorithms. `Cmd_createvisualtask` and `IsContest` are recorded as provenance evidence; the script parser is not run.

Stubs supply controlled battler reads, sprite storage allocation/teardown, task teardown, and OAM-boundary observations. The source `CreateSprite` routine chooses ascending free slots, including slot reuse. OAM observation records live slots, not actual rendered pixels or sorted native OAM. No asset decompression, affine animation, graphical output, or audio correctness is claimed.

The original helper shifts negative signed values left, which is not portable ISO C; this oracle explicitly uses the native GNU C signed-shift behavior and 32-bit `int`, requires arithmetic right shift, and checks O0/O2 parity. It is numerical evidence for this pinned source/host model, not a claim that undefined source expressions are portable to every C compiler.

`glare-source-vectors.json` includes source commit, full source hashes, extract hashes and line ranges, exact compilation commands, compiler identity, harness hashes, coordinate inputs, and validation assertions. The committed repository remains untouched by this staging work.

# Sky Attack and Spit Up original-C evidence

The tracked generator extracts `AnimSkyAttackBird`, `AnimSkyAttackBird_Step`,
`AnimSpitUpOrb`, `AnimSpitUpOrb_Step`, `AnimTask_SpitUpDeformMon`,
`TrySetSpriteRotScale`, and their original templates and affine programs from
pinned pokefirered 037335f4. It reuses the existing original-C sprite/frame/affine
and task interpreter harness and its pinned input checks. Only battle geometry,
host-safe stored pointers and BIOS angle/matrix arithmetic are adapters. Managed
runtime values never produce expected rows.

## Default evidence

The default is 41 explicit semantic and boundary cases, with every frame retained:

- Bird: both single-battle directions and all twelve legal doubles directions,
  including both partner directions on both sides. The original signed 12-frame
  fixed-point velocity continues until the source screen-exit boundary.
- Orb: all sixteen authored phases at duration 12, phase 255, durations 0, 1 and
  255, and a hidden-present rear actor. The postincrement lifetime, signed word
  accumulation, frame program and indefinitely growing affine program are checked.
- Deformation: all four source actor geometries and hidden-present actors on
  both sides, including every task-affine frame and terminal matrix restoration.

These cases produce 1,413 rows. Tick -1 follows source synchronous initialization.
Every ordinary row is the complete post-sprite/post-task state. The comparator
checks positions, offsets, all eight callback words, frame/affine program state,
matrices, visibility, task state and exact destruction. Native termination checks
sprite, task and matrix allocation. Original C builds at O0 and O2 must agree for
every selected row before a fixture is emitted.

Full-root presentation tests separately exercise Sky Attack setup/release,
Spit Up counts 1/2/3, target-partner branches, retained duration arguments,
sounds, source background and palette ownership, composition and cancellation
at every frame. Application tests authenticate real public-kernel receipts,
including zero/failed stockpile counts and forged/misordered consumption markers.
Spit Up uses the committed count as animation turn; its source animation power
remains Normal 100. The source damage multiplier does not change that register.

## Reproduce locally

Generate into a new directory; no generated JSON or native binary is tracked:

```sh
python tests/Game.Presentation.Tests/Reference/generate_charged_root_vectors.py --root . --output-dir /tmp/charged-root-candidate
FIRERED_SOURCE_VECTOR_DIR=/tmp/charged-root-candidate dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --filter FullyQualifiedName~ChargedRootSourceVectorTests
```

Alternatively generate the default file in ignored
`tests/Game.Presentation.Tests/Fixtures`. The generator refuses to overwrite an
existing candidate. The emitted C, native binaries and numeric JSON remain local.
Ordinary tests never invoke a native compiler and fail if evidence is missing.

Add `--full` to generate the optional corpus: the same 41 cases plus all fourteen
legal single/double actor-target layouts, phases in increments of 8, and durations
0/1/12/255. Select that directory with `FIRERED_SOURCE_VECTOR_DIR`; the same
comparator consumes every selected case and frame without a fallback corpus.

No full-corpus or managed-test pass is implied by the generator's O0/O2 parity.

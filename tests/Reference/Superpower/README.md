# Superpower extracted-source oracle

Explicit local generation (including the complete birth/sound schedule):

```
python tests/Reference/Superpower/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/superpower-source-vectors.json
```

Inputs are pinned to pret/pokefirered037335f4c725d7c9aecdac87066f2002b4bd7e14.
The generator extracts six original orb/rock/fireball callbacks, original final-Y
coordinate helper, linear-translation functions, sprite frame/affine engines and
four original script load/delay scheduler functions. It does not execute upstream
scripts or use managed runtime code to obtain expectations. Ordinary tests never
compile C; output JSON, generated C and binaries remain ignored local evidence.
The generator refuses to overwrite an existing reviewed fixture.

The86 cases /7188 rows cover six single/double actor-target layouts; both argument
selectors; all five authored rocks; signed16 position/speed/timer boundaries; and
original uint8 Y wrapping/clamping with elevation. The split pointer accumulator
in the source rock callback carries numeric ARM32 data, not an actual address.
Its two halves are represented as u32 and its intptr_t shift as s32, avoiding host
pointer UB while retaining original callback branches and integer operations.

Primitive frame0 invokes the original initializer followed by AnimateSprite.
Subsequent frames use original AnimateSprites. Platform battler coordinates and
palette/BLDCNT I/O are narrow explicit stubs. Dummy frame END retains tile0;
the existing shared managed dummy loop has equivalent visible pixels. Compare
lifetime always and sprite state while alive; destroyed source memory is retained
only for terminal observation. Argument words are checked for non-mutation.

The schedule is generated in the same command, using original RunAnimScriptCommand,
WaitAnimFrameCount, Cmd_loadspritegfx and Cmd_delay. Non-yielding source operations
emit events or act as no-ops. This scheduler stops at the final visual wait after
all authored births and sounds; it does not claim full-root native drain parity.
Separate managed complete-root tests compare that source schedule, native primitive
vectors, scene composition, callback BLDCNT clearing, actual drain and independent
pre-attachment idle restoration. Cancellation and pre-effect capacity denial are
also separate gates. Audio event evidence is not actual device output evidence.

Generation was run explicitly with byte-identical O0/O2 outputs for the primitive
and schedule oracles. Managed execution remains the parent integrator's gate.

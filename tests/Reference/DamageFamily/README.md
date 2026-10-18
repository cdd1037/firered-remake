# Damage-family source callback oracle

This explicit generator extracts bounded callbacks and player helpers from pinned FireRed revision `037335f4c725d7c9aecdac87066f2002b4bd7e14`. It does not execute upstream scripts. Expected values are never read from managed product code.

Generate into a local ignored fixture directory or an external evidence directory:

```
python tests/Reference/DamageFamily/generate.py --root . --output /external/damage-family-source-vectors.json --build-dir /external/native-build
```

The authored harness compiles at O0 and O2 through `tests/Reference/reference_c.py`, requiring byte-identical JSON. Function hashes and source-file SHA256 pins are emitted. The only callback-body adaptation is an explicit pointer cast for task-affine byte arithmetic. An extra union alignment member preserves the ARM 8-byte affine command stride on host C compilers, checked by static assertion. Saved callback/pointer words are opaque host handles.

The representative corpus has all six single/double actor positions, both participant selectors, sprite-animation callback handoffs, every impact affine variant, both Rapid Spin directions and elevation phases, scanline-bank retention, full stretch/impact/reappear phases, all 24 dust births and deaths, all three Air Cutter raw births and shared-register phase changes, signed/unsigned fixed-point boundaries, callback sounds, retained argument writes, preexisting battler position/secondary offsets, hidden partner averaging, and picture geometry. Every native frame is retained until callback termination.

Explicit battler positions, visibility and picture inputs replace party/species lookup boundaries. Raw slot allocation uses the extracted source allocator with actual battler slots reserved and no fabricated allocation outcome. The admitted root capacity proof is tested separately; this corpus only executes successful raw allocations. The angle-zero BIOS matrix boundary is exact. Scanline DMA is modeled only at its SetParams/VBlank/stop boundary; original callback writes are executed unchanged against all 1920 retained words.

Zero Air Cutter reciprocal divisors are recorded under `excludedUndefinedInputs`, not executed or presented as a source result. Speed2 and3 remain defined signed-narrowing cases and are executed. Generated C, executables and numerical vectors stay ignored/local; authored generator, pins, harness, focused comparator and this description are source files.

## Raw Air Cutter cadence proof

The earliest source child lifetime is 41 callback visits: one initial transition, nine easing callbacks (postdecrement8 through0), and31 hold callbacks (postdecrement30 through0). With the first birth at frame1, the earliest destruction is frame42. A spawn cadence argument19 means20 ticks between births, so the third birth occurs40 ticks after the first, at frame41 while all three remain active. Argument20 would schedule that third birth at frame43, after the first child could decrement the task counter used as the emission index; it is outside the finite successful-closure proof. The explicit speed2/cadence19 case records births1/21/41, the first child disappearing42, and terminal frame150 after later children consume the shared task-register mutation. The comparator requires this evidence, both speed2/3 signed reciprocal cases, the first ordinary positive speed4 boundary, the high-speed32766 nonzero boundary, and both undefined-zero exclusions.

Current representative generation:268 cases /8,559 rows. The optional full actor-target corpus also compiled identically at O0/O2:516 cases /15,275 rows. Managed comparison is a separate gate and is not implied by native optimization parity.

The corpus additionally requires54 exact authored-root operand rows: nine callback recipes on all six battle actor layouts. These preserve the Rapid Spin ring40/-2 velocity and speed2 elevation phases, Air Cutter1536 speed and128 priority bit, averaged slice coordinates, both persistent-impact offsets and eight-tick holds, and small/big Revenge retained register2 values0 and4. They are additional to the parameter-boundary cases, not replacements.

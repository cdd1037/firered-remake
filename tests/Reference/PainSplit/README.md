# Pain Split extracted-source oracle

Explicit local generation:

```
python tests/Reference/PainSplit/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/pain-split-source-vectors.json
```

The generator pins original inputs at pret/pokefirered revision
037335f4c725d7c9aecdac87066f2002b4bd7e14 and extracts original projectile, battler
movement, finite-frame engine, rot-scale, Y-offset and script timing bodies.
Only platform coordinate/species lookup, hardware resource allocation, audio I/O,
and BIOS Q1.14 matrix boundaries are authored. BIOS sine constants are generated
independently with math.sin; original scale and rotation inputs are also observed.
No upstream scripts or managed runtime code are executed to obtain expectations.

The explicit corpus contains104 cases: six single/double actor layouts, both
selectors, all three projectile offsets and all three affine modes; twenty signed16
offset/bounce cases; twelve hidden-target/initial-offset cases. Hidden fixtures
set both source logical and sprite invisibility, respecting the native OR predicate.
Runtime registers remain16 words; native8 are compared and remaining8 stay untouched.

The reconstructed generator was freshly executed after environment recovery:
104 cases,1848 rows and byte-identical O0/O2 results. Source projectile births
are3/28/53 in pairs, task/sound starts15/40/65, and drain78. Managed comparison
is a separate pending gate; native parity alone does not establish it. The generator refuses
to overwrite existing output. Numerical trajectories, generated C and binaries
are local ignored files. Normal tests never compile C or regenerate expectations.

Primitive frame0 is native initializer plus AnimateSprite for projectiles, and
initializer only for tasks. Subsequent native frames run the original callbacks
and frame engine. Inactive memory is retained solely for terminal observation.
Root timing executes original load/delay/wait handlers; narrow pointer-free wrappers
bind this root's creation operands. End uses the source visual-count gate with
explicitly no playing audio. This does not establish actual audio-device output.

The focused full-root test captures an independent idle frame BEFORE attaching
its owner. Active scene CaptureIdle calls are forbidden. Cancellation and source
capacity checks are separate from primitive numerical parity.

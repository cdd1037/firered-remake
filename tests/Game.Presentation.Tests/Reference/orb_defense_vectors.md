# Stockpile and Swallow native callback evidence

The generator extracts the original `AnimPowerAbsorptionOrb`, `AnimSwallowBlueOrb`, `AnimTask_StockpileDeformMon`, and `AnimTask_SwallowDeformMon` functions and their original affine arrays from pinned 037335f4. It reuses the existing native sprite/frame/affine/task harness and its pinned input verification. The translation initializer, 8.8 translator, callback installation, destruction, matrix allocation and task-affine interpreter are original C. Platform adapters provide fixed battle geometry, host-safe stored pointers and BIOS matrix arithmetic. No managed implementation produces expected rows.

The fixture contains 440 native cases and 18,210 logical frame observations. Both battle layouts, all actor/target combinations, every authored orb offset, extra bounded offsets/durations and hidden-present task actors are covered. Tick -1 follows synchronous source initialization. Ordinary frames observe sprites before tasks, then task-owned battler state after tasks. Native termination asserts visual counts, sprite allocation and matrix allocation return to their initial state.

Identical actor-owned trajectories across irrelevant target choices are deduplicated. Rows are numeric `[state, sprites, afterTaskSprites]` arrays with explicit column names. 440 case references share 144 trajectories; the committed compact JSON is 822,146 bytes. Protect, Reversal and Shadow Ball continue using the unchanged existing PowerGuard source oracle.

Reproduction (write to a new empty directory):

`python tests/Game.Presentation.Tests/Reference/generate_orb_defense_vectors.py --root . --output-dir /tmp/orb-defense-candidate`

The generator refuses to overwrite an existing candidate and preserves the emitted C and raw observations beside its compact JSON. A second generation was byte-identical to the staged fixture. Managed callback comparisons and the full integration suite are separate checks owned by the core compiler.

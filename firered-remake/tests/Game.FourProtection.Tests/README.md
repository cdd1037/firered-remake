# Shared four-slot Protect / Detect / Endure

Focused domain-only proof against base `e14578ebdbd60a50df2a975edb6102d350b7aa2e` and original FireRed `037335f4c725d7c9aecdac87066f2002b4bd7e14`.

```sh
bash tests/Game.FourProtection.Tests/run.sh COMPLETE_PRODUCT_ROOT PINNED_REFERENCE_ROOT NEW_EVIDENCE_DIRECTORY
```

The runner uses the product's existing official .NET 10.0.401 SDK, an offline locked restore, and `/tmp/firered-remake-build.lock`. It builds only this focused test executable plus its Domain/Content dependencies; no full solution, GUI, export, or broad suite.

The independently compiled C harness executes the verbatim `Cmd_setprotectlike` body over 224 deterministic cases: every action position, all three moves and another preceding move, first through fourth attempts, inclusive threshold equality and failure. No original out-of-range table read is executed. Fifth and subsequent attempts separately test the user-approved 8191 threshold (12.5% inclusive over 65536 outcomes) and saturating byte, not a claim about undefined original behavior.

Another 36 vectors execute verbatim `ApplyRandomDmgMultiplier` and `Cmd_adjustnormaldamage` with no held item or Substitute. They are checked against the existing shared damage primitive, while real four-action tests exercise damage, contact, multihit, spread scripts and residual continuations through the public owner. The source probe also checks cleanup before field/battler residuals and delayed attacks.

Real four-slot tests cover original speed/priority order, numeric last-action failure despite cancelled pending slots, state serialization at continuations, one shared family count, intervening move and unable-action history, no-PP selection/execution, stale transactions, invalid counters/flags, RNG overflow, ordinary contact and Surf's two target PP scripts, primary status and unaffected move flags, Endure at one HP, multihit termination, Explosion command order, poison/burn/sandstorm/Future damage, and actual reward plus reserve-entry continuation.

Six immutable SHA-256 baselines cover all serialized state, events and RNG from legacy Protect/Detect/Endure action sequences at seeds 0 and 31. They were captured from the unmodified pinned base binaries, before compiling changed Domain code, and are never rewritten by the runner.

This does not admit Metronome, Sleep Talk, Assist, Mirror Move, Focus Band, unimplemented damage effects, encounters, AI or presentation. Existing direct/spread move metadata and script ordering remain authoritative. Pokémon assets are read only from the existing local catalog root.

# Shared four-slot turn moves

Run `bash tests/Game.FourTurnMoves.Tests/run.sh PRODUCT_ROOT PINNED_REFERENCE NEW_OUTPUT`.
The product root supplies existing authenticated data and the offline toolchain configuration; no resource tree is copied.

This runner executes genuine complete four-action turns with JSON continuation parity, five consecutive Rollout hits, Disable applied after selection, its selection rejection and residual expiry, source-extracted random vectors, stat flags at caps, Follow Me's retained selected position, zero-PP forced continuation, multi-hit target/PP/immunity/KO and malformed rollback. It verifies the actual default level-22 Twins move lists without changing their species, level or abilities. Cute Charm admission belongs to its separate shared contact patch.

The local C oracle compiles verbatim pinned `Cmd_rolloutdamagecalculation`, `Cmd_disablelastusedattack` and `Cmd_setmultihitcounter`, with deterministic RNG shims. Its per-hit critical/damage/secondary calls follow `BattleScript_EffectMultiHit`; it proves exact counts and draw order, not a full GBA battle execution. The source pin, input file hashes and numeric vectors are required and checked against the frozen fixture.

`Game.TurnMoveParity.Tests` preserves 368 hashes across 724 successful old ordinary/called action, continuation and residual calls. The baseline was produced from an isolated source-only snapshot of fixed `08f76bd` plus declared ally-support and `1acdc06` immunity dependencies before this patch; it is never regenerated to match candidate output. Both runners reject missing arguments; the shell runner rejects an existing evidence directory and propagates source/build/runtime failures.

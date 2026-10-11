# M04 source targeting and spread tests

This is the recovered M04 implementation validated again on 2026-10-11 against fixed product source `3a0344d0231cf270801c34d5458944103c39f6cd`. The seven product-file bodies were first reconstructed from retained conversation; all results here are new runs after that reconstruction. Lost historical binaries, logs and hashes are not claimed recovered.

Run from the candidate or integrated repository:

```sh
bash tests/Game.SourceTargeting.Tests/run.sh COMPLETE_PRODUCT_CONTENT_ROOT NEW_EVIDENCE_DIRECTORY
```

The runner sources that product's official `scripts/env.sh`; `.tools/reference-root` must identify pinned `pret/pokefirered@037335f4c725d7c9aecdac87066f2002b4bd7e14`. It builds only Domain, Content and this BCL-only test executable with empty NuGet sources and locked restore. Compilation takes `/tmp/firered-remake-build.lock`; execution runs outside the lock. It does not download or run upstream reference scripts.

The target oracle extracts the actual original `HandleAction_UseMove` target branch and `GetMoveTarget` function after verifying seven source files against the pinned Git blobs. The C harness supplies four numeric slots, reverse retained turn order, exact LCG and ability-count stubs. Its 11,520 vectors compare target, redirection flag and final RNG seed/cursor. The damage oracle independently transcribes original integer operations for neutral stage/type cases; tests supply actual authenticated species stats. The evidence includes the source hashes, generated C and CSV, compiler logs and JSON result.

The 21 groups cover source absence versus HP versus deferred vacancy, actual Electrike Lightning Rod, BOTH and Explosion traversal, Pressure, screens/spread integer arithmetic, independent histories, final PP and first KO, source Damp/Plus/Minus field queries, two genuine four-action turns, serialized resume, pending enemy EXP, second-target RNG rollback through both kernel and turn owner, plus unsupported branch atomicity.

Legacy safety is executable: a real Hidden Power control succeeds, while actual Plusle/Minun on each side and donor-authenticated copied Plus/Minus abilities reject before PP, events or RNG. Plus/Minus is deliberately not added to global singles capability admission. Actual Ditto Transform into Electrike proves copied Lightning Rod still takes Electric damage and gains no Special Attack stage. See [SOURCE_CONTRACT.md](SOURCE_CONTRACT.md) for the exact supported boundary and integration ownership.

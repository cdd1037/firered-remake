# Shared source end-turn recipes on actual four slots

当前入口：`dotnet test tests/Game.Battle.Tests/Game.Battle.Tests.csproj --filter FullyQualifiedName~FireRed.Tests.Battle.Residuals.`。参见[测试组织](../../README.md)。

以下保留原验收与参考向量的历史说明；旧 shell、console DLL、锁定还原命令和计数不再是当前测试入口，历史套件名称也不表示当前仍有对应项目。

Run `bash tests/Game.Battle.Tests/Residuals/run.sh AUTHENTICATED_CONTENT_ROOT PINNED_SOURCE_ROOT NEW_EVIDENCE_DIRECTORY`.
The runner requires the official .NET 10 toolchain selected by `scripts/env.sh`, uses an empty local NuGet source and locked restore, and serializes short builds with `/tmp/firered-remake-build.lock`.

The canonical `FrameEndTurnContext` performs field ordering, battler ordering, weather/status damage and end-turn abilities for both single and double battles. Its scheduler and storage now form one concrete partial implementation; there is no historical singles bridge, paired-singles projection or second poison/weather implementation.

Admitted ordinary state is temporary rain/sandstorm/sun/hail, poison/burn/Toxic and Shed Skin/Rain Dish/Speed Boost. Actual selected poison/burn effects can now continue through this owner. Truant's recipe is shared, but four-action admission remains blocked until its action canceller is migrated. Items, links, locks, screens, Wish/Future/Perish, permanent weather, Forecast/Trace and other unported phases still reject explicitly. No reward is invented: an opponent residual KO yields `RewardsPending`; real reserves yield `ReplacementsPending`. Damage registers remain alive until the actual delayed cleanup boundary.

The cursor retains one fixed slot order, source stage, next index and revision/event bounds. Only real paused reward/replacement/terminal phases may carry it. A forged cursor in Actions/EndTurn, an inconsistent delayed stage/index/order, a future revision/event bound or competing weather is rejected. The old singles public cursor is unchanged. The four-turn contract is versioned `firered-four-action-shared-end-turn-v2`.

The new console suite covers genuine four-action turns, HP/status synchronization through actual party keys, source ability positions, temporary-weather expiry, source absence versus living-HP weather queries, actual poison/burn creation, residual KO boundaries, typed cursor JSON, stale replay, exhausted counters and unported state rejection. The authentic species and moves are created from the shared authenticated catalog; isolated oracle arithmetic cases explicitly edit scalar registers and do not claim encounter admission.

The original-C oracle authenticates source and constant-header bytes at pret `037335f4c725d7c9aecdac87066f2002b4bd7e14`. It compiles the complete unchanged `Cmd_weatherdamage` function and exact poison/bad-poison/burn cases from `DoBattlerEndTurnEffects`. The 576 weather-function and 112 status-case vectors execute those original commands with explicit environment stubs. This is original C function/case execution, not ROM execution. Existing Rain Dish, Speed Boost, Truant and recharge extracted-C suites supply additional checks; the cycle probe now calls the shared `SourceEndTurnAbility` adapter instead of the removed private implementation.

`Game.FourEndTurnParity.Tests` freezes 64 serialized phase-result hashes independently generated from unmodified product commit `3a0344d0231cf270801c34d5458944103c39f6cd` before comparison. Each hash includes state, typed events, RNG trace and cursor. The unchanged old source-end-turn assertions also execute. Missing or mismatching frozen data fails; tests never rewrite the golden. The first candidate and the independently built baseline additionally produced byte-identical complete stdout.

The existing four-action regression retains its source ordering oracle and KO boundaries. Its old rain/poison rejection examples were removed, and its formerly rejected Poison Powder example was replaced with Sleep Powder, because the new poison capability is deliberately admitted.

This is Domain-only bounded acceptance. No actual EXP/EV/learning, replacement entry resume, full doubles, campaign/encounter/AI admission, UI, native window or audio verification is claimed. Permanent weather is the next explicit ownership delta; its version validation and speed query must be widened together.

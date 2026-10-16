# Current canonical singles trainer contract

当前入口：`dotnet test tests/Game.Battle.Tests/Game.Battle.Tests.csproj --filter FullyQualifiedName~FireRed.Tests.Battle.TrainerPolicy.`。参见[测试组织](../../README.md)。

以下保留原验收与参考向量的历史说明；旧 shell、console DLL、锁定还原命令和计数不再是当前测试入口，历史套件名称也不表示当前仍有对应项目。

This focused executable creates trainer encounters only through authenticated
`SourceTrainerEncounterEntry.Prepare` → `FuchsiaGymTrainerPartyFactory.Create` →
`SourceTrainerEncounterEntry.Begin`. It needs authenticated content from the main
checkout, and does not call the retired trainer executor, import an old battle
state, or use a retired controller as its oracle.

The six groups cover:

- Authentic flags 1/7 metadata and legal maximum-score move selection; the four
  source damage-simulation draws and contiguous Gen III LCG trace; zero-PP move
  exclusion; stale controller rollback; Encore and Struggle controller bypasses.
- Koga's first Hyper Potion at 1 HP, selection-time debit and action-time 200-HP
  recovery, duplicate/stale rollback, same-process JSON continuation, authentic
  factory receipts, and forged metadata, generated identities, and JSON proof
  rejection.
- Perish Song switching, legal faint reserves, living replacement refusal, and
  player reserve / Set / player-fainted gating of Shift.
- Authentic KO → EXP → Shift decline and accept, rooted refusal, JSON and stale
  continuations, plus the 1-HP Shift reserve fainting to Spikes. The cached
  opponent enters before the next player replacement prompt and remains fixed
  when that player replacement is selected.
- Explosion mutual KO, EXP before replacement, player replacement before trainer,
  and suppression of Shift after the player's faint.
- Terminal trainer action versus terminal residual KO, once-only EXP and exact
  residual cursor resumption without duplicate faint or next-turn ready events.

These are direct current-contract assertions and targeted fixture variations.
They do not create new parity goldens from implementation output. The earlier
comparison-based 225-check report in
`docs/r3-trainer-encounter-policy.md` remains historical evidence; its source-only
Spikes caveat describes that earlier report, not this current test.

## Verified command and result

From the candidate root, using the official SDK and local package source:

```sh
export FIRERED_TOOLCHAIN_ROOT=/workspace/scratch/18c02ed4ed0d/firered-toolchain-20261013
source /workspace/scratch/18c02ed4ed0d/firered-restored-20261013/firered-remake/scripts/env.sh
flock /tmp/firered-remake-build.lock dotnet build tests/Game.Battle.Tests/TrainerPolicy/Game.SourceTrainerPolicy.Tests.csproj --configfile /workspace/scratch/18c02ed4ed0d/firered-toolchain-20261013/NuGet.OfficialLocal.Config -v:q
dotnet tests/Game.Battle.Tests/TrainerPolicy/bin/Debug/net10.0/Game.SourceTrainerPolicy.Tests.dll /workspace/scratch/18c02ed4ed0d/firered-restored-20261013/firered-remake
```

The final code-only rebuild used the same build command with `--no-restore` in
place of `--configfile`. On 2026-10-13: zero build warnings/errors; hosted runtime
10.0.12; 6 groups, 258 checks, zero failures. Empty/missing groups and any failed
assertion fail the executable. No Application cutover, full-route replay, visual
or audio acceptance is claimed by this suite.

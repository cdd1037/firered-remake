# Current source battle lifecycle probes

当前入口：`dotnet test tests/Game.Battle.Tests/Game.Battle.Tests.csproj --filter FullyQualifiedName~FireRed.Tests.Battle.Lifecycle.`。参见[测试组织](../../README.md)。

以下保留原验收与参考向量的历史说明；旧 shell、console DLL、锁定还原命令和计数不再是当前测试入口，历史套件名称也不表示当前仍有对应项目。

This focused Domain harness executes only `SourceBattleFrame` and its existing
`SourceFourActionTurns` owner. Singles use two real positions with
`SourceImmediate` KO timing; unused storage positions stay null, unbound and
source-absent. Real four-position battles retain their existing end-turn KO
replacement contract. No game resources are copied.

Fourteen groups cover normal moves, terminal wild KO/EXP, action-time player
replacement, interrupted poison and delayed attacks, voluntary switch/Pursuit,
real four-actor turns and deferred replacement, source normal/Struggle behavior,
Intimidate/Protect/Leftovers, ghost Master Ball dodge before capture RNG and
slower-player guaranteed escape before the foe/Ready continuation. The two ghost
cases directly construct a canonical policy fixture; Application tests cover
special story entry authorization and callbacks.

Every accepted continuation validates canonical identity. Interrupted state is
serialized and resumed with exact state/event/RNG equality; stale calls return
the same input with empty events and trace. Tests inspect HP/PP, identity serials,
once-only EXP, event order, source LCG accounting and residual/Ready counts.
The source comparisons use two small frozen vectors with party hashes captured
from the old source owner before its removal, plus full action RNG traces.
See [vector provenance](fixtures/README.md). No old owner or state adapter is
compiled or invoked by this executable.

Run from a checkout using an authenticated content checkout and fresh output:

```sh
bash tests/Game.Battle.Tests/Lifecycle/run.sh \
  /workspace/scratch/18c02ed4ed0d/firered-restored-20261013/firered-remake \
  /tmp/firered-current-lifecycle-result \
  /workspace/scratch/18c02ed4ed0d/firered-toolchain-20261013/NuGet.OfficialLocal.Config
```

The runner sources the official toolchain, uses the shared short build lock,
and does not build or change the content checkout. The retirement gate passed
14 groups / 690 checks on runtime 10.0.12 with zero build warnings/errors;
see [exact current result](evidence/retirement/result.json).

The optional `--four-parity` mode emits four deterministic complete canonical
results. The previously captured accepted-baseline comparison remains in
`evidence/four-parity/`; its full byte parity describes that earlier snapshot.
Historical failed and intermediate runs remain under `evidence/`.

This suite does not claim full campaign, active-save support, Godot rendering,
or physical audio acceptance. Current Application wild/trainer suites establish
the public command and story continuation boundary separately.

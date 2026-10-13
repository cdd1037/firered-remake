# Current source battle lifecycle probes

This focused domain harness targets the R3 candidate derived from `687d0c9`.
It exercises the existing `SourceFourActionTurns` owner with two real positions,
current `SourceBattleKernel.Version`, `SourceImmediate` KO timing and ordinary-wild
EXP, plus representative real four-position regressions. The remaining two
storage positions stay null, unbound and source-absent throughout every single
battle. Primary canonical fixtures are constructed directly. Two explicit parity probes
use the old current singles owner only as a reference and import its entry fixture
before shared execution. No game resources are copied.

Twelve groups cover authenticated fixtures, a normal turn, terminal KO/EXP,
action-time player replacement, poison and delayed-attack interruptions,
voluntary switch/Pursuit, a real four-actor turn, and deferred doubles replacement. Two source-owner parity probes compare normal
moves and forced Struggle, and one checks actual Intimidate entry, Protect and
Leftovers with null unused positions.
Every accepted continuation validates canonical identity. Interrupted state is
serialized and resumed with exact state/event/RNG equality; stale calls must
return the identical input with empty events and trace. The tests inspect actual
party HP/PP, identity serials, exactly-once EXP via the existing reward calculator,
event order, trace accounting and residual/Ready counts.

This is domain prerequisite evidence only. It does not establish Application
cutover, capture/escape policy, current campaign completion, old save conversion,
Godot rendering or physical audio acceptance.

Run with a fresh evidence destination, after the coordinated core build is ready:

```sh
bash tests/Game.SourceLifecycle.Tests/run.sh \
  /workspace/scratch/18c02ed4ed0d/firered-restored-20261013/firered-remake \
  /workspace/scratch/18c02ed4ed0d/firered-r3-lifecycle-candidate-20261013/tests/Game.SourceLifecycle.Tests/evidence/run-1 \
  /workspace/scratch/18c02ed4ed0d/firered-toolchain-20261013/NuGet.OfficialLocal.Config
```

The script reads the accepted checkout's official environment setup, uses the
shared build lock, and builds against candidate Domain/Content projects. It never
builds or changes the accepted checkout. Failed runs remain evidence; no golden
expectation is regenerated from implementation output.

The optional `--four-parity` argument emits four complete deterministic results
(clean turn, deferred player KO, opponent KO/EXP and poison/replacement). Running
the same harness binary with the accepted baseline Domain/Content DLLs in an
isolated folder and with candidate DLLs permits an exact byte comparison of full
final state, events and RNG trace, without rebuilding the accepted checkout.
See [the result and limitations](evidence/REPORT.md).

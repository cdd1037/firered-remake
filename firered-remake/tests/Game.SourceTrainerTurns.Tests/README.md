# Full source trainer turns

This BCL test runs authentic source trainer parties and a supplied controlled owned party through complete actions, genuine random Metronome calls, EXP suspension, forced replacements and end-turn continuation. It is not a naturally obtained party or an Application/native acceptance test.

`evidence/run2.log`: five trainer groups (Sheila, Celia, Kirk, Nate and Koga), 1,454 checks, 514 turns, 727 owner commands and 495 called transitions passed. The later narrow `evidence/baton-boundary-2.log` adds one group/13 checks over four commands: slot-three Baton Pass into a one-move real reserve completes the opponent action and end turn, charges only the outgoing PP, and permits the actual incoming next action. Source `GetWhoStrikesFirst` reads the current chosen slot; a compact-array missing slot represents the original fixed-array `MOVE_NONE`, priority zero. It must not preserve the pre-switch move ID instead.

Run with the official toolchain:

```sh
source scripts/env.sh
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SourceTrainerTurns.Tests/Game.SourceTrainerTurns.Tests.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet tests/Game.SourceTrainerTurns.Tests/bin/Debug/net10.0/Game.SourceTrainerTurns.Tests.dll "$PWD"
# Boundary-only increment, without repeating the full random run:
dotnet tests/Game.SourceTrainerTurns.Tests/bin/Debug/net10.0/Game.SourceTrainerTurns.Tests.dll "$PWD" --boundary
```

The initial failures and compile/nullability correction remain in evidence. The first boundary invocation used an older binary after that failed build and is explicitly not a passed result. The corrected build and second boundary result are the acceptance pair. Preset doubles, source-specific battle animations and physical audio are separate gates.

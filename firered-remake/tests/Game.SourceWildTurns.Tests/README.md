# Source ordinary wild turns

Opt-in `WildBattleState.SourceWild` owns a complete actual wild battle turn. `CalledParty` carries the complete supplied player party and the one already-generated wild opponent. `SourceWild.Turn` holds the queued opponent and the source delayed-end-turn cursor across mandatory Baton Pass and faint replacements. No selected Metronome result is removed or rerolled because of an unsupported owner boundary.

The public move, Run, capture, Potion, Medicine and Poké Flute entry points dispatch through that explicit owner. The by-index `SwitchSourceWildPlayer` and `ReplaceSourceWildPlayer` APIs preserve distinct owned identities even when two Pokémon have identical data; the original combatant-based calls reject ambiguous source matches. Baton Pass uses `ChooseSourceWildParty` and rejects unrelated actions until the genuine choice is resolved.

The old Fuchsia rule identifier and all earlier public profiles retain their original move admission; the additional owner marker is required. Source entry does not regenerate an encounter. It preserves the original AI setup and held-item draws, Old Rod restriction, Ghost/Marowak capture policies and exact scripted Snorlax identity. Source battle state is still not an in-game save feature.

Source references, pinned commit 037335f4c725d7c9aecdac87066f2002b4bd7e14:

- `src/battle_main.c`: RunTurnActionsFunctions, BattleTurnPassed, HandleAction_ActionFinished; chosen wild actions and delayed callbacks
- `src/battle_script_commands.c`: Cmd_metronome, Cmd_handleballthrow, Cmd_switchindataupdate
- `src/battle_util.c`: HandleFaintedMonActions and end-turn status callbacks
- `data/battle_scripts_1.s`: Baton Pass, poison and wild escape scripts

`GetWhoStrikesFirst` at `battle_main.c:3484–3504` reads the current battle Pokémon's move at `chosenMovePositions`, not the separately stored `gChosenMoveByBattler` ID. Its four-slot source array yields `MOVE_NONE` when Transform or Baton Pass leaves the chosen slot empty. `SourceOrder` therefore treats a now-missing compact-array slot as priority zero and otherwise still reads the current slot. The owner does not clear a meaningful slot on Baton Pass/faint replacement or preserve an obsolete chosen-move priority. Focused cases exercise slot-three Baton Pass and Transform into a one-move Pokémon. The investigated chosen-ID alternative was discarded after this source check; no such metadata remains in the implementation.

Build uses the official pinned .NET SDK; run records the hosted runtime. Keep compiler serialization limited to the build and run the test executable after releasing the lock:

```sh
source scripts/env.sh
flock /tmp/firered-remake-build.lock dotnet restore tests/Game.SourceWildTurns.Tests/Game.SourceWildTurns.Tests.csproj --configfile NuGet.Config --ignore-failed-sources -m:1 -nr:false
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SourceWildTurns.Tests/Game.SourceWildTurns.Tests.csproj --no-restore -m:1 -nr:false
dotnet tests/Game.SourceWildTurns.Tests/bin/Debug/net10.0/Game.SourceWildTurns.Tests.dll .
# Recheck the narrower boundary suite after a boundary-only correction:
dotnet tests/Game.SourceWildTurns.Tests/bin/Debug/net10.0/Game.SourceWildTurns.Tests.dll . --boundaries-only
```

The executable exercises actual learned selections and authentic RNG-selected Metronome turns. It checks continued source validation, no partial state or RNG on rejections, genuine party identity, capture success/failure, ordinary Run, mandatory Baton Pass, action faint/replacement, delayed residual continuation, source medicine/flute, entry flags, and old-owner rejection. It does not establish preset doubles, all-game completeness, presentation or physical audio acceptance.

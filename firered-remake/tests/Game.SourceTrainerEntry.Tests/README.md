# Source trainer entry contract

This targeted executable checks explicit full-party source entry across the sixteen
historical trainer catalog families, from Viridian Forest through Pokémon Tower.
It uses real imported catalogs and the pinned trainer factories. It does not edit
a campaign save or replace a source trainer roster.

The lead is a source-created level-29 Clefairy, which learns Metronome at level 29.
TM06 Toxic occupies a compatible move slot with one PP already spent. The party
also includes an actually fainted first slot and a real living reserve. Entry must
retain their identities, HP, PP, held items, badge properties and party positions.
The fixed source is `037335f4c725d7c9aecdac87066f2002b4bd7e14`; the learnset and TM
compatibility evidence is in `src/data/pokemon/level_up_learnsets.h` and
`src/data/pokemon/tmhm_learnsets.h` in that reference.

The public `StartSource` overloads bypass no historical public contract: they use
the real factory and exact historical entry AI, NPC OT, ordering, ability and
turn-number RNG steps with the full source party present. Historical `Start`
overloads remain unchanged. `PromoteSourceTrainer` is restricted to untouched,
successful Fuchsia-base entry results and preserves their event/trace arrays,
RNG, counters and active entry effects. Source reserve normalization uses the
same shared synchronization as direct source entry.

Every imported older trainer is exercised. One representative entry per family
is also compared against promotion of an authentic old `Start` result. An
Intimidate lead makes entry-effect preservation observable. Additional groups
cover malformed parties and results, opponent identity/metadata tampering, JSON
round trips, exact canonical Toxic status versus rejected preloaded counters,
and actual Toxic/Metronome turns against an old trainer.

The private source roster projection is entered only after source battle and
complete party authentication. It skips the redundant historical move-mechanics
check while retaining the historical exact roster, move IDs, personality,
immutable identity and phase checks. Public historical validation retains its
original mechanics check.

Run under the project toolchain and shared build lock:

```sh
source scripts/env.sh
flock /tmp/firered-remake-build.lock dotnet restore tests/Game.SourceTrainerEntry.Tests/Game.SourceTrainerEntry.Tests.csproj --configfile .toolhome/recovery-local-only.config --disable-parallel
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SourceTrainerEntry.Tests/Game.SourceTrainerEntry.Tests.csproj --no-restore -m:1 -nr:false
flock /tmp/firered-remake-build.lock dotnet tests/Game.SourceTrainerEntry.Tests/bin/Debug/net10.0/Game.SourceTrainerEntry.Tests.dll "$PWD"
```

The harness loads Route22 with `includeSourceCryWaves:false`; entry validation
requires authenticated Pokémon/trainer metadata, not obsolete optional rendered
cry WAV files. No presentation or audible-output acceptance is claimed.

The first transient compile failure (a concurrent shared item helper was not yet
present) and the first harness loader failure (optional legacy cry WAVs) remain in
separate logs. They are not passed checks.

## Verified result, 2026-10-10

`results.log`: **37 groups, 1,628 assertions, 176 pinned rosters, zero failures**.
`build.log`: zero warnings and zero errors. `toolchain.log` identifies the pinned
SDK 10.0.401 and .NET host/runtime 10.0.12. Only this target and its Domain/Content
references were built; no full solution, Godot or GUI pass was run.

The first natural-turn assertion incorrectly expected a legacy move-used event
named Metronome. The retained diagnostic trace instead shows the correct source
sequence: attack-string Metronome, a random Bind selection, Metronome PP 10→9,
Bind damage/faint, and the trainer EXP boundary. The final assertion checks the
source attack-string event, actual PP decrement and called transition. This was
a test expectation correction; the battle implementation was not changed.

# Source trainer AI adapter

Pinned source: pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`.

The source single-trainer contract uses the existing Cerulean scoring, switch and
replacement algorithms through an authenticated context. Historical calls keep
their existing default profile. Actual battle types, ability overlays, status,
field timers and copied moves reach mechanics; permanent identity is checked by
the central source trainer validator. Replacement permits the narrowly specified
internal EXP-completed transition through `ValidateSourceTrainerAiState`; it does
not alter HP, Pokémon identity or the published trainer phase.

New behavior includes the source CHECK_BAD_MOVE dispatches reachable after
Metronome/Transform, Koga's Toxic/Minimize/Acid Armor/Selfdestruct scoring, duplicate
Future Sight avoidance, genuine no-dispatch Metronome/Role Play behavior, Perish
Song and copied Natural Cure switching, and Koga's two Hyper Potions/Full Heal.
Item selection is pure: the caller commits the returned item slot only after its
command succeeds. The original item count and strict HP/reserve thresholds remain.

Source quirks remain explicit: hidden target abilities are guessed per script
instruction unless history or trapping reveals them; `AI_TypeCalc` in switching
reads species types while trapping and `TypeCalc` read live types; duplicate best
move ties, stale-current-move replacement damage, active stats and byte best damage
are retained. This is a bounded original singles adapter, not a new strategy or a
claim to implement every possible trainer AI flag.

## Verification

- `run-final.log`: 11 groups, 144 assertions, zero failures, actual .NET 10.0.12.
- All 18 original gym team members and both Sheila/Celia natural Metronome singles.
- Nine independently derived pinned-source Muk score/RNG vectors, including toxic
  targets and capped defensive stages. The oracle hashes both original source files
  and never calls the product implementation or executes reference scripts.
- Real source Metronome → Transform, original Clefairy identity, copied Snore and
  Sleep Talk scoring, live Conversion types, Natural Cure, Perish Song, item gates,
  EXP-first replacement and atomic negative cases.
- Historical Cerulean AI regression: 624 checks, zero failures.
- Earlier failed fixture/build attempts remain in `evidence`; no golden was rewritten
  to fit the product output. The first source vector emission was derived exclusively
  from pinned source and independently passed against the C# adapter.

Run from the repository root, using the pinned installed toolchain and local package
source. No full solution, network access, native window or audio check is involved.

```sh
source scripts/env.sh
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SourceTrainerAi.Tests/Game.SourceTrainerAi.Tests.csproj -m:1 -nr:false -p:RestoreSources="$FIRERED_TOOLCHAIN_ROOT/godot/Godot_v4.7.2-stable_mono_linux_x86_64/GodotSharp/Tools/nupkgs"
flock /tmp/firered-remake-build.lock dotnet run --project tests/Game.SourceTrainerAi.Tests --no-build -- "$PWD"
python3 tests/Game.SourceTrainerAi.Tests/source_vectors.py "$FIRERED_ORIGINAL_REFERENCE"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.CeruleanTrainerAi.Tests/Game.CeruleanTrainerAi.Tests.csproj -m:1 -nr:false -p:RestoreSources="$FIRERED_TOOLCHAIN_ROOT/godot/Godot_v4.7.2-stable_mono_linux_x86_64/GodotSharp/Tools/nupkgs"
flock /tmp/firered-remake-build.lock dotnet run --project tests/Game.CeruleanTrainerAi.Tests --no-build -- "$PWD"
```

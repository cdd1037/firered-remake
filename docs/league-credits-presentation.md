# League presentation source reconstruction

The Hall/save/completion/revisit application transactions are unchanged. Scene effects use only presentation state; confetti has a private source-style LCG seeded by challenge number, so it cannot consume gameplay RNG or alter saved history. Its precise random sequence is deliberately not claimed to match the original shared-RNG game.

Pinned source: pret/pokefirered 037335f4c725d7c9aecdac87066f2002b4bd7e14, src/hall_of_fame.c, src/credits.c, src/overworld.c, src/trig.c and the referenced graphics/credits inputs. Import recipes retain source provenance; runtime uses generated PNG/JSON only.

Implemented source presentation:
- Hall background BG1/BG3 additive 16/7 palette mix, priority-aware sprite border crossings and nine-step border fade; source RGB(22,24,29), coefficient-12 previous-member palette blending. Hall text uses shared archived-party detail formatting for national number, personality gender, nickname, level and original trainer ID.
- Hall confetti every fourth countdown frame while remaining >110, 17 shapes, source LCG, source sine lookup, per-frame random horizontal amplitude, one/two-pixel vertical motion and bounded lifetime.
- Credits half-rate map camera rather than quarter-rate, source scroll direction/start positions and duration cap; 36..124 central window and 10/16 BG darkening.
- Authored character/ground variants across the five scenes, rival rise, initial player entry, original six/eight-frame cadence and clipping to the credits window.
- A compact 480x336 atlas of the twelve authored Pokemon zoom windows, four palette-specific Pokeball backgrounds, the source affine white-circle background, staged image changes and delayed cry. Credits text uses the source menu header color, outgoing/incoming palette-fade stages and printer dispatch delay. The final player walks left during the slow copyright transition, followed by the distinct copyright/The End reveal fades and white fade before the existing reset command.

Validation command:
`python scripts/imports/presentation/import-pokemon-league-presentation.py --inherited-root . --check`
Checks all generated outputs against conversion of pinned local inputs; 140 raw inputs, 74 outputs, 66 credits commands. Tests: PokemonLeagueScenePresentationTests covers camera cadence/cap, runner roles/entry, zoom/affine boundaries, confetti spawn/lifetime and dropped-frame equivalence. Compiler/runtime execution belongs to integrated validation, not the importer result.

Remaining fidelity gate: no native visual inspection performed for this change. Exact palette-transition/command dispatch frame accounting still requires frame-trace comparison and native verification. Current mon timing represents the authored stages but is not a frame-trace-certified port of all setup/task transitions. Do not label this the complete original scene or full-game visual acceptance.

Integrated check (2026-10-16, .NET SDK 10.0.401 / runtime 10.0.12):
- `source scripts/env.sh; dotnet build src/Game.Godot/Game.Godot.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false`: passed, 0 warnings / 0 errors, 35.97 s.
- `dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --filter FullyQualifiedName~PokemonLeagueScenePresentationTests`: 4 passed, 0 failed, 0 skipped.
- Earlier integrated attempt failed on simultaneous QuestLog `BattleStats.Hp` references. Its owner corrected these to `MaxHp`; the commands above are the successful rerun, not a relabeling of that failure.
- `git diff --check` on the modified League view/importer: passed.

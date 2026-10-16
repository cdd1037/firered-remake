# Native MSTest migration — 2026-10-15

The previous MSTest entry exposed whole console executables as single tests.
Ordinary C# scenarios now execute directly in the test host and have their own
test methods or data rows. The later dependency-layout pass split the initial
single test assembly into eight MSTest projects. Current paths, responsibilities
and commands are in [tests/README.md](../tests/README.md).

The Audit example is split into `SaveTests`, `CommandTests`,
`CanonicalHashTests` and `RuleValidationTests`, with shared fixture/storage
helpers in `AuditFixtures.cs`. Other areas keep their scenario names. Tests use
standard assertions and per-test setup rather than registration lists, custom
filters, exit codes and a second result accumulator.

All 354 moves have four independently discovered actor rows (1,416 public turns).
The ordinary cry catalog has 386 independently discovered render rows. Repel,
stone families, Everstone and lifecycle variants use separate data rows. Test
counts now refer to methods/data rows; they are not comparable to the old CLI's
manually incremented assertion counts.

Only resource imports and cold-process hash determinism start child processes.
The latter builds the small `tests/Helpers/HashProbe` executable transitively
and checks three distinct process IDs and their canonical state hashes. Ordinary
game/audio tests do not launch test DLLs or the audio CLI.

Removed: the executable regression adapter, `suites.json`, 21 per-area game
project files/locks, duplicate audio self-test implementations in the CLI, old
console-test dispatch commands and suite-only JSON/XML result plumbing. Build
outputs remain ignored. Source-vector generators and committed independent
fixtures remain; no expectations were regenerated to match this migration.

## Validation

- Full native run: 2,379 passed, zero failed/skipped, 4m 1s. Command:
  `dotnet test --no-build --logger "trx;LogFileName=final.trx" --results-directory .toolhome/mstest-migration/final`.
  Local report: `.toolhome/mstest-migration/final/final.trx`.
- A selected Audit concurrency method ran exactly one test, with a deliberately
  invalid Python path. A `MOVE_TACKLE` name filter ran four actor rows. A
  `SUPER_REPEL` row filter ran one test. An unmatched filter returned exit 1.
- Targeted migration checks: 424 passed, zero failed/skipped, including fixed
  fixture paths, resource import output paths, process hashing and cry catalog.
- After the full run, audio preparation code was deduplicated and explicitly
  prepared frame/sequence state was retained in four echo/TIE cases. All 574 audio
  cases passed again (15s); report:
  `.toolhome/mstest-migration/audio-verified/audio.trx`.
- Final empty-initializer removal and complete 5/48/48 rollout/Disable/multihit
  fixture guards were verified by 68 passing affected cases (4s); report:
  `.toolhome/mstest-migration/fixture-verified/fixtures.trx`.
- `dotnet build --no-restore -m:1 -nr:false` built the complete solution and hash
  helper with zero warnings/errors. Local log:
  `.toolhome/mstest-migration/full-build.log`.

The first migration run is retained under `.toolhome/mstest-migration/first-run`:
six failures identified fixture paths, a pre-created importer output directory,
two pan scenarios relying on earlier mutations, and a retired suite counter.
Assertions were preserved; each behavioral case now prepares its own inputs.
The superseded audio deduplication failure report is also retained under
`.toolhome/mstest-migration/audio-final`; it is not acceptance evidence.

## Scope

The former catalog CLI depended on the absent research file
the retired phase-4 audio render-coverage report. Historical PCM-hash
equivalence was outside the maintained default suite and is not claimed here.
Current catalog tests cover fixed metadata, invalid requests, rejection atomicity,
finite rendering, completion/clipping and exact cache accounting. The old optional
`PAN_ORACLE` CLI switch is also retired; its external research comparison was not
part of the default regression. Existing numerical fixtures and literal audio
expectations remain unchanged.

Natural full-story routes, visual comparisons and actual speaker output remain
separate acceptance work. Offline audio assertions do not establish native output.

## Dependency layout follow-up

Mixed Domain, Growth/Capture, Integration, Content, Weather/Screen, Wild Battle
and audio sequence files were split by behavior. Shared fixtures remain local
to their feature; projects share only the small source-linked `TestAssets` helper
and common MSBuild properties. Audio internals are exposed only to `Game.Audio.Tests`.
The hash helper shares its scenario with `Game.Process.Tests`, without linking
save-storage fixtures or adding MSTest to the executable.

Current validation is recorded in `docs/test-layout-20261015.md`. Earlier run
commands and counts above describe the initial native migration.

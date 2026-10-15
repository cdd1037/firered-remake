# Test maintenance — 2026-10-15

The suite-level adapter below is superseded by the
[native MSTest migration](mstest-migration-20261015.md). Current commands and
inputs are in [tests/README.md](../tests/README.md). The earlier 31-suite results
below remain historical validation of the entry-level adapter.

## .NET test entry

The complete .NET entry passed **31/31**, with zero failures or skipped tests,
in 7 minutes 9 seconds:
`dotnet test --logger "trx;LogFileName=regression.trx" --results-directory .toolhome/dotnet-test-results`.
The native TRX report is `.toolhome/dotnet-test-results/regression.trx`;
suite logs are in `.toolhome/test-runs/20261015T0750462646574Z-dotnet/summary.json`.
`dotnet build --no-restore -m:1 -nr:false` also built all 29 solution projects,
including Godot and Tooling, with zero warnings and zero errors. Its local log is
`.toolhome/dotnet-full-build.log`.

The maintained entry is now `dotnet test`, using `Game.Regression.Tests` with
MSTest and the standard VSTest adapter. All 31 manifest entries have separate
discoverable test methods. Existing executable assertions remain intact; the
C# adapter supplies fixtures, starts the processes, enforces timeouts and records
exit codes/log attachments. Python runs only for six `ResourceImport` checks.
The Python suite dispatcher and redundant checkpoint verification wrapper were
removed. The solution now contains 29 projects including the adapter.

Direct NuGet restore uses nuget.org, including official Godot packages. The
optional `dev build` helper can also use packages from the local Godot installation.
There is no dependency on Python environment discovery for `dotnet test`.

Entry checks passed: discovery listed 31 cases; an empty filter returned exit 1;
a C#-only selection passed with a deliberately nonexistent Python executable;
two selected resource checks both failed with that missing interpreter, continued
execution and returned exit 1. Standard filters and TRX reporting replace custom
`--only` options; `FIRERED_TEST_CHECKPOINT` replaces the checkpoint CLI option.

## Validation

`python dev test` completed all 31 runs with zero failures. The current solution
built with SDK 10.0.102: zero warnings and zero errors. Local full-command logs and
suite outputs are in `.toolhome/test-runs/20261015T073107674583Z/summary.json`.
The move inventory exercised 1,416 public turns with 31,304 checks; the field-item
suite passed 417 checks over 81 primary command observations; offline audio passed
313 self-tests. All 17 changed/new Python files parsed successfully, the manifest
matched all 21 retained test projects, and an unknown suite selection failed.
After adding the final wild-suite empty-case assertion, `python dev build` again
completed with zero warnings/errors, followed by a passing targeted run:
`python dev test --no-build --only Game.CanonicalWildApplication.Tests`.
Its local report is `.toolhome/test-runs/20261015T073958975662Z/summary.json`.

## Changes and boundaries

The original 93 C# test projects were compiled together. Seventy-two referenced
retired production APIs and failed compilation; 21 remain in the current solution.
The per-project diagnostics and historical cases that were not replaced are in
[the retirement inventory](test-retirement-20261015.json). Removing these projects
does not establish equivalent coverage for their historical comparisons.

`python dev test` builds all 28 maintained solution projects and executes 31 runs:
21 game test executables, three additional move/lifecycle/Continue runs, the audio
self-test, and six Python importer/resource checks. The runner rejects undeclared
test projects, unknown suite selections and empty output, records every result,
and continues after individual failures. See [test inputs and scope](../tests/README.md).

Missing numerical fixtures were restored from independent Python reference
evaluators or bounded C reference functions. Ordinary tests consume committed
vectors; they do not regenerate expectations or download inputs. Explicit C
fixture generators now support installed Windows MSVC and Linux C compilers.
Application cases use the current checkpoint and preserve acquisition identities;
their field/HP/PP/EXP preparation is documented fixture setup.

The repaired application tests exposed two production defects:

- Seafoam inventory projection omitted supplemental key items and balls. Repel
  use could fail with a later-game inventory. Projection/restoration now preserves
  all pockets, and the field-item tests check unrelated items survive unchanged.
- Gideon's completed-quest dialogue used an alternate `MSGBOX_AUTOCLOSE` text,
  leaving field controls locked. The script's autoclose instruction now releases
  controls; the trainer suite checks re-entry, saving and duplicate-reward guards.

The late checkpoint does not recreate the missing historical pre-Gideon battle
proof. Old full importer pipelines, historical recordings, natural whole-story
routes, visual comparisons and native audio output are outside this automated
result. Audio self-tests validate offline synthesis, not speaker output.

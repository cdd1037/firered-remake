# Current runtime retirement

The maintained game is `firered-current-campaign-v1`. This slice deletes
historical runtime selection; unsupported saves fail explicitly. The original
runtime-retirement slice left Domain battle executors, source catalog factories
and trainer wiring unchanged. Their later canonical cutover and retirement are
recorded in [the current integration status](project-refactor-plan.md#2026-10-13-content-specialization-and-battle-owner-retirement).

## Removed runtime and tools

- `CampaignDiagnosticProfiles.cs`, its three historical rules selectors and
  historical machine/shop validation, plus `BindDiagnosticCatalog`,
  `CreateDiagnostic` and `RestoreDiagnostic`.
- All 13 standalone `Campaign*SaveUpgrade.cs` files, and only the trailing
  `CampaignSaveUpgrade` blocks in `CampaignCelio`, `CampaignFiveIsland`,
  `CampaignFourIsland`, `CampaignSixIsland`, `CampaignSeviiRuby` and
  `CampaignNationalDex`. Current source/story/battle declarations remain.
- `Game.Tooling.SaveUpgradeProfiles` and its 43 source/target entries.
  Old `save upgrade-*` requests return exit 2 with `save_upgrade_retired`.
- `CampaignWindowReplay` and `scenario replay-campaign-window`. Unknown or
  retired replay commands return usage/exit 2 without publishing files.
- 46 historical regional/feature `scripts/launch-*.sh` scripts and the
  historical `launch_campaign` dispatcher. Git retains their source history.
- Historical per-region `dev` runner branches. The maintained dispatcher shrinks
  from 1,014 to 140 lines and exposes only current product/contracts, current
  checkpoint/battle checks, toolchain setup/doctor, probe/export/P0 and supported
  Tooling commands. For example, `./dev test-forest` returns exit 2 with
  `unknown_or_retired_command` without running gameplay. Original runners remain
  reproducible from their historical Git revision.

## Remaining current consumers

- Godot uses `LoadCurrent`, `CreateCurrent` and `RestoreCurrent`.
- The public `CampaignSession` constructor and `Restore` are current-only
  convenience aliases. They cannot select a predecessor profile.
- `CurrentCheckpointConversion` is an offline selected-Celio converter. It
  accepts the exact old envelope rules ID, validates using the current semantic
  validator, writes a new destination, preserves payload/generation and verifies
  source bytes. This is not an automatic load path or a successor registry.
- Current launch preparation retains checkpoint arguments and official resource
  import. Probe, harness, Pallet and exported-host engineering launchers remain;
  the probe launcher explicitly passes `--probe`.
- Current authenticated regional Content construction, Domain ownership proofs,
  source importers and Godot regional presentation still have production
  consumers. Their names alone do not justify deletion in this slice.

## Default build/test and historical source

`./dev build` restores/builds all 17 projects in `FireRed.Current.slnx`: the five
product assemblies, current composition, shared lifecycle and trainer-policy
contracts, canonical wild/trainer Application suites, five foundational
Domain/transaction/story suites and the original content fixture verifier.
Current locked restore uses the installed, toolchain-hash-verified official
Godot package directory; it does not perform a network vulnerability lookup for
this offline-only package set.

`./dev test` runs eight contract suites: CurrentComposition, SourceLifecycle,
SourceTrainerPolicy and the five foundation suites, plus the content verifier.
The two canonical Application suites compile by default but run only through
`./dev test-current-battles CURRENT_CHECKPOINT OUTPUT_DIRECTORY`, which requires
a strict current checkpoint and a new output directory. The trainer suite also
requires its two exact private proof inputs documented in
[its README](../tests/Game.CanonicalTrainerApplication.Tests/README.md); passing
an unrelated current checkpoint does not replace those inputs. The dispatcher
checks their presence and refuses an existing output directory before either
Application suite runs.

`./dev test-current-checkpoints HANDOFF BILL NEW_OUTPUT` adds the two selected
real checkpoint checks. Authenticated source content must be available under
the project root, as for current gameplay. Missing fixtures are prerequisite
failures, not skipped passes.

The larger `FireRed.sln` remains an optional source-test index. Exactly 52 test
projects that compile against deleted campaign APIs are removed from it. Their
paths, triggering source files and reasons are in
[historical-test-projects.json](historical-test-projects.json). No test source, checkpoint or unique evidence file was deleted. The one project
descriptor with an explicit include of deleted `SaveUpgradeProfiles.cs` is kept
as `Game.ToolingUpgrade.Tests.csproj.archived`; there is no dangling active
Compile include or runtime stub. Other historical
fixtures may still compile but cannot demand an old runtime from the current-only
constructor. Named historical `dev` runners have been deleted from the current
dispatcher. Use the historical source revision to reproduce historical results.
No full historical solution or journey replay is an acceptance gate here.

After physical battle-owner retirement, the two source-wild migration comparison
suites preserve their original program/project/runner/lock files under `archive/`
with `.txt` suffixes and retain all existing evidence. Their prior passes are
historical; they are no longer current executable gates. See
[battle-owner retirement](r3-battle-owner-retirement.md).

## Fixed baseline and verification

`tests/Game.CurrentComposition.Tests/baseline-a52074b.json` was captured before
retirement from unmodified commit `a52074b3e30834ccf477447b4847113ffbc58c8d`.
The capture source and build are retained in `evidence/runtime-retirement`.
It pins authenticated content, seeded New Game state, exact original checkpoint
bytes at generations 47785/47819, full payloads and one public East-command
outcome for each. Tests read this fixed baseline; they never regenerate expected
values from the candidate. The original 57-check report under
`evidence/r1-current-composition/test-result.json` is unchanged historical evidence.

The candidate normal Godot and Tooling builds pass with zero warnings/errors.
The current checkpoint suite passes 67 checks on .NET 10.0.12, preserving the two
original files, both converted payloads and fixed command outcomes. CLI checks
cover conversion, destination/source collision, malformed envelope, double
conversion and both retired command refusals. The report in
`evidence/runtime-retirement/verification.json` records final commands and scope.
Headless startup does not establish native visual or audible-output acceptance.

These are the original runtime-retirement candidate results. The subsequent
combined 16-project build with Content specialization, both canonical Application
cutovers and Domain retirement passed with zero warnings/errors in 57.52 seconds.
Default current tests and the content verifier passed, as did 67 real-checkpoint
checks, 53 ordinary-wild checks/11 public commands and 524 trainer checks/494
public commands. These were actual combined-tree runs. See
[build evidence](../evidence/refactor-final-20261013/build.log) and
[the latest integration result and remaining field-dispatch cleanup](project-refactor-plan.md#2026-10-13-content-specialization-and-battle-owner-retirement).

The current field-item Application project also compiles by default. Run `./dev test-current-items CURRENT_CHECKPOINT OUTPUT_JSON` explicitly using the generation-47819 current checkpoint described in its [README](../tests/Game.CurrentFieldItems.Tests/README.md). This fixture-dependent gate covers current items and the repaired owned-catalog Candy admission; it is not part of the fixture-free default test command.

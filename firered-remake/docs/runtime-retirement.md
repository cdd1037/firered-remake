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

The maintained solution has fifteen projects: five game assemblies, two audio projects and eight dependency-scoped MSTest projects. A dedicated cross-process hash helper is built transitively. Game and audio cases run directly in MSTest, with independent discovery and filtering. Only Python resource checks and cross-process determinism start child processes. `dotnet test` is the single solution entry; see [test organization](../tests/README.md) for project-scoped commands. The console-suite registry, adapter project and per-feature executable projects were removed.

See [the maintained test contract](../tests/README.md). The 93-project audit found 72 projects referring to removed APIs; they were deleted with per-project diagnostics in [the retirement inventory](test-retirement-20261015.json). Historical comparisons were not rerun or replaced one-for-one. No legacy production dispatcher was restored.

Current trainer cases consume a validated current checkpoint. Completed Gideon re-entry is distinct from the missing historical pre-Gideon victory/conversion proof.

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
[the integration result and subsequent field-dispatch closure](project-refactor-plan.md#2026-10-13-content-specialization-and-battle-owner-retirement).

Current field-item and stone-evolution cases are native MSTest tests included by default. Their current checkpoint inputs and controlled setup are documented in [the test contract](../tests/README.md); the old `dev test-current-items` console command is retired.

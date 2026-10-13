# Current runtime retirement

The maintained game is `firered-current-campaign-v1`. This slice deletes
historical runtime selection; unsupported saves fail explicitly. It does not
change Domain battle executors, source catalog factories or trainer wiring.

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

`./dev build` restores/builds `FireRed.Current.slnx`: the five product assemblies,
current composition and shared lifecycle contracts, five foundational Domain/transaction/story suites
and original content fixture verifier. Current locked restore uses the installed, toolchain-hash-verified official Godot package directory; it does not perform a network vulnerability lookup for this offline-only package set. `./dev test` runs those seven contract
suites plus the content verifier. `./dev test-current-checkpoints HANDOFF BILL
NEW_OUTPUT` adds the two selected real checkpoint checks. Source content must
be available under the project root, as for current gameplay.

The larger `FireRed.sln` remains an optional source-test index. Exactly 52 test
projects that compile against deleted campaign APIs are removed from it. Their
paths, triggering source files and reasons are in
[historical-test-projects.json](historical-test-projects.json). No test source, checkpoint or unique evidence file was deleted. The one project
descriptor with an explicit include of deleted `SaveUpgradeProfiles.cs` is kept
as `Game.ToolingUpgrade.Tests.csproj.archived`; there is no dangling active
Compile include or runtime stub. Other historical
fixtures may still compile but cannot demand an old runtime from the current-only
constructor. Existing named historical `dev` runners remain evidence workflows,
are omitted from current help, and are not promised runnable against current
assemblies. Use the historical source revision to reproduce historical results.
No full historical solution or journey replay is an acceptance gate here.

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

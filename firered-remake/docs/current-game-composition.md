# Current-game composition and retired runtime profiles

The normal game is the accepted Celio composition, with one save rules ID:
`firered-current-campaign-v1`. This does not claim that unfinished optional content,
League rematches or Cerulean Cave are implemented. The authenticated source and
owned/opponent admission limits remain unchanged.

## Ownership

- `GameStartupSelection` in Application parses the host's argument/environment
  snapshot once. It is BCL-only and does not read a process environment itself.
- Godot `Bootstrap` chooses either Current or one explicitly requested engineering
  diagnostic and passes the resolved selection to `CampaignView`. The view loads
  `PretCampaignCatalog.LoadCurrent`, then creates/restores only current sessions.
- Content `LoadCurrent` is the sole public catalog factory and directly builds
  the authenticated current resource set. The generic `Load` API and all 22
  regional public profile wrappers are removed. Typed regional construction
  dependencies remain source proofs, not launch choices. See
  [Content specialization](current-content-composition.md).
- Application `CampaignCatalogBinding` validates once per session construction and
  fixes campaign, wild and encounter rules plus the owned battle catalog. Current
  binds directly; New Game and Restore use the same catalog validation. Restore
  validates state and creates a command session without entering a map or drawing RNG.
- Domain stays BCL-only, with no Content or Godot references. Content specialization
  preserves admission factories and ownership/opponent authentication. The separate
  [R3 retirement](r3-battle-owner-retirement.md) removes the old battle executors
  after current Application callers move to the canonical frame.

The normal path no longer calls the latest-to-earliest catalog loader chain,
regional `ProfileRequested` properties, the old constructors or diagnostic binding.
It also no longer recomputes rules selection in getters or repeats the land
encounter version chain in initialization, validation and encounter checks.

## Startup rules

Run `bash scripts/launch-current.sh [current-checkpoint.json]`. The current save
folder is `.play/current`, separate from the original Celio files.

The default is current. `--profile=current`, `--profile current`,
`--story-current`, `--story`, and `FIRERED_PROFILE=current` select the same game.
If CLI and environment both name a profile, they must agree. Neither silently
wins. Conflicting repeated CLI values fail. Empty/unknown profile values fail.
The previous `--story-REGION` selectors and nonempty historical regional/feature
variables fail with explicit retired-selector errors, including values `0`.
The only supported compact-map setting is unset or `1`.

`--probe`, `--harness`, and `--pallet` are explicit engineering diagnostics. They
cannot be combined with each other or a current profile request/checkpoint.
These modes are not historical campaign launch profiles. Non-profile runtime
settings, such as audio, player entry and evidence output, keep their own owners.

The launcher sets one profile and no predecessor feature flags. The 46 historical
regional/feature launch scripts and their shared fall-through dispatcher have
been removed. Current, probe, harness, Pallet and exported-host launchers remain.
`launch-probe.sh` now explicitly selects the engineering probe.

## Save boundary and offline checkpoint contract

The envelope schema remains `SaveCodec.CurrentSchema == 1`; the explicit new
rules ID is the compatibility boundary. Current strict loads reject old Celio,
other historical and unknown rules IDs as `save_version_mismatch`. Normal startup
never converts a save or reinterprets an old payload. Old source files must remain
backed up and byte-identical.

The one-time offline converter may accept only the selected original Celio
checkpoints. Strictly decode using `CampaignSession.CelioRulesVersion` and the
original authenticated ContentVersion and the current semantic validator. Encode the same full
payload and generation with `CurrentGameProfile.RulesVersion` into a new path,
refusing an existing destination. Strictly decode and `RestoreCurrent` the output.
Record both file hashes and verify full canonical payload equality, including
party, HP/PP, inventory, money, flags/variables, map, battle history and both RNG
streams. No multi-profile upgrade router or automatic startup conversion is added.

Current APIs:

- `PretCampaignCatalog.LoadCurrent(string root)`
- `CampaignSession.CreateCurrent(ICampaignCatalog, CampaignNewGameOptions? = null)`
- `CampaignSession.RestoreCurrent(ICampaignCatalog, CampaignState, long revision)`
- `CampaignSession.IsCurrentGame`

## Historical runtime retirement

`CreateDiagnostic`, `RestoreDiagnostic`, `CampaignDiagnosticProfiles`, diagnostic
catalog binding and all historical rules/machine/shop selectors are removed.
The public constructor and `Restore` remain current-only conveniences; both
reject a catalog without authenticated Celio composition. There is no runtime
path from a historical save's rules ID to a historical session.

The 43-entry Tooling upgrade router, 13 standalone Application upgrade files,
six trailing upgrade blocks in mixed gameplay files, and the early-lab
`CampaignWindowReplay` implementation/command are removed. The mixed files keep
their current story, source validation, battle and progress logic. The sole
checkpoint conversion is the explicit offline Celio-to-current command; it
strictly decodes the named old envelope and validates using current rules.

The original 57-check report remains historical evidence unchanged. The retained
current suite now checks absence of diagnostic factories, current-only alias
rejection, strict saves, selected conversion and immutable source/state/movement
hashes captured from commit `a52074b3e30834ccf477447b4847113ffbc58c8d` before
retirement. It never recreates a historical runtime to compute expected results.
See [retirement inventory and command scope](runtime-retirement.md).

Domain's `OwnedBattlePokemonCatalog` still proves predecessor relationships for
current encounter/trainer factories. Removing its historical branches requires
moving those actual current consumers and is not done by weakening authentication.
The sole current Content builder still constructs authenticated predecessors. The current
presentation's regional capability checks and machine-art proof chain remain;
they no longer decide startup. Removing a predecessor requires moving its actual
current consumers without weakening source authentication. Current trainer wiring
and ordinary battle ownership have their own completed implementation boundaries;
see [the latest integration status](project-refactor-plan.md#2026-10-13-content-specialization-and-battle-owner-retirement).

No historical full-route or full-solution suite is required for this slice.
`Game.CurrentComposition.Tests` covers startup conflicts, current-only API
rejection, strict saves, the exact generation-47785/47819 source payloads,
side-effect-free Restore, short public movement parity and malformed ownership,
opponent, machine and shop bindings. A normal Godot build and short headless
startup additionally exercise the real composition host. Headless checks do not
establish native visual or audible-output acceptance.

## Integrated checkpoint conversion

The normal Godot and Tooling targets were built after integration with zero warnings/errors. `Game.Tooling convert-current-checkpoint SOURCE NEW_DESTINATION` converts only the backed-up Celio envelope into the current rules boundary. It strictly validates before publishing a flushed new file and refuses an existing destination. `scripts/check-current-checkpoint-conversion.py` exercised five actual CLI cases: valid47819 conversion, existing-target refusal, source-as-target refusal, malformed-envelope refusal before any target, and refusal to re-convert a current file. Every case preserved the original bytes; no temporary staging file remained. Full payload/generation equality and current output SHA598f8a51a736773c82d75b1ea47a987ec3424aae695f9c73a7baf6c1072c3a3b match the independent R1 current-save result. Evidence: `evidence/current-composition-20261013/conversion-run01/result.json`.

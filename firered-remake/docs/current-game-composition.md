# Current-game composition: R1 slice

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
- Content `LoadCurrent` binds the authenticated Celio resources. Its regional
  construction dependencies remain source-proof construction, not launch choices.
- Application `CampaignCatalogBinding` validates once per session construction and
  fixes campaign, wild and encounter rules plus the owned battle catalog. Current
  binds directly; New Game and Restore use the same catalog validation. Restore
  validates state and creates a command session without entering a map or drawing RNG.
- Domain stays BCL-only. No Content or Godot references, battle executors, admission
  factories or ownership/opponent authentication rules are changed by this slice.

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

The launcher sets one profile and no predecessor feature flags. Old regional
launcher scripts no longer select a playable historical campaign: the resolver
refuses their retired flags. Their source/evidence retirement belongs to R4.

## Save boundary and offline checkpoint contract

The envelope schema remains `SaveCodec.CurrentSchema == 1`; the explicit new
rules ID is the compatibility boundary. Current strict loads reject old Celio,
other historical and unknown rules IDs as `save_version_mismatch`. Normal startup
never converts a save or reinterprets an old payload. Old source files must remain
backed up and byte-identical.

The one-time offline converter may accept only the selected original Celio
checkpoints. Strictly decode using `CampaignSession.CelioRulesVersion` and the
original authenticated ContentVersion/diagnostic validator. Encode the same full
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

## Explicit remaining R1 scope and removal conditions

`CreateDiagnostic`/`RestoreDiagnostic` isolate historical resolution. The old
public constructor and `Restore` are temporary aliases, not normal-game entrypoints.
Their concrete remaining consumers are `CampaignSaveUpgrade` and its regional
partial classes, `Game.Tooling.SaveUpgradeProfiles`, `CampaignWindowReplay`, and
archived regional test fixtures that instantiate `CampaignSession` directly.
The aliases and `CampaignDiagnosticProfiles` must be removed when the upgrade
router is retired, replay is current-only or archived, and the retained fixtures
are ported to a standalone diagnostic harness. Historical saves alone do not
justify retaining them. R4 is assigned the old upgrade/replay retirement; this
slice does not add a new historical runtime commitment or claim R1 is complete.

Domain's `OwnedBattlePokemonCatalog` still proves predecessor relationships for
current encounter/trainer factories. Removing its historical branches requires
moving those actual current consumers and is not done by weakening authentication.
Regional Content loaders still construct authenticated predecessors. The current
presentation's regional capability checks and machine-art proof chain remain;
they no longer decide startup. Their consolidation should follow their real
consumers, separately from R2 trainer wiring and R3 battle ownership.

No historical full-route or full-solution suite is required for this slice.
`Game.CurrentComposition.Tests` covers startup conflicts, current/diagnostic
separation, strict saves, the exact generation-47785/47819 source payloads,
side-effect-free Restore, short public movement parity and malformed ownership,
opponent, machine and shop bindings. A normal Godot build and short headless
startup additionally exercise the real composition host. Headless checks do not
establish native visual or audible-output acceptance.

## Integrated checkpoint conversion

The normal Godot and Tooling targets were built after integration with zero warnings/errors. `Game.Tooling convert-current-checkpoint SOURCE NEW_DESTINATION` converts only the backed-up Celio envelope into the current rules boundary. It strictly validates before publishing a flushed new file and refuses an existing destination. `scripts/check-current-checkpoint-conversion.py` exercised five actual CLI cases: valid47819 conversion, existing-target refusal, source-as-target refusal, malformed-envelope refusal before any target, and refusal to re-convert a current file. Every case preserved the original bytes; no temporary staging file remained. Full payload/generation equality and current output SHA598f8a51a736773c82d75b1ea47a987ec3424aae695f9c73a7baf6c1072c3a3b match the independent R1 current-save result. Evidence: `evidence/current-composition-20261013/conversion-run01/result.json`.

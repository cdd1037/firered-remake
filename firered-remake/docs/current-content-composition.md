# Current Content composition

`PretCampaignCatalog.LoadCurrent(string projectRoot)` is the sole public catalog
factory. It directly builds the fixed current resource set in `PretCurrentGame.cs`.
The generic `Load` API, its 60 booleans and 24 alternate roots, and all 22 regional
public profile wrappers are removed. The private constructor requires typed
resource inputs without feature booleans or optional defaults.

This removes historical runtime composition choices. It retains the typed source
schemas, authentic factories, story validation, canonical hashes and regional
owned/opponent/shop/machine predecessor proofs used by current content. Those
proofs are not independently selectable game profiles. The large private typed
constructor remains a possible structural improvement; this change introduces no
untyped resource table or new admission shortcut.

## Current consumers and boundaries

Godot `CampaignView` and Tooling `CurrentCheckpointConversion` already call
`LoadCurrent`. `PretCampaignCatalog.cs` retains current field/story access, and
its Old Rod composition and source-symbol resolver use the fixed current path.
The 22 regional files lose their obsolete loader declarations while retaining
required typed resource construction.

Current session construction, strict saves and the narrow offline converter keep
the [current rules boundary](current-game-composition.md). Unsupported historical
rules IDs fail explicitly; active battles remain invalid player saves. Historical
test source that calls removed loaders is archived evidence, not justification
for restoring public profile selection. Current test consumers must use
`LoadCurrent` and obtain any required authenticated typed predecessor from it.

## Candidate evidence and integration status

The isolated candidate was based on `2ea82db105c2bc61d445230f17c7132e39684681`.
Its Content, Application dependencies and focused current-test build passed with
zero warnings/errors under SDK 10.0.401 and actual runtime 10.0.12. The candidate
patch SHA256 is `98c5f2dba995d45eb0decf9d75a5ab06da1ca479f18f077fa6d8fe1e70d17cff`.
The [candidate report](../evidence/current-content-specialization-20261013/REPORT.md)
and [verification summary](../evidence/current-content-specialization-20261013/verification-summary.json)
retain exact scope and reproduction commands.

- 67 retained current checks passed against the immutable `a52074b` baseline,
  including New Game, strict save/Continue, old-rules and authenticated-capability
  refusal, and selected checkpoints at generations 47785 and 47819. Original
  checkpoint bytes remained unchanged.
- All 22 complete characterization fields matched before/after, covering 254 maps,
  cells/borders/events/warps, scripts/text, catalog and encounter data, pickups,
  machine/shop/mart data, New Game payload and five object-identity checks.
- All 322 harvested `MAP_*` source-symbol cases matched their prior resolution or
  refusal. Reflection found only the public `LoadCurrent(string)` factory.
- Independent static review found no blocker and confirmed the original current
  order of 156 hash contributions, 36 map mutations, 38 validators and 258 retained
  factory/validation calls. Removed alternatives were unreachable from current
  composition. Diff/apply checks and evidence JSON/XML parsing also passed.

The Content fingerprint remains
`abe2347f42398636fa1a38c7fb4514f4375f69dfe17e4c3af640962b9b6736a0`;
the New Game hash remains
`2b95e8e91e6146384a6b136f92ec9cf9a4d20b789de67ef902d92c1d12eac340`.

Those results belong to the isolated candidate. After Application cutover
`870f3fb`, the combined tree with Content specialization and Domain retirement
also passed its 16-project normal build with zero warnings/errors in 57.52
seconds, default current contracts, 67 real-checkpoint checks, the 53-check
ordinary-wild Application smoke and 524-check trainer Application suite. See
[combined build evidence](../evidence/refactor-final-20261013/build.log) and
[the complete refactor status](project-refactor-plan.md#2026-10-13-content-specialization-and-battle-owner-retirement).

The subsequent current field encounter/fishing/item cleanup and Moon/Sun Stone
closure are complete in [the final bounded result](project-refactor-plan.md#2026-10-13-final-bounded-refactor-result); sole-loader retirement is not a claim that every
historical branch is gone. No historical full solution or regional route was
replayed, and no resource importer was run for this Content change. Native visual
and physical audio acceptance are outside this slice.

## Cerulean Cave successor, 2026-10-13

The single current composition now appends the authenticated Cave maps and Mewtwo
proofs: 257 maps, 139 owned species, 163 opponent species and 244 field pickups.
Its fingerprint is
`8ad0e9c765ae8b9f50977c171ed975e2aa55810032eeddb0806ac8077a857713`.
The preceding fingerprint above remains historical refactor evidence. The
expanded current contract passed 102 checks without rewriting that baseline or
the named original checkpoint payloads. Current field items passed their 396-check
contract against the explicitly converted checkpoint. See [composition test
scope](current-composition-test-scope.md) and [Cave acceptance](cerulean-cave-implementation.md).

## Upgraded League successor, 2026-10-13

The sole current composition now includes all seven authenticated upgraded
trainer teams and five new opponent-only species. Counts are 257 maps,139 owned,
168 opponents,244 pickups; the previous Cave proof remains an authenticated
predecessor. The current fingerprint is
`ec57b1b5a50c8e9f49dd6ef5d1090a03888dc9915ff0880afa0f658aca42ec96`.
The final133 current-composition checks passed, including unchanged seeded New Game
payload, independent deterministic loading, strict Continue and malformed
rematch binding rejection. No historical baseline was rewritten. The source
flag selects upgraded parties within the one current trainer registry and
shared execution path; there is no new selectable regional runtime profile.
See [rematch acceptance](league-rematch-campaign.md).

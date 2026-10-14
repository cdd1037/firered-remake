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

## Power Plant implementation checkpoint, 2026-10-13

The sole current composition appends the original Power Plant without changing
Route 10: 300 maps, 152 owned species, 186 opponent species, 354 moves,
123 items, 41 machines and 264 field pickups. The two Electrode item-ball objects
remain scripted battles. Zapdos, ordinary walking encounters and Thunder Stone
use the shared current execution and ownership services. A single authenticated
regional pickup reader now covers both the existing Cave and Power Plant source
contracts; it preserves each predecessor's source counts and checks.

This is a recoverable implementation checkpoint. The original map/resource suite
passed 1,992 checks, catalog/policy passed 457 plus the 460-check rerun that
adds actual indoor walking→pending→generation→shared battle→completion, and the combined Godot project
built without warnings or errors. The original-pixel comparisons and 23-check
audio/loader probe are independent source/PCM evidence. The Application run05 has now completed 1,141 public commands from the
disclosed Route 10 shoreline setup, including Surf, Pokémaniac Mark, all seven
pickups, both Electrode outcomes and one existing Ultra Ball capturing Zapdos
into the PC at generation51140. The resulting strict save is valid; a separate
process Continue and the new production view batch remain pending at this
checkpoint. Lower-layer passes are not substitutes for those checks. Native pixels and physical audio remain
unverified. See [Power Plant scope](power-plant-campaign.md).

The follow-up Power Plant closure has now passed independent process Continue
at51140, all four separate static-outcome probes (84 public commands), and
81 production-headless checks/four Draws. The sole startup repair makes the
Seven Island asset loader inspect its own authenticated predecessor rather
than the expanded current species count. The exact source and claim limits
remain in [Power Plant acceptance](power-plant-campaign.md#final-bounded-acceptance-2026-10-13).

## Trainer Tower implementation, 2026-10-13

Current composition adds eleven original map IDs with 27 format-selected layouts:
311 maps, 152 owned species, 204 enemy species and 124 items. Source-only recovery
uses four pinned Tower importers. Factory/Domain and loader checks pass; the
initial real lobby entry is saved at 51141. Full four-course application and
presentation acceptance remain pending. See [current Tower scope](trainer-tower-campaign.md).


## Fossil laboratory successor, 2026-10-13

The single current composition adds the original Entrance and Experiment Room after Dojo: 314 maps, 158 owned species and 190 ordinary opponents (Tower roster204 separately). Two source level40 evolution edges and164 compatibility cells extend the existing41 machines. No predecessor runtime is revived. Content fingerprint `67397acae6d36ea1b7beeaa98c7bc57c6a17b6abc3de141ebf8a9bd530838ed2`. Public route and strict receipt boundaries are documented in [fossil lab acceptance](fossil-lab-campaign.md).


## Old Amber successor, 2026-10-13

The same fossil package now adds Aerodactyl to159 owned species and item354 to125 inventory definitions; maps314 and ordinary opponents190 are unchanged. The separate Tower roster remains204. All41 machine rows retain identity, with205 fossil cells. The original Museum map/actor scripts are reused. Content fingerprint `86e36a116df5b2bf16abfa6b918f29c8862da35d551886d51c11a62d0b92fda2`. Museum acquisition and the second fossil receipt are covered by the [Old Amber route](old-amber-campaign.md).


## Shared teaching-service rooms, 2026-10-14

The sole current composition now includes the original Two Island Move Reminder
house and Fuchsia Move Deleter house, for329 maps. Both reuse the authenticated
single-room composition validator; map merging preserves predecessor objects,
original reciprocal warps, and source actor/script identity. The final owned
catalog binds deletion after resource loading. The shared teaching surface
supports both services without adding a regional runtime profile. The current
content fingerprint is `4ce9d194223a381fb2e8378a909e33ec2a2845a6a3d3845aacdb7020650d9697`.
See [Reminder](move-reminder-flow.md) and [Deleter](move-deleter-flow.md) for their
separate current-flow, strict-save and headless presentation evidence and limits.


The subsequent existing-map tutor increment keeps329 maps and162 owned species,
and admits original indices1/5/11/13 through one generated actor-binding table
and shared executor. The final authenticated source lock expands to16 files;
its current fingerprint is `ac6087eb18d240b0d2574ddf3ea2ef8561ebdb71f678f35b38556d6f93a45e57`.
The prior room fingerprint above identifies the separate Deleter proof cohort.
Three real-party lessons and stable Continue are documented in
[original tutor integration](original-move-tutors.md).

# Current content composition

`PretCampaignCatalog.LoadCurrent(projectRoot)` consumes `content/generated` without
reading the raw pret checkout. Importers own source acquisition and conversion.
See [resource layout](resource-layout.md) and [import tools](../scripts/README.md).

## Shared definitions

- Canonical species, moves, abilities and form mechanics have one definition.
  Current playable ownership and encounter availability remain explicit; an entry
  in the canonical catalog alone does not make a species obtainable.
- Shared items and machines supply definitions. Regional resources describe actual
  acquisition, shop stock and presentation. The 50 TMs and seven usable HMs retain
  their ordering and compatibility; HM08 data does not admit teaching HM08.
- Trainers reference shared definitions, retaining encoded names, numeric IDs,
  party order and real creation parameters. Derived move lists are computed from
  the shared learnsets or explicit custom slots, not stored as duplicate proofs.
- Breeding, NPC trades, held items and battle mechanics are explicit immutable
  data, without catalog-owner registries or predecessor reconstruction chains.
- Older damage/AI helpers use the current explicit mechanics and original trainer
  AI program; regional species counts do not select implementations.

## Runtime boundaries

Immutable definitions are validated on load; transactions validate current player
state, capacity, legal actions and atomic changes. Catalog lengths and provenance
commits are not permission gates. Resource filenames, dimensions, map identities,
encounter rates and actual source-script dispatch keys are meaningful data.

Runtime provenance inventories, unused manifests, conversion receipts and fixed
verification flags have been removed. Names containing `source` are not removed
blindly: topology source endpoints and script/event dispatch paths still have
runtime meaning. Original acquisition recipes stay in import tooling.

Generated resources have six functional roots. Most single-document packages are
plain files, map files are flat inside regions, and opening/player/NPC art is
shared. A few service packages still colocate item data and graphics; these are
explicit residual layout debt, not required historical compatibility layers.

## Checkpoints and verification limits

The current content version is `firered-current-content-v4`; campaign rules remain
`firered-current-campaign-v3`. Use the explicit offline
`convert-current-checkpoint SOURCE NEW_DESTINATION` tooling command for older
checkpoints. Normal Continue does not silently migrate schemas. Save integrity
and all actual player state remain part of the save contract.

Game completeness is separate from this cleanup; see [scope audit](full-scope-audit.md).
Not all optional content or dedicated move animations are implemented. Native
checks use Dummy audio and do not establish audible output. Some original audio
cue recipes are absent, so a complete cold resource rebuild is not claimed.

## Measured cleanup baseline

Against the preceding local revision `53435c7`, generated JSON decreased from
20,792,637 to 16,962,939 bytes (1,241 to 1,192 files). All generated files total
22,098,167 bytes. The intermediate directory-layout baseline had 806 directories;
the measured layout has 327, with six top-level categories instead of 363.
These are on-disk source-resource sizes, not exported game or GPU-memory sizes.

Independent Release measurements taken before the final directory migration showed
fresh-process content-load allocation falling from about 547 to 408 MiB. Warm
420-slot storage operations fell from about 659 to 300 KiB allocated per operation.
These are managed headless measurements, not frame-rate or complete Godot startup
benchmarks. The final nested-field cleanup was not assigned invented timing gains.

## Verified local checkpoint

The final serialized solution build completed with zero warnings and errors.
The eight maintained MSTest suites passed 2,646 cases with zero failures or
skips, summed from their TRX counters: Application 128, Audio 574, Battle 1587, Content 177, Domain 144, Presentation 15, Process 1, ResourceImport 20.

Godot 4.7.2 loaded the current checkpoint from a generated-only resource root.
Map/character rendering, movement and the right-click menu were checked after
the final schema cleanup; Bag, Key Items and all seven HM rows in TM CASE were
also checked during the same layout integration. The final native run logged no
errors and closed normally. Audio was Dummy, not a listening test.

The import checks include real raw-to-runtime regeneration and pixel comparisons.
Five presentation importers now compare JSON types/values/array order and PNG
dimensions/RGBA for --check; irrelevant object-key ordering and PNG compression
do not cause false failures. Other binary formats remain byte-exact.

The governance batch additionally exercised unreadable primary-save recovery via
Continue, visible permission failure, and a successful retry in an isolated native
window. The existing verified backup remained byte-identical. No real player save
was modified. Runtime command hashes and default detailed evidence were removed;
explicit diagnostics and save integrity remain. See [quality review](project-quality-review.md)
for remaining work and measurement boundaries.

A subsequent governance integration built the complete solution with zero warnings
or errors, then ran all affected Domain (163), Application (147), Content (181)
and ResourceImport (21) cases: 512 passed, no failures or skips. Battle/Audio
were not rerun for those non-battle/audio changes. Ordinary settled wild-history
retention and its remaining special-ledger limitations are documented in
[wild history retention](wild-history-retention.md).

## Current ownership rules (campaign v3)

Released Pokemon and ribbon-recipient proof lists are no longer persisted.
Acquisition/evolution sources are retained for current ownership and active work;
finite gift/trade completion, independent Hall of Fame snapshots, Pokedex facts
and cumulative statistics survive an entity leaving. IDs remain monotonic.

The explicit prior-rules checkpoint conversion preserves RNG, generation and the
original input file. The current representative checkpoint is 196,808 bytes,
compared with 205,104 bytes after the preceding battle-history reduction. This
8,296-byte difference is the ownership cleanup's incremental change, not the
entire reduction from the older 315,347-byte file.

Final affected-project verification: Application 202/202 and ResourceImport 23/23,
zero failures or skips; complete solution build zero warnings/errors. Other focused
groups overlap and are not added to those totals. See [retention contract](wild-history-retention.md)
for remaining finite receipts and unrelated long-running-history limits.

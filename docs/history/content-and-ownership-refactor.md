# 内容与所有权重构：历史测量和验证

本页保存早期内容、资源布局与所有权重构的测量。原记录引用的是当时来源修订，不能按历史测试总数或“final”字样认定当前版本已通过。当前定义见[内容组合](../current-content-composition.md)，最新检查见[验证记录](../validation.md)。

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
explicit diagnostics and save integrity remain. See [quality review](../project-quality-review.md)
for remaining work and measurement boundaries.

A subsequent governance integration built the complete solution with zero warnings
or errors, then ran all affected Domain (163), Application (147), Content (181)
and ResourceImport (21) cases: 512 passed, no failures or skips. Battle/Audio
were not rerun for those non-battle/audio changes. Ordinary settled wild-history
retention and its remaining special-ledger limitations are documented in
[wild history retention](../features/wild-history-retention.md).

## Ownership retention introduced in campaign v3

Released Pokemon and ribbon-recipient proof lists are no longer persisted.
Acquisition/evolution sources are retained for current ownership and active work;
finite gift/trade completion, independent Hall of Fame snapshots, Pokedex facts
and cumulative statistics survive an entity leaving. IDs remain monotonic.

The historical representative checkpoint measured 196,808 bytes,
compared with 205,104 bytes after the preceding battle-history reduction. This
8,296-byte difference is the ownership cleanup's incremental change, not the
entire reduction from the older 315,347-byte file.

Final affected-project verification: Application 202/202 and ResourceImport 23/23,
zero failures or skips; complete solution build zero warnings/errors. Other focused
groups overlap and are not added to those totals. See [retention contract](../features/wild-history-retention.md)
for remaining finite receipts and unrelated long-running-history limits.

# Settled wild battle retention

## Current boundary

`NextWildBattleId` is the monotonic allocator, incremented at battle creation. It no longer depends on retained array length. Saved receipts have canonical positive numeric IDs, strictly increasing below that allocator; the most recent settlement remains available to the Lostelle callback's last-settlement query. No aggregate counter, audit hash, ring buffer, or old-format guessing is added.

Compaction runs only when a successful command closes a wild battle, Safari battle, or evolution. It is deferred while any of those flows remains active. Ordinary movement, menu commands and ticks perform no history scan. The existing capture-delivery, reward and return guards still see the active receipt. Capture receipts and wins referenced by completed evolutions remain in original order.

The pure `CampaignWildHistoryRetention.Compact` operation can also be applied explicitly to an already validated stable checkpoint. Full old arrays naturally satisfy the new identity contract. There is no schema change or automatic rewriting of player files; encoding a compacted state uses the existing save checksum and semantic validation.

## Ordinary encounters

- Untyped wild, fishing, SourceTable, Four/Five/Six Island, Sevii Ruby, Victory Road, non-static Cerulean Cave and Safari records without capture/evolution references are removed after settlement. The latest callback record remains until superseded.
- Rock Smash, Seafoam, Power Plant and Seven Island retain the first sighting per source family and species. These bounded representatives satisfy existing SeenBy and source-access validators. All capture and evolution references are retained separately.
- For Unown, the original first sighting is retained. The first capture is retained too and still takes precedence in the existing Pokedex personality validator. Repeated uncaught sightings do not append permanent history.
- Rock Smash, SourceTable and Seafoam validation checks retained receipts' individual generation and trainer identity, cursor bounds against the current world RNG, and `next.RngBefore.Cursor >= previous.RngAfter.Cursor`. It does not require adjacency or equality; removing nonreferenced intermediate receipts preserves this ordering. Celio's different individual-continuity ledger is excluded from this reduction.
- No encounter initiation rules, RNG transitions, capture source matching, current Pokemon state, Seen/Caught sets, or evolution requirements are changed.

## Explicitly unfinished special cases

Event Island retries, Celio roamer settlements, scripted Marowak/Snorlax/Lostelle, Mewtwo, Articuno and static Power Plant encounters retain their full special ledgers. Repeated special attempts can still grow. A later change must replace those consumers with their actual event state/latest attempt/Hall-of-Fame eligibility and authoritative live roamer state, with dedicated regression tests. This change does not claim that all history growth has been eliminated.

## Verification

The original generation 57604 checkpoint is read-only input. Tests exercise canonical IDs, 10,000 ordinary records, repeated ordinary regional/fishing retention, capture/evolution references, pending evolution, Unown first-sighting/first-capture ordering, actual checkpoint semantic validation, and SaveCodec roundtrip. Existing public battle tests retain stale-command rejection and exactly-once EXP checks, using battle identity rather than old audit-array length.

Verified on .NET SDK 10.0.401, runtime target net10.0:

```
source scripts/env.sh && dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-restore --filter 'FullyQualifiedName~WildBattles|FullyQualifiedName~Evolution' --logger 'trx;LogFileName=wild-history-retention-regions.trx' --results-directory /tmp/firered-wild-history-tests
```

Result: 32 passed, 0 failed, 0 skipped. Real checkpoint history: 634 → 71 records; canonical history JSON: 131,710 → 24,185 bytes. No real save was written. This is storage reduction, not a measured frame-time claim.

Retained source breakdown (overlapping receipt fields are not additive): SourceTable 5→1; PowerPlant 39→8; EventIsland 2→2; CeruleanCave 27→2 (both Mewtwo); SixIsland 10→0; FiveIsland 9→0; SeviiRuby 24→1; VictoryRoad 37→1; Safari 6→0; scripted Marowak, Snorlax and Lostelle each 1→1. Capture and evolution referenced ordinary records remain in the total.

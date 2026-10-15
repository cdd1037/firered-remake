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

## Special encounter retention

Event Island retains the latest attempt per Ho-Oh/Lugia/Deoxys kind. Existing event flags and the current Hall-of-Fame count continue to govern availability; latest outcome and its HallEntries govern flew-away completion validation. Older capture/evolution references remain independently. There is no attempt count or permanent retry log.

Mewtwo, Articuno and each Power Plant static actor retain their latest attempt. Their terminal outcome blocks another attempt at the existing source-script entry, so the final win/capture is naturally the latest record. These retained source receipts also satisfy SeenBy; a separate first-sighting record is unnecessary for species without an individual Pokedex representative. The accepted Mewtwo validator uses Any(Escaped) for hidden-but-not-fought states, so its last escape remains while those flags require it (at most one extra row).

Lostelle retains its latest attempt; a completed rescue prevents subsequent attempts at the existing event entry. Marowak retains its latest attempt and its latest victory if distinct, because Tower scene completion explicitly consumes a winning record. Snorlax remains at most one record per route under the unchanged source validator. The global latest settlement still owns Lostelle's callback LastOrDefault query.

Without capture/evolution references, retained attempts are bounded per event: Event Island 1/kind; Articuno 1; Power Plant 1/static kind; Mewtwo 1 (or 2 while its accepted hide reason requires a different escape); Lostelle 1; Marowak at most 2; Snorlax at most 1/route. Referenced evolution wins grow only with actual retained creature evolution lineage, not failed attempts.

## Explicitly unfinished special case

Celio roamer settlements still retain the complete individual-continuity ledger. A later change must validate the authoritative live roamer state and initialization identity without replaying every prior battle. This change does not claim all history growth has been eliminated.

## Verification

The original generation 57604 checkpoint is read-only input. Tests exercise canonical IDs, 10,000 ordinary records, repeated ordinary regional/fishing retention, capture/evolution references, pending evolution, Unown first-sighting/first-capture ordering, actual checkpoint semantic validation, and SaveCodec roundtrip. Existing public battle tests retain stale-command rejection and exactly-once EXP checks, using battle identity rather than old audit-array length.

Verified on .NET SDK 10.0.401, runtime target net10.0:

```
source scripts/env.sh && dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-restore --filter 'FullyQualifiedName~WildBattles|FullyQualifiedName~Evolution' --logger 'trx;LogFileName=wild-history-retention-regions.trx' --results-directory /tmp/firered-wild-history-tests
```

Result: 32 passed, 0 failed, 0 skipped. Real checkpoint history: 634 → 71 records; canonical history JSON: 131,710 → 24,185 bytes. No real save was written. This is storage reduction, not a measured frame-time claim.

Retained source breakdown (overlapping receipt fields are not additive): SourceTable 5→1; PowerPlant 39→8; EventIsland 2→2; CeruleanCave 27→2 (both Mewtwo); SixIsland 10→0; FiveIsland 9→0; SeviiRuby 24→1; VictoryRoad 37→1; Safari 6→0; scripted Marowak, Snorlax and Lostelle each 1→1. Capture and evolution referenced ordinary records remain in the total.

The special-event follow-on reduces the same original checkpoint further to 69 records / 21,467 canonical history bytes (Event Island 2→1, Mewtwo 2→1). The 634→71 figures above describe the preceding ordinary-encounter stage, not the final special-event result. The new `SpecialWildHistoryRetentionTests` expands Event Island, Mewtwo and Zapdos separately by 1,000 nonterminal attempts, validates each full model before reduction, then validates the compacted state and save roundtrip. These are explicitly constructed accepted models, not a claim of 3,000 battles replayed. Articuno's test generates 1,000 consecutive source individuals and verifies its existing RNG-order/latest-outcome consumer. Dedicated Hall-of-Fame renewal and Mewtwo hide-reason tests preserve existing completion rejection paths.

Final targeted command used the same filter with `--logger 'trx;LogFileName=wild-history-retention-special-final.trx'`: 38 passed, 0 failed, 0 skipped. Report: `/tmp/firered-wild-history-tests/wild-history-retention-special-final.trx`.

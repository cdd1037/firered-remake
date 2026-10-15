# Settled wild battle retention

## Current boundary

`NextWildBattleId` is the monotonic allocator, incremented at battle creation. It no longer depends on retained array length. Saved receipts have canonical positive numeric IDs, strictly increasing below that allocator; the most recent settlement remains available to the Lostelle callback's last-settlement query. No aggregate counter, audit hash, ring buffer, or old-format guessing is added.

Compaction runs only when a successful command closes a wild battle, Safari battle, or evolution. It is deferred while any of those flows remains active. Ordinary movement, menu commands and ticks perform no history scan. The existing capture-delivery, reward and return guards still see the active receipt. Capture receipts and wins referenced by completed evolutions remain in original order.

The pure `CampaignWildHistoryRetention.Compact` operation can also be applied explicitly to an already validated stable checkpoint. Full old arrays naturally satisfy the new identity contract. There is no schema change or automatic rewriting of player files; encoding a compacted state uses the existing save checksum and semantic validation.

## Ordinary encounters

- Untyped wild, fishing, SourceTable, Four/Five/Six Island, Sevii Ruby, Victory Road, non-static Cerulean Cave and Safari records without capture/evolution references are removed after settlement. The latest callback record remains until superseded.
- Rock Smash, Seafoam, Power Plant and Seven Island retain the first sighting per source family and species. These bounded representatives satisfy existing SeenBy and source-access validators. All capture and evolution references are retained separately.
- For Unown, the original first sighting is retained. The first capture is retained too and still takes precedence in the existing Pokedex personality validator. Repeated uncaught sightings do not append permanent history.
- Rock Smash, SourceTable and Seafoam validation checks retained receipts' individual generation and trainer identity, cursor bounds against the current world RNG, and `next.RngBefore.Cursor >= previous.RngAfter.Cursor`. It does not require adjacency or equality; removing nonreferenced intermediate receipts preserves this ordering. Celio validates its latest settlement separately against the authoritative current individual; it does not require adjacency between retained historical receipts.
- No encounter initiation rules, RNG transitions, capture source matching, current Pokemon state, Seen/Caught sets, or evolution requirements are changed.

## Special encounter retention

Event Island retains the latest attempt per Ho-Oh/Lugia/Deoxys kind. Existing event flags and the current Hall-of-Fame count continue to govern availability; latest outcome and its HallEntries govern flew-away completion validation. Older capture/evolution references remain independently. There is no attempt count or permanent retry log.

Mewtwo, Articuno and each Power Plant static actor retain their latest attempt. Their terminal outcome blocks another attempt at the existing source-script entry, so the final win/capture is naturally the latest record. These retained source receipts also satisfy SeenBy; a separate first-sighting record is unnecessary for species without an individual Pokedex representative. The accepted Mewtwo validator uses Any(Escaped) for hidden-but-not-fought states, so its last escape remains while those flags require it (at most one extra row).

Lostelle retains its latest attempt; a completed rescue prevents subsequent attempts at the existing event entry. Marowak retains its latest attempt and its latest victory if distinct, because Tower scene completion explicitly consumes a winning record. Snorlax remains at most one record per route under the unchanged source validator. The global latest settlement still owns Lostelle's callback LastOrDefault query.

Without capture/evolution references, retained attempts are bounded per event: Event Island 1/kind; Articuno 1; Power Plant 1/static kind; Mewtwo 1 (or 2 while its accepted hide reason requires a different escape); Lostelle 1; Marowak at most 2; Snorlax at most 1/route. Referenced evolution wins grow only with actual retained creature evolution lineage, not failed attempts.

## Celio roamer

Celio retains its latest settlement, with capture/evolution references handled by the same existing reference rules. The existing initialization and current-state validator owns fixed PID/IV/species/level identity, HP/status/active validity, route validity and the three-map player movement memory. Progress validation compares the latest settlement's resulting individual to the authoritative current individual; with no settlement it compares to the initialized individual. It no longer walks a chain from initialization through every historical encounter. The local settlement receipt is still checked by the deterministic settlement rule, including its relocation RNG, and its RNG range must fit initialization/current world bounds. Normal later movement may change runtime route without changing the individual.

Capture/KO/draw deactivate the only initialized roamer, so its normally retained settlement count is one, including a capture or evolution reference to that terminal settlement. Active battle receipt guards still prevent repeated settlement draws. Encounter admission still binds the candidate to the exact current individual. No new fields or gameplay transitions are introduced.

## Remaining acquisition-history growth

This work bounds repeated nonreferenced battle attempts. It does not bound the entire save. Capture/evolution acquisition records still include released creatures: release appends a full final PartyPokemon to ReleasedPokemon, HistoricalPokemon includes those retired objects, and lifetime acquisition-count validators require their capture/evolution sources. Repeated catching and releasing can therefore still grow the save. Retired creature/source cleanup is a separate model change involving current ownership, monotonic NextPokemonId, NPC exchange fulfillment, persistent Pokedex sets, Hall-of-Fame member snapshots and champion ribbons. No released data is removed by this change.

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

### Celio follow-on verification

The first Celio run (`wild-history-retention-celio.trx`) had 39 passed and 1 failed. The 1,000-settlement fixture assumed every source roaming route had a current land/surf encounter table; a valid relocation reached an unmapped route and `Single` failed. The fixture now performs bounded real `SourceRoamerRules.Transition(..., Warp, ...)` draws until a supported route is reached, failing if none is reached within 100 transitions. It does not overwrite the RNG or fabricate the next route.

Final command:

```
source scripts/env.sh && dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-restore --filter 'FullyQualifiedName~WildBattles|FullyQualifiedName~Evolution' --logger 'trx;LogFileName=wild-history-retention-celio-final.trx' --results-directory /tmp/firered-wild-history-tests
```

Result: 40 passed, 0 failed, 0 skipped (SDK 10.0.401, net10.0). The final report includes the measured fixture output:

- Celio settlements: 1,000 → 1.
- Whole battle-history array in that fixture: 1,634 → 69 records; canonical JSON: 1,763,779 → 21,741 bytes.
- Unmodified generation 57604 input remains 634 → 69 records / 131,710 → 21,467 bytes. It contains no prior Celio settlements, so this stage does not further reduce that original input.

The Celio fixture directly calls the deterministic source settlement and movement rules with controlled HP/status/outcomes; it is not a replay of 1,000 full public-command battles. Full save validation succeeds before and after reduction, and the compacted state roundtrips through SaveCodec. Current individual, world RNG and next battle ID are unchanged by compaction; the next restored encounter and next settlement remain identical. Separate checks reject HP/status disagreement with initialization/latest settlement and accept legitimate later relocation. Existing public Celio flee and capture/PC-delivery tests now start with two prior settlements and verify reduction to one after real public battle completion, including typed capture provenance and Continue validation.

No real player save was written. These results establish retained-history reduction and the tested gameplay/save invariants, not frame-time improvement or a bound on all acquisition/release history.


The final Celio cleanup replaces the local settlement's canonical-JSON hash comparison with direct structural equality: all `RoamerIndividual` record fields (including contest values), runtime location, and the ordered player-map history via `SequenceEqual`. Default history arrays are rejected. These are every instance field in `RoamerState` / `RoamerRuntime`; no generic serializer/equality framework or new stored proof is introduced. SaveCodec checksums remain unchanged.

After that replacement, `dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-restore --filter 'FullyQualifiedName~CelioHistoryRetentionTests|Name~public_Celio' --logger 'trx;LogFileName=celio-structural-equality.trx' --results-directory /tmp/firered-wild-history-tests` passed 4/4, with no failures or skips. This reruns the two Celio retention cases and the public flee/capture cases; it also rejects altered contest values, runtime locations, player-history entries and default history arrays. The earlier 40-case report predates this final comparison-only replacement; the final targeted report is `/tmp/firered-wild-history-tests/celio-structural-equality.trx`.

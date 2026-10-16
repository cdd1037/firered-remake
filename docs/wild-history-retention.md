# Wild history and current ownership

## State, not a lifetime proof log

`NextWildBattleId` is a monotonic allocator. Retained IDs are positive, ordered and
below the allocator; their array length does not determine the next ID. The latest
settlement stays available to the final battle callback. No discarded-record
counter, hash chain or fixed-size audit ring is stored.

Compaction runs at authoritative battle/evolution closure and after successful
release/NPC trade. It does not scan history during movement, menu commands or
ordinary ticks. Active capture delivery, reward, evolution and callback flows keep
the records they still need.

`NextPokemonId` likewise allocates IDs independently of lifetime acquisition
counts. Current ownership includes party, boxes, both daycares and the PC-held
Pokemon. Released entities are no longer a persistent ownership collection.

## What remains

- Capture and evolution sources needed by currently owned Pokemon.
- The most recent settlement needed by the active callback.
- Finite special-event state and the latest relevant attempt: Event Island,
  Mewtwo, Articuno, static Power Plant encounters and Lostelle. Mewtwo's accepted
  hidden-after-escape state may also retain one escape; Marowak may retain its
  successful victory. Snorlax retains its route-specific settlement.
- The roamer's authoritative current individual/runtime and latest settlement.
  Initial fixed identity, current HP/status and latest-settlement consistency are
  checked; old battles are not replayed from birth to prove the current state.
- Actual game facts: Seen/Caught sets, Unown representative personality, cumulative
  statistics, finite gift/trade completion, and the 50-team Hall of Fame snapshots.

Ordinary unused sightings and catch records for released/traded-away Pokemon are
not permanent evidence. A just-released catch may remain as the single latest
settlement until the next battle; this does not grow with repeated catch/release.
Unown's representative is retained directly and is not reselected from the
remaining history. Releasing a captured special encounter does not reopen it.

Finite NPC trade receipts remain transaction facts, not members of a global
historical-ownership universe. A completed gift remains received even after its
Pokemon leaves. Current egg/hatching callbacks still need their active data;
parent input snapshots do not require those parents to remain owned forever.

## Persistence boundary

The current campaign rules version is `firered-current-campaign-v3`; content remains
`firered-current-content-v4`. Normal Continue does not migrate an old rules version.
The explicit `convert-current-checkpoint SOURCE NEW_DESTINATION` command handles
only the preceding campaign-v2/content-v4 input. It preserves the source file,
NextPokemonId, RNG, generation, current entities, game facts and snapshots while
removing retired proof data, then validates the resulting current state.

SaveCodec checksums, staged-write byte comparison, valid-primary backup handling
and current transaction checks remain. The roamer's local state comparison uses
its typed scalar fields and ordered player-history values, not JSON hashing.

## Verification scope

Tests cover public capture/evolution/release and NPC trade flows, monotonic IDs,
current source retention, gift/hatching completion, independent Hall of Fame
snapshots and explicit checkpoint conversion. Controlled large-history fixtures
exercise repeated ordinary and special attempts plus deterministic roamer rules.
These fixtures are not thousands of full public-command battle playthroughs.

Before the current-ownership cleanup, the generation-57604 checkpoint's wild
history shrank from 634 to 69 records (131,710 to 21,467 canonical JSON bytes).
A separate fixture containing 1,000 roamer settlements retained one roamer record.
Those are measurements of the earlier battle-history reduction, not a new
measurement of the entire v3 save or a frame-rate claim.

The remaining finite transaction receipts and other gameplay histories must still
be evaluated by their real consumers. This document does not claim that every
possible long-running campaign collection has a fixed bound.

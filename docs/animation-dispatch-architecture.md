# Animation invocation and resource authority

The post-content audit identified two parallel authorities: a move-symbol/number support table in Application, and obsolete Physical/Elemental pack readers beside the maintained resource resolver. This refactor removes both rather than adding another compatibility route.

## Invocation boundary

`CampaignMoveAnimationPresentation` receives the actual `PokemonMoveDefinition` catalog and a predicate backed by the scene's resolved scripts. A definition supplies `SourceMoveId`; the resolved script must have both that ID and the corresponding source `Move_*` label. Missing definitions, invalid metadata, missing scripts, or a mismatched label fail closed. There is no copied list of admitted move IDs in Application.

Singles retain their existing exported event envelope. A small explicit adapter converts the receipt kinds consumed by invocation matching into canonical source receipts, including PP-owner identity. Unconsumed receipts retain sequence and turn. Both modes then use one invocation validator. Grouped/stat result callbacks continue to use their existing authoritative result validators and event-time projection; no fake double-battle frame or final-state reconstruction is created.

Real source distinctions remain explicit: doubles target revisits, setup-only Wish/Future Sight, self-destruction, primary status/recovery, and grouped stat timing. Per-hit scheduling and planned-hit bounds come from the source move effect (`MULTI_HIT`, `DOUBLE_HIT`, `TWINEEDLE`, `TRIPLE_KICK`) and still require an available matching script. Primary and grouped stat outputs also obtain numeric source identity from the supplied catalog.

## Resource boundary

`SourceAnimationResources` remains the product pack/dependency resolver. The removed Physical/Elemental catalog classes no longer supply parallel parsers, numeric `Supports` lists, or incomplete helper closures. Family callback partials remain source implementations, not separate pack readers. Positive rendering tests use the resolver-backed composition fixture; intentionally incomplete primitive tests remain explicit.

Application test fixtures read actual generated script IDs and labels and provide the current content catalog. Tests for missing metadata pass `null` explicitly; they do not receive an implicit fallback. New focused cases check single/double decision parity, foreign PP ownership, missing script support, missing definitions, and a valid-looking numeric ID pointing to another source root.

This is an authority/lifecycle refactor. It adds no move roots, gameplay receipts, RNG draws, save fields, or remote writes. Native UI and audible behavior require their own bounded acceptance; managed structural tests are not a substitute for that evidence.

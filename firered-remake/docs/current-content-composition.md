# Current content composition

`PretCampaignCatalog.LoadCurrent(projectRoot)` builds the game from
`content/generated`. Import tools read original sources; the running game does
not parse pret C/ASM or require the raw checkout.

## Business data

- `PretCanonicalPokemonCatalog` owns shared species, moves, abilities and form
  mechanics. Regional resources reference these definitions instead of copying
  full tables.
- `GamePokemonCatalog` explicitly defines the supported campaign rosters:
  189 owned species, 212 ordinary opponent species and 204 Trainer Tower species.
  Availability and supported evolution edges are separate from canonical data;
  having a canonical definition does not make a species obtainable.
- `SharedItemCatalog` supplies common item definitions. The current shop data
  defines 151 items and 14 stocks. `InventoryCatalog` also admits the actual
  quest/service items and machines and validates quantities and pocket rules.
- `MachineCatalog` contains the 50 TMs and seven usable HMs in their established
  display order. Compatibility is a species lookup, with no regional catalog
  ancestry. HM08 compatibility data does not make HM08 teachable.
- Breeding, NPC trades, held-item effects and battle mechanics are explicit
  immutable data. They are not recovered through object-identity registries.

## Runtime responsibilities

Resources are validated when loaded. Transactions validate the player's current
state, supported operations, quantities, capacity and atomic changes. Source
file paths, line numbers, historical table sizes and predecessor construction
order are not gameplay authority.

The presentation layer reads textures, atlas regions and cues, and uses current
business definitions for names and descriptions. Source import information
belongs in the tools, not in a duplicate runtime gameplay catalog.

## Current migration status

The October 15 consolidation passes the full solution build and all 2,606
maintained tests. Comparisons with the previous verified revision preserve all
three species rosters, moves, breeding data, current items and stocks, and the
57 machine definitions and compatibility values. The converted v4 checkpoint
loads from a generated-only resource root in Godot 4.7.2; map rendering,
movement, the field menu, Bag, Key Items and TM CASE (including all seven HMs)
have been checked graphically.
Audio used the Dummy driver in this check; this is not an audio-device test.

Game completeness is tracked separately in [the scope audit](full-scope-audit.md).
Data consolidation does not mean all optional content or dedicated move
animations are finished.

## Saved checkpoints

The current content version is `firered-current-content-v4`; campaign rules remain
`firered-current-campaign-v2`. Normal Continue does not accept older content schemas.
Use `dotnet run --project src/Game.Tooling -- convert-current-checkpoint SOURCE NEW_DESTINATION`
for an explicit offline conversion. For v3 checkpoints this removes only the obsolete
NPC-trade catalog fingerprint, retaining trade identity, received level and all gameplay
state. The original file is retained and the converted state must pass current validation.

## Remaining cleanup boundary

Some legacy battle helpers still contain regional species-count branches and
historical names. This consolidation removes their catalog-owner registries;
it does not yet rewrite every older damage/AI helper. Immutable resource-read
and JSON serializer-options caches are retained for their actual reuse benefit.
They are not used as proof that a game object is authorized.

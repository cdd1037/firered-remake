# Shared TM/HM catalog

`content/generated/machine-catalog/catalog.json` is the single definition and
compatibility source for machine views. It contains 57 admitted machine
**definitions**, plus one compact compatibility row for each of 386 real species.
It contains no region predecessor, acquisition-history, or cumulative roster
chain. Icons remain in their existing external resources.

## Typed schema

- `schemaVersion`: 1
- `sourceCommit`: the pinned local pret source commit
- `machines`: `PewterAdditionalMachine` fields `machine`, `move`, `sourceMoveId`,
  and `movePp`, without a repeated `compatibility` array
- `compatibility`: `species`, `sourceSpeciesId`, and `compatibleItems`
  for each real species; all IDs retain the existing `{ "value": "pret:…" }` form

Compatibility source-location proof is not part of the runtime schema. The
source species numeric ID is retained, as are every species/item identifier,
eligibility value, and ordered regional view. Source locations remain available
from the pinned raw inputs and are reconstructed only by provenance tests.

The absence of an item in a known species row means `CanLearn = false`. Unknown
item/species IDs are errors. A compatibility row covers all 58 source machines,
including HM08. The latter is deliberately absent from the 57 admitted machine
**definitions**, matching the existing game's 50 TMs and seven used HMs. The
source's party-menu table and item aliases both include HM08/Dive; the four
pre-existing Gastly/Haunter/Snorlax/Eevee extension cells for HM08 are retained.
No new acquisition, teachable inventory item, or field action is enabled.

The 46 remaining regional machine resource documents retain their ordered views through two
machine-specific shapes:

- Full machine view: `{ "item": "pret:ITEM_TM39", "species": ["pret:SPECIES_…"] }`
- Compatibility cell: `{ "item": "pret:ITEM_TM39", "species": "pret:SPECIES_…" }`

The initial Pewter `catalog.tm39` uses `{ "item": "pret:ITEM_TM39" }`.
`catalog.tmCase` resolves the shared item definition. These are typed machine references, not a generic
resource reference or runtime JSON transformation mechanism.

## Rebuild and verification

Run from `firered-remake`:

```sh
python scripts/import-machine-catalog.py --references-only
python scripts/import-machine-catalog.py --references-only --check
python scripts/test-machine-catalog-import.py
python scripts/test-machine-importers.py
python scripts/test-late-machine-importers.py
python scripts/test-shop-machine-importers.py
```

Generation reads the shared local raw source directly. It parses machine item
constants, item definitions, move mapping/PP, descriptions, real species IDs,
and TM/HM learnset tokens. It verifies that item aliases and the native party
menu map the same 58 machines. It does not run upstream programs, read a reference
Git checkout, or read older region outputs to build the shared catalog.

The small `scripts/import-config/machine-catalog.json` recipe retains existing
presentation decisions: item rather than move descriptions for
HM06/HM07/TM36 and the existing description wrapping. It also retains the 57
UI-facing machine IDs in current game order. Text values and all gameplay fields are still derived from raw input.
These choices preserve current behavior; this cleanup does not silently correct
presentation quirks.

Current regional importers use `scripts/machine_catalog.py` directly. Their
independent ordered admission selections live in
`scripts/import-config/machine-admissions.json`: The current selection covers 46 resource files after retiring the Pewter
presentation package’s former gameplay catalog. The recipe contains membership only, never copies of definitions
or compatibility values, and never names a predecessor catalog.

Both current rebuild drivers compile the shared source catalog first and run
a reference-only validation/canonicalization step before the final item views. They reject an importer
that emits legacy full definitions or compatibility cells. There is no expansion
bridge. `--check` never writes runtime files. The source compiler also defaults
to rejecting expanded input; the explicit flag makes the pipeline contract
visible. Existing full-value reconstruction exists only for exact parity tests.

## Exact migration evidence

`evidence/shared-machine-catalog-20261015/baseline.json` was captured once from
the original expanded resource values before conversion. Its canonical JSON
hashes cover every field and array order in all 47 files, including icons and
other fields outside machines. It is not a runtime asset authentication gate.

The original whole-document snapshot remains unchanged. A separately captured
`machine-values-baseline.json` preserves every original typed definition,
compatibility cell, source reference, and ordered view position. Tests reconstruct
the retired compatibility source locations from raw data and continue to verify
this original baseline unchanged. A separate `machine-gameplay-baseline.json`
was captured before source removal, omitting only compatibility `source` fields;
it verifies every current machine value and view position. Their exact assertions
run in tests, not as an ordinary regeneration gate. Removing unused `rulesSources` and
updating source-only input inventories intentionally changes whole-document
hashes without changing machine values. The machine baseline covers:
141 original full-machine occurrences, 17,896 compatibility cells, 10,600 unique
item/species pairs, and 188 previously represented species. There were no
conflicting definitions or compatibility/source values. The expanded source
catalog now describes all 22,388 possible 386-by-58 compatibility cells.

The thirteen focused Python checks cover regeneration, exact machine round-trip,
all raw learnset cells/source IDs, HM08 admission boundaries, presentation
preservation, conflicting definitions/cells, missing context, unknown IDs,
invalid source tokens, duplicate JSON/catalog keys, independent target
admissions, rejection of expanded importer output, source-free compatibility
schema, and the original source-inclusive baseline. Additional tests run 41
regional source converters using retained raw-byte fixtures and compare their
compact machine views with runtime. These fixtures do not claim reference Git
authentication or a full graphics/audio regeneration. The maintained .NET
resource-import suite invokes the checks through `MachineCatalog`.

Measured in compact UTF-8 JSON, the same 47 original documents total 4,624,272
bytes. Reference deduplication alone reduced them plus the shared catalog to 1,911,364
bytes (58.7% less). After removing unused source-proof arrays, the 47 documents
plus the catalog totaled 1,136,971 bytes (767,705 + 369,266). Removing only the
386 per-species compatibility source objects saves another 43,614 bytes, leaving
1,093,357 bytes (767,705 + 325,652), a net reduction of 3,530,915 bytes, or 76.4%.
`evidence/shared-machine-catalog-20261015/compatibility-source-removal.json`
records the before/after catalog hashes and exact proof-independent parity. All
47 regional JSON files are byte-for-byte unchanged by this latter removal.

## Current game composition

`SharedMachineCatalog.LoadCurrent(GamePokemonCatalog)` builds one direct
`MachineCatalog` with 57 ordered definitions and the current 189 owned species.
The shared generated `current` selection contains the ordered machine IDs, TM
Case reference/presentation, and real pocket limits. It contains no predecessor
profiles. Definition validation runs once during loading. Compatibility is a
species lookup; its rows use canonical source-species order rather than seven
historical construction orders. An external migration comparison verified all
10,773 current eligibility values and numeric species IDs, every machine's
business fields/description/move/PP, the UI-facing order, TM Case and limits.

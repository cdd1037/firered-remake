# Shared TM/HM catalog

`content/generated/data/machines.json` is the single source of runtime machine
definitions and compatibility. It contains 57 admitted machine definitions and
one compatibility row for each of the 386 real species. It has no source-commit
header, predecessor chain, historical count proofs or duplicate definitions.

## Runtime schema

- `schemaVersion`: 1.
- `machines`: machine item/description, move ID, numeric source move ID and PP.
- `compatibility`: species ID, numeric source species ID and compatible item IDs.
- `current`: the ordered machine selection, TM Case presentation and pocket limits.

Numeric source IDs are actual game identifiers. An absent item in a known
species row means the species cannot learn it; unknown item/species IDs are
errors. The compatibility table covers all 58 source TM/HM slots, including
HM08. HM08 is not an admitted definition and is not enabled for teaching,
acquisition or field use.

`SharedMachineCatalog.LoadCurrent(GamePokemonCatalog)` creates the current
machine catalog directly. It validates immutable definitions once during loading.
Eligibility uses the shared species rows; it does not replay historical region
extensions.

Regional selections use two explicit typed forms:

- Machine view: `{ "item": "pret:ITEM_TM39", "species": ["pret:SPECIES_…"] }`.
- Compatibility cell: `{ "item": "pret:ITEM_TM39", "species": "pret:SPECIES_…" }`.

These selections preserve actual machine order and admitted species. They are
not generic runtime JSON transformations.

## Resources and presentation

`data/machine-learning` contains direct files: `pewter.json`, `bill.json`,
`cerulean.json`, `ssanne.json`, `route11.json`, `vermilion.json`,
`department-machine-stock.json`, `acquisition-machines.json` and
`acquisitions.json`. Pewter supplies TM39/TM Case icons; the other early machine
views retain the selection and presentation fields their consumers use.
Inventory icons point directly to `sprites/items.png` with explicit rectangles.
There are no receipt manifests or nested one-file regional packages here.

Unused standalone compatibility-extension copies are gone. Their eligibility
values remain in the shared 386-species table. Actual current regional views in
other item/service resources keep their ordered selections.

## Import and verification

Run from `firered-remake`:

```sh
python scripts/imports/items/import-machine-catalog.py --references-only --check
python scripts/checks/test-machine-catalog-import.py
python scripts/checks/test-machine-importers.py
python scripts/checks/test-late-machine-importers.py
python scripts/checks/test-shop-machine-importers.py
python scripts/checks/test-single-file-imports.py
```

The shared compiler reads retained local raw data: machine item aliases,
item definitions, move mapping/PP, descriptions, species IDs and learnset tokens.
It checks the native party-menu mapping against all 58 machine aliases without
executing upstream programs or reading predecessor regional outputs.

Presentation choices live in `scripts/recipes/config/machine-catalog.json`.
Ordered item/species selections live in
`scripts/recipes/config/machine-admissions.json`. These are import tooling inputs,
not runtime resource dependencies.

Regional importers use `scripts/lib/machine_catalog.py` to emit compact typed
references directly. The rebuild drivers validate this boundary and reject
expanded importer output. `--check` never writes resources.

Tests independently compare all 22,388 eligibility cells and numeric species IDs
with retained source tokens, test the HM08 boundary and invalid inputs, and
verify each admission view. The single-file CLI regression regenerates starters
and early machine documents into an isolated tree, checks actual file outputs,
compares gameplay fields and decodes every emitted machine icon against its
current atlas rectangle. It does not claim full campaign or audio acceptance.

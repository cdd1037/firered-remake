# Shared item business catalog

`content/generated/item-catalog/catalog.json` owns all 307 real pinned-source
item definitions. The 375 source slots contain 68 `ITEM_NONE` placeholders;
those placeholders are not items. IDs, names, prices, pockets, numeric source
IDs, field/battle handlers, hold effects, secondary IDs and toss/sell/give
capabilities are defined once. Runtime regional resources contain stable item
ID references, never copies of those definitions or source-path proof records.

`SharedItemCatalog` resolves those references explicitly. Being known to this
catalog does not grant inventory admission or implement an item's effect.
The source compiler reads retained `src/data/items.json` and item constants;
runtime reads generated resources only.

## Current inventory and shops

`content/generated/current-items/catalog.json` is one flat current selection:
151 ordered item/use entries and 14 ordered Mart stocks. Its import recipe
contains item IDs, explicit supported-use choices and source stock labels.
Descriptions, effect bytes, medicine amounts, status bits, prices and stock
contents come from the source compilers. Existing machine-specific description
choices reuse the shared machine catalog. Empty effect arrays remain empty where
the game has not admitted that action, even when the original source has effects.

`CurrentItemCatalog.Load(root, items)` returns the current shop/use catalog
without reconstructing a historical sequence of regional catalogs. The game's
`InventoryCatalog` separately grants its complete admitted pocket scope,
including quest keys, berries and machines. A lookup in the 307-definition
catalog never broadens that scope. HM08 remains known source metadata and is
not added to the 57 admitted teaching machines.

Regional pickups, service quantities, rewards and transaction constraints remain
in their own semantic contracts. Opening Potion and source battle-item views
resolve the same shared definition; their executable effect data remains in the
relevant capability view. The item-effect source reader parses restricted integer
expressions without compiling or running upstream C.

## Rebuild and tests

```sh
python scripts/import-item-catalog.py --check
python scripts/import-current-items.py --check
python scripts/source-battle-items/import.py --check
python scripts/test-item-catalog-import.py
python scripts/test-current-items-import.py
python scripts/test-machine-catalog-import.py
```

Both rebuild drivers compile the shared item source first, then the final current
item selection and source battle capabilities. Tests verify independent raw
business values, strict identity references, bounded admission, retained deferred
use, exact effect bytes and regeneration. Migration comparison against the
original composed game was performed outside the repository: all 151 item
business/use/description values, item order, 14 stock arrays and limits matched.
The current profile name replaces a historical predecessor profile.

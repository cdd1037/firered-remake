# Hidden pickup binding

`PretWorldHiddenPickups` projects hidden-item backgrounds from the selected generated world, resolving item definitions and numeric flags through the shared item/constants catalogs. Regional item contracts contribute visible item balls only; they no longer select which hidden backgrounds are playable. Runtime never reads the raw source tree.

Ordinary pickups use the existing atomic inventory/script transaction, pocket capacity checks and one-time flag. `ITEM_NONE` retains the source hidden-coin behavior and quantity. Underfoot backgrounds retain their source bit regardless of regional flavor support, so ordinary interaction cannot bypass Itemfinder requirements. The existing Itemfinder contract owns the special acquisition flow.

Current source selection contains 183 hidden backgrounds: 165 ordinary items, 12 coin backgrounds and six Itemfinder-only backgrounds. These are descriptive totals, not admission gates. Route24/25 berries, Ether and Elixir are included through the same world projection, as are previously omitted backgrounds on other ordinary maps.

`WorldHiddenPickupTests` checks selected-event/catalog parity and metadata, plus public ordinary pickup transactions, one-time behavior and strict save roundtrips. Controlled test positioning does not establish a naturally replayed full-game route.

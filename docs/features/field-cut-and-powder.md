# CUT grass and Berry Powder

Source: pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`.

## Field CUT

`fldeff_cut.c` checks Dotted Hole, then the facing Cut tree, then terrain 1 at the player's elevation in a centered 3×3 square. `import-cut-grass.py` reads the original nine-entry metatile table and uses the shared map renderer for replacement art. No map admission list or Emerald-only grass/flower rules are added. The replacement keeps elevation and clears collision, as `MapGridSetMetatileIdAt` does. Movement, encounter eligibility and battle terrain use the resulting shared sparse metatile edit layer. CUT consumes no PP and increments its source game statistic once.

Grass changes live in the current map grid. `PrepareFieldSave` retains only the source 15×14 viewport (x −7…+7, y −7…+6). Camera connections translate that overlap using bounded direct-neighbor coordinates; a warp rebuilds the map. There is no persistent history of cleared old maps. The same coordinate resolver projects border edits for movement and three-layer map art. Normal tree/Dotted Hole behavior retains priority.

`field_cut_grass_ack` is a correlated presentation callback. The common show-Pokémon cue precedes the eight source grass particles and SE_M_CUT; these are bounded host presentation phases, not a claim of GBA task/VBlank timing.

## Powder Man / Powder Jar

Cerulean House 5 uses its compiled original script. Interest and Berry Pouch gates, decline/repeat conversation, the ordered twelve-row list (including exit), confirmation/cancel and capacity checks stay script-owned. List labels retain the source CLEAR_TO/FONT_SMALL price controls, rendered by a generic label presenter. The fixed source seven-row window and amount display are supported.

The gift flag is set before the source giveitem routine. That ordering is preserved, including the capacity-failure branch; this is not a claim that a full key pocket is reachable before the gift in ordinary play. The shared item-capacity transaction returns failure without adding an item.

Berry Powder is a bounded 0…99,999 balance, default zero. No singleplayer earning path is invented. A zero balance cannot buy any of the eleven source rewards. The amount display preserves source balance semantics. Wireless earning and the four herb rewards that require it are outside this local zero-balance task; their labels come from the existing generated battle-item catalog. The Jar displays the amount from the Bag or registered-item callback and returns to the corresponding origin.

## Reproduction

- `python scripts/checks/test-cut-grass-import.py`
- `python scripts/checks/test-story-menus.py`
- `FieldGrassAndPowderTests`, `ConnectedFieldCoordinatesTests`
- Existing `NoncombatFieldMoveTests`, `BadgeTopicListMenuTests` and regional metatile regressions

The runtime reads generated resources only. Raw source and PNG inputs belong to the import boundary.

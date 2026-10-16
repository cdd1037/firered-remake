# Current world resources

The current world is an explicit selection of 395 ordinary maps and 19 real layout variants. `data/world-maps.json` records source names, exceptional stable IDs, layout choices, and presentation differences. Group names organize output packing; they are not predecessor stages. `scripts/lib/world_selection.py` derives repeated IDs and filenames. All source geometry, ordered events, map symbols, tiles, palettes, and graphics are read from the pinned raw tree.

## Build

```sh
python scripts/imports/maps/import-world.py --raw content/raw/pokefirered/037335f4 --out content/generated
python scripts/imports/story/import-early-field-rules.py --raw content/raw/pokefirered/037335f4 --out content/generated
python scripts/imports/story/import-world-field-rules.py --raw content/raw/pokefirered/037335f4 --out content/generated
python scripts/imports/story/import-field-services.py --raw content/raw/pokefirered/037335f4 --out content/generated
python scripts/imports/trainers/import-cinnabar-trainers.py --raw content/raw/pokefirered/037335f4 --out content/generated
```

The world command writes the schema-3 compact maps, `maps/world.json`, complete field metatile tables, the map atlas, and the shared overworld sprite catalog. It verifies atlas pixels before removing temporary per-map PNGs. Compact documents retain logical image paths resolved by the atlas. The command always compiles the complete selected world and its presentation resources together. `--check` performs an isolated full conversion and compares final JSON values, PNG pixels, and binary streams.

The field commands compile actual gameplay contracts, encounters, constants and interactive art. They do not render base maps, copy predecessor resources, produce catalogs of prior regions, or persist source script/geometry inventories. There is no historical region preparation step.

## Runtime

`PretCurrentGame` loads one `PretCurrentWorld` and explicitly passes it to feature loaders. Only `maps/world.json` selects current maps and real variants. `PretWorldMapConnections` projects connections through the same source-symbol registry. Presentation reads each map's index binding and uses the shared NPC catalog. The old region catalogs, cumulative catalog chain, separate opening border package, regional NPC inventories and forwarding map import commands have been retired.

## Selected scope

The source map-group registry lists 425 names. The existing raw tree contains 414 map headers. These 11 registered names have no retained header and are outside the current single-player selection:

- `BattleColosseum_2P`, `BattleColosseum_4P`, `RecordCorner`
- `Prototype_SeviiIsle_6`, `Prototype_SeviiIsle_7`, `Prototype_SeviiIsle_8`, `Prototype_SeviiIsle_9`
- `Route19_UnusedHouse`, `Route23_UnusedHouse`, `Route6_UnusedHouse`, `SevenIsland_UnusedHouse`

Of the retained headers, the following 19 are explicitly unselected, preserving the existing supported world:

- Pokémon Center 2F in Cinnabar, Five Island, Four Island, Fuchsia, Indigo Plateau, One Island, Pewter, Route 4, Saffron, Seven Island, Six Island, Three Island, Two Island, Vermilion and Viridian
- `TradeCenter`, `UnionRoom`, `SaffronCity_Connection`, `SevenIsland_House_Room2`

No directory enumeration implicitly admits these maps. No missing headers were fabricated or copied from generated resources. Variant selection preserves base MapId/events and explicitly names a compatible raw layout: Trainer Tower double/knockout floors, Seafoam stopped-current floors, and the dug-out Dunsparce tunnel.

## Independent verification

```sh
python scripts/checks/test-world-pipeline-oracle.py --generated content/generated
python scripts/checks/test-map-consolidation.py
python scripts/checks/verify-map-importer-unification.py
python scripts/checks/test-world-field-rules.py
python scripts/checks/test-pokemon-tower-features.py
```

The world oracle independently checks raw block/border binaries, metatile attributes, dimensions, ordered events, script endpoints, and business-sensitive layout differences. An optional temporary `--baseline` captures old consumed final resources for semantic/pixel comparison; it is never a compiler source or a committed historical hash inventory. Migration comparison covered all 414 map documents, 3,296 logical RGBA images through the final atlas, 126 NPC graphics with 1,088 frames, and the walking-player texture. Opening borders were checked against all 20 previous logical border images.

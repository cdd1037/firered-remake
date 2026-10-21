# Current world resources

`data/world-maps.json` is the authored selection of ordinary maps and genuine
layout variants. It records source names, exceptional stable IDs, layout choices
and presentation differences. Group names organize output packing, not predecessor
stages; `scripts/lib/world_selection.py` derives repeated IDs and filenames.
The compiler reads geometry, ordered events, symbols, tiles, palettes and graphics
from the pinned raw tree. Do not admit maps by directory enumeration.

## Compilation and runtime

Use `python scripts/imports/rebuild-resources.py world`, or add `--check` for an
isolated comparison. Input/output flags, dependent field rules and story commands
are maintained in the [reconstruction guide](resource-reconstruction-guide.md).
The world target always compiles the whole selected world with its presentation
resources, not an incremental chain of former regions.

It writes the compact maps and `maps/world.json`, complete field metatile tables,
shared overworld sprites, and [map-render atlases](map-render-atlases.md). Fresh
map PNGs are checked against the encoded atlas before staging images are removed;
map documents retain logical image references. Render format and runtime boundaries
are defined in [resource layout](resource-layout.md).

`PretCurrentGame` loads one `PretCurrentWorld` and passes it to feature loaders.
Only `maps/world.json` selects runtime maps and variants. `PretWorldMapConnections`
uses the same source-symbol registry. Field-rule/service producers compile their
actual gameplay contracts and interactive art separately; they do not render base
maps or copy predecessor catalogs. A map being selected does not prove all of its
story interactions or natural routes are complete.

## Real variants and exclusions

Variants preserve the base MapId, original event header, NPCs and warps while
selecting a compatible raw layout. Do not merge by MapId alone:

- Trainer Tower floors retain single, double and knockout layouts.
- Seafoam lower floors retain stopped-current layouts.
- Three Island's Dunsparce tunnel retains its dug-out layout.

The source registry includes names without retained map headers: Battle Colosseum
2P/4P, Record Corner, prototype Sevii Isles 6–9, and the unused houses on Routes
6/19/23 and Seven Island. Missing headers are not fabricated from generated data.
Other retained-but-unselected headers include Trade Center, Union Room,
`SaffronCity_Connection`, `SevenIsland_House_Room2`, and Pokémon Center 2F in
Cinnabar, Five Island, Four Island, Fuchsia, Indigo Plateau, One Island, Pewter,
Route 4, Saffron, Seven Island, Six Island, Three Island, Two Island, Vermilion and
Viridian. Selection remains explicit; raw presence alone does not extend scope.

## Independent verification

```sh
python scripts/checks/test-world-pipeline-oracle.py --generated content/generated
python scripts/checks/test-map-consolidation.py
python scripts/checks/verify-map-importer-unification.py
python scripts/checks/test-world-field-rules.py
python scripts/checks/test-pokemon-tower-features.py
```

The world oracle independently decodes raw block/border binaries and metatile
attributes, and checks dimensions, ordered events, script endpoints and
business-sensitive layout differences. Selection expansion is shared, but the
raw decoder and final-atlas unpacker are independent of the compiler. This keeps
source mistakes detectable instead of only comparing a producer with itself.

Optional `--baseline` reads independently retained prior final resources for
semantic and full-RGBA comparison, including transparent RGB. It is verification
input only, never compiler input, a required predecessor package, or a committed
historical hash inventory. A generated-only repack proves consistency, not
independent source fidelity. Neither establishes full gameplay or audio acceptance.

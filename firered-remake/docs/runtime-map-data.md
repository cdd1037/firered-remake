# Runtime map data

The generated-resource refactor on 2026-10-15 uses one map format. Every map is a `runtime-map.json` with schema 2 and converter `firered-runtime-map-v2`. The runtime no longer reads `world.json`, reconstructs embedded source documents, or selects a rich-format fallback.

## Stored data

Each compact map retains its stable ID, source map path/symbol/commit, dimensions, PNG layer references, optional technical spawn, and complete event header. Original little-endian 16-bit blocks remain packed in canonical base64; both collision bits and all elevation bits are preserved. Unique metatile rows retain the original attribute words and attribute-file indices. Behavior names and all decoded attributes remain available through `GetSourceCell` when needed.

Script paths are import provenance only. Runtime programs and texts load from `story/runtime.json`; no original script is opened during game startup. Script and text bodies are not duplicated in generated maps. Map-level source metadata remains available; per-cell source records are reconstructed from the metatile table only on request.

Complete events stay in each map because they are runtime inputs. Seafoam stopped-current layouts and Trainer Tower battle-format layouts keep their explicit existing directory names and original event headers. Maps are never merged solely by stable ID.

The 15 cumulative map catalogs contain direct paths relative to `content/generated`; they no longer contain copied map payloads. A single `map-source-registry.json` supplies the original base-map source inventory. Region loaders read generated metadata only. Five Pallet `border.json` files now contain map references, repeat rules and PNG references; geometry and blocks come from the compact map, and field behavior/attributes come from the compiled `field-tilesets.json`. Image references resolve through the regional render atlas.

## Regeneration

Map renderers produce only compact runtime maps. Their temporary in-memory source cells are checked against original block/attribute bytes before conversion. `scripts/map_runtime.py` owns that conversion; `scripts/runtime_map_manifest.py` creates cumulative catalogs without copying map payloads. `scripts/import-pallet.py` also creates the shared base source registry.

Run the applicable source importer first, then its catalog builder. The base catalog can be rebuilt or checked with:

```sh
python3 scripts/export-runtime-maps.py
python3 scripts/export-runtime-maps.py --check
python3 scripts/export-runtime-maps.py --resolve map.pallet-town 6 7
```

The source lookup reports the compact map path, original attribute-file path and byte offset, raw block, collision/elevation, attributes and behavior. It does not require a rich development map.

## Bounded verification

The migration compared all 1,464 old runtime payloads, including blocks, borders, attributes, behaviors, event data and artifact references. Every live script body matched its shared raw file. Conversion checked 93,992 rich map/border cells against original bytes. Variant paths were retained whenever their complete payload differed.

The prior 156 `world.json` files used 80,957,734 bytes, and the 1,464 runtime copies used 15,793,206 bytes. The resulting 386 canonical maps and explicit variants use 2,425,743 bytes. These figures exclude PNGs, other region metadata and the shared raw tree. The previous 15 identical source registries are now one file. Machine-readable migration evidence is in `evidence/map-format-unification.json`.

The format change does not change gameplay IDs, rules, events or saved-game fields. Current New Game and generation-57604 Continue checks are owned by the enclosing resource-refactor verification; no new full campaign replay is implied by this map migration.

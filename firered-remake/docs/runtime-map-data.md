# Compact runtime map data

Implemented and checked locally on 2026-10-09 against the 24-map selection in `map-resource-baseline.md`. This component adds a deterministic exporter, authenticated loader, compatibility seam, source lookup and isolated tests. Production integration now selects this representation in `scripts/launch-current.sh` through `FIRERED_COMPACT_MAPS=1`. Existing importers, content/save IDs, golden expectations and original `world.json` files are retained. `FIRERED_COMPACT_MAPS=0` explicitly selects the historical rich loader for comparison. Generated game data stays under the existing ignored `content/generated` tree; no public deployment or asset upload is involved.

## Representation and trust

`scripts/export-runtime-maps.py` emits `content/generated/runtime-maps/manifest.json`, `source-registry.json` and one `runtime-map.json` per map. Schema 1 and converter `firered-runtime-map-v1` store:

- Original little-endian 16-bit map and border blocks in canonical base64, in source row order. Collision keeps both bits; elevation keeps all four bits.
- Sorted, unique metatile rows `[source metatile ID, original uint32 attributes, attribute-source index]`. All eight decoded attribute fields remain available, including unsupported behavior classifications.
- Deduplicated attribute-file references and behavior names. Cell coordinates and metatile source symbols are reconstructed exactly rather than repeated per cell.
- Original map ID, dimensions, source identity, optional technical spawn and artifact references.
- Complete original events, including objects, clones, warps, connections, background/coordinate triggers, music, map flags and movement metadata. The Route 2 neighbor clone outside its map rectangle is deliberately preserved.
- Original map script/text strings and hashes, because `PretCampaignCatalog` compiles them at runtime. They are live dependencies, not removable audit evidence.

The exporter pins each of the 24 reviewed rich-world SHA-256 digests and source-set hashes. It checks embedded source hashes, original event equality, original layout dimensions, original block bytes, all decoded attributes and source references before producing output. It validates all inputs before writing any output and atomically replaces individual files, writing the manifest last. Running `--check` writes nothing. Python's standard library is sufficient.

`PretRuntimeMapCatalog.Load(directory)` authenticates the manifest using the compiled digest, checks its converter/source/schema identity, then authenticates each file's length and SHA-256. The optional expected-manifest argument is a caller-provided trust anchor for explicit versioned integrations/tests; it must never be read from an unauthenticated adjacent file. Rewriting self-reported hashes cannot make the default loader accept modified content.

Current manifest: `bd47464db904e8c9e49a0f7f4975d048bb9d8e04d43d931b6e5b096e9a82c2f7`.

Current exporter source: `9300c36ac56fd7172477fe6032a76f104e6b698974ca2a56c0081d3231f49cd8`.

The loader rejects missing/empty collections, oversized files/grids, duplicate JSON properties/IDs/paths/table rows, unknown top-level fields, missing required properties, unsafe relative paths, invalid table indices, incomplete/noncanonical block encodings, unsupported layer 3, invalid script hashes, invalid event bounds and bad loaded warp endpoints. Original profile-specific warp resolvers remain in use, preserving supported external-boundary IDs.

Rich development files retain complete original binaries, repeated cell provenance, source inventory, layouts, inspection images and original source bytes. Nothing is deleted or regenerated to fit these results. The map runtime files retain enough references to resolve each cell back to those local development artifacts.

## API and integration seam

`PretRuntimeMapCatalog.Maps` is indexed by unchanged `MapId`. `PretRuntimeMapData` implements `IFieldMap` directly with compact block storage. `GetTile`, `SourceBehaviorAt` and `FieldWarps` preserve original behavior. `GetSourceCell`, `ExpandCells` and `ExpandBorderCells` recreate the existing `PretWorldCell` record, including every attribute, behavior name and complete `SourceRef`.

`PretRuntimeMapCatalog.BaseMetadata` exposes the one authenticated 128-file metadata registry shared by all five base Pallet maps. Its own file hash is bound into the fixed manifest, and its source-set/material hashes are checked against the original compiled `FullSourceHash` and `SourceInventoryHash`. It contains no embedded source binaries or per-cell records; base runtime loaders do not need to reopen rich worlds for metadata. Region loaders can continue to construct their metadata from their existing authenticated source locks.

The internal `ToHeaderDocument(metadata)` method retains identity, dimensions, artifacts, events and live script/text with empty cell/border arrays. Compact-aware `PretWorldMap` adapters can route cell queries to the compact instance and expand legacy arrays only when requested.

The internal `ToWorldDocument(PretWorldMetadata authenticatedMetadata)` method preserves compatibility with `PretWorldMap` factories and existing event/script consumers. It intentionally takes the existing authenticated metadata inventory: `Metadata.Files` is still used for script source references and NPC texture authentication. It embeds only the live map script/text dependencies. Do not call the legacy full-evidence `Validate` method on this reconstructed runtime document; its embedded layout/binary inventory belongs to the rich development representation.

The integrated `PretWorldMap.FromRuntime` uses `ToHeaderDocument` and delegates cell/tile access to the compact map; it does not retain expanded per-cell source records. `PretRuntimeMapIntegration` verifies the compact world source digest against each existing authenticated region output manifest, retains original map/script/raw-source authentication, and verifies the original PNG identities. Region loaders construct the unchanged metadata once from source locks. Original region validation still expands cells temporarily where it compares original block data, then releases the expanded records.

The selected 24 rich `world.json` files are no longer read in compact mode. A real isolated data tree with those exact 24 files absent loads the complete campaign and replays all four frozen Route22 natural chains with identical full state hashes. Two duplicated Viridian audit worlds and raw sources used by the current script compiler remain required: this is a completed map-data optimization, not a claim that all provenance/compiler data has been eliminated from a complete runtime package.

`tests/Game.RuntimeMapIntegration.Tests/fixtures` pins the pre-change 6d2ed1d traces. Both representations have the same `ContentVersion` (`ad5dee38…5513c`), fresh-session hash, 24 map events/warps/scripts/artifact references, all cell behavior and source lookup. All **1,864** recorded commands reproduce the original revision and complete state hash; the same replay succeeds with the 24 rich worlds removed. No save migration is needed.

Separate cold .NET processes, after full collection, retained **55,814,480 B** for the rich full campaign catalog and **27,381,608 B** for compact mode (about 51% less). Allocations while loading were 351,259,816 B and 298,756,864 B. These are managed measurements for this build, not peak RSS, native memory, GPU allocation, or a repeatable startup benchmark. Small timing differences are recorded only as observations. Texture residency/pixel checks are separately documented in `map-resource-cache.md`.

## Measured bytes

The same 24 original worlds total **16,100,416 B**. Runtime map files total **393,786 B**, plus a **10,725 B** manifest and **45,068 B** shared live source registry, or **449,579 B** in total: **15,650,837 B less (97.21%)**, approximately **35.81× smaller** for this map-data comparison. This includes map script/text and all events. Original development files remain 16,100,416 B on disk; the two representations coexist. PNG/atlas/NPC assets, other region catalogs, compression and filesystem allocation are excluded.

| Map directory | Original world B | Runtime map B |
| --- | ---: | ---: |
| forest-region/forest-north-gate | 331,109 | 6,933 |
| forest-region/forest-south-gate | 329,420 | 5,978 |
| forest-region/route-2 | 1,275,616 | 13,450 |
| forest-region/viridian-forest | 2,302,184 | 25,136 |
| pallet | 556,614 | 22,216 |
| pewter-region/pewter-city | 1,309,606 | 40,849 |
| pewter-region/pewter-gym | 352,389 | 13,305 |
| pewter-region/pewter-house-1 | 285,006 | 6,524 |
| pewter-region/pewter-house-2 | 283,451 | 5,694 |
| pewter-region/pewter-mart | 284,215 | 6,659 |
| pewter-region/pewter-museum-1f | 414,644 | 17,802 |
| pewter-region/pewter-museum-2f | 340,351 | 9,988 |
| pewter-region/pewter-pokemon-center-1f | 319,171 | 9,559 |
| players-house-1f | 346,901 | 8,175 |
| players-house-2f | 332,538 | 6,483 |
| professor-oaks-lab | 465,766 | 74,284 |
| rivals-house | 358,617 | 16,203 |
| route22-region/route-22 | 869,075 | 18,197 |
| route22-region/route-22-north-entrance | 328,689 | 5,335 |
| route22-region/route-23 | 2,366,045 | 29,649 |
| viridian-center/viridian-pokemon-center-1f | 315,338 | 7,504 |
| viridian-region/route-1 | 752,791 | 8,322 |
| viridian-region/viridian-city | 1,294,291 | 27,175 |
| viridian-region/viridian-mart | 286,589 | 8,366 |

The lab's larger runtime file is dominated by its live script dependency, which must not be discarded merely to improve a size result.

## Verification and source lookup

From the project root:

```sh
python3 scripts/export-runtime-maps.py
python3 scripts/export-runtime-maps.py --check
python3 tests/Game.RuntimeMapData.Tests/test_export.py
source scripts/env.sh
flock /tmp/firered-remake-build.lock dotnet build tests/Game.RuntimeMapData.Tests/Game.RuntimeMapData.Tests.csproj -m:1 -nr:false
dotnet run --no-build --project tests/Game.RuntimeMapData.Tests -- .
python3 scripts/export-runtime-maps.py --resolve map.pallet-town 6 7
python3 scripts/export-runtime-maps.py --resolve map.pallet-town 0 0 --border
```

Source lookup returns the authenticated rich-world path/hash, original grid byte offset/hash, metatile attribute byte offset/source reference and original values. It is read-only and rejects invalid coordinates/IDs.

Measured tools: .NET SDK **10.0.401**, Microsoft.NETCore.App **10.0.12**, Python **3.12.14**. No engine or external packages are used by this component. The C# suite loads the original maps through all existing authenticated loaders, compares **24 maps, 18,410 map cells and 98 border cells**, complete event inventories, every reconstructed public cell/source record, artifact references, script/text strings and resolved warp records. It also exercises corruption controls beyond the outer digest by explicitly providing the test manifest digest. The Python suite compares two independent export directories byte-for-byte and tests source corruption, missing inputs, strict decoding and offline lookup. Empty/missing suites are not accepted as success.

Earlier failed attempts remain recorded: the initial C# compile exposed a missing `IFieldMap.FieldWarps` member; an initial test command incorrectly passed MSBuild flags as application arguments; a generic warp resolver omitted Viridian Center 2F, so the loader now preserves each original source profile's resolver; a Python mutation fixture assumed pretty-printed source JSON and was replaced with an actual parsed-node mutation. These were implementation/test corrections. Original source outputs and goldens were not changed.

## Integrated commands

From the checkout, `./dev test-runtime-maps` runs exporter identity/determinism, standalone map decoding and the current campaign integration. `./dev test-map-resource-cache` uses actual Godot readback with compact mode enabled. `scripts/launch-current.sh` enables compact mode in the playable route; override `FIRERED_COMPACT_MAPS=0` only to compare the rich loader. The tests do not drive a new native OS-input session.

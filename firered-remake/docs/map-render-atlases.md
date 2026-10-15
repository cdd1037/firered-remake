# Regional map-render atlases

`python3 scripts/imports/maps/import-map-atlases.py` runs after the ordinary map and border importers. It reads their generated `runtime-map.json` and `map-borders/*/border.json` artifact lists. It does not read upstream scripts, change collision/events, or touch animated and interactive patch assets. Original map PNGs are removed only by the explicit finalization command below; player sprites and patch/animation assets are excluded from packing and pruning.

Outputs are flat under `content/generated/maps/atlases/`:

- `catalog.json`: schema version 1; `tileSize=16`, `pageColumns=64`, `tilesPerPage=4096`
- `indices.bin`: concatenated independently zlib-compressed UInt16 little-endian streams
- `<regional-package>-000.png`, etc.: exact RGBA tile pages, at most 1024×1024

`pages` maps each regional package to ordered `{path,width,height,tileCount}` page descriptors. `images` maps each original path relative to `content/generated` to `{pack,width,height,offset,length}`. Read `length` bytes at `offset` in `indices.bin`, inflate, then read one UInt16 per row-major 16×16 image cell. Repeated identical compressed streams can share an offset. Tile IDs are local to the regional package. The page is `id / 4096`; its column is `id % 64` and row is `(id % 4096) / 64`. The last page may have fewer rows.

A tile's four channels, including RGB under transparent alpha, are preserved exactly. The deduplication domain is one regional package, sharing cells across its maps, bottom/middle/top layers, composite images, and border layers. Files containing patches or animation frames remain separate.

The Godot renderer reconstructs individual logical layers on entering a map through `MapAtlasImages` and `CampaignResourceCache.Owner.MapTexture`. Native Godot image decoding preserves RGBA before the existing display-color conversion. The existing bounded map-layer cache and dynamic patch behavior remain; no scene node is created per tile. This reduces packaged files and bytes, but does not claim lower active-map GPU memory or improved frame rate.

## Updating and finalizing

Run the affected ordinary map importer(s), then:

    python3 scripts/imports/maps/import-map-atlases.py --prune

This regenerates the atlas set, verifies it, backs up original map PNGs and changed manifests outside the project, then removes only packed map PNGs. The output reports the backup directory. Use `--backup-dir /path/outside/project/new-empty-directory` to choose it. Runtime-map and border artifact paths remain logical image IDs; actual `manifest.json` output/file/artifact lists are updated to stop listing removed map PNGs.

Freshly emitted PNGs always take precedence. When an unaffected map's PNGs are absent, the importer recovers those logical images from the existing atlas. Updating one region therefore does not require restoring every original PNG. Missing both a PNG and its existing atlas entry is an error. Running without `--prune` updates atlases while leaving any PNGs in place.

## Verification and evidence

- Generation reconstructs images from the newly encoded PNG pages and index streams before installing outputs. Fresh images are compared against the actual PNGs; recovered images are compared against their prior atlas representation.
- `python3 scripts/imports/maps/import-map-atlases.py --check` regenerates in a temporary directory and compares every output byte. It works with full, mixed, or packed-only inputs.
- `python3 scripts/imports/maps/import-map-atlases.py --verify` validates installed dimensions, page/tile bounds, index streams and logical inventory. Where original PNGs exist, it also compares their RGBA pixels.
- Reports distinguish `originalImagesCompared`, `originalPixelsCompared`, `recoveredImagesCompared`, and `packedOnlyImages`. A packed-only consistency check is not independent evidence of original pixels. `originalPixelParity` is true only when every image was compared against an actual fresh PNG.
- `python3 scripts/checks/test-map-atlases.py` exercises explicit backup/prune, packed-only deterministic repacking, fresh-image precedence, transparent RGB preservation, and corrupt-index rejection using isolated test data.

The importer uses Python and Pillow, the same image dependency as the existing map importers. It is an offline derived-render-data step, not a new renderer or a substitute for upstream resource import.

The initial packed set contains 3,064 map and border images in 66 regional packs. Native Godot reconstruction matched all 227,195,904 original RGBA pixels, including transparent RGB, before originals were pruned. The packed files total 2,483,278 bytes versus 19,118,530 original PNG bytes. Five player images and interactive patches were excluded.

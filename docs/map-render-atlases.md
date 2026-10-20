# Map-render atlas format

The normal source-to-final entrypoint is
`python scripts/imports/rebuild-resources.py world`; it compiles selected maps,
verifies final atlas pixels and removes temporary map PNGs. See
[world selection and oracle](world-resource-pipeline.md) and
[reconstruction commands](resource-reconstruction-guide.md).

The standalone `scripts/imports/maps/import-map-atlases.py` repacks generated map
artifacts selected by `maps/world.json`. Each referenced compact map declares its
logical layer, composite and border image paths. It does not read upstream scripts,
change collision/events, or pack animated/interactive patches and player sprites.

## Stored format

Outputs are flat under `content/generated/maps/atlases/`:

- `catalog.json`: schema 1, `tileSize=16`, `pageColumns=64`, `tilesPerPage=4096`.
- `indices.bin`: concatenated independently zlib-compressed UInt16 little-endian streams.
- `<region>-000.png`, etc.: exact RGBA tile pages, at most 1024×1024.

`pages` maps each regional pack to ordered `{path,width,height,tileCount}` pages.
`images` maps each logical path relative to generated to
`{pack,width,height,offset,length}`. Inflate the specified byte span, then read one
UInt16 per row-major 16×16 cell. Identical compressed streams may share an offset.
Tile IDs are pack-local: page = `id / 4096`, column = `id % 64`, row =
`(id % 4096) / 64`, using integer division. The final page may have fewer rows.

Deduplication spans maps, layers, composites and borders within one regional pack.
All RGBA channels, including transparent RGB, are preserved. Invalid geometry,
index spans, tile references and logical-image inventories fail rather than fall
back to an unrelated image.

`MapAtlasImages` and `CampaignResourceCache.Owner.MapTexture` reconstruct logical
layers when entering a map. Native decoding occurs before the existing display
color conversion. The bounded layer cache and dynamic patches remain; there is no
node per tile. Packing does not by itself lower active-map GPU memory or improve
frame rate. Edited-tile lookup is documented in
[metatile presentation](shared-metatile-presentation.md).

## Standalone maintenance and checks

```sh
python scripts/imports/maps/import-map-atlases.py --check
python scripts/imports/maps/import-map-atlases.py --verify
python scripts/checks/test-map-atlases.py
```

`--check` regenerates in a temporary directory and compares output bytes; it works
with full, mixed or packed-only inputs. `--verify` checks installed dimensions,
page/tile bounds, streams and logical inventory, and compares fresh PNGs when
present. Neither reads raw source geometry or independently proves source pixels.

For a deliberate generated-image repack, fresh PNGs take precedence; absent PNGs
are recovered from the existing atlas. Missing both representations is an error.
Without `--prune`, fresh PNGs remain. Explicit `--prune` regenerates and verifies,
backs up only the packed originals outside the project, then removes them. Use
`--backup-dir /path/outside/project/new-empty-directory` to select that backup.
Logical paths in map documents remain unchanged. Normal world compilation already
finalizes its own staging images and does not need this extra step.

Reports distinguish `originalImagesCompared`, `originalPixelsCompared`,
`recoveredImagesCompared` and `packedOnlyImages`. `originalPixelParity` is true
only when every logical image was compared with an actual fresh PNG. A packed-only
repack is self-consistency evidence, not an independent original-pixel oracle.
`test-map-atlases.py` checks isolated backup/prune, fresh-image precedence,
transparent RGB, packed-only deterministic repacking and corrupt-index rejection.

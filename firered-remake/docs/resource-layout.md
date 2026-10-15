# Generated resource layout

`content/generated` contains six functional roots:

- `data`: gameplay catalogs, trainer definitions and occurrences, encounter/item/service/story data.
- `maps`: regional map files, map collections, shared map metadata, border bindings and render atlases.
- `sprites`: Pokémon, trainer, item and overworld graphics.
- `battle`: battle text, animation and presentation data.
- `ui`: screens and interaction presentation.
- `audio`: shared synthesis data and cue bindings.

Single-file catalogs use direct filenames (for example `data/trainers.json` and
`maps/catalogs/maps.json`), rather than a package directory
containing only one manifest. The runtime uses actual new paths. There are no old
path aliases, symlinks or migration lookup tables.

## Map files

A regional map is a file such as `maps/regions/viridian/route-1.json`. Real alternate
layouts retain an explicit suffix, such as `trainer-tower-1f.double.json`. Map
collection entries name the actual JSON file; regional rows use `mapPath`.

Compact map schema 3 retains map identity, dimensions, events, warps, script lookup
paths, blocks, numeric metatile attributes and render bindings. Metatile entries
are `[id, attributes]`. Per-cell provenance, attribute-source indices and the
map-source object have been removed. `sourceMapSymbol` and `sourceMapName` are
real dispatch identities. Runtime loaders reject old schemas rather than migrate
them during loading. Story script source paths and line data remain where needed
by actual script dispatch.

Map render logical keys follow the same functional paths. Atlas page filenames
are regional, and all 3,064 logical images are recoverable from the shared atlas.
A migration comparison against the pre-change resources checked all 227,195,904
pixels, including alternate layouts. It did not introduce persisted proof hashes.

Map importers may render into private per-map staging directories. The shared
`publish_map_package` function publishes flat filenames before constructing the
actual loading index. It is a publishing transformation, not a runtime fallback.
Map manifests that still drive resource loading retain their output index; count
receipts and provenance headers are not runtime requirements. Pokémon Tower and
Rocket Hideout read their explicitly named resources directly without redundant
manifest permission lists.

## Opening scenes and Pokémon artwork

The five starting scenes share `maps/regions/opening`. Their NPC identities use
`sprites/overworld/opening.json` and the shared overworld atlas. The player sheet
uses `sprites/overworld/opening-player.png`: its original palette differs from
the general overworld Red sheet and is deliberately preserved. Twelve prior NPC
sheets were compared pixel-for-pixel against the shared atlas before removing
local copies. The five identical player copies were reduced to one.

Early Pokémon region bundles no longer contain retired duplicate gameplay
catalogs. Their battle sprites, icons, footprints and presentation metadata live
under `sprites/pokemon`. Shared Pokémon definitions remain under `data/pokemon`.
Regional map packages still contain some service item/machine documents because
their service importers and readers compose those contracts together; these are
remaining semantic splitting points, not a claim that every package is minimal.

## Development tools

See `scripts/README.md` and `resource-reconstruction-guide.md`. Importers, shared
libraries, checks and source recipes live separately. Raw upstream data and source
recipes belong to reconstruction tooling; runtime code must not read `content/raw`
or execute upstream repository code.

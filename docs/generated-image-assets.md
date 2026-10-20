# Generated image boundary

Game.Content reads generated metadata and, when needed, encoded PNG bytes. It does
not decode pixels, retain a second RGBA cache or compose original tiles/palettes.
Godot's native PNG loader and `NativeImages` own runtime decoding and genuinely
dynamic pixel access. Import tools use Pillow for offline conversion and packing.
General runtime/source boundaries are defined in [resource layout](resource-layout.md).

## Pixels, colors and lifetime

`CampaignResourceCache` keeps exact/native textures and the existing RGB555 display
conversion under separate cache keys. `AtlasTexture` regions share owner-counted
pages, use nearest rendering and `FilterClip`, and preserve region-local drawing
coordinates. Geometry must be valid within the declared page; releasing an owner
must not invalidate a page retained by another owner.

Pixel equality includes all four RGBA channels, including RGB under zero alpha.
Indexed input preserves its indices where they carry meaning: GBA modulo-16
palette selection, font color roles and footprint inversion remain explicit
conversion rules. Shiny art, trainer palette variants and opening-player colors
are genuine distinctions, not duplicates to erase.

`image_io.encode_png` chooses the smaller exact indexed or compressed RGBA output
when there are at most 256 unique RGBA colors, and compressed RGBA otherwise.
There is no quantization, hidden-RGB normalization or geometry change. Some
standalone producers still write compressed RGBA directly; encoded-byte differences
do not imply pixel differences. Original indexed inputs remain untouched.

## Bounded presentation packages

Region rectangles live in their owning package metadata, not an extra global
registry or image sidecars. Shared presentation pages have descriptive names,
one-pixel gutters and dimensions at most 1024 on either axis. Large standalone
source sheets retain their required geometry. Map tile pages use their own
[atlas format](map-render-atlases.md), not these presentation gutters.

- Overworld actors use `sprites/overworld/catalog.json`, bounded resident, trainer,
  Pokémon and object pages, plus standalone player/boat sheets. Source events and
  dynamic actor tables select graphics. Animation order, anchors, dimensions,
  inanimate behavior and palettes remain intact. The walking opening player is
  distinct from the expanded general Red animation strip.
- Inventory icons share `sprites/items.png`; actual palette variants and logical
  records remain distinct. Package item catalogs own their regions. After an item
  reimport, `python scripts/lib/item_icon_exports.py` merges new icons with existing
  atlas regions without requiring every family to be rebuilt.
- Option frames preserve logical variants, nine-slice coordinates and lab-battle
  defaults. Battle HP and dialogue pieces reuse their canonical textures.
- Fame Checker backgrounds/overlays and Fly maps use prepared regions, including
  exact dimmed/dark versions. Scrolling changes draw rectangles rather than
  rebuilding a texture for every offset.
- Slot static art and prepared patches share a page; dynamic affine, scanline and
  RGB555 effects remain in the Godot renderer. Packing does not precompute every
  state combination or move pixel operations into gameplay rules.
- Trainer Tower portraits and character variants reference the owning packages.
  Trainer Card has its own bounded cohort and display/flip rules, documented in
  [Trainer Card atlas](trainer-card-atlas.md), not a global UI registry.

Shared overworld regeneration is included in `rebuild-resources.py world` or the
`overworld-art` target. Other producers and their input limits are maintained in
[resource reconstruction](resource-reconstruction-guide.md) and
[scripts](../scripts/README.md). Edited field tiles use the same resolver across
live and historical views; see [metatile presentation](shared-metatile-presentation.md).

## Verification boundaries

Content tests check generated-only selection, paths, dimensions and region
metadata. Offline tools verify source conversion, exact RGBA, deterministic packing
and partial reimport. Native Godot checks additionally cover decoding, display-color
conversion, region-local rendering, page sharing and owner release. These layers
are complementary; no managed runtime PNG decoder is needed for tests.

Atlas consistency alone cannot establish source fidelity. Use independent retained
source or original-image comparisons, preserving actual palette and layout
variants. File/PNG-byte savings, decoded page payload, active-map GPU memory and
frame rate are different measurements. Do not infer the latter from the former,
or merge independent lifetimes merely to reduce file count.

Map rendering retains its bounded reconstructed-layer cache. Renderer/texture tests
are not a natural public-UI campaign replay, and Dummy audio is not listening
acceptance. Current incomplete source closures, including double-battle healthbox
inputs, remain explicit in the reconstruction guide.

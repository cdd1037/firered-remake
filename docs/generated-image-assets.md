# Generated image boundary

Game.Content reads generated image metadata and, where needed, encoded PNG bytes.
It does not decode pixels, keep a second RGBA cache, or compose
source tiles/palettes. The custom `PngRgba8` implementation has been removed.

Godot uses its native PNG loader. `NativeImages` provides exact image loading and
pixel access for genuinely dynamic renderers. `CampaignResourceCache` separately
keys exact/native textures and the pre-existing RGB555 display conversion, so
migration does not change colors. Texture pages are reference-counted by owners;
`AtlasTexture` regions share a page and use nearest rendering/FilterClip. Pixel
operations still required for the slot machine's dynamic scanline effects remain
in its Godot renderer, not in content loading or gameplay rules.

## Offline work and ordinary assets

Import tools use Pillow for PNG decoding and encoding, fixed palette variants,
composition and atlas packing. Indexed input stays indexed: GBA modulo-16 palette
selection, font color roles and footprint inversion are explicit caller rules.
Hand-written PNG filters, chunk CRCs and DEFLATE framing have been removed from
both runtime and importer code. Region rectangles live in existing package metadata, not image
sidecars or another registry. The pages have descriptive names, one-pixel gutters,
and dimensions no larger than 1024 in either axis (large original standalone
sheets keep their original dimensions). There is no hash-addressed
object store or restoration step.

- Regional NPC/player art: the original 646 copies now share four bounded pages
  (residents, trainers, Pokémon, objects), plus the always-used player sheet and
  two large boats. The 119 canonical animation descriptors live in
  `shared-overworld/catalog.json`; 723 regional entries carry only graphics IDs.
  Frame sequence, dimensions, anchor, inanimate behavior and palette colors are
  unchanged. Run `python scripts/imports/presentation/import-shared-overworld.py` after regional imports
  to regenerate the shared cohort; `--check` verifies deterministic output.
  `--raw` and `--generated` select input/output trees. Six importers share
  `overworld_sprites.py`; regional imports no longer render temporary image copies
  or compare shared files. The shared import is their explicit build dependency.
- UI: 27 owned option-frame PNGs share one 1024×250 page; all 40 logical variants
  and four shared lab-battle defaults retain their original placement and nine-slice
  coordinates. This removes 26 files and 851,823 bytes. Battle HP pieces, dialogue
  and forest status contexts reuse their canonical textures.
- Trainer Tower: portraits and Meowth/Seaking variants reference their existing
  owning packages. All 132 logical assets remain.
- Inventory icons: 136 PNGs now share one 416×208 `item-icons/items.png` page,
  containing 120 exact RGBA variants for 174 logical records. This removes 135
  files and 250,599 PNG bytes. Palette variants remain distinct. After an item
  import run `python scripts/lib/item_icon_exports.py`; the pass can reuse existing
  atlas regions while replacing a single regenerated family, so a full reimport
  is not required. Region data stays in each package's existing item catalog.
- Fame Checker: portraits, silhouettes, exact dimmed fact icons and cursor regions
  share a sprite page. Three base backgrounds share a page; six 512-pixel overlay
  strips share a third. Scrolling changes the draw rectangle without recomposing
  pixels or creating a texture for every offset.
- Fly/wall maps: six flag-dependent backgrounds and their exact darkened versions
  are imported into one 484×972 page. Runtime selects a region; it no longer
  composes map backgrounds or darkens each pixel.
- Slot machine: static help/background/sprites and prepared line, button and light
  patches share one page. Godot selects/blits patches and retains the original
  dynamic affine, scanline and RGB555 effects. No combination explosion is baked.

The initial non-map sharing pass removed 612 PNG files / 1,660,962 bytes before the
subsequent atlas changes. Other independent image families remain; these figures
are not a claim that all images are atlased. Map rendering is documented separately.

## Verification scope

- All 646 prior NPC images matched the 119 regenerated sheets; 723 metadata records
  preserved non-path data, and six region-importer entry points emitted no copies.
  The next atlas pass removed another 112 PNGs and 3,574,605 bytes including repeated
  regional metadata. Every one of 119 sheets and 1,041 animation frames matched
  through native Godot atlas regions; all seven pages shared and released correctly.
  Canonical import regeneration matched all outputs exactly.
- Official Godot 4.7.2 decoded 5,351 legacy-compatible generated RGBA8 PNGs exactly
  like the removed decoder, including transparent RGB. Nine other-format images
  were outside that comparison. Native/existing color-mode separation, atlas region
  sharing, dimension failure and two-owner release were separately checked.
- Fame Checker matched 1,568 outputs from the prior C# implementation, including
  every one of 241 scroll offsets in all six overlay states. Fly matched all twelve
  normal/dark background variants. Both importers reproduced the new outputs from
  retained source inputs (Fame's Git transport was substituted with local bytes).
- Slot matched 129 prior C# outputs: help plus all 128 bet/line combinations while
  varying button/light states. Full importer output reproduction also passed.
- Importer codec migration matched 1,271 lab-UI, 1,273 Teachy-TV and 1,271 Sevii
  source PNG decodes, plus 928 specialized sprite/font/footprint comparisons.
  Six complete specialized conversions retained 24 exact RGBA PNGs; Fame, Slot
  and option-frame regeneration also passed. PNG compression bytes may change
  for older packages on their next import; dimensions and all RGBA channels do not.
- Inventory atlas: 168 backed-up logical icon records matched through native
  Godot regions. Eight import families regenerated 82 source icons, and partial
  reimport/idempotence tests passed. Viridian Gym, Fuchsia Gym and Power Plant
  source comparisons now read predecessor atlas regions rather than retired PNGs.
- Headless Content tests cover generated-only image packages, logical selections,
  shared paths and region metadata. Pixel checks use offline tools or Godot; no new
  managed PNG decoder or game-runtime image library was introduced.

The integrated graphical check also passed in an official Godot 4.7.2 window
from a generated-only workspace: checkpoint town map/NPCs, right-click menu,
Bag Items/Key Items, TM CASE icons/HM list, and Options Type 1→Type 2 frame.
The inspected display was crisp with no missing/cropped atlas regions; normal
window close completed with zero game-log errors. Audio used the Dummy backend.
These checks do not claim a full gameplay-route replay or physical-audio acceptance. Full Route22 import still references a retired planning manifest; full
source regeneration of double-battle UI lacks the original healthbox input. Neither
was represented as a passing check. Build and broader runtime acceptance results
are recorded with the corresponding project cleanup.

## Exact PNG encoding (2026-10-15)

The shared offline writer (`image_io.encode_png`) uses Pillow. For at most 256
unique RGBA colors it constructs an exact palette and transparency table, then
keeps whichever is smaller: indexed PNG or compressed RGBA PNG. Above 256 colors
it retains compressed RGBA. There is no quantization, lossy conversion, hidden-RGB
normalization, or change to pixel dimensions. The shared map/presentation atlas
writers use the same helper. Original indexed input files remain untouched.

The one-time generated-output refresh checked all 2,077 PNGs, changing 1,957 only
when smaller. Their combined size fell from 27,087,301 to 2,208,552 bytes. Identical
PNG-only ZIP inventories at DEFLATE level 9 fell from 3,874,117 to 2,498,803 bytes;
these are measured ZIP sizes, not a claim that the full project archive saves
24.9 MB. Re-encoding as RGBA alone would have increased that ZIP's size.

The largest cause was 1,472 stale PNGs encoded with stored/uncompressed DEFLATE
by retired import code (24,482,112 bytes). Map PNGs were already compressed;
exact palette encoding reduced their 66 pages from 1,790,625 to 891,906 bytes.
Map indices and metadata are unchanged. Some standalone importers still write
compressed RGBA directly; they remain correct but do not all use indexed output.

Every output matched the backed-up original in Pillow and official Godot 4.7.2:
2,077 images, 18,532,236 pixels, all RGBA channels including transparent RGB.
The codec tests cover duplicate source palette colors/indices, partial alpha,
same RGB with different alpha, malformed geometry, and >256-color fallback.

## Remaining sharing opportunities

The pixel audit finds 56 identical groups with 166 extra PNG files, mainly
footprints, small empty tiles, trainer portraits and battle backgrounds. After
exact palette encoding their theoretical duplicate payload is only about 46 KB;
no additional registry or indirection was introduced to remove these.

497 explicitly named shiny images are genuine palette variants, not accidental
copies. They occupied 6,848,971 bytes before encoding cleanup (about 376 KB with
exact palette encoding). Converting them to runtime palette swaps would require
new rendering/data behavior and is not part of this lossless file cleanup.

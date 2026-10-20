# Trainer Card texture cohort

Trainer Card now stores its 59 logical assets as 46 exact RGBA regions in two
dedicated, guttered pages: 726×972 and 726×552. Thirteen formerly separate images
are exact duplicates of backgrounds or front/back panels. Gender, earned-star palettes,
badges, stickers, portraits, placements and logical asset IDs remain separate
where their pixels or meaning differ. Transparent RGB is part of equality.

This is a bounded presentation package, not a global texture registry. The source
recipe remains the existing fixed Trainer Card input selection. The runtime
manifest and `trainer-card.json` use schema 3 and contain page dimensions plus
per-asset regions; original-file inventories do not enter the runtime contract.

## Measured effects

The complete before/after cohort, including both JSON files, was compared:

| Metric | Before | After |
| --- | ---: | ---: |
| PNG files / backing image uploads | 59 | 2 |
| PNG bytes | 44,010 | 24,193 |
| Package bytes, including JSON | 59,765 | 43,404 |
| Package ZIP, DEFLATE level 9 | 52,683 | 22,992 |
| Uncompressed display-page RGBA bytes | 6,190,848 | 4,425,696 |

The last row is the no-mipmap image payload retained for this cohort, a reduction
of 1,765,152 bytes (28.51%). It is not a measurement of driver allocations,
whole-game peak memory or frame rate. Atlas objects still exist for the 46 unique
regions: the cache retains 48 entries, not just two objects.

The card was already loaded at campaign startup and held by the scene texture
owner. That lifetime is unchanged. All logical regions share the two page uploads
and use the existing RGB555 bit-replicated display-color mode, not native-color
mode. Native and display modes remain separate cache keys. The ordinary draw and
240×1 card-flip scanline coordinates stay local to each logical region.

## Importing and checks

From the project root:

```sh
python scripts/imports/trainers/import-trainer-card.py
python scripts/imports/trainers/import-trainer-card.py --check
python scripts/checks/test-trainer-card-atlas.py
python scripts/checks/test-trainer-card-photo-import.py
python scripts/checks/test-special-presentation-importers.py
```

The importer generates and validates all source conversions before writing. After
successful publication it removes only the former `<logical-id>.png` files that
this importer owns. Unrelated files are not pruned. `--check` does not modify
resources and rejects stale standalone card PNGs.

Packing checks exercise deterministic order, bounded pages, one-pixel gutters,
exact duplicate reuse, transparent RGB and distinct-alpha/palette preservation.
The source sticker/star oracle and full special-presentation regeneration remain
active. Content tests reject duplicate or missing assets/pages, bad geometry,
unknown page references and missing required region metadata. The Fuchsia Gym
recipe no longer lists an unused physical `badge-5.png`; its actual logical badge
binding and imported layout are unchanged.

The one-time migration compared all 59 assets against independently retained
pre-change images: 1,547,712 pixels and all four channels matched exactly. Asset
order, IDs, dimensions, placements, texts, layout and display rules were unchanged.

An isolated native check on 2026-10-20 used official Godot 4.7.2 .NET, a net10.0
probe built by SDK 10.0.401, and the cloud desktop's OpenGL compatibility renderer
(Mesa llvmpipe).
It linked the production cache/image-loading source and checked:

- All 59 native-color and RGB555 display-color regions against the retained
  originals, including transparent RGB.
- Exactly two display page decodes/uploads, separate from the two extra native
  page decodes used only for parity checking; 48 display cache entries.
- Shared regions across two owners, negative/empty/out-of-bounds/overflow region
  rejection, and zero retained entries/bytes after both owners were released.
- 635 paired rendered comparisons: every full logical asset, plus 36 flip frames
  for each of the 16 panel variants using the existing local scanline geometry.

The first probe failed because the desktop window did not retain the requested
viewport dimensions. That failed run was preserved, and the corrected fixture
used an explicit 480×160 SubViewport. The second run passed and exited normally.
This is texture/render parity, not a natural public-UI playthrough, frame-rate
benchmark, GPU-driver-memory measurement or audio acceptance; audio was Dummy.

The affected Fuchsia importer initially failed its byte-oriented `--check`
because retained output encoding differed from the maintained writer. All JSON
values and all three portrait images matched independently before regeneration.
Its four outputs were backed up outside the project, regenerated without logical
changes, compared again, and its unchanged `--check` then passed.

## Why not merge the rest indiscriminately?

The initial audit counted 2,278 generated PNGs and 3,968,188 encoded bytes before
this change. The sum of all decoded RGBA sizes was 145,169,616 bytes; this is not
resident memory because map caching, standalone sheets and modal lifetimes differ.
Map layers already use regional packed storage and reconstruct active layers, so
repacking those pages does not itself reduce active-map GPU memory.

Other UI trials reduced file count but increased image payload because of gutters
and empty space. Pokémon sprites have genuine independent palettes and consumers;
combining palettes can exceed exact indexed-PNG limits and increase disk bytes.
For example, a local Mt. Moon cohort trial increased its PNG payload from 48,547
to 77,204 bytes. Broader icon sharing can be useful for co-drawn party/storage
icons, but should follow a common importer/reader boundary and preserve species,
form and palette identities rather than introduce a monolithic atlas.

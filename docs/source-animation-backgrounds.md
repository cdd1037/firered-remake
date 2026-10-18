# Indexed battle-animation backgrounds

`SourceAnimationBackgroundCatalog` loads the two-file generated background pack. The manifest contains only resource IDs and byte ranges. The deduplicated atlas keeps packed 4bpp tiles, source screenblock maps and RGB555 palettes. It includes the source terrain table and the Dark/Psychic move backgrounds. No source paths, source hashes, converted full-screen images or runtime import recipes are required.

`SourceAnimationBackgroundSurface` owns the bounded BG3 memory for one scene. It starts from cleared VRAM, loads the selected terrain's complete 4096-byte map, and retains the actual subsequent writes:

- BG3 uses character block 2, map blocks 26/27, and initially screen-size 1 (512×256). Screen-size 0 explicitly selects 256×256, as `ToggleBg3Mode(0)` does for sliding tasks.
- Dark replaces 2048 map bytes; Psychic replaces 1280. Neither repeats or clears the rest. Move loads replace only palette bank 2; terrain loads restore banks 2–4.
- Bank 1 is owned by the selected HUD window palette. These BG3 resources never address it; unsupported bank-1 map references are rejected.
- Pixel index zero selects the bank-0 backdrop. Bank-0 map entries with nonzero pixel indices still use their actual textbox colors. Psychic's last two map columns are an example.
- Software blending/inversion respects each pixel's real palette bank. A supplied faded bank-2 override has last-write ownership. Hardware BG3/backdrop darkening is applied afterward and uses the hardware's round-before-subtraction arithmetic.
- `Restore()` reproduces a normal terrain reload. `Reset()` initializes a new/cancelled scene without residual writes. Neither immutable assets nor managed scene arrays require a ceremonial disposal API; only the catalog owns a disposable stream.

The shared VM owns source fade timing, background load revisions and scrolling. The frame composer applies background loads only when those revisions change and renders into its caller-owned pixel buffer. Adding these assets does not, by itself, admit any move script.

## Rebuild and checks

Rebuild the two generated files from the pinned local source directory, or use `--check` to compare without writing:

```
python scripts/imports/battle/import-source-animation-backgrounds.py --reference content/raw/pokefirered/037335f4 --output content/generated/battle/animations/backgrounds --check
python scripts/checks/test-animation-background-import.py
dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --filter FullyQualifiedName~SourceAnimationBackgroundTests
```

`tests/Reference/generate_background_vectors.py` explicitly creates a new candidate reference file from raw PNG/JASC/map data. It does not call the importer or product code and refuses to overwrite an existing file. Ordinary tests read the reviewed vectors under `tests/Game.Presentation.Tests/Fixtures/`; they do not regenerate expected results. Coverage includes all imported terrain renders, retained Dark→Psychic partial writes, terrain restoration, both screen sizes with negative/edge scrolling, source bank ownership, transparency/backdrop behavior, lazy catalog lifetime, invalid input, and allocation-free repeated background rendering.

## BG1 overlays and hardware target selection

The same catalog now owns the source Scary Face player/opponent and fog tile/map/palette resources. Script `overlayDependencies` selects these resources; `materialDependencies` declares load-only OBJ graphics such as White Feather and Whirlwind Lines, so palette allocation is not inferred from task names in the resolver.

BG1 overlays retain source 256×256 wrapping, map flip bits and literal palette banks. Their palette load replaces bank 8; source cleanup clears the overlay and blending. The current admitted overlay closure rejects concurrent `monbg` use rather than conflating its battler-visibility ownership with overlay cleanup.

The composer retains BG1, BG2, BG3/backdrop and frontmost OBJ independently. BLDCNT first/second-target masks select the immediately underlying visible layer; an unselected layer is not skipped to reach a deeper target. BG3 index-zero provenance selects the backdrop bit, even when its color equals an opaque BG3 pixel. Each hardware alpha coefficient clamps to 16 independently, and resulting channels saturate at 31; Mist Ball's coefficient sum of 17 is intentional. The existing common OBJ-alpha path has an equivalent fast path, checked against general target-mask composition.

New source fixtures independently cover task registers, raw Glare allocation/OAM timing, Mist movement, source materials and blending. The shared map/priority/mosaic tests remain applicable; these resources do not imply scanline, window or arbitrary parallel-overlay support.

## Weather overlays and source subsprites

Sandstorm and Heat Wave reuse the BG1 overlay owner and source indexed Flying Dirt palette. Their BG1 map clear and register cleanup are separate callbacks. Heat Wave writes the loaded background palette and moves only the event-time visible target pair. Sky Uppercut uses the existing BG3 scroll/register owner with its source delay and unsigned wrapping; its BG_IN_AIR material remains catalog-owned.

A recipe may carry a source `subsprites` table. The composer expands one logical sprite into ordered non-affine OAM rectangles, preserving top-left table offsets, tile offsets, per-piece priority, both flips, wrapped coordinates, and the parent's retained palette/material. Earlier table pieces win equal-priority ties. This does not allocate new logical sprite cells or visual counters. The admitted sand closure proves a conservative source OAM bound of 22 stable field entries plus twice every authored crescent plus all retained scene fragments, at most 64. Arbitrary affine subsprite tables and unreviewed concurrent emitter closures remain rejected.

Rain uses the shared scene's raw child allocation and pre-task OAM snapshot: each attempt consumes its two animation-local random draws before allocation, and uncounted drops can outlive an isolated emitter. Palette ownership follows the retired invocation while subsequent controllers reuse slots. Sunny Day uses the shared raw signed-pan sweep (`panse_adjustnone`) without actor-side adjustment. Magical Leaf upgrades the existing leaf materials to indexed palettes under their original catalog owner; its cycle writes the authoritative faded banks from retained unfaded colors.

The weather source fixtures include independent native OAM table rows and source-pixel hashes for both the full 240×160 source layer and the renderer's 240×112 battle field. Callback/register vectors are separate from those pixel checks; existing viewport/HUD ownership is unchanged.

## Indexed direct faded-palette writes

Moonlight writes source BG palette banks 1–3 at entries 1–15 while preserving each bank's entry zero. The runtime retains these indexed faded writes and publishes detached frame values; the compositor applies them through actual indexed terrain/overlay membership, before hardware darkening. It never guesses membership from flattened RGB colors. Whole-bank palette loads and normal fades supersede prior direct entries for their selected banks. Bank-2 readers use the same updated faded values, and its palette rotation replaces the corresponding sparse projection. Inversion and cancellation follow the shared palette owner.

The authored Moonlight closure excludes `monbg`, whose palette-copy task ordering would require separate proof for arbitrary synthetic combinations. Morning Sun uses the existing single BG1 overlay owner; Aurora uses a catalog BG3 material and the existing OBJ palette-bank cycle. These additions do not implement scanline effects, arbitrary concurrent background owners, or a new palette compositor.

## Source battle-side background sets

`fadetobgfromset` reads all three source byte operands and selects the first for an opponent-side target or the second for a player-side target. The pinned FireRed command reads but does not use the third contest operand (`battle_anim.c:1045–1064`). The shared resolver therefore loads the two reachable battle materials, while the VM still validates all three identities. This uses the existing fade controller, palette ownership and cleanup; it does not add a move-specific compositor branch.

Guillotine adds source backgrounds 12/13; Solar Beam adds 24/25 using the existing Impact tiles/maps with its own palette. Import checks preserve every previous terrain, move and overlay pixel/map/palette block. Solar Beam's secondary task creates 15 uncounted raw orbs through the shared scene; authored big orbs and source fade waits drain them before root end. Tests separately compare original-C callback/frame phases and compose complete setup/release roots. The sunny release is authenticated from its early PP, optional fresh charge marker, successful canceller prefix, released marker and unchanged release PP; final weather is not used to reconstruct a cue.

## Scanline register sampling

The shared battle scene retains the source scanline engine's two 960-word buffers and selected source-buffer index. Starting a transfer changes its destination/width without clearing memory; a deferred stop takes effect at the next VBlank without flipping or clearing. Scene reset clears the banks. Each displayed frame owns detached samples for160 visible rows plus the terminal HBlank transfer. Repeated snapshots do not advance DMA or expose the retained buffers.

BG1/BG2 horizontal and paired horizontal/vertical writes sample the original battler tile plane before screen clipping. The existing overlay sampler also supports explicitly declared512×256 screenblock maps. Alpha-register samples participate in the same first/second-layer selection as ordinary blending. No family has a separate water or distortion compositor.

Timing is an explicit frame-phase model, not a CPU-cycle simulation: main-loop GPU writes queue, VBlank base-register writes are immediate, then existing dirty entries flush their latest shadow after the first-row seed. The source `SetGpuReg` actually chooses queued/immediate behavior from each write's VCOUNT (and forced blank); source control flow alone does not prove whether a particular Battle VBlank write lands on line160 or161. The independent native oracle exercises both conditional paths, partial paired-register overrides and retained dirty entries. Rows1–159 and the terminal transfer follow the selected DMA bank; this model makes no cycle-level original-hardware equivalence claim for the line-zero boundary.

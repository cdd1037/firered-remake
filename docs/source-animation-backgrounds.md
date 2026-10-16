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

Run the importer with the pinned local source directory and a new output directory, then compare the two generated files before replacing the pack:

```
python scripts/imports/battle/import-source-animation-backgrounds.py content/raw/pokefirered/037335f4 NEW_OUTPUT
python scripts/checks/test-animation-background-import.py
dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --filter FullyQualifiedName~SourceAnimationBackgroundTests
```

`tests/Reference/generate_background_vectors.py` explicitly creates a new candidate reference file from raw PNG/JASC/map data. It does not call the importer or product code and refuses to overwrite an existing file. Ordinary tests read the reviewed vectors under `tests/Game.Presentation.Tests/Fixtures/`; they do not regenerate expected results. Coverage includes all imported terrain renders, retained Dark→Psychic partial writes, terrain restoration, both screen sizes with negative/edge scrolling, source bank ownership, transparency/backdrop behavior, lazy catalog lifetime, invalid input, and allocation-free repeated background rendering.

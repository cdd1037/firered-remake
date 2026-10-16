# Pokédex diploma

The Celadon Condominiums 3F designer now opens the original certificate instead of an unavailable-screen notice. This is a collection-side native screen, not a new reward or a new completion rule.

## Source behavior

Pinned reference: pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`.

- `data/maps/CeladonCity_Condominiums_3F/scripts.inc`: caught Kanto species 1–150, excluding Mew, select the congratulations branch. Existing message printing, 60-frame delay, quest-log guard, `special ShowDiploma`, `waitstate`, release and end remain in the source script.
- `src/diploma.c`: the screen separately calls `HasAllMons()` to choose Kanto or National artwork/text. Application uses the existing National species-ID projection, without adding a National-unlock condition. The required National set is 380 species, excluding Mew, Lugia, Ho-Oh, Celebi, Jirachi and Deoxys. Species IDs are not National Pokédex numbers.
- The native callback does not add an inventory item, flag, star, date, export or printing option. It can be opened again. Full completion is not asserted to be naturally reachable in one ordinary single-player save.
- The presentation fades in, requests the existing `MUS_OBTAIN_BADGE` fanfare, waits for its 340-frame counter plus retirement tick and the shared playback token, then accepts a fresh A edge. Early or held A is discarded; B does nothing. Closing fades out, restores the field under a fade-in, then resumes the original waitstate. Enter/keypad Enter/A, gamepad A and left-click are the host A input; no extra button prompt is added.

## Resource and adapter boundaries

`python scripts/imports/rebuild-resources.py diploma` is the normal explicit resource target and honors `--raw`, `--out` and `--check`. Its producer `python scripts/imports/presentation/import-diploma.py` reads the pinned raw cache; `--reference-root` instead reads fixed-commit Git blobs. It does not execute upstream scripts. `--out` supports an isolated output directory and `--check` verifies existing files without writing them.

New original inputs are only `src/diploma.c`, `graphics/diploma/diploma.png` and `graphics/diploma/diploma.bin`. Existing `src/strings.c`, `src/new_menu_helpers.c` and `src/sound.c` supply text, font line height and fanfare metadata. Source/provenance remains in tools and this document, not generated runtime metadata.

The 128×128 indexed sheet supplies 256 4bpp tiles and two 16-color palette banks. The 4096-byte map is two horizontal 32×32 screenblocks forming a 512×256 BG. Conversion honors tile index, horizontal/vertical flips, palette bank, RGB555 expansion and BG color-zero backdrop. The generated runtime package contains two opaque 240×160 PNGs, cropped at x=0 and x=256, and one typed layout/text document under `content/generated/ui/diploma`.

Godot uses the shared normal bitmap font and scene texture owner. Text is centered at x=120: player at y=20; the four-line body at y=46/60/74/88 is centered by its longest row and then left aligned; GAME FREAK is at (120,121). The saved player name is substituted directly. Audio uses the existing shared fanfare backend and pack; no synthesis algorithm or duplicate audio runtime was added.

`CampaignDiploma` is transient and rejected by the save boundary. Application verifies the current actor and source wait instruction before accepting its completion command. Generic acknowledgements and unrelated commands cannot release it. The pure `DiplomaPresentation` helper owns only visual phases and input edges, never inventory, Pokédex ownership, RNG or persistence.

## Checks

- `python scripts/checks/test-diploma-import.py`: every output pixel is checked with independent hardware-address arithmetic; cold regeneration, read-only checking, wrong source input semantics and invalid tile/palette references are exercised.
- `DiplomaPresentationContentTests`: generated-only loading, original layout/placeholders and malformed/missing resources.
- `DiplomaPresentationTests`: fade/fanfare/new-A/return ordering, held and early A, release/reopen, invalid delta.
- `DaisyDesignerServicesTests`: source designer gate, National internal-ID selection, no separate unlock test, modal command rejection, actor/instruction callback ownership, unchanged collection/inventory/party/RNG/flags, reopening, and clean save roundtrip.

The frame adapter preserves ordering and nominal 60 Hz timing. It does not claim cycle-exact GBA initialization or palette-fade scheduling. Real audible output and full original-emulator visual comparison remain separate acceptance gates; Dummy playback cannot pass the audio gate.

Verified 2026-10-16: Application 25/25, Content 2/2, pure presentation 5/5, ResourceImport 1/1 (four independent Python cases), and script-layout 25/25, with zero skipped. The first Application run exposed the missing native completion route in source-script preflight; that route was fixed and the final test covers the original release/end. Full-National test fixtures then exposed required Unown/Tanoby save facts; explicitly synthetic representatives and unlock receipts were added to the fixtures, not bypasses to production save validation. Earlier failed TRX results remain retained.

```sh
source scripts/env.sh
dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~DaisyDesignerServicesTests -m:1 -nr:false --logger 'trx;LogFileName=diploma-application-passed.trx'
dotnet test tests/Game.Content.Tests/Game.Content.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~DiplomaPresentationContentTests -m:1 -nr:false --logger 'trx;LogFileName=diploma-content.trx'
dotnet test tests/Game.Presentation.Tests/Game.Presentation.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~DiplomaPresentationTests -m:1 -nr:false --logger 'trx;LogFileName=diploma-presentation.trx'
dotnet test tests/Game.ResourceImport.Tests/Game.ResourceImport.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~DiplomaImportTests -m:1 -nr:false --logger 'trx;LogFileName=diploma-import.trx'
python scripts/checks/test-diploma-import.py
python scripts/checks/test-script-layout.py
python scripts/imports/rebuild-resources.py diploma --check
```

The isolated Kanto/National visual-check branches also passed current save validation, full serialization roundtrip, designer interaction, native-screen callback, and return to idle field. They use explicit synthetic collection/hatch/Tanoby facts and live outside the project. The historic generation57604 player checkpoint was byte-checked unchanged. Those branches are test evidence only and are not normal-play completion or a migrated player save.

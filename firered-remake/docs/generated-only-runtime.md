# Generated-only runtime

The running game reads gameplay data and images/audio from `content/generated`. It does not open `content/raw`, original pret C/ASM files, import recipes or source locks. Godot's project font and authored effects remain normal project assets. The `Pret` names still present in some types are historical names, not runtime source parsers.

Import tools own original-format parsing and conversion:

- `scripts/import-story.py`: story program IR, event conditions/movements, text control codes and constants.
- `scripts/import-field-tilesets.py`: layout and metatile behavior/terrain/encounter/layer attributes, including replacement tiles used by gameplay.
- `scripts/import-map-numbers.py`: event map-number lookup.
- Domain importers compile teaching, encounters/trainers and other actual game catalogs. Battle text and fanfare metadata have dedicated small importers.
- `scripts/import-map-atlases.py --prune`: regional map-layer tile pages and compressed index streams. See [render atlas format](map-render-atlases.md).
- Image decoding is a Godot boundary; offline image conversion belongs to import tools. See [generated images](generated-image-assets.md).

Mail's generated vocabulary contains words, selection flags, ordered groups/alphabetical alternatives and actual author character encoding. It no longer repeats source path/line proofs for every word. `python3 scripts/test-mail-composer.py` regenerates these fields from retained original inputs; `GeneratedMailComposerTests` exercises generated-only loading and progression-based selection.

## Focused verification

From the project root after building:

```sh
source scripts/env.sh
dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-restore --filter 'FullyQualifiedName~GeneratedOnlyCampaignTests|FullyQualifiedName~CampaignCompositionTests'
dotnet test tests/Game.Content.Tests/Game.Content.Tests.csproj --no-restore
```

`GeneratedOnlyCampaignTests` copies only generated resources to an isolated root, loads the current campaign, creates/saves/continues a new game, moves, and validates the existing generation-57604 checkpoint without changing its payload, RNG or generation. Map PNG originals are absent after atlas finalization. Focused tests do not constitute a full campaign replay or audiovisual acceptance.

Original source revision and conversion commands remain in import tools. `content/raw/local` contains three genuine palette variants; it is not needed to run the game. Some broader domain types still carry provenance metadata and historical names; those should not be confused with required original-file access.

## Current integration check (2026-10-15)

Official Godot 4.7.2 .NET on .NET 10 loaded the generated-only resource snapshot and existing Saffron checkpoint. A native graphical check exercised the town map/NPCs, contextual menu, Bag item/key-item pockets, TM Case icons/HM list and option-frame Type 1→Type 2 selection. Normal window close completed without logged errors. Audio used Dummy: synthesized music started, but this is not a real-speaker/listening check. Forced engine `--quit-after` with active audio can bypass the game's normal audio-retirement shutdown path; the ordinary window-close path was tested separately.

## Compact runtime artifacts (2026-10-15)

Story programs store their shared source path once per program, instruction line numbers individually, and only exceptional symbol overrides. The reader reconstructs the same typed instructions; save and gameplay types are unchanged. The artifact is 3,268,494 bytes (previously 5,378,134). Shared TM/HM compatibility retains actual species/machine eligibility without source-path proof records or Domain provenance gates.

Generated PNG encoding uses exact RGBA palette entries where this is smaller, including RGB values under zero alpha. All 2,077 images decoded identically in native Godot across 18,532,236 pixels; total PNG size is 2,208,552 bytes (previously 27,087,301). This is a disk/transfer reduction, not a claim of reduced decoded texture memory or faster rendering.

After these changes the full maintained solution build passed with zero warnings/errors and all 2,431 tests passed. The generated-only checkpoint retained generation 57604 and its payload hash. A second graphical check rendered the indexed map/NPC assets, moved two steps, displayed the town sign dialogue, and closed normally without logged errors.

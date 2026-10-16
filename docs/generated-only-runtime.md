# Generated-only runtime

The running game reads gameplay data and images/audio from `content/generated`. It does not open `content/raw`, original pret C/ASM files, import recipes or source locks. Godot's project font and authored effects remain normal project assets. The `Pret` names still present in some types are historical names, not runtime source parsers.

Import tools own original-format parsing and conversion:

- `scripts/imports/story/import-story.py`: story program IR, event conditions/movements, text control codes and constants.
- `scripts/imports/maps/import-field-tilesets.py`: layout and metatile behavior/terrain/encounter/layer attributes, including replacement tiles used by gameplay.
- `scripts/imports/maps/import-map-numbers.py`: event map-number lookup.
- Domain importers compile teaching, encounters/trainers and other actual game catalogs. Battle text and fanfare metadata have dedicated small importers.
- `scripts/imports/maps/import-map-atlases.py --prune`: regional map-layer tile pages and compressed index streams. See [render atlas format](map-render-atlases.md).
- Image decoding is a Godot boundary; offline image conversion belongs to import tools. See [generated images](generated-image-assets.md).

Mail's generated vocabulary contains words, selection flags, ordered groups/alphabetical alternatives and actual author character encoding. It no longer repeats source path/line proofs for every word. `python3 scripts/checks/test-mail-composer.py` regenerates these fields from retained original inputs; `GeneratedMailComposerTests` exercises generated-only loading and progression-based selection.

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

## Runtime diagnostics

Ordinary player sessions log compact ready/command summaries and errors; they do not serialize the complete campaign state or emit routine play-time clock records. Set `FIRERED_VERBOSE=1` for detailed console diagnostics, or set `FIRERED_EVIDENCE` to capture the established detailed evidence format. Evidence capture deliberately includes full ready/command state for verification tools; do not enable it for normal play.

The startup simplification passed a zero-warning solution build and all 2,435 maintained tests. Native checkpoint runs with diagnostics off and evidence on retained the same generation/payload hash; the detailed evidence payload remains available only when explicitly requested. The Pokemon import-family refactor compared 645 temporary output files, including all JSON fields and 508 decoded PNGs, without changing runtime resources.

## Gameplay JSON cleanup

Learnset rows retain move and level semantics without per-row source spans. Generated top-level input inventories are tool bookkeeping and no longer belong to runtime documents. Breeding/ordinary-evolution/gift-egg metadata retain egg groups/cycles, base egg species and ordered egg moves; their repeated machine-move lists are projected from the existing shared compatibility catalog and preserved 58-slot machine mapping (including HM08, without enabling a teachable HM08 definition). All 188 metadata rows match frozen gameplay values.

Breeding can regenerate from retained raw files with `python3 scripts/imports/pokemon/import-breeding-species.py - . . --check`; `-` selects ordinary raw input, not a claim of Git authentication. Ordinary-evolution and gift-egg importers retain their `--dependency-root . --project-root . --check` entry points.

This JSON slice reduced 1,295 files / 30,320,868 bytes to 1,293 files / 26,067,341 bytes: 1,755,435 bytes of unused/duplicated semantics and 2,498,092 bytes of formatting. The same JSON set compressed with ZIP DEFLATE level 9 changed from 3,655,177 to 3,476,165 bytes; raw-file savings are not archive savings. Current direct-output producers emit the compact form, with parsed values and array order preserved. Two unused source-input receipts were removed.

Machine item wrappers now retain the actual displayed description and item definition only; redundant original declaration text/source spans and six Domain proof checks were removed. Gameplay checks for machine category, consumption, eligibility and PP remain. Exact comparison of 27 affected documents changed only those two removed fields, saving 54,525 bytes; this is primarily a responsibility cleanup rather than a large storage reduction.

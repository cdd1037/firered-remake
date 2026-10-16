# Trainer Card and original Photo Printer

The own Trainer Card now has its original front/back input contract: A flips front to back; B flips back to front; B on front or A on back returns to the start menu. The flip hides text, portraits, badges, stars, stickers and photos while deforming the card layer, retaining the static background. Supported earned stars select the source blue, green, bronze and silver palettes.

Supported stars use existing campaign facts and shared Pokédex completion rules:
- A known nonzero first Hall of Fame time, including seconds. A legacy unknown debut is not reconstructed from the current clock or Hall of Fame count.
- All first 150 Kanto entries caught, excluding Mew.
- The original National completion predicate, excluding Mew, Lugia, Ho-Oh, Celebi, Jirachi and Deoxys.

The multiplayer Berry Picking/Pokémon Jump star is unavailable. This is the single-player card surface, not a claim of complete multiplayer Trainer Card behavior. Link wins/losses, link trades, Union Room and Berry Crush rows remain absent; NPC trade receipts are not substituted for link trades. The back displays known nonzero Hall of Fame debut time. For a legacy Hall of Fame record with an unknown debut, an explicit unavailable note is shown. Existing play-time provenance remains visible on the front.

## Earned stickers and photographs

The back renders only the actual stored Hall of Fame and egg sticker awards, using the original two 16×16 sticker images and four source palettes. It does not automatically award stickers from counters. The original Sticker Man script remains the award owner.

The Celadon Game Corner's original east-facing background event opens the original Photo Printer script. Declining or having less than 50 leaves the old photograph and money unchanged. Accepting with sufficient money removes 50 before the original forced four-choice tint menu. Normal, black, pink and sepia use the original RGB555 fixed-point tint calculation. The original flash and 60-frame script delay precede the snapshot.

The shared `SpeciesToMailSpecies` implementation stores six species-or-egg values, including Unown form encoding, in the original persistent script variables. The snapshot survives party changes, subsequent hatching/evolution, saving and Continue. Reprinting replaces all six slots and clears unused positions. Icon presentation reuses shared species/Unown/egg assets; the first icon frame is used. No new parallel photo ownership or historical-party record is introduced.

## Resources and checks

Original inputs are pinned to pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`. The Trainer Card import recipe now includes the supported earned-color palettes, star palette and sticker graphics/palettes. Runtime loads only generated data; source paths stay in tooling. Existing blue front/back resource identifiers remain valid for downstream badge consumers.

Targeted tests cover source-script pricing/cancellation, all four tints, snapshot persistence and immutability, egg/Unown conversion, stale revisions, malformed saved variables, known/unknown debut and both completion stars, source sticker/star pixels and original script ordering. Native visual verification and real audio are separate acceptance checks; no full-game or speaker-output pass is implied by these tests.

## Verification, 2026-10-16

The combined serial solution build passed with 0 warnings and 0 errors (.NET SDK 10.0.401 / runtime 10.0.12). Focused final results: 9 Application photo/card cases, 2 card Content cases, 1 shared Mail composer Content regression, and 1 ResourceImport wrapper passed, with no skips. Independent Python checks passed: 2 card source/pixel cases, 8 special-presentation regeneration cases, 4 shared menu cases, 3 Sticker Man cases and 25 script-layout cases. The ResourceImport wrapper reruns the two Python card cases; these are not additional unique cases.

The initial Application attempt exposed fixture helpers stopping before the final page of a yes/no question and a synthetic caught set missing its seen set. After those fixture corrections, the next run exposed a real missing static menu import for `MULTICHOICE_TRAINER_CARD_ICON_TINT`. The shared menu selection was extended and the source story regenerated; the final nine-case rerun passed. No fallback menu or invented choices were substituted.

Commands used the standard serial build and no-build targeted tests:

```sh
source scripts/env.sh
dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~TrainerCardPhotoTests' -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/Game.Content.Tests/Game.Content.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~GeneratedMailComposerTests|FullyQualifiedName~TrainerCardPresentationTests' -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/Game.ResourceImport.Tests/Game.ResourceImport.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~TrainerCardPhotoImportTests' -m:1 -nr:false -p:UseSharedCompilation=false
python scripts/checks/test-trainer-card-photo-import.py
python scripts/checks/test-special-presentation-importers.py
python scripts/checks/test-story-menus.py
python scripts/checks/test-sticker-man-import.py
python scripts/checks/test-script-layout.py
```

An optional `FIRERED_TRAINER_CARD_FIXTURE_DIR` on the Application test run emits four strictly validated visual test saves and an explanatory README. With that variable unset, these tests write no fixture files. The emitted fixtures inherit the shared test checkpoint's explicitly synthetic hatch total; they are never a replacement player save. Each photograph was purchased through the original source script, includes its actual six-member party (including an egg), and preserves the unknown historical Hall of Fame debut. No synthetic sticker awards or achievement times are added.

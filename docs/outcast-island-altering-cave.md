# Outcast Island and Altering Cave

## Implemented source scope

The Six Island package now includes the northward connection from Green Path to Outcast Island, and the original Outcast Island entrance at (7, 21) into Altering Cave. Both maps use their pinned layouts, tilesets, terrain, objects, warp coordinates, and map-header field restrictions. The cave exit is (18, 22); the outdoor Escape Rope return anchor is (7, 22).

Outcast Island includes six trainer actors sharing five original first-battle teams, including the Ava/Geb double battle. The Rocket's original `FLAG_HIDE_FIVE_ISLAND_ROCKETS` visibility condition is preserved. Its ordinary item ball is PP Up; the original hidden items are Star Piece and Net Ball. Surf and all three rods use their FireRed source slots.

Altering Cave uses only its ordinary Zubat encounter header. The pinned `src/event_data.c` initializes `VAR_ALTERING_CAVE_WILD_SET` to zero; `src/wild_encounter.c` offsets the header for external selections. The eight numbered alternative distribution tables are not active, and no external-event selector, gift workflow, or invented interaction is provided. The cave has no source actors or background interactions and does not require Flash.

All new encounter and trainer species are already present in the canonical current-game admissions: 196 owned species and 220 opponent species. This branch adds reachable collection opportunities without expanding those admission sets.

## Shared boundaries

Ordinary encounter imports select one unnumbered version header per map and reject duplicate defaults. Numbered externally selected variants are not treated as additional ordinary encounter tables. Six Island map imports, trainer definitions, pickups, story instructions, and encounters continue through the existing shared resource formats.

The obsolete Six Island excluded-map validation list is removed. Loaded map dimensions determine connections. Preset doubles use authenticated script shape and actor/battle bindings rather than adding Ava/Geb to Water Path-specific admission checks. The existing regional battle-generation paths preserve their RNG semantics.

## Reconstruction

Run from the repository root using the existing pinned raw source cache:

```
python scripts/imports/maps/import-six-island-region.py --raw-root content/raw/pokefirered/037335f4 --dependency-root . --project-root .
python scripts/imports/story/import-six-island-rules.py content/raw/pokefirered/037335f4 . .
python scripts/imports/items/import-six-island-items.py content/raw/pokefirered/037335f4 . .
python scripts/imports/story/import-six-island-encounters.py content/raw/pokefirered/037335f4 .
python scripts/imports/maps/import-field-tilesets.py
python scripts/imports/presentation/import-shared-overworld.py
python scripts/imports/maps/import-map-atlases.py
source scripts/env.sh
python scripts/imports/story/import-story.py
```

Each importer supports `--check`. Newly required source files were fetched directly from pret/pokefirered commit `037335f4c725d7c9aecdac87066f2002b4bd7e14`; no upstream code was executed and no ROM was used. Altering Cave has no `text.inc` in that source revision; its absence is not replaced with invented dialogue.

## Verification

These checks use controlled initial states followed by public commands; they do not claim an uninterrupted new-game playthrough or native-window/audio acceptance.

- Pinned map, trainer, item, and encounter importers pass `--check`; story regeneration also passes `--check`.
- `python scripts/checks/test-fishing-import.py`: 3 passed, including default-only Altering Cave selection and duplicate-default rejection.
- `python scripts/checks/test-script-layout.py`: 8 passed.
- Focused Content: 23 passed, zero skipped.
- Focused Application (second run): 48 passed, one Surf-music restoration failure; the shared fix is included in the subsequent full verification.
- Serial full solution build, including Godot: zero warnings/errors (`dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false`). SDK 10.0.401, installed/used .NET 10 runtime 10.0.12, Godot 4.7.2 .NET.
- Default seven-project suite: 2,553 tests, 2,551 passed, two failed, zero skipped. Application 374/374, Battle 1,606/1,606, Domain 248/248, Presentation 19/19, Process 1/1 all passed. The two failures were Content’s shared map-atlas inventory and ResourceImport’s compact machine-document byte check.
- The new maps were then registered in the shared map atlas, field tilesets and overworld reconstruction. Six Island’s item importer now emits the required compact JSON directly and its `--check` agrees with the shared machine checker.
- Affected project reruns passed: Content 278/278 (including two new all-cell/border metatile cases), ResourceImport 29/29, zero skipped. The full suite was not repeated for these local generated-resource fixes. Latest results across all seven projects total 2,555 passing cases.
- Shared field tilesets, overworld catalog and map atlas regeneration all pass `--check`; the atlas verifies 3,168 images (384 original images compared and 2,784 recovered from existing packs). Both newly generated full-map images were inspected locally; this is not a native Godot window check.
- The added metatile test initially failed to compile because it used `IFieldMap` for source cells. It was corrected to the existing `SourceMaps` view before the passing Content rerun.

The initial parallel solution-build invocation stopped immediately with `Build FAILED`, zero warnings/errors and no further diagnostics. The serial retry succeeded; the original failed attempt is retained in the verification record rather than counted as success.

Initial focused checks found and corrected two integration defects: the three newly needed story constants were missing, and placing the cave table in the old regional encounter list bypassed the shared map-derived land contract. The cave now uses that ordinary contract directly. Tests also caught doubles restoring route music after a Surf battle; return now uses the existing shared field-travel music resolver.

The representative checkpoint is after the Warehouse/Celio sequence. Outcast's Rocket is therefore correctly hidden, and clearing that flag alone violates existing quest-progress validation. Its source team/script and hidden condition are covered, but a public pre-Warehouse Rocket battle is not claimed. The other five trainer actors are exercised publicly. Earlier checkpoint copies present here have the same completed progression and are not substituted as early-game evidence.

Final affected-project commands:

```
dotnet test tests/Game.Content.Tests/Game.Content.Tests.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/Game.ResourceImport.Tests/Game.ResourceImport.Tests.csproj --no-build --no-restore -m:1 -nr:false
```

The initial default run used `dotnet test FireRed.Current.slnx --no-restore --no-build -m:1 -nr:false`. No audio tests or native-window interaction acceptance were substituted for the project checks.

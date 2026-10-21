# Pewter museum fossil pictures

The two existing museum exhibit scripts now open their original fossil pictures through
the ordinary script-picture/dialog lifecycle. This slice adds no maps, collectible items,
Pokédex entries, font changes, sound changes or battle rules.

## Source and runtime

- Source is pret `037335f4c725d7c9aecdac87066f2002b4bd7e14`.
- `data/maps/PewterCity_Museum_1F/scripts.inc` already supplies the Aerodactyl and
  Kabutops scripts, species arguments, picture position `(10, 3)`, original explanatory
  text, and open/close/release order.
- `src/script_menu.c:OpenMuseumFossilPic` binds each species to its fossil sprite and
  embedded palette. The two authored inputs are
  `graphics/script_menu/aerodactyl_fossil.png` and `kabutops_fossil.png`, each indexed
  64×64. There are no separate authored `.pal` files for these pictures.
- `scripts/imports/presentation/import-museum-pictures.py` reads those bindings and
  source species constants, converts palette channels through RGB5, and preserves
  transparent index zero. Only named picture identities, species IDs, dimensions,
  relative texture names and PNG pixels enter `content/generated/ui/museum-pictures`.
  No source paths, raw data or fixed-file hashes are required at runtime.
- `CampaignMonPicture.AssetId` selects a named picture while retaining the existing
  species and tile-coordinate contract. Null keeps the ordinary Pokémon portrait path.
  The museum uses `aerodactyl-fossil` and `kabutops-fossil`, so a live Pokémon sprite is
  never substituted for an exhibit.
- The source frame surrounds a 64×64 interior with an 8-pixel border. The sprite is
  drawn at `(left×8+8, top×8+8)`; `SetStdWindowBorderStyle` uses the player's selected
  window frame, so the existing shared `DrawUserFrame` renderer is used. Texture
  creation follows shared `SceneTextures` ownership and validates dimensions when
  Godot decodes each texture.

Opening an exhibit locks the field and blocks saving. Acknowledging its ordinary text
closes the picture and releases the field. It can be inspected repeatedly. No species
is registered seen or caught, no item is awarded, and party, inventory and RNG remain
unchanged. Unknown picture identities and invalid native operands fail explicitly.

## Reproduction and checks

```sh
python scripts/imports/presentation/import-museum-pictures.py
python scripts/imports/presentation/import-museum-pictures.py --check
python scripts/checks/test-museum-pictures.py

source scripts/env.sh
dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-build --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --filter FullyQualifiedName~MuseumPictureServicesTests
dotnet test tests/Game.Content.Tests/Game.Content.Tests.csproj --no-build --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --filter FullyQualifiedName~MuseumPicturesTests
dotnet test tests/Game.ResourceImport.Tests/Game.ResourceImport.Tests.csproj --no-build --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --filter FullyQualifiedName~MuseumPictureImportTests
```

The import checks cover both complete RGBA images, source bindings, isolated cold
input conversion, read-only `--check`, stale-output detection, and missing-input
failure without partial output. The application tests start from explicitly controlled
positions facing each original exhibit, then use public interaction and acknowledgement
commands for open/close/reopen, field lock, save blocking, invariants and save/Continue.
Invalid-operand checks are direct native-handler checks, not claims about reachable
ordinary scripts.

These bounded tests do not establish a natural new-game museum route, original
frame-by-frame native-window parity or actual audio output. Diploma and photo-printer
screens are outside this slice.

Verified 2026-10-16 with SDK 10.0.401 and .NET runtime 10.0.12:

- Application: 5/5 passed, zero skipped. This comprises two original exhibit command
  cases and three explicitly invalid native-operand cases.
- Content: 6/6 passed, zero skipped. The positive loader case copies only the three
  generated files to an isolated root without `content/raw`; malformed, duplicate,
  empty, out-of-bounds metadata and unsafe-path cases fail explicitly.
- ResourceImport: 1/1 passed, zero skipped, running the five Python import checks.
  The same Python checks also passed directly; these overlapping executions are not
  added together. The unrelated common script-layout regression passed 24/24.
- `git diff --check` passed. Native Godot rendering remains for separate validation.

The three test commands above were executed without `--no-build`, retaining
`--no-restore -m:1 -nr:false -p:UseSharedCompilation=false`, and each returned exit 0.
They used `--logger 'trx;LogFileName=…'` with `museum-services.trx`,
`museum-pictures-content.trx` and `museum-pictures-import.trx` respectively under
their projects' `TestResults` directories. The centralized solution build is outside
this slice's individual test claim.

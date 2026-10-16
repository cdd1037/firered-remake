# Treasure Beach / beachcombing

Pinned reference: pret/pokefirered `037335f4c725d7c9aecdac87066f2002b4bd7e14`.

## Source and integration

- The original 24×40 `OneIsland_TreasureBeach` layout connects north to One Island with offset zero. Its water corridor and sand/grass shoreline remain source blocks, with no synthetic entrance or shortcut.
- The source boy dialogue and Swimmer Amara are imported through the ordinary script/actor and trainer registries. Amara has two level-36 Seel and a level-36 Dewgong, all source IV parameter zero.
- All eight hidden item events retain their positions, quantities and independent flags: two Stardust, two Pearl, two Ultra Ball, one Star Piece and one Big Pearl. The existing global 1,500-step renewable-item sampling and actual map-entry hook govern reappearance; collection does not reset every time the map is entered.
- Source land encounter rate is 21; Surf is 2; fishing is 20. Land uses the ordinary catalog-bound process. The shared generated-header decoder now reads five-slot water tables, and composition checks overlapping source definitions before merging additional mounted tables. Three rods are admitted by the existing source fishing catalog when the map is mounted.
- `allow_escaping=false` keeps Escape Rope unavailable. The outdoor route type uses the shared Fly departure rules; the beach does not acquire a Fly destination. Town Map uses the source Treasure Beach section and actual map dimensions.
- No new save schema, map-name admission exception or fixed predecessor count is added. First-Sevii trainer loading validates actual membership and source-occurrence keys instead of a duplicate trainer/line allowlist; the existing compiled-script and trainer registry checks remain authoritative. The four biker scene calls retain their separate replay/sequence rules.

## Reproduction

Use the first-Sevii map/rule, field-tileset, shared-overworld, town-map, story and map-atlas import commands in `resource-reconstruction-guide.md`. The story recipe explicitly includes the beach script/text and its eight hidden flags. Raw map scripts/text and layout binaries were fetched from the pinned source; no ROM is used.

Verification uses controlled valid checkpoint positions and supplies, then public commands. It does not establish a natural new-game playthrough, native-window pixel comparison or actual audio output. Fonts and audio implementation are unchanged. Exact test outcomes are recorded after execution below.

## Focused verification (2026-10-16 UTC)

Actual Linux toolchain: .NET SDK 10.0.401, MSBuild 18.9.11, host/runtime 10.0.12; target net10.0.

```sh
source scripts/env.sh
dotnet test tests/Game.Content.Tests/Game.Content.Tests.csproj --no-restore \
  --filter 'FullyQualifiedName~TreasureBeachTests|FullyQualifiedName~FirstSeviiTrainerCatalogTests' \
  -v minimal --logger 'trx;LogFileName=treasure-beach-content-final.trx'
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-restore \
  --filter FullyQualifiedName~TreasureBeachTests \
  -v minimal --logger 'trx;LogFileName=treasure-beach-application-final.trx'
```

Results: Content **8 passed**, Application **10 passed**, zero failures or skips. The project-local `TestResults` directories retain the named final TRX files.

The application cases exercise reciprocal One Island water connection, public Surf dismount/remount, boy dialogue, Amara victory and defeated-trainer repeat text, eight independent hidden-item pickups, renewal after the 1,499→1,500 step transition on actual map entry, and Continue without spurious regeneration. They also capture from grass, Surf and each rod through public battle commands and round-trip strict saves. Escape Rope rejects atomically; public Fly departs to One Island.

Content checks cover original dimensions/events, exact land/Surf/rod slots and rates, renewable flag membership, malformed water headers, and trainer membership/key errors without historical roster gates. An additional existing-content filter passed **11/11**; these overlap other suites and are not additive regression totals.

Map/rules, field-tileset, shared-overworld, town-map and map-atlas `--check` passed, as did the 11-case map-importer unification checker. The final story compile/exact check passed with 4,144 programs and 4,190 text labels. Atlas checks compare available original layers and reconstructed packed layers; they do not establish global original-pixel parity or native-window rendering.

Earlier failures remain visible: the first water merge used `SetItems` on an interface and failed compilation; adding the generated hidden items before their story constants caused `first_sevii_pickup_flag`; the application fixture initially exceeded the single Master Ball gift bound. All were corrected rather than weakening validation. Restoring the existing story recipe order required one regeneration after an exact-check stale-resource failure. The town-map recipe also had to include the new source input. These checks were rerun successfully.

## Default-solution regression

```sh
source scripts/env.sh
dotnet build FireRed.Current.slnx --no-restore -m:1 -v minimal
dotnet test FireRed.Current.slnx --no-build --no-restore -m:1 \
  --logger 'trx;LogFilePrefix=treasure-beach-final' \
  --results-directory .toolhome/test-results/treasure-beach-full -v minimal
```

Build, including Godot: **zero warnings/errors**. The default suite ran **2,522 cases: 2,521 passed, one failed, zero skipped**. Project totals: Application 353, Battle 1,605, Content 267, Domain 248, Presentation 19, Process 1, ResourceImport 29. All failures were confined to the ResourceImport test whose old Python fixture asserted Treasure Beach was not mounted. The fixture now requires Treasure Beach among mounted source fishing maps while retaining Outcast Island as unmounted. All source-slot comparisons remain unchanged.

Only that affected fixture was rerun after this local test correction:

```sh
dotnet test tests/Game.ResourceImport.Tests/Game.ResourceImport.Tests.csproj \
  --no-build --no-restore --filter FullyQualifiedName~OrdinaryFishingImportTests \
  --logger 'trx;LogFileName=treasure-beach-fishing-fixture-fixed.trx' \
  --results-directory .toolhome/test-results/treasure-beach-full -v minimal
```

Result: **1/1 passed**, no failures/skips. The broad-run failure and targeted corrective run are separate preserved artifacts; the full suite was not redundantly repeated. Default tests exclude opt-in audio tests. No native-window or actual-speaker check was performed for this slice.

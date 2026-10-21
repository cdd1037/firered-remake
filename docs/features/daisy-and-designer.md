# Daisy grooming and Kanto designer

This slice adds no maps and reads only generated runtime resources. It uses the retained original field scripts for the branches, text, waits and return to field. Source audit is pinned to pret `037335f4c725d7c9aecdac87066f2002b4bd7e14`.

## Daisy

- `data/maps/PalletTown_RivalsHouse/scripts.inc` gates the post-League service with `FLAG_SYS_GAME_CLEAR`. Earlier Town Map dialogue remains unchanged.
- Below 500 in `VAR_MASSAGE_COOLDOWN_STEP_COUNTER`, Daisy rates the first non-egg party member, including a fainted member. Source fallback for an all-egg party is slot zero. The existing `SourceGiftEggRules.GetLeadMonFriendship` supplies source bands: 0, 1–49, 50–99, 100–149, 150–199, 200–254, 255.
- At 500, the source yes/no branch opens the existing occupied-party chooser renderer. Cancel returns source slot 6; an egg receives the source refusal without consuming cooldown. Fainted Pokémon are eligible.
- `src/field_specials.c:DaisyMassageServices` calls `AdjustFriendship(...MASSAGE)` then resets the counter to zero. The shared friendship calculation applies base +3, Soothe Bell rounding first, then Luxury Ball and matching met-section bonuses, capped at 255. Grooming does not heal HP or change egg cycles.
- The existing walking-step hook saturates the counter at 500. Completed transactions persist friendship and the ordinary script counter; in-progress callback/chooser states cannot be saved. Selection is bound to the actual occupied slot and instance ID.
- Keyboard and pointer input use the same command transaction; the party chooser draws through `DrawServicePartyChooser` and retains the existing transition-input lock. Native-window visual/input validation is still pending.

## Designer

`src/pokedex.c:HasAllKantoMons` checks caught National Dex entries 1–150 inclusive, excluding Mew alone. Seen entries, Mew, and later-generation catches cannot replace a missing Kanto catch. A single normal FireRed save is not asserted to be able to obtain every entry without external dependencies.

The original condominium 3F script reaches its incomplete-Dex reply or congratulations normally. The subsequent collection slice replaces the initial unavailable-screen notice with the original Kanto/National native certificate and the suspended source waitstate callback. See [Pokédex diploma](pokedex-diploma.md) for the resource, completion, input and return-to-field contracts.

## Verification boundary

Focused generated-only application tests exercise the original scripts from controlled valid checkpoint states. Domain tests cover massage bands, bonus ordering, cap, eggs/fainting, and all 150 missing-entry cases. These are not natural new-game-to-League route evidence. The historic checkpoint remains untouched and still requires an explicit known lifetime hatch count migration; test fixtures retain their documented synthetic counter.

Badge-topic list menus, museum fossil pictures and the diploma were completed by subsequent slices; their dedicated documents own the current verification boundaries. Photo-printer behavior, communication facilities and e-Reader content remain outside this slice. Actual audio output and full original visual parity remain open.

Reproducible commands (SDK 10.0.401, target/runtime major 10):

```sh
source scripts/env.sh
dotnet build FireRed.Current.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/Game.Domain.Tests/Game.Domain.Tests.csproj --no-build --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --filter FullyQualifiedName~DaisyGroomingAndKantoPokedexTests --logger 'trx;LogFileName=daisy-kanto-domain-final.trx'
dotnet test tests/Game.Application.Tests/Game.Application.Tests.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --filter 'FullyQualifiedName~DaisyDesignerServicesTests|FullyQualifiedName~GeneratedOnlyCampaignTests|FullyQualifiedName~faint_replacement_preserves_continuation_and_charges_friendship_once' --logger 'trx;LogFileName=daisy-designer-regressions.trx'
```

The leading-egg rating case uses an authenticated egg receipt plus a fainted non-egg. All-egg and empty-party fallback checks call the shared rating rule directly: the PC correctly prevents depositing the last usable Pokémon, so these checks do not claim an ordinary all-egg player save is reachable. No full battle suite is required or claimed by this native-service slice.

Verified 2026-10-16: domain 4/4 and application 25/25 (22 service cases, 2 generated-only checks, 1 existing faint-friendship regression), zero skipped. TRX evidence is under each test project's `TestResults` directory using the filenames above. The first domain test compilation exposed a missing namespace import in the new test; that import was corrected and both later domain runs passed. No production-test failures were hidden or golden outputs rewritten.

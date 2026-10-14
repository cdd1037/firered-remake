using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.Items;
using Game.Domain.Encounters;
using Game.Domain.Roaming;
using Game.Domain.Slots;

namespace Game.Content;
public sealed partial class PretCampaignCatalog
{
    /// <summary>Builds the sole current game from its authenticated source predecessors, in source fingerprint order.</summary>
    public static PretCampaignCatalog LoadCurrent(string projectRoot)
    {
        // Load the fixed resource set through its source-authenticating factories.
        var eventIsland = PretEventIslandRegion.Load(projectRoot);
        var powerPlant = PretPowerPlantRegion.Load(projectRoot);
        var trainerTower = PretTrainerTowerRegion.Load(projectRoot);
        var fightingDojo = PretFightingDojoRegion.Load(projectRoot);
        var fossilLab = PretFossilLabRegion.Load(projectRoot);
        var itemfinder = PretItemfinderSource.Load(projectRoot);
        var fameChecker = PretFameChecker.Load(projectRoot);
        var vsSeeker = PretVsSeeker.Load(projectRoot);
        var vsSeekerPresentation = PretVsSeekerPresentation.Load(projectRoot);
        var vsSeekerTrainers = PretVsSeekerParties.Load(projectRoot);
        var vsSeekerOccurrences = PretVsSeekerParties.LoadOccurrences(projectRoot);
        var slotRules = SlotMachineRules.FromAuthenticatedJson(File.ReadAllBytes(Path.Combine(projectRoot, "content/generated/slot-machine/rules.json")));
        var sevenIsland = PretSevenIslandRegion.Load(projectRoot);
        var ceruleanCave = PretCeruleanCaveRegion.Load(projectRoot);
        var fiveIsland = PretFiveIslandRegion.Load(projectRoot, compact: true);
        var fiveIslandTrainers = PretFiveIslandRules.LoadTrainers(projectRoot);
        var sixIsland = PretSixIslandRegion.Load(projectRoot, compact: true);
        var sixIslandTrainers = PretSixIslandRules.LoadTrainers(projectRoot);
        var fourIsland = PretFourIslandRegion.Load(projectRoot, compact: true);
        var fourIslandTrainers = PretFourIslandRules.LoadTrainers(projectRoot);
        var seviiRuby = PretSeviiRubyRegion.Load(projectRoot, compact: true);
        var seviiRubyTrainers = PretSeviiRubyRules.LoadTrainers(projectRoot);
        var nationalDexSource = PretNationalDexSource.Load(projectRoot);
        var twins = PretRoute8Twins.Load(projectRoot);
        var doubleMechanics = SourceMechanicsCatalogLoader.LoadAuthenticated(projectRoot);
        var npcTrades = PretNpcTrades.Load(projectRoot, doubleMechanics);
        var npcTradeElyssa = PretNpcTradeElyssa.Load(projectRoot);
        var npcTradeHaden = PretNpcTradeHaden.Load(projectRoot);
        var npcTradeMail = PretNpcTradeMail.Load(projectRoot);
        var goodRodService = PretGoodRodService.Load(projectRoot);
        var superRodService = PretSuperRodService.Load(projectRoot);
        var seafoam = PretSeafoamRegion.Load(projectRoot);
        var seafoamTrainers = PretSeafoamRules.LoadTrainers(projectRoot);
        var moveReminderHouse = PretMoveReminderHouse.Load(projectRoot);
        var moveDeleterHouse = PretMoveDeleterHouse.Load(projectRoot);
        var originalTutorRooms = PretOriginalTutorRooms.Load(projectRoot);
        var copycatHouse = PretCopycatHouse.Load(projectRoot);
        var capeBrinkTutor = PretCapeBrinkTutor.Load(projectRoot);
        var route16FlyHouse = PretRoute16FlyHouse.Load(projectRoot);
        var route5DayCare = PretRoute5DayCare.Load(projectRoot);
        var fourIslandDayCare = PretFourIslandDayCare.Load(projectRoot);
        var originalTmAcquisition = PretOriginalTmAcquisition.Load(projectRoot);
        PretFieldFly.ValidateSourceEvidence(projectRoot);
        var fieldFly = PretFieldFly.Load(projectRoot);
        var fieldRockSmash = PretFieldRockSmash.Load(projectRoot);
        var flyMapPresentation = PretFlyMap.Load(projectRoot);
        var doubleItems = Game.Domain.Battle.SourceHeldItemCatalog.For(doubleMechanics.Catalog) ?? throw new InvalidDataException("Preset doubles requires the authenticated battle item catalog.");
        var magikarp = PretMagikarpContract.Load(projectRoot);
        var runtimeMaps = PretRuntimeMapCatalog.Load(Path.Combine(projectRoot, PretRocketHideoutRuntimeMaps.DirectoryName), PretRocketHideoutRuntimeMaps.ManifestSha256);
        var route22 = PretRoute22Region.Load(projectRoot, runtimeMaps);
        var route3 = PretRoute3Region.Load(projectRoot, runtimeMaps);
        var oakPokedex = PretPokedexMenu.Load(projectRoot);
        var route22Pokemon = PretRoute22Pokemon.Load(projectRoot, includeSourceCryWaves: false, pokedexMenu: oakPokedex);
        var route3Pokemon = PretRoute3Pokemon.Load(projectRoot, pokedexMenu: oakPokedex);
        var ceruleanPokemon = PretCeruleanPokemon.Load(projectRoot, pokedexMenu: oakPokedex);
        var pokemonTower = PretPokemonTowerRegion.Load(projectRoot, PretPokemonTowerRuntimeMaps.Load(projectRoot));
        PretPokemonTowerPokemon pokemonTowerPokemon;
        var snorlax = PretSnorlaxRegion.Load(projectRoot);
        PretSnorlaxPokemon snorlaxPokemon;
        var snorlaxArt = PretSnorlaxArt.Load(projectRoot, pokedexMenu: oakPokedex);
        var regionalServices = PretRegionalServiceMaps.Load(projectRoot, compact: true);
        var regionalGift = PretRegionalGiftContract.Load(projectRoot);
        var eeveeArt = PretEeveeArt.Load(projectRoot, pokedexMenu: oakPokedex);
        var departmentStore = PretDepartmentStoreContract.Load(projectRoot);
        var nameRater = PretNameRaterContract.Load(projectRoot);
        PretEeveePokemon eeveePokemon;
        var fuchsiaRoute = PretFuchsiaRouteRegion.Load(projectRoot, compact: true);
        PretFuchsiaRoutePokemon fuchsiaPokemon;
        var fuchsiaGym = PretFuchsiaGymRegion.Load(projectRoot, runtimeMaps.FuchsiaGymPackage(projectRoot));
        var fuchsiaGymTrainers = PretFuchsiaGymTrainers.LoadDocument(projectRoot);
        var fuchsiaGymItems = PretFuchsiaGymItems.LoadProject(projectRoot);
        var fuchsiaGymMachines = PretFuchsiaGymMachines.LoadProject(projectRoot);
        var fuchsiaGymMoveSource = PretMetronomeMoveCatalog.Load(projectRoot);
        var safari = PretSafariRegion.Load(projectRoot, compact: true);
        var safariEncounters = PretSafariEncounters.Load(safari);
        var safariItems = PretSafariItems.LoadProject(projectRoot);
        var safariArt = PretSafariArt.Load(projectRoot);
        PretSafariPokemon safariPokemon;
        SafariOwnedPokemonCatalog safariOwned;
        PretSurfPokemon surfPokemon;
        PretSurfArt surfArt;
        PretSurfEncounters surfEncounters;
        SurfOwnedPokemonCatalog surfOwned;
        var surfScripts = PretSurfScripts.Load(projectRoot);
        var viridianGym = PretViridianGymRegion.Load(projectRoot, compact: true);
        var viridianGymTrainers = PretViridianGymRules.LoadTrainers(projectRoot);
        var leagueRivalTrainers = PretLeagueRivalRules.LoadTrainers(projectRoot);
        var victoryRoad = PretVictoryRoadRegion.Load(projectRoot, compact: true);
        var victoryRoadTrainers = PretVictoryRoadRules.LoadTrainers(projectRoot);
        var victoryRoadItems = PretVictoryRoadItems.LoadProject(projectRoot);
        _ = PretVictoryRoadRules.LoadWild(projectRoot);
        var pokemonLeague = PretPokemonLeagueRegion.Load(projectRoot, compact: true);
        var pokemonLeagueTrainers = PretPokemonLeagueRules.LoadTrainers(projectRoot);
        var firstSevii = PretFirstSeviiRegion.Load(projectRoot, compact: true);
        var firstSeviiTrainers = PretFirstSeviiRules.LoadTrainers(projectRoot);
        var firstSeviiItems = PretFirstSeviiRules.LoadItems(projectRoot);
        var firstSeviiPresentation = PretFirstSeviiPresentation.LoadProject(projectRoot, projectRoot);
        var cinnabar = PretCinnabarRegion.Load(projectRoot, compact: true);
        var cinnabarPokemon = PretCinnabarPokemon.LoadDocument(projectRoot, projectRoot);
        var cinnabarTrainers = PretCinnabarTrainers.Load(projectRoot);
        var cinnabarItems = PretCinnabarItems.LoadProject(projectRoot);
        CinnabarOwnedPokemonCatalog cinnabarOwned;
        CinnabarOpponentPokemonCatalog cinnabarOpponents;
        var saffronGym = PretSaffronGymRegion.Load(projectRoot, compact: true);
        var saffronGymTrainers = PretSaffronGymTrainers.Load(projectRoot);
        var saffronGymArt = PretSaffronGymArt.Load(projectRoot, projectRoot, pokedexMenu: oakPokedex);
        var cinnabarArt = PretCinnabarArt.Load(projectRoot, projectRoot, pokedexMenu: oakPokedex);
        var cinnabarPresentation = PretCinnabarPresentation.Load(projectRoot);
        var saffronGymItems = PretSaffronGymItems.LoadProject(projectRoot);
        var saffronGymPokemon = PretSaffronGymPokemon.LoadDocument(projectRoot, projectRoot);
        SaffronGymOpponentPokemonCatalog saffronGymOpponents;
        var silph = PretSilphRegion.Load(projectRoot, true);
        var silphItems = PretSilphItems.LoadProject(projectRoot);
        var silphTrainers = PretSilphTrainers.Load(projectRoot);
        PretSilphPokemon silphPokemon;
        SilphOwnedPokemonCatalog silphOwned;
        SilphOpponentPokemonCatalog silphOpponents;
        PretSilphArt silphArt;
        var fuchsiaArt = PretFuchsiaRouteArt.Load(projectRoot, pokedexMenu: oakPokedex);
        var pokemonTowerItems = PretPokemonTowerItems.LoadProject(projectRoot);
        var pokemonTowerPickups = PretPokemonTowerItems.LoadPickupsProject(projectRoot);
        var pokemonTowerTrainers = PretPokemonTowerTrainers.Load(projectRoot);
        var rocketHideout = PretRocketHideoutRegion.Load(projectRoot, runtimeMaps);
        var rocketHideoutPokemon = PretRocketHideoutPokemon.Load(projectRoot, pokedexMenu: oakPokedex);
        var rocketHideoutItems = PretRocketHideoutItems.LoadProject(projectRoot);
        var rocketHideoutPickups = PretRocketHideoutItems.LoadPickupsProject(projectRoot);
        var rocketHideoutTrainers = PretRocketHideoutTrainers.Load(projectRoot);
        var celadonGym = PretCeladonGymRegion.Load(projectRoot, runtimeMaps);
        var celadonGymPokemon = PretCeladonGymPokemon.Load(projectRoot, pokedexMenu: oakPokedex);
        var celadonGymItems = PretCeladonGymItems.LoadProject(projectRoot);
        var celadonGymTrainers = PretCeladonGymTrainers.Load(projectRoot);
        var celadonApproach = PretCeladonApproachRegion.Load(projectRoot, runtimeMaps);
        var celadonApproachPokemon = PretCeladonApproachPokemon.Load(projectRoot, pokedexMenu: oakPokedex);
        var celadonApproachItems = PretCeladonApproachItems.LoadProject(projectRoot);
        var celadonApproachEncounters = PretCeladonApproachEncounters.Load(projectRoot);
        var celadonApproachTrainers = PretCeladonApproachTrainers.Load(projectRoot);
        var rockTunnel = PretRockTunnelRegion.Load(projectRoot, runtimeMaps);
        var rockTunnelPokemon = PretRockTunnelPokemon.Load(projectRoot, pokedexMenu: oakPokedex);
        var rockTunnelItems = PretRockTunnelItems.LoadProject(projectRoot);
        var rockTunnelEncounters = PretRockTunnelEncounters.Load(projectRoot);
        var rockTunnelTrainers = PretRockTunnelTrainers.Load(projectRoot);
        var route11 = PretRoute11Region.Load(projectRoot, runtimeMaps);
        var route11Pokemon = PretRoute11Pokemon.Load(projectRoot, pokedexMenu: oakPokedex);
        var route11Items = PretRoute11Items.LoadProject(projectRoot);
        var route11Encounters = PretRoute11Encounters.Load(projectRoot);
        var route11Trainers = PretRoute11Trainers.Load(projectRoot);
        var cityServices = PretCityServices.Load(projectRoot, runtimeMaps);
        var gameCornerCoins = PretGameCornerCoins.Load(projectRoot);
        PretGameCornerHiddenCoins.ValidateSourceEvidence(projectRoot);
        var oldRod = PretOldRod.Load(projectRoot);
        var vermilionGym = PretVermilionGymRegion.Load(projectRoot, runtimeMaps);
        var vermilionGymTrainers = PretVermilionGymTrainers.Load(projectRoot);
        var vermilionGymAssets = PretVermilionGymAssets.Load(projectRoot);
        var ssAnne = PretSsAnneRegion.Load(projectRoot, runtimeMaps);
        var ssAnnePokemon = PretSsAnnePokemon.Load(projectRoot, pokedexMenu: oakPokedex);
        var ssAnneTrainers = PretSsAnneTrainers.Load(projectRoot);
        var ssAnneItems = PretSsAnneItems.LoadProject(projectRoot);
        var vermilionPokemon = PretVermilionPokemon.Load(projectRoot, pokedexMenu: oakPokedex);
        var vermilion = PretVermilionRegion.Load(projectRoot, runtimeMaps);
        var vermilionTrainers = PretVermilionTrainers.Load(projectRoot);
        var billPokemon = PretBillPokemon.Load(projectRoot, pokedexMenu: oakPokedex);
        var bill = PretBillRegion.Load(projectRoot, runtimeMaps);
        var billItems = PretBillItems.LoadProject(projectRoot);
        var billTrainers = PretBillTrainers.Load(projectRoot);
        var cerulean = PretCeruleanRegion.Load(projectRoot, runtimeMaps);
        var mtMoon = PretMtMoonRegion.Load(projectRoot, runtimeMaps);
        var mtMoonPokemon = PretMtMoonPokemon.Load(projectRoot, pokedexMenu: oakPokedex);
        var oakRating = PretOakRating.Load(projectRoot);
        var townMap = PretTownMap.Load(projectRoot);
        // Extend map anchors in source order; each retained predecessor validates current data.
        var maps = PretWorldCatalog.Load(Path.Combine(projectRoot, "content/generated"), runtimeMaps);
        var region = PretViridianRegion.Load(projectRoot, runtimeMaps);
        maps = maps.AddRange(region.NewMaps);
        var center = PretViridianCenter.Load(projectRoot, runtimeMaps);
        maps = maps.Add(center.Map.Id, center.Map);
        var forest = PretForestRegion.Load(projectRoot, runtimeMaps);
        maps = maps.AddRange(forest.NewMaps);
        var pewter = PretPewterRegion.Load(projectRoot, runtimeMaps);
        maps = maps.AddRange(pewter.NewMaps);
        maps = maps.AddRange(route22.NewMaps);
        maps = maps.AddRange(route3.NewMaps);
        maps = maps.AddRange(mtMoon.NewMaps);
        maps = maps.AddRange(cerulean.NewMaps);
        maps = maps.AddRange(bill.NewMaps);
        maps = maps.AddRange(vermilion.NewMaps);
        maps = maps.AddRange(ssAnne.NewMaps);
        maps = maps.AddRange(vermilionGym.NewMaps);
        maps = maps.AddRange(cityServices.NewMaps);
        maps = maps.AddRange(route11.NewMaps);
        maps = maps.AddRange(rockTunnel.NewMaps);
        maps = maps.AddRange(celadonApproach.NewMaps);
        maps = maps.AddRange(celadonGym.NewMaps);
        maps = maps.AddRange(rocketHideout.NewMaps);
        maps = maps.AddRange(pokemonTower.NewMaps);
        maps = maps.AddRange(snorlax.NewMaps);
        var snorlaxCompositionMaps = maps;
        {
            regionalServices.ValidateReusedMaps(maps);
            maps = maps.AddRange(RegionalServiceMapIds.Select(id => KeyValuePair.Create(id, regionalServices.NewMaps[id])));
        }

        var regionalServiceCompositionMaps = maps;
        maps = maps.AddRange(RegionalInteriorMapIds.Select(id => KeyValuePair.Create(id, regionalServices.SourceMaps[id])));
        var regionalInteriorCompositionMaps = maps;
        {
            fuchsiaRoute.ValidateReusedMaps(maps);
            maps = maps.AddRange(fuchsiaRoute.NewMaps);
        }

        var fuchsiaRouteCompositionMaps = maps;
        maps = maps.AddRange(fuchsiaGym.NewMaps);
        var fuchsiaGymCompositionMaps = maps;
        {
            safari.ValidateReusedMaps(maps);
            maps = maps.AddRange(safari.SourceMaps);
        }

        maps = maps.AddRange(silph.NewMaps);
        {
            if (maps.Count != 179 || !JsonElement.DeepEquals(maps[new("map.saffron-city")].Events, saffronGym.ExternalSaffronEvents))
                throw new InvalidDataException("saffron_gym_predecessor_maps");
            maps = maps.AddRange(saffronGym.NewMaps);
        }

        maps = maps.AddRange(cinnabar.NewMaps);
        var firstSeviiPredecessorMaps = maps;
        {
            ValidateFirstSeviiAnchors(firstSevii, maps);
            maps = maps.AddRange(firstSevii.NewMaps);
        }

        var viridianGymPredecessorMaps = maps;
        {
            ValidateViridianGymAnchors(viridianGym, maps);
            maps = maps.AddRange(viridianGym.NewMaps);
        }

        var victoryRoadPredecessorMaps = maps;
        {
            ValidateVictoryRoadAnchors(victoryRoad, maps);
            maps = maps.AddRange(victoryRoad.NewMaps);
        }

        var pokemonLeaguePredecessorMaps = maps;
        {
            ValidatePokemonLeagueAnchors(pokemonLeague, maps);
            maps = maps.AddRange(pokemonLeague.NewMaps);
        }

        var seviiRubyPredecessorMaps = maps;
        {
            ValidateSeviiRubyAnchors(seviiRuby, maps);
            maps = maps.AddRange(seviiRuby.NewMaps);
        }

        var fourIslandPredecessorMaps = maps;
        {
            ValidateFourIslandAnchors(fourIsland, maps);
            maps = maps.AddRange(fourIsland.NewMaps);
        }

        var sixIslandPredecessorMaps = maps;
        {
            ValidateSixIslandAnchors(sixIsland, maps);
            maps = maps.AddRange(sixIsland.NewMaps);
        }

        var fiveIslandPredecessorMaps = maps;
        {
            ValidateFiveIslandAnchors(fiveIsland, maps);
            maps = maps.AddRange(fiveIsland.NewMaps);
        }

        // Preserve script, constant and content-hash ordering across the whole source bundle.
        var sources = new List<PretSourceScriptFile>();
        var textFiles = new List<string>();
        var constants = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var map in maps.Values.OrderBy(x => x.Id.Value, StringComparer.Ordinal))
        {
            string path = map.SourceRef.Path.Replace("map.json", "scripts.inc", StringComparison.Ordinal);
            var f = map.Metadata.Files.Single(x => x.Path == path);
            sources.Add(new(path, map.ScriptSource, new(SourceKind.Pret, path, map.SourceMapSymbol, map.Metadata.Repository, map.Metadata.Commit, f.Sha256)));
            if (map.Metadata.Files.Any(x => x.Path == map.SourceRef.Path.Replace("map.json", "text.inc", StringComparison.Ordinal)))
                textFiles.Add(map.TextSource);
        }

        {
            // The existing map interpreter executes the same authenticated original script;
            // never replace Daisy's program with a hand-written reward shortcut.
            const string daisyPath = "data/maps/PalletTown_RivalsHouse/scripts.inc";
            var source = sources.Single(s => s.Path == daisyPath);
            if (source.SourceRef.Sha256 != townMap.SourceFiles.Single(s => s.Path == daisyPath).Sha256)
                throw new InvalidDataException("town_map_daisy_source_conflict");
        }

        // Vs Seeker item ID is authenticated by the same immutable original item record used by the Bag.
        constants["ITEM_VS_SEEKER"] = vsSeekerPresentation.ItemDefinition.GetProperty("sourceItemId").GetInt32();
        using var l = JsonDocument.Parse(File.ReadAllText(Path.Combine(projectRoot, "scripts/fetch-story.lock.json")));
        var hashes = new List<string>
        {
            PretWorldMap.FullSourceHash
        };
        foreach (var e in l.RootElement.GetProperty("files").EnumerateArray())
        {
            var path = e.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(projectRoot, "content/raw/story", path));
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var header = Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
            if (hash != e.GetProperty("sha256").GetString() || Convert.ToHexStringLower(SHA1.HashData([..header, ..bytes])) != e.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("Story source differs from pinned lock: " + path);
            hashes.Add(path + ":" + hash);
            var text = Encoding.UTF8.GetString(bytes);
            if (path is "data/event_scripts.s" or "data/scripts/movement.inc" or "data/scripts/fame_checker.inc" or "data/scripts/std_msgbox.inc" or "data/scripts/obtain_item.inc")
            {
                sources.Add(new(path, text, new(SourceKind.Pret, path, "common-script-library", "pret/pokefirered", PretWorldMap.SourceCommit, hash)));
                textFiles.Add(text);
            }

            if (path.StartsWith("include/constants/", StringComparison.Ordinal))
                foreach (Match m in Regex.Matches(text, @"^#define\s+([A-Z][A-Z0-9_]+)\s+(0x[0-9a-fA-F]+|[0-9]+)\s*(?://[^\n]*)?$", RegexOptions.Multiline | RegexOptions.CultureInvariant))
                    constants[m.Groups[1].Value] = m.Groups[2].Value.StartsWith("0x", StringComparison.Ordinal) ? Convert.ToInt32(m.Groups[2].Value, 16) : int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        {
            hashes.Add("viridian-region:" + PretViridianRegion.ManifestSha256);
            hashes.AddRange(region.AdditionalSourceHashes);
            var paths = sources.Select(x => x.Path).ToHashSet(StringComparer.Ordinal);
            var textPaths = maps.Values.Select(x => x.SourceRef.Path.Replace("map.json", "text.inc", StringComparison.Ordinal)).Concat(paths).ToHashSet(StringComparer.Ordinal);
            foreach (var source in region.Scripts)
                if (paths.Add(source.Path))
                {
                    sources.Add(source);
                    textFiles.Add(source.Text);
                    textPaths.Add(source.Path);
                }

            foreach (var(path, text)in region.TextSources)
                if (textPaths.Add(path))
                    textFiles.Add(text);
            foreach (var(symbol, value)in region.Constants)
                constants[symbol] = value;
        }

        {
            hashes.AddRange(center.AdditionalSourceHashes);
            var byPath = sources.ToDictionary(x => x.Path, StringComparer.Ordinal);
            var textPaths = maps.Values.Select(x => x.SourceRef.Path.Replace("map.json", "text.inc", StringComparison.Ordinal)).Concat(byPath.Keys).Concat(region.TextSources.Keys).ToHashSet(StringComparer.Ordinal);
            foreach (var source in center.Scripts)
            {
                if (byPath.TryGetValue(source.Path, out var prior))
                {
                    if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                        throw new InvalidDataException("Conflicting authenticated center source: " + source.Path);
                    continue;
                }

                byPath.Add(source.Path, source);
                sources.Add(source);
                textFiles.Add(source.Text);
                textPaths.Add(source.Path);
            }

            foreach (var(path, text)in center.TextSources)
                if (textPaths.Add(path))
                    textFiles.Add(text);
            foreach (var(symbol, value)in center.Constants)
                constants[symbol] = value;
        }

        {
            hashes.AddRange(forest.AdditionalSourceHashes);
            var paths = sources.ToDictionary(x => x.Path, StringComparer.Ordinal);
            foreach (var source in forest.Scripts)
            {
                if (paths.TryGetValue(source.Path, out var prior))
                {
                    if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                        throw new InvalidDataException("Conflicting forest source: " + source.Path);
                    continue;
                }

                paths.Add(source.Path, source);
                sources.Add(source);
                textFiles.Add(source.Text);
            }

            foreach (var text in forest.TextSources.Values)
                if (!textFiles.Contains(text, StringComparer.Ordinal))
                    textFiles.Add(text);
            foreach (var(symbol, value)in forest.Constants)
                constants[symbol] = value;
        }

        {
            hashes.AddRange(pewter.AdditionalSourceHashes);
            var paths = sources.ToDictionary(x => x.Path, StringComparer.Ordinal);
            foreach (var source in pewter.Scripts)
            {
                if (paths.TryGetValue(source.Path, out var prior))
                {
                    if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                        throw new InvalidDataException("Conflicting Pewter source: " + source.Path);
                    continue;
                }

                paths.Add(source.Path, source);
                sources.Add(source);
                textFiles.Add(source.Text);
            }

            foreach (var text in pewter.TextSources.Values)
                if (!textFiles.Contains(text, StringComparer.Ordinal))
                    textFiles.Add(text);
            foreach (var(symbol, value)in pewter.Constants)
                constants[symbol] = value;
        }

        var pokemonStorage = PretPokemonStorageSource.Load(projectRoot);
        {
            hashes.AddRange(pokemonStorage.AdditionalSourceHashes);
            var paths = sources.ToDictionary(x => x.Path, StringComparer.Ordinal);
            foreach (var source in pokemonStorage.Scripts)
            {
                if (paths.TryGetValue(source.Path, out var prior))
                {
                    if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                        throw new InvalidDataException("Conflicting storage source: " + source.Path);
                    continue;
                }

                paths.Add(source.Path, source);
                sources.Add(source);
                textFiles.Add(source.Text);
            }

            foreach (var text in pokemonStorage.TextSources.Values)
                if (!textFiles.Contains(text, StringComparer.Ordinal))
                    textFiles.Add(text);
            foreach (var(symbol, value)in pokemonStorage.Constants)
            {
                if (constants.TryGetValue(symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting storage constant: " + symbol);
                constants[symbol] = value;
            }
        }

        var fieldPickups = PretEarlyPickups.Load(projectRoot);
        {
            hashes.AddRange(fieldPickups.AdditionalSourceHashes);
            var paths = sources.ToDictionary(x => x.Path, StringComparer.Ordinal);
            foreach (var source in fieldPickups.Scripts)
            {
                if (paths.TryGetValue(source.Path, out var prior))
                {
                    if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                        throw new InvalidDataException("Conflicting pickup source: " + source.Path);
                    continue;
                }

                paths.Add(source.Path, source);
                sources.Add(source);
                textFiles.Add(source.Text);
            }

            foreach (var text in fieldPickups.TextSources.Values)
                if (!textFiles.Contains(text, StringComparer.Ordinal))
                    textFiles.Add(text);
            foreach (var(symbol, value)in fieldPickups.Constants)
            {
                if (constants.TryGetValue(symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting pickup constant: " + symbol);
                constants[symbol] = value;
            }
        }

        {
            hashes.AddRange(oakRating.AdditionalSourceHashes);
            hashes.AddRange(oakPokedex.AdditionalSourceHashes);
            hashes.Add("oak-rating-application:fr-oak-rating-campaign-v1");
            var paths = sources.ToDictionary(x => x.Path, StringComparer.Ordinal);
            foreach (var source in oakRating.Scripts.Concat(oakRating.CallerScripts))
            {
                if (paths.TryGetValue(source.Path, out var prior))
                {
                    if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                        throw new InvalidDataException("Conflicting Oak rating source: " + source.Path);
                    continue;
                }

                paths.Add(source.Path, source);
                sources.Add(source);
                textFiles.Add(source.Text);
            }

            foreach (var text in oakRating.TextSources.Values)
                if (!textFiles.Contains(text, StringComparer.Ordinal))
                    textFiles.Add(text);
            foreach (var(symbol, value)in oakRating.Constants)
            {
                if (constants.TryGetValue(symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting Oak rating constant: " + symbol);
                constants[symbol] = value;
            }
        }

        {
            hashes.AddRange(route22.AdditionalSourceHashes);
            hashes.AddRange(route22Pokemon.AdditionalSourceHashes);
            hashes.Add("route22-application:fr-route22-campaign-v1");
            var paths = sources.ToDictionary(x => x.Path, StringComparer.Ordinal);
            foreach (var source in route22.Scripts)
            {
                if (paths.TryGetValue(source.Path, out var prior))
                {
                    if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                        throw new InvalidDataException("Conflicting Route22 source: " + source.Path);
                    continue;
                }

                paths.Add(source.Path, source);
                sources.Add(source);
                textFiles.Add(source.Text);
            }

            foreach (var text in route22.TextSources.Values)
                if (!textFiles.Contains(text, StringComparer.Ordinal))
                    textFiles.Add(text);
            foreach (var(symbol, value)in route22.Constants)
            {
                if (constants.TryGetValue(symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting Route22 constant: " + symbol);
                constants[symbol] = value;
            }
        }

        {
            hashes.AddRange(route3.AdditionalSourceHashes);
            hashes.AddRange(route3Pokemon.AdditionalSourceHashes);
            hashes.Add("route3-application:fr-route3-campaign-v1");
            var paths = sources.ToDictionary(x => x.Path, StringComparer.Ordinal);
            foreach (var source in route3.Scripts)
            {
                if (paths.TryGetValue(source.Path, out var prior))
                {
                    if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                        throw new InvalidDataException("Conflicting Route3 source: " + source.Path);
                    continue;
                }

                paths.Add(source.Path, source);
                sources.Add(source);
                textFiles.Add(source.Text);
            }

            foreach (var text in route3.TextSources.Values)
                if (!textFiles.Contains(text, StringComparer.Ordinal))
                    textFiles.Add(text);
            foreach (var(symbol, value)in route3.Constants)
            {
                if (constants.TryGetValue(symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting Route3 constant: " + symbol);
                constants[symbol] = value;
            }
        }

        {
            hashes.AddRange(mtMoon.AdditionalSourceHashes);
            hashes.AddRange(mtMoonPokemon.AdditionalSourceHashes);
            hashes.Add("mtmoon-application:fr-mtmoon-campaign-v1");
            var paths = sources.ToDictionary(x => x.Path, StringComparer.Ordinal);
            foreach (var source in mtMoon.Scripts)
            {
                if (paths.TryGetValue(source.Path, out var prior))
                {
                    if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                        throw new InvalidDataException("Conflicting MtMoon source: " + source.Path);
                    continue;
                }

                paths.Add(source.Path, source);
                sources.Add(source);
                textFiles.Add(source.Text);
            }

            foreach (var text in mtMoon.TextSources.Values)
                if (!textFiles.Contains(text, StringComparer.Ordinal))
                    textFiles.Add(text);
            foreach (var(symbol, value)in mtMoon.Constants)
            {
                if (constants.TryGetValue(symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting MtMoon constant: " + symbol);
                constants[symbol] = value;
            }
        }

        {
            hashes.AddRange(cerulean.AdditionalSourceHashes);
            hashes.Add("cerulean-application:fr-cerulean-campaign-v1");
            var paths = sources.ToDictionary(x => x.Path, StringComparer.Ordinal);
            foreach (var source in cerulean.Scripts)
            {
                if (paths.TryGetValue(source.Path, out var prior))
                {
                    if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                        throw new InvalidDataException("Conflicting Cerulean source: " + source.Path);
                    continue;
                }

                paths.Add(source.Path, source);
                sources.Add(source);
                textFiles.Add(source.Text);
            }

            foreach (var text in cerulean.TextSources.Values)
                if (!textFiles.Contains(text, StringComparer.Ordinal))
                    textFiles.Add(text);
            foreach (var(symbol, value)in cerulean.Constants)
            {
                if (constants.TryGetValue(symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting Cerulean constant: " + symbol);
                constants[symbol] = value;
            }
        }

        {
            hashes.AddRange(bill.AdditionalSourceHashes);
            hashes.Add("bill-application:fr-bill-campaign-v1");
            var paths = sources.ToDictionary(x => x.Path, StringComparer.Ordinal);
            foreach (var source in bill.Scripts)
            {
                if (paths.TryGetValue(source.Path, out var prior))
                {
                    if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                        throw new InvalidDataException("Conflicting Bill source: " + source.Path);
                    continue;
                }

                paths.Add(source.Path, source);
                sources.Add(source);
                textFiles.Add(source.Text);
            }

            foreach (var text in bill.TextSources.Values)
                if (!textFiles.Contains(text, StringComparer.Ordinal))
                    textFiles.Add(text);
            foreach (var(symbol, value)in bill.Constants)
            {
                if (constants.TryGetValue(symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting Bill constant: " + symbol);
                constants[symbol] = value;
            }
        }

        {
            hashes.AddRange(vermilion.AdditionalSourceHashes);
            hashes.Add("vermilion-application:fr-vermilion-campaign-v1");
            var paths = sources.ToDictionary(x => x.Path, StringComparer.Ordinal);
            foreach (var source in vermilion.Scripts)
            {
                if (paths.TryGetValue(source.Path, out var prior))
                {
                    if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                        throw new InvalidDataException("Conflicting Vermilion source: " + source.Path);
                    continue;
                }

                paths.Add(source.Path, source);
                sources.Add(source);
                textFiles.Add(source.Text);
            }

            foreach (var text in vermilion.TextSources.Values)
                if (!textFiles.Contains(text, StringComparer.Ordinal))
                    textFiles.Add(text);
            foreach (var(symbol, value)in vermilion.Constants)
            {
                if (constants.TryGetValue(symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting Vermilion constant: " + symbol);
                constants[symbol] = value;
            }
        }

        {
            hashes.AddRange(ssAnne.AdditionalSourceHashes);
            hashes.Add("ssanne-application:fr-ssanne-campaign-v1");
            var paths = sources.ToDictionary(x => x.Path, StringComparer.Ordinal);
            foreach (var source in ssAnne.Scripts)
            {
                if (paths.TryGetValue(source.Path, out var prior))
                {
                    if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                        throw new InvalidDataException("Conflicting S.S. Anne source: " + source.Path);
                    continue;
                }

                paths.Add(source.Path, source);
                sources.Add(source);
                textFiles.Add(source.Text);
            }

            foreach (var text in ssAnne.TextSources.Values)
                if (!textFiles.Contains(text, StringComparer.Ordinal))
                    textFiles.Add(text);
            foreach (var(symbol, value)in ssAnne.Constants)
            {
                if (constants.TryGetValue(symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting S.S. Anne constant: " + symbol);
                constants[symbol] = value;
            }
        }

        {
            hashes.AddRange(vermilionGym.AdditionalSourceHashes);
            hashes.AddRange(vermilionGymAssets.AdditionalSourceHashes);
            hashes.Add("vermilion-gym-application:fr-vermilion-gym-campaign-v1");
            var paths = sources.ToDictionary(x => x.Path, StringComparer.Ordinal);
            foreach (var source in vermilionGym.Scripts)
            {
                if (paths.TryGetValue(source.Path, out var prior))
                {
                    if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                        throw new InvalidDataException("Conflicting Vermilion Gym source: " + source.Path);
                    continue;
                }

                paths.Add(source.Path, source);
                sources.Add(source);
                textFiles.Add(source.Text);
            }

            foreach (var text in vermilionGym.TextSources.Values)
                if (!textFiles.Contains(text, StringComparer.Ordinal))
                    textFiles.Add(text);
            foreach (var(symbol, value)in vermilionGym.Constants)
            {
                if (constants.TryGetValue(symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting Vermilion Gym constant: " + symbol);
                constants[symbol] = value;
            }
        }

        {
            hashes.AddRange(cityServices.AdditionalSourceHashes);
            hashes.AddRange(oldRod.AdditionalSourceHashes);
            hashes.Add("city-services-application:fr-city-services-campaign-v1");
            var paths = sources.ToDictionary(x => x.Path, StringComparer.Ordinal);
            foreach (var source in cityServices.Scripts)
            {
                if (paths.TryGetValue(source.Path, out var prior))
                {
                    if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                        throw new InvalidDataException("Conflicting City services source: " + source.Path);
                    continue;
                }

                paths.Add(source.Path, source);
                sources.Add(source);
                textFiles.Add(source.Text);
            }

            foreach (var text in cityServices.TextSources.Values)
                if (!textFiles.Contains(text, StringComparer.Ordinal))
                    textFiles.Add(text);
            foreach (var(symbol, value)in cityServices.Constants)
            {
                if (constants.TryGetValue(symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting City services constant: " + symbol);
                constants[symbol] = value;
            }
        }

        {
            hashes.AddRange(route11.AdditionalSourceHashes);
            hashes.AddRange(route11Encounters.AdditionalSourceHashes);
            hashes.Add("route11-application:fr-route11-campaign-v1");
            var paths = sources.ToDictionary(x => x.Path, StringComparer.Ordinal);
            foreach (var source in route11.Scripts)
            {
                if (paths.TryGetValue(source.Path, out var prior))
                {
                    if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                        throw new InvalidDataException("Conflicting Route11 source: " + source.Path);
                    continue;
                }

                paths.Add(source.Path, source);
                sources.Add(source);
                textFiles.Add(source.Text);
            }

            foreach (var text in route11.TextSources.Values)
                if (!textFiles.Contains(text, StringComparer.Ordinal))
                    textFiles.Add(text);
            foreach (var(symbol, value)in route11.Constants)
            {
                if (constants.TryGetValue(symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting Route11 constant: " + symbol);
                constants[symbol] = value;
            }
        }

        {
            hashes.AddRange(rockTunnel.AdditionalSourceHashes);
            hashes.AddRange(rockTunnelEncounters.AdditionalSourceHashes);
            hashes.Add("rocktunnel-application:fr-rocktunnel-campaign-v1");
            var paths = sources.ToDictionary(x => x.Path, StringComparer.Ordinal);
            foreach (var source in rockTunnel.Scripts)
            {
                if (paths.TryGetValue(source.Path, out var prior))
                {
                    if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                        throw new InvalidDataException("Conflicting RockTunnel source: " + source.Path);
                    continue;
                }

                paths.Add(source.Path, source);
                sources.Add(source);
                textFiles.Add(source.Text);
            }

            foreach (var text in rockTunnel.TextSources.Values)
                if (!textFiles.Contains(text, StringComparer.Ordinal))
                    textFiles.Add(text);
            foreach (var(symbol, value)in rockTunnel.Constants)
            {
                if (constants.TryGetValue(symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting RockTunnel constant: " + symbol);
                constants[symbol] = value;
            }
        }

        {
            hashes.AddRange(celadonApproach.AdditionalSourceHashes);
            hashes.AddRange(celadonApproachEncounters.AdditionalSourceHashes);
            hashes.Add("celadon-approach-application:fr-celadon-approach-campaign-v1");
            var paths = sources.ToDictionary(x => x.Path, StringComparer.Ordinal);
            foreach (var source in celadonApproach.Scripts)
            {
                if (paths.TryGetValue(source.Path, out var prior))
                {
                    if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                        throw new InvalidDataException("Conflicting CeladonApproach source: " + source.Path);
                    continue;
                }

                paths.Add(source.Path, source);
                sources.Add(source);
                textFiles.Add(source.Text);
            }

            foreach (var text in celadonApproach.TextSources.Values)
                if (!textFiles.Contains(text, StringComparer.Ordinal))
                    textFiles.Add(text);
            foreach (var(symbol, value)in celadonApproach.Constants)
            {
                if (constants.TryGetValue(symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting CeladonApproach constant: " + symbol);
                constants[symbol] = value;
            }
        }

        {
            hashes.AddRange(celadonGym.AdditionalSourceHashes);
            hashes.Add("celadon-gym-application:fr-celadon-gym-campaign-v1");
            var paths = sources.ToDictionary(x => x.Path, StringComparer.Ordinal);
            foreach (var source in celadonGym.Scripts)
            {
                if (paths.TryGetValue(source.Path, out var prior))
                {
                    if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                        throw new InvalidDataException("Conflicting CeladonGym source: " + source.Path);
                    continue;
                }

                paths.Add(source.Path, source);
                sources.Add(source);
                textFiles.Add(source.Text);
            }

            foreach (var text in celadonGym.TextSources.Values)
                if (!textFiles.Contains(text, StringComparer.Ordinal))
                    textFiles.Add(text);
            foreach (var(symbol, value)in celadonGym.Constants)
            {
                if (constants.TryGetValue(symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting CeladonGym constant: " + symbol);
                constants[symbol] = value;
            }
        }

        {
            hashes.AddRange(rocketHideout.AdditionalSourceHashes);
            hashes.Add("rocket-hideout-application:fr-rocket-hideout-campaign-v1");
            var paths = sources.ToDictionary(x => x.Path, StringComparer.Ordinal);
            foreach (var source in rocketHideout.Scripts)
            {
                if (paths.TryGetValue(source.Path, out var prior))
                {
                    if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                        throw new InvalidDataException("Conflicting Rocket Hideout source: " + source.Path);
                    continue;
                }

                paths.Add(source.Path, source);
                sources.Add(source);
                textFiles.Add(source.Text);
            }

            foreach (var text in rocketHideout.TextSources.Values)
                if (!textFiles.Contains(text, StringComparer.Ordinal))
                    textFiles.Add(text);
            foreach (var(symbol, value)in rocketHideout.Constants)
            {
                if (constants.TryGetValue(symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting Rocket Hideout constant: " + symbol);
                constants[symbol] = value;
            }

            // Metatile labels use mixed case, unlike the source region's uppercase
            // numeric constant inventory. Resolve only the authenticated patch atlas.
            foreach (var tile in rocketHideout.SourceContract.GetProperty("patchAtlas").EnumerateArray())
            {
                string symbol = tile.GetProperty("symbol").GetString()!;
                int value = tile.GetProperty("metatileId").GetInt32();
                if (constants.TryGetValue(symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting Rocket Hideout metatile constant: " + symbol);
                constants[symbol] = value;
            }
        }

        {
            hashes.AddRange(pokemonTower.AdditionalSourceHashes);
            hashes.Add("pokemon-tower-trainers:" + PretPokemonTowerTrainers.LockSha256 + ":" + PretPokemonTowerTrainers.GeneratedSha256);
            var paths = sources.ToDictionary(x => x.Path, StringComparer.Ordinal);
            foreach (var source in pokemonTower.Scripts)
            {
                if (paths.TryGetValue(source.Path, out var prior))
                {
                    if (prior.SourceRef.Sha256 != source.SourceRef.Sha256 || prior.Text != source.Text)
                        throw new InvalidDataException("Conflicting Tower source: " + source.Path);
                    continue;
                }

                paths.Add(source.Path, source);
                sources.Add(source);
                textFiles.Add(source.Text);
            }

            foreach (var text in pokemonTower.TextSources.Values)
                if (!textFiles.Contains(text, StringComparer.Ordinal))
                    textFiles.Add(text);
            foreach (var(symbol, value)in pokemonTower.Constants)
            {
                if (constants.TryGetValue(symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting Tower constant: " + symbol);
                constants[symbol] = value;
            }
        }

        AppendSnorlaxSources(snorlax, sources, textFiles, constants, hashes);
        AppendRegionalFlavorSources(projectRoot, sources, textFiles, hashes);
        AppendRegionalServiceSources(projectRoot, regionalServices, regionalGift, maps, sources, textFiles, constants, hashes);
        AppendFuchsiaRouteSources(fuchsiaRoute, sources, textFiles, constants, hashes);
        AppendFuchsiaGymSources(fuchsiaGym, fuchsiaGymItems, fuchsiaGymMoveSource, maps, sources, textFiles, constants, hashes);
        AppendSafariSources(safari, safariItems, sources, textFiles, constants, hashes);
        AppendSurfSources(surfScripts, sources, textFiles, constants, hashes);
        AppendSilphSources(silph, silphItems, sources, textFiles, constants, hashes);
        {
            AppendSaffronGymSources(saffronGym, saffronGymItems, sources, textFiles, constants, hashes);
            hashes.AddRange(saffronGymArt.AdditionalSourceHashes);
        }

        AppendCinnabarSources(cinnabar, cinnabarItems, sources, textFiles, constants, hashes);
        hashes.AddRange(cinnabarArt.AdditionalSourceHashes);
        hashes.AddRange(cinnabarPresentation.AdditionalSourceHashes);
        AppendFirstSeviiSources(firstSevii, sources, textFiles, constants, hashes);
        AppendViridianGymSources(viridianGym, sources, textFiles, constants, hashes);
        {
            AppendVictoryRoadSources(victoryRoad, sources, textFiles, constants, hashes);
            hashes.AddRange(PretVictoryRoadRules.ValidateSourceEvidence(projectRoot));
        }

        {
            AppendPokemonLeagueSources(pokemonLeague, sources, textFiles, constants, hashes);
            PretPokemonLeagueRules.ValidateSourceEvidence(projectRoot);
        }

        AppendNationalDexSources(nationalDexSource, sources, textFiles, constants, hashes);
        {
            AppendSeviiRubySources(seviiRuby, sources, textFiles, constants, hashes);
            PretSeviiRubyRules.ValidateSourceEvidence(projectRoot);
        }

        {
            AppendFourIslandSources(fourIsland, sources, textFiles, constants, hashes);
            PretFourIslandRules.ValidateSourceEvidence(projectRoot);
        }

        {
            AppendSixIslandSources(sixIsland, sources, textFiles, constants, hashes);
            PretSixIslandRules.ValidateSourceEvidence(projectRoot, projectRoot);
        }

        {
            AppendFiveIslandSources(fiveIsland, sources, textFiles, constants, hashes);
            PretFiveIslandRules.ValidateSourceEvidence(projectRoot, projectRoot);
        }

        AppendCeruleanCaveSources(ceruleanCave, sources, textFiles, constants);
        AppendSevenIslandSources(sevenIsland, sources, textFiles, constants, hashes);
        AppendEventIslandSources(eventIsland, sources, textFiles, constants, hashes);
        AppendPowerPlantSources(powerPlant, sources, textFiles, constants, hashes);
        AppendTrainerTowerSources(trainerTower, sources, textFiles, constants, hashes);
        AppendFightingDojoSources(fightingDojo, sources, textFiles, constants, hashes);
        AppendFossilLabSources(fossilLab, sources, textFiles, constants, hashes);
        AppendNpcTradeSources(npcTrades, sources, textFiles, constants, hashes);
        AppendAuthenticatedRegionalSources(npcTradeHaden.Scripts, npcTradeHaden.TextSources.Values, npcTradeHaden.Constants,
            npcTradeHaden.AdditionalSourceHashes, sources, textFiles, constants, hashes);
        AppendAuthenticatedRegionalSources(goodRodService.Scripts, goodRodService.TextSources.Values, goodRodService.Constants,
            goodRodService.AdditionalSourceHashes, sources, textFiles, constants, hashes);
        AppendAuthenticatedRegionalSources(seafoam.Scripts, seafoam.TextSources.Values, seafoam.Constants,
            seafoam.AdditionalSourceHashes, sources, textFiles, constants, hashes);
        AppendAuthenticatedRegionalSources(superRodService.Scripts, superRodService.TextSources.Values, superRodService.Constants,
            superRodService.AdditionalSourceHashes, sources, textFiles, constants, hashes);

        AppendAuthenticatedRegionalSources(moveReminderHouse.Scripts, moveReminderHouse.TextSources.Values, moveReminderHouse.Constants,
            moveReminderHouse.AdditionalSourceHashes, sources, textFiles, constants, hashes);

        AppendAuthenticatedRegionalSources(moveDeleterHouse.Scripts, moveDeleterHouse.TextSources.Values, moveDeleterHouse.Constants,
            moveDeleterHouse.AdditionalSourceHashes, sources, textFiles, constants, hashes);

        AppendAuthenticatedRegionalSources(originalTutorRooms.Scripts, originalTutorRooms.TextSources.Values, originalTutorRooms.Constants,
            originalTutorRooms.AdditionalSourceHashes, sources, textFiles, constants, hashes);

        AppendAuthenticatedRegionalSources(copycatHouse.Scripts, copycatHouse.TextSources.Values, copycatHouse.Constants,
            copycatHouse.AdditionalSourceHashes, sources, textFiles, constants, hashes);

        AppendAuthenticatedRegionalSources(capeBrinkTutor.Scripts, capeBrinkTutor.TextSources.Values, capeBrinkTutor.Constants,
            capeBrinkTutor.AdditionalSourceHashes, sources, textFiles, constants, hashes);

        AppendAuthenticatedRegionalSources(fourIslandDayCare.Scripts, fourIslandDayCare.TextSources.Values, fourIslandDayCare.Constants,
            fourIslandDayCare.AdditionalSourceHashes, sources, textFiles, constants, hashes);
        AppendAuthenticatedRegionalSources(route5DayCare.Scripts, route5DayCare.TextSources.Values, route5DayCare.Constants,
            route5DayCare.AdditionalSourceHashes, sources, textFiles, constants, hashes);
        AppendAuthenticatedRegionalSources(route16FlyHouse.Scripts, route16FlyHouse.TextSources.Values, route16FlyHouse.Constants,
            route16FlyHouse.AdditionalSourceHashes, sources, textFiles, constants, hashes);
        originalTmAcquisition.AppendSources(sources, textFiles, constants, hashes);
        hashes.AddRange(PretFieldFly.AdditionalSourceHashes);
        hashes.Add("field-rock-smash:" + PretFieldRockSmash.DocumentSha256);
        hashes.AddRange(flyMapPresentation.AdditionalSourceHashes);

        hashes.AddRange(firstSeviiPresentation.AdditionalSourceHashes);
        MergeRegionalServiceConstants(constants, gameCornerCoins.Constants);
        hashes.AddRange(PretGameCornerCoins.AdditionalSourceHashes);
        hashes.AddRange(PretGameCornerHiddenCoins.AdditionalSourceHashes);
        {
            MergeRegionalServiceConstants(constants, departmentStore.Constants);
            MergeRegionalServiceConstants(constants, new Dictionary<string, int> { { "PARTY_SIZE", 6 }, { "SPECIES_EGG", 412 } });
            hashes.AddRange(departmentStore.AdditionalSourceHashes);
            hashes.AddRange(nameRater.AdditionalSourceHashes);
            hashes.Add("regional-interiors-application:fr-regional-interiors-v1");
        }

        // Keep the authenticated owned/opponent predecessor chain and its object identities.
        CampaignPokemonContent pokemonContent;
        {
            var extra = new Dictionary<string, string>(StringComparer.Ordinal);
            var starterLockBytes = File.ReadAllBytes(Path.Combine(projectRoot, "scripts/fetch-starter-events.lock.json"));
            if (Convert.ToHexStringLower(SHA256.HashData(starterLockBytes)) != "9889ffe7ebc66aba632be43c3549cbdbd734f83dcb6deb431e08e7a8d65d5c4a")
                throw new InvalidDataException("Starter event lock hash mismatch.");
            using var starterLock = JsonDocument.Parse(starterLockBytes);
            if (starterLock.RootElement.GetProperty("commit").GetString() != PretWorldMap.SourceCommit)
                throw new InvalidDataException("Starter event source commit mismatch.");
            foreach (var entry in starterLock.RootElement.GetProperty("files").EnumerateArray())
            {
                var path = entry.GetProperty("path").GetString()!;
                if (Path.IsPathRooted(path) || path.Split('/').Contains(".."))
                    throw new InvalidDataException("Starter source path invalid.");
                var bytes = File.ReadAllBytes(Path.Combine(projectRoot, "content/raw/starter-events", path));
                var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                var header = Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
                if (bytes.Length != entry.GetProperty("byteLength").GetInt32() || hash != entry.GetProperty("sha256").GetString() || Convert.ToHexStringLower(SHA1.HashData([..header, ..bytes])) != entry.GetProperty("gitBlobSha1").GetString())
                    throw new InvalidDataException("Starter event source mismatch: " + path);
                hashes.Add(path + ":" + hash);
                extra.Add(path, Encoding.UTF8.GetString(bytes));
            }

            foreach (Match m in Regex.Matches(extra["include/constants/species.h"], @"^#define\s+(SPECIES_[A-Z0-9_]+)\s+([0-9]+)\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant))
                constants[m.Groups[1].Value] = int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            foreach (var constant in new[]
            {
                "YES",
                "NO"
            }

            )
            {
                var m = Regex.Match(extra["asm/macros/event.inc"], @"^\s*" + constant + @"\s*=\s*([0-9]+)\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
                if (!m.Success)
                    throw new InvalidDataException("Missing source yes/no constant.");
                constants[constant] = int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            }

            var pokemonJson = File.ReadAllText(Path.Combine(projectRoot, "content/generated/pokemon/starter-catalog.json"));
            var pokemon = PretPokemonCatalog.Load(pokemonJson);
            hashes.Add("pokemon:" + PretPokemonCatalog.GeneratedSha256);
            var names = ImmutableSortedDictionary.CreateBuilder<int, string>();
            foreach (Match m in Regex.Matches(extra["src/data/text/species_names.h"], @"\[(SPECIES_[A-Z0-9_]+)\]\s*=\s*_\(""([^""]+)""\)", RegexOptions.CultureInvariant))
                if (constants.TryGetValue(m.Groups[1].Value, out var speciesId) && pokemon.Species.Values.Any(x => x.SourceSpeciesId == speciesId))
                    names.Add(speciesId, m.Groups[2].Value);
            if (names.Count != pokemon.Species.Count)
                throw new InvalidDataException("Missing source species names.");
            var keyboard = Regex.Match(extra["src/naming_screen.c"], @"static const u8 sKeyboardChars[^=]+=(.*?);", RegexOptions.Singleline | RegexOptions.CultureInvariant);
            if (!keyboard.Success)
                throw new InvalidDataException("Nickname keyboard missing.");
            var chars = string.Concat(Regex.Matches(keyboard.Groups[1].Value, @"__\(""([^""]*)""\)", RegexOptions.CultureInvariant).Select(m => m.Groups[1].Value)).Distinct().Order().ToArray();
            if (chars.Length < 60)
                throw new InvalidDataException("Nickname alphabet incomplete.");
            pokemonContent = new(pokemon, names.ToImmutable(), new string (chars));
        }

        PretRoute1PokemonContent route1Pokemon;
        {
            route1Pokemon = PretRoute1PokemonCatalog.LoadContent(File.ReadAllText(Path.Combine(projectRoot, "content/generated/route1-pokemon/catalog.json"), Encoding.UTF8));
            hashes.Add("route1-pokemon:" + PretRoute1PokemonCatalog.GeneratedSha256);
            pokemonContent = pokemonContent with
            {
                Catalog = route1Pokemon.Catalog,
                SpeciesNames = route1Pokemon.SpeciesNames
            };
        }

        var oldMan = PretOldManTutorial.LoadContent(File.ReadAllText(Path.Combine(projectRoot, "content/generated/route1-pokemon/catalog.json"), Encoding.UTF8), File.ReadAllText(Path.Combine(projectRoot, "content/generated/old-man-tutorial/catalog.json"), Encoding.UTF8));
        {
            pokemonContent = pokemonContent with
            {
                Catalog = oldMan.Catalog,
                SpeciesNames = oldMan.SpeciesNames
            };
            hashes.Add("old-man-tutorial:" + PretOldManTutorial.GeneratedSha256);
        }

        var forestPokemon = PretForestPokemon.Load(projectRoot);
        {
            pokemonContent = pokemonContent with
            {
                Catalog = forestPokemon.Catalog,
                SpeciesNames = forestPokemon.SpeciesNames
            };
            hashes.AddRange(forestPokemon.AdditionalSourceHashes);
            foreach (var(symbol, value)in forestPokemon.Constants)
                constants[symbol] = value;
        }

        var pewterPokemon = PretPewterPokemon.Load(projectRoot);
        {
            pokemonContent = pokemonContent with
            {
                Catalog = pewterPokemon.Catalog,
                SpeciesNames = pewterPokemon.SpeciesNames
            };
            hashes.AddRange(pewterPokemon.AdditionalSourceHashes);
            foreach (var(symbol, value)in pewterPokemon.Constants)
                constants[symbol] = value;
        }

        {
            pokemonContent = pokemonContent with
            {
                Catalog = route22Pokemon.Catalog,
                SpeciesNames = route22Pokemon.SpeciesNames
            };
            foreach (var(symbol, value)in route22Pokemon.Constants)
                constants[symbol] = value;
        }

        {
            pokemonContent = pokemonContent with
            {
                Catalog = route3Pokemon.Catalog,
                SpeciesNames = route3Pokemon.SpeciesNames
            };
            foreach (var(symbol, value)in route3Pokemon.Constants)
                constants[symbol] = value;
        }

        {
            pokemonContent = pokemonContent with
            {
                Catalog = mtMoonPokemon.Catalog,
                SpeciesNames = mtMoonPokemon.SpeciesNames
            };
            foreach (var(symbol, value)in mtMoonPokemon.Constants)
                constants[symbol] = value;
        }

        {
            pokemonContent = pokemonContent with
            {
                Catalog = ceruleanPokemon.Catalog,
                SpeciesNames = ceruleanPokemon.SpeciesNames
            };
            hashes.AddRange(ceruleanPokemon.AdditionalSourceHashes);
            hashes.Add("cerulean-trainers:" + PretCeruleanTrainers.LockSha256);
            foreach (var(symbol, value)in ceruleanPokemon.Constants)
                constants[symbol] = value;
        }

        {
            pokemonContent = pokemonContent with
            {
                Catalog = billPokemon.Catalog,
                SpeciesNames = billPokemon.SpeciesNames
            };
            hashes.AddRange(billPokemon.AdditionalSourceHashes);
            hashes.Add("bill-trainers:" + PretBillTrainers.LockSha256 + ":" + PretBillTrainers.GeneratedSha256);
            foreach (var(symbol, value)in billPokemon.Constants)
                constants[symbol] = value;
        }

        {
            pokemonContent = pokemonContent with
            {
                Catalog = vermilionPokemon.Catalog,
                SpeciesNames = vermilionPokemon.SpeciesNames
            };
            hashes.AddRange(vermilionPokemon.AdditionalSourceHashes);
            hashes.Add("vermilion-trainers:" + PretVermilionTrainers.LockSha256 + ":" + PretVermilionTrainers.GeneratedSha256);
            foreach (var(symbol, value)in vermilionPokemon.Constants)
                constants[symbol] = value;
        }

        {
            pokemonContent = pokemonContent with
            {
                Catalog = ssAnnePokemon.Catalog,
                SpeciesNames = ssAnnePokemon.SpeciesNames
            };
            hashes.AddRange(ssAnnePokemon.AdditionalSourceHashes);
            hashes.Add("ssanne-trainers:" + PretSsAnneTrainers.LockSha256 + ":" + PretSsAnneTrainers.GeneratedSha256);
            foreach (var(symbol, value)in ssAnnePokemon.Constants)
                constants[symbol] = value;
        }

        {
            pokemonContent = pokemonContent with
            {
                Catalog = route11Pokemon.Catalog,
                SpeciesNames = route11Pokemon.SpeciesNames,
                AllowFreezeStatus = true
            };
            hashes.AddRange(route11Pokemon.AdditionalSourceHashes);
            hashes.Add("route11-trainers:" + PretRoute11Trainers.LockSha256 + ":" + PretRoute11Trainers.GeneratedSha256);
            foreach (var(symbol, value)in route11Pokemon.Constants)
                constants[symbol] = value;
        }

        {
            pokemonContent = pokemonContent with
            {
                Catalog = PretVermilionGymTrainers.ExtendPokemon(projectRoot, route11Pokemon.Catalog, route11Rules: true)
            };
            hashes.Add("vermilion-gym-trainers:" + PretVermilionGymTrainers.LockSha256 + ":" + PretVermilionGymTrainers.GeneratedSha256);
        }

        {
            pokemonContent = pokemonContent with
            {
                Catalog = RockTunnelCampaignPokemon(rockTunnelPokemon.Catalog, pokemonContent.Catalog),
                SpeciesNames = rockTunnelPokemon.SpeciesNames
            };
            hashes.AddRange(rockTunnelPokemon.AdditionalSourceHashes);
            hashes.Add("rocktunnel-trainers:" + PretRockTunnelTrainers.LockSha256 + ":" + PretRockTunnelTrainers.GeneratedSha256);
            foreach (var(symbol, value)in rockTunnelPokemon.Constants)
                constants[symbol] = value;
        }

        {
            pokemonContent = pokemonContent with
            {
                Catalog = CeladonApproachCampaignPokemon(celadonApproachPokemon.Catalog, pokemonContent.Catalog),
                SpeciesNames = celadonApproachPokemon.SpeciesNames
            };
            hashes.AddRange(celadonApproachPokemon.AdditionalSourceHashes);
            hashes.Add("celadon-approach-trainers:" + PretCeladonApproachTrainers.LockSha256 + ":" + PretCeladonApproachTrainers.GeneratedSha256);
            foreach (var(symbol, value)in celadonApproachPokemon.Constants)
                constants[symbol] = value;
        }

        {
            pokemonContent = pokemonContent with
            {
                Catalog = CeladonGymCampaignPokemon(celadonGymPokemon.Catalog, pokemonContent.Catalog),
                SpeciesNames = celadonGymPokemon.SpeciesNames
            };
            hashes.AddRange(celadonGymPokemon.AdditionalSourceHashes);
            hashes.Add("celadon-gym-trainers:" + PretCeladonGymTrainers.LockSha256 + ":" + PretCeladonGymTrainers.GeneratedSha256);
            foreach (var(symbol, value)in celadonGymPokemon.Constants)
                constants[symbol] = value;
        }

        {
            pokemonContent = pokemonContent with
            {
                Catalog = RocketHideoutCampaignPokemon(rocketHideoutPokemon.Catalog, pokemonContent.Catalog),
                SpeciesNames = rocketHideoutPokemon.SpeciesNames
            };
            hashes.AddRange(rocketHideoutPokemon.AdditionalSourceHashes);
            hashes.Add("rocket-hideout-trainers:" + PretRocketHideoutTrainers.LockSha256 + ":" + PretRocketHideoutTrainers.GeneratedSha256);
            foreach (var(symbol, value)in rocketHideoutPokemon.Constants)
                constants[symbol] = value;
        }

        {
            pokemonTowerPokemon = PretPokemonTowerPokemon.Load(pokemonContent.Catalog, projectRoot);
            pokemonContent = pokemonContent with
            {
                Catalog = pokemonTowerPokemon.Catalog,
                SpeciesNames = pokemonContent.SpeciesNames.AddRange(pokemonTowerPokemon.AdditionalSpeciesNames)
            };
            hashes.AddRange(pokemonTowerPokemon.AdditionalSourceHashes);
            foreach (var(symbol, value)in pokemonTowerPokemon.Constants)
                constants[symbol] = value;
        }

        {
            snorlaxPokemon = PretSnorlaxPokemon.Load(pokemonContent.Catalog, projectRoot);
            pokemonContent = pokemonContent with
            {
                Catalog = snorlaxPokemon.Catalog,
                SpeciesNames = pokemonContent.SpeciesNames.AddRange(snorlaxPokemon.AdditionalSpeciesNames)
            };
            hashes.AddRange(snorlaxPokemon.AdditionalSourceHashes);
            hashes.AddRange(snorlaxArt.AdditionalSourceHashes);
            hashes.Add("snorlax-application:fr-snorlax-campaign-v1");
            foreach (var(symbol, value)in snorlaxPokemon.Constants)
            {
                if (constants.TryGetValue(symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting Snorlax Pokemon constant: " + symbol);
                constants[symbol] = value;
            }
        }

        var snorlaxCompositionPokemon = pokemonContent;
        {
            eeveePokemon = PretEeveePokemon.Load(pokemonContent.Catalog, projectRoot);
            pokemonContent = pokemonContent with
            {
                Catalog = eeveePokemon.Catalog,
                SpeciesNames = pokemonContent.SpeciesNames.AddRange(eeveePokemon.AdditionalSpeciesNames)
            };
            hashes.AddRange(eeveePokemon.AdditionalSourceHashes);
            hashes.AddRange(eeveeArt.AdditionalSourceHashes);
            MergeRegionalServiceConstants(constants, eeveePokemon.Constants);
        }

        var regionalServiceCompositionPokemon = pokemonContent;
        {
            fuchsiaPokemon = PretFuchsiaRoutePokemon.Load(pokemonContent.Catalog, projectRoot);
            pokemonContent = pokemonContent with
            {
                Catalog = fuchsiaPokemon.Catalog,
                SpeciesNames = pokemonContent.SpeciesNames.AddRange(fuchsiaPokemon.AdditionalSpeciesNames)
            };
            hashes.AddRange(fuchsiaPokemon.AdditionalSourceHashes);
            hashes.AddRange(fuchsiaArt.AdditionalSourceHashes);
            hashes.Add("fuchsia-route-application:fr-fuchsia-route-campaign-v1");
            MergeRegionalServiceConstants(constants, fuchsiaPokemon.Constants);
        }

        var fuchsiaRouteCompositionPokemon = pokemonContent;
        pokemonContent = pokemonContent with
        {
            Catalog = FuchsiaGymPokemon(pokemonContent.Catalog, fuchsiaGymTrainers, fuchsiaGymMoveSource),
            AllowToxicStatus = true
        };
        var fuchsiaGymCompositionPokemon = pokemonContent;
        {
            safariPokemon = PretSafariPokemon.Load(pokemonContent.Catalog, projectRoot);
            safariOwned = SafariOwnedPokemonCatalog.Authenticate(pokemonContent.Catalog, safariPokemon.Catalog, doubleMechanics);
            pokemonContent = pokemonContent with
            {
                Catalog = safariOwned.Catalog,
                SpeciesNames = pokemonContent.SpeciesNames.AddRange(safariPokemon.AdditionalSpeciesNames)
            };
            hashes.AddRange(safariPokemon.AdditionalSourceHashes);
            hashes.AddRange(safariArt.AdditionalSourceHashes);
            MergeRegionalServiceConstants(constants, safariPokemon.Constants);
        }

        {
            surfPokemon = PretSurfPokemon.Load(safariOwned.Catalog, projectRoot, projectRoot);
            surfOwned = SurfOwnedPokemonCatalog.Authenticate(safariOwned, surfPokemon.Catalog);
            surfArt = PretSurfArt.Load(projectRoot, projectRoot);
            surfEncounters = PretSurfEncounters.Load(projectRoot);
            pokemonContent = pokemonContent with
            {
                Catalog = surfOwned.Catalog,
                SpeciesNames = pokemonContent.SpeciesNames.AddRange(surfPokemon.AdditionalSpeciesNames)
            };
            hashes.AddRange(surfPokemon.AdditionalSourceHashes);
            hashes.AddRange(surfArt.AdditionalSourceHashes);
            hashes.AddRange(surfEncounters.AdditionalSourceHashes);
            MergeRegionalServiceConstants(constants, surfPokemon.Constants);
        }

        {
            silphPokemon = PretSilphPokemon.Load(surfOwned.Catalog, projectRoot, projectRoot);
            silphOwned = SilphOwnedPokemonCatalog.Authenticate(surfOwned, silphPokemon.Catalog);
            silphOpponents = SilphOpponentPokemonCatalog.Authenticate(silphOwned, silphPokemon.OpponentSpecies);
            saffronGymOpponents = SaffronGymOpponentPokemonCatalog.Authenticate(silphOpponents, saffronGymPokemon.OpponentSpecies);
            silphArt = PretSilphArt.Load(projectRoot, projectRoot, oakPokedex);
            pokemonContent = pokemonContent with
            {
                Catalog = silphOwned.Catalog,
                SpeciesNames = pokemonContent.SpeciesNames.Add(131, silphPokemon.AdditionalSpeciesNames[131])
            };
            hashes.AddRange(silphPokemon.AdditionalSourceHashes);
            hashes.AddRange(silphArt.AdditionalSourceHashes);
            MergeRegionalServiceConstants(constants, silphPokemon.Constants);
        }

        {
            var candidate = PretCinnabarPokemon.Extend(silphOwned.Catalog, cinnabarPokemon);
            cinnabarOwned = CinnabarOwnedPokemonCatalog.Authenticate(silphOwned, candidate);
            cinnabarOpponents = CinnabarOpponentPokemonCatalog.Authenticate(cinnabarOwned, saffronGymOpponents, cinnabarPokemon.OpponentSpecies);
            pokemonContent = pokemonContent with
            {
                Catalog = cinnabarOwned.Catalog,
                SpeciesNames = pokemonContent.SpeciesNames.AddRange(cinnabarPokemon.SpeciesNames.Where(n => cinnabarPokemon.Species.Any(p => p.Id == n.Id)).Select(n => KeyValuePair.Create(cinnabarPokemon.Species.Single(p => p.Id == n.Id).SourceSpeciesId, n.Name)))
            };
        }

        var mtMoonItems = PretMtMoonItems.LoadProject(projectRoot);
        hashes.Add("mtmoon-items:" + PretMtMoonItems.GeneratedSha256 + ":" + PretMtMoonItems.LockSha256);
        var machines = PretSsAnneMachines.LoadCatalogProject(projectRoot);
        machines = vermilionGymAssets.ExtendMachines(machines);
        {
            machines = PretRoute11Machines.LoadCatalogProject(projectRoot);
            hashes.Add("route11-machines:" + PretRoute11Machines.GeneratedSha256 + ":" + PretRoute11Machines.LockSha256);
        }

        {
            machines = PretRockTunnelMachines.LoadCatalogProject(projectRoot);
            hashes.Add("rocktunnel-machines:" + PretRockTunnelMachines.GeneratedSha256 + ":" + PretRockTunnelMachines.LockSha256);
        }

        {
            machines = PretCeladonApproachMachines.LoadCatalogProject(projectRoot);
            hashes.Add("celadon-approach-machines:" + PretCeladonApproachMachines.GeneratedSha256 + ":" + PretCeladonApproachItems.LockSha256);
        }

        {
            machines = PretCeladonGymMachines.LoadCatalogProject(projectRoot);
            hashes.Add("celadon-gym-machines:" + PretCeladonGymMachines.GeneratedSha256 + ":" + PretCeladonGymItems.LockSha256);
        }

        {
            machines = PretRocketHideoutMachines.LoadCatalogProject(projectRoot);
            hashes.Add("rocket-hideout-machines:" + PretRocketHideoutMachines.GeneratedSha256 + ":" + PretRocketHideoutItems.LockSha256);
        }

        {
            machines = PretPokemonTowerMachines.Extend(projectRoot, machines);
            hashes.Add("pokemon-tower-machines:" + PokemonTowerMachineRules.Profile + ":" + PokemonTowerMachineRules.SourceSha256);
        }

        {
            machines = PretSnorlaxMachines.Extend(projectRoot, machines);
            hashes.Add("snorlax-machines:" + SnorlaxMachineRules.Profile + ":" + SnorlaxMachineRules.SourceSha256);
        }

        {
            machines = PretEeveeMachines.Extend(projectRoot, machines);
            hashes.Add("eevee-machines:" + EeveeMachineRules.Profile + ":" + EeveeMachineRules.SourceSha256);
        }

        {
            machines = PretFuchsiaMachines.Extend(machines, fuchsiaPokemon);
            hashes.Add("fuchsia-machines:" + PretFuchsiaMachines.CanonicalSha256(machines));
        }

        machines = ComposeFuchsiaGymMachines(machines, fuchsiaGymMachines);
        {
            var safariTeaching = SafariMachineCatalog.Authenticate(machines, PretSafariMachines.Extend(machines, safariPokemon, safariItems), safariOwned);
            {
                var surfTeaching = SurfMachineCatalog.Authenticate(safariTeaching, PretSurfMachines.Extend(safariTeaching, surfPokemon), surfOwned);
                machines = SilphMachineCatalog.Authenticate(surfTeaching, PretSilphMachines.Extend(surfTeaching, silphPokemon, silphItems), silphOwned).Catalog;
            }
        }

        hashes.Add("ssanne-machines:" + PretSsAnneMachines.GeneratedSha256 + ":" + PretSsAnneMachines.LockSha256);
        hashes.Add("vermilion-machines:" + PretVermilionMachines.GeneratedSha256 + ":" + PretVermilionMachines.LockSha256);
        hashes.Add("bill-machines:" + PretBillMachines.GeneratedSha256 + ":" + PretBillMachines.LockSha256);
        hashes.Add("cerulean-machines:" + PretCeruleanMachines.GeneratedSha256 + ":" + PretCeruleanMachines.LockSha256);
        hashes.Add("route3-machines:" + PewterMachineRules.Route3Profile);
        hashes.Add("route22-machines:" + PewterMachineRules.Route22Profile);
        hashes.Add("pewter-machines:" + PretPewterMachines.GeneratedSha256 + ":" + PretPewterMachines.LockSha256);
        CampaignBattleContent battleContent;
        {
            var trainers = PretLabTrainerCatalog.Load(File.ReadAllText(Path.Combine(projectRoot, "content/generated/lab-trainers/lab-trainers.json"), Encoding.UTF8));
            hashes.Add("trainers:" + PretLabTrainerCatalog.GeneratedSha256);
            var lockBytes = File.ReadAllBytes(Path.Combine(projectRoot, "scripts/fetch-lab-battle-extra.lock.json"));
            if (Convert.ToHexStringLower(SHA256.HashData(lockBytes)) != "f514c7854d728c0f58f0b5ea83acabd8bbefddf7f3a4799f2de4304e0838cb6e")
                throw new InvalidDataException("Battle presentation lock mismatch.");
            using var extraLock = JsonDocument.Parse(lockBytes);
            string? moveNamesSource = null;
            foreach (var input in extraLock.RootElement.GetProperty("files").EnumerateArray())
            {
                var path = input.GetProperty("path").GetString()!;
                var bytes = File.ReadAllBytes(Path.Combine(projectRoot, "content/raw/lab-battle-extra", path));
                var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                var header = Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
                if (bytes.Length != input.GetProperty("byteLength").GetInt32() || hash != input.GetProperty("sha256").GetString() || Convert.ToHexStringLower(SHA1.HashData([..header, ..bytes])) != input.GetProperty("gitBlobSha1").GetString())
                    throw new InvalidDataException("Battle source differs: " + path);
                hashes.Add(path + ":" + hash);
                if (path == "src/data/text/move_names.h")
                    moveNamesSource = Encoding.UTF8.GetString(bytes);
            }

            var names = ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(moveNamesSource ?? throw new InvalidDataException("Move names source absent."), @"\[(MOVE_[A-Z0-9_]+)\]\s*=\s*_\(""([^""]*)""\)", RegexOptions.CultureInvariant))
                names.Add("pret:" + m.Groups[1].Value, m.Groups[2].Value);
            foreach (var move in new[]
            {
                "TACKLE",
                "SCRATCH",
                "GROWL",
                "TAIL_WHIP",
                "STRUGGLE"
            }

            )
                if (!names.ContainsKey("pret:MOVE_" + move))
                    throw new InvalidDataException("Battle move name missing.");
            foreach (var(move, name)in route1Pokemon.MoveNames)
            {
                if (names.TryGetValue(move.Value, out var prior) && prior != name)
                    throw new InvalidDataException("Conflicting source move name: " + move.Value);
                names[move.Value] = name;
            }

            foreach (var(move, name)in oldMan.MoveNames)
                names[move.Value] = name;
            foreach (var(move, name)in forestPokemon.MoveNames)
                names[move.Value] = name;
            foreach (var(move, name)in pewterPokemon.MoveNames)
                names[move.Value] = name;
            foreach (var(move, name)in route3Pokemon.MoveNames)
                names[move.Value] = name;
            foreach (var(move, name)in mtMoonPokemon.MoveNames)
                names[move.Value] = name;
            foreach (var(move, name)in route11Pokemon.MoveNames)
            {
                if (names.TryGetValue(move.Value, out var prior) && prior != name)
                    throw new InvalidDataException("Conflicting Route11 move name: " + move.Value);
                names[move.Value] = name;
            }

            foreach (var(move, name)in rockTunnelPokemon.MoveNames)
            {
                if (names.TryGetValue(move.Value, out var prior) && prior != name)
                    throw new InvalidDataException("Conflicting RockTunnel move name: " + move.Value);
                names[move.Value] = name;
            }

            foreach (var(move, name)in celadonApproachPokemon.MoveNames)
            {
                if (names.TryGetValue(move.Value, out var prior) && prior != name)
                    throw new InvalidDataException("Conflicting CeladonApproach move name: " + move.Value);
                names[move.Value] = name;
            }

            foreach (var(move, name)in celadonGymPokemon.MoveNames)
            {
                if (names.TryGetValue(move.Value, out var prior) && prior != name)
                    throw new InvalidDataException("Conflicting CeladonGym move name: " + move.Value);
                names[move.Value] = name;
            }

            foreach (var(move, name)in rocketHideoutPokemon.MoveNames)
            {
                if (names.TryGetValue(move.Value, out var prior) && prior != name)
                    throw new InvalidDataException("Conflicting Rocket Hideout move name: " + move.Value);
                names[move.Value] = name;
            }

            foreach (var(move, name)in pokemonTowerPokemon.AdditionalMoveNames)
                names[move.Value] = name;
            foreach (var(move, name)in snorlaxPokemon.AdditionalMoveNames)
            {
                if (names.TryGetValue(move.Value, out var prior) && prior != name)
                    throw new InvalidDataException("Conflicting Snorlax move name: " + move.Value);
                names[move.Value] = name;
            }

            foreach (var(move, name)in eeveePokemon.AdditionalMoveNames)
            {
                if (names.TryGetValue(move.Value, out var prior) && prior != name)
                    throw new InvalidDataException("Conflicting Eevee move name: " + move.Value);
                names[move.Value] = name;
            }

            foreach (var(move, name)in fuchsiaPokemon.AdditionalMoveNames)
                names[move.Value] = name;
            foreach (var(move, name)in fuchsiaGymMoveSource.MoveNames)
            {
                if (names.TryGetValue(move.Value, out var prior) && prior != name)
                    throw new InvalidDataException("Conflicting Fuchsia Gym source move name: " + move.Value);
                names[move.Value] = name;
            }

            foreach (var(move, name)in PretVermilionGymTrainers.LoadMoveNames(projectRoot))
            {
                if (names.TryGetValue(move.Value, out var prior) && prior != name)
                    throw new InvalidDataException("Conflicting Vermilion Gym move name: " + move.Value);
                names[move.Value] = name;
            }

            battleContent = new(trainers, names.ToImmutable());
        }

        OpeningItemsCatalog openingItems;
        var openingUiTexts = new Dictionary<string, string>(StringComparer.Ordinal);
        {
            openingItems = PretOpeningItems.Load(File.ReadAllText(Path.Combine(projectRoot, "content/generated/opening-items/opening-items.json"), Encoding.UTF8));
            hashes.Add("opening-items:" + PretOpeningItems.GeneratedSha256);
            var lockBytes = File.ReadAllBytes(Path.Combine(projectRoot, "scripts/fetch-opening-ui.lock.json"));
            if (Convert.ToHexStringLower(SHA256.HashData(lockBytes)) != "50e94450ab84cf6a965f0056068b1eaf33ed8314b09163bc9795ee6ca35cdb54")
                throw new InvalidDataException("Opening UI lock mismatch.");
            using var uiLock = JsonDocument.Parse(lockBytes);
            foreach (var input in uiLock.RootElement.GetProperty("files").EnumerateArray())
            {
                var path = input.GetProperty("path").GetString()!;
                var bytes = File.ReadAllBytes(Path.Combine(projectRoot, "content/raw/opening-ui", path));
                var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                var header = Encoding.ASCII.GetBytes($"blob {bytes.Length}\0");
                if (bytes.Length != input.GetProperty("byteLength").GetInt32() || hash != input.GetProperty("sha256").GetString() || Convert.ToHexStringLower(SHA1.HashData([..header, ..bytes])) != input.GetProperty("gitBlobSha1").GetString())
                    throw new InvalidDataException("Opening UI source differs: " + path);
                hashes.Add(path + ":" + hash);
                var sourceText = Encoding.UTF8.GetString(bytes);
                if (path == "include/constants/help_system.h")
                    foreach (Match m in Regex.Matches(sourceText, @"^#define\s+([A-Z][A-Z0-9_]+)\s+([0-9]+)\s*(?://[^\n]*)?$", RegexOptions.Multiline | RegexOptions.CultureInvariant))
                        constants[m.Groups[1].Value] = int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                else
                    foreach (Match m in Regex.Matches(sourceText, "const u8 ([A-Za-z0-9_]+)\\[\\] = _\\(\"((?:\\\\.|[^\"\\\\])*)\"\\);", RegexOptions.CultureInvariant))
                        openingUiTexts.TryAdd(m.Groups[1].Value, m.Groups[2].Value);
            }
        }

        // Variable and flag identity stays symbolic; numeric IDs are not save identity.
        foreach (var name in constants.Keys.Where(x => x.StartsWith("VAR_", StringComparison.Ordinal)).ToArray())
            constants.Remove(name);
        foreach (var map in maps.Values)
        {
            int localId = 1;
            foreach (var actor in map.Events.GetProperty("object_events").EnumerateArray())
            {
                if (actor.TryGetProperty("local_id", out var symbol))
                    constants[symbol.GetString()!] = localId;
                localId++;
            }
        }

        QuestItemsCatalog questItems;
        PretMartMetatiles metatiles;
        {
            questItems = PretQuestItems.Load(File.ReadAllText(Path.Combine(projectRoot, "content/generated/quest-items/quest-items.json"), Encoding.UTF8));
            {
                var tv = oldMan.TeachyTv;
                var r = tv.Source;
                var item = new QuestItemDefinition(QuestItemRules.TeachyTvId, QuestItemRules.TeachyTvSymbol, tv.SourceItemId, tv.Name, tv.Price, tv.Importance, tv.Registrability, tv.Pocket, tv.Type, tv.HoldEffect, tv.HoldEffectParam, tv.FieldUseFunc, tv.BattleUsage, tv.BattleUseFunc, tv.SecondaryId, false, false, false, new(r.Path, r.Symbol, r.StartLine, r.EndLine, r.Sha256));
                questItems = questItems with
                {
                    Profile = QuestItemRules.TutorialProfile,
                    Items = questItems.Items.Add(item)
                };
                QuestItemRules.ValidateCatalog(questItems);
                constants[QuestItemRules.TeachyTvSymbol] = tv.SourceItemId;
            }

            {
                questItems = questItems with
                {
                    Profile = QuestItemRules.TownMapProfile,
                    Items = questItems.Items.Add(townMap.Item)
                };
                QuestItemRules.ValidateCatalog(questItems);
                constants[QuestItemRules.TownMapSymbol] = townMap.Item.SourceItemId;
            }

            metatiles = PretMartMetatiles.Load(projectRoot);
            constants["METATILE_Mart_CounterMid_Top"] = metatiles.OnLoadPatches.Single(x => x.Y == 3).RawMetatileId;
            constants["METATILE_Mart_CounterMid_Bottom"] = metatiles.OnLoadPatches.Single(x => x.Y == 4).RawMetatileId;
            var parcelSource = PretParcelEventSource.Load(projectRoot);
            foreach (var pair in parcelSource.Constants)
                if (!pair.Key.StartsWith("VAR_", StringComparison.Ordinal))
                    constants[pair.Key] = pair.Value;
            foreach (var pair in parcelSource.Text)
                openingUiTexts[pair.Key] = pair.Value;
            hashes.Add("quest-items:" + PretQuestItems.GeneratedSha256);
            hashes.Add("parcel-events:" + PretParcelEventSource.LockSha256);
            hashes.Add("mart-metatiles:" + PretMartMetatiles.ManifestSha256);
        }

        var earlyShop = PretEarlyShop.Load(File.ReadAllText(Path.Combine(projectRoot, "content/generated/early-shop/early-shop.json"), Encoding.UTF8));
        earlyShop = earlyShop with
        {
            Catalog = PretBillItems.LoadShopProject(projectRoot)
        };
        earlyShop = earlyShop with
        {
            Catalog = VermilionShop(earlyShop.Catalog, vermilion)
        };
        {
            earlyShop = earlyShop with
            {
                Catalog = PretSsAnneItems.LoadShopProject(projectRoot, earlyShop.Catalog)
            };
            hashes.Add("ssanne-items:" + PretSsAnneItems.GeneratedSha256 + ":" + PretSsAnneItems.LockSha256);
        }

        {
            earlyShop = earlyShop with
            {
                Catalog = PretRoute11Items.LoadShopProject(projectRoot, earlyShop.Catalog)
            };
            hashes.Add("route11-items:" + PretRoute11Items.GeneratedSha256 + ":" + PretRoute11Items.LockSha256);
        }

        {
            earlyShop = earlyShop with
            {
                Catalog = PretRockTunnelItems.LoadShopProject(projectRoot, earlyShop.Catalog)
            };
            hashes.Add("rocktunnel-items:" + PretRockTunnelItems.GeneratedSha256 + ":" + PretRockTunnelItems.LockSha256);
        }

        {
            earlyShop = earlyShop with
            {
                Catalog = PretCeladonApproachItems.LoadShopProject(projectRoot, earlyShop.Catalog)
            };
            hashes.Add("celadon-approach-items:" + PretCeladonApproachItems.GeneratedSha256 + ":" + PretCeladonApproachItems.LockSha256);
        }

        {
            earlyShop = earlyShop with
            {
                Catalog = PretCeladonGymItems.LoadShopProject(projectRoot, earlyShop.Catalog)
            };
            hashes.Add("celadon-gym-items:" + PretCeladonGymItems.GeneratedSha256 + ":" + PretCeladonGymItems.LockSha256);
        }

        {
            earlyShop = earlyShop with
            {
                Catalog = PretRocketHideoutItems.LoadShopProject(projectRoot, earlyShop.Catalog)
            };
            hashes.Add("rocket-hideout-items:" + PretRocketHideoutItems.GeneratedSha256 + ":" + PretRocketHideoutItems.LockSha256 + ":" + PretRocketHideoutItems.ManifestSha256 + ":" + PretRocketHideoutItems.PickupsSha256);
        }

        {
            earlyShop = earlyShop with
            {
                Catalog = PretPokemonTowerItems.LoadShopProject(projectRoot, earlyShop.Catalog)
            };
            hashes.AddRange(PretPokemonTowerItems.SourceHashesProject(projectRoot));
        }

        earlyShop = earlyShop with
        {
            Catalog = ComposeFuchsiaGymShop(earlyShop.Catalog, fuchsiaGymItems)
        };
        {
            var oldShop = earlyShop.Catalog;
            earlyShop = earlyShop with
            {
                Catalog = SafariShopCatalog.Authenticate(oldShop, oldShop with { Profile = SafariShopCatalog.Profile, Items = oldShop.Items.AddRange(safariItems.Items.Select(i => safariItems.Definition(i.Metadata.Item.Id))) }).Catalog
            };
        }

        hashes.Add("early-shop:" + PretEarlyShop.GeneratedSha256);
        hashes.Add("cerulean-items:" + PretCeruleanItems.GeneratedSha256 + ":" + PretCeruleanItems.LockSha256);
        hashes.Add("bill-items:" + PretBillItems.GeneratedSha256 + ":" + PretBillItems.LockSha256);
        earlyShop = earlyShop with
        {
            Catalog = SilphShopCatalog.Authenticate(earlyShop.Catalog, PretSilphItems.Extend(earlyShop.Catalog, silphItems)).Catalog
        };
        {
            var previous = SilphMachineCatalog.For(machines) ?? throw new InvalidDataException("saffron_gym_teaching_predecessor");
            machines = SaffronGymMachineCatalog.Authenticate(previous, PretSaffronGymMachines.Extend(machines, saffronGymItems)).Catalog;
            earlyShop = earlyShop with
            {
                Catalog = ComposeSaffronGymShop(earlyShop.Catalog, saffronGym, saffronGymItems)
            };
        }

        {
            var priorMachines = SaffronGymMachineCatalog.For(machines) ?? throw new InvalidDataException("cinnabar_machine_predecessor");
            machines = CinnabarMachineCatalog.Authenticate(priorMachines, cinnabarOwned, PretCinnabarMachines.Extend(machines, cinnabarItems)).Catalog;
            var priorShop = SaffronGymShopCatalog.For(earlyShop.Catalog) ?? throw new InvalidDataException("cinnabar_shop_predecessor");
            earlyShop = earlyShop with
            {
                Catalog = CinnabarShopCatalog.Authenticate(priorShop, PretCinnabarItems.Extend(earlyShop.Catalog, cinnabarItems)).Catalog
            };
        }

        {
            var previous = CinnabarShopCatalog.For(earlyShop.Catalog) ?? throw new InvalidDataException("first_sevii_shop_predecessor");
            earlyShop = earlyShop with
            {
                Catalog = FirstSeviiShopCatalog.Authenticate(previous, firstSeviiItems).Catalog
            };
        }

        {
            var predecessor = CinnabarMachineCatalog.For(machines) ?? throw new InvalidDataException("viridian_gym_machine_predecessor");
            machines = PretViridianGymRules.ExtendMachines(predecessor, projectRoot).Catalog;
            PretViridianGymRules.ValidateSourceEvidence(projectRoot);
        }

        var victoryRoadPredecessorMachines = machines;
        {
            var shopProof = FirstSeviiShopCatalog.For(earlyShop.Catalog) ?? throw new InvalidDataException("victory_road_shop_predecessor");
            earlyShop = earlyShop with
            {
                Catalog = VictoryRoadShopCatalog.Authenticate(shopProof, PretVictoryRoadItems.ExtendShop(shopProof, victoryRoadItems)).Catalog
            };
            var machineProof = ViridianGymMachineCatalog.For(machines) ?? throw new InvalidDataException("victory_road_machine_predecessor");
            machines = VictoryRoadMachineCatalog.Authenticate(machineProof, PretVictoryRoadItems.ExtendMachines(machineProof, victoryRoadItems)).Catalog;
        }

        // Compile and validate the complete current script vocabulary.
        var compiled = PretStoryCompiler.Compile(sources, constants, includeGiftOpcodes: true, includeRoute3Opcodes: true, includeMtMoonOpcodes: true, includeBillOpcodes: true, includeSsAnneOpcodes: true, includeVermilionGymOpcodes: true, includeRockTunnelOpcodes: true, includeRocketHideoutOpcodes: true, includePokemonTowerOpcodes: true, includeSnorlaxOpcodes: true, includePresetDoubleOpcodes: true, includeSafariOpcodes: true, includeSilphOpcodes: true, includeSaffronGymData: true, includeCinnabarData: true, includeFirstSevii: true, includeViridianGym: true, includeVictoryRoad: true, includePokemonLeague: true, includeSeviiRuby: true, includeFourIsland: true, includeSixIsland: true, includeFiveIsland: true, includeCelio: true, includeSevenIsland: true, includeEventIslands: true, includePowerPlant: true, includeTrainerTower: true, includeSeafoam: true, includeGameCornerCoins: true, includeGameCornerSlots: true);
        compiled = CompleteSurfSourceFallthrough(compiled);
        compiled = CompleteFourIslandSourceFallthrough(compiled);
        compiled = CompleteFourIslandDayCareFallthrough(compiled);
        {
            var lab = maps.Values.Single(x => x.SourceMapSymbol == "MAP_PALLET_TOWN_PROFESSOR_OAKS_LAB");
            var objects = lab.Events.GetProperty("object_events").EnumerateArray().ToArray();
            var choices = ImmutableArray.CreateBuilder<StarterChoiceDefinition>();
            int Literal(string token) => int.TryParse(token, out var value) ? value : constants.TryGetValue(token, out value) ? value : throw new InvalidDataException("Starter choice source constant missing: " + token);
            foreach (var actor in objects)
            {
                var program = compiled.FindProgram(actor.GetProperty("script").GetString()!);
                if (program is null)
                    continue;
                var assignments = program.Instructions.Where(i => i.Op == StoryOp.SetVar).GroupBy(i => i.Args[0]).ToDictionary(g => g.Key, g => g.First().Args[1], StringComparer.Ordinal);
                if (!new[]
                {
                    "VAR_TEMP_1",
                    "VAR_TEMP_2",
                    "VAR_TEMP_3",
                    "VAR_TEMP_4"
                }.All(assignments.ContainsKey))
                    continue;
                var rival = objects[Literal(assignments["VAR_TEMP_4"]) - 1];
                choices.Add(new(Literal(assignments["VAR_TEMP_1"]), Literal(assignments["VAR_TEMP_2"]), Literal(assignments["VAR_TEMP_3"]), actor.GetProperty("local_id").GetString()!, actor.GetProperty("flag").GetString()!, rival.GetProperty("local_id").GetString()!, rival.GetProperty("flag").GetString()!));
            }

            if (choices.Count != 3 || !choices.Select(x => x.Index).Order().SequenceEqual(new[] { 0, 1, 2 }) || choices.Select(x => x.BallId).Distinct().Count() != 3)
                throw new InvalidDataException("Source starter branch declarations invalid.");
            pokemonContent = pokemonContent with
            {
                StarterChoices = choices.OrderBy(x => x.Index).ToImmutableArray()
            };
        }

        var bank = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var(symbol, text)in region.SourceUiTexts)
            bank.Add(symbol, text);
        foreach (var(symbol, text)in center.SourceUiTexts)
        {
            if (bank.TryGetValue(symbol, out var old) && old != text)
                throw new InvalidDataException("Conflicting center UI text: " + symbol);
            bank[symbol] = text;
        }

        AppendFourIslandDayCareTexts(fourIslandDayCare, bank);

        foreach (var text in textFiles)
        {
            string? label = null;
            StringBuilder? value = null;
            foreach (var raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            {
                var line = raw.Trim();
                var m = Regex.Match(line, @"^([A-Za-z_][A-Za-z0-9_]*):{1,2}(?:\s*@.*)?$", RegexOptions.CultureInvariant);
                if (m.Success)
                {
                    label = m.Groups[1].Value;
                    value = null;
                    continue;
                }

                if ((line.StartsWith(".string", StringComparison.Ordinal) || line.StartsWith(".braille", StringComparison.Ordinal)) && label is not null)
                {
                    var part = Regex.Match(line, "^\\.(?:string|braille)\\s+\"(.*)\"$", RegexOptions.CultureInvariant);
                    if (!part.Success)
                        throw new InvalidDataException("Unsupported text directive: " + label);
                    value ??= new();
                    value.Append(part.Groups[1].Value);
                    if (value.ToString().EndsWith('$'))
                    {
                        if (!bank.TryAdd(label, value.ToString()))
                            throw new InvalidDataException("Duplicate source text: " + label);
                        label = null;
                    }
                }
            }
        }

        {
            foreach (var symbol in new[]
            {
                "gText_TakeOutItemsFromThePC",
                "gText_StoreItemsInThePC",
                "gText_GoBackToThePreviousMenu",
                "gOtherText_Give",
                "gText_WhatWouldYouLikeToDo",
                "gText_ItemStorage",
                "gText_Mailbox",
                "gText_TurnOff",
                "gText_WithdrawItem2",
                "gText_DepositItem2",
                "gFameCheckerText_Cancel",
                "gText_ThereAreNoItems",
                "gText_TheresNoMailHere",
                "gText_WithdrawHowMany",
                "gText_DepositHowManyStrVars1",
                "gText_WithdrewQuantItem",
                "gText_DepositedStrVar2StrVar1s",
                "gText_NoMoreRoomInBag",
                "gText_ThereIsNoPokemon",
                "gText_WontHaveEffect",
                "gText_PkmnHPRestoredByVar2",
                "gOtherText_Use",
                "gText_MenuBag"
            }

            )
            {
                if (!openingUiTexts.TryGetValue(symbol, out var text))
                    throw new InvalidDataException("Opening menu text missing: " + symbol);
                bank.Add(symbol, text.Replace("{PAUSE_UNTIL_PRESS}", "", StringComparison.Ordinal) + "$");
            }
        }

        foreach (var symbol in new[]
        {
            "gText_PlayedPokeFlute",
            "gText_PlayedPokeFluteCatchy",
            "gText_PokeFluteAwakenedMon"
        }

        )
            bank.Add(symbol, openingUiTexts[symbol].Replace("{PAUSE_UNTIL_PRESS}", "", StringComparison.Ordinal) + "$");
        foreach (var symbol in new[]
        {
            "gText_BigGuy",
            "gText_BigGirl"
        }

        )
            bank.Add(symbol, openingUiTexts[symbol] + "$");
        bank.Add("gText_BerryPouch_2", openingUiTexts["gText_BerryPouch_2"] + "$");
        bank.Add("gText_PPWasRestored", openingUiTexts["gText_PPWasRestored"].Replace("{PAUSE_UNTIL_PRESS}", "", StringComparison.Ordinal) + "$");
        bank.Add("gText_MovesPPIncreased", openingUiTexts["gText_MovesPPIncreased"].Replace("{PAUSE_UNTIL_PRESS}", "", StringComparison.Ordinal) + "$");
        bank.Add("gText_BoostPp", openingUiTexts["gText_BoostPp"] + "$");
        foreach (var symbol in new[] { "gText_PkmnBaseVar2StatIncreased", "gText_ItemEffect_HP",
            "gText_ItemEffect_Attack", "gText_ItemEffect_Defense", "gText_ItemEffect_Speed",
            "gText_ItemEffect_SpAtk", "gText_ItemEffect_SpDef" })
            bank.Add(symbol, openingUiTexts[symbol].Replace("{PAUSE_UNTIL_PRESS}", "", StringComparison.Ordinal) + "$");
        foreach (var symbol in new[]
        {
            "gText_ItemsPocket",
            "gText_KeyItemsPocket",
            "gText_PokeBallsPocket"
        }

        )
            bank.Add(symbol, openingUiTexts[symbol] + "$");
        // The pinned opening-ui strings.c is byte-identical to item-ui/src/strings.c.
        // Select the English runtime text, not the unused Japanese BootedUpTM literal.
        foreach (var symbol in new[]
        {
            "gText_TMCase",
            "gText_Close",
            "gText_TMCaseWillBePutAway",
            "gText_TeachWhichPokemon",
            "gText_PkmnLearnedMove3",
            "gText_PkmnCantLearnMove",
            "gText_PkmnNeedsToReplaceMove",
            "gText_WhichMoveToForget",
            "gText_PkmnAlreadyKnows",
            "gText_TeachMoveQues",
            "gText_Able2",
            "gText_NotAble",
            "gText_Learned",
            "gText_Yes",
            "gText_No"
        }

        )
            bank.Add(symbol, openingUiTexts[symbol].Replace("{PAUSE_UNTIL_PRESS}", "", StringComparison.Ordinal) + "$");
        foreach (var(symbol, text)in oldMan.Texts)
            bank.Add(symbol, text.Text + "$");
        foreach (var text in earlyShop.Texts)
        {
            var value = text.Text.Replace("{PAUSE_UNTIL_PRESS}", "", StringComparison.Ordinal).Replace("\n", @"\n", StringComparison.Ordinal).TrimEnd('$') + "$";
            if (bank.TryGetValue(text.Symbol, out var prior) && prior != value)
                throw new InvalidDataException("Conflicting shop text: " + text.Symbol);
            bank[text.Symbol] = value;
        }

        foreach (var(symbol, text)in pokemonStorage.SourceUiTexts)
        {
            var value = text.Replace("{PAUSE_UNTIL_PRESS}", "", StringComparison.Ordinal);
            if (bank.TryGetValue(symbol, out var prior) && prior != value)
                throw new InvalidDataException("Conflicting storage UI text: " + symbol);
            bank[symbol] = value;
        }

        foreach (var(symbol, text)in oldRod.Texts)
        {
            var value = text.Replace("{PAUSE_UNTIL_PRESS}", "", StringComparison.Ordinal).Replace("\n", @"\n", StringComparison.Ordinal).TrimEnd('$') + "$";
            if (bank.TryGetValue(symbol, out var prior) && prior != value)
                throw new InvalidDataException("Conflicting fishing text: " + symbol);
            bank[symbol] = value;
        }

        // Bag and registered field-item callbacks share the same authenticated Oak refusal.
        var questItemUi = PretQuestItemUi.Load(File.ReadAllText(Path.Combine(projectRoot,
            "content/generated/quest-item-ui/quest-item-ui.json"), Encoding.UTF8));
        bank.Add("gText_OakForbidsUseOfItemHere", questItemUi.Texts["gText_OakForbidsUseOfItemHere"]
            .Replace("{PAUSE_UNTIL_PRESS}", "", StringComparison.Ordinal).Replace("\n", @"\n", StringComparison.Ordinal).TrimEnd('$') + "$");
        hashes.Add("quest-item-common-text:" + PretQuestItemUi.GeneratedSha256);

        bank.Add("gText_CoinCase", gameCornerCoins.ItemUseText.Replace("{PAUSE_UNTIL_PRESS}", "", StringComparison.Ordinal)
            .Replace("\n", @"\n", StringComparison.Ordinal).TrimEnd('$') + "$");

        var repel = PretEarlyRepel.Load(File.ReadAllText(Path.Combine(projectRoot, "content/generated/early-repel/early-repel.json"), Encoding.UTF8));
        {
            foreach (var text in repel.Texts)
            {
                var value = text.Text.Replace("{PAUSE_UNTIL_PRESS}", "", StringComparison.Ordinal).Replace("\n", @"\n", StringComparison.Ordinal).TrimEnd('$') + "$";
                if (bank.TryGetValue(text.Symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting Repel text: " + text.Symbol);
                bank[text.Symbol] = value;
            }

            hashes.Add("early-repel:" + PretEarlyRepel.GeneratedSha256);
            hashes.Add("repel-application:fr-repel-campaign-v1");
        }

        {
            hashes.AddRange(townMap.AdditionalSourceHashes);
            hashes.Add("town-map-application:fr-town-map-campaign-v1");
        }

        hashes.Add("capture-application:fr-party-capture-v1");
        {
            hashes.Add("early-starter-application:fr-early-starter-campaign-v1");
            pokemonContent = pokemonContent with
            {
                AllowSleepStatus = true
            };
        }

        hashes.Add("early-party-application:fr-early-party-campaign-v1");
        hashes.Add("pokemon-storage-application:fr-pokemon-storage-campaign-v1");
        hashes.Add("field-pickups-application:fr-field-pickups-campaign-v1");
        {
            hashes.AddRange(magikarp.AdditionalSourceHashes);
            foreach (var label in magikarp.RequiredCommonScriptLabels)
                if (compiled.FindProgram(label)is null)
                    throw new InvalidDataException("magikarp_common_script_missing: " + label);
            foreach (var label in magikarp.RequiredCommonTextLabels)
                if (!bank.ContainsKey(label))
                    throw new InvalidDataException("magikarp_common_text_missing: " + label);
        }

        ValidateSnorlaxComposition(snorlaxCompositionMaps, snorlaxCompositionPokemon, compiled, bank);
        ValidateRegionalServiceComposition(regionalServices, regionalGift, regionalServiceCompositionMaps, snorlaxCompositionMaps, regionalServiceCompositionPokemon, snorlaxCompositionPokemon, compiled, bank);
        ValidateRegionalInteriorComposition(regionalServices, departmentStore, regionalInteriorCompositionMaps, regionalServiceCompositionMaps);
        ValidateRegionalFlavorComposition(regionalInteriorCompositionMaps, compiled, bank);
        ValidateFuchsiaRouteComposition(fuchsiaRoute, fuchsiaRouteCompositionMaps, regionalInteriorCompositionMaps, fuchsiaRouteCompositionPokemon);
        ValidateFuchsiaGymComposition(fuchsiaGym, fuchsiaGymCompositionMaps, fuchsiaRouteCompositionMaps, fuchsiaGymCompositionPokemon.Catalog, fuchsiaRouteCompositionPokemon.Catalog, fuchsiaGymTrainers, compiled);
        hashes.Add("route16-shared-encounters:" + Route16FlyDoubleTrainerEncounter.Version + ":" + Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(Route16LandEncounterTables.Route16))));
        hashes.Add("preset-doubles:" + PresetDoublesContentProfile + ":" + PretRoute8Twins.GeneratedSha256 + ":" + Game.Domain.Battle.SourceBattleKernel.Version);
        ValidateFirstSeviiComposition(firstSevii, viridianGymPredecessorMaps, firstSeviiPredecessorMaps, cinnabarOwned, cinnabarOpponents, firstSeviiTrainers, compiled, bank);
        ValidateViridianGymComposition(viridianGym, victoryRoadPredecessorMaps, viridianGymPredecessorMaps, viridianGymTrainers, compiled, victoryRoadPredecessorMachines, cinnabarOwned);
        {
            PretLeagueRivalRules.ValidateSourceEvidence(projectRoot);
            ValidateLeagueRivalComposition(victoryRoadPredecessorMaps, leagueRivalTrainers, compiled, sources, bank, pokemonContent, cinnabarOwned, cinnabarOpponents);
            hashes.Add("league-rival-trainers:" + PretLeagueRivalRules.TrainersSha256);
            hashes.Add("league-rival-source-inputs:" + PretLeagueRivalRules.SourceInputsSha256);
            hashes.Add("league-rival-campaign:" + LeagueRivalContentProfile);
        }

        ValidateVictoryRoadComposition(victoryRoad, pokemonLeaguePredecessorMaps, victoryRoadPredecessorMaps, victoryRoadTrainers, cinnabarOwned, compiled);
        var pokemonLeagueOpponents = PretPokemonLeagueRules.LoadOpponents(projectRoot, PretVictoryRoadOpponents.Load(cinnabarOpponents));
        ValidatePokemonLeagueComposition(pokemonLeague, seviiRubyPredecessorMaps, pokemonLeaguePredecessorMaps, pokemonLeagueTrainers, pokemonLeagueOpponents, cinnabarOwned, compiled, bank);
        SeviiRubyOwnedPokemonCatalog seviiRubyOwned;
        SeviiRubyOpponentPokemonCatalog seviiRubyOpponents;
        SeviiRubyItemContent seviiRubyItems;
        {
            seviiRubyOwned = PretSeviiRubyRules.LoadOwned(projectRoot, cinnabarOwned);
            seviiRubyOpponents = PretSeviiRubyRules.LoadOpponents(pokemonLeagueOpponents, seviiRubyOwned);
            pokemonContent = pokemonContent with
            {
                Catalog = seviiRubyOwned.Catalog,
                SpeciesNames = pokemonContent.SpeciesNames.AddRange(SeviiRubySpeciesNames(oakPokedex, seviiRubyOwned))
            };
            seviiRubyItems = PretSeviiRubyItems.Load(seviiRuby, seviiRubyOwned, earlyShop.Catalog, machines, compiled);
            earlyShop = earlyShop with
            {
                Catalog = seviiRubyItems.Shop.Catalog
            };
            machines = seviiRubyItems.Machines.Catalog;
            hashes.Add("sevii-ruby-items:" + PretSeviiRubyItems.ItemsSha256);
            ValidateSeviiRubyComposition(seviiRuby, fourIslandPredecessorMaps, seviiRubyPredecessorMaps, seviiRubyOwned, seviiRubyOpponents, seviiRubyTrainers, compiled, pokemonContent, seviiRubyItems);
        }

        FourIslandOwnedPokemonCatalog fourIslandOwned;
        FourIslandOpponentPokemonCatalog fourIslandOpponents;
        FourIslandItemContent fourIslandItems;
        {
            fourIslandOwned = PretFourIslandRules.LoadOwned(projectRoot, seviiRubyOwned);
            fourIslandOpponents = PretFourIslandRules.LoadOpponents(seviiRubyOpponents, fourIslandOwned);
            pokemonContent = pokemonContent with
            {
                Catalog = fourIslandOwned.Catalog,
                SpeciesNames = pokemonContent.SpeciesNames.AddRange(FourIslandSpeciesNames(oakPokedex, fourIslandOwned))
            };
            fourIslandItems = PretFourIslandItems.Load(fourIsland, fourIslandOwned, earlyShop.Catalog, machines, compiled);
            earlyShop = earlyShop with
            {
                Catalog = fourIslandItems.Shop.Catalog
            };
            machines = fourIslandItems.Machines.Catalog;
            hashes.Add("four-island-items:" + PretFourIslandItems.ItemsSha256);
            ValidateFourIslandComposition(fourIsland, sixIslandPredecessorMaps, fourIslandPredecessorMaps, fourIslandOwned, fourIslandOpponents, fourIslandTrainers, compiled, pokemonContent, fourIslandItems);
        }

        SixIslandOwnedPokemonCatalog sixIslandOwned;
        SixIslandOpponentPokemonCatalog sixIslandOpponents;
        SixIslandItemContent sixIslandItems;
        {
            sixIslandOwned = PretSixIslandRules.LoadOwned(projectRoot, fourIslandOwned);
            sixIslandOpponents = PretSixIslandRules.LoadOpponents(fourIslandOpponents, sixIslandOwned);
            pokemonContent = pokemonContent with
            {
                Catalog = sixIslandOwned.Catalog,
                SpeciesNames = pokemonContent.SpeciesNames.AddRange(SixIslandSpeciesNames(oakPokedex, sixIslandOwned))
            };
            sixIslandItems = PretSixIslandItems.Load(sixIsland, sixIslandOwned, earlyShop.Catalog, machines, compiled, projectRoot);
            earlyShop = earlyShop with
            {
                Catalog = sixIslandItems.Shop.Catalog
            };
            machines = sixIslandItems.Machines.Catalog;
            hashes.Add("six-island-items:" + PretSixIslandItems.ItemsSha256);
            ValidateSixIslandComposition(sixIsland, fiveIslandPredecessorMaps, sixIslandPredecessorMaps, sixIslandOwned, sixIslandOpponents, sixIslandTrainers, compiled, pokemonContent, sixIslandItems);
        }

        FiveIslandOwnedPokemonCatalog fiveIslandOwned;
        FiveIslandOpponentPokemonCatalog fiveIslandOpponents;
        FiveIslandItemContent fiveIslandItems;
        {
            fiveIslandOwned = PretFiveIslandRules.LoadOwned(projectRoot, sixIslandOwned);
            fiveIslandOpponents = PretFiveIslandRules.LoadOpponents(sixIslandOpponents, fiveIslandOwned);
            pokemonContent = pokemonContent with
            {
                Catalog = fiveIslandOwned.Catalog,
                SpeciesNames = pokemonContent.SpeciesNames.AddRange(FiveIslandSpeciesNames(oakPokedex, fiveIslandOwned))
            };
            fiveIslandItems = PretFiveIslandItems.Load(fiveIsland, fiveIslandOwned, earlyShop.Catalog, machines, compiled, projectRoot);
            earlyShop = earlyShop with
            {
                Catalog = fiveIslandItems.Shop.Catalog
            };
            machines = fiveIslandItems.Machines.Catalog;
            hashes.Add("five-island-items:" + PretFiveIslandItems.ItemsSha256);
            ValidateFiveIslandComposition(fiveIsland, maps, fiveIslandPredecessorMaps, fiveIslandOwned, fiveIslandOpponents, fiveIslandTrainers, compiled, pokemonContent, fiveIslandItems, true);
        }

        CelioPokemonContent celioPokemon;
        ImmutableDictionary<MapId, RoamerMap> celioRoamerMaps;
        {
            celioRoamerMaps = PretCelioMapNumbers.Load(projectRoot, maps);
            PretCelioPokemon.ValidateSourceEvidence(projectRoot, projectRoot);
            celioPokemon = PretCelioPokemon.Load(projectRoot, fiveIslandOwned, fiveIslandOpponents, fiveIslandItems.Machines);
            pokemonContent = pokemonContent with
            {
                Catalog = celioPokemon.Owned.Catalog,
                SpeciesNames = pokemonContent.SpeciesNames.AddRange(CelioOwnedPokemonCatalog.AdditionalSpecies.Select(id => KeyValuePair.Create(celioPokemon.Owned.Catalog.Species[id].SourceSpeciesId, oakPokedex.Entries[celioPokemon.Owned.Catalog.Species[id].SourceSpeciesId].Name)))
            };
            machines = celioPokemon.Machines.Catalog;
            ValidateCelioComposition(maps, compiled, pokemonContent, celioPokemon, fiveIslandOwned, fiveIslandOpponents, fiveIslandItems.Machines);
            hashes.Add("celio-campaign:" + CelioContentProfile);
            hashes.Add("celio-story:" + PretCelioStory.SourceSha256);
            hashes.Add("celio-map-groups:" + PretCelioMapNumbers.SourceSha256);
            hashes.Add("celio-pokemon:" + PretCelioPokemon.PokemonSha256);
            hashes.Add("celio-machines:" + PretCelioPokemon.MachinesSha256);
            hashes.Add("celio-source-inputs:" + PretCelioPokemon.SourceInputsSha256);
        }

        // Validate the original Celio composition before extending the sole current game.
        var celioMaps = maps;
        maps = maps.AddRange(ceruleanCave.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendCeruleanCave(projectRoot, maps, celioRoamerMaps);
        PretCeruleanCavePokemon.ValidateSourceEvidence(projectRoot, projectRoot);
        var ceruleanCavePokemon = PretCeruleanCavePokemon.Load(projectRoot, celioPokemon.Owned, celioPokemon.Opponents, celioPokemon.Machines);
        pokemonContent = pokemonContent with
        {
            Catalog = ceruleanCavePokemon.Owned.Catalog,
            SpeciesNames = pokemonContent.SpeciesNames.AddRange(CeruleanCaveOwnedPokemonCatalog.AdditionalSpecies.Select(id =>
                KeyValuePair.Create(ceruleanCavePokemon.Owned.Catalog.Species[id].SourceSpeciesId,
                    oakPokedex.Entries[ceruleanCavePokemon.Owned.Catalog.Species[id].SourceSpeciesId].Name)))
        };
        machines = ceruleanCavePokemon.Machines.Catalog;
        ValidateCeruleanCaveComposition(ceruleanCave, maps, celioMaps, compiled, pokemonContent, ceruleanCavePokemon, celioPokemon);
        hashes.AddRange(ceruleanCave.AdditionalSourceHashes);
        hashes.Add("cerulean-cave-pokemon:" + PretCeruleanCavePokemon.PokemonSha256);
        hashes.Add("cerulean-cave-machines:" + PretCeruleanCavePokemon.MachinesSha256);
        hashes.Add("cerulean-cave-source-inputs:" + PretCeruleanCavePokemon.SourceInputsSha256);

        // The same current game retains original League teams and activates the source rematches after Celio.
        PretLeagueRematchRules.ValidateSourceEvidence(projectRoot);
        var leagueRematchTrainers = PretLeagueRematchRules.LoadTrainers(projectRoot);
        var leagueRematchOpponents = PretLeagueRematchRules.LoadOpponents(projectRoot, ceruleanCavePokemon.Opponents);
        ValidateLeagueRematchComposition(leagueRematchTrainers, leagueRematchOpponents, ceruleanCavePokemon, compiled, bank);
        hashes.Add("league-rematch-trainers:" + PretLeagueRematchRules.TrainersSha256);
        hashes.Add("league-rematch-opponents:" + PretLeagueRematchRules.PokemonSha256);
        hashes.Add("league-rematch-rules:" + PretLeagueRematchRules.RulesSha256);
        hashes.Add("league-rematch-source-inputs:" + PretLeagueRematchRules.SourceInputsSha256);

        var sevenPredecessorMaps = maps;
        maps = maps.AddRange(sevenIsland.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendSevenIsland(projectRoot, maps, celioRoamerMaps, sevenIsland.NewMaps);
        PretSevenIslandRules.ValidateSourceEvidence(projectRoot);
        var sevenOwned = PretSevenIslandRules.LoadOwned(projectRoot, ceruleanCavePokemon.Owned);
        var sevenOpponents = PretSevenIslandRules.LoadOpponents(projectRoot, leagueRematchOpponents, sevenOwned);
        var sevenTrainers = PretSevenIslandRules.LoadTrainers(projectRoot);
        var sevenMachines = PretSevenIslandRules.LoadMachines(projectRoot, ceruleanCavePokemon.Machines, sevenOwned);
        var sevenShop = PretSevenIslandRules.LoadShop(projectRoot, fiveIslandItems.Shop);
        var sevenItems = PretSevenIslandRules.LoadItemsDocument(projectRoot);
        pokemonContent = pokemonContent with { Catalog = sevenOwned.Catalog,
            SpeciesNames = pokemonContent.SpeciesNames.AddRange(SevenIslandOwnedPokemonCatalog.AdditionalSpecies.Select(id =>
                KeyValuePair.Create(sevenOwned.Catalog.Species[id].SourceSpeciesId, oakPokedex.Entries[sevenOwned.Catalog.Species[id].SourceSpeciesId].Name))) };
        machines = sevenMachines.Catalog;
        earlyShop = earlyShop with { Catalog = sevenShop.Catalog };

        ValidateSevenIslandComposition(sevenIsland, maps, sevenPredecessorMaps, sevenOwned, sevenOpponents,
            sevenTrainers, sevenMachines, sevenShop, sevenItems, compiled);
        hashes.Add("seven-island-trainers:" + PretSevenIslandRules.TrainersSha256);
        hashes.Add("seven-island-pokemon:" + PretSevenIslandRules.PokemonSha256);
        hashes.Add("seven-island-machines:" + PretSevenIslandRules.MachinesSha256);
        hashes.Add("seven-island-items:" + PretSevenIslandRules.ItemsSha256);
        hashes.Add("seven-island-encounters:" + PretSevenIslandRules.EncountersSha256);
        hashes.Add("seven-island-source-inputs:" + PretSevenIslandRules.SourceInputsSha256);

        maps = maps.AddRange(eventIsland.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendEventIslands(projectRoot, maps, celioRoamerMaps, eventIsland.NewMaps);
        PretEventIslandRules.ValidateSourceEvidence(projectRoot, projectRoot);
        var eventOwned = PretEventIslandRules.LoadOwned(projectRoot, sevenOwned);
        var eventOpponents = PretEventIslandRules.LoadOpponents(projectRoot, sevenOpponents, eventOwned);
        var eventMachines = PretEventIslandRules.LoadMachines(projectRoot, sevenMachines, eventOwned);
        var eventShop = PretEventIslandRules.LoadShop(projectRoot, sevenShop);
        pokemonContent = pokemonContent with { Catalog = eventOwned.Catalog,
            SpeciesNames = pokemonContent.SpeciesNames.AddRange(EventIslandOwnedPokemonCatalog.AdditionalSpecies.Select(id =>
                KeyValuePair.Create(eventOwned.Catalog.Species[id].SourceSpeciesId, oakPokedex.Entries[eventOwned.Catalog.Species[id].SourceSpeciesId].Name))) };
        machines = eventMachines.Catalog;
        earlyShop = earlyShop with { Catalog = eventShop.Catalog };
        maps=maps.AddRange(powerPlant.NewMaps);
        celioRoamerMaps=PretCelioMapNumbers.ExtendPowerPlant(projectRoot,maps,celioRoamerMaps,powerPlant.NewMaps);
        PretPowerPlantRules.ValidateSourceEvidence(projectRoot,projectRoot);
        var powerPlantOwned=PretPowerPlantRules.LoadOwned(projectRoot,eventOwned);
        var powerPlantOpponents=PretPowerPlantRules.LoadOpponents(projectRoot,eventOpponents,powerPlantOwned);
        var powerPlantMachines=PretPowerPlantRules.LoadMachines(projectRoot,eventMachines,powerPlantOwned);
        var powerPlantShop=PretPowerPlantRules.LoadShop(projectRoot,eventShop);
        pokemonContent=pokemonContent with {Catalog=powerPlantOwned.Catalog,
            SpeciesNames=pokemonContent.SpeciesNames.AddRange(PowerPlantOwnedPokemonCatalog.AdditionalSpecies.Select(id=>
                KeyValuePair.Create(powerPlantOwned.Catalog.Species[id].SourceSpeciesId,oakPokedex.Entries[powerPlantOwned.Catalog.Species[id].SourceSpeciesId].Name)))};
        machines=powerPlantMachines.Catalog;
        earlyShop=earlyShop with {Catalog=powerPlantShop.Catalog};
        hashes.Add("power-plant-species:"+PretPowerPlantRules.CatalogSha256);
        hashes.Add("power-plant-source-lock:"+PretPowerPlantRules.LockSha256);

        hashes.Add("event-island-species:" + PretEventIslandRules.CatalogSha256);
        hashes.Add("event-island-source-lock:" + PretEventIslandRules.LockSha256);
        hashes.Add("itemfinder-source:" + PretItemfinderSource.DocumentSha256);

        maps = maps.AddRange(trainerTower.Maps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendTrainerTower(projectRoot, maps, celioRoamerMaps, trainerTower.Maps);
        PretTrainerTowerRules.ValidateSourceEvidence(projectRoot);
        var trainerTowerCatalog = PretTrainerTowerRules.Load(projectRoot);
        var trainerTowerOpponents = PretTrainerTowerRules.LoadOpponents(projectRoot, powerPlantOpponents);
        var trainerTowerShop = PretTrainerTowerRules.LoadShop(projectRoot, powerPlantShop);
        earlyShop = earlyShop with { Catalog = trainerTowerShop.Catalog };
        ValidateTrainerTowerComposition(trainerTower, maps, compiled, trainerTowerCatalog, trainerTowerOpponents, trainerTowerShop);
        PretTrainerTowerPresentation.ValidateEvidence(projectRoot);
        hashes.Add("trainer-tower-rules:" + PretTrainerTowerRules.CatalogSha256);
        hashes.Add("trainer-tower-opponents:" + PretTrainerTowerRules.PokemonSha256);
        hashes.Add("trainer-tower-items:" + PretTrainerTowerRules.ItemsSha256);
        hashes.Add("trainer-tower-source-lock:" + PretTrainerTowerRules.LockSha256);
        hashes.Add("trainer-tower-presentation:" + PretTrainerTowerPresentation.ManifestSha256);
        hashes.Add("trainer-tower-presentation-source-lock:" + PretTrainerTowerPresentation.LockSha256);

        var dojoPreviousMaps = maps;
        maps = maps.AddRange(fightingDojo.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendFightingDojo(projectRoot, maps, celioRoamerMaps, fightingDojo.NewMaps);
        PretFightingDojoRules.ValidateSourceEvidence(projectRoot);
        var dojoOwned = PretFightingDojoRules.LoadOwned(projectRoot, powerPlantOwned);
        var dojoOpponents = PretFightingDojoRules.LoadOpponents(projectRoot, powerPlantOpponents, dojoOwned);
        var dojoMachines = PretFightingDojoRules.LoadMachines(projectRoot, powerPlantMachines, dojoOwned);
        var dojoTrainers = PretFightingDojoRules.LoadTrainers(projectRoot);
        pokemonContent = pokemonContent with { Catalog = dojoOwned.Catalog,
            SpeciesNames = pokemonContent.SpeciesNames.AddRange(FightingDojoOwnedPokemonCatalog.AdditionalSpecies.Select(id =>
                KeyValuePair.Create(dojoOwned.Catalog.Species[id].SourceSpeciesId, oakPokedex.Entries[dojoOwned.Catalog.Species[id].SourceSpeciesId].Name))) };
        machines = dojoMachines.Catalog;

        hashes.Add("fighting-dojo-rules:" + PretFightingDojoRules.CatalogSha256);
        hashes.Add("fighting-dojo-source-lock:" + PretFightingDojoRules.LockSha256);
        hashes.Add("fighting-dojo-trainers:" + PretFightingDojoRules.TrainersSha256);
        hashes.Add("fighting-dojo-machines:" + PretFightingDojoRules.MachinesSha256);
        hashes.Add("fighting-dojo-rule-contract:" + PretFightingDojoRules.RulesSha256);
        hashes.Add("fighting-dojo-inputs:" + PretFightingDojoRules.SourceInputsSha256);
        ValidateFightingDojoComposition(fightingDojo, maps, dojoPreviousMaps, compiled);

        var fossilLabPreviousMaps = maps;
        maps = maps.AddRange(fossilLab.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendFossilLab(projectRoot, maps, celioRoamerMaps, fossilLab.NewMaps);
        PretFossilLabRules.ValidateSourceEvidence(projectRoot);
        var fossilOwned = PretFossilLabRules.LoadOwned(projectRoot, dojoOwned);
        var fossilOpponents = PretFossilLabRules.LoadOpponents(projectRoot, dojoOpponents, fossilOwned);
        var fossilMachines = PretFossilLabRules.LoadMachines(projectRoot, dojoMachines, fossilOwned);
        var fossilShop = PretFossilLabRules.LoadShop(projectRoot, trainerTowerShop);
        earlyShop = earlyShop with { Catalog = fossilShop.Catalog };
        pokemonContent = pokemonContent with { Catalog = fossilOwned.Catalog,
            SpeciesNames = pokemonContent.SpeciesNames.AddRange(FossilLabOwnedPokemonCatalog.AdditionalSpecies.Select(id =>
                KeyValuePair.Create(fossilOwned.Catalog.Species[id].SourceSpeciesId, oakPokedex.Entries[fossilOwned.Catalog.Species[id].SourceSpeciesId].Name))) };
        machines = fossilMachines.Catalog;
        hashes.Add("fossil-lab-rules:" + PretFossilLabRules.CatalogSha256);
        hashes.Add("fossil-lab-source-lock:" + PretFossilLabRules.LockSha256);
        hashes.Add("fossil-lab-machines:" + PretFossilLabRules.MachinesSha256);
        hashes.Add("fossil-lab-shop:" + PretFossilLabRules.ShopSha256);
        hashes.Add("fossil-lab-rule-contract:" + PretFossilLabRules.RulesSha256);
        hashes.Add("fossil-lab-inputs:" + PretFossilLabRules.SourceInputsSha256);
        ValidateFossilLabComposition(fossilLab, maps, fossilLabPreviousMaps, compiled);
        hashes.Add("fossil-lab-presentation:" + PretFossilLabPresentation.ManifestSha256);
        hashes.Add("fossil-lab-presentation-source-lock:" + PretFossilLabPresentation.LockSha256);
        maps = maps.AddRange(npcTradeElyssa.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(projectRoot, maps, celioRoamerMaps, npcTradeElyssa.NewMaps);
        earlyShop = earlyShop with { Catalog = npcTradeElyssa.ExtendShop(fossilShop, npcTradeMail).Catalog };
        hashes.AddRange(npcTradeElyssa.AdditionalSourceHashes);
        hashes.AddRange(npcTradeMail.AdditionalSourceHashes);
        maps = maps.AddRange(npcTradeHaden.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(projectRoot, maps, celioRoamerMaps, npcTradeHaden.NewMaps);
        var npcTradePreviousMaps = maps;
        maps = maps.AddRange(npcTrades.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(projectRoot, maps, celioRoamerMaps, npcTrades.NewMaps);
        ValidateNpcTradeComposition(npcTrades, maps, npcTradePreviousMaps, compiled);
        var goodRodPreviousMaps = maps;
        maps = maps.AddRange(goodRodService.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(projectRoot, maps, celioRoamerMaps, goodRodService.NewMaps);
        ValidateGoodRodComposition(goodRodService, maps, goodRodPreviousMaps, compiled);
        PretNpcTradeOwnedRules.ValidateSourceEvidence(projectRoot);
        var tradeOwned = PretNpcTradeOwnedRules.LoadOwned(projectRoot, fossilOwned);
        var tradeOpponents = PretNpcTradeOwnedRules.LoadOpponents(projectRoot, fossilOpponents, tradeOwned);
        var tradeMachines = PretNpcTradeOwnedRules.LoadMachines(projectRoot, fossilMachines, tradeOwned);
        pokemonContent = pokemonContent with { Catalog = tradeOwned.Catalog,
            SpeciesNames = pokemonContent.SpeciesNames.SetItem(122, oakPokedex.Entries[122].Name).SetItem(124, oakPokedex.Entries[124].Name) };
        machines = tradeMachines.Catalog;
        PretSeafoamItems.ValidateSourceEvidence(projectRoot);
        earlyShop=earlyShop with { Catalog=PretSeafoamItems.LoadShop(projectRoot,NpcTradeShopCatalog.For(earlyShop.Catalog)!).Catalog };
        hashes.AddRange(PretSeafoamItems.AdditionalSourceHashes);
        var seafoamPreviousMaps = maps;
        maps = maps.AddRange(seafoam.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(projectRoot, maps, celioRoamerMaps, seafoam.NewMaps);
        PretSeafoamRules.ValidateSourceEvidence(projectRoot);
        PretPokeDollItems.ValidateSourceEvidence(projectRoot);
        earlyShop = earlyShop with { Catalog = PretPokeDollItems.LoadShop(projectRoot,
            SeafoamShopCatalog.For(earlyShop.Catalog) ?? throw new InvalidDataException("poke_doll_predecessor")).Catalog };
        _ = PretPokeDollItems.LoadSourceContract(projectRoot);
        hashes.AddRange(PretPokeDollItems.AdditionalSourceHashes);
        PretOrdinaryItemSupplement.ValidateSourceEvidence(projectRoot);
        earlyShop = earlyShop with { Catalog = PretOrdinaryItemSupplement.LoadShop(projectRoot, earlyShop.Catalog).Catalog };
        hashes.AddRange(PretOrdinaryItemSupplement.AdditionalSourceHashes);
        PretDepartmentFourthFloor.ValidateSourceEvidence(projectRoot);
        earlyShop = earlyShop with { Catalog = PretDepartmentFourthFloor.LoadShop(projectRoot, earlyShop.Catalog).Catalog };
        hashes.AddRange(PretDepartmentFourthFloor.AdditionalSourceHashes);
        PretDepartmentRooftopItems.ValidateSourceEvidence(projectRoot);
        earlyShop = earlyShop with { Catalog = PretDepartmentRooftopItems.LoadShop(projectRoot, earlyShop.Catalog).Catalog };
        hashes.AddRange(PretDepartmentRooftopItems.AdditionalSourceHashes);
        var seafoamOwned = PretSeafoamRules.LoadOwned(projectRoot, tradeOwned);
        var seafoamOpponents = PretSeafoamRules.LoadOpponents(projectRoot, tradeOpponents, seafoamOwned);
        var seafoamMachines = PretSeafoamRules.LoadMachines(projectRoot, tradeMachines, seafoamOwned);
        pokemonContent = pokemonContent with { Catalog = seafoamOwned.Catalog,
            SpeciesNames = pokemonContent.SpeciesNames.SetItem(144, oakPokedex.Entries[144].Name) };
        machines = seafoamMachines.Catalog;
        ValidateSeafoamComposition(seafoam, maps, seafoamPreviousMaps, compiled);
        AppendSeafoamRuleHashes(hashes);
        var reminderPreviousMaps = maps;
        maps = maps.AddRange(moveReminderHouse.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(projectRoot, maps, celioRoamerMaps, moveReminderHouse.NewMaps);
        ValidateMoveReminderComposition(moveReminderHouse, maps, reminderPreviousMaps, compiled);
        var deleterPreviousMaps = maps;
        maps = maps.AddRange(moveDeleterHouse.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(projectRoot, maps, celioRoamerMaps, moveDeleterHouse.NewMaps);
        ValidateSourceHouseComposition("move_deleter", moveDeleterHouse.NewMaps, maps, deleterPreviousMaps, compiled,
            "map.fuchsia-city", 10, "data/maps/FuchsiaCity_House3/scripts.inc", "FuchsiaCity_House3_EventScript_MoveDeleter");
        foreach (var (mapId, townId, townWarp, scriptPath, entry) in new[] {
            (PretOriginalTutorRooms.BodySlamMapId, "map.four-island", 2, "data/maps/FourIsland_House1/scripts.inc", "FourIsland_House1_EventScript_BodySlamTutor"),
            (PretOriginalTutorRooms.MetronomeMapId, "map.cinnabar-island-pokemon-lab-entrance", 4, "data/maps/CinnabarIsland_PokemonLab_ResearchRoom/scripts.inc", "CinnabarIsland_PokemonLab_ResearchRoom_EventScript_MetronomeTutor") })
        {
            var previous = maps;
            var room = ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(mapId, originalTutorRooms.NewMaps[mapId]);
            maps = maps.AddRange(room);
            celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(projectRoot, maps, celioRoamerMaps, room);
            ValidateSourceHouseComposition("original_tutor_room", room, maps, previous, compiled, townId, townWarp, scriptPath, entry);
        }
        var copycatPreviousMaps = maps;
        maps = maps.AddRange(copycatHouse.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(projectRoot, maps, celioRoamerMaps, copycatHouse.NewMaps);
        ValidateCopycatHouseComposition(copycatHouse, maps, copycatPreviousMaps, compiled);
        var superRodPreviousMaps = maps;
        maps = maps.AddRange(superRodService.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(projectRoot, maps, celioRoamerMaps, superRodService.NewMaps);
        ValidateSuperRodComposition(superRodService, maps, superRodPreviousMaps, compiled);
        PretSuperRodSpecies.ValidateSourceEvidence(projectRoot);
        var superRodOwned = PretSuperRodSpecies.LoadOwned(projectRoot, seafoamOwned);
        var superRodOpponents = PretSuperRodSpecies.LoadOpponents(projectRoot, seafoamOpponents, superRodOwned);
        var superRodMachines = PretSuperRodSpecies.LoadMachines(projectRoot, seafoamMachines, superRodOwned);
        pokemonContent = pokemonContent with { Catalog = superRodOwned.Catalog,
            SpeciesNames = pokemonContent.SpeciesNames.SetItem(211, oakPokedex.Entries[211].Name) };
        var departmentMachines = PretDepartmentStoreMachines.Load(projectRoot, departmentStore, superRodMachines.Catalog);
        var stoneOwned = PretStoneEvolutionSpecies.LoadOwned(projectRoot, superRodOwned);
        var stoneOpponents = PretStoneEvolutionSpecies.LoadOpponents(projectRoot, superRodOpponents, stoneOwned);
        var stoneMachines = PretStoneEvolutionSpecies.LoadMachines(projectRoot, departmentMachines, stoneOwned);
        pokemonContent = pokemonContent with { Catalog = stoneOwned.Catalog,
            SpeciesNames = pokemonContent.SpeciesNames.SetItems(StoneEvolutionOwnedPokemonCatalog.AdditionalSpecies.Select(id => {
                int number = stoneOwned.Catalog.Species[id].SourceSpeciesId;
                return KeyValuePair.Create(number, oakPokedex.Entries[number].Name);
            })) };
        machines = PretDepartmentRooftopItems.LoadMachines(projectRoot, stoneMachines.Catalog).Machines;
        PretGameCornerSpecies.ValidateSourceEvidence(projectRoot);
        PretGameCornerPrizes.ValidateSourceEvidence(projectRoot);
        var prizeOwned = PretGameCornerSpecies.LoadOwned(projectRoot, stoneOwned);
        _ = PretGameCornerSpecies.LoadOpponents(projectRoot, stoneOpponents, prizeOwned);
        earlyShop = earlyShop with { Catalog = PretGameCornerPrizes.LoadItems(projectRoot, earlyShop.Catalog).Catalog };
        var vsSeekerItem = vsSeekerPresentation.ItemDefinition.Deserialize<EarlyShopItemDefinition>(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidDataException("vs_seeker_item_missing");
        earlyShop = earlyShop with { Catalog = OrdinaryItemInventoryCatalog.AppendVsSeeker(earlyShop.Catalog, vsSeekerItem).Catalog };
        machines = PretGameCornerPrizes.LoadMachines(projectRoot, machines, prizeOwned).Catalog;
        pokemonContent = pokemonContent with { Catalog = prizeOwned.Catalog,
            SpeciesNames = pokemonContent.SpeciesNames.SetItems(GameCornerOwnedPokemonCatalog.AdditionalSpecies.Select(id => {
                int number = prizeOwned.Catalog.Species[id].SourceSpeciesId;
                return KeyValuePair.Create(number, oakPokedex.Entries[number].Name);
            })) };
        machines = PretFieldFly.LoadMachines(projectRoot, machines).Catalog;
        PretBreedingSpecies.ValidateSourceEvidence(projectRoot);
        var breedingOwned = PretBreedingSpecies.LoadOwned(projectRoot, prizeOwned);
        _ = PretBreedingSpecies.LoadOpponents(projectRoot, GameCornerOpponentPokemonCatalog.For(prizeOwned)!, breedingOwned);
        machines = PretBreedingSpecies.LoadMachines(projectRoot, machines, breedingOwned).Catalog;
        _ = PretBreedingSpecies.LoadMetadata(projectRoot, breedingOwned);
        pokemonContent = pokemonContent with { Catalog = breedingOwned.Catalog,
            SpeciesNames = pokemonContent.SpeciesNames.SetItems(BreedingOwnedPokemonCatalog.AdditionalSpecies.Select(id => {
                int number = breedingOwned.Catalog.Species[id].SourceSpeciesId;
                return KeyValuePair.Create(number, oakPokedex.Entries[number].Name);
            })) };
        hashes.AddRange(PretBreedingSpecies.AdditionalSourceHashes);
        hashes.Add(PretBreedingPresentation.LockSha256);
        hashes.Add(PretBreedingPresentation.ManifestSha256);
        PretOrdinaryEvolutionSpecies.ValidateSourceEvidence(projectRoot);
        var ordinaryOwned = PretOrdinaryEvolutionSpecies.LoadOwned(projectRoot, breedingOwned);
        var ordinaryOpponents = PretOrdinaryEvolutionSpecies.LoadOpponents(projectRoot, BreedingOpponentPokemonCatalog.For(breedingOwned)!, ordinaryOwned);
        var memorialOpponents = MemorialPillarOpponentPokemonCatalog.Authenticate(ordinaryOpponents);
        var vsSeekerOpponents = VsSeekerOpponentPokemonCatalog.Authenticate(memorialOpponents.Owned.SharedBattle, memorialOpponents.Catalog);
        var memorialTrainers = PretMemorialPillarTrainers.Load(originalTmAcquisition.MemorialTrainers);
        hashes.Add(PretMemorialPillarTrainers.TrainersSha256);
        hashes.Add(MemorialPillarOpponentPokemonCatalog.Profile);
        hashes.Add(PretMemorialPillarPresentation.ManifestSha256);
        hashes.Add(PretMemorialPillarPresentation.LockSha256);
        machines = PretOrdinaryEvolutionSpecies.LoadMachines(projectRoot, machines, ordinaryOwned).Catalog;
        _ = PretOrdinaryEvolutionSpecies.LoadMetadata(projectRoot, ordinaryOwned);
        pokemonContent = pokemonContent with { Catalog = ordinaryOwned.Catalog,
            SpeciesNames = pokemonContent.SpeciesNames.SetItems(OrdinaryEvolutionOwnedPokemonCatalog.AdditionalSpecies.Select(id => {
                int number = ordinaryOwned.Catalog.Species[id].SourceSpeciesId;
                return KeyValuePair.Create(number, oakPokedex.Entries[number].Name);
            })) };
        hashes.AddRange(PretOrdinaryEvolutionSpecies.AdditionalSourceHashes);
        hashes.Add(PretOrdinaryEvolutionPresentation.LockSha256);
        hashes.Add(PretOrdinaryEvolutionPresentation.ManifestSha256);
        PretOriginalMachineCatalog.ValidateSourceEvidence(projectRoot);
        machines = PretOriginalMachineCatalog.Load(projectRoot, OrdinaryEvolutionMachineCatalog.For(machines)
            ?? throw new InvalidDataException("original_machine_predecessor")).Catalog;
        hashes.AddRange(PretOriginalMachineCatalog.AdditionalSourceHashes);
        var gameCornerPrizes = PretGameCornerPrizes.LoadCatalog(projectRoot, ordinaryOwned.Catalog, earlyShop.Catalog, machines);
        hashes.AddRange(PretGameCornerSpecies.AdditionalSourceHashes);
        hashes.AddRange(PretGameCornerPrizes.AdditionalSourceHashes);
        hashes.AddRange(PretStoneEvolutionSpecies.AdditionalSourceHashes);
        hashes.AddRange(PretSuperRodSpecies.AdditionalSourceHashes);
        hashes.AddRange(PretDepartmentStoreMachines.AdditionalSourceHashes);
        var capeBrinkPreviousMaps = maps;
        maps = maps.AddRange(capeBrinkTutor.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(projectRoot, maps, celioRoamerMaps, capeBrinkTutor.NewMaps);
        ValidateCapeBrinkComposition(capeBrinkTutor, maps, capeBrinkPreviousMaps, compiled, superRodService);
        var prizeRoom = PretGameCornerPrizeRoom.Load(regionalServices, maps);
        maps = maps.AddRange(prizeRoom.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(projectRoot, maps, celioRoamerMaps, prizeRoom.NewMaps);
        hashes.AddRange(prizeRoom.AdditionalSourceHashes);
        route16FlyHouse.ValidateReusedMaps(maps);
        maps = maps.AddRange(route16FlyHouse.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(projectRoot, maps, celioRoamerMaps, route16FlyHouse.NewMaps);
        var daycarePreviousMaps = maps;
        maps = maps.AddRange(route5DayCare.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(projectRoot, maps, celioRoamerMaps, route5DayCare.NewMaps);
        ValidateRoute5DayCareComposition(route5DayCare, maps, daycarePreviousMaps, compiled);
        var fourDaycarePreviousMaps = maps;
        maps = maps.AddRange(fourIslandDayCare.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(projectRoot, maps, celioRoamerMaps, fourIslandDayCare.NewMaps);
        ValidateFourIslandDayCareComposition(fourIslandDayCare, maps, fourDaycarePreviousMaps, compiled);
        maps = originalTmAcquisition.MountMaps(maps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(projectRoot, maps, celioRoamerMaps, originalTmAcquisition.NewMaps);
        ValidateCapeBrinkEncounterSources(capeBrinkTutor, ordinaryOwned.Catalog, superRodService);
        var moveReminder = PretMoveReminderService.Load(projectRoot, ordinaryOwned.Catalog);
        hashes.AddRange(moveReminder.AdditionalSourceHashes);
        var moveTutors = PretMoveTutors.Load(projectRoot, ordinaryOwned.Catalog);
        hashes.AddRange(moveTutors.AdditionalSourceHashes);
        hashes.Add("npc-trade-owned:" + PretNpcTradeOwnedRules.PokemonSha256);
        hashes.Add("npc-trade-owned-machines:" + PretNpcTradeOwnedRules.MachinesSha256);
        hashes.Add("npc-trade-owned-source-lock:" + PretNpcTradeOwnedRules.LockSha256);
        hashes.Add("npc-trade-owned-inputs:" + PretNpcTradeOwnedRules.SourceInputsSha256);

        hashes.Add("slot-machine-rules:" + SlotMachineRules.RulesSha256);
        hashes.Add("slot-machine-policy:" + SlotMachineSessionContract.ApprovedProductionPolicyId);

        hashes.AddRange(fameChecker.AdditionalSourceHashes);
        hashes.AddRange(vsSeeker.AdditionalSourceHashes);
        hashes.AddRange(vsSeekerPresentation.AdditionalSourceHashes);
        hashes.AddRange(new[] { "vs-seeker-parties:" + PretVsSeekerParties.CatalogSha256, "vs-seeker-occurrences:" + PretVsSeekerParties.OccurrencesSha256, "vs-seeker-party-lock:" + PretVsSeekerParties.LockSha256 });

        // Source additions deliberately change the current content/save fingerprint.
        var version = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", hashes))));
        return new(
            maps: maps,
            scripts: compiled,
            text: bank.ToImmutable(),
            version: version,
            pokemonContent: pokemonContent,
            battleContent: battleContent,
            openingItems: openingItems,
            region: region,
            questItems: questItems,
            metatiles: metatiles,
            center: center,
            oldMan: oldMan,
            forest: forest,
            forestPokemon: forestPokemon,
            pewter: pewter,
            pewterPokemon: pewterPokemon,
            machines: machines,
            earlyShop: earlyShop,
            pokemonStorage: pokemonStorage,
            fieldPickups: fieldPickups,
            repel: repel,
            townMap: townMap,
            oakRating: oakRating,
            oakPokedex: oakPokedex,
            route22: route22,
            route22Pokemon: route22Pokemon,
            route3: route3,
            route3Pokemon: route3Pokemon,
            magikarp: magikarp,
            mtMoon: mtMoon,
            mtMoonPokemon: mtMoonPokemon,
            mtMoonItems: mtMoonItems,
            cerulean: cerulean,
            ceruleanPokemon: ceruleanPokemon,
            bill: bill,
            billPokemon: billPokemon,
            billItems: billItems,
            billTrainers: billTrainers,
            vermilion: vermilion,
            vermilionPokemon: vermilionPokemon,
            vermilionTrainers: vermilionTrainers,
            ssAnne: ssAnne,
            ssAnnePokemon: ssAnnePokemon,
            ssAnneTrainers: ssAnneTrainers,
            ssAnneItems: ssAnneItems,
            vermilionGym: vermilionGym,
            vermilionGymTrainers: vermilionGymTrainers,
            cityServices: cityServices,
            gameCornerCoins: gameCornerCoins,
            gameCornerPrizes: gameCornerPrizes,
            oldRod: oldRod,
            route11: route11,
            route11Pokemon: route11Pokemon,
            route11Items: route11Items,
            route11Encounters: route11Encounters,
            route11Trainers: route11Trainers,
            rockTunnel: rockTunnel,
            rockTunnelPokemon: rockTunnelPokemon,
            rockTunnelItems: rockTunnelItems,
            rockTunnelEncounters: rockTunnelEncounters,
            rockTunnelTrainers: rockTunnelTrainers,
            celadonApproach: celadonApproach,
            celadonApproachPokemon: celadonApproachPokemon,
            celadonApproachItems: celadonApproachItems,
            celadonApproachEncounters: celadonApproachEncounters,
            celadonApproachTrainers: celadonApproachTrainers,
            celadonGym: celadonGym,
            celadonGymPokemon: celadonGymPokemon,
            celadonGymItems: celadonGymItems,
            celadonGymTrainers: celadonGymTrainers,
            rocketHideout: rocketHideout,
            rocketHideoutPokemon: rocketHideoutPokemon,
            rocketHideoutItems: rocketHideoutItems,
            rocketHideoutPickups: rocketHideoutPickups,
            rocketHideoutTrainers: rocketHideoutTrainers,
            pokemonTower: pokemonTower,
            pokemonTowerPokemon: pokemonTowerPokemon,
            pokemonTowerItems: pokemonTowerItems,
            pokemonTowerPickups: pokemonTowerPickups,
            pokemonTowerTrainers: pokemonTowerTrainers,
            snorlax: snorlax,
            snorlaxPokemon: snorlaxPokemon,
            snorlaxArt: snorlaxArt,
            regionalServices: regionalServices,
            regionalGift: regionalGift,
            eeveePokemon: eeveePokemon,
            eeveeArt: eeveeArt,
            departmentStore: departmentStore,
            nameRater: nameRater,
            fuchsiaRoute: fuchsiaRoute,
            fuchsiaPokemon: fuchsiaPokemon,
            fuchsiaArt: fuchsiaArt,
            fuchsiaGym: fuchsiaGym,
            fuchsiaGymTrainers: fuchsiaGymTrainers,
            fuchsiaGymItems: fuchsiaGymItems,
            fuchsiaGymMachines: fuchsiaGymMachines,
            twins: twins,
            doubleMechanics: doubleMechanics,
            doubleItems: doubleItems,
            safari: safari,
            safariPokemon: safariPokemon,
            safariArt: safariArt,
            safariItems: safariItems,
            safariEncounters: safariEncounters,
            surfPokemon: surfPokemon,
            surfArt: surfArt,
            surfEncounters: surfEncounters,
            surfOwned: surfOwned,
            silph: silph,
            silphPokemon: silphPokemon,
            silphItems: silphItems,
            silphTrainers: silphTrainers,
            silphOwned: silphOwned,
            silphOpponents: silphOpponents,
            silphArt: silphArt,
            saffronGym: saffronGym,
            saffronGymTrainers: saffronGymTrainers,
            saffronGymItems: saffronGymItems,
            saffronGymOpponents: saffronGymOpponents,
            saffronGymPokemon: saffronGymPokemon,
            saffronGymArt: saffronGymArt,
            cinnabar: cinnabar,
            cinnabarPokemon: cinnabarPokemon,
            cinnabarTrainers: cinnabarTrainers,
            cinnabarItems: cinnabarItems,
            cinnabarOwned: cinnabarOwned,
            cinnabarOpponents: cinnabarOpponents,
            cinnabarArt: cinnabarArt,
            cinnabarPresentation: cinnabarPresentation,
            firstSevii: firstSevii,
            firstSeviiTrainers: firstSeviiTrainers,
            firstSeviiItems: firstSeviiItems,
            firstSeviiPresentation: firstSeviiPresentation,
            viridianGym: viridianGym,
            viridianGymTrainers: viridianGymTrainers,
            leagueRivalTrainers: leagueRivalTrainers,
            victoryRoad: victoryRoad,
            victoryRoadTrainers: victoryRoadTrainers,
            victoryRoadItems: victoryRoadItems,
            pokemonLeague: pokemonLeague,
            pokemonLeagueTrainers: pokemonLeagueTrainers,
            pokemonLeagueOpponents: pokemonLeagueOpponents,
            nationalDexSource: nationalDexSource,
            seviiRuby: seviiRuby,
            seviiRubyOwned: seviiRubyOwned,
            seviiRubyOpponents: seviiRubyOpponents,
            seviiRubyTrainers: seviiRubyTrainers,
            seviiRubyItems: seviiRubyItems,
            fourIsland: fourIsland,
            fourIslandOwned: fourIslandOwned,
            fourIslandOpponents: fourIslandOpponents,
            fourIslandTrainers: fourIslandTrainers,
            fourIslandItems: fourIslandItems,
            sixIsland: sixIsland,
            sixIslandOwned: sixIslandOwned,
            sixIslandOpponents: sixIslandOpponents,
            sixIslandTrainers: sixIslandTrainers,
            sixIslandItems: sixIslandItems,
            fiveIsland: fiveIsland,
            fiveIslandOwned: fiveIslandOwned,
            fiveIslandOpponents: fiveIslandOpponents,
            fiveIslandTrainers: fiveIslandTrainers,
            fiveIslandItems: fiveIslandItems,
            celioPokemon: celioPokemon,
            celioRoamerMaps: celioRoamerMaps,
            ceruleanCave: ceruleanCave,
            ceruleanCavePokemon: ceruleanCavePokemon,
            leagueRematchTrainers: leagueRematchTrainers,
            leagueRematchOpponents: leagueRematchOpponents,
            sevenIsland: sevenIsland, sevenOwned: sevenOwned, sevenOpponents: sevenOpponents,
            sevenTrainers: sevenTrainers, sevenMachines: sevenMachines, sevenShop: sevenShop, sevenItems: sevenItems, moveTutors: moveTutors, eventIsland: eventIsland, eventOwned: eventOwned, eventOpponents: eventOpponents, eventMachines: eventMachines, eventShop: eventShop, powerPlant: powerPlant, powerPlantOwned: powerPlantOwned, powerPlantOpponents: powerPlantOpponents, powerPlantMachines: powerPlantMachines, powerPlantShop: powerPlantShop, trainerTower: trainerTower, trainerTowerCatalog: trainerTowerCatalog,
            trainerTowerOpponents: trainerTowerOpponents, trainerTowerShop: trainerTowerShop,
            fightingDojo: fightingDojo, dojoOwned: dojoOwned, dojoOpponents: dojoOpponents, dojoMachines: dojoMachines, dojoTrainers: dojoTrainers, fossilLab: fossilLab, fossilOwned: fossilOwned, fossilOpponents: fossilOpponents, fossilMachines: fossilMachines, fossilShop: fossilShop, npcTrades: npcTrades, npcTradeElyssa: npcTradeElyssa, npcTradeHaden: npcTradeHaden, npcTradeMail: npcTradeMail, goodRodService: goodRodService, seafoam: seafoam, seafoamOwned: seafoamOwned, seafoamOpponents: seafoamOpponents, seafoamMachines: seafoamMachines, seafoamTrainers: seafoamTrainers, moveReminderHouse: moveReminderHouse, moveReminder: moveReminder, moveDeleterHouse: moveDeleterHouse, originalTutorRooms: originalTutorRooms, copycatHouse: copycatHouse, superRodService: superRodService, superRodOwned: superRodOwned, superRodOpponents: superRodOpponents, superRodMachines: superRodMachines, capeBrinkTutor: capeBrinkTutor, fieldFly: fieldFly, flyMapPresentation: flyMapPresentation, route16FlyHouse: route16FlyHouse, route5DayCare: route5DayCare, fourIslandDayCare: fourIslandDayCare, originalTmAcquisition: originalTmAcquisition) { VsSeeker = vsSeeker.Catalog, VsSeekerTexts = vsSeekerPresentation.EncodedTexts, VsSeekerTrainers = vsSeekerTrainers, VsSeekerBattleOccurrences = vsSeekerOccurrences, VsSeekerOpponents = vsSeekerOpponents, FameChecker = fameChecker.Catalog, GameCornerSlots = SlotMachineSessionContract.CreateApprovedProduction(slotRules), ItemfinderDocument = itemfinder, FieldRockSmash = fieldRockSmash, MemorialPillarTrainers = memorialTrainers, MemorialPillarOpponents = memorialOpponents };
    }

    private static void AppendCeruleanCaveSources(PretCeruleanCaveRegion region,
        List<PretSourceScriptFile> sources, List<string> texts, Dictionary<string, int> constants)
    {
        var existing = sources.ToDictionary(s => s.Path, StringComparer.Ordinal);
        foreach (var file in region.Scripts)
        {
            if (existing.TryGetValue(file.Path, out var prior))
            {
                if (prior.SourceRef.Sha256 != file.SourceRef.Sha256 || prior.Text != file.Text)
                    throw new InvalidDataException("cerulean_cave_source_conflict:" + file.Path);
            }
            else { existing.Add(file.Path, file); sources.Add(file); }
            if (!texts.Contains(file.Text, StringComparer.Ordinal)) texts.Add(file.Text);
        }
        foreach (var text in region.TextSources.Values)
            if (!texts.Contains(text, StringComparer.Ordinal)) texts.Add(text);
        MergeRegionalServiceConstants(constants, region.Constants);
    }

    private static void ValidateCeruleanCaveComposition(PretCeruleanCaveRegion region,
        ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, PretWorldMap> previous,
        PretStoryCompilation scripts, CampaignPokemonContent pokemon, CeruleanCavePokemonContent cave, CelioPokemonContent celio)
    {
        if (previous.Count != 254 || maps.Count != 257 || region.NewMaps.Count != 3 ||
            previous.Any(p => !ReferenceEquals(p.Value, maps[p.Key])) ||
            region.NewMaps.Any(p => previous.ContainsKey(p.Key) || !ReferenceEquals(p.Value, maps[p.Key])) ||
            cave.Owned.Catalog.Species.Count != 139 || cave.Opponents.Catalog.Species.Count != 163 ||
            cave.Machines.Catalog.AdditionalMachines is not { Length: 38 } ||
            !ReferenceEquals(cave.Owned.Predecessor, celio.Owned) || !ReferenceEquals(cave.Opponents.Predecessor, celio.Opponents) ||
            !ReferenceEquals(cave.Machines.Predecessor, celio.Machines) || !ReferenceEquals(pokemon.Catalog, cave.Owned.Catalog) ||
            !ReferenceEquals(cave.Owned.Mechanics, celio.Owned.Mechanics) ||
            !ReferenceEquals(cave.Owned.Catalog.Moves, celio.Owned.Catalog.Moves) || pokemon.Catalog.Moves.Count != 354)
            throw new InvalidDataException("cerulean_cave_composition_scope");
        var entrance = region.Contracts.GetProperty("cityEntrance");
        var city = previous[new MapId("map.cerulean-city")];
        var warp = entrance.GetProperty("entranceWarp");
        var guard = entrance.GetProperty("guard");
        if (!JsonElement.DeepEquals(city.Events.GetProperty("warp_events")[warp.GetProperty("index").GetInt32()], warp.GetProperty("event")) ||
            !JsonElement.DeepEquals(city.Events.GetProperty("object_events")[guard.GetProperty("localId").GetInt32() - 1], guard.GetProperty("event")))
            throw new InvalidDataException("cerulean_cave_city_anchor");
        foreach (var label in new[] { "CeruleanCave_B1F_OnResume", "CeruleanCave_B1F_EventScript_TryRemoveMewtwo",
            "CeruleanCave_B1F_OnTransition", "CeruleanCave_B1F_EventScript_ShowMewtwo", "CeruleanCave_B1F_EventScript_Mewtwo",
            "CeruleanCave_B1F_EventScript_DefeatedMewtwo", "CeruleanCave_B1F_EventScript_RanFromMewtwo" })
            if (scripts.FindProgram(label) is not { } program || program.Instructions.IsEmpty || program.Instructions.Any(i => i.Op == StoryOp.Unsupported))
                throw new InvalidDataException("cerulean_cave_story_incomplete:" + label);
        PokemonFactory.ValidateCatalog(cave.Owned.Catalog);
    }
}

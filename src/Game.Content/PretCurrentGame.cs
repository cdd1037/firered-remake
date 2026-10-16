using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
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
    /// <summary>Builds the current game from generated gameplay resources and compiled story IR.</summary>
    public static PretCampaignCatalog LoadCurrent(string projectRoot)
    {
        var canonicalPokemon = PretCanonicalPokemonCatalog.Load(projectRoot, projectRoot);
        var sharedItems = SharedItemCatalog.LoadProject(projectRoot);
        var sharedMachines = SharedMachineCatalog.LoadProject(projectRoot, sharedItems);
        // Resource readers validate their formats; no original-file identity pass is required.
        var mapNumbers = PretCelioMapNumbers.LoadSourceTable(projectRoot);
        var eventIsland = PretEventIslandRegion.Load(projectRoot);
        var powerPlant = PretPowerPlantRegion.Load(projectRoot);
        var trainerTower = PretTrainerTowerRegion.Load(projectRoot);
        var fightingDojo = PretFightingDojoRegion.Load(projectRoot);
        var fossilLab = PretFossilLabRegion.Load(projectRoot);
        var itemfinder = PretItemfinderSource.Load(projectRoot);
        var teachyTv = PretTeachyTv.Load(File.ReadAllText(Path.Combine(projectRoot, "content/generated/data/services/teachy-tv.json")));
        var fameChecker = PretFameChecker.Load(projectRoot);
        var vsSeeker = PretVsSeeker.Load(projectRoot);
        var vsSeekerPresentation = PretVsSeekerPresentation.Load(projectRoot);
        var vsSeekerTrainers = PretVsSeekerParties.Load(projectRoot);
        var vsSeekerOccurrences = PretVsSeekerParties.LoadOccurrences(projectRoot);
        var slotRules = SlotMachineRules.FromAuthenticatedJson(File.ReadAllBytes(Path.Combine(projectRoot, "content/generated/data/services/slot-machine/rules.json")));
        var sevenIsland = PretSevenIslandRegion.Load(projectRoot);
        var ceruleanCave = PretCeruleanCaveRegion.Load(projectRoot);
        var fiveIsland = PretFiveIslandRegion.Load(projectRoot);
        var fiveIslandTrainers = PretFiveIslandRules.LoadTrainers(projectRoot);
        var sixIsland = PretSixIslandRegion.Load(projectRoot);
        var sixIslandTrainers = PretSixIslandRules.LoadTrainers(projectRoot);
        var fourIsland = PretFourIslandRegion.Load(projectRoot);
        var fourIslandTrainers = PretFourIslandRules.LoadTrainers(projectRoot);
        var seviiRuby = PretSeviiRubyRegion.Load(projectRoot);
        var seviiRubyTrainers = PretSeviiRubyRules.LoadTrainers(projectRoot);
        var twins = PretRoute8Twins.Load(projectRoot);
        var doubleMechanics = SourceMechanicsCatalogLoader.LoadAuthenticated(canonicalPokemon, projectRoot, sharedItems);
        var npcTrades = PretNpcTrades.Load(projectRoot, doubleMechanics.Catalog);
        var gamePokemon = PretGamePokemonCatalog.Load(projectRoot, canonicalPokemon, doubleMechanics, npcTrades.Trades, sharedMachines);
        var npcTradeElyssa = PretNpcTradeElyssa.Load(projectRoot);
        var npcTradeHaden = PretNpcTradeHaden.Load(projectRoot);
        var npcTradeMail = PretNpcTradeMail.Load(projectRoot);
        var mailComposer = PretMailComposer.Load(projectRoot);
        var mailPresentation = PretMailPresentation.Load(projectRoot, items: sharedItems);
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
        var cyclingRoad = PretCyclingRoadRegion.Load(projectRoot);
        var route5DayCare = PretRoute5DayCare.Load(projectRoot);
        var fourIslandDayCare = PretFourIslandDayCare.Load(projectRoot);
        var originalTmAcquisition = PretOriginalTmAcquisition.Load(projectRoot);
        var schoolHouseInteriors = PretSchoolHouseInteriors.Load(projectRoot);
        var lostCave = PretLostCaveRegion.Load(projectRoot);
        var trainerFanClub = PretTrainerFanClub.Load(projectRoot);
        var lostCaveTrainers = LostCaveTrainerPartyFactory.Authenticate(vsSeekerTrainers, SharedTrainerCatalog.LoadProject(projectRoot).Select(LostCaveTrainerPartyFactory.AllTrainers));
        var fieldFly = PretFieldFly.Load(projectRoot, shared: sharedMachines);
        var fieldRockSmash = PretFieldRockSmash.Load(projectRoot);
        var flyMapPresentation = PretFlyMap.Load(projectRoot);
        var doubleItems = doubleMechanics.Catalog.BattleItems ?? throw new InvalidDataException("Preset doubles requires the authenticated battle item catalog.");
        var magikarp = PretMagikarpContract.Load(projectRoot);
        var runtimeMaps = PretRuntimeMapCatalog.Load(Path.Combine(projectRoot, PretRocketHideoutRuntimeMaps.ManifestPath));
        var route22 = PretRoute22Region.Load(projectRoot, runtimeMaps);
        var route3 = PretRoute3Region.Load(projectRoot, runtimeMaps);
        var oakPokedex = PretPokedexMenu.Load(projectRoot);
        var route22Pokemon = PretRoute22Pokemon.Load(canonicalPokemon, projectRoot, includeSourceCryWaves: false, pokedexMenu: oakPokedex);
        var route3Pokemon = PretRoute3Pokemon.Load(canonicalPokemon, projectRoot, pokedexMenu: oakPokedex);
        var ceruleanPokemon = PretCeruleanPokemon.Load(canonicalPokemon, projectRoot, pokedexMenu: oakPokedex);
        var pokemonTower = PretPokemonTowerRegion.Load(projectRoot, PretPokemonTowerRuntimeMaps.Load(projectRoot));
        PretPokemonTowerPokemon pokemonTowerPokemon;
        var snorlax = PretSnorlaxRegion.Load(projectRoot);
        PretSnorlaxPokemon snorlaxPokemon;
        var snorlaxArt = PretSnorlaxArt.Load(projectRoot, pokedexMenu: oakPokedex);
        var regionalServices = PretRegionalServiceMaps.Load(projectRoot);
        var regionalGift = PretRegionalGiftContract.Load(projectRoot, sharedItems);
        var eeveeArt = PretEeveeArt.Load(projectRoot, pokedexMenu: oakPokedex);
        var departmentStore = PretDepartmentStoreContract.Load(projectRoot);
        var nameRater = PretNameRaterContract.Load(projectRoot);
        PretEeveePokemon eeveePokemon;
        var fuchsiaRoute = PretFuchsiaRouteRegion.Load(projectRoot);
        PretFuchsiaRoutePokemon fuchsiaPokemon;
        var fuchsiaGym = PretFuchsiaGymRegion.Load(projectRoot, runtimeMaps.FuchsiaGymPackage(projectRoot));
        var fuchsiaGymTrainers = PretFuchsiaGymTrainers.LoadDocument(canonicalPokemon, projectRoot);
        var fuchsiaGymItems = PretFuchsiaGymItems.LoadProject(projectRoot, shared: sharedMachines);
        var fuchsiaGymMachines = PretFuchsiaGymMachines.LoadProject(projectRoot);
        var fuchsiaGymMoveSource = canonicalPokemon.MoveCatalog;
        var safari = PretSafariRegion.Load(projectRoot);
        var safariEncounters = PretSafariEncounters.Load(safari);
        var safariItems = PretSafariItems.LoadProject(projectRoot);
        var safariArt = PretSafariArt.Load(projectRoot);
        PretSafariPokemon safariPokemon;

        PretSurfPokemon surfPokemon;
        PretSurfArt surfArt;
        PretSurfEncounters surfEncounters;

        var viridianGym = PretViridianGymRegion.Load(projectRoot);
        var viridianGymTrainers = PretViridianGymRules.LoadTrainers(projectRoot);
        var leagueRivalTrainers = PretLeagueRivalRules.LoadTrainers(projectRoot);
        var victoryRoad = PretVictoryRoadRegion.Load(projectRoot);
        var victoryRoadTrainers = PretVictoryRoadRules.LoadTrainers(projectRoot);
        var victoryRoadItems = PretVictoryRoadItems.LoadProject(projectRoot);
        _ = PretVictoryRoadRules.LoadWild(projectRoot);
        var pokemonLeague = PretPokemonLeagueRegion.Load(projectRoot);
        var pokemonLeagueTrainers = PretPokemonLeagueRules.LoadTrainers(projectRoot);
        var firstSevii = PretFirstSeviiRegion.Load(projectRoot);
        var firstSeviiTrainers = PretFirstSeviiRules.LoadTrainers(projectRoot, shared: sharedMachines);
        var firstSeviiItems = PretFirstSeviiRules.LoadItems(projectRoot, shared: sharedMachines);
        var firstSeviiPresentation = PretFirstSeviiPresentation.LoadProject(projectRoot, projectRoot);
        var cinnabar = PretCinnabarRegion.Load(projectRoot);
        var cinnabarPokemon = PretCinnabarPokemon.LoadDocument(canonicalPokemon, projectRoot, projectRoot);
        var cinnabarTrainers = PretCinnabarTrainers.Load(projectRoot);
        var cinnabarItems = PretCinnabarItems.LoadProject(projectRoot);

        var saffronGym = PretSaffronGymRegion.Load(projectRoot);
        var saffronGymTrainers = PretSaffronGymTrainers.Load(canonicalPokemon, projectRoot);
        var saffronGymArt = PretSaffronGymArt.Load(projectRoot, projectRoot, pokedexMenu: oakPokedex);
        var cinnabarArt = PretCinnabarArt.Load(projectRoot, projectRoot, pokedexMenu: oakPokedex);
        var cinnabarPresentation = PretCinnabarPresentation.Load(projectRoot);
        var saffronGymItems = PretSaffronGymItems.LoadProject(projectRoot);
        var saffronGymPokemon = PretSaffronGymPokemon.LoadDocument(canonicalPokemon, projectRoot, projectRoot);

        var silph = PretSilphRegion.Load(projectRoot);
        var silphItems = PretSilphItems.LoadProject(projectRoot);
        var silphTrainers = PretSilphTrainers.Load(canonicalPokemon, projectRoot);
        PretSilphPokemon silphPokemon;

        PretSilphArt silphArt;
        var fuchsiaArt = PretFuchsiaRouteArt.Load(projectRoot, pokedexMenu: oakPokedex);
        var pokemonTowerItems = PretPokemonTowerItems.LoadProject(projectRoot, shared: sharedMachines);
        var pokemonTowerPickups = PretPokemonTowerItems.LoadPickupsProject(projectRoot);
        var pokemonTowerTrainers = PretPokemonTowerTrainers.Load(canonicalPokemon, projectRoot);
        var rocketHideout = PretRocketHideoutRegion.Load(projectRoot, runtimeMaps);
        var rocketHideoutPokemon = PretRocketHideoutPokemon.Load(canonicalPokemon, projectRoot, pokedexMenu: oakPokedex);
        var rocketHideoutItems = PretRocketHideoutItems.LoadProject(projectRoot, shared: sharedMachines);
        var rocketHideoutPickups = PretRocketHideoutItems.LoadPickupsProject(projectRoot);
        var rocketHideoutTrainers = PretRocketHideoutTrainers.Load(canonicalPokemon, projectRoot);
        var celadonGym = PretCeladonGymRegion.Load(projectRoot, runtimeMaps);
        var celadonGymPokemon = PretCeladonGymPokemon.Load(canonicalPokemon, projectRoot, pokedexMenu: oakPokedex);
        var celadonGymItems = PretCeladonGymItems.LoadProject(projectRoot, shared: sharedMachines);
        var celadonGymTrainers = PretCeladonGymTrainers.Load(canonicalPokemon, projectRoot);
        var celadonApproach = PretCeladonApproachRegion.Load(projectRoot, runtimeMaps);
        var celadonApproachPokemon = PretCeladonApproachPokemon.Load(canonicalPokemon, projectRoot, pokedexMenu: oakPokedex);
        var celadonApproachItems = PretCeladonApproachItems.LoadProject(projectRoot, shared: sharedMachines);
        var celadonApproachEncounters = PretCeladonApproachEncounters.Load(projectRoot);
        var celadonApproachTrainers = PretCeladonApproachTrainers.Load(projectRoot);
        var rockTunnel = PretRockTunnelRegion.Load(projectRoot, runtimeMaps);
        var rockTunnelPokemon = PretRockTunnelPokemon.Load(canonicalPokemon, projectRoot, pokedexMenu: oakPokedex);
        var rockTunnelItems = PretRockTunnelItems.LoadProject(projectRoot, shared: sharedMachines);
        var rockTunnelEncounters = PretRockTunnelEncounters.Load(projectRoot);
        var rockTunnelTrainers = PretRockTunnelTrainers.Load(projectRoot);
        var route11 = PretRoute11Region.Load(projectRoot, runtimeMaps);
        var route11Pokemon = PretRoute11Pokemon.Load(canonicalPokemon, projectRoot, pokedexMenu: oakPokedex);
        var route11Items = PretRoute11Items.LoadProject(projectRoot, shared: sharedMachines);
        var route11Encounters = PretRoute11Encounters.Load(projectRoot);
        var route11Trainers = PretRoute11Trainers.Load(projectRoot);
        var cityServices = PretCityServices.Load(projectRoot, runtimeMaps, shared: sharedMachines);
        var gameCornerCoins = PretGameCornerCoins.Load(projectRoot, shared: sharedMachines);
        var oldRod = PretOldRod.Load(projectRoot);
        var vermilionGym = PretVermilionGymRegion.Load(projectRoot, runtimeMaps);
        var vermilionGymTrainers = PretVermilionGymTrainers.Load(canonicalPokemon, projectRoot);
        var vermilionGymAssets = PretVermilionGymAssets.Load(projectRoot, shared: sharedMachines);
        var ssAnne = PretSsAnneRegion.Load(projectRoot, runtimeMaps);
        var ssAnnePokemon = PretSsAnnePokemon.Load(canonicalPokemon, projectRoot, pokedexMenu: oakPokedex);
        var ssAnneTrainers = PretSsAnneTrainers.Load(projectRoot);
        var ssAnneItems = PretSsAnneItems.LoadProject(projectRoot, shared: sharedMachines);
        var vermilionPokemon = PretVermilionPokemon.Load(canonicalPokemon, projectRoot, pokedexMenu: oakPokedex);
        var vermilion = PretVermilionRegion.Load(projectRoot, runtimeMaps);
        var vermilionTrainers = PretVermilionTrainers.Load(projectRoot);
        var billPokemon = PretBillPokemon.Load(canonicalPokemon, projectRoot, pokedexMenu: oakPokedex);
        var bill = PretBillRegion.Load(projectRoot, runtimeMaps);
        var billItems = PretBillItems.LoadProject(projectRoot, shared: sharedMachines);
        var billTrainers = PretBillTrainers.Load(projectRoot);
        var cerulean = PretCeruleanRegion.Load(projectRoot, runtimeMaps);
        var mtMoon = PretMtMoonRegion.Load(projectRoot, runtimeMaps);
        var mtMoonPokemon = PretMtMoonPokemon.Load(canonicalPokemon, projectRoot, pokedexMenu: oakPokedex);
        var oakRating = PretOakRating.Load(projectRoot);
        var townMap = PretTownMap.Load(projectRoot, sharedItems);
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
            if (!maps.TryGetValue(new("map.saffron-city"), out var saffronCity) ||
                !JsonElement.DeepEquals(saffronCity.Events, saffronGym.ExternalSaffronEvents))
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

        var pokemonStorage = PretPokemonStorageSource.Load(projectRoot);
        var fieldPickups = PretEarlyPickups.Load(projectRoot);
        var storyRuntime = PretStoryRuntime.Load(projectRoot);
        var constants = storyRuntime.Compilation.Constants.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

        var pokemonContent = new CampaignPokemonContent(gamePokemon,
            gamePokemon.Catalog.Species.Values.ToImmutableSortedDictionary(species => species.SourceSpeciesId,
                species => oakPokedex.Entries[species.SourceSpeciesId].Name), storyRuntime.NicknameCharacters)
        { AllowSleepStatus = true, AllowFreezeStatus = true, AllowToxicStatus = true };

        var oldMan = PretOldManTutorial.LoadContent(canonicalPokemon, File.ReadAllText(Path.Combine(projectRoot, "content/generated/data/pokemon/route1/catalog.json"), Encoding.UTF8), File.ReadAllText(Path.Combine(projectRoot, "content/generated/data/services/old-man-tutorial.json"), Encoding.UTF8));
        var forestPokemon = PretForestPokemon.Load(canonicalPokemon, projectRoot);

        var pewterPokemon = PretPewterPokemon.Load(canonicalPokemon, projectRoot);

        {
            pokemonTowerPokemon = PretPokemonTowerPokemon.Load(canonicalPokemon, pokemonContent.Catalog, projectRoot);
            foreach (var(symbol, value)in pokemonTowerPokemon.Constants)
                constants[symbol] = value;
        }

        {
            snorlaxPokemon = PretSnorlaxPokemon.Load(canonicalPokemon, pokemonContent.Catalog, projectRoot);
            foreach (var(symbol, value)in snorlaxPokemon.Constants)
            {
                if (constants.TryGetValue(symbol, out var prior) && prior != value)
                    throw new InvalidDataException("Conflicting Snorlax Pokemon constant: " + symbol);
                constants[symbol] = value;
            }
        }

        var snorlaxCompositionPokemon = pokemonContent;
        {
            eeveePokemon = PretEeveePokemon.Load(canonicalPokemon, pokemonContent.Catalog, projectRoot);
            MergeRegionalServiceConstants(constants, eeveePokemon.Constants);
        }

        var regionalServiceCompositionPokemon = pokemonContent;
        {
            fuchsiaPokemon = PretFuchsiaRoutePokemon.Load(canonicalPokemon, pokemonContent.Catalog, projectRoot);
            MergeRegionalServiceConstants(constants, fuchsiaPokemon.Constants);
        }

        var fuchsiaRouteCompositionPokemon = pokemonContent;
        {
            safariPokemon = PretSafariPokemon.Load(canonicalPokemon, pokemonContent.Catalog, projectRoot);

            MergeRegionalServiceConstants(constants, safariPokemon.Constants);
        }

        {
            surfPokemon = PretSurfPokemon.Load(canonicalPokemon, gamePokemon.Catalog, projectRoot);

            surfArt = PretSurfArt.Load(projectRoot, projectRoot);
            surfEncounters = PretSurfEncounters.Load(projectRoot);
            MergeRegionalServiceConstants(constants, surfPokemon.Constants);
        }

        {
            silphPokemon = PretSilphPokemon.Load(canonicalPokemon, gamePokemon.Catalog, projectRoot);

            silphArt = PretSilphArt.Load(projectRoot, projectRoot, oakPokedex);
            MergeRegionalServiceConstants(constants, silphPokemon.Constants);
        }
        var mtMoonItems = PretMtMoonItems.LoadProject(projectRoot, shared: sharedMachines);
        var machines = sharedMachines.LoadCurrent(gamePokemon);
        CampaignBattleContent battleContent;
        {
            var trainers = PretLabTrainerCatalog.Load(File.ReadAllText(Path.Combine(projectRoot, "content/generated/data/trainer-battles/lab/lab-trainers.json"), Encoding.UTF8), SharedTrainerCatalog.LoadProject(projectRoot));
            var names = storyRuntime.MoveNames.ToBuilder();
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
            foreach (var (move, name) in canonicalPokemon.MoveCatalog.MoveNames)
                if (!names.TryGetValue(move.Value, out var displayed) || displayed != name)
                    throw new InvalidDataException("Battle move name differs from the shared move catalog: " + move.Value);

            battleContent = new(trainers, names.ToImmutable());
        }

        OpeningItemsCatalog openingItems;
        openingItems = PretOpeningItems.Load(File.ReadAllText(Path.Combine(projectRoot, "content/generated/data/items/opening/opening-items.json"), Encoding.UTF8), sharedItems);

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
            questItems = PretQuestItems.Load(File.ReadAllText(Path.Combine(projectRoot, "content/generated/data/items/quest/quest-items.json"), Encoding.UTF8), sharedItems);
            var teachyTvItem = sharedItems.Definition(QuestItemRules.TeachyTvId);
            questItems = questItems with { Items = questItems.Items.Add(teachyTvItem).Add(townMap.Item) };
            QuestItemRules.ValidateCatalog(questItems);
            constants[QuestItemRules.TeachyTvSymbol] = teachyTvItem.SourceItemId;
            constants[QuestItemRules.TownMapSymbol] = townMap.Item.SourceItemId;

            metatiles = PretMartMetatiles.Load(projectRoot);
            constants["METATILE_Mart_CounterMid_Top"] = metatiles.OnLoadPatches.Single(x => x.Y == 3).RawMetatileId;
            constants["METATILE_Mart_CounterMid_Bottom"] = metatiles.OnLoadPatches.Single(x => x.Y == 4).RawMetatileId;
        }

        var earlyShop = PretEarlyShop.LoadPresentation(projectRoot, CurrentItemCatalog.Load(projectRoot, sharedItems));

        // The importer compiled source syntax, edition branches, menus and fallthrough.
        var compiled = storyRuntime.Compilation;
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
            pokemonContent = pokemonContent with { StarterChoices = choices.OrderBy(x => x.Index).ToImmutableArray() };
        }

        var bank = storyRuntime.TextBank.ToBuilder();
        var repel = PretEarlyRepel.Load(File.ReadAllText(Path.Combine(projectRoot, "content/generated/data/services/early-repel.json"), Encoding.UTF8));
        {
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
        ValidateFuchsiaGymComposition(fuchsiaGymCompositionMaps, fuchsiaRouteCompositionMaps, fuchsiaGymTrainers, compiled);
        ValidateFirstSeviiComposition(firstSevii, viridianGymPredecessorMaps, firstSeviiPredecessorMaps, gamePokemon, gamePokemon.Opponents, firstSeviiTrainers, compiled, bank);
        ValidateViridianGymComposition(viridianGym, victoryRoadPredecessorMaps, viridianGymPredecessorMaps, viridianGymTrainers, compiled, machines, gamePokemon);
        {
            ValidateLeagueRivalComposition(victoryRoadPredecessorMaps, leagueRivalTrainers, compiled, bank, pokemonContent, gamePokemon, gamePokemon.Opponents);
        }

        ValidateVictoryRoadComposition(victoryRoad, pokemonLeaguePredecessorMaps, victoryRoadPredecessorMaps, victoryRoadTrainers, gamePokemon, compiled);

        ValidatePokemonLeagueComposition(pokemonLeague, seviiRubyPredecessorMaps, pokemonLeaguePredecessorMaps, pokemonLeagueTrainers, gamePokemon.Opponents, gamePokemon, compiled, bank);

        SeviiRubyItemContent seviiRubyItems;
        {

            seviiRubyItems = PretSeviiRubyItems.Load(seviiRuby, gamePokemon, earlyShop.Catalog, machines, compiled, sharedMachines);

            ValidateSeviiRubyComposition(seviiRuby, fourIslandPredecessorMaps, seviiRubyPredecessorMaps, gamePokemon, gamePokemon.Opponents, seviiRubyTrainers, compiled, pokemonContent, seviiRubyItems);
        }

        FourIslandItemContent fourIslandItems;
        {

            fourIslandItems = PretFourIslandItems.Load(fourIsland, gamePokemon, earlyShop.Catalog, machines, compiled);

            ValidateFourIslandComposition(sixIslandPredecessorMaps, fourIslandTrainers, compiled);
        }

        SixIslandItemContent sixIslandItems;
        {

            sixIslandItems = PretSixIslandItems.Load(sixIsland, gamePokemon, earlyShop.Catalog, machines, compiled, projectRoot);

            ValidateSixIslandComposition(fiveIslandPredecessorMaps, sixIslandTrainers, compiled);
        }

        FiveIslandItemContent fiveIslandItems;
        {

            fiveIslandItems = PretFiveIslandItems.Load(fiveIsland, gamePokemon, earlyShop.Catalog, machines, compiled, projectRoot);

            ValidateFiveIslandComposition(maps, fiveIslandTrainers, compiled, fiveIslandItems, true);
        }

        CelioPokemonContent celioPokemon;
        ImmutableDictionary<MapId, RoamerMap> celioRoamerMaps;
        {
            celioRoamerMaps = PretCelioMapNumbers.Load(mapNumbers, maps);
            celioPokemon = new(gamePokemon, gamePokemon.Opponents, machines);

            ValidateCelioComposition(maps, compiled, pokemonContent, celioPokemon, gamePokemon, gamePokemon.Opponents, fiveIslandItems.Machines);
        }

        // Validate the original Celio composition before extending the sole current game.
        var celioMaps = maps;
        maps = maps.AddRange(ceruleanCave.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendCeruleanCave(mapNumbers, maps, celioRoamerMaps);
        var ceruleanCavePokemon = new CeruleanCavePokemonContent(gamePokemon, gamePokemon.Opponents, machines);

        ValidateCeruleanCaveComposition(ceruleanCave, maps, celioMaps, compiled, pokemonContent, ceruleanCavePokemon, celioPokemon);

        // The same current game retains original League teams and activates the source rematches after Celio.
        var leagueRematchTrainers = PretLeagueRematchRules.LoadTrainers(projectRoot);

        ValidateLeagueRematchComposition(leagueRematchTrainers, gamePokemon.Opponents, ceruleanCavePokemon, compiled, bank);

        maps = maps.AddRange(sevenIsland.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendSevenIsland(mapNumbers, maps, celioRoamerMaps, sevenIsland.NewMaps);

        var sevenTrainers = PretSevenIslandRules.LoadTrainers(projectRoot, shared: sharedMachines);

        var sevenItems = PretSevenIslandRules.LoadItemsDocument(projectRoot, shared: sharedMachines);

        ValidateSevenIslandComposition(sevenIsland, sevenTrainers, sevenItems, compiled);

        maps = maps.AddRange(eventIsland.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendEventIslands(mapNumbers, maps, celioRoamerMaps, eventIsland.NewMaps);

        maps=maps.AddRange(powerPlant.NewMaps);
        celioRoamerMaps=PretCelioMapNumbers.ExtendPowerPlant(mapNumbers,maps,celioRoamerMaps,powerPlant.NewMaps);

        maps = maps.AddRange(trainerTower.Maps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendTrainerTower(mapNumbers, maps, celioRoamerMaps, trainerTower.Maps);
        var trainerTowerCatalog = PretTrainerTowerRules.Load(projectRoot);

        ValidateTrainerTowerComposition(trainerTower, compiled, trainerTowerCatalog, earlyShop.Catalog);

        var dojoPreviousMaps = maps;
        maps = maps.AddRange(fightingDojo.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendFightingDojo(mapNumbers, maps, celioRoamerMaps, fightingDojo.NewMaps);

        var dojoTrainers = PretFightingDojoRules.LoadTrainers(projectRoot);

        ValidateFightingDojoComposition(fightingDojo, maps, dojoPreviousMaps, compiled);

        var fossilLabPreviousMaps = maps;
        maps = maps.AddRange(fossilLab.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendFossilLab(mapNumbers, maps, celioRoamerMaps, fossilLab.NewMaps);

        ValidateFossilLabComposition(fossilLab, maps, fossilLabPreviousMaps, compiled);
        maps = maps.AddRange(npcTradeElyssa.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(mapNumbers, maps, celioRoamerMaps, npcTradeElyssa.NewMaps);
        maps = maps.AddRange(npcTradeHaden.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(mapNumbers, maps, celioRoamerMaps, npcTradeHaden.NewMaps);
        var npcTradePreviousMaps = maps;
        maps = maps.AddRange(npcTrades.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(mapNumbers, maps, celioRoamerMaps, npcTrades.NewMaps);
        ValidateNpcTradeComposition(npcTrades, maps, npcTradePreviousMaps, compiled);
        var goodRodPreviousMaps = maps;
        maps = maps.AddRange(goodRodService.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(mapNumbers, maps, celioRoamerMaps, goodRodService.NewMaps);
        ValidateGoodRodComposition(goodRodService, maps, goodRodPreviousMaps, compiled);

        var seafoamPreviousMaps = maps;
        maps = maps.AddRange(seafoam.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(mapNumbers, maps, celioRoamerMaps, seafoam.NewMaps);
        _ = PretPokeDollItems.LoadSourceContract(projectRoot);

        ValidateSeafoamComposition(seafoam, maps, seafoamPreviousMaps, compiled);
        var reminderPreviousMaps = maps;
        maps = maps.AddRange(moveReminderHouse.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(mapNumbers, maps, celioRoamerMaps, moveReminderHouse.NewMaps);
        ValidateMoveReminderComposition(moveReminderHouse, maps, reminderPreviousMaps, compiled);
        var deleterPreviousMaps = maps;
        maps = maps.AddRange(moveDeleterHouse.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(mapNumbers, maps, celioRoamerMaps, moveDeleterHouse.NewMaps);
        ValidateSourceHouseComposition("move_deleter", moveDeleterHouse.NewMaps, maps, deleterPreviousMaps, compiled,
            "map.fuchsia-city", 10, "data/maps/FuchsiaCity_House3/scripts.inc", "FuchsiaCity_House3_EventScript_MoveDeleter");
        foreach (var (mapId, townId, townWarp, scriptPath, entry) in new[] {
            (PretOriginalTutorRooms.BodySlamMapId, "map.four-island", 2, "data/maps/FourIsland_House1/scripts.inc", "FourIsland_House1_EventScript_BodySlamTutor"),
            (PretOriginalTutorRooms.MetronomeMapId, "map.cinnabar-island-pokemon-lab-entrance", 4, "data/maps/CinnabarIsland_PokemonLab_ResearchRoom/scripts.inc", "CinnabarIsland_PokemonLab_ResearchRoom_EventScript_MetronomeTutor") })
        {
            var previous = maps;
            var room = ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(mapId, originalTutorRooms.NewMaps[mapId]);
            maps = maps.AddRange(room);
            celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(mapNumbers, maps, celioRoamerMaps, room);
            ValidateSourceHouseComposition("original_tutor_room", room, maps, previous, compiled, townId, townWarp, scriptPath, entry);
        }
        var copycatPreviousMaps = maps;
        maps = maps.AddRange(copycatHouse.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(mapNumbers, maps, celioRoamerMaps, copycatHouse.NewMaps);
        ValidateCopycatHouseComposition(copycatHouse, maps, copycatPreviousMaps, compiled);
        var superRodPreviousMaps = maps;
        maps = maps.AddRange(superRodService.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(mapNumbers, maps, celioRoamerMaps, superRodService.NewMaps);
        ValidateSuperRodComposition(superRodService, maps, superRodPreviousMaps, compiled);

        var vsSeekerItem = vsSeekerPresentation.ItemDefinition.Deserialize<EarlyShopItemDefinition>(sharedItems.CreateJsonOptions())
            ?? throw new InvalidDataException("vs_seeker_item_missing");

        var memorialTrainers = PretMemorialPillarTrainers.Load(originalTmAcquisition.MemorialTrainers);

        var gameCornerPrizes = PretGameCornerPrizes.LoadCatalog(projectRoot, gamePokemon, earlyShop.Catalog, machines, shared: sharedMachines);
        var capeBrinkPreviousMaps = maps;
        maps = maps.AddRange(capeBrinkTutor.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(mapNumbers, maps, celioRoamerMaps, capeBrinkTutor.NewMaps);
        ValidateCapeBrinkComposition(capeBrinkTutor, maps, capeBrinkPreviousMaps, compiled);
        var prizeRoom = PretGameCornerPrizeRoom.Load(regionalServices, maps);
        maps = maps.AddRange(prizeRoom.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(mapNumbers, maps, celioRoamerMaps, prizeRoom.NewMaps);
        route16FlyHouse.ValidateReusedMaps(maps);
        maps = maps.AddRange(route16FlyHouse.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(mapNumbers, maps, celioRoamerMaps, route16FlyHouse.NewMaps);
        maps = maps.AddRange(cyclingRoad.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(mapNumbers, maps, celioRoamerMaps, cyclingRoad.NewMaps);
        var daycarePreviousMaps = maps;
        maps = maps.AddRange(route5DayCare.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(mapNumbers, maps, celioRoamerMaps, route5DayCare.NewMaps);
        ValidateRoute5DayCareComposition(route5DayCare, maps, daycarePreviousMaps, compiled);
        var fourDaycarePreviousMaps = maps;
        maps = maps.AddRange(fourIslandDayCare.NewMaps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(mapNumbers, maps, celioRoamerMaps, fourIslandDayCare.NewMaps);
        ValidateFourIslandDayCareComposition(fourIslandDayCare, maps, fourDaycarePreviousMaps, compiled);
        maps = originalTmAcquisition.MountMaps(maps);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(mapNumbers, maps, celioRoamerMaps, originalTmAcquisition.NewMaps);
        schoolHouseInteriors.ValidateReusedMaps(maps);
        var schoolHousePreviousMaps = maps;
        maps = maps.AddRange(schoolHouseInteriors.NewMaps);
        if (maps.Count != schoolHousePreviousMaps.Count + schoolHouseInteriors.NewMaps.Count || schoolHousePreviousMaps.Any(p => !ReferenceEquals(p.Value, maps[p.Key])))
            throw new InvalidDataException("school_house_map_composition");
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(mapNumbers, maps, celioRoamerMaps, schoolHouseInteriors.NewMaps);
        lostCave.ValidateReusedMaps(maps);
        maps = maps.AddRange(lostCave.NewMaps);
        ValidateLostCaveScripts(lostCave, compiled);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(mapNumbers, maps, celioRoamerMaps, lostCave.NewMaps);
        var trainerFanClubPreviousMaps = maps;
        maps = maps.AddRange(trainerFanClub.NewMaps);
        ValidateTrainerFanClubComposition(trainerFanClub, maps, trainerFanClubPreviousMaps, compiled);
        celioRoamerMaps = PretCelioMapNumbers.ExtendAuthenticatedMaps(mapNumbers, maps, celioRoamerMaps, trainerFanClub.NewMaps);
        ValidateCapeBrinkEncounterSources(capeBrinkTutor, pokemonContent.Catalog);
        var moveReminder = PretMoveReminderService.Load(projectRoot, pokemonContent.Catalog);
        var moveTutors = PretMoveTutors.Load(projectRoot, pokemonContent.Catalog);

        // Change this explicit version only when the saved content model changes.
        const string version = "firered-current-content-v4";
        var result = new PretCampaignCatalog(
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
            silph: silph,
            silphPokemon: silphPokemon,
            silphItems: silphItems,
            silphTrainers: silphTrainers,
            silphArt: silphArt,
            saffronGym: saffronGym,
            saffronGymTrainers: saffronGymTrainers,
            saffronGymItems: saffronGymItems,
            saffronGymPokemon: saffronGymPokemon,
            saffronGymArt: saffronGymArt,
            cinnabar: cinnabar,
            cinnabarPokemon: cinnabarPokemon,
            cinnabarTrainers: cinnabarTrainers,
            cinnabarItems: cinnabarItems,
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
            nationalDexEnabled: true,
            seviiRuby: seviiRuby,
            seviiRubyTrainers: seviiRubyTrainers,
            seviiRubyItems: seviiRubyItems,
            fourIsland: fourIsland,
            fourIslandTrainers: fourIslandTrainers,
            fourIslandItems: fourIslandItems,
            sixIsland: sixIsland,
            sixIslandTrainers: sixIslandTrainers,
            sixIslandItems: sixIslandItems,
            fiveIsland: fiveIsland,
            fiveIslandTrainers: fiveIslandTrainers,
            fiveIslandItems: fiveIslandItems,
            celioPokemon: celioPokemon,
            celioRoamerMaps: celioRoamerMaps,
            ceruleanCave: ceruleanCave,
            ceruleanCavePokemon: ceruleanCavePokemon,
            leagueRematchTrainers: leagueRematchTrainers,
            sevenIsland: sevenIsland, sevenTrainers: sevenTrainers, sevenItems: sevenItems, moveTutors: moveTutors, eventIsland: eventIsland, powerPlant: powerPlant, trainerTower: trainerTower, trainerTowerCatalog: trainerTowerCatalog,
            fightingDojo: fightingDojo, dojoTrainers: dojoTrainers, fossilLab: fossilLab, npcTrades: npcTrades, npcTradeElyssa: npcTradeElyssa, npcTradeHaden: npcTradeHaden, npcTradeMail: npcTradeMail, goodRodService: goodRodService, seafoam: seafoam, seafoamTrainers: seafoamTrainers, moveReminderHouse: moveReminderHouse, moveReminder: moveReminder, moveDeleterHouse: moveDeleterHouse, originalTutorRooms: originalTutorRooms, copycatHouse: copycatHouse, superRodService: superRodService, capeBrinkTutor: capeBrinkTutor, fieldFly: fieldFly, flyMapPresentation: flyMapPresentation, route16FlyHouse: route16FlyHouse, route5DayCare: route5DayCare, fourIslandDayCare: fourIslandDayCare, originalTmAcquisition: originalTmAcquisition, schoolHouseInteriors: schoolHouseInteriors, lostCave: lostCave, lostCaveTrainers: lostCaveTrainers, trainerFanClub: trainerFanClub, cyclingRoad: cyclingRoad, canonicalPokemon: canonicalPokemon, fishingDefinitions: PretOrdinaryFishing.Load(projectRoot)) { MailPresentation = mailPresentation, MailComposer = mailComposer.Catalog, TeachyTv = teachyTv, VsSeeker = vsSeeker.Catalog, VsSeekerTexts = vsSeekerPresentation.EncodedTexts, VsSeekerTrainers = vsSeekerTrainers, VsSeekerBattleOccurrences = vsSeekerOccurrences, VsSeekerOpponents = gamePokemon.Opponents, FameChecker = fameChecker.Catalog, GameCornerSlots = SlotMachineSessionContract.CreateApprovedProduction(slotRules), ItemfinderDocument = itemfinder, FieldRockSmash = fieldRockSmash, MemorialPillarTrainers = memorialTrainers, MemorialPillarOpponents = gamePokemon.Opponents };
        result.BindPokedexAides();
        result.BindCyclingRoadSceneInteriors();
        return result;
    }

    private static void ValidateCeruleanCaveComposition(PretCeruleanCaveRegion region,
        ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, PretWorldMap> previous,
        PretStoryCompilation scripts, CampaignPokemonContent pokemon, CeruleanCavePokemonContent cave, CelioPokemonContent celio)
    {
        if (region.NewMaps.Any(pair => previous.ContainsKey(pair.Key) || !maps.ContainsKey(pair.Key)))
            throw new InvalidDataException("cerulean_cave_map_collision");
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
        PokemonFactory.ValidateDefinitions(cave.Owned.Catalog);
    }
}

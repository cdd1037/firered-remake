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
    public static PretCampaignCatalog LoadCurrent(string projectRoot) => new(projectRoot);

    private PretCampaignCatalog(string projectRoot)
    {
        var world = PretCurrentWorld.Load(projectRoot);
        QuestLogSource = PretQuestLogSource.Load(projectRoot);
        var canonicalPokemon = PretCanonicalPokemonCatalog.Load(projectRoot, projectRoot);
        var sharedItems = SharedItemCatalog.LoadProject(projectRoot);
        DisplayNames = SourceDisplayNames.LoadProject(projectRoot, sharedItems, canonicalPokemon.Catalog);
        var sharedMachines = SharedMachineCatalog.LoadProject(projectRoot, sharedItems);
        // Resource readers validate their formats; no original-file identity pass is required.
        var mapNumbers = PretCelioMapNumbers.LoadSourceTable(projectRoot);
        var eventIsland = PretEventIslandRegion.Load(world);
        var powerPlant = PretPowerPlantRegion.Load(world);
        var trainerTower = PretTrainerTowerRegion.Load(world);
        var fightingDojo = PretFightingDojoRegion.Load(world);
        var fossilLab = PretFossilLabRegion.Load(world);
        var itemfinder = PretItemfinderSource.Load(projectRoot);
        var teachyTv = PretTeachyTv.Load(File.ReadAllText(Path.Combine(projectRoot, "content/generated/data/services/teachy-tv.json")));
        var fameChecker = PretFameChecker.Load(projectRoot);
        var vsSeeker = PretVsSeeker.Load(projectRoot);
        var vsSeekerPresentation = PretVsSeekerPresentation.Load(projectRoot);
        var vsSeekerTrainers = PretVsSeekerParties.Load(projectRoot);
        var vsSeekerOccurrences = PretVsSeekerParties.LoadOccurrences(projectRoot);
        var slotRules = SlotMachineRules.FromAuthenticatedJson(File.ReadAllBytes(Path.Combine(projectRoot, "content/generated/data/services/slot-machine/rules.json")));
        var sevenIsland = PretSevenIslandRegion.Load(world);
        var ceruleanCave = PretCeruleanCaveRegion.Load(world);
        var fiveIsland = PretFiveIslandRegion.Load(world);
        var fiveIslandTrainers = PretFiveIslandRules.LoadTrainers(projectRoot);
        var sixIsland = PretSixIslandRegion.Load(world);
        var sixIslandTrainers = PretSixIslandRules.LoadTrainers(projectRoot);
        var fourIsland = PretFourIslandRegion.Load(world);
        var fourIslandTrainers = PretFourIslandRules.LoadTrainers(projectRoot);
        var seviiRuby = PretSeviiRubyRegion.Load(world);
        var seviiRubyTrainers = PretSeviiRubyRules.LoadTrainers(projectRoot);
        var twins = PretRoute8Twins.Load(projectRoot);
        var doubleMechanics = SourceMechanicsCatalogLoader.LoadAuthenticated(canonicalPokemon, projectRoot, sharedItems);
        var npcTrades = PretNpcTrades.Load(world, doubleMechanics.Catalog);
        var gamePokemon = PretGamePokemonCatalog.Load(projectRoot, canonicalPokemon, doubleMechanics, npcTrades.Trades, sharedMachines);
        var npcTradeElyssa = PretNpcTradeElyssa.Load(world);
        var npcTradeHaden = PretNpcTradeHaden.Load(world);
        var npcTradeMail = PretNpcTradeMail.Load(projectRoot);
        var mailComposer = PretMailComposer.Load(projectRoot);
        var mailPresentation = PretMailPresentation.Load(projectRoot, items: sharedItems);
        var goodRodService = PretGoodRodService.Load(world);
        var superRodService = PretSuperRodService.Load(world);
        var seafoam = PretSeafoamRegion.Load(world);
        var seafoamTrainers = PretSeafoamRules.LoadTrainers(projectRoot);
        var moveReminderHouse = PretMoveReminderHouse.Load(world);
        var moveDeleterHouse = PretMoveDeleterHouse.Load(world);
        var originalTutorRooms = PretOriginalTutorRooms.Load(world);
        var copycatHouse = PretCopycatHouse.Load(world);
        var capeBrinkTutor = PretCapeBrinkTutor.Load(world);
        var route16FlyHouse = PretRoute16FlyHouse.Load(world);
        var cyclingRoad = PretCyclingRoadRegion.Load(world);
        var route5DayCare = PretRoute5DayCare.Load(world);
        var fourIslandDayCare = PretFourIslandDayCare.Load(world);
        var originalTmAcquisition = PretOriginalTmAcquisition.Load(world);
        var schoolHouseInteriors = PretSchoolHouseInteriors.Load(world);
        var lostCave = PretLostCaveRegion.Load(world);
        var trainerFanClub = PretTrainerFanClub.Load(world);
        var lostCaveTrainers = LostCaveTrainerPartyFactory.Authenticate(vsSeekerTrainers, SharedTrainerCatalog.LoadProject(projectRoot).Select(LostCaveTrainerPartyFactory.AllTrainers));
        var fieldFly = PretFieldFly.Load(projectRoot, shared: sharedMachines);
        var fieldRockSmash = PretFieldRockSmash.Load(projectRoot);
        var flyMapPresentation = PretFlyMap.Load(projectRoot);
        var doubleItems = doubleMechanics.Catalog.BattleItems ?? throw new InvalidDataException("Preset doubles requires the authenticated battle item catalog.");
        var magikarp = PretMagikarpContract.Load(projectRoot);
        var route22 = PretRoute22Region.Load(world);
        var route3 = PretRoute3Region.Load(world);
        var oakPokedex = PretPokedexMenu.Load(projectRoot);
        var route22Pokemon = PokemonPresentationPackage.Load(canonicalPokemon, projectRoot, "route22", pokedexMenu: oakPokedex);
        var route3Pokemon = PokemonPresentationPackage.Load(canonicalPokemon, projectRoot, "route3", pokedexMenu: oakPokedex);
        var ceruleanPokemon = PokemonPresentationPackage.Load(canonicalPokemon, projectRoot, "cerulean", pokedexMenu: oakPokedex);
        var pokemonTower = PretPokemonTowerRegion.Load(world);
        var snorlax = PretSnorlaxRegion.Load(world);
        var snorlaxArt = PretSnorlaxArt.Load(projectRoot, pokedexMenu: oakPokedex);
        var regionalServices = PretRegionalServiceMaps.Load(world);
        var regionalGift = PretRegionalGiftContract.Load(projectRoot, sharedItems);
        var eeveeArt = PretEeveeArt.Load(projectRoot, pokedexMenu: oakPokedex);
        var departmentStore = PretDepartmentStoreContract.Load(projectRoot);
        var nameRater = PretNameRaterContract.Load(projectRoot);
        var fuchsiaRoute = PretFuchsiaRouteRegion.Load(world);
        var fuchsiaGym = PretFuchsiaGymRegion.Load(world);
        var fuchsiaGymTrainers = PretFuchsiaGymTrainers.LoadDocument(canonicalPokemon, projectRoot);
        var fuchsiaGymItems = PretFuchsiaGymItems.LoadProject(projectRoot, shared: sharedMachines);
        var fuchsiaGymMoveSource = canonicalPokemon.MoveCatalog;
        var safari = PretSafariRegion.Load(world);
        var safariEncounters = PretSafariEncounters.Load(safari);
        var safariItems = PretSafariItems.LoadProject(projectRoot);
        var safariArt = PretSafariArt.Load(projectRoot);

        PretSurfArt surfArt;
        PretSurfEncounters surfEncounters;

        var viridianGym = PretViridianGymRegion.Load(world);
        var viridianGymTrainers = PretViridianGymRules.LoadTrainers(projectRoot);
        var leagueRivalTrainers = PretLeagueRivalRules.LoadTrainers(projectRoot);
        var victoryRoad = PretVictoryRoadRegion.Load(world);
        var victoryRoadTrainers = PretVictoryRoadRules.LoadTrainers(projectRoot);
        var victoryRoadItems = PretVictoryRoadItems.LoadProject(projectRoot);
        var pokemonLeague = PretPokemonLeagueRegion.Load(world);
        var pokemonLeagueTrainers = PretPokemonLeagueRules.LoadTrainers(projectRoot);
        var firstSevii = PretFirstSeviiRegion.Load(world);
        var firstSeviiTrainers = PretFirstSeviiRules.LoadTrainers(projectRoot, shared: sharedMachines);
        var firstSeviiItems = PretFirstSeviiRules.LoadItems(projectRoot, shared: sharedMachines);
        var firstSeviiPresentation = PretFirstSeviiPresentation.LoadProject(projectRoot, projectRoot);
        var cinnabar = PretCinnabarRegion.Load(world);
        var cinnabarTrainers = PretCinnabarTrainers.Load(projectRoot);
        var cinnabarItems = PretCinnabarItems.LoadProject(projectRoot);

        var saffronGym = PretSaffronGymRegion.Load(world);
        var saffronGymTrainers = PretSaffronGymTrainers.Load(canonicalPokemon, projectRoot);
        var saffronGymArt = PretSaffronGymArt.Load(projectRoot, projectRoot, pokedexMenu: oakPokedex);
        var cinnabarArt = PretCinnabarArt.Load(projectRoot, projectRoot, pokedexMenu: oakPokedex);
        var cinnabarPresentation = PretCinnabarPresentation.Load(projectRoot);
        var saffronGymItems = PretSaffronGymItems.LoadProject(projectRoot);

        var silph = PretSilphRegion.Load(world);
        var silphItems = PretSilphItems.LoadProject(projectRoot);
        var silphTrainers = PretSilphTrainers.Load(canonicalPokemon, projectRoot);

        PretSilphArt silphArt;
        var fuchsiaArt = PretFuchsiaRouteArt.Load(projectRoot, pokedexMenu: oakPokedex);
        var pokemonTowerItems = PretPokemonTowerItems.LoadProject(projectRoot, shared: sharedMachines);
        var pokemonTowerPickups = PretPokemonTowerItems.LoadPickupsProject(projectRoot);
        var pokemonTowerTrainers = PretPokemonTowerTrainers.Load(canonicalPokemon, projectRoot);
        var rocketHideout = PretRocketHideoutRegion.Load(world);
        var rocketHideoutPokemon = PokemonPresentationPackage.Load(canonicalPokemon, projectRoot, "rocket-hideout", pokedexMenu: oakPokedex);
        var rocketHideoutItems = PretRocketHideoutItems.LoadProject(projectRoot, shared: sharedMachines);
        var rocketHideoutPickups = PretRocketHideoutItems.LoadPickupsProject(projectRoot);
        var rocketHideoutTrainers = PretRocketHideoutTrainers.Load(canonicalPokemon, projectRoot);
        var celadonGym = PretCeladonGymRegion.Load(world);
        var celadonGymPokemon = PokemonPresentationPackage.Load(canonicalPokemon, projectRoot, "celadon-gym", pokedexMenu: oakPokedex);
        var celadonGymItems = PretCeladonGymItems.LoadProject(projectRoot, shared: sharedMachines);
        var celadonGymTrainers = PretCeladonGymTrainers.Load(canonicalPokemon, projectRoot);
        var celadonApproach = PretCeladonApproachRegion.Load(world);
        var celadonApproachPokemon = PokemonPresentationPackage.Load(canonicalPokemon, projectRoot, "celadon-approach", pokedexMenu: oakPokedex);
        var celadonApproachItems = PretCeladonApproachItems.LoadProject(projectRoot, shared: sharedMachines);
        var celadonApproachEncounters = PretCeladonApproachEncounters.Load(projectRoot);
        var celadonApproachTrainers = PretCeladonApproachTrainers.Load(projectRoot);
        var rockTunnel = PretRockTunnelRegion.Load(world);
        var rockTunnelPokemon = PokemonPresentationPackage.Load(canonicalPokemon, projectRoot, "rocktunnel", pokedexMenu: oakPokedex);
        var rockTunnelItems = PretRockTunnelItems.LoadProject(projectRoot, shared: sharedMachines);
        var rockTunnelEncounters = PretRockTunnelEncounters.Load(projectRoot);
        var rockTunnelTrainers = PretRockTunnelTrainers.Load(projectRoot);
        var route11 = PretRoute11Region.Load(world);
        var route11Pokemon = PokemonPresentationPackage.Load(canonicalPokemon, projectRoot, "route11", pokedexMenu: oakPokedex);
        var route11Items = PretRoute11Items.LoadProject(projectRoot, shared: sharedMachines);
        var route11Encounters = PretRoute11Encounters.Load(projectRoot);
        var route11Trainers = PretRoute11Trainers.Load(projectRoot);
        var cityServices = PretCityServices.Load(world, shared: sharedMachines);
        var gameCornerCoins = PretGameCornerCoins.Load(projectRoot, shared: sharedMachines);
        var oldRod = PretOldRod.Load(projectRoot);
        var vermilionGym = PretVermilionGymRegion.Load(world);
        var vermilionGymTrainers = PretVermilionGymTrainers.Load(canonicalPokemon, projectRoot);
        var ssAnne = PretSsAnneRegion.Load(world);
        var ssAnnePokemon = PokemonPresentationPackage.Load(canonicalPokemon, projectRoot, "ssanne", pokedexMenu: oakPokedex);
        var ssAnneTrainers = PretSsAnneTrainers.Load(projectRoot);
        var ssAnneItems = PretSsAnneItems.LoadProject(projectRoot, shared: sharedMachines);
        var vermilionPokemon = PokemonPresentationPackage.Load(canonicalPokemon, projectRoot, "vermilion", pokedexMenu: oakPokedex);
        var vermilion = PretVermilionRegion.Load(world);
        var vermilionTrainers = PretVermilionTrainers.Load(projectRoot);
        var billPokemon = PokemonPresentationPackage.Load(canonicalPokemon, projectRoot, "bill", pokedexMenu: oakPokedex);
        var bill = PretBillRegion.Load(world);
        var billItems = PretBillItems.LoadProject(projectRoot, shared: sharedMachines);
        var billTrainers = PretBillTrainers.Load(projectRoot);
        var cerulean = PretCeruleanRegion.Load(world);
        var mtMoon = PretMtMoonRegion.Load(world);
        var mtMoonPokemon = PokemonPresentationPackage.Load(canonicalPokemon, projectRoot, "mtmoon", pokedexMenu: oakPokedex);
        var oakRating = PretOakRating.Load(projectRoot);
        var townMap = PretTownMap.Load(projectRoot, sharedItems);
        // All ordinary maps come from the authoritative current world index.
        var maps = world.Maps;
        var region = PretViridianRegion.Load(world);

        var center = PretViridianCenter.Load(world);

        var forest = PretForestRegion.Load(world);

        var pewter = PretPewterRegion.Load(world);

        regionalServices.ValidateReusedMaps(maps);

        fuchsiaRoute.ValidateReusedMaps(maps);

        safari.ValidateReusedMaps(maps);

        {
            if (!maps.TryGetValue(new("map.saffron-city"), out var saffronCity) ||
                !JsonElement.DeepEquals(saffronCity.Events, saffronGym.ExternalSaffronEvents))
                throw new InvalidDataException("saffron_gym_city_anchor");

        }









        var pokemonStorage = PretPokemonStorageSource.Load(projectRoot);
        var fieldPickups = PretEarlyPickups.Load(projectRoot);
        var storyRuntime = PretStoryRuntime.Load(projectRoot);

        var pokemonContent = new CampaignPokemonContent(gamePokemon,
            gamePokemon.Catalog.Species.Values.ToImmutableSortedDictionary(species => species.SourceSpeciesId,
                species => oakPokedex.Entries[species.SourceSpeciesId].Name), storyRuntime.NicknameCharacters)
        { AllowSleepStatus = true, AllowFreezeStatus = true, AllowToxicStatus = true };

        var oldMan = PretOldManTutorial.LoadContent(canonicalPokemon, File.ReadAllText(Path.Combine(projectRoot, "content/generated/data/pokemon/route1/catalog.json"), Encoding.UTF8), File.ReadAllText(Path.Combine(projectRoot, "content/generated/data/services/old-man-tutorial.json"), Encoding.UTF8));
        var forestPokemon = PretForestPokemon.Load(canonicalPokemon, projectRoot);

        var pewterPokemon = PretPewterPokemon.Load(canonicalPokemon, projectRoot);

        var fuchsiaTrainers = PretFuchsiaRouteTrainers.Load(projectRoot);
        surfArt = PretSurfArt.Load(projectRoot, projectRoot);
        surfEncounters = PretSurfEncounters.Load(projectRoot);
        silphArt = PretSilphArt.Load(projectRoot, projectRoot, oakPokedex);
        var mtMoonItems = PretMtMoonItems.LoadProject(projectRoot, shared: sharedMachines);
        var machines = sharedMachines.LoadCurrent(gamePokemon);
        GamePokemon = gamePokemon;
        OpponentPokemon = gamePokemon.Opponents;
        Machines = machines;
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

        QuestItemsCatalog questItems;
        PretMartMetatiles metatiles;
        {
            questItems = PretQuestItems.Load(File.ReadAllText(Path.Combine(projectRoot, "content/generated/data/items/quest/quest-items.json"), Encoding.UTF8), sharedItems);
            var teachyTvItem = sharedItems.Definition(QuestItemRules.TeachyTvId);
            questItems = questItems with { Items = questItems.Items.Add(teachyTvItem).Add(townMap.Item) };
            QuestItemRules.ValidateCatalog(questItems);

            metatiles = PretMartMetatiles.Load(projectRoot);
        }

        var earlyShop = PretEarlyShop.LoadPresentation(projectRoot, CurrentItemCatalog.Load(projectRoot, sharedItems));
        if (new[] { "pret:ITEM_TM16", "pret:ITEM_TM20", "pret:ITEM_TM33" }.Any(id => MachineRules.Definition(machines, new(id)) is null) ||
            !earlyShop.Catalog.Items.Any(item => item.Id == new ItemId("pret:ITEM_LEMONADE")))
            throw new InvalidDataException("department_rooftop_item_bindings_missing");

        // The importer compiled source syntax, edition branches, menus and fallthrough.
        var compiled = storyRuntime.Compilation;
        {
            var lab = maps.Values.Single(x => x.SourceMapSymbol == "MAP_PALLET_TOWN_PROFESSOR_OAKS_LAB");
            var objects = lab.Events.GetProperty("object_events").EnumerateArray().ToArray();
            var choices = ImmutableArray.CreateBuilder<StarterChoiceDefinition>();
            int Literal(string token) => int.TryParse(token, out var value) ? value : compiled.Constants.TryGetValue(token, out value) ? value : throw new InvalidDataException("Starter choice source constant missing: " + token);
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

        ValidateSnorlaxComposition(maps, pokemonContent, compiled, bank);
        ValidateRegionalServiceComposition(regionalServices, regionalGift, maps, pokemonContent, pokemonContent, compiled, bank);
        ValidateRegionalInteriorComposition(regionalServices, departmentStore, maps);
        ValidateRegionalFlavorComposition(maps, compiled, bank);
        ValidateFuchsiaRouteComposition(fuchsiaRoute, maps, pokemonContent);
        ValidateFuchsiaGymComposition(maps, fuchsiaGymTrainers, compiled);
        ValidateFirstSeviiComposition(firstSevii, maps, gamePokemon, gamePokemon.Opponents, firstSeviiTrainers, compiled, bank);
        ValidateViridianGymComposition(viridianGym, maps, viridianGymTrainers, compiled, machines, gamePokemon);
        {
            ValidateLeagueRivalComposition(maps, leagueRivalTrainers, compiled, bank, pokemonContent, gamePokemon, gamePokemon.Opponents);
        }

        ValidateVictoryRoadComposition(victoryRoad, maps, victoryRoadTrainers, gamePokemon, compiled);

        ValidatePokemonLeagueComposition(pokemonLeague, maps, pokemonLeagueTrainers, gamePokemon.Opponents, gamePokemon, compiled, bank);

        SeviiRubyItemContent seviiRubyItems;
        {

            seviiRubyItems = PretSeviiRubyItems.Load(seviiRuby, gamePokemon, earlyShop.Catalog, machines, compiled, sharedMachines);

            ValidateSeviiRubyComposition(seviiRuby, maps, gamePokemon, gamePokemon.Opponents, seviiRubyTrainers, compiled, pokemonContent, seviiRubyItems);
        }

        FourIslandItemContent fourIslandItems;
        {

            fourIslandItems = PretFourIslandItems.Load(fourIsland, gamePokemon, earlyShop.Catalog, machines, compiled);

            ValidateFourIslandComposition(maps, fourIslandTrainers, compiled);
        }

        SixIslandItemContent sixIslandItems;
        {

            sixIslandItems = PretSixIslandItems.Load(sixIsland, gamePokemon, earlyShop.Catalog, machines, compiled, projectRoot);

            ValidateSixIslandComposition(sixIslandTrainers, compiled);
        }

        FiveIslandItemContent fiveIslandItems;
        {

            fiveIslandItems = PretFiveIslandItems.Load(fiveIsland, gamePokemon, earlyShop.Catalog, machines, compiled, projectRoot);

            ValidateFiveIslandComposition(maps, fiveIslandTrainers, compiled, fiveIslandItems, true);
        }

        ImmutableDictionary<MapId, RoamerMap> celioRoamerMaps;
        {
            celioRoamerMaps = PretCelioMapNumbers.Load(mapNumbers, maps);

            PretCelioStory.Validate(compiled);
        }


        ValidateCeruleanCaveComposition(ceruleanCave, maps, compiled);

        // The same current game retains original League teams and activates the source rematches after Celio.
        var leagueRematchTrainers = PretLeagueRematchRules.LoadTrainers(projectRoot);

        ValidateLeagueRematchComposition(leagueRematchTrainers, gamePokemon.Opponents, compiled, bank);

        var sevenTrainers = PretSevenIslandRules.LoadTrainers(projectRoot, shared: sharedMachines);

        var sevenItems = PretSevenIslandRules.LoadItemsDocument(projectRoot, shared: sharedMachines);

        ValidateSevenIslandComposition(sevenIsland, sevenTrainers, sevenItems, compiled);

        var trainerTowerCatalog = PretTrainerTowerRules.Load(projectRoot);

        ValidateTrainerTowerComposition(trainerTower, compiled, trainerTowerCatalog, earlyShop.Catalog);

        var dojoTrainers = PretFightingDojoRules.LoadTrainers(projectRoot);

        ValidateFightingDojoComposition(fightingDojo, maps, compiled);

        ValidateFossilLabComposition(fossilLab, maps, compiled);

        ValidateNpcTradeComposition(npcTrades, maps, compiled);

        ValidateGoodRodComposition(goodRodService, maps, compiled);

        ValidateSeafoamComposition(seafoam, maps, compiled);

        ValidateMoveReminderComposition(moveReminderHouse, maps, compiled);

        ValidateSourceHouseComposition("move_deleter", moveDeleterHouse.NewMaps, maps, compiled,
            "map.fuchsia-city", 10, "data/maps/FuchsiaCity_House3/scripts.inc", "FuchsiaCity_House3_EventScript_MoveDeleter");
        foreach (var (mapId, townId, townWarp, scriptPath, entry) in new[] {
            (PretOriginalTutorRooms.BodySlamMapId, "map.four-island", 2, "data/maps/FourIsland_House1/scripts.inc", "FourIsland_House1_EventScript_BodySlamTutor"),
            (PretOriginalTutorRooms.MetronomeMapId, "map.cinnabar-island-pokemon-lab-entrance", 4, "data/maps/CinnabarIsland_PokemonLab_ResearchRoom/scripts.inc", "CinnabarIsland_PokemonLab_ResearchRoom_EventScript_MetronomeTutor") })
        {

            var room = ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(mapId, originalTutorRooms.NewMaps[mapId]);

            ValidateSourceHouseComposition("original_tutor_room", room, maps, compiled, townId, townWarp, scriptPath, entry);
        }

        ValidateCopycatHouseComposition(copycatHouse, maps, compiled);

        ValidateSuperRodComposition(superRodService, maps, compiled);

        var vsSeekerItem = vsSeekerPresentation.ItemDefinition.Deserialize<EarlyShopItemDefinition>(sharedItems.CreateJsonOptions())
            ?? throw new InvalidDataException("vs_seeker_item_missing");

        var memorialTrainers = PretMemorialPillarTrainers.Load(originalTmAcquisition.MemorialTrainers);

        var gameCornerPrizes = PretGameCornerPrizes.LoadCatalog(projectRoot, gamePokemon, earlyShop.Catalog, machines, shared: sharedMachines);

        ValidateCapeBrinkComposition(capeBrinkTutor, maps, compiled);
        var prizeRoom = PretGameCornerPrizeRoom.Load(regionalServices, maps);


        ValidateRoute5DayCareComposition(route5DayCare, maps, compiled);

        ValidateFourIslandDayCareComposition(fourIslandDayCare, maps, compiled);

        schoolHouseInteriors.ValidateReusedMaps(maps);

        lostCave.ValidateReusedMaps(maps);

        ValidateLostCaveScripts(lostCave, compiled);

        ValidateTrainerFanClubComposition(trainerFanClub, maps, compiled);

        ValidateCapeBrinkEncounterSources(capeBrinkTutor, pokemonContent.Catalog);
        var moveReminder = PretMoveReminderService.Load(projectRoot, pokemonContent.Catalog);
        var moveTutors = PretMoveTutors.Load(projectRoot, pokemonContent.Catalog);

        // Change this explicit version only when the saved content model changes.
        const string version = "firered-current-content-v4";
        var fishingDefinitions = PretOrdinaryFishing.Load(projectRoot);
        SafariFishing = PretSafariFishing.Load(safari, gamePokemon);
        CyclingRoadRegion = cyclingRoad;
        NpcTradeContent = npcTrades; NpcTradeElyssa = npcTradeElyssa; NpcTradeHaden = npcTradeHaden; NpcTradeMail = npcTradeMail;
        NpcTradeMaps = ResolveNpcTradeMaps(npcTrades, maps);
        FossilLabRegion = fossilLab;
        FightingDojoRegion = fightingDojo;
         FightingDojoTrainers = dojoTrainers;
        TrainerTowerRegion = trainerTower; TrainerTowerCatalog = trainerTowerCatalog;
        TrainerTowerOpponents = gamePokemon.TrainerTowerOpponents;
        _trainerTowerConnectedMaps = trainerTower.Layouts.ToImmutableDictionary(p => p.Key, p => PretWorldMapConnections.Create(p.Value));
        PowerPlantRegion=powerPlant;

        CapeBrinkTutor = capeBrinkTutor;
        Route5DayCare = route5DayCare; FourIslandDayCare = fourIslandDayCare;
        OriginalTmAcquisition = originalTmAcquisition; SchoolHouseInteriors = schoolHouseInteriors;
        LostCaveRegion = lostCave; LostCaveTrainers = lostCaveTrainers;
        TrainerFanClub = trainerFanClub;
        FieldItemServices = originalTmAcquisition.ServiceDefinitions().AddRange(lostCave.ServiceDefinitions()).AddRange(firstSevii.ItemServices);
        FieldFly = fieldFly; FlyMapPresentation = flyMapPresentation; Route16FlyHouse = route16FlyHouse;
        Route16FlyTrainers = new(route16FlyHouse.Trainers.Trainers.ToImmutableDictionary(t => t.Id),
            route16FlyHouse.Trainers.Battles.ToImmutableDictionary(t => t.ScriptEntry, StringComparer.Ordinal));
        Route16FlyTrainerPartyFactory.ValidateCatalog(Route16FlyTrainers);
        MoveTutors = moveTutors.Catalog.WithCapeBrink(capeBrinkTutor.AuthenticateTutor(pokemonContent.Catalog));
        MoveReminderHouse = moveReminderHouse; MoveReminder = moveReminder.Catalog;
        OriginalTutorRooms = originalTutorRooms; CopycatHouse = copycatHouse;
        MoveDeleterHouse = moveDeleterHouse; MoveDeleter = moveDeleterHouse.Bind(pokemonContent!.Catalog);
        EventIslandRegion = eventIsland;

        SevenIslandRegion = sevenIsland;
        SevenIslandTrainerParties = sevenTrainers;   SevenIslandItems = sevenItems;
        LeagueRematchTrainerParties = leagueRematchTrainers;

        CeruleanCaveRegion = ceruleanCave;
        CelioRoamerMaps = celioRoamerMaps;
        FiveIslandRegion = fiveIsland;


        FiveIslandTrainers = fiveIslandTrainers;
        FiveIslandItems = fiveIslandItems;
        SixIslandRegion = sixIsland;


        SixIslandTrainers = sixIslandTrainers;
        SixIslandItems = sixIslandItems;
        FourIslandRegion = fourIsland;
        StatFlagUnlocks = ImmutableDictionary<string, FieldStatFlagUnlocks>.Empty.Add(fourIsland.DollCollection.Special, fourIsland.DollCollection);


        FourIslandTrainers = fourIslandTrainers;
        FourIslandItems = fourIslandItems;
        SeviiRubyRegion = seviiRuby;


        SeviiRubyTrainers = seviiRubyTrainers;
        SeviiRubyItems = seviiRubyItems;
        PokemonLeagueRegion = pokemonLeague;
        PokemonLeagueTrainerParties = pokemonLeagueTrainers;

        VictoryRoadRegion = victoryRoad;
        VictoryRoadTrainerParties = victoryRoadTrainers;
        VictoryRoadItems = victoryRoadItems;

        if (OpponentPokemon is { } victoryOpponents && victoryRoadTrainers.Trainers.Values.SelectMany(t => t.Party).Any(p => !victoryOpponents.Catalog.Species.ContainsKey(p.Species)))
            throw new InvalidDataException("victory_road_trainer_species_missing");
        LeagueRivalTrainerParties = leagueRivalTrainers;
        ViridianGymRegion = viridianGym;
        ViridianGymTrainerParties = viridianGymTrainers;
        FirstSeviiRegion = firstSevii;
        FirstSeviiTrainerParties = firstSeviiTrainers;
        FirstSeviiItems = firstSeviiItems;
        FirstSeviiPresentation = firstSeviiPresentation;
        CinnabarArt = cinnabarArt;
        CinnabarPresentation = cinnabarPresentation;
        CinnabarRegion = cinnabar;
        CinnabarTrainerParties = cinnabarTrainers;
        CinnabarItems = cinnabarItems;


        SaffronGymArt = saffronGymArt;
        SaffronGymRegion = saffronGym;
        SaffronGymTrainers = saffronGymTrainers;
        SaffronGymItems = saffronGymItems;

        SilphRegion = silph;
        SilphItems = silphItems;
        SilphTrainers = silphTrainers;


        SilphArt = silphArt;
        SurfArt = surfArt;
        SurfEncounters = surfEncounters;

        SafariRegion = safari;
        SafariArt = safariArt;
        SafariItems = safariItems;
        SafariEncounters = safariEncounters;
        Route8Twins = twins;
        PresetDoublePokemon = doubleMechanics.Catalog;
        PresetDoubleMechanics = doubleMechanics;
        PresetDoubleItems = doubleItems;
        FuchsiaGymRegion = fuchsiaGym;
        FuchsiaGymTrainerDocument = fuchsiaGymTrainers;
        FuchsiaGymItems = fuchsiaGymItems;
        FuchsiaGymTrainers = new(fuchsiaGymTrainers.Trainers.ToImmutableDictionary(t => t.Id), fuchsiaGymTrainers.Battles.ToImmutableDictionary(b => b.Trainer));
        FuchsiaRouteRegion = fuchsiaRoute;
        FuchsiaRouteTrainers = fuchsiaTrainers;
        FuchsiaRouteArt = fuchsiaArt;
        DepartmentStoreContract = departmentStore;
        NameRaterContract = nameRater;
        RegionalServiceMaps = regionalServices;
        RegionalGiftContract = regionalGift;
        EeveeArt = eeveeArt;
        SnorlaxRegion = snorlax;
        SnorlaxArt = snorlaxArt;
        PokemonTowerRegion = pokemonTower;
        PokemonTowerItems = pokemonTowerItems;
        PokemonTowerPickups = pokemonTowerPickups;
        PokemonTowerTrainers = pokemonTowerTrainers;
        RocketHideoutRegion = rocketHideout;
        RocketHideoutPokemon = rocketHideoutPokemon;
        RocketHideoutItems = rocketHideoutItems;
        RocketHideoutPickups = rocketHideoutPickups;
        RocketHideoutTrainers = rocketHideoutTrainers;
        CeladonGymRegion = celadonGym;
        CeladonGymPokemon = celadonGymPokemon;
        CeladonGymItems = celadonGymItems;
        CeladonGymTrainers = celadonGymTrainers;
        CeladonApproachRegion = celadonApproach;
        CeladonApproachPokemon = celadonApproachPokemon;
        CeladonApproachItems = celadonApproachItems;
        CeladonApproachEncounters = celadonApproachEncounters;
        CeladonApproachTrainers = celadonApproachTrainers;
        RockTunnelRegion = rockTunnel;
        RockTunnelPokemon = rockTunnelPokemon;
        RockTunnelItems = rockTunnelItems;
        RockTunnelEncounters = rockTunnelEncounters;
        RockTunnelTrainers = rockTunnelTrainers;
        Route11Region = route11;
        Route11Pokemon = route11Pokemon;
        Route11Items = route11Items;
        Route11Encounters = route11Encounters;
        Route11Trainers = route11Trainers;
        CityServices = cityServices;
        GameCornerCoins = gameCornerCoins;
        GameCornerPrizes = gameCornerPrizes;
        OldRod = oldRod;
        SeafoamRegion = seafoam;
         SeafoamTrainers = seafoamTrainers;
        GoodRodService = goodRodService;
        SuperRodService = superRodService;

        var fishingRows = fishingDefinitions.Where(row => maps.ContainsKey(row.Table.Map)).ToImmutableArray();
        SuperRodEncounters = fishingRows.Where(row => row.Rod == FishingRod.Super).ToImmutableDictionary(row => row.Table.Map, row => row.Table);
        OldRodEncounters = fishingRows.Where(row => row.Rod == FishingRod.Old).ToImmutableDictionary(row => row.Table.Map, row => row.Table);
        GoodRodEncounters = fishingRows.Where(row => row.Rod == FishingRod.Good).ToImmutableDictionary(row => row.Table.Map, row => row.Table);
        var fishingKeys = fishingRows.Select(row => (row.Rod, row.Table.Map)).ToHashSet();
        // A mounted map without a fishing header follows the original no-bite path.
        // Shore eligibility is checked by the shared task before any cast can start.
        var emptyFishingMaps = maps.Keys.Where(map => !safari.SourceMaps.ContainsKey(map))
            .SelectMany(map => Enum.GetValues<FishingRod>().Select(rod => new FishingMapAdmission(rod, map)))
            .Where(row => !fishingKeys.Contains((row.Rod, row.Map))).ToImmutableArray();
        var fishingItems = fishingRows.SelectMany(row => row.Table.Slots).Select(slot => slot.Species).Distinct()
            .Select(id => canonicalPokemon.Metadata.TryGetValue(id, out var metadata)
                ? new FishingHeldItem(id, metadata.ItemCommon, metadata.ItemRare)
                : throw new InvalidDataException("fishing_species_metadata_missing")).ToImmutableArray();
        if (fishingItems.SelectMany(row => new[] { row.Common, row.Rare }).Any(symbol => symbol != "ITEM_NONE" &&
            !gamePokemon.Catalog.BattleItems!.Items.ContainsKey(new("pret:" + symbol))))
            throw new InvalidDataException("fishing_held_item_missing");
        FishingTables = new("firered-ordinary-fishing-task-v1", fishingRows, emptyFishingMaps, fishingItems);
        if (FishingTables.Tables.Values.SelectMany(t => t.Slots).Any(slot => !gamePokemon.Catalog.Species.ContainsKey(slot.Species)))
            throw new InvalidDataException("fishing_owned_species_missing");
        CityServiceItems = cityServices.Items.AddRange(goodRodService.Items).AddRange(superRodService.Items).AddRange(gameCornerCoins.Items);
        VermilionGymRegion = vermilionGym;
        VermilionGymTrainers = vermilionGymTrainers;
        SsAnneRegion = ssAnne;
        SsAnnePokemon = ssAnnePokemon;
        SsAnneTrainers = ssAnneTrainers;
        VermilionRegion = vermilion;
        VermilionPokemon = vermilionPokemon;
        VermilionTrainers = vermilionTrainers;
        BillRegion = bill;
        BillPokemon = billPokemon;
        BillItems = billItems;
        BillTrainers = billTrainers;
        CeruleanPokemon = ceruleanPokemon;
        CeruleanTrainers = PretCeruleanTrainers.Load(cerulean, ceruleanPokemon);
        CeruleanRegion = cerulean;
        MtMoonItems = mtMoonItems;
        MtMoonRegion = mtMoon;
        MtMoonPokemon = mtMoonPokemon;
        MtMoonTrainers = PretMtMoonTrainers.Load(mtMoon, mtMoonPokemon);
        MagikarpContract = magikarp;
        Route3Region = route3;
        Route3Pokemon = route3Pokemon;
        Route3Trainers = PretRoute3Trainers.Load(route3, route3Pokemon);
        _route4WesternPositions = Route4WesternPositions(route3);
        Route22Region = route22;
        Route22Pokemon = route22Pokemon;
        Route22Trainers = PretRoute22Trainers.Load(route22, route22Pokemon);
        EarlyRepel = repel;
        TownMap = townMap;
        OakRating = oakRating;
        PokedexMenu = oakPokedex;
        SourceMaps = maps;
        LoadFieldCut(projectRoot);
        Maps = maps.ToImmutableDictionary(x => x.Key, x => (IFieldMap)x.Value);
        EscapeRoutes = CurrentEscapeRoutes.Create(maps, compiled);
        _scripts = compiled;
        _text = bank.ToImmutable();
        ContentVersion = version;
        PokemonContent = pokemonContent;
        BattleContent = battleContent;
        OpeningItems = openingItems;
        ViridianRegion = region;
        QuestItems = questItems;
        MartMetatiles = metatiles;
        ViridianCenter = center;
        OldManTutorial = oldMan;
        PokemonStorageSource = pokemonStorage;
        EarlyPickups = fieldPickups;
        FieldPickups = fieldPickups.Pickups.Where(p => p.Kind != "hiddenItem").Select(p => new FieldPickupDefinition(p.Id, p.MapSymbol, p.Kind == "hiddenItem", p.SourceLocalId, p.X, p.Y, p.Elevation, p.Script, p.ItemSymbol, p.SourceItemId, new ItemId(p.ItemId), p.ItemName, p.Pocket, p.Quantity, p.Flag, p.SourceFlagId)).ToImmutableArray();
        EarlyShopDocument = earlyShop;
        var inventoryDefinitions = earlyShop.Catalog.Items.Select(i => i.Item)
            .Concat(questItems.Items)
            .Concat(CityServiceItems.Select(i => i.Item))
            .Append(regionalGift.TeaItem.Item)
            .Append(safariItems.Items.Single(i => i.Metadata.Item.Id == PretSafariItems.GoldTeethId).Metadata.Item)
            .Append(machines.TmCase.Item)
            .Concat(machines.Machines.Select(m => m.Machine.Item));
        Inventory = new(inventoryDefinitions, earlyShop.Catalog.Limits, earlyShop.Catalog.QuestLimits,
            machines.MachineSlots, CityServiceItems.Select(i => i.Item.Id)
                .Append(regionalGift.TeaItem.Item.Id).Append(PretSafariItems.GoldTeethId)
                .Append(ItemIds.VsSeekerId));
        EarlyShopDocument = earlyShop with { Catalog = earlyShop.Catalog with { Inventory = Inventory } };
        EarlyShopInventoryRules.ValidateCatalog(EarlyShopDocument.Catalog);
        PewterRegion = pewter;
        PewterPokemon = pewterPokemon;

        PewterTrainers = PretPewterTrainers.Load(pewter);
        ForestRegion = forest;
        ForestPokemon = forestPokemon;
        ForestTrainers = PretForestTrainers.Load(forest);
        LandEncounters = VermilionEncounterTables(forest, route22, route3, mtMoon, bill, vermilion);
        FieldPickups = FieldPickups.AddRange(MtMoonPickupDefinitions(mtMoonItems, earlyShop.Catalog, machines, compiled));
        FieldPickups = FieldPickups.AddRange(BillPickupDefinitions(billItems, machines, compiled));
        FieldPickups = FieldPickups.AddRange(SsAnnePickupDefinitions(ssAnneItems, machines, earlyShop.Catalog, compiled));
        FieldPickups = FieldPickups.AddRange(Route11PickupDefinitions(route11Items, compiled));
        LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(route11Encounters.Tables);
        FieldPickups = FieldPickups.AddRange(RockTunnelPickupDefinitions(rockTunnelItems, earlyShop.Catalog, machines, compiled));
        LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(rockTunnelEncounters.Tables);
        FieldPickups = FieldPickups.AddRange(CeladonApproachPickupDefinitions(celadonApproachItems, earlyShop.Catalog, machines, compiled));
        LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(celadonApproachEncounters.Tables);
        FieldPickups = FieldPickups.AddRange(RocketHideoutPickupDefinitions(rocketHideoutPickups, earlyShop.Catalog, machines, rocketHideout, compiled));
        LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(FuchsiaRouteLandEncounterTables.All.Select(t => KeyValuePair.Create(t.Map, t)));
        LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(PokemonTowerEncounterTables(pokemonTower));
        FieldPickups = FieldPickups.AddRange(PokemonTowerPickupDefinitions(pokemonTowerPickups, earlyShop.Catalog, compiled));
        FieldPickups = FieldPickups.AddRange(SafariPickupDefinitions(safari, earlyShop.Catalog, machines, safariItems, compiled));
        FieldPickups = FieldPickups.AddRange(SilphPickupDefinitions(silph, earlyShop.Catalog, machines, silphItems, compiled));
        FieldPickups = FieldPickups.AddRange(CinnabarPickupDefinitions(cinnabar, earlyShop.Catalog, machines, compiled));
        LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(safariEncounters.Tables);
        {
            LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(CinnabarLandEncounterTables.All.Select(t => KeyValuePair.Create(t.Map, t)));
            CinnabarWaterEncounters = surfEncounters.Catalog.Add(CinnabarWaterEncounterCatalog.SourceAdditions);
        }

        {
            FieldPickups = FieldPickups.AddRange(FirstSeviiPickupDefinitions(firstSevii, firstSeviiItems, earlyShop.Catalog, compiled).Where(p => !p.Hidden));
            LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(firstSevii.LandEncounters.Select(t => KeyValuePair.Create(t.Map, t)));
            FirstSeviiWaterEncounters = CinnabarWaterEncounters!.Add(FirstSeviiEncounterTables.Water);
        }

        {
            FieldPickups = FieldPickups.AddRange(VictoryRoadPickupDefinitions(victoryRoad, victoryRoadItems, earlyShop.Catalog, compiled).Where(p => !p.Hidden));
            LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(VictoryRoadEncounterTables.Land.Select(t => KeyValuePair.Create(t.Map, t)));
            VictoryRoadWaterEncounters = FirstSeviiWaterEncounters!.Add(VictoryRoadEncounterTables.Water);
        }

        {
            FieldPickups = FieldPickups.AddRange(seviiRubyItems.Pickups.Where(p => !p.Hidden));
            LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(SeviiRubyEncounterTables.Land.Select(t => KeyValuePair.Create(t.Map, t)));
            SeviiRubyWaterEncounters = VictoryRoadWaterEncounters!.Add(SeviiRubyEncounterTables.Water);
        }

        {
            FieldPickups = FieldPickups.AddRange(fourIslandItems.Pickups.Where(p => !p.Hidden));
            LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(FourIslandEncounterTables.Land.Select(t => KeyValuePair.Create(t.Map, t)));
            FourIslandWaterEncounters = SeviiRubyWaterEncounters!.Add(FourIslandEncounterTables.Water);
        }

        {
            FieldPickups = FieldPickups.AddRange(sixIslandItems.Pickups.Where(p => !p.Hidden));
            LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(sixIsland.LandEncounters.Select(t => KeyValuePair.Create(t.Map, t)));
            SixIslandWaterEncounters = FourIslandWaterEncounters!.Add(SixIslandEncounterTables.Water);
        }

        {
            FieldPickups = FieldPickups.AddRange(fiveIslandItems.Pickups.Where(p => !p.Hidden));
            LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(FiveIslandEncounterTables.Land.Select(t => KeyValuePair.Create(t.Map, t)));
            FiveIslandWaterEncounters = SixIslandWaterEncounters!.Add(FiveIslandEncounterTables.Water);
        }

        FieldPickups = FieldPickups.AddRange(SourceRegionPickupDefinitions(ceruleanCave.NewMaps, ceruleanCave.Items, ceruleanCave.Contracts, PretCeruleanCaveRegion.MapIdFromSourceSymbol, "maps/regions/cerulean-cave", earlyShop.Catalog, compiled));
        LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(CeruleanCaveEncounterTables.Land.Select(t => KeyValuePair.Create(t.Map, t)));
        CeruleanCaveWaterEncounters = FiveIslandWaterEncounters!.Add(CeruleanCaveEncounterTables.Water);
        if (FieldPickups.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != FieldPickups.Length)
            throw new InvalidDataException("cerulean_cave_pickup_composition");

        FieldPickups = FieldPickups.AddRange(sevenItems.Pickups.Where(p => !p.Hidden));
        LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(SevenIslandEncounterTables.Land.Select(t => KeyValuePair.Create(t.Map, t)));
        SevenIslandWaterEncounters = CeruleanCaveWaterEncounters.Add(SevenIslandEncounterTables.Water);
        SeafoamWaterEncounters = SevenIslandWaterEncounters.Add(SeafoamEncounterTables.Water);
        CapeBrinkWaterEncounters = SeafoamWaterEncounters.Add(CapeBrinkEncounterTables.Water);
        var waterCatalog = CapeBrinkWaterEncounters.Add(MemorialPillarEncounterTables.Water)
            .Add(LostCaveEncounterTables.Water).Add(firstSevii.WaterEncounters);
        WaterEncounters = waterCatalog.Tables;
        if (FieldPickups.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != FieldPickups.Length)
            throw new InvalidDataException("seven_island_pickup_composition");

        var powerPickups=SourceRegionPickupDefinitions(powerPlant.NewMaps,powerPlant.Items,powerPlant.Contracts,
            PretPowerPlantRegion.MapIdFromSourceSymbol,"power-plant",earlyShop.Catalog,compiled);
        if(FieldPickups.Select(p=>p.Id).Intersect(powerPickups.Select(p=>p.Id),StringComparer.Ordinal).Any())
            throw new InvalidDataException("power_plant_duplicate_pickup");
        FieldPickups=FieldPickups.AddRange(powerPickups);
        FieldPickups=FieldPickups.AddRange(SourceRegionPickupDefinitions(seafoam.NewMaps,seafoam.Items,seafoam.Contracts,
            PretSeafoamRegion.MapIdFromSourceSymbol,"seafoam",earlyShop.Catalog,compiled));
        FieldPickups = FieldPickups.AddRange(originalTmAcquisition.MachineBallPickups(machines));
        FieldPickups = FieldPickups.AddRange(originalTmAcquisition.MemorialPickups(earlyShop.Catalog));
        FieldPickups = FieldPickups.AddRange(LostCavePickupDefinitions(lostCave, earlyShop.Catalog, compiled));
        if (FieldPickups.Any(p => p.Hidden))
            throw new InvalidDataException("hidden_pickups_require_world_binding");
        FieldPickups = FieldPickups.AddRange(PretWorldHiddenPickups.Load(world, sharedItems));
        if (FieldPickups.Where(p => p.ItemSymbol != "ITEM_NONE").Any(p => !Inventory.Items.ContainsKey(p.Item)))
            throw new InvalidDataException("field_pickup_inventory_admission");
        var underfootFlags = world.Maps.Values.SelectMany(map => map.Events.GetProperty("bg_events").EnumerateArray())
            .Where(row => row.GetProperty("type").GetString() == "hidden_item" && row.GetProperty("underfoot").GetBoolean())
            .Select(row => row.GetProperty("flag").GetString()!).ToHashSet(StringComparer.Ordinal);
        WorldUnderfootItems = FieldPickups.Where(p => p.Hidden && underfootFlags.Contains(p.Flag))
            .Select(p => new ItemfinderUnderfoot(p.MapSymbol, p.X, p.Y, p.Elevation, p.ItemSymbol, p.SourceItemId,
                p.Flag, p.SourceFlagId, p.Quantity)).ToImmutableArray();
        LandEncounters=LandEncounters!.ToImmutableDictionary().Add(PowerPlantEncounterTables.Land.Map,PowerPlantEncounterTables.Land);
        LandEncounters = LandEncounters.ToImmutableDictionary().AddRange(SeafoamEncounterTables.Land.Select(table => KeyValuePair.Create(table.Map, table)));
        LandEncounters = LandEncounters.ToImmutableDictionary()
            .AddRange(SourceTableEncounterRegistry.Land.Where(table => maps.ContainsKey(table.Map))
                .Select(table => KeyValuePair.Create(table.Map, table)))
            .AddRange(cyclingRoad.LandEncounters.Select(table => KeyValuePair.Create(table.Map, table)));

        var metatileDefinitions = metatiles.Definitions.ToDictionary(x => x.Key, x => new FieldMetatileDefinition(x.Key, x.Value.Behavior, x.Value.Terrain, x.Value.EncounterType, x.Value.LayerType));
        {
            // Source metatile IDs are local to a tileset. Preserve the Mart's 0x2BF
            // definition; the map-aware lookup supplies the Gym's different layer.
            foreach (var(id, tile)in vermilionGym.BeamDefinitions)
                metatileDefinitions.TryAdd(id, new(id, tile.Behavior, tile.Terrain, tile.EncounterType, tile.LayerType));
        }

        waterCatalog.ValidateReferences(
            maps.ToImmutableDictionary(pair => pair.Key, pair => (IFieldMap)pair.Value), gamePokemon.Catalog);
        MetatileDefinitions = metatileDefinitions;
        ConnectedFieldMaps = maps.ToImmutableDictionary(x => x.Key, x => PretWorldMapConnections.Create(x.Value));
        _definitions = maps.ToImmutableDictionary(x => x.Key, x => Definition(x.Value));
        OrdinaryMarts = OrdinaryMartCatalog.Bind(compiled, _definitions.Values, EarlyShopDocument.Catalog);
        InitializeMapLayouts(seafoam.LayoutVariants.Concat(firstSevii.LayoutVariants));


        MailPresentation = mailPresentation;
        MailComposer = mailComposer.Catalog;
        TeachyTv = teachyTv;
        VsSeeker = vsSeeker.Catalog;
        VsSeekerTexts = vsSeekerPresentation.EncodedTexts;
        VsSeekerTrainers = vsSeekerTrainers;
        VsSeekerBattleOccurrences = vsSeekerOccurrences;

        FameChecker = fameChecker.Catalog;
        GameCornerSlots = SlotMachineSessionContract.CreateApprovedProduction(slotRules);
        ItemfinderDocument = itemfinder;
        FieldRockSmash = fieldRockSmash;
        MemorialPillarTrainers = memorialTrainers;

        BindPokedexAides();
        BindCyclingRoadSceneInteriors();
    }

    private static void ValidateCeruleanCaveComposition(PretCeruleanCaveRegion region,
        ImmutableDictionary<MapId, PretWorldMap> maps,
        PretStoryCompilation scripts)
    {
        if (region.NewMaps.Any(pair => !maps.ContainsKey(pair.Key)))
            throw new InvalidDataException("cerulean_cave_map_collision");
        var city = maps[new MapId("map.cerulean-city")];
        var caveEntrance = city.FieldWarps.Single(w => w.DestinationMapId == new MapId("map.cerulean-cave-1f"));
        var destination = maps[caveEntrance.DestinationMapId].FieldWarps[caveEntrance.DestinationWarpId];
        var guard = city.Events.GetProperty("object_events").EnumerateArray().Single(e =>
            e.TryGetProperty("script", out var label) && label.GetString() == "CeruleanCity_EventScript_CeruleanCaveGuard");
        if (destination.DestinationMapId != city.Id ||
            guard.GetProperty("flag").GetString() != "FLAG_HIDE_CERULEAN_CAVE_GUARD" ||
            scripts.FindProgram("CeruleanCity_EventScript_CeruleanCaveGuard") is not { Instructions.IsEmpty: false })
            throw new InvalidDataException("cerulean_cave_city_anchor");
        foreach (var label in new[] { "CeruleanCave_B1F_OnResume", "CeruleanCave_B1F_EventScript_TryRemoveMewtwo",
            "CeruleanCave_B1F_OnTransition", "CeruleanCave_B1F_EventScript_ShowMewtwo", "CeruleanCave_B1F_EventScript_Mewtwo",
            "CeruleanCave_B1F_EventScript_DefeatedMewtwo", "CeruleanCave_B1F_EventScript_RanFromMewtwo" })
            if (scripts.FindProgram(label) is not { } program || program.Instructions.IsEmpty || program.Instructions.Any(i => i.Op == StoryOp.Unsupported))
                throw new InvalidDataException("cerulean_cave_story_incomplete:" + label);
    }
}

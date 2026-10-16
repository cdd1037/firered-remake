using System.Collections.Immutable;
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

namespace Game.Content;
/// <summary>The current-game content catalog. Unknown operations remain faults.</summary>
public sealed partial class PretCampaignCatalog : ICampaignCatalog
{
    private readonly PretStoryCompilation _scripts;
    private readonly ImmutableDictionary<MapId, FieldMapDefinition> _definitions;
    private readonly ImmutableDictionary<string, string> _text;
    public string ContentVersion { get; }
    public CampaignPokemonContent? PokemonContent { get; }
    public CampaignBattleContent? BattleContent { get; }
    public OpeningItemsCatalog? OpeningItems { get; }
    public QuestItemsCatalog? QuestItems { get; }
    public InventoryCatalog Inventory { get; }
    public EscapeRopePolicy EscapeRoutes { get; }
    public bool CaptureEnabled { get; }
    public PretViridianCenter? ViridianCenter { get; }
    public bool ViridianCenterEnabled => ViridianCenter is not null;
    public PretOldManTutorialContent? OldManTutorial { get; }
    public bool OldManTutorialEnabled => OldManTutorial is not null;
    public bool PartyTrainingEnabled { get; }
    public PretForestRegion? ForestRegion { get; }
    public PretForestPokemon? ForestPokemon { get; }
    public bool ForestEnabled => ForestRegion is not null;
    public bool ForestTrainersEnabled => ForestTrainers is not null;
    public PretPewterRegion? PewterRegion { get; }
    public bool PewterEnabled => PewterRegion is not null;
    public bool EarlyStarterEnabled { get; }
    public bool EarlyPartyEnabled { get; }
    public PretPokemonStorageSource? PokemonStorageSource { get; }
    public bool PokemonStorageEnabled => PokemonStorageSource is not null;
    public PretEarlyPickups? EarlyPickups { get; }
    public ImmutableArray<FieldPickupDefinition> FieldPickups { get; }
    public ImmutableArray<FieldItemServiceDefinition> FieldItemServices { get; init; } = [];
    public PretEarlyRepelDocument? EarlyRepel { get; }
    public bool RepelEnabled => EarlyRepel is not null;
    public PretTownMap? TownMap { get; }
    public bool TownMapEnabled => TownMap is not null;
    public PretOakRating? OakRating { get; }
    /// <summary>Immutable metadata shared by Oak, Route22 and presentation in this composition.</summary>
    public PretPokedexMenu? PokedexMenu { get; }
    public bool OakRatingEnabled => OakRating is not null;
    public PretRoute22Region? Route22Region { get; }
    public PretRoute22Pokemon? Route22Pokemon { get; }
    public Route22TrainerCatalog? Route22Trainers { get; }
    public bool Route22Enabled => Route22Region is not null;
    public PretRoute3Region? Route3Region { get; }
    public PretRoute3Pokemon? Route3Pokemon { get; }
    public Route3TrainerCatalog? Route3Trainers { get; }
    public bool Route3Enabled => Route3Region is not null;
    public PretMagikarpContract? MagikarpContract { get; }
    public bool MagikarpEnabled => MagikarpContract is not null;
    public PretMtMoonRegion? MtMoonRegion { get; }
    public PretMtMoonPokemon? MtMoonPokemon { get; }
    public MtMoonTrainerCatalog? MtMoonTrainers { get; }
    public bool MtMoonEnabled => MtMoonRegion is not null;
    public PretCeruleanRegion? CeruleanRegion { get; }
    public PretCeruleanPokemon? CeruleanPokemon { get; }
    public CeruleanTrainerCatalog? CeruleanTrainers { get; }
    public bool CeruleanEnabled => CeruleanRegion is not null;
    public PretCeruleanCaveRegion CeruleanCaveRegion { get; }
    public CeruleanCavePokemonContent CeruleanCavePokemon { get; }
    public bool CeruleanCaveEnabled => CeruleanCavePokemon is not null;
    public GamePokemonCatalog CeruleanCaveOwnership => CeruleanCavePokemon.Owned;
    public GameOpponentPokemonCatalog CeruleanCaveOpponents => CeruleanCavePokemon.Opponents;
    public MachineCatalog CeruleanCaveMachines => CeruleanCavePokemon.Machines;
    public CeruleanCaveWaterEncounterCatalog CeruleanCaveWaterEncounters { get; }
    public PretCityServices? CityServices { get; }
    public PretGameCornerCoins GameCornerCoins { get; }
    public GameCornerPrizeCatalog GameCornerPrizes { get; }
    public FieldFlyCatalog FieldFly { get; }
    public FieldRockSmashCatalog? FieldRockSmash { get; private init; }
    public PretFlyMap FlyMapPresentation { get; }
    public PretRoute16FlyHouse Route16FlyHouse { get; }
    public PretOriginalTmAcquisition OriginalTmAcquisition { get; }
    public PretSchoolHouseInteriors SchoolHouseInteriors { get; }
    public PretTrainerFanClub TrainerFanClub { get; }
    public bool TrainerFanClubEnabled => TrainerFanClub is not null;
    public PretLostCaveRegion LostCaveRegion { get; }
    public LostCaveTrainerCatalog LostCaveTrainers { get; }
    public bool LostCaveEnabled => LostCaveRegion is not null;
    public Route16FlyTrainerCatalog Route16FlyTrainers { get; }
    public bool CityServicesEnabled => CityServices is not null;
    public PretOldRod? OldRod { get; }
    public IReadOnlyDictionary<MapId, OldRodEncounterTable> OldRodEncounters { get; }
    public ImmutableArray<MachineItem> CityServiceItems { get; }
    public PretVermilionGymRegion? VermilionGymRegion { get; }
    public VermilionGymTrainerCatalog? VermilionGymTrainers { get; }
    public bool VermilionGymEnabled => VermilionGymRegion is not null;
    public PretSsAnneRegion? SsAnneRegion { get; }
    public PretSsAnnePokemon? SsAnnePokemon { get; }
    public SsAnneTrainerCatalog? SsAnneTrainers { get; }
    public bool SsAnneEnabled => SsAnneRegion is not null;
    public PretVermilionRegion? VermilionRegion { get; }
    public PretVermilionPokemon? VermilionPokemon { get; }
    public VermilionTrainerCatalog? VermilionTrainers { get; }
    public bool VermilionEnabled => VermilionRegion is not null;
    public PretBillRegion? BillRegion { get; }
    public PretBillPokemon? BillPokemon { get; }
    public PretBillItemsDocument? BillItems { get; }
    public BillTrainerCatalog? BillTrainers { get; }
    public bool BillEnabled => BillRegion is not null;
    public PretMtMoonItemsDocument? MtMoonItems { get; }

    public bool IsValidPokedexSourceSpecies(int sourceSpeciesId) => PokedexMenu?.Entries.ContainsKey(sourceSpeciesId) == true;
    public int? SourceSpeciesHeightDecimeters(int sourceSpeciesId) => PokedexMenu?.Entries.GetValueOrDefault(sourceSpeciesId)?.HeightDecimeters;
    public string? SourceSpeciesName(int sourceSpeciesId) => PokedexMenu?.Entries.GetValueOrDefault(sourceSpeciesId)?.Name;

    public CampaignOakRatingProjection EvaluateOakRating(IEnumerable<int> seenSpecies,IEnumerable<int> caughtSpecies)
    {
        var result = (OakRating ?? throw new InvalidDataException("oak_rating_not_enabled")).EvaluateSpeciesIds(seenSpecies, caughtSpecies, PokedexMenu!, new(HasPokedex: true));
        return new(result.Kanto.Seen, result.Kanto.Caught, result.Rating.TextSymbol, result.Rating.KantoComplete);
    }

    public PretEarlyShopDocument? EarlyShopDocument { get; }
    public EarlyShopCatalog? EarlyShop => EarlyShopDocument?.Catalog;
    public OrdinaryMartCatalog OrdinaryMarts { get; }
    public ForestTrainerCatalog? ForestTrainers { get; }
    public PewterTrainerCatalog? PewterTrainers { get; }
    public PretPewterPokemon? PewterPokemon { get; }
    public MachineCatalog? PewterMachines { get; }
    public IReadOnlyDictionary<MapId, EarlyLandEncounterTable>? LandEncounters { get; }
    public PretMartMetatiles? MartMetatiles { get; }
    public IReadOnlyDictionary<int, FieldMetatileDefinition>? MetatileDefinitions { get; }
    public IReadOnlyDictionary<MapId, IFieldMap> Maps { get; }
    public IReadOnlyDictionary<MapId, PretWorldMap> SourceMaps { get; }
    public PretStoryCompilation ScriptCompilation => _scripts;
    public bool RegionEnabled { get; }
    public PretViridianRegion? ViridianRegion { get; }
    public IReadOnlyDictionary<MapId, ConnectedFieldMap>? ConnectedFieldMaps { get; }

    private PretCampaignCatalog(
        ImmutableDictionary<MapId, PretWorldMap> maps,
        PretStoryCompilation scripts,
        ImmutableDictionary<string, string> text,
        string version,
        CampaignPokemonContent pokemonContent,
        CampaignBattleContent battleContent,
        OpeningItemsCatalog openingItems,
        PretViridianRegion region,
        QuestItemsCatalog questItems,
        PretMartMetatiles metatiles,
        PretViridianCenter center,
        PretOldManTutorialContent oldMan,
        PretForestRegion forest,
        PretForestPokemon forestPokemon,
        PretPewterRegion pewter,
        PretPewterPokemon pewterPokemon,
        MachineCatalog machines,
        PretEarlyShopDocument earlyShop,
        PretPokemonStorageSource pokemonStorage,
        PretEarlyPickups fieldPickups,
        PretEarlyRepelDocument repel,
        PretTownMap townMap,
        PretOakRating oakRating,
        PretPokedexMenu oakPokedex,
        PretRoute22Region route22,
        PretRoute22Pokemon route22Pokemon,
        PretRoute3Region route3,
        PretRoute3Pokemon route3Pokemon,
        PretMagikarpContract magikarp,
        PretMtMoonRegion mtMoon,
        PretMtMoonPokemon mtMoonPokemon,
        PretMtMoonItemsDocument mtMoonItems,
        PretCeruleanRegion cerulean,
        PretCeruleanPokemon ceruleanPokemon,
        PretBillRegion bill,
        PretBillPokemon billPokemon,
        PretBillItemsDocument billItems,
        BillTrainerCatalog billTrainers,
        PretVermilionRegion vermilion,
        PretVermilionPokemon vermilionPokemon,
        VermilionTrainerCatalog vermilionTrainers,
        PretSsAnneRegion ssAnne,
        PretSsAnnePokemon ssAnnePokemon,
        SsAnneTrainerCatalog ssAnneTrainers,
        PretSsAnneItemsDocument ssAnneItems,
        PretVermilionGymRegion vermilionGym,
        VermilionGymTrainerCatalog vermilionGymTrainers,
        PretCityServices cityServices,
        PretGameCornerCoins gameCornerCoins,
        GameCornerPrizeCatalog gameCornerPrizes,
        PretOldRod oldRod,
        PretRoute11Region route11,
        PretRoute11Pokemon route11Pokemon,
        PretRoute11ItemsDocument route11Items,
        PretRoute11Encounters route11Encounters,
        Route11TrainerCatalog route11Trainers,
        PretRockTunnelRegion rockTunnel,
        PretRockTunnelPokemon rockTunnelPokemon,
        PretRockTunnelItemsDocument rockTunnelItems,
        PretRockTunnelEncounters rockTunnelEncounters,
        RockTunnelTrainerCatalog rockTunnelTrainers,
        PretCeladonApproachRegion celadonApproach,
        PretCeladonApproachPokemon celadonApproachPokemon,
        PretCeladonApproachItemsDocument celadonApproachItems,
        PretCeladonApproachEncounters celadonApproachEncounters,
        CeladonApproachTrainerCatalog celadonApproachTrainers,
        PretCeladonGymRegion celadonGym,
        PretCeladonGymPokemon celadonGymPokemon,
        PretCeladonGymItemsDocument celadonGymItems,
        CeladonGymTrainerCatalog celadonGymTrainers,
        PretRocketHideoutRegion rocketHideout,
        PretRocketHideoutPokemon rocketHideoutPokemon,
        PretRocketHideoutItemsDocument rocketHideoutItems,
        PretRocketHideoutPickupsDocument rocketHideoutPickups,
        RocketHideoutTrainerCatalog rocketHideoutTrainers,
        PretPokemonTowerRegion pokemonTower,
        PretPokemonTowerPokemon pokemonTowerPokemon,
        PretPokemonTowerItemsDocument pokemonTowerItems,
        PretPokemonTowerPickupsDocument pokemonTowerPickups,
        PokemonTowerTrainerCatalog pokemonTowerTrainers,
        PretSnorlaxRegion snorlax,
        PretSnorlaxPokemon snorlaxPokemon,
        PretSnorlaxArt snorlaxArt,
        PretRegionalServiceMaps regionalServices,
        PretRegionalGiftContract regionalGift,
        PretEeveePokemon eeveePokemon,
        PretEeveeArt eeveeArt,
        PretDepartmentStoreContract departmentStore,
        PretNameRaterContract nameRater,
        PretFuchsiaRouteRegion fuchsiaRoute,
        PretFuchsiaRoutePokemon fuchsiaPokemon,
        PretFuchsiaRouteArt fuchsiaArt,
        PretFuchsiaGymRegion fuchsiaGym,
        FuchsiaGymTrainerDocument fuchsiaGymTrainers,
        PretFuchsiaGymItemsDocument fuchsiaGymItems,
        PretRockTunnelMachinesDocument fuchsiaGymMachines,
        SourceTwinsTrainerDefinition twins,
        SourceMechanicsCatalog doubleMechanics,
        SourceBattleItemCatalog doubleItems,
        PretSafariRegion safari,
        PretSafariPokemon safariPokemon,
        PretSafariArt safariArt,
        PretSafariItems safariItems,
        PretSafariEncounters safariEncounters,
        PretSurfPokemon surfPokemon,
        PretSurfArt surfArt,
        PretSurfEncounters surfEncounters,
        PretSilphRegion silph,
        PretSilphPokemon silphPokemon,
        PretSilphItemsDocument silphItems,
        SilphTrainerCatalogContent silphTrainers,
        PretSilphArt silphArt,
        PretSaffronGymRegion saffronGym,
        SaffronGymTrainerCatalogContent saffronGymTrainers,
        PretSaffronGymItemsDocument saffronGymItems,
        SilphPokemonDocument saffronGymPokemon,
        PretSaffronGymArt saffronGymArt,
        PretCinnabarRegion cinnabar,
        SilphPokemonDocument cinnabarPokemon,
        CinnabarTrainerCatalog cinnabarTrainers,
        PretCinnabarItemsDocument cinnabarItems,
        PretCinnabarArt cinnabarArt,
        PretCinnabarPresentation cinnabarPresentation,
        PretFirstSeviiRegion firstSevii,
        FirstSeviiTrainerCatalog firstSeviiTrainers,
        FirstSeviiItemCatalog firstSeviiItems,
        PretFirstSeviiPresentation firstSeviiPresentation,
        PretViridianGymRegion viridianGym,
        ViridianGymTrainerCatalog viridianGymTrainers,
        LeagueRivalTrainerCatalog leagueRivalTrainers,
        PretVictoryRoadRegion victoryRoad,
        VictoryRoadTrainerCatalog victoryRoadTrainers,
        VictoryRoadItemContent victoryRoadItems,
        PretPokemonLeagueRegion pokemonLeague,
        PokemonLeagueTrainerCatalog pokemonLeagueTrainers,
        bool nationalDexEnabled,
        PretSeviiRubyRegion seviiRuby,
        SeviiRubyTrainerCatalog seviiRubyTrainers,
        SeviiRubyItemContent seviiRubyItems,
        PretFourIslandRegion fourIsland,
        FourIslandTrainerCatalog fourIslandTrainers,
        FourIslandItemContent fourIslandItems,
        PretSixIslandRegion sixIsland,
        SixIslandTrainerCatalog sixIslandTrainers,
        SixIslandItemContent sixIslandItems,
        PretFiveIslandRegion fiveIsland,
        FiveIslandTrainerCatalog fiveIslandTrainers,
        FiveIslandItemContent fiveIslandItems,
        CelioPokemonContent celioPokemon,
        ImmutableDictionary<MapId, RoamerMap> celioRoamerMaps,
        PretCeruleanCaveRegion ceruleanCave,
        CeruleanCavePokemonContent ceruleanCavePokemon,
        LeagueRematchTrainerCatalog leagueRematchTrainers,
        PretSevenIslandRegion sevenIsland, SevenIslandTrainerCatalog sevenTrainers,
        SevenIslandItemsDocument sevenItems, PretMoveTutors moveTutors,
        PretEventIslandRegion eventIsland, PretPowerPlantRegion powerPlant, PretTrainerTowerRegion trainerTower, TrainerTowerCatalog trainerTowerCatalog,
        PretFightingDojoRegion fightingDojo, FightingDojoTrainerCatalog dojoTrainers,
        PretFossilLabRegion fossilLab, PretNpcTrades npcTrades, PretNpcTradeElyssa npcTradeElyssa, PretNpcTradeHaden npcTradeHaden, PretNpcTradeMail npcTradeMail, PretGoodRodService goodRodService,
        PretSeafoamRegion seafoam, SeafoamTrainerCatalog seafoamTrainers,
        PretMoveReminderHouse moveReminderHouse, PretMoveReminderService moveReminder, PretMoveDeleterHouse moveDeleterHouse, PretOriginalTutorRooms originalTutorRooms, PretCopycatHouse copycatHouse,
        PretSuperRodService superRodService, PretCapeBrinkTutor capeBrinkTutor,
        FieldFlyCatalog fieldFly, PretFlyMap flyMapPresentation, PretRoute16FlyHouse route16FlyHouse, PretRoute5DayCare route5DayCare, PretFourIslandDayCare fourIslandDayCare, PretOriginalTmAcquisition originalTmAcquisition, PretSchoolHouseInteriors schoolHouseInteriors, PretLostCaveRegion lostCave, LostCaveTrainerCatalog lostCaveTrainers, PretTrainerFanClub trainerFanClub, PretCyclingRoadRegion cyclingRoad, PretCanonicalPokemonCatalog canonicalPokemon, ImmutableArray<FishingRodTable> fishingDefinitions)
    {
        var gamePokemon = pokemonContent.Pokemon;
        SafariFishing = PretSafariFishing.Load(safari, gamePokemon);
        CyclingRoadRegion = cyclingRoad;
        NpcTradeContent = npcTrades; NpcTradeElyssa = npcTradeElyssa; NpcTradeHaden = npcTradeHaden; NpcTradeMail = npcTradeMail;
        NpcTradeMaps = ResolveNpcTradeMaps(npcTrades, maps);
        FossilLabRegion = fossilLab; FossilLabOwnership = gamePokemon; FossilLabOpponents = gamePokemon.Opponents; FossilLabMachines = machines;
        FightingDojoRegion = fightingDojo; FightingDojoOwnership = gamePokemon; FightingDojoOpponents = gamePokemon.Opponents;
        FightingDojoMachines = machines; FightingDojoTrainers = dojoTrainers;
        TrainerTowerRegion = trainerTower; TrainerTowerCatalog = trainerTowerCatalog;
        TrainerTowerOpponents = gamePokemon.TrainerTowerOpponents;
        _trainerTowerConnectedMaps = trainerTower.Layouts.ToImmutableDictionary(p => p.Key, p => PretTrainerTowerRegion.ConnectedMap(p.Value));
        PowerPlantRegion=powerPlant; PowerPlantOwnership=gamePokemon; PowerPlantOpponents=gamePokemon.Opponents;
        PowerPlantMachines = machines;
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
        EventIslandRegion = eventIsland; EventIslandOwnership = gamePokemon; EventIslandOpponents = gamePokemon.Opponents;
        EventIslandMachines = machines;
        SevenIslandRegion = sevenIsland; SevenIslandOwnership = gamePokemon; SevenIslandOpponents = gamePokemon.Opponents;
        SevenIslandTrainerParties = sevenTrainers; SevenIslandMachines = machines;  SevenIslandItems = sevenItems;
        LeagueRematchTrainerParties = leagueRematchTrainers;
        LeagueRematchOpponents = gamePokemon.Opponents;
        CeruleanCaveRegion = ceruleanCave;
        CeruleanCavePokemon = ceruleanCavePokemon;
        CelioPokemon = celioPokemon;
        CelioRoamerMaps = celioRoamerMaps;
        FiveIslandRegion = fiveIsland;
        FiveIslandOwnership = gamePokemon;
        FiveIslandOpponents = gamePokemon.Opponents;
        FiveIslandTrainers = fiveIslandTrainers;
        FiveIslandItems = fiveIslandItems;
        SixIslandRegion = sixIsland;
        SixIslandOwnership = gamePokemon;
        SixIslandOpponents = gamePokemon.Opponents;
        SixIslandTrainers = sixIslandTrainers;
        SixIslandItems = sixIslandItems;
        FourIslandRegion = fourIsland;
        FourIslandOwnership = gamePokemon;
        FourIslandOpponents = gamePokemon.Opponents;
        FourIslandTrainers = fourIslandTrainers;
        FourIslandItems = fourIslandItems;
        SeviiRubyRegion = seviiRuby;
        SeviiRubyOwnership = gamePokemon;
        SeviiRubyOpponents = gamePokemon.Opponents;
        SeviiRubyTrainers = seviiRubyTrainers;
        SeviiRubyItems = seviiRubyItems;
        NationalDexEnabled = nationalDexEnabled;
        PokemonLeagueRegion = pokemonLeague;
        PokemonLeagueTrainerParties = pokemonLeagueTrainers;
        PokemonLeagueOpponents = gamePokemon.Opponents;
        PokemonLeagueMartStock = CreatePokemonLeagueMartStock(pokemonLeague, earlyShop.Catalog, scripts);
        VictoryRoadRegion = victoryRoad;
        VictoryRoadTrainerParties = victoryRoadTrainers;
        VictoryRoadItems = victoryRoadItems;
        VictoryRoadOpponents = gamePokemon.Opponents;
        if (VictoryRoadOpponents is { } victoryOpponents && victoryRoadTrainers.Trainers.Values.SelectMany(t => t.Party).Any(p => !victoryOpponents.Catalog.Species.ContainsKey(p.Species)))
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
        CinnabarPokemon = cinnabarPokemon;
        CinnabarTrainerParties = cinnabarTrainers;
        CinnabarItems = cinnabarItems;
        CinnabarOwnership = gamePokemon;
        CinnabarOpponents = gamePokemon.Opponents;
        SaffronGymArt = saffronGymArt;
        SaffronGymRegion = saffronGym;
        SaffronGymTrainers = saffronGymTrainers;
        SaffronGymItems = saffronGymItems;
        SaffronGymOpponents = gamePokemon.Opponents;
        SaffronGymSpeciesNames = saffronGymPokemon.SpeciesNames.ToImmutableSortedDictionary(n => saffronGymPokemon.OpponentSpecies.Single(s => s.Id == n.Id).SourceSpeciesId, n => n.Name);
        SilphRegion = silph;
        SilphPokemon = silphPokemon;
        SilphItems = silphItems;
        SilphTrainers = silphTrainers;
        SilphOwnership = gamePokemon;
        SilphOpponents = gamePokemon.Opponents;
        SilphArt = silphArt;
        SurfPokemon = surfPokemon;
        SurfArt = surfArt;
        SurfEncounters = surfEncounters;
        SurfOwnership = gamePokemon;
        SafariRegion = safari;
        SafariPokemon = safariPokemon;
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
        FuchsiaGymMachines = fuchsiaGymMachines;
        FuchsiaGymTrainers = new(fuchsiaGymTrainers.Trainers.ToImmutableDictionary(t => t.Id), fuchsiaGymTrainers.Battles.ToImmutableDictionary(b => b.Trainer));
        FuchsiaRouteRegion = fuchsiaRoute;
        FuchsiaRoutePokemon = fuchsiaPokemon;
        FuchsiaRouteArt = fuchsiaArt;
        DepartmentStoreContract = departmentStore;
        NameRaterContract = nameRater;
        RegionalServiceMaps = regionalServices;
        RegionalGiftContract = regionalGift;
        EeveePokemon = eeveePokemon;
        EeveeArt = eeveeArt;
        RegionalFlavorEnabled = true;
        SnorlaxRegion = snorlax;
        SnorlaxPokemon = snorlaxPokemon;
        SnorlaxArt = snorlaxArt;
        PokemonTowerRegion = pokemonTower;
        PokemonTowerPokemon = pokemonTowerPokemon;
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
        SeafoamRegion = seafoam; SeafoamOwnership = gamePokemon; SeafoamOpponents = gamePokemon.Opponents;
        SeafoamMachines = machines; SeafoamTrainers = seafoamTrainers;
        GoodRodService = goodRodService;
        SuperRodService = superRodService; SuperRodOwnership = gamePokemon;
        SuperRodOpponents = gamePokemon.Opponents; SuperRodMachines = machines;
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
        Maps = maps.ToImmutableDictionary(x => x.Key, x => (IFieldMap)x.Value);
        EscapeRoutes = CurrentEscapeRoutes.Create(maps, scripts);
        _scripts = scripts;
        _text = text;
        ContentVersion = version;
        PokemonContent = pokemonContent;
        BattleContent = battleContent;
        OpeningItems = openingItems;
        ViridianRegion = region;
        RegionEnabled = true;
        QuestItems = questItems;
        MartMetatiles = metatiles;
        CaptureEnabled = true;
        ViridianCenter = center;
        OldManTutorial = oldMan;
        PartyTrainingEnabled = true;
        EarlyStarterEnabled = true;
        EarlyPartyEnabled = true;
        PokemonStorageSource = pokemonStorage;
        EarlyPickups = fieldPickups;
        FieldPickups = fieldPickups.Pickups.Select(p => new FieldPickupDefinition(p.Id, p.MapSymbol, p.Kind == "hiddenItem", p.SourceLocalId, p.X, p.Y, p.Elevation, p.Script, p.ItemSymbol, p.SourceItemId, new ItemId(p.ItemId), p.ItemName, p.Pocket, p.Quantity, p.Flag, p.SourceFlagId)).ToImmutableArray();
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
        PewterMachines = machines;
        PewterTrainers = PretPewterTrainers.Load(pewter);
        ForestRegion = forest;
        ForestPokemon = forestPokemon;
        ForestTrainers = PretForestTrainers.Load(forest);
        LandEncounters = VermilionEncounterTables(forest, route22, route3, mtMoon, bill, vermilion);
        FieldPickups = FieldPickups.AddRange(MtMoonPickupDefinitions(mtMoonItems, earlyShop.Catalog, machines, scripts));
        FieldPickups = FieldPickups.AddRange(BillPickupDefinitions(billItems, machines, scripts));
        FieldPickups = FieldPickups.AddRange(SsAnnePickupDefinitions(ssAnneItems, machines, earlyShop.Catalog, scripts));
        FieldPickups = FieldPickups.AddRange(Route11PickupDefinitions(route11Items, scripts));
        LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(route11Encounters.Tables);
        FieldPickups = FieldPickups.AddRange(RockTunnelPickupDefinitions(rockTunnelItems, earlyShop.Catalog, machines, scripts));
        LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(rockTunnelEncounters.Tables);
        FieldPickups = FieldPickups.AddRange(CeladonApproachPickupDefinitions(celadonApproachItems, earlyShop.Catalog, machines, scripts));
        LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(celadonApproachEncounters.Tables);
        FieldPickups = FieldPickups.AddRange(RocketHideoutPickupDefinitions(rocketHideoutPickups, earlyShop.Catalog, machines, rocketHideout, scripts));
        LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(FuchsiaRouteLandEncounterTables.All.Select(t => KeyValuePair.Create(t.Map, t)));
        LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(PokemonTowerEncounterTables(pokemonTower));
        FieldPickups = FieldPickups.AddRange(PokemonTowerPickupDefinitions(pokemonTowerPickups, earlyShop.Catalog, scripts));
        FieldPickups = FieldPickups.AddRange(SafariPickupDefinitions(safari, earlyShop.Catalog, machines, safariItems, scripts));
        FieldPickups = FieldPickups.AddRange(SilphPickupDefinitions(silph, earlyShop.Catalog, machines, silphItems, scripts));
        FieldPickups = FieldPickups.AddRange(CinnabarPickupDefinitions(cinnabar, earlyShop.Catalog, machines, scripts));
        LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(safariEncounters.Tables);
        {
            LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(CinnabarLandEncounterTables.All.Select(t => KeyValuePair.Create(t.Map, t)));
            CinnabarWaterEncounters = CinnabarWaterEncounterCatalog.Authenticate(surfEncounters.Catalog, CinnabarWaterEncounterCatalog.SourceAdditions);
        }

        {
            FieldPickups = FieldPickups.AddRange(FirstSeviiPickupDefinitions(firstSevii, firstSeviiItems, earlyShop.Catalog, scripts));
            LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(firstSevii.LandEncounters.Select(t => KeyValuePair.Create(t.Map, t)));
            FirstSeviiWaterEncounters = FirstSeviiWaterEncounterCatalog.Authenticate(CinnabarWaterEncounters!, FirstSeviiWaterEncounterCatalog.SourceAdditions);
        }

        {
            FieldPickups = FieldPickups.AddRange(VictoryRoadPickupDefinitions(victoryRoad, victoryRoadItems, earlyShop.Catalog, scripts));
            LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(VictoryRoadEncounterTables.Land.Select(t => KeyValuePair.Create(t.Map, t)));
            VictoryRoadWaterEncounters = VictoryRoadWaterEncounterCatalog.Authenticate(FirstSeviiWaterEncounters!, VictoryRoadEncounterTables.Water);
        }

        {
            FieldPickups = FieldPickups.AddRange(seviiRubyItems.Pickups);
            LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(SeviiRubyEncounterTables.Land.Select(t => KeyValuePair.Create(t.Map, t)));
            SeviiRubyWaterEncounters = SeviiRubyWaterEncounterCatalog.Authenticate(VictoryRoadWaterEncounters!, SeviiRubyWaterEncounterCatalog.SourceAdditions);
        }

        {
            FieldPickups = FieldPickups.AddRange(fourIslandItems.Pickups);
            LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(FourIslandEncounterTables.Land.Select(t => KeyValuePair.Create(t.Map, t)));
            FourIslandWaterEncounters = FourIslandWaterEncounterCatalog.Authenticate(SeviiRubyWaterEncounters!, FourIslandEncounterTables.Water);
        }

        {
            FieldPickups = FieldPickups.AddRange(sixIslandItems.Pickups);
            LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(sixIsland.LandEncounters.Select(t => KeyValuePair.Create(t.Map, t)));
            SixIslandWaterEncounters = SixIslandWaterEncounterCatalog.Authenticate(FourIslandWaterEncounters!, SixIslandEncounterTables.Water);
        }

        {
            FieldPickups = FieldPickups.AddRange(fiveIslandItems.Pickups);
            LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(FiveIslandEncounterTables.Land.Select(t => KeyValuePair.Create(t.Map, t)));
            FiveIslandWaterEncounters = FiveIslandWaterEncounterCatalog.Authenticate(SixIslandWaterEncounters!, FiveIslandEncounterTables.Water);
        }

        FieldPickups = FieldPickups.AddRange(SourceRegionPickupDefinitions(ceruleanCave.NewMaps, ceruleanCave.Items, ceruleanCave.Contracts, PretCeruleanCaveRegion.MapIdFromSourceSymbol, "maps/regions/cerulean-cave", earlyShop.Catalog, scripts));
        LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(CeruleanCaveEncounterTables.Land.Select(t => KeyValuePair.Create(t.Map, t)));
        CeruleanCaveWaterEncounters = CeruleanCaveWaterEncounterCatalog.Authenticate(FiveIslandWaterEncounters!, CeruleanCaveEncounterTables.Water);
        if (FieldPickups.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != FieldPickups.Length)
            throw new InvalidDataException("cerulean_cave_pickup_composition");

        FieldPickups = FieldPickups.AddRange(sevenItems.Pickups);
        LandEncounters = LandEncounters!.ToImmutableDictionary().AddRange(SevenIslandEncounterTables.Land.Select(t => KeyValuePair.Create(t.Map, t)));
        SevenIslandWaterEncounters = SevenIslandWaterEncounterCatalog.Authenticate(CeruleanCaveWaterEncounters, SevenIslandEncounterTables.Water);
        SeafoamWaterEncounters = SeafoamWaterEncounterCatalog.Authenticate(SevenIslandWaterEncounters, SeafoamEncounterTables.Water);
        CapeBrinkWaterEncounters = CapeBrinkWaterEncounterCatalog.Authenticate(SeafoamWaterEncounters, CapeBrinkEncounterTables.Water);
        WaterEncounters = CapeBrinkWaterEncounters.Tables.AddRange(
            SourceTableWaterEncounterCatalog.Authenticate(MemorialPillarEncounterTables.Water).Tables)
            .AddRange(SourceTableWaterEncounterCatalog.Authenticate(LostCaveEncounterTables.Water).Tables);
        // Generated regional headers extend the shared Surf table set. Existing source
        // tables must agree; adding a map does not change predecessor catalog counts.
        foreach (var table in firstSevii.WaterEncounters)
            if (WaterEncounters.TryGetValue(table.Map, out var existing) && !WaterEncounterCatalog.SameDefinition(existing, table))
                throw new InvalidDataException("water_encounter_header_conflict");
        WaterEncounters = WaterEncounters.ToImmutableDictionary().SetItems(firstSevii.WaterEncounters.Select(table => KeyValuePair.Create(table.Map, table)));
        if (FieldPickups.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != FieldPickups.Length)
            throw new InvalidDataException("seven_island_pickup_composition");

        var powerPickups=SourceRegionPickupDefinitions(powerPlant.NewMaps,powerPlant.Items,powerPlant.Contracts,
            PretPowerPlantRegion.MapIdFromSourceSymbol,"power-plant",earlyShop.Catalog,scripts);
        if(FieldPickups.Select(p=>p.Id).Intersect(powerPickups.Select(p=>p.Id),StringComparer.Ordinal).Any())
            throw new InvalidDataException("power_plant_duplicate_pickup");
        FieldPickups=FieldPickups.AddRange(powerPickups);
        FieldPickups=FieldPickups.AddRange(SourceRegionPickupDefinitions(seafoam.NewMaps,seafoam.Items,seafoam.Contracts,
            PretSeafoamRegion.MapIdFromSourceSymbol,"seafoam",earlyShop.Catalog,scripts));
        FieldPickups = FieldPickups.Add(CopycatHiddenPickup(copycatHouse, earlyShop.Catalog, scripts));
        FieldPickups = FieldPickups.AddRange(PretGameCornerHiddenCoins.Pickups(maps[new(GameCornerCoinSource.Map)], scripts));
        var capeBrinkPickups = CapeBrinkHiddenPickups(capeBrinkTutor, earlyShop.Catalog, scripts);
        FieldPickups = FieldPickups.AddRange(capeBrinkPickups);
        FieldPickups = FieldPickups.AddRange(originalTmAcquisition.MachineBallPickups(machines));
        FieldPickups = FieldPickups.AddRange(originalTmAcquisition.MemorialPickups(earlyShop.Catalog));
        FieldPickups = FieldPickups.AddRange(LostCavePickupDefinitions(lostCave, earlyShop.Catalog, scripts));
        FieldPickups = FieldPickups.AddRange(cyclingRoad.HiddenPickups(earlyShop.Catalog));
        CapeBrinkUnderfootItems = capeBrinkPickups.Where(p => p.ItemSymbol == "ITEM_PP_MAX")
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

        MetatileDefinitions = metatileDefinitions;
        Maps = Maps.ToImmutableDictionary().SetItem(new("map.viridian-city"), new ViridianGymCityFieldMap(maps[new("map.viridian-city")]));
        ConnectedFieldMaps = maps.ToImmutableDictionary(x => x.Key, x => cyclingRoad.NewMaps.ContainsKey(x.Key) ? PretCyclingRoadRegion.ConnectedMap(x.Value) : trainerFanClub.NewMaps.ContainsKey(x.Key) ? PretTrainerFanClub.ConnectedMap(x.Value) : lostCave.NewMaps.ContainsKey(x.Key) ? PretLostCaveRegion.ConnectedMap(x.Value) : schoolHouseInteriors.NewMaps.ContainsKey(x.Key) ? PretSchoolHouseInteriors.ConnectedMap(x.Value) : originalTmAcquisition.NewMaps.ContainsKey(x.Key) ? PretOriginalTmAcquisition.ConnectedMap(x.Value) : fourIslandDayCare.NewMaps.ContainsKey(x.Key) ? PretFourIslandDayCare.ConnectedMap(x.Value) : route5DayCare.NewMaps.ContainsKey(x.Key) ? PretRoute5DayCare.ConnectedMap(x.Value) : route16FlyHouse.NewMaps.ContainsKey(x.Key) ? PretRoute16FlyHouse.ConnectedMap(x.Value) : capeBrinkTutor.NewMaps.ContainsKey(x.Key) ? PretCapeBrinkTutor.ConnectedMap(x.Value) : superRodService.NewMaps.ContainsKey(x.Key) ? PretSuperRodService.ConnectedMap(x.Value) : copycatHouse.NewMaps.ContainsKey(x.Key) ? PretCopycatHouse.ConnectedMap(x.Value) : originalTutorRooms.NewMaps.ContainsKey(x.Key) ? PretOriginalTutorRooms.ConnectedMap(x.Value) : moveDeleterHouse.NewMaps.ContainsKey(x.Key) ? PretMoveDeleterHouse.ConnectedMap(x.Value) : moveReminderHouse.NewMaps.ContainsKey(x.Key) ? PretMoveReminderHouse.ConnectedMap(x.Value) : seafoam.NewMaps.ContainsKey(x.Key) ? PretSeafoamRegion.ConnectedMap(x.Value) : goodRodService.NewMaps.ContainsKey(x.Key) ? PretGoodRodService.ConnectedMap(x.Value) : npcTradeHaden.NewMaps.ContainsKey(x.Key) ? PretNpcTradeHaden.ConnectedMap(x.Value) : npcTradeElyssa.NewMaps.ContainsKey(x.Key) ? PretNpcTradeElyssa.ConnectedMap(x.Value) : npcTrades.NewMaps.ContainsKey(x.Key) ? PretNpcTrades.ConnectedMap(x.Value) : fossilLab.NewMaps.ContainsKey(x.Key) ? PretFossilLabRegion.ConnectedMap(x.Value) : fightingDojo.NewMaps.ContainsKey(x.Key) ? PretFightingDojoRegion.ConnectedMap(x.Value) : trainerTower.Maps.ContainsKey(x.Key) ? PretTrainerTowerRegion.ConnectedMap(x.Value) : powerPlant.NewMaps.ContainsKey(x.Key) ? PretPowerPlantRegion.ConnectedMap(x.Value) : eventIsland.NewMaps.ContainsKey(x.Key) ? PretEventIslandRegion.ConnectedMap(x.Value) : sevenIsland.NewMaps.ContainsKey(x.Key) ? PretSevenIslandRegion.ConnectedMap(x.Value) : ceruleanCave.NewMaps.ContainsKey(x.Key) ? CeruleanCaveConnectedMap(x.Value) : fiveIsland.SourceMaps.ContainsKey(x.Key) ? PretFiveIslandRegion.ConnectedMap(x.Value) : sixIsland.SourceMaps.ContainsKey(x.Key) ? PretSixIslandRegion.ConnectedMap(x.Value) : fourIsland.SourceMaps.ContainsKey(x.Key) ? PretFourIslandRegion.ConnectedMap(x.Value) : seviiRuby.SourceMaps.ContainsKey(x.Key) ? PretSeviiRubyRegion.ConnectedMap(x.Value) : pokemonLeague.SourceMaps.ContainsKey(x.Key) ? PretPokemonLeagueRegion.ConnectedMap(x.Value) : victoryRoad.SourceMaps.ContainsKey(x.Key) ? PretVictoryRoadRegion.ConnectedMap(x.Value) : viridianGym.NewMaps.ContainsKey(x.Key) ? PretViridianGymRegion.ConnectedMap(x.Value) : firstSevii.NewMaps.ContainsKey(x.Key) ? PretFirstSeviiRegion.ConnectedMap(x.Value) : cinnabar.NewMaps.ContainsKey(x.Key) ? PretCinnabarRegion.ConnectedMap(x.Value) : saffronGym.NewMaps.ContainsKey(x.Key) ? PretSaffronGymRegion.ConnectedMap(x.Value) : silph.NewMaps.ContainsKey(x.Key) ? PretSilphRegion.ConnectedMap(x.Value) : safari.SourceMaps.ContainsKey(x.Key) ? PretSafariRegion.ConnectedMap(x.Value) : fuchsiaGym.NewMaps.ContainsKey(x.Key) ? PretFuchsiaGymRegion.ConnectedMap(x.Value) : (fuchsiaRoute.NewMaps.ContainsKey(x.Key) || x.Key.Value == "map.route-12") ? PretFuchsiaRouteRegion.ConnectedMap(x.Value) : IsRegionalServiceMap(x.Key) ? RegionalServiceConnectedMap(x.Value) : snorlax.NewMaps.ContainsKey(x.Key) ? PretSnorlaxRegion.ConnectedMap(x.Value) : pokemonTower.NewMaps.ContainsKey(x.Key) ? PretPokemonTowerRegion.ConnectedMap(x.Value) : x.Key.Value == "map.vermilion-city-gym" ? PretVermilionGymRegion.ConnectedMap(x.Value) : PretRocketHideoutRegion.ConnectedMap(x.Value));
        _definitions = maps.ToImmutableDictionary(x => x.Key, x => Definition(x.Value));
        OrdinaryMarts = OrdinaryMartCatalog.Bind(scripts, _definitions.Values, EarlyShopDocument.Catalog);
        InitializeMapLayouts(seafoam.LayoutVariants.Concat(firstSevii.LayoutVariants));
    }

    private static ConnectedFieldMap CeruleanCaveConnectedMap(PretWorldMap map)
    {
        if (map.Events.GetProperty("connections").ValueKind != JsonValueKind.Null)
            throw new InvalidDataException("cerulean_cave_unexpected_connection");
        return new(map.Id, map.Width, map.Height,
            map.CreateConnectedCells(), []);
    }

    private static ImmutableArray<FieldPickupDefinition> SourceRegionPickupDefinitions(IReadOnlyDictionary<MapId,PretWorldMap> regionMaps,JsonElement items,JsonElement contracts,Func<string,MapId> resolveMap,string proofId,EarlyShopCatalog shop,PretStoryCompilation scripts)
    {
        var definitions = items.GetProperty("declarations").EnumerateArray()
            .ToDictionary(e => e.GetProperty("itemId").GetString()!, StringComparer.Ordinal);
        EarlyShopItemDefinition Item(string symbol)
        {
            var item = shop.Items.SingleOrDefault(i => i.SourceItemSymbol == symbol)
                ?? throw new InvalidDataException(proofId+"_pickup_item_missing:" + symbol);
            if (!definitions.TryGetValue(symbol, out var source) || item.Id.Value != "pret:" + symbol ||
                !scripts.TryConstant(symbol, out var id) || item.SourceItemId != id ||
                item.Name != source.GetProperty("english").GetString() || item.Pocket != source.GetProperty("pocket").GetString() ||
                item.Price != source.GetProperty("price").GetInt32())
                throw new InvalidDataException(proofId+"_pickup_item_source:" + symbol);
            return item;
        }
        var pickups = ImmutableArray.CreateBuilder<FieldPickupDefinition>();
        foreach (var row in contracts.GetProperty("itemPickups").EnumerateArray())
        {
            var source = row.GetProperty("event");
            var map = regionMaps[resolveMap(row.GetProperty("map").GetString()!)];
            int local = row.GetProperty("localId").GetInt32(),quantity = row.GetProperty("quantity").GetInt32();
            string label = source.GetProperty("script").GetString()!,symbol = row.GetProperty("item").GetString()!;
            string flag = source.GetProperty("flag").GetString()!;
            var item = Item(symbol);
            var origin = row.GetProperty("sourceRef");
            if (quantity != 1 || !JsonElement.DeepEquals(source, map.Events.GetProperty("object_events")[local - 1]) ||
                !scripts.TryConstant(flag, out int flagId) || scripts.FindProgram(label) is not { } program ||
                program.Instructions.Any(i => i.Op == StoryOp.Unsupported || i.Source.Path != origin.GetProperty("path").GetString()) ||
                !program.Instructions.Any(i => i.Opcode == "setorcopyvar" && i.Args.SequenceEqual(new[] { "VAR_0x8000",symbol })) ||
                !program.Instructions.Any(i => i.Opcode == "setorcopyvar" && i.Args.SequenceEqual(new[] { "VAR_0x8001","1" })))
                throw new InvalidDataException(proofId+"_pickup_source:" + label);
            pickups.Add(new(proofId+":" + flag, map.SourceMapSymbol, false, local,
                source.GetProperty("x").GetInt32(), source.GetProperty("y").GetInt32(), source.GetProperty("elevation").GetInt32(),
                label, symbol, item.SourceItemId, item.Id, item.Name, item.Pocket, quantity, flag, flagId));
        }
        foreach (var row in contracts.GetProperty("hiddenItems").EnumerateArray())
        {
            var source = row.GetProperty("event");
            var map = regionMaps[resolveMap(row.GetProperty("map").GetString()!)];
            string symbol = source.GetProperty("item").GetString()!,flag = source.GetProperty("flag").GetString()!;
            var item = Item(symbol);
            if (source.GetProperty("quantity").GetInt32() != 1 || source.GetProperty("underfoot").GetBoolean() ||
                !map.Events.GetProperty("bg_events").EnumerateArray().Any(e => JsonElement.DeepEquals(e, source)) ||
                !scripts.TryConstant(flag, out int flagId))
                throw new InvalidDataException(proofId+"_hidden_source:" + flag);
            pickups.Add(new(proofId+":" + flag, map.SourceMapSymbol, true, null,
                source.GetProperty("x").GetInt32(), source.GetProperty("y").GetInt32(), source.GetProperty("elevation").GetInt32(),
                "EventScript_HiddenItemScript", symbol, item.SourceItemId, item.Id, item.Name, item.Pocket, 1, flag, flagId));
        }
        if (pickups.Select(p => p.SourceFlagId).Distinct().Count() != pickups.Count)
            throw new InvalidDataException(proofId+"_pickup_scope");
        return pickups.ToImmutable();
    }

    private FieldMapDefinition Definition(PretWorldMap map)
    {
        var e = map.Events;
        var root = map.SourceMapName + "_MapScripts";
        var header = _scripts.MapHeaders.GetValueOrDefault(root, []);
        string? Target(string kind) => header.FirstOrDefault(x => x.EventKind == kind)?.Target;
        var actors = e.GetProperty("object_events").EnumerateArray().Select((x, index) => CampaignSourceActor(map, x, index)).ToImmutableArray();
        var coords = e.GetProperty("coord_events").EnumerateArray().Select(x => new FieldCoordTrigger(x.GetProperty("x").GetInt32(), x.GetProperty("y").GetInt32(), x.GetProperty("elevation").GetInt32(), x.GetProperty("var").GetString()!, int.Parse(x.GetProperty("var_value").GetString()!, System.Globalization.CultureInfo.InvariantCulture), x.GetProperty("script").GetString()!)).ToImmutableArray();
        var signs = e.GetProperty("bg_events").EnumerateArray().Select(x => RegionalBackgroundTrigger(x, SupportsRegionalFlavor(map.Id))).ToImmutableArray();
        return new(map.Id, map.SourceMapSymbol, actors, coords, signs, new(Target("MAP_SCRIPT_ON_TRANSITION"), Target("MAP_SCRIPT_ON_WARP_INTO_MAP_TABLE"), Target("MAP_SCRIPT_ON_FRAME_TABLE"), Target("MAP_SCRIPT_ON_LOAD"), Target("MAP_SCRIPT_ON_RESUME"), Target("MAP_SCRIPT_ON_RETURN_TO_FIELD")), e.GetProperty("music").GetString()!, e.GetProperty("region_map_section").GetString()!)
        {
            MapType = e.GetProperty("map_type").GetString()!,RequiresFlash = e.GetProperty("requires_flash").GetBoolean()
        };
    }

    public FieldMetatileDefinition? MetatileDefinition(MapId map,int id)
    {
        if (SeafoamRegion.NewMaps.ContainsKey(map)) return SeafoamRegion.MetatileDefinition(map, id);
        if (TrainerFanClub.NewMaps.TryGetValue(map, out var goodRodMap) || LostCaveRegion.NewMaps.TryGetValue(map, out goodRodMap) || SchoolHouseInteriors.NewMaps.TryGetValue(map, out goodRodMap) || OriginalTmAcquisition.NewMaps.TryGetValue(map, out goodRodMap) || FourIslandDayCare.NewMaps.TryGetValue(map, out goodRodMap) || Route5DayCare.NewMaps.TryGetValue(map, out goodRodMap) || Route16FlyHouse.NewMaps.TryGetValue(map, out goodRodMap) || CapeBrinkTutor.NewMaps.TryGetValue(map, out goodRodMap) || SuperRodService.NewMaps.TryGetValue(map, out goodRodMap) || GoodRodService.NewMaps.TryGetValue(map, out goodRodMap) || MoveReminderHouse.NewMaps.TryGetValue(map, out goodRodMap) || MoveDeleterHouse.NewMaps.TryGetValue(map, out goodRodMap) || OriginalTutorRooms.NewMaps.TryGetValue(map, out goodRodMap) || CopycatHouse.NewMaps.TryGetValue(map, out goodRodMap))
        {
            var cell = goodRodMap.Cells.Concat(goodRodMap.BorderCells).FirstOrDefault(c => c.MetatileId == id);
            return cell is null ? null : new(id, cell.BehaviorId, cell.Terrain, cell.EncounterType, cell.LayerType);
        }
        if (NpcTradeContent.NewMaps.TryGetValue(map, out var npcTradeMap) || NpcTradeElyssa.NewMaps.TryGetValue(map, out npcTradeMap) || NpcTradeHaden.NewMaps.TryGetValue(map, out npcTradeMap))
        {
            var cell = npcTradeMap.Cells.Concat(npcTradeMap.BorderCells).FirstOrDefault(c => c.MetatileId == id);
            return cell is null ? null : new(id, cell.BehaviorId, cell.Terrain, cell.EncounterType, cell.LayerType);
        }
        if (FossilLabRegion.NewMaps.TryGetValue(map, out var fossilMap))
        {
            var cell = fossilMap.Cells.Concat(fossilMap.BorderCells).FirstOrDefault(c => c.MetatileId == id);
            return cell is null ? null : new(id, cell.BehaviorId, cell.Terrain, cell.EncounterType, cell.LayerType);
        }
        if (FightingDojoRegion.NewMaps.TryGetValue(map, out var dojoMap))
        {
            var cell = dojoMap.Cells.Concat(dojoMap.BorderCells).FirstOrDefault(c => c.MetatileId == id);
            return cell is null ? null : new(id, cell.BehaviorId, cell.Terrain, cell.EncounterType, cell.LayerType);
        }
        if (TrainerTowerRegion.Maps.ContainsKey(map))
        {
            var cell = TrainerTowerRegion.Layouts.Where(p => p.Key.Map == map).SelectMany(p => p.Value.Cells.Concat(p.Value.BorderCells)).FirstOrDefault(c => c.MetatileId == id);
            return cell is null ? null : new(id, cell.BehaviorId, cell.Terrain, cell.EncounterType, cell.LayerType);
        }
        if(PowerPlantRegion.NewMaps.TryGetValue(map,out var plantMap))
        {
            var cell=plantMap.Cells.Concat(plantMap.BorderCells).FirstOrDefault(c=>c.MetatileId==id);
            return cell is null?null:new(id,cell.BehaviorId,cell.Terrain,cell.EncounterType,cell.LayerType);
        }
        if (EventIslandRegion.NewMaps.TryGetValue(map, out var eventMap))
        {
            var cell = eventMap.Cells.Concat(eventMap.BorderCells).FirstOrDefault(c => c.MetatileId == id);
            return cell is null ? null : new(id, cell.BehaviorId, cell.Terrain, cell.EncounterType, cell.LayerType);
        }
        if (SevenIslandRegion.NewMaps.ContainsKey(map)) return SevenIslandRegion.MetatileDefinition(map, id);
        if (CeruleanCaveRegion.NewMaps.TryGetValue(map, out var cave))
        {
            var cell = cave.Cells.Concat(cave.BorderCells).FirstOrDefault(c => c.MetatileId == id);
            return cell is null ? null : new(id, cell.BehaviorId, cell.Terrain, cell.EncounterType, cell.LayerType);
        }
        if (FiveIslandRegion?.SourceMaps.ContainsKey(map) == true)
            return FiveIslandRegion.MetatileDefinition(map, id);
        if (SixIslandRegion?.SourceMaps.ContainsKey(map) == true)
            return SixIslandRegion.MetatileDefinition(map, id);
        if (FourIslandRegion?.SourceMaps.ContainsKey(map) == true)
            return FourIslandRegion.MetatileDefinition(map, id);
        if (SeviiRubyRegion?.SourceMaps.ContainsKey(map) == true)
            return SeviiRubyRegion.MetatileDefinition(map, id);
        if (PokemonLeagueRegion?.SourceMaps.ContainsKey(map) == true)
            return PokemonLeagueRegion.MetatileDefinition(map, id);
        if (VictoryRoadRegion?.SourceMaps.ContainsKey(map) == true)
            return VictoryRoadRegion.MetatileDefinition(map, id);
        if (ViridianGymRegion?.NewMaps.ContainsKey(map) == true)
            return ViridianGymRegion.MetatileDefinition(map, id);
        if (FirstSeviiRegion?.NewMaps.ContainsKey(map) == true)
            return FirstSeviiRegion.MetatileDefinition(map, id);
        if (CinnabarRegion?.NewMaps.ContainsKey(map) == true)
            return CinnabarRegion.MetatileDefinition(map, id);
        if (SilphRegion?.NewMaps.ContainsKey(map) == true)
            return SilphRegion.MetatileDefinition(map, id);
        if (VermilionGymRegion is not null && map.Value == "map.vermilion-city-gym")
            return VermilionGymRegion.BeamDefinitions.TryGetValue(id, out var tile) ? new(id, tile.Behavior, tile.Terrain, tile.EncounterType, tile.LayerType) : null;
        if (RocketHideoutRegion?.NewMaps.ContainsKey(map) == true)
            return RocketHideoutMetatileDefinition(map, id);
        return MetatileDefinitions?.GetValueOrDefault(id);
    }

    public int? TerrainAt(MapId map,int x,int y) => SourceMaps.TryGetValue(map, out var source) ? source.GetSourceCell(x, y)?.Terrain : null;
    public bool AllowsCycling(MapId map) => SourceMaps.TryGetValue(map, out var source) && source.Events.GetProperty("allow_cycling").GetBoolean();
    public FieldMapDefinition MapDefinition(MapId id) => _definitions[id];
    public MapId MapFromSourceSymbol(string symbol)
    {
        var loaded = SourceMaps.Values.FirstOrDefault(map => map.SourceMapSymbol == symbol);
        if (loaded is not null) return loaded.Id;
        // Region factories resolve the current world's unloaded boundaries.
        if (symbol.StartsWith("MAP_CINNABAR_ISLAND_POKEMON_LAB_", StringComparison.Ordinal)) return PretFossilLabRegion.MapIdFromSourceSymbol(symbol);
        if (symbol == "MAP_SAFFRON_CITY_DOJO") return PretFightingDojoRegion.MapIdFromSourceSymbol(symbol);
        if (symbol.StartsWith("MAP_TRAINER_TOWER_", StringComparison.Ordinal)) return PretTrainerTowerRegion.MapIdFromSourceSymbol(symbol);
        if(symbol == "MAP_SAFFRON_CITY_COPYCATS_HOUSE_1F") return PretCopycatHouse.Floor1MapId;
        if(symbol == "MAP_SAFFRON_CITY_COPYCATS_HOUSE_2F") return PretCopycatHouse.Floor2MapId;
        if(symbol == "MAP_FOUR_ISLAND_HOUSE1") return PretOriginalTutorRooms.BodySlamMapId;
        if(symbol == "MAP_CINNABAR_ISLAND_POKEMON_LAB_RESEARCH_ROOM") return PretOriginalTutorRooms.MetronomeMapId;
        if(symbol == "MAP_FUCHSIA_CITY_HOUSE3") return PretMoveDeleterHouse.HouseMapId;
        if(symbol == "MAP_TWO_ISLAND_HOUSE") return PretMoveReminderHouse.HouseMapId;
        if(symbol == "MAP_ROUTE12_FISHING_HOUSE") return PretSuperRodService.HouseMapId;
        if(symbol is "MAP_ROUTE19" or "MAP_ROUTE20" || symbol.StartsWith("MAP_SEAFOAM_ISLANDS_",StringComparison.Ordinal)) return PretSeafoamRegion.MapIdFromSourceSymbol(symbol);
        if(symbol=="MAP_POWER_PLANT")return PretPowerPlantRegion.MapIdFromSourceSymbol(symbol);
        if (symbol.StartsWith("MAP_NAVEL_ROCK", StringComparison.Ordinal) || symbol.StartsWith("MAP_BIRTH_ISLAND", StringComparison.Ordinal)) return PretEventIslandRegion.MapIdFromSourceSymbol(symbol);
        if (symbol.StartsWith("MAP_SEVEN_ISLAND", StringComparison.Ordinal) || symbol == "MAP_TRAINER_TOWER_LOBBY") return PretSevenIslandRegion.MapIdFromSourceSymbol(symbol);
        if (symbol.StartsWith("MAP_FIVE_ISLAND", StringComparison.Ordinal)) return PretFiveIslandRegion.MapIdFromSourceSymbol(symbol);
        if (symbol.StartsWith("MAP_SIX_ISLAND", StringComparison.Ordinal)) return PretSixIslandRegion.MapIdFromSourceSymbol(symbol);
        if (symbol.StartsWith("MAP_FOUR_ISLAND", StringComparison.Ordinal)) return PretFourIslandRegion.MapIdFromSourceSymbol(symbol);
        if (symbol.StartsWith("MAP_MT_EMBER_", StringComparison.Ordinal) || symbol.StartsWith("MAP_ONE_ISLAND_KINDLE_ROAD", StringComparison.Ordinal))
            return PretSeviiRubyRegion.MapIdFromSourceSymbol(symbol);
        if (symbol.StartsWith("MAP_POKEMON_LEAGUE_", StringComparison.Ordinal) || symbol.StartsWith("MAP_INDIGO_PLATEAU_", StringComparison.Ordinal))
            return PretPokemonLeagueRegion.MapIdFromSourceSymbol(symbol);
        if (symbol.StartsWith("MAP_FUCHSIA_CITY", StringComparison.Ordinal) || symbol is "MAP_ROUTE13" or "MAP_ROUTE14" or "MAP_ROUTE15" || symbol.StartsWith("MAP_ROUTE15_", StringComparison.Ordinal))
            return PretFuchsiaRouteRegion.MapIdFromSourceSymbol(symbol);
        if (symbol is "MAP_ROUTE12" or "MAP_ROUTE16" or "MAP_ROUTE13" or "MAP_ROUTE17" || symbol.StartsWith("MAP_ROUTE12_", StringComparison.Ordinal) || symbol.StartsWith("MAP_ROUTE16_", StringComparison.Ordinal))
            return PretSnorlaxRegion.MapIdFromSourceSymbol(symbol);
        if (symbol.StartsWith("MAP_POKEMON_TOWER_", StringComparison.Ordinal)) return PretPokemonTowerRegion.MapIdFromSourceSymbol(symbol);
        if (symbol == "MAP_VERMILION_CITY_GYM") return new("map.vermilion-city-gym");
        return PretRocketHideoutRegion.MapIdFromSourceSymbol(symbol);
    }
    public ImmutableArray<FieldProgramCondition> Conditions(string table) => _scripts.MapConditions.TryGetValue(table, out var list) ? list.Select(x => new FieldProgramCondition(x.Variable, ConditionValue(table, x.Expected), x.Target)).ToImmutableArray() : [];
    private int ConditionValue(string table,string token)
    {
        if (int.TryParse(token, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value))
            return value;
        if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(token[2..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out value))
            return value;
        if (_scripts.TryConstant(token, out value))
            return value;
        throw ContentJson.Error("map_condition_value", table, "Unknown map condition value: " + token);
    }

    public ImmutableArray<string> Movement(string program) => _scripts.Movements.TryGetValue(program, out var list) ? list.Select(x => x.Opcode).ToImmutableArray() : throw new InvalidDataException("Missing movement program: " + program);
    public StoryProgram? FindProgram(string id) => _scripts.FindProgram(id);
    public StoryMenuDefinition? FindMenu(string id) => _scripts.FindMenu(id);
    public bool TryConstant(string symbol,out int value) => _scripts.TryConstant(symbol, out value);
    public ImmutableArray<string> TextPages(string symbol)
    {
        if (!_text.TryGetValue(symbol, out var raw))
            throw new InvalidDataException("Missing source text: " + symbol);
        if (Regex.IsMatch(raw, @"\\(?![npl])"))
            throw new InvalidDataException("Unsupported text escape: " + symbol);
        var pages = ImmutableArray.CreateBuilder<string>();
        var lines = new List<string>();
        var line = new StringBuilder();
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == '$')
            {
                lines.Add(line.ToString());
                pages.Add(string.Join("\n", lines));
                break;
            }

            if (c == '\\')
            {
                char control = raw[++i];
                lines.Add(line.ToString());
                line.Clear();
                if (control == 'p')
                {
                    pages.Add(string.Join("\n", lines));
                    lines.Clear();
                }
                else if (control == 'l')
                {
                    pages.Add(string.Join("\n", lines));
                    var last = lines[^1];
                    lines.Clear();
                    lines.Add(last);
                }
            }
            else
                line.Append(c);
        }

        if (pages.Count == 0)
            throw new InvalidDataException("Missing text terminator: " + symbol);
        return pages.ToImmutable();
    }
}

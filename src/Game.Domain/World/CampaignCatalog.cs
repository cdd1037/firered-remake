using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.Items;
using Game.Domain.Encounters;
namespace Game.Domain.World;
public sealed record FieldActorDefinition(string LocalId,int SourceLocalId,string GraphicsId,int X,int Y,int Elevation,string MovementType,string ScriptId,string HideFlag,bool IsClone=false,string TrainerType="TRAINER_TYPE_NONE",int TrainerSight=0);
public sealed record FieldCoordTrigger(int X,int Y,int Elevation,string Variable,int Expected,string ScriptId);
public sealed record FieldBackgroundTrigger(int X,int Y,int Elevation,string ScriptId,string? HiddenItem=null,string? HiddenFlag=null,int Quantity=1)
{
    public Direction? PlayerFacing { get; init; }
    public bool Underfoot { get; init; }
}
public sealed record FieldPickupDefinition(string Id,string MapSymbol,bool Hidden,int? SourceLocalId,int X,int Y,int Elevation,
    string Script,string ItemSymbol,int SourceItemId,ItemId Item,string ItemName,string Pocket,int Quantity,string Flag,int SourceFlagId);
public sealed record FieldMapPrograms(string? OnTransition,string? OnWarpTable,string? OnFrameTable,string? OnLoad=null,string? OnResume=null,string? OnReturnToField=null);
public sealed record FieldProgramCondition(string Variable,int Expected,string ScriptId);
public sealed record FieldMapDefinition(MapId Id,string SourceMapSymbol,ImmutableArray<FieldActorDefinition> Actors,
    ImmutableArray<FieldCoordTrigger> CoordTriggers,ImmutableArray<FieldBackgroundTrigger> BackgroundTriggers,FieldMapPrograms Programs,string Music,string RegionMapSection="")
{
    public string MapType { get; init; } = "MAP_TYPE_NONE";
    public bool RequiresFlash { get; init; }
}
public sealed record CampaignBattleContent(TrainerCatalog Trainers,ImmutableSortedDictionary<string,string> MoveNames);
public sealed record CampaignOakRatingProjection(int Seen,int Caught,string RatingTextSymbol,bool KantoComplete);
public sealed record CampaignNationalDexProjection(int Seen,int Caught,bool Complete);
public sealed record CampaignMapMetatileCell(int X,int Y,int SourceMetatileId,int Collision,int Elevation,
    uint RawAttributes,string AtlasPath,int AtlasX,int AtlasY);
public sealed record CampaignMapMetatilePatch(string Label,MapId MapId,ImmutableArray<CampaignMapMetatileCell> Cells);
public sealed record CampaignRocketElevatorFloor(int MenuIndex,string Label,int SourceFloorValue,MapId DestinationMapId,
    int X,int Y,int Elevation,int DestinationWarpId);
public sealed record CampaignRocketElevatorEntrance(MapId SourceMapId,int SourceWarpId,int ElevatorWarpId,
    int ReturnX,int ReturnY,int ReturnElevation);
public sealed record CampaignRocketDynamicExit(int WarpIndex,int X,int Y,int Elevation,int SourceWarpId);
public sealed record CampaignDepartmentElevatorFloor(int MenuIndex,string Label,int SourceFloorValue,MapId DestinationMapId,
    int X,int Y,int Elevation,int DestinationWarpId);
public sealed record CampaignDepartmentElevatorEntrance(MapId SourceMapId,int SourceWarpId,int ElevatorWarpId,
    int ReturnX,int ReturnY,int ReturnElevation);
public sealed record CampaignDepartmentDynamicExit(int WarpIndex,int X,int Y,int Elevation,int SourceWarpId);
public interface ICampaignCatalog:IStoryCatalog
{
    InventoryCatalog Inventory { get; }
    GamePokemonCatalog GamePokemon { get; }
    GameOpponentPokemonCatalog OpponentPokemon { get; }
    MachineCatalog Machines { get; }
    ImmutableDictionary<string, FieldMilestoneCollection> FieldMilestones => ImmutableDictionary<string, FieldMilestoneCollection>.Empty;
    ImmutableDictionary<string, FieldStatFlagUnlocks> StatFlagUnlocks => ImmutableDictionary<string, FieldStatFlagUnlocks>.Empty;
    PokedexAideCatalog PokedexAides => PokedexAideCatalog.Empty;
    EscapeRopePolicy EscapeRoutes { get; }
    string ContentVersion{get;}
    Game.Domain.Pokemon.SourceEasyChatCatalog? MailComposer=>null;
    Game.Domain.Tutorials.TeachyTvCatalog? TeachyTv=>null;
    FameCheckerCatalog? FameChecker=>null;
    VsSeekerCatalog? VsSeeker=>null;
    VsSeekerTrainerCatalog? VsSeekerTrainers=>null;
    ImmutableArray<VsSeekerBattleOccurrence> VsSeekerBattleOccurrences=>[];
    ImmutableDictionary<string,string> VsSeekerTexts=>ImmutableDictionary<string,string>.Empty;
    bool SupportsRegionalFlavor(MapId map)=>false;
    CampaignPokemonContent? PokemonContent=>null;
    CampaignBattleContent? BattleContent=>null;
    OpeningItemsCatalog? OpeningItems=>null;
    QuestItemsCatalog? QuestItems=>null;
    FieldFlyCatalog? FieldFly=>null;
    FieldRockSmashCatalog? FieldRockSmash=>null;
    CeladonGymTrainerCatalog? CeladonGymTrainers=>null;
    FuchsiaRouteTrainerCatalog? FuchsiaRouteTrainers=>null;
    PokemonCatalog? PresetDoublePokemon=>null;
    SourceMechanicsCatalog? PresetDoubleMechanics=>null;
    SourceBattleItemCatalog? PresetDoubleItems=>null;
    SourceTwinsTrainerDefinition? Route8Twins=>null;
    SafariFishingEncounterCatalog? SafariFishing => null;
    ImmutableHashSet<MapId> CyclingRoadSceneInteriors => ImmutableHashSet<MapId>.Empty;
    CyclingRoadTrainerCatalog? CyclingRoadTrainers => null;
    NpcTradePokemonCatalog? NpcTrades=>null;
    SourceMailMessage? NpcTradeMailMessage=>null;
    MapId NpcTradeMap(int tradeId)=>NpcTrades!.Definitions[tradeId].Map;
    FightingDojoTrainerCatalog? FightingDojoTrainers=>null;
    TrainerTowerCatalog? TrainerTowerCatalog=>null;
    GameOpponentPokemonCatalog? TrainerTowerOpponents=>null;
    IFieldMap? TrainerTowerMap(MapId map,TrainerTowerBattleFormat format)=>null;
    ConnectedFieldMap? TrainerTowerConnectedMap(MapId map,TrainerTowerBattleFormat format)=>null;
    ImmutableDictionary<string,int> TrainerTowerNpcGraphics(TrainerTowerBattleFormat format,TrainerTowerFloorTemplate template)=>throw new NotSupportedException("trainer_tower_not_enabled");
    int TrainerTowerTextGender(TrainerTowerFloorTemplate template,int trainerIndex,bool secondDoubleSpeaker)=>throw new NotSupportedException("trainer_tower_not_enabled");
    string TrainerTowerEncounterMusic(TrainerTowerFloorTemplate template,int trainerIndex)=>throw new NotSupportedException("trainer_tower_not_enabled");
    bool HasMapLayouts(MapId map)=>false;
    IFieldMap? MapLayout(MapId map,string layout)=>null;
    ConnectedFieldMap? ConnectedMapLayout(MapId map,string layout)=>null;
    LostCaveTrainerCatalog? LostCaveTrainers=>null;
    SeafoamTrainerCatalog? SeafoamTrainers=>null;
    Route16FlyTrainerCatalog? Route16FlyTrainers=>null;
    MemorialPillarTrainerCatalog? MemorialPillarTrainers=>null;
    IFieldMap? SeafoamMap(MapId map,bool stoppedCurrent)=>null;
    ConnectedFieldMap? SeafoamConnectedMap(MapId map,bool stoppedCurrent)=>null;
    EventIslandFieldData? EventIslandField=>null;
    SevenIslandFieldData? SevenIslandField=>null;
    SevenIslandTrainerCatalog? SevenIslandTrainerParties=>null;
    SevenIslandTrainerCatalog? SevenIslandTrainers=>SevenIslandTrainerParties;
    WaterEncounterCatalog? SevenIslandWaterEncounters=>null;
    LeagueRematchTrainerCatalog? LeagueRematchTrainerParties=>null;
    IReadOnlyDictionary<MapId,Game.Domain.Roaming.RoamerMap>? CelioRoamerMaps=>null;
    FiveIslandTrainerCatalog? FiveIslandTrainers=>null;
    FiveIslandTrainerCatalog? FiveIslandTrainerParties=>FiveIslandTrainers;
    WaterEncounterCatalog? FiveIslandWaterEncounters=>null;
    SixIslandTrainerCatalog? SixIslandTrainers=>null;
    SixIslandTrainerCatalog? SixIslandTrainerParties=>SixIslandTrainers;
    WaterEncounterCatalog? SixIslandWaterEncounters=>null;
    FourIslandTrainerCatalog? FourIslandTrainers=>null;
    FourIslandTrainerCatalog? FourIslandTrainerParties=>FourIslandTrainers;
    WaterEncounterCatalog? FourIslandWaterEncounters=>null;
    SeviiRubyTrainerCatalog? SeviiRubyTrainers=>null;
    SeviiRubyTrainerCatalog? SeviiRubyTrainerParties=>SeviiRubyTrainers;
    WaterEncounterCatalog? SeviiRubyWaterEncounters=>null;
    // Original species IDs include Hoenn entries 277–411 and skip legacy Unown placeholders.
    bool IsValidPokedexSourceSpecies(int sourceSpeciesId) => sourceSpeciesId is >=1 and <=251 or >=277 and <=411;
    // Dex metadata does not imply that a species is admitted for ownership or battle.
    int? SourceSpeciesHeightDecimeters(int sourceSpeciesId) => null;
    string? SourceSpeciesName(int sourceSpeciesId) => PokemonContent?.SpeciesNames.GetValueOrDefault(sourceSpeciesId);
    CampaignNationalDexProjection EvaluateNationalDex(IEnumerable<int> seenSpecies,IEnumerable<int> caughtSpecies)=>throw new NotSupportedException("national_dex_not_enabled");
    PokemonLeagueTrainerCatalog? PokemonLeagueTrainerParties=>null;
    PokemonLeagueMartStock? PokemonLeagueMartStock=>null;
    VictoryRoadTrainerCatalog? VictoryRoadTrainerParties=>null;
    LeagueRivalTrainerCatalog? LeagueRivalTrainerParties=>null;
    ViridianGymTrainerCatalog? ViridianGymTrainerParties=>null;
    FirstSeviiTrainerCatalog? FirstSeviiTrainerParties=>null;
    WaterEncounterCatalog? FirstSeviiWaterEncounters=>null;
    FirstSeviiItemCatalog? FirstSeviiItems=>null;
    CinnabarTrainerCatalog? CinnabarTrainerParties=>null;
    SaffronGymTrainerCatalog? SaffronGymTrainerParties=>null;
    bool SupportsTeleportPads(MapId map)=>SilphFieldRules.IsSilphMap(map) && SilphWarpPads.Any(p=>p.SourceMapId==map);
    SilphTrainerCatalog? SilphTrainerParties=>null;
    ImmutableArray<SilphWarpPad> SilphWarpPads=>[];
    ImmutableArray<SilphDoorDefinition> SilphDoors=>[];
    ImmutableArray<CampaignSilphElevatorFloor> SilphElevatorFloors=>[];
    ImmutableArray<CampaignSilphElevatorEntrance> SilphElevatorEntrances=>[];
    ImmutableArray<CampaignSilphDynamicExit> SilphDynamicExits=>[];
    IReadOnlyDictionary<MapId,WaterEncounterTable>? WaterEncounters=>null;
    FuchsiaGymTrainerCatalog? FuchsiaGymTrainerParties=>null;
    bool IsFuchsiaRouteWarpAllowed(MapId map,int sourceWarpId)=>true;
    Game.Domain.Slots.SlotMachineSessionContract? GameCornerSlots=>null;
    GameCornerPrizeCatalog? GameCornerPrizes=>null;
    string? DeferredRegionalServiceReason(MapId map,string script)=>null;
    NameRaterSourceContent? NameRaterSource=>null;
    MoveTutorCatalog? MoveTutors=>null;
    MoveDeleterCatalog? MoveDeleter=>null;
    MoveReminderCatalog? MoveReminder=>null;
    PokemonBreedingData? BreedingMetadata=>null;
    IReadOnlyDictionary<int,string> DayCareCompatibilityTextSymbols=>ImmutableDictionary<int,string>.Empty;
    DepartmentStoreOrdinaryStock? DepartmentStoreOrdinaryStock=>null;
    DepartmentStoreCounterStock? DepartmentStoreCounter(string stockId)=>null;
    PokeDollShopStock? PokeDollShopStock=>null;
    ImmutableArray<CampaignDepartmentElevatorFloor> DepartmentStoreElevatorFloors=>[];
    ImmutableArray<CampaignDepartmentElevatorEntrance> DepartmentStoreElevatorEntrances=>[];
    ImmutableArray<CampaignDepartmentDynamicExit> DepartmentStoreDynamicExits=>[];
    int DepartmentStoreElevatorShakeFrames(int from,int to)=>throw new NotSupportedException();
    int DepartmentStoreElevatorWindowCycles(int distance)=>throw new NotSupportedException();
    bool IsSnorlaxStablePosition(MapId map,int x,int y)=>true;
    bool IsNpcTradeStablePosition(MapId map,int x,int y)=>true;
    bool IsSnorlaxWarpAllowed(MapId map,int sourceWarpId)=>true;
    PokemonTowerTrainerCatalog? PokemonTowerTrainers=>null;
    ImmutableArray<int> PokemonTowerInitiallySetFlags=>[];
    RocketHideoutTrainerCatalog? RocketHideoutTrainers=>null;
    ImmutableArray<int> RocketHideoutInitiallySetFlags=>[];
    ImmutableArray<CampaignMapMetatilePatch> RocketHideoutMetatilePatches=>[];
    ImmutableArray<CampaignRocketElevatorFloor> RocketHideoutElevatorFloors=>[];
    ImmutableArray<CampaignRocketElevatorEntrance> RocketHideoutElevatorEntrances=>[];
    ImmutableArray<CampaignRocketDynamicExit> RocketHideoutDynamicExits=>[];
    ImmutableArray<CampaignMapMetatilePatch> RocketHideoutInitialPatches(bool entranceOpened,bool b1GuardDefeated,bool bothB4GuardsDefeated)=>[];
    CeladonApproachTrainerCatalog? CeladonApproachTrainers=>null;
    Route11TrainerCatalog? Route11Trainers=>null;
    RockTunnelTrainerCatalog? RockTunnelTrainers=>null;
    ImmutableArray<MachineItem> CityServiceItems=>[];
    IReadOnlyDictionary<MapId,OldRodEncounterTable>? OldRodEncounters=>null;
    ImmutableDictionary<MapId,OldRodEncounterTable> SuperRodEncounters=>ImmutableDictionary<MapId,OldRodEncounterTable>.Empty;
    FishingTaskProfile? FishingTables=>null;
    ImmutableDictionary<MapId,OldRodEncounterTable> GoodRodEncounters=>ImmutableDictionary<MapId,OldRodEncounterTable>.Empty;
    int? TerrainAt(MapId map,int x,int y)=>null;
    bool AllowsCycling(MapId map)=>false;
    VermilionGymTrainerCatalog? VermilionGymTrainers=>null;
    SsAnneTrainerCatalog? SsAnneTrainers=>null;
    VermilionTrainerCatalog? VermilionTrainers=>null;
    BillTrainerCatalog? BillTrainers=>null;
    CeruleanTrainerCatalog? CeruleanTrainers=>null;
    MtMoonTrainerCatalog? MtMoonTrainers=>null;
    Route3TrainerCatalog? Route3Trainers=>null;
    bool IsRoute3StablePosition(MapId map,int x,int y)=>false;
    Route22TrainerCatalog? Route22Trainers=>null;
    bool IsRoute22StablePosition(MapId map,int x,int y)=>false;
    CampaignOakRatingProjection EvaluateOakRating(IEnumerable<int> seenSpecies,IEnumerable<int> caughtSpecies)=>throw new NotSupportedException("oak_rating_not_enabled");
    ImmutableArray<FieldPickupDefinition> FieldPickups=>[];
    ImmutableArray<FieldItemServiceDefinition> FieldItemServices=>[];
    EarlyShopCatalog? EarlyShop=>null;
    OrdinaryMartCatalog OrdinaryMarts => OrdinaryMartCatalog.Empty;
    ForestTrainerCatalog? ForestTrainers=>null;
    PewterTrainerCatalog? PewterTrainers=>null;
    IReadOnlyDictionary<MapId,EarlyLandEncounterTable>? LandEncounters=>null;
    IReadOnlyDictionary<int,FieldMetatileDefinition>? MetatileDefinitions=>null;
    FieldMetatileDefinition? MetatileDefinition(MapId map,int id)=>MetatileDefinitions?.GetValueOrDefault(id);
    IReadOnlyDictionary<MapId,ConnectedFieldMap>? ConnectedFieldMaps=>null;
    IReadOnlyDictionary<MapId,IFieldMap> Maps{get;}
    FieldMapDefinition MapDefinition(MapId id);
    MapId MapFromSourceSymbol(string symbol);
    ImmutableArray<FieldProgramCondition> Conditions(string table);
    ImmutableArray<string> Movement(string program);
    ImmutableArray<string> TextPages(string symbol);
}

/// <summary>Bounded source GetInteractedMetatileScript flavor and wall-map dispatch.</summary>
public static class RegionalFlavorRules
{
    private static readonly ImmutableDictionary<int,string> Labels = new Dictionary<int,string>
    {
        [0x81]="EventScript_Bookshelf", [0x82]="EventScript_PokeMartShelf",
        [0x86]="EventScript_PlayerFacingTVScreen", [0x87]="EventScript_PokecenterSign",
        [0x88]="EventScript_PokemartSign", [0x89]="EventScript_Cabinet", [0x8a]="EventScript_Kitchen",
        [0x90]="EventScript_Food", [0x98]="EventScript_AdvertisingPoster",
        [0x9c]="EventScript_PolishedWindow", [0x9d]="EventScript_BeautifulSkyWindow",
        [0x9f]="EventScript_NeatlyLinedUpTools"
    }.ToImmutableDictionary();
    // Only simple MSGBOX_SIGN/end programs belong to the flavor closure validator.
    public static IEnumerable<string> Scripts => Labels.Values;
    public static string? ScriptFor(int behavior, Direction facing) =>
        !Enum.IsDefined(facing) || behavior is 0x86 or 0x87 or 0x88 && facing != Direction.North
            ? null : behavior == 0x85 ? "EventScript_WallTownMap" : Labels.GetValueOrDefault(behavior);
}

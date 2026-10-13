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
public sealed record FieldMapPrograms(string? OnTransition,string? OnWarpTable,string? OnFrameTable,string? OnLoad=null,string? OnResume=null);
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
    string ContentVersion{get;}
    bool RegionalFlavorEnabled=>false;
    bool SupportsRegionalFlavor(MapId map)=>false;
    CampaignPokemonContent? PokemonContent=>null;
    CampaignBattleContent? BattleContent=>null;
    OpeningItemsCatalog? OpeningItems=>null;
    QuestItemsCatalog? QuestItems=>null;
    bool CaptureEnabled=>false;
    bool ViridianCenterEnabled=>false;
    bool OldManTutorialEnabled=>false;
    bool PartyTrainingEnabled=>false;
    bool ForestEnabled=>false;
    bool ForestTrainersEnabled=>false;
    bool PewterEnabled=>false;
    bool EarlyStarterEnabled=>false;
    bool EarlyPartyEnabled=>false;
    bool PokemonStorageEnabled=>false;
    bool RepelEnabled=>false;
    bool TownMapEnabled=>false;
    bool OakRatingEnabled=>false;
    bool Route22Enabled=>false;
    bool Route3Enabled=>false;
    bool MagikarpEnabled=>false;
    bool MtMoonEnabled=>false;
    bool CeruleanEnabled=>false;
    bool CityServicesEnabled=>false;
    bool Route11Enabled=>false;
    bool CeladonGymEnabled=>false;
    CeladonGymTrainerCatalog? CeladonGymTrainers=>null;
    bool RegionalServicesEnabled=>false;
    bool RegionalInteriorServicesEnabled=>false;
    bool FuchsiaRouteEnabled=>false;
    FuchsiaRouteTrainerCatalog? FuchsiaRouteTrainers=>null;
    bool PresetDoublesEnabled=>false;
    PokemonCatalog? PresetDoublePokemon=>null;
    SourceMechanicsCatalog? PresetDoubleMechanics=>null;
    SourceBattleItemCatalog? PresetDoubleItems=>null;
    SourceTwinsTrainerDefinition? Route8Twins=>null;
    bool FuchsiaGymEnabled=>false;
    bool SafariEnabled=>false;
    bool PokemonLeagueEnabled=>false;
    bool NationalDexEnabled=>false;
    bool SixIslandEnabled=>false;
    SixIslandOwnedPokemonCatalog? SixIslandOwnership=>null;
    SixIslandOpponentPokemonCatalog? SixIslandOpponents=>null;
    SixIslandTrainerCatalog? SixIslandTrainers=>null;
    SixIslandTrainerCatalog? SixIslandTrainerParties=>SixIslandTrainers;
    SixIslandShopCatalog? SixIslandShop=>null;
    SixIslandMachineCatalog? SixIslandMachines=>null;
    SixIslandWaterEncounterCatalog? SixIslandWaterEncounters=>null;
    bool FourIslandEnabled=>false;
    FourIslandOwnedPokemonCatalog? FourIslandOwnership=>null;
    FourIslandOpponentPokemonCatalog? FourIslandOpponents=>null;
    FourIslandTrainerCatalog? FourIslandTrainers=>null;
    FourIslandTrainerCatalog? FourIslandTrainerParties=>FourIslandTrainers;
    FourIslandShopCatalog? FourIslandShop=>null;
    FourIslandMachineCatalog? FourIslandMachines=>null;
    FourIslandWaterEncounterCatalog? FourIslandWaterEncounters=>null;
    bool SeviiRubyEnabled=>false;
    SeviiRubyOwnedPokemonCatalog? SeviiRubyOwnership=>null;
    SeviiRubyOpponentPokemonCatalog? SeviiRubyOpponents=>null;
    SeviiRubyTrainerCatalog? SeviiRubyTrainers=>null;
    SeviiRubyTrainerCatalog? SeviiRubyTrainerParties=>SeviiRubyTrainers;
    SeviiRubyWaterEncounterCatalog? SeviiRubyWaterEncounters=>null;
    CampaignNationalDexProjection EvaluateNationalDex(IEnumerable<int> seenSpecies,IEnumerable<int> caughtSpecies)=>throw new NotSupportedException("national_dex_not_enabled");
    PokemonLeagueTrainerCatalog? PokemonLeagueTrainerParties=>null;
    PokemonLeagueOpponentPokemonCatalog? PokemonLeagueOpponents=>null;
    PokemonLeagueMartStock? PokemonLeagueMartStock=>null;
    bool VictoryRoadEnabled=>false;
    VictoryRoadTrainerCatalog? VictoryRoadTrainerParties=>null;
    VictoryRoadOpponentPokemonCatalog? VictoryRoadOpponents=>null;
    bool LeagueRivalEnabled=>false;
    LeagueRivalTrainerCatalog? LeagueRivalTrainerParties=>null;
    bool ViridianGymEnabled=>false;
    ViridianGymTrainerCatalog? ViridianGymTrainerParties=>null;
    bool FirstSeviiEnabled=>false;
    FirstSeviiTrainerCatalog? FirstSeviiTrainerParties=>null;
    FirstSeviiWaterEncounterCatalog? FirstSeviiWaterEncounters=>null;
    FirstSeviiItemCatalog? FirstSeviiItems=>null;
    bool CinnabarEnabled=>false;
    CinnabarOwnedPokemonCatalog? CinnabarOwnership=>null;
    CinnabarOpponentPokemonCatalog? CinnabarOpponents=>null;
    CinnabarTrainerCatalog? CinnabarTrainerParties=>null;
    bool SaffronGymEnabled=>false;
    SaffronGymTrainerCatalog? SaffronGymTrainerParties=>null;
    SaffronGymOpponentPokemonCatalog? SaffronGymOpponents=>null;
    bool SupportsTeleportPads(MapId map)=>SilphEnabled && SilphFieldRules.IsSilphMap(map) && SilphWarpPads.Any(p=>p.SourceMapId==map);
    bool SilphEnabled=>false;
    SilphOwnedPokemonCatalog? SilphOwnership=>null;
    SilphOpponentPokemonCatalog? SilphOpponents=>null;
    SilphTrainerCatalog? SilphTrainerParties=>null;
    ImmutableArray<SilphWarpPad> SilphWarpPads=>[];
    ImmutableArray<SilphDoorDefinition> SilphDoors=>[];
    ImmutableArray<CampaignSilphElevatorFloor> SilphElevatorFloors=>[];
    ImmutableArray<CampaignSilphElevatorEntrance> SilphElevatorEntrances=>[];
    ImmutableArray<CampaignSilphDynamicExit> SilphDynamicExits=>[];
    bool SurfEnabled=>false;
    SurfOwnedPokemonCatalog? SurfOwnership=>null;
    IReadOnlyDictionary<MapId,WaterEncounterTable>? WaterEncounters=>null;
    FuchsiaGymTrainerCatalog? FuchsiaGymTrainerParties=>null;
    bool IsFuchsiaRouteWarpAllowed(MapId map,int sourceWarpId)=>true;
    string? DeferredRegionalServiceReason(MapId map,string script)=>null;
    NameRaterSourceContent? NameRaterSource=>null;
    DepartmentStoreOrdinaryStock? DepartmentStoreOrdinaryStock=>null;
    ImmutableArray<CampaignDepartmentElevatorFloor> DepartmentStoreElevatorFloors=>[];
    ImmutableArray<CampaignDepartmentElevatorEntrance> DepartmentStoreElevatorEntrances=>[];
    ImmutableArray<CampaignDepartmentDynamicExit> DepartmentStoreDynamicExits=>[];
    int DepartmentStoreElevatorShakeFrames(int from,int to)=>throw new NotSupportedException();
    int DepartmentStoreElevatorWindowCycles(int distance)=>throw new NotSupportedException();
    bool SnorlaxEnabled=>false;
    bool IsSnorlaxStablePosition(MapId map,int x,int y)=>true;
    bool IsSnorlaxWarpAllowed(MapId map,int sourceWarpId)=>true;
    bool PokemonTowerEnabled=>false;
    PokemonTowerTrainerCatalog? PokemonTowerTrainers=>null;
    ImmutableArray<int> PokemonTowerInitiallySetFlags=>[];
    bool RocketHideoutEnabled=>false;
    RocketHideoutTrainerCatalog? RocketHideoutTrainers=>null;
    ImmutableArray<int> RocketHideoutInitiallySetFlags=>[];
    ImmutableArray<CampaignMapMetatilePatch> RocketHideoutMetatilePatches=>[];
    ImmutableArray<CampaignRocketElevatorFloor> RocketHideoutElevatorFloors=>[];
    ImmutableArray<CampaignRocketElevatorEntrance> RocketHideoutElevatorEntrances=>[];
    ImmutableArray<CampaignRocketDynamicExit> RocketHideoutDynamicExits=>[];
    ImmutableArray<CampaignMapMetatilePatch> RocketHideoutInitialPatches(bool entranceOpened,bool b1GuardDefeated,bool bothB4GuardsDefeated)=>[];
    bool CeladonApproachEnabled=>false;
    CeladonApproachTrainerCatalog? CeladonApproachTrainers=>null;
    bool RockTunnelEnabled=>false;
    Route11TrainerCatalog? Route11Trainers=>null;
    RockTunnelTrainerCatalog? RockTunnelTrainers=>null;
    ImmutableArray<PewterMachineItem> CityServiceItems=>[];
    IReadOnlyDictionary<MapId,OldRodEncounterTable>? OldRodEncounters=>null;
    int? TerrainAt(MapId map,int x,int y)=>null;
    bool AllowsCycling(MapId map)=>false;
    bool VermilionGymEnabled=>false;
    VermilionGymTrainerCatalog? VermilionGymTrainers=>null;
    bool SsAnneEnabled=>false;
    SsAnneTrainerCatalog? SsAnneTrainers=>null;
    bool VermilionEnabled=>false;
    VermilionTrainerCatalog? VermilionTrainers=>null;
    bool BillEnabled=>false;
    BillTrainerCatalog? BillTrainers=>null;
    CeruleanTrainerCatalog? CeruleanTrainers=>null;
    MtMoonTrainerCatalog? MtMoonTrainers=>null;
    Route3TrainerCatalog? Route3Trainers=>null;
    bool IsRoute3StablePosition(MapId map,int x,int y)=>false;
    Route22TrainerCatalog? Route22Trainers=>null;
    bool IsRoute22StablePosition(MapId map,int x,int y)=>false;
    CampaignOakRatingProjection EvaluateOakRating(IEnumerable<int> seenSpecies,IEnumerable<int> caughtSpecies)=>throw new NotSupportedException("oak_rating_not_enabled");
    ImmutableArray<FieldPickupDefinition> FieldPickups=>[];
    EarlyShopCatalog? EarlyShop=>null;
    ForestTrainerCatalog? ForestTrainers=>null;
    PewterTrainerCatalog? PewterTrainers=>null;
    PewterMachineCatalog? PewterMachines=>null;
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

/// <summary>Bounded source GetInteractedMetatileScript flavor subset; service behaviors remain unadmitted.</summary>
public static class RegionalFlavorRules
{
    private static readonly ImmutableDictionary<int,string> Labels = new Dictionary<int,string>
    {
        [0x81]="EventScript_Bookshelf", [0x82]="EventScript_PokeMartShelf",
        [0x86]="EventScript_PlayerFacingTVScreen", [0x87]="EventScript_PokecenterSign",
        [0x88]="EventScript_PokemartSign", [0x8a]="EventScript_Kitchen",
        [0x90]="EventScript_Food", [0x98]="EventScript_AdvertisingPoster",
        [0x9c]="EventScript_PolishedWindow", [0x9d]="EventScript_BeautifulSkyWindow",
        [0x9f]="EventScript_NeatlyLinedUpTools"
    }.ToImmutableDictionary();
    public static IEnumerable<string> Scripts => Labels.Values;
    public static string? ScriptFor(int behavior, Direction facing) =>
        !Enum.IsDefined(facing) || behavior is 0x86 or 0x87 or 0x88 && facing != Direction.North
            ? null : Labels.GetValueOrDefault(behavior);
}

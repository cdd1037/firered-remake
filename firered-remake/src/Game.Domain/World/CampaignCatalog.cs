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
public sealed record FieldBackgroundTrigger(int X,int Y,int Elevation,string ScriptId,string? HiddenItem=null,string? HiddenFlag=null,int Quantity=1);
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
public interface ICampaignCatalog:IStoryCatalog
{
    string ContentVersion{get;}
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

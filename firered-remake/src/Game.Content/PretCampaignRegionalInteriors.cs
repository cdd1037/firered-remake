using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public static PretCampaignCatalog LoadRegionalInteriorServicesProfile(string root) => Load(root,
        includeStarterEvents:true, includeFirstBattle:true, includeOpeningItems:true,
        includeViridianRegion:true, includeParcelQuest:true, includeCapture:true, includeViridianCenter:true,
        includeOldManTutorial:true, includePartyTraining:true, includeForest:true, includeForestTrainers:true,
        includePewter:true, includeEarlyShop:true, includeEarlyStarter:true, includeEarlyParty:true,
        includePokemonStorage:true, includeFieldPickups:true, includeRepel:true, includeTownMap:true,
        includeOakRating:true, includeRoute22:true, useCompactMapData:true, includeRoute3:true,
        includeMagikarp:true, includeMtMoon:true, includeCerulean:true, includeBill:true, includeVermilion:true,
        includeSsAnne:true, includeVermilionGym:true, includeCityServices:true, includeRoute11:true,
        includeRockTunnel:true, includeCeladonApproach:true, includeCeladonGym:true,
        includeRocketHideout:true, includePokemonTower:true, includeSnorlax:true,
        includeRegionalFlavor:true, includeRegionalServices:true, includeRegionalInteriorServices:true);

    public PretDepartmentStoreContract? DepartmentStoreContract {get;}
    public PretNameRaterContract? NameRaterContract {get;}
    public bool RegionalInteriorServicesEnabled => DepartmentStoreContract is not null && NameRaterContract is not null;
    public NameRaterSourceContent? NameRaterSource => NameRaterContract?.Source;
    public DepartmentStoreOrdinaryStock? DepartmentStoreOrdinaryStock => DepartmentStoreContract is not {} c ? null :
        new(PretDepartmentStoreContract.SourceCommit,PretDepartmentStoreContract.ContractSha256,
            PretDepartmentStoreContract.OrdinaryMapId,c.OrdinaryStock.ClerkScript,
            c.OrdinaryStock.Items.Select(i=>new DepartmentStoreOrdinaryItem(new ItemId(i.StableId),i.SourceItemId,i.Name,i.Price,i.Pocket)).ToImmutableArray(),
            new(c.OrdinaryStock.Source.Path,c.OrdinaryStock.Source.Symbol,c.OrdinaryStock.Source.LineStart,c.OrdinaryStock.Source.LineEnd,c.OrdinaryStock.Source.Sha256));
    public static ImmutableArray<MapId> RegionalInteriorMapIds {get;} = [
        new("map.celadon-city-department-store-1f"),new("map.celadon-city-department-store-2f"),
        new("map.celadon-city-department-store-3f"),new("map.celadon-city-department-store-4f"),
        new("map.celadon-city-department-store-5f"),new("map.celadon-city-department-store-roof"),
        new("map.celadon-city-department-store-elevator"),new("map.celadon-city-hotel"),
        new("map.celadon-city-house-1"),new("map.celadon-city-restaurant"),new("map.lavender-town-house-2")];
    public bool IsRegionalServiceMap(MapId id) => RegionalServicesEnabled && (RegionalServiceMapIds.Contains(id)||RegionalInteriorServicesEnabled&&RegionalInteriorMapIds.Contains(id));
    public ImmutableArray<CampaignDepartmentElevatorFloor> DepartmentStoreElevatorFloors =>
        DepartmentStoreContract?.ElevatorFloors.Select(f=>new CampaignDepartmentElevatorFloor(f.MenuIndex,f.Label,f.SourceFloorValue,
            new(f.DestinationMapId),f.X,f.Y,SourceMaps[new(f.DestinationMapId)].Cells[f.Y*SourceMaps[new(f.DestinationMapId)].Width+f.X].Elevation,f.DestinationWarpId)).ToImmutableArray() ?? [];
    public ImmutableArray<CampaignDepartmentElevatorEntrance> DepartmentStoreElevatorEntrances =>
        DepartmentStoreContract?.ElevatorEntrances.Select(e=>new CampaignDepartmentElevatorEntrance(new(e.SourceMapId),e.SourceWarpIndex,e.ElevatorWarpIndex,
            e.X,e.Y,SourceMaps[new(e.SourceMapId)].Cells[e.Y*SourceMaps[new(e.SourceMapId)].Width+e.X].Elevation)).ToImmutableArray() ?? [];
    public ImmutableArray<CampaignDepartmentDynamicExit> DepartmentStoreDynamicExits => !RegionalInteriorServicesEnabled ? [] :
        SourceMaps[PretRegionalServiceMaps.ElevatorMapId].Warps.Select(w=>new CampaignDepartmentDynamicExit(w.Index,w.X,w.Y,w.Elevation,w.DestinationWarpId)).ToImmutableArray();
    public int DepartmentStoreElevatorShakeFrames(int from,int to) => DepartmentStoreContract?.ElevatorShakeFrames(from,to) ?? throw new NotSupportedException();
    public int DepartmentStoreElevatorWindowCycles(int distance)
    {
        if(DepartmentStoreContract is not {} c || distance is <1 or >4)throw new InvalidDataException("department_elevator_distance");
        return c.Document.GetProperty("elevator").GetProperty("windowTable")[distance].GetInt32();
    }
    public string? DeferredRegionalServiceReason(MapId map,string script)
    {
        if(!RegionalInteriorServicesEnabled)return null;
        if(map.Value=="map.celadon-city-restaurant"&&script=="CeladonCity_Restaurant_EventScript_CoinCaseMan")
            return "regional_service_unavailable: Coin Case service\nis not available yet.";
        var deferred=DepartmentStoreContract!.DeferredServices.FirstOrDefault(d=>d.MapId==map.Value&&d.Script==script);
        return deferred is null ? null : "regional_service_unavailable: "+deferred.Service+"\nis not available yet.";
    }
    private static void ValidateRegionalInteriorComposition(PretRegionalServiceMaps region,PretDepartmentStoreContract store,
        ImmutableDictionary<MapId,PretWorldMap> maps,ImmutableDictionary<MapId,PretWorldMap> predecessor)
    {
        if(maps.Count!=147||!maps.Keys.Except(predecessor.Keys).ToHashSet().SetEquals(RegionalInteriorMapIds)||
            predecessor.Any(p=>!ReferenceEquals(p.Value,maps[p.Key])))throw new InvalidDataException("regional_interiors_composition_scope");
        foreach(var id in RegionalInteriorMapIds)
        {
            var map=maps[id];if(!ReferenceEquals(map,region.SourceMaps[id]))throw new InvalidDataException("regional_interiors_map_identity");
            if(id.Value.StartsWith("map.celadon-city-department-store-",StringComparison.Ordinal))store.ValidateMapEvents(id.Value,map.Events);
            foreach(var warp in map.FieldWarps)
            {
                if(warp.DestinationMapId.Value=="map.dynamic")
                {if(id!=PretRegionalServiceMaps.ElevatorMapId||warp.DestinationWarpId!=127)throw new InvalidDataException("regional_interiors_dynamic_warp");continue;}
                if(!maps.TryGetValue(warp.DestinationMapId,out var target)||warp.DestinationWarpId<0||warp.DestinationWarpId>=target.FieldWarps.Length)
                    throw new InvalidDataException("regional_interiors_static_warp");
            }
        }
    }
}

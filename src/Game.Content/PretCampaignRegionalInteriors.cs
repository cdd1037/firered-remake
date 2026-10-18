using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{

    public PretDepartmentStoreContract DepartmentStoreContract {get;}
    public PretNameRaterContract NameRaterContract {get;}
    public NameRaterSourceContent? NameRaterSource => NameRaterContract?.Source;
    public static ImmutableArray<MapId> RegionalInteriorMapIds {get;} = [
        new("map.celadon-city-department-store-1f"),new("map.celadon-city-department-store-2f"),
        new("map.celadon-city-department-store-3f"),new("map.celadon-city-department-store-4f"),
        new("map.celadon-city-department-store-5f"),new("map.celadon-city-department-store-roof"),
        new("map.celadon-city-department-store-elevator"),new("map.celadon-city-hotel"),
        new("map.celadon-city-house-1"),new("map.celadon-city-restaurant"),new("map.lavender-town-house-2")];
    public bool IsRegionalServiceMap(MapId id) => (RegionalServiceMapIds.Contains(id)||RegionalInteriorMapIds.Contains(id)||GameCornerPrizes is not null&&id==PretGameCornerPrizeRoom.MapId);
    public ImmutableArray<CampaignDepartmentElevatorFloor> DepartmentStoreElevatorFloors =>
        DepartmentStoreContract?.ElevatorFloors.Select(f=>new CampaignDepartmentElevatorFloor(f.MenuIndex,f.Label,f.SourceFloorValue,
            new(f.DestinationMapId),f.X,f.Y,SourceMaps[new(f.DestinationMapId)].GetSourceCell(f.X,f.Y)!.Elevation,f.DestinationWarpId)).ToImmutableArray() ?? [];
    public ImmutableArray<CampaignDepartmentElevatorEntrance> DepartmentStoreElevatorEntrances =>
        DepartmentStoreContract?.ElevatorEntrances.Select(e=>new CampaignDepartmentElevatorEntrance(new(e.SourceMapId),e.SourceWarpIndex,e.ElevatorWarpIndex,
            e.X,e.Y,SourceMaps[new(e.SourceMapId)].GetSourceCell(e.X,e.Y)!.Elevation)).ToImmutableArray() ?? [];
    public ImmutableArray<CampaignDepartmentDynamicExit> DepartmentStoreDynamicExits => SourceMaps[PretRegionalServiceMaps.ElevatorMapId].Warps.Select(w=>new CampaignDepartmentDynamicExit(w.Index,w.X,w.Y,w.Elevation,w.DestinationWarpId)).ToImmutableArray();
    public int DepartmentStoreElevatorShakeFrames(int from,int to) => DepartmentStoreContract?.ElevatorShakeFrames(from,to) ?? throw new NotSupportedException();
    public int DepartmentStoreElevatorWindowCycles(int distance)
    {
        if(DepartmentStoreContract is not {} c || distance is <1 or >4)throw new InvalidDataException("department_elevator_distance");
        return c.Document.GetProperty("elevator").GetProperty("windowTable")[distance].GetInt32();
    }
    public string? DeferredRegionalServiceReason(MapId map,string script)
    {
        if(map.Value==Game.Domain.Events.DepartmentRooftopSource.Map &&
            script is Game.Domain.Events.DepartmentRooftopSource.Girl or Game.Domain.Events.DepartmentRooftopSource.Vending)return null;
        if(map.Value=="map.celadon-city-department-store-3f"&&script=="CeladonCity_DepartmentStore_3F_EventScript_CounterTutor"&&
            MoveTutors?.Definitions.ContainsKey(5)==true)return null;
        if (OrdinaryMarts.ByActor.Any(pair => pair.Key.Map == map && pair.Value.Actor.ScriptId == script)) return null;
        if(map.Value==Game.Domain.Events.GameCornerCoinSource.RestaurantMap &&
            script==Game.Domain.Events.GameCornerCoinSource.CoinCaseMan)return null;
        if(map.Value=="map.celadon-city-restaurant"&&script=="CeladonCity_Restaurant_EventScript_CoinCaseMan")
            return "regional_service_unavailable: Coin Case service\nis not available yet.";
        var deferred=DepartmentStoreContract!.DeferredServices.FirstOrDefault(d=>d.MapId==map.Value&&d.Script==script);
        return deferred is null ? null : "regional_service_unavailable: "+deferred.Service+"\nis not available yet.";
    }
    private static void ValidateRegionalInteriorComposition(PretRegionalServiceMaps region,PretDepartmentStoreContract store,ImmutableDictionary<MapId,PretWorldMap> maps)
    {
        if(!RegionalInteriorMapIds.All(maps.ContainsKey))throw new InvalidDataException("regional_interiors_composition_scope");
        foreach(var id in RegionalInteriorMapIds)
        {
            var map=maps[id];            if(id.Value.StartsWith("map.celadon-city-department-store-",StringComparison.Ordinal))store.ValidateMapEvents(id.Value,map.Events);
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

using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    private readonly ImmutableHashSet<(int X,int Y)> _route4WesternPositions;

    public bool IsRoute3StablePosition(MapId map,int x,int y) => SourceMaps.ContainsKey(map);

    private static ImmutableHashSet<(int X,int Y)> Route4WesternPositions(PretRoute3Region? region)
    {
        if(region is null)return [];
        var map=region.NewMaps[new("map.route-4")];
        var reached=ImmutableHashSet.CreateBuilder<(int X,int Y)>();
        var pending=new Queue<(int X,int Y)>();
        pending.Enqueue((12,6));
        while(pending.TryDequeue(out var position))
        {
            if(reached.Contains(position)||map.GetSourceCell(position.X,position.Y) is not { Collision:0 })continue;
            reached.Add(position);
            pending.Enqueue((position.X-1,position.Y));pending.Enqueue((position.X+1,position.Y));
            pending.Enqueue((position.X,position.Y-1));pending.Enqueue((position.X,position.Y+1));
        }
        if(reached.Count!=176||reached.Any(p=>map.GetSourceCell(p.X,p.Y)!.EncounterType!=0))
            throw new InvalidDataException("route3_western_route4_source_boundary_differs");
        return reached.ToImmutable();
    }

    private static ImmutableDictionary<MapId,EarlyLandEncounterTable> Route3EncounterTables(
        PretForestRegion forest,PretRoute22Region? route22,PretRoute3Region? route3)
    {
        var tables=Route22EncounterTables(forest,route22);
        if(route3 is null)return tables;
        var table=PretRoute3Encounters.Load(route3);
        return tables.Add(table.Map,table);
    }

    private FieldActorDefinition CampaignSourceActor(PretWorldMap map, JsonElement source, int index)
        => SourceActor(source, index);
}

using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    private readonly ImmutableHashSet<(int X,int Y)> _route4WesternPositions;

    public bool IsRoute3StablePosition(MapId map,int x,int y) => Route3Enabled && SourceMaps.ContainsKey(map) &&
        (map.Value != "map.route-4" || MtMoonEnabled || _route4WesternPositions.Contains((x,y)));

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

    private FieldActorDefinition CampaignSourceActor(PretWorldMap map,JsonElement source,int index)
    {
        if(CeladonApproachRegion is {} approach && map.SourceMapSymbol=="MAP_CELADON_CITY" && source.GetProperty("type").GetString()=="clone")
        {
            if(source.GetProperty("target_map").GetString()!="MAP_ROUTE16" || source.GetProperty("target_local_id").GetString()!="LOCALID_ROUTE16_CUT_TREE")
                throw new InvalidDataException("celadon_border_clone_target_differs");
            var celadonCloneTarget=approach.ExternalRoute16Events.GetProperty("object_events").EnumerateArray().Select((value,i)=>(value,i))
                .Single(x=>x.value.TryGetProperty("local_id",out var local)&&local.GetString()=="LOCALID_ROUTE16_CUT_TREE");
            return SourceActor(celadonCloneTarget.value,celadonCloneTarget.i) with{X=source.GetProperty("x").GetInt32(),Y=source.GetProperty("y").GetInt32(),IsClone=true};
        }
        if(CeruleanRegion is {} cerulean && map.SourceMapSymbol=="MAP_CERULEAN_CITY" && source.GetProperty("type").GetString()=="clone")
        {
            var clone=cerulean.NpcSources.GetProperty("clones").EnumerateArray().Single(c=>c.GetProperty("map").GetString()==map.SourceMapSymbol&&c.GetProperty("localId").GetInt32()==index+1);
            if(!JsonElement.DeepEquals(clone.GetProperty("declaration"),source)||source.GetProperty("target_map").GetString()!="MAP_ROUTE9")
                throw new InvalidDataException("cerulean_clone_source_differs");
            string id=source.GetProperty("target_local_id").GetString()!;
            var cloneTemplate=cerulean.ExternalRoute9Events.GetProperty("object_events").EnumerateArray().Select((value,i)=>(value,i)).Single(pair=>pair.value.TryGetProperty("local_id",out var local)&&local.GetString()==id);
            if(!JsonElement.DeepEquals(cloneTemplate.value,clone.GetProperty("target")))throw new InvalidDataException("cerulean_clone_target_differs");
            return SourceActor(cloneTemplate.value,cloneTemplate.i) with{X=source.GetProperty("x").GetInt32(),Y=source.GetProperty("y").GetInt32(),IsClone=true};
        }
        if(Route3Region is null||source.GetProperty("type").GetString()!="clone"||
            SourceMaps.ContainsKey(MapFromSourceSymbol(source.GetProperty("target_map").GetString()!)))
            return SourceActor(source,index);

        // Route4 has an original Cerulean guard clone, although Cerulean itself is outside this profile.
        // The importer retains the authenticated template; it does not require admitting the target map.
        var entries=Route3Region.Dependencies.GetProperty("objects").EnumerateArray().Where(x=>
            x.GetProperty("map").GetString()==map.SourceMapSymbol&&
            x.GetProperty("sourceObjectIndex").GetInt32()==index).ToArray();
        if(entries.Length!=1||!JsonElement.DeepEquals(entries[0].GetProperty("object"),source)||
            !entries[0].TryGetProperty("cloneTarget",out var target))
            throw new InvalidDataException("route3_clone_dependency_differs");
        var template=target.GetProperty("object");
        var identity=target.GetProperty("sourceRef");
        string symbol=source.GetProperty("target_local_id").GetString()!,path=identity.GetProperty("path").GetString()!;
        int targetIndex=target.GetProperty("sourceObjectIndex").GetInt32();
        if(target.GetProperty("map").GetString()!=source.GetProperty("target_map").GetString()||
            target.GetProperty("localId").GetInt32()!=targetIndex+1||targetIndex<0||
            template.GetProperty("local_id").GetString()!=symbol||template.GetProperty("type").GetString()!="object"||
            identity.GetProperty("kind").GetString()!="pret"||identity.GetProperty("repository").GetString()!="pret/pokefirered"||
            identity.GetProperty("commit").GetString()!=PretWorldMap.SourceCommit||identity.GetProperty("symbol").GetString()!=symbol||
            !Route3Region.SourceFiles.Any(f=>f.Path==path&&f.Sha256==identity.GetProperty("sha256").GetString()))
            throw new InvalidDataException("route3_clone_source_identity_differs");
        return SourceActor(template,targetIndex) with{X=source.GetProperty("x").GetInt32(),
            Y=source.GetProperty("y").GetInt32(),IsClone=true};
    }
}

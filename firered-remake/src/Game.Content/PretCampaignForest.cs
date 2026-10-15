using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    private static ImmutableDictionary<MapId,EarlyLandEncounterTable> ForestEncounterTables(PretForestRegion forest)
    {
        var tables=ImmutableDictionary.CreateBuilder<MapId,EarlyLandEncounterTable>();
        tables.Add(EarlyLandEncounterRules.Route1Table.Map,EarlyLandEncounterRules.Route1Table);
        foreach(var entry in forest.Dependencies.GetProperty("encounters").EnumerateArray())
        {
            var source=entry.GetProperty("sourceRef");var table=entry.GetProperty("table");var land=table.GetProperty("land_mons");
            var weights=entry.GetProperty("slotWeights").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            var slots=land.GetProperty("mons").EnumerateArray().Select((m,i)=>new EarlyLandEncounterSlot(i,new SpeciesId("pret:"+m.GetProperty("species").GetString()),checked((byte)m.GetProperty("min_level").GetInt32()),checked((byte)m.GetProperty("max_level").GetInt32()),weights[i])).ToImmutableArray();
            var value=new EarlyLandEncounterTable(PretForestRegion.MapIdFromSourceSymbol(table.GetProperty("map").GetString()!),source.GetProperty("commit").GetString()!,source.GetProperty("path").GetString()!,source.GetProperty("symbol").GetString()!,land.GetProperty("encounter_rate").GetInt32(),slots);
            EarlyLandEncounterRules.ValidateTable(value);tables.Add(value.Map,value);
        }
        if(tables.Count!=3)throw new InvalidDataException("forest_encounter_table_count");
        return tables.ToImmutable();
    }

    private FieldActorDefinition SourceActor(JsonElement source,int index)
    {
        int x=source.GetProperty("x").GetInt32(),y=source.GetProperty("y").GetInt32();bool clone=source.GetProperty("type").GetString()=="clone";
        if(clone)
        {
            var target=SourceMaps[MapFromSourceSymbol(source.GetProperty("target_map").GetString()!)];
            string id=source.GetProperty("target_local_id").GetString()!;
            var template=target.Events.GetProperty("object_events").EnumerateArray().Select((value,i)=>(value,i)).Single(pair=>pair.value.TryGetProperty("local_id",out var local)&&local.GetString()==id);
            source=template.value;index=template.i;
        }
        var localId=source.TryGetProperty("local_id",out var name)?name.GetString()!:"local:"+(index+1);
        string trainer=source.TryGetProperty("trainer_type",out var type)?type.GetString()!:"TRAINER_TYPE_NONE";
        int sight=0;if(source.TryGetProperty("trainer_sight_or_berry_tree_id",out var range))
        {if(range.ValueKind==JsonValueKind.Number)sight=range.GetInt32();else if(!int.TryParse(range.GetString(),out sight))sight=0;}
        return new(localId,index+1,source.GetProperty("graphics_id").GetString()!,x,y,source.GetProperty("elevation").GetInt32(),source.GetProperty("movement_type").GetString()!,source.GetProperty("script").GetString()!,source.GetProperty("flag").GetString()!,clone,trainer,sight);
    }
}

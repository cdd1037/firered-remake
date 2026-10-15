using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    private static IEnumerable<FieldPickupDefinition> MtMoonPickupDefinitions(PretMtMoonItemsDocument items,EarlyShopCatalog shop,PewterMachineCatalog machines,PretStoryCompilation scripts)
    {
        foreach(var p in items.Pickups)
        {
            var metadata=shop.Items.FirstOrDefault(i=>i.Id==p.Item);
            var tm=items.Machines.FirstOrDefault(m=>m.Machine.Item.Id==p.Item);
            int number=metadata?.SourceItemId??tm?.Machine.Item.SourceItemId??throw new InvalidDataException("mtmoon_pickup_item_missing");
            string name=metadata?.Name??tm!.Machine.Item.Name;
            string pocket=metadata?.Pocket??tm!.Machine.Item.Pocket;
            if(!scripts.TryConstant(p.Flag,out int flag))throw new InvalidDataException("mtmoon_pickup_flag_missing: "+p.Flag);
            string map=p.Map switch{"MtMoon_1F"=>"MAP_MT_MOON_1F","MtMoon_B1F"=>"MAP_MT_MOON_B1F","MtMoon_B2F"=>"MAP_MT_MOON_B2F","Route4"=>"MAP_ROUTE4",_=>throw new InvalidDataException("mtmoon_pickup_map_invalid")};
            yield return new("mtmoon:"+p.Flag,map,p.Kind=="hidden",p.LocalObjectId,p.X,p.Y,p.Elevation,p.Script??"EventScript_HiddenItemScript",p.Item.Value[5..],number,p.Item,name,pocket,p.Quantity,p.Flag,flag);
        }
    }

    private static ImmutableDictionary<MapId,EarlyLandEncounterTable> MtMoonEncounterTables(
        PretForestRegion forest,PretRoute22Region? route22,PretRoute3Region? route3,PretMtMoonRegion? moon)
    {
        var tables=Route3EncounterTables(forest,route22,route3);
        if(moon is null)return tables;
        foreach(var expected in MtMoonLandEncounterTables.All)
        {
            var entries=(expected.Map.Value=="map.route-4"?route3!.Encounters:moon.Encounters).EnumerateArray()
                .Where(e=>e.GetProperty("table").GetProperty("base_label").GetString()==expected.SourceSymbol).ToArray();
            if(entries.Length!=1)throw new InvalidDataException("mtmoon_encounter_table_missing");
            var entry=entries[0];var source=entry.GetProperty("sourceRef");var table=entry.GetProperty("table");
            var land=table.GetProperty("land_mons");var weights=entry.GetProperty("slotWeights").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
            var parsed=new EarlyLandEncounterTable(PretMtMoonRegion.MapIdFromSourceSymbol(table.GetProperty("map").GetString()!),                source.GetProperty("commit").GetString()!,source.GetProperty("path").GetString()!,source.GetProperty("symbol").GetString()!,                land.GetProperty("encounter_rate").GetInt32(),land.GetProperty("mons").EnumerateArray().Select((m,i)=>new EarlyLandEncounterSlot(i,new("pret:"+m.GetProperty("species").GetString()),m.GetProperty("min_level").GetByte(),m.GetProperty("max_level").GetByte(),weights[i])).ToImmutableArray());
            EarlyLandEncounterRules.ValidateTable(parsed);
            if(parsed.Map!=expected.Map||parsed.SourcePath!=expected.SourcePath||parsed.SourceSymbol!=expected.SourceSymbol||parsed.RawEncounterRate!=expected.RawEncounterRate||!parsed.Slots.SequenceEqual(expected.Slots))
                throw new InvalidDataException("mtmoon_encounter_source_differs");
            tables=tables.Add(parsed.Map,parsed);
        }
        return tables;
    }
}

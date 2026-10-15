using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;
namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    private static IEnumerable<FieldPickupDefinition> BillPickupDefinitions(PretBillItemsDocument items,MachineCatalog machines,PretStoryCompilation scripts)
    {
        // Hidden Elixir/berries retain authenticated source metadata. Their pocket/use
        // closure is a separate increment; unsupported interactions reject in preflight.
        foreach(var p in items.Pickups.Where(p=>p.Kind=="visible"))
        {
            var item=machines.Machines.Single(m=>m.Machine.Item.Id==p.Item).Machine.Item;
            if(!scripts.TryConstant(p.Flag,out int flag))throw new InvalidDataException("bill_pickup_flag_missing");
            string map=p.Map switch{"Route24"=>"MAP_ROUTE24","Route25"=>"MAP_ROUTE25",_=>throw new InvalidDataException("bill_pickup_map_invalid")};
            yield return new("bill:"+p.Flag,map,false,p.LocalObjectId,p.X,p.Y,p.Elevation,p.Script!,item.SourceItemSymbol,item.SourceItemId,item.Id,item.Name,item.Pocket,p.Quantity,p.Flag,flag);
        }
    }
    private static ImmutableDictionary<MapId,EarlyLandEncounterTable> BillEncounterTables(PretForestRegion forest,PretRoute22Region? route22,PretRoute3Region? route3,PretMtMoonRegion? moon,PretBillRegion? bill)
    {
        var tables=MtMoonEncounterTables(forest,route22,route3,moon);
        if(bill is null)return tables;
        foreach(var expected in BillLandEncounterTables.All)
        {
            var entry=bill.Encounters.GetProperty("maps").EnumerateArray().Single(e=>PretBillRegion.MapIdFromSourceSymbol(e.GetProperty("declaration").GetProperty("map").GetString()!)==expected.Map);
            var table=entry.GetProperty("declaration");var land=table.GetProperty("land_mons");
            var parsed=new EarlyLandEncounterTable(PretBillRegion.MapIdFromSourceSymbol(table.GetProperty("map").GetString()!),land.GetProperty("encounter_rate").GetInt32(),land.GetProperty("mons").EnumerateArray().Select((m,i)=>new EarlyLandEncounterSlot(i,new("pret:"+m.GetProperty("species").GetString()),m.GetProperty("min_level").GetByte(),m.GetProperty("max_level").GetByte(),EarlyLandEncounterRules.SlotWeights[i])).ToImmutableArray());
            EarlyLandEncounterRules.ValidateTable(parsed);
            if(parsed.Map!=expected.Map||parsed.RawEncounterRate!=expected.RawEncounterRate||!parsed.Slots.SequenceEqual(expected.Slots))throw new InvalidDataException("bill_encounter_source_differs");
            tables=tables.Add(parsed.Map,parsed);
        }
        return tables;
    }
}

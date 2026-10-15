using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;
namespace Game.Content;

public sealed partial class PretCampaignCatalog
{

    private static ImmutableDictionary<MapId,EarlyLandEncounterTable> VermilionEncounterTables(PretForestRegion forest,PretRoute22Region? route22,PretRoute3Region? route3,PretMtMoonRegion? moon,PretBillRegion? bill,PretVermilionRegion? vermilion)
    {
        var tables=BillEncounterTables(forest,route22,route3,moon,bill);
        if(vermilion is null)return tables;
        foreach(var expected in VermilionLandEncounterTables.All)
        {
            var entry=vermilion.Encounters.GetProperty("maps").EnumerateArray().Single(e=>e.GetProperty("declaration").GetProperty("base_label").GetString()==expected.SourceSymbol);
            var source=entry.GetProperty("sourceRef");var table=entry.GetProperty("declaration");var land=table.GetProperty("land_mons");
            var parsed=new EarlyLandEncounterTable(PretVermilionRegion.MapIdFromSourceSymbol(table.GetProperty("map").GetString()!),source.GetProperty("commit").GetString()!,source.GetProperty("path").GetString()!,source.GetProperty("symbol").GetString()!,land.GetProperty("encounter_rate").GetInt32(),land.GetProperty("mons").EnumerateArray().Select((m,i)=>new EarlyLandEncounterSlot(i,new("pret:"+m.GetProperty("species").GetString()),m.GetProperty("min_level").GetByte(),m.GetProperty("max_level").GetByte(),EarlyLandEncounterRules.SlotWeights[i])).ToImmutableArray());
            EarlyLandEncounterRules.ValidateTable(parsed);
            if(parsed.Map!=expected.Map||parsed.SourcePath!=expected.SourcePath||parsed.SourceSymbol!=expected.SourceSymbol||parsed.RawEncounterRate!=expected.RawEncounterRate||!parsed.Slots.SequenceEqual(expected.Slots))throw new InvalidDataException("vermilion_encounter_source_differs");
            tables=tables.Add(parsed.Map,parsed);
        }
        return tables;
    }
}

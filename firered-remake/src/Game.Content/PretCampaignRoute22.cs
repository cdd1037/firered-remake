using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Ids;
namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public bool IsRoute22StablePosition(MapId map,int x,int y)=>Route22Enabled && (map.Value!="map.route-23"||VictoryRoadEnabled&&Maps[map].GetTile(x,y) is not null||Route22Region!.FirstBadgeRoute23SourceStops.Contains((x,y)));

    private static ImmutableDictionary<MapId,EarlyLandEncounterTable> Route22EncounterTables(PretForestRegion forest,PretRoute22Region? region)
    {
        var tables=ForestEncounterTables(forest);
        if(region is null)return tables;
        var expected=EarlyLandEncounterRules.Route22Table;
        var rows=region.Encounters.GetProperty("route22LandSlots").EnumerateArray().ToArray();
        if(region.Encounters.GetProperty("route22LandRate").GetInt32()!=expected.RawEncounterRate||rows.Length!=expected.Slots.Length)
            throw new InvalidDataException("route22_encounter_source_differs");
        for(int i=0;i<rows.Length;i++)
        {
            var row=rows[i];var slot=expected.Slots[i];
            if(row.GetProperty("slot").GetInt32()!=i||"pret:"+row.GetProperty("species").GetString()!=slot.Species.Value||
                row.GetProperty("min_level").GetInt32()!=slot.MinimumLevel||row.GetProperty("max_level").GetInt32()!=slot.MaximumLevel||row.GetProperty("weightPercent").GetInt32()!=slot.Weight)
                throw new InvalidDataException("route22_encounter_slot_differs");
        }
        EarlyLandEncounterRules.ValidateTable(expected);
        return tables.Add(expected.Map,expected);
    }
}

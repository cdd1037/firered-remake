using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Ids;
namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public bool IsRoute22StablePosition(MapId map,int x,int y)=>(map.Value!="map.route-23"||Maps[map].GetTile(x,y) is not null);

    private static ImmutableDictionary<MapId,EarlyLandEncounterTable> Route22EncounterTables(PretForestRegion forest,PretRoute22Region? region)
    {
        var tables=ForestEncounterTables(forest);
        if(region is null)return tables;
        var expected=EarlyLandEncounterRules.Route22Table;
        var actual = PretWildEncounterTables.Land(region.Encounters, PretRoute22Region.MapIdFromSourceSymbol).Single();
        if(actual.Map != expected.Map || actual.RawEncounterRate != expected.RawEncounterRate || !actual.Slots.SequenceEqual(expected.Slots))
            throw new InvalidDataException("route22_encounter_source_differs");
        EarlyLandEncounterRules.ValidateTable(expected);
        return tables.Add(expected.Map,expected);
    }
}

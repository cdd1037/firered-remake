using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;

/// <summary>Validated regional water encounter definitions.</summary>
public sealed class FourIslandWaterEncounterCatalog
{
    public SeviiRubyWaterEncounterCatalog Predecessor { get; }
    public ImmutableDictionary<MapId,WaterEncounterTable> Tables { get; }
    private FourIslandWaterEncounterCatalog(SeviiRubyWaterEncounterCatalog predecessor, ImmutableArray<WaterEncounterTable> added)
    { Predecessor = predecessor; Tables = predecessor.Tables.AddRange(added.Select(t => KeyValuePair.Create(t.Map,t))); }
    public static ImmutableArray<WaterEncounterTable> SourceAdditions => FourIslandEncounterTables.Water;
    public static FourIslandWaterEncounterCatalog Authenticate(SeviiRubyWaterEncounterCatalog predecessor, ImmutableArray<WaterEncounterTable> additions)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (predecessor.Tables.Count != 28 || predecessor.Tables.Values.Any(t => !WaterEncounterCatalog.IsValidDefinition(t) && !CinnabarWaterEncounterCatalog.MatchesDefinition(t) && !FirstSeviiWaterEncounterCatalog.MatchesDefinition(t) && !VictoryRoadWaterEncounterCatalog.MatchesDefinition(t) && !SeviiRubyWaterEncounterCatalog.MatchesDefinition(t)) ||
            additions.IsDefault || additions.Length != 3 || additions.Any(t => t is null) || additions.Select(t => t.Map).Distinct().Count() != 3)
            throw new ArgumentException("four_island_water_scope");
        foreach (var actual in additions)
        {
            var expected = SourceAdditions.SingleOrDefault(t => t.Map == actual.Map);
            if (expected is null || actual.Slots.IsDefault || !actual.Slots.SequenceEqual(expected.Slots) ||
                !WaterEncounterCatalog.SameDefinition(actual, expected)) throw new ArgumentException("four_island_water_source");
        }
        var copies = additions.Select(t => t with { }).ToImmutableArray();
        var proof = new FourIslandWaterEncounterCatalog(predecessor,copies);
        return proof;
    }
    public static bool MatchesDefinition(WaterEncounterTable? table) =>
        table is not null && SourceAdditions.Any(expected => WaterEncounterCatalog.SameDefinition(table, expected));
}

using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Encounters;

/// <summary>Exact additional Surf tables, independent of regional ownership/profile inheritance.</summary>
public sealed class SourceTableWaterEncounterCatalog
{
    public static ImmutableArray<WaterEncounterTable> SourceTables => [.. MemorialPillarEncounterTables.Water, .. LostCaveEncounterTables.Water];
    public ImmutableDictionary<MapId, WaterEncounterTable> Tables { get; }
    private SourceTableWaterEncounterCatalog(ImmutableArray<WaterEncounterTable> tables) =>
        Tables = tables.ToImmutableDictionary(t => t.Map);

    public static SourceTableWaterEncounterCatalog Authenticate(ImmutableArray<WaterEncounterTable> tables)
    {
        if (tables.IsDefaultOrEmpty || tables.Length > SourceTables.Length || tables.Any(t => t is null) ||
            tables.Select(t => t.Map).Distinct().Count() != tables.Length)
            throw new ArgumentException("source_table_water_scope");
        foreach (var actual in tables)
        {
            var expected = SourceTables.SingleOrDefault(t => t.Map == actual.Map);
            if (expected is null || actual.Slots.IsDefault || !WaterEncounterCatalog.SameDefinition(actual, expected) ||
                !actual.Slots.SequenceEqual(expected.Slots))
                throw new ArgumentException("source_table_water_source");
        }
        var proof = new SourceTableWaterEncounterCatalog(tables.Select(t => t with { }).ToImmutableArray());
        return proof;
    }

    public static bool MatchesDefinition(WaterEncounterTable? table) =>
        table is not null && SourceTables.Any(expected => WaterEncounterCatalog.SameDefinition(table, expected));
}

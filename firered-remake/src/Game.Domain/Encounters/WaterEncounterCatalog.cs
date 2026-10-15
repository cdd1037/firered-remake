using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Encounters;

/// <summary>Immutable map encounter definitions loaded at the content boundary.</summary>
public sealed class WaterEncounterCatalog
{
    public ImmutableDictionary<MapId, WaterEncounterTable> Tables { get; }
    private WaterEncounterCatalog(ImmutableArray<WaterEncounterTable> tables) => Tables = tables.ToImmutableDictionary(t => t.Map);
    public static WaterEncounterCatalog Authenticate(ImmutableArray<WaterEncounterTable> tables)
    {
        if (tables.IsDefaultOrEmpty || tables.Any(t => !IsValidDefinition(t)) ||
            tables.Select(t => t.Map).Distinct().Count() != tables.Length)
            throw new ArgumentException("water_catalog_scope");
        return new(tables);
    }
    public static bool IsValidDefinition(WaterEncounterTable? table) => table is not null &&
        !string.IsNullOrWhiteSpace(table.Map.Value) && table.RawEncounterRate is >= 1 and <= 100 &&
        !table.Slots.IsDefault && table.Slots.Length == 5 &&
        table.Slots.All(s => s is not null) &&
        table.Slots.Select(s => s.Index).SequenceEqual(Enumerable.Range(0, 5)) &&
        table.Slots.Select(s => s.Weight).SequenceEqual(WaterEncounterRules.SlotWeights) &&
        table.Slots.All(s => s is not null && s.MinimumLevel is >= 1 and <= 100 &&
            s.MaximumLevel >= s.MinimumLevel && s.MaximumLevel <= 100 && !string.IsNullOrWhiteSpace(s.Species.Value));
    public static bool SameDefinition(WaterEncounterTable? actual, WaterEncounterTable expected) =>
        actual is not null && actual.Map == expected.Map && actual.RawEncounterRate == expected.RawEncounterRate &&
        !actual.Slots.IsDefault && actual.Slots.SequenceEqual(expected.Slots);
    public static void ValidateTable(WaterEncounterTable table)
    { if (!IsValidDefinition(table)) throw new ArgumentException("water_table_definition"); }
    public static void ValidateAdmission(IReadOnlyDictionary<MapId, WaterEncounterTable> tables, WaterEncounterTable table)
    {
        ArgumentNullException.ThrowIfNull(tables);
        if (table is null || !tables.TryGetValue(table.Map, out var expected) || !SameDefinition(table, expected))
            throw new ArgumentException("water_table_admission");
    }
}

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Encounters;

/// <summary>Exact21-table source proof. A copied record does not inherit table authentication.</summary>
public sealed class WaterEncounterCatalog
{
    private static readonly ConditionalWeakTable<WaterEncounterTable, WaterEncounterCatalog> Authenticated = new();
    public ImmutableDictionary<MapId, WaterEncounterTable> Tables { get; }
    private WaterEncounterCatalog(ImmutableArray<WaterEncounterTable> tables) => Tables = tables.ToImmutableDictionary(t => t.Map);
    public static WaterEncounterCatalog Authenticate(ImmutableArray<WaterEncounterTable> tables)
    {
        if (tables.IsDefault || tables.Length != 21 || tables.Any(t => t is null || t.Slots.IsDefault || t.Slots.Length != 5 ||
            t.RawEncounterRate is < 1 or > 100 ||
            t.Slots.Any(s => s is null || s.MinimumLevel is < 1 or > 100 || s.MaximumLevel < s.MinimumLevel ||
                s.MaximumLevel > 100 || s.Weight <= 0 || string.IsNullOrWhiteSpace(s.Species.Value)) ||
            !t.Slots.Select(s => s.Index).SequenceEqual(Enumerable.Range(0, 5))))
            throw new ArgumentException("water_catalog_scope");
        // Each proof receives independent roots; caller-side records and clones stay unauthenticated.
        var proof = new WaterEncounterCatalog(tables.Select(t => t with { }).ToImmutableArray());
        foreach (var table in proof.Tables.Values) Authenticated.Add(table, proof);
        return proof;
    }
    public static bool IsAuthenticated(WaterEncounterTable? table) => table is not null && Authenticated.TryGetValue(table,out _);
    public static void ValidateTable(WaterEncounterTable table)
    { if (!IsAuthenticated(table)) throw new ArgumentException("water_table_admission"); }
}

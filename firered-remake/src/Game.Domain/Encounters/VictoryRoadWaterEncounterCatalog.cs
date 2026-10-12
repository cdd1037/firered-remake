using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;

/// <summary>An exact Route23 table gets a new profile proof over FirstSevii27; historical objects retain their proofs.</summary>
public sealed class VictoryRoadWaterEncounterCatalog
{
    private static readonly ConditionalWeakTable<WaterEncounterTable,VictoryRoadWaterEncounterCatalog> Proofs = new();
    public FirstSeviiWaterEncounterCatalog Predecessor { get; }
    public ImmutableDictionary<MapId,WaterEncounterTable> Tables { get; }
    private VictoryRoadWaterEncounterCatalog(FirstSeviiWaterEncounterCatalog predecessor, ImmutableArray<WaterEncounterTable> added)
    { Predecessor = predecessor; Tables = predecessor.Tables.SetItems(added.Select(t => KeyValuePair.Create(t.Map,t))); }
    public static ImmutableArray<WaterEncounterTable> SourceAdditions => VictoryRoadEncounterTables.Water;
    public static VictoryRoadWaterEncounterCatalog Authenticate(FirstSeviiWaterEncounterCatalog predecessor, ImmutableArray<WaterEncounterTable> additions)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (predecessor.Tables.Count != 27 || predecessor.Tables.Values.Any(t => !WaterEncounterCatalog.IsAuthenticated(t) && !CinnabarWaterEncounterCatalog.IsAuthenticated(t) && !FirstSeviiWaterEncounterCatalog.IsAuthenticated(t)) ||
            additions.IsDefault || additions.Length != 1 || additions.Any(t => t is null) || additions.Select(t => t.Map).Distinct().Count() != 1)
            throw new ArgumentException("victory_road_water_scope");
        foreach (var actual in additions)
        {
            var expected = SourceAdditions.SingleOrDefault(t => t.Map == actual.Map);
            if (expected is null || actual.Slots.IsDefault || !actual.Slots.SequenceEqual(expected.Slots) ||
                actual with { Slots = expected.Slots } != expected) throw new ArgumentException("victory_road_water_source");
            // Route23 was imported with the earlier Surf tables while its northern
            // field boundary was closed. Verify the shared source table in full,
            // then register a separate copy for the successor battle protocol.
            if (!predecessor.Tables.TryGetValue(actual.Map,out var previous) ||
                previous.Slots.IsDefault || !previous.Slots.SequenceEqual(expected.Slots) ||
                previous with { Slots = expected.Slots } != expected)
                throw new ArgumentException("victory_road_water_predecessor_mismatch");
        }
        var copies = additions.Select(t => t with { }).ToImmutableArray();
        var proof = new VictoryRoadWaterEncounterCatalog(predecessor,copies);
        foreach (var table in copies) Proofs.Add(table,proof);
        return proof;
    }
    public static bool IsAuthenticated(WaterEncounterTable? table) => table is not null && Proofs.TryGetValue(table,out _);
}

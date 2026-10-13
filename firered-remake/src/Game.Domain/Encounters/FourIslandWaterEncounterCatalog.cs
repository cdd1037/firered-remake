using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;

/// <summary>Three exact Four Island additions over Ruby28; no historical table proof is relabelled.</summary>
public sealed class FourIslandWaterEncounterCatalog
{
    private static readonly ConditionalWeakTable<WaterEncounterTable,FourIslandWaterEncounterCatalog> Proofs = new();
    public SeviiRubyWaterEncounterCatalog Predecessor { get; }
    public ImmutableDictionary<MapId,WaterEncounterTable> Tables { get; }
    private FourIslandWaterEncounterCatalog(SeviiRubyWaterEncounterCatalog predecessor, ImmutableArray<WaterEncounterTable> added)
    { Predecessor = predecessor; Tables = predecessor.Tables.AddRange(added.Select(t => KeyValuePair.Create(t.Map,t))); }
    public static ImmutableArray<WaterEncounterTable> SourceAdditions => FourIslandEncounterTables.Water;
    public static FourIslandWaterEncounterCatalog Authenticate(SeviiRubyWaterEncounterCatalog predecessor, ImmutableArray<WaterEncounterTable> additions)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (predecessor.Tables.Count != 28 || predecessor.Tables.Values.Any(t => !WaterEncounterCatalog.IsAuthenticated(t) && !CinnabarWaterEncounterCatalog.IsAuthenticated(t) && !FirstSeviiWaterEncounterCatalog.IsAuthenticated(t) && !VictoryRoadWaterEncounterCatalog.IsAuthenticated(t) && !SeviiRubyWaterEncounterCatalog.IsAuthenticated(t)) ||
            additions.IsDefault || additions.Length != 3 || additions.Any(t => t is null) || additions.Select(t => t.Map).Distinct().Count() != 3)
            throw new ArgumentException("four_island_water_scope");
        foreach (var actual in additions)
        {
            var expected = SourceAdditions.SingleOrDefault(t => t.Map == actual.Map);
            if (expected is null || actual.Slots.IsDefault || !actual.Slots.SequenceEqual(expected.Slots) ||
                actual with { Slots = expected.Slots } != expected) throw new ArgumentException("four_island_water_source");
        }
        var copies = additions.Select(t => t with { }).ToImmutableArray();
        var proof = new FourIslandWaterEncounterCatalog(predecessor,copies);
        foreach (var table in copies) Proofs.Add(table,proof);
        return proof;
    }
    public static bool IsAuthenticated(WaterEncounterTable? table) => table is not null && Proofs.TryGetValue(table,out _);
}

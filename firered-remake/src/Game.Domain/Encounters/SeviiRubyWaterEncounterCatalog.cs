using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;

/// <summary>One exact Kindle Road addition over VictoryRoad27; no historical table proof is relabelled.</summary>
public sealed class SeviiRubyWaterEncounterCatalog
{
    private static readonly ConditionalWeakTable<WaterEncounterTable,SeviiRubyWaterEncounterCatalog> Proofs = new();
    public VictoryRoadWaterEncounterCatalog Predecessor { get; }
    public ImmutableDictionary<MapId,WaterEncounterTable> Tables { get; }
    private SeviiRubyWaterEncounterCatalog(VictoryRoadWaterEncounterCatalog predecessor, ImmutableArray<WaterEncounterTable> added)
    { Predecessor = predecessor; Tables = predecessor.Tables.AddRange(added.Select(t => KeyValuePair.Create(t.Map,t))); }
    public static ImmutableArray<WaterEncounterTable> SourceAdditions => SeviiRubyEncounterTables.Water;
    public static SeviiRubyWaterEncounterCatalog Authenticate(VictoryRoadWaterEncounterCatalog predecessor, ImmutableArray<WaterEncounterTable> additions)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (predecessor.Tables.Count != 27 || predecessor.Tables.Values.Any(t => !WaterEncounterCatalog.IsAuthenticated(t) && !CinnabarWaterEncounterCatalog.IsAuthenticated(t) && !FirstSeviiWaterEncounterCatalog.IsAuthenticated(t) && !VictoryRoadWaterEncounterCatalog.IsAuthenticated(t)) ||
            additions.IsDefault || additions.Length != 1 || additions.Any(t => t is null) || additions.Select(t => t.Map).Distinct().Count() != 1)
            throw new ArgumentException("sevii_ruby_water_scope");
        foreach (var actual in additions)
        {
            var expected = SourceAdditions.SingleOrDefault(t => t.Map == actual.Map);
            if (expected is null || actual.Slots.IsDefault || !actual.Slots.SequenceEqual(expected.Slots) ||
                actual with { Slots = expected.Slots } != expected) throw new ArgumentException("sevii_ruby_water_source");
        }
        var copies = additions.Select(t => t with { }).ToImmutableArray();
        var proof = new SeviiRubyWaterEncounterCatalog(predecessor,copies);
        foreach (var table in copies) Proofs.Add(table,proof);
        return proof;
    }
    public static bool IsAuthenticated(WaterEncounterTable? table) => table is not null && Proofs.TryGetValue(table,out _);
}

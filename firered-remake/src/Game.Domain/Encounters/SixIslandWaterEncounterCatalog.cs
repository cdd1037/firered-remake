using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;

/// <summary>Two exact Six Island additions over Four31; no historical table proof is relabelled.</summary>
public sealed class SixIslandWaterEncounterCatalog
{
    private static readonly ConditionalWeakTable<WaterEncounterTable,SixIslandWaterEncounterCatalog> Proofs = new();
    public FourIslandWaterEncounterCatalog Predecessor { get; }
    public ImmutableDictionary<MapId,WaterEncounterTable> Tables { get; }
    private SixIslandWaterEncounterCatalog(FourIslandWaterEncounterCatalog predecessor, ImmutableArray<WaterEncounterTable> added)
    { Predecessor = predecessor; Tables = predecessor.Tables.AddRange(added.Select(t => KeyValuePair.Create(t.Map,t))); }
    public static ImmutableArray<WaterEncounterTable> SourceAdditions => SixIslandEncounterTables.Water;
    public static SixIslandWaterEncounterCatalog Authenticate(FourIslandWaterEncounterCatalog predecessor, ImmutableArray<WaterEncounterTable> additions)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (predecessor.Tables.Count != 31 || predecessor.Tables.Values.Any(t => !WaterEncounterCatalog.IsAuthenticated(t) && !CinnabarWaterEncounterCatalog.IsAuthenticated(t) && !FirstSeviiWaterEncounterCatalog.IsAuthenticated(t) && !VictoryRoadWaterEncounterCatalog.IsAuthenticated(t) && !SeviiRubyWaterEncounterCatalog.IsAuthenticated(t) && !FourIslandWaterEncounterCatalog.IsAuthenticated(t)) ||
            additions.IsDefault || additions.Length != 2 || additions.Any(t => t is null) || additions.Select(t => t.Map).Distinct().Count() != 2)
            throw new ArgumentException("six_island_water_scope");
        foreach (var actual in additions)
        {
            var expected = SourceAdditions.SingleOrDefault(t => t.Map == actual.Map);
            if (expected is null || actual.Slots.IsDefault || !actual.Slots.SequenceEqual(expected.Slots) ||
                actual with { Slots = expected.Slots } != expected) throw new ArgumentException("six_island_water_source");
        }
        var copies = additions.Select(t => t with { }).ToImmutableArray();
        var proof = new SixIslandWaterEncounterCatalog(predecessor,copies);
        foreach (var table in copies) Proofs.Add(table,proof);
        return proof;
    }
    public static bool IsAuthenticated(WaterEncounterTable? table) => table is not null && Proofs.TryGetValue(table,out _);
}

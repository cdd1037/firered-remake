using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;

/// <summary>Two exact Five Island additions over Six33; no historical table proof is relabelled.</summary>
public sealed class FiveIslandWaterEncounterCatalog
{
    private static readonly ConditionalWeakTable<WaterEncounterTable,FiveIslandWaterEncounterCatalog> Proofs = new();
    public SixIslandWaterEncounterCatalog Predecessor { get; }
    public ImmutableDictionary<MapId,WaterEncounterTable> Tables { get; }
    private FiveIslandWaterEncounterCatalog(SixIslandWaterEncounterCatalog predecessor, ImmutableArray<WaterEncounterTable> added)
    { Predecessor = predecessor; Tables = predecessor.Tables.AddRange(added.Select(t => KeyValuePair.Create(t.Map,t))); }
    public static ImmutableArray<WaterEncounterTable> SourceAdditions => FiveIslandEncounterTables.Water;
    public static FiveIslandWaterEncounterCatalog Authenticate(SixIslandWaterEncounterCatalog predecessor, ImmutableArray<WaterEncounterTable> additions)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (predecessor.Tables.Count != 33 || predecessor.Tables.Values.Any(t => !WaterEncounterCatalog.IsAuthenticated(t) && !CinnabarWaterEncounterCatalog.IsAuthenticated(t) && !FirstSeviiWaterEncounterCatalog.IsAuthenticated(t) && !VictoryRoadWaterEncounterCatalog.IsAuthenticated(t) && !SeviiRubyWaterEncounterCatalog.IsAuthenticated(t) && !FourIslandWaterEncounterCatalog.IsAuthenticated(t) && !SixIslandWaterEncounterCatalog.IsAuthenticated(t)) ||
            additions.IsDefault || additions.Length != 2 || additions.Any(t => t is null) || additions.Select(t => t.Map).Distinct().Count() != 2)
            throw new ArgumentException("five_island_water_scope");
        foreach (var actual in additions)
        {
            var expected = SourceAdditions.SingleOrDefault(t => t.Map == actual.Map);
            if (expected is null || actual.Slots.IsDefault || !actual.Slots.SequenceEqual(expected.Slots) ||
                actual with { Slots = expected.Slots } != expected) throw new ArgumentException("five_island_water_source");
        }
        var copies = additions.Select(t => t with { }).ToImmutableArray();
        var proof = new FiveIslandWaterEncounterCatalog(predecessor,copies);
        foreach (var table in copies) Proofs.Add(table,proof);
        return proof;
    }
    public static bool IsAuthenticated(WaterEncounterTable? table) => table is not null && Proofs.TryGetValue(table,out _);
}

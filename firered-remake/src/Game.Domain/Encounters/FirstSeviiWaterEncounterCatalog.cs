using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;

/// <summary>Three exact water-table additions over Cinnabar24; no historical table proof is relabelled.</summary>
public sealed class FirstSeviiWaterEncounterCatalog
{
    private static readonly ConditionalWeakTable<WaterEncounterTable,FirstSeviiWaterEncounterCatalog> Proofs = new();
    public CinnabarWaterEncounterCatalog Predecessor { get; }
    public ImmutableDictionary<MapId,WaterEncounterTable> Tables { get; }
    private FirstSeviiWaterEncounterCatalog(CinnabarWaterEncounterCatalog predecessor, ImmutableArray<WaterEncounterTable> added)
    { Predecessor = predecessor; Tables = predecessor.Tables.AddRange(added.Select(t => KeyValuePair.Create(t.Map,t))); }
    public static ImmutableArray<WaterEncounterTable> SourceAdditions => FirstSeviiEncounterTables.Water;
    public static FirstSeviiWaterEncounterCatalog Authenticate(CinnabarWaterEncounterCatalog predecessor, ImmutableArray<WaterEncounterTable> additions)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (predecessor.Tables.Count != 24 || predecessor.Tables.Values.Any(t => !WaterEncounterCatalog.IsAuthenticated(t) && !CinnabarWaterEncounterCatalog.IsAuthenticated(t)) ||
            additions.IsDefault || additions.Length != 3 || additions.Any(t => t is null) || additions.Select(t => t.Map).Distinct().Count() != 3)
            throw new ArgumentException("first_sevii_water_scope");
        foreach (var actual in additions)
        {
            var expected = SourceAdditions.SingleOrDefault(t => t.Map == actual.Map);
            if (expected is null || actual.Slots.IsDefault || !actual.Slots.SequenceEqual(expected.Slots) ||
                actual with { Slots = expected.Slots } != expected) throw new ArgumentException("first_sevii_water_source");
        }
        var copies = additions.Select(t => t with { }).ToImmutableArray();
        var proof = new FirstSeviiWaterEncounterCatalog(predecessor,copies);
        foreach (var table in copies) Proofs.Add(table,proof);
        return proof;
    }
    public static bool IsAuthenticated(WaterEncounterTable? table) => table is not null && Proofs.TryGetValue(table,out _);
}

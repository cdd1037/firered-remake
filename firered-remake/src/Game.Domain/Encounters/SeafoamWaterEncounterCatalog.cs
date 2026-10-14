using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Encounters;

/// <summary>Four exact Seafoam/Route19/20 Surf tables extend the current39 without relabelling any predecessor proof.</summary>
public sealed class SeafoamWaterEncounterCatalog
{
    private static readonly ConditionalWeakTable<WaterEncounterTable, SeafoamWaterEncounterCatalog> Proofs = new();
    public SevenIslandWaterEncounterCatalog Predecessor { get; }
    public ImmutableDictionary<MapId, WaterEncounterTable> Tables { get; }
    private SeafoamWaterEncounterCatalog(SevenIslandWaterEncounterCatalog predecessor, ImmutableArray<WaterEncounterTable> additions)
    { Predecessor = predecessor; Tables = predecessor.Tables.AddRange(additions.Select(t => KeyValuePair.Create(t.Map, t))); }
    public static ImmutableArray<WaterEncounterTable> SourceAdditions => SeafoamEncounterTables.Water;

    public static SeafoamWaterEncounterCatalog Authenticate(SevenIslandWaterEncounterCatalog predecessor,
        ImmutableArray<WaterEncounterTable> additions)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (predecessor.Tables.Count != 39 || additions.IsDefault || additions.Length != SourceAdditions.Length ||
            additions.Any(t => t is null) || additions.Select(t => t.Map).Distinct().Count() != SourceAdditions.Length)
            throw new ArgumentException("seafoam_water_scope");
        // SevenIsland's private constructor authenticates its inherited tables; validate its own exact identities too.
        if (SevenIslandWaterEncounterCatalog.SourceAdditions.Any(t => !predecessor.Tables.TryGetValue(t.Map, out var actual) ||
            !SevenIslandWaterEncounterCatalog.IsAuthenticated(actual)))
            throw new ArgumentException("seafoam_water_predecessor");
        foreach (var actual in additions)
        {
            var expected = SourceAdditions.SingleOrDefault(t => t.Map == actual.Map);
            if (expected is null || actual.Slots.IsDefault || !actual.Slots.SequenceEqual(expected.Slots) ||
                actual with { Slots = expected.Slots } != expected)
                throw new ArgumentException("seafoam_water_source");
        }
        var copies = additions.Select(t => t with { }).ToImmutableArray();
        var proof = new SeafoamWaterEncounterCatalog(predecessor, copies);
        foreach (var table in copies) Proofs.Add(table, proof);
        return proof;
    }
    public static bool IsAuthenticated(WaterEncounterTable? table) => table is not null && Proofs.TryGetValue(table, out _);
}

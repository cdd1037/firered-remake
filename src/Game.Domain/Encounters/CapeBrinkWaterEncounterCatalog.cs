using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Encounters;

/// <summary>The exact Cape Brink Surf table extends the current43 without relabelling any predecessor proof.</summary>
public sealed class CapeBrinkWaterEncounterCatalog
{
    public SeafoamWaterEncounterCatalog Predecessor { get; }
    public ImmutableDictionary<MapId, WaterEncounterTable> Tables { get; }
    private CapeBrinkWaterEncounterCatalog(SeafoamWaterEncounterCatalog predecessor, ImmutableArray<WaterEncounterTable> additions)
    { Predecessor = predecessor; Tables = predecessor.Tables.AddRange(additions.Select(t => KeyValuePair.Create(t.Map, t))); }
    public static ImmutableArray<WaterEncounterTable> SourceAdditions => CapeBrinkEncounterTables.Water;

    public static CapeBrinkWaterEncounterCatalog Authenticate(SeafoamWaterEncounterCatalog predecessor,
        ImmutableArray<WaterEncounterTable> additions)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (predecessor.Tables.Count != 43 || additions.IsDefault || additions.Length != SourceAdditions.Length ||
            additions.Any(t => t is null) || additions.Select(t => t.Map).Distinct().Count() != SourceAdditions.Length)
            throw new ArgumentException("cape_brink_water_scope");
        // Seafoam's private constructor authenticates its inherited tables; validate its own exact identities too.
        if (SeafoamWaterEncounterCatalog.SourceAdditions.Any(t => !predecessor.Tables.TryGetValue(t.Map, out var actual) ||
            !SeafoamWaterEncounterCatalog.MatchesDefinition(actual)))
            throw new ArgumentException("cape_brink_water_predecessor");
        foreach (var actual in additions)
        {
            var expected = SourceAdditions.SingleOrDefault(t => t.Map == actual.Map);
            if (expected is null || actual.Slots.IsDefault || !actual.Slots.SequenceEqual(expected.Slots) ||
                !WaterEncounterCatalog.SameDefinition(actual, expected))
                throw new ArgumentException("cape_brink_water_source");
        }
        var copies = additions.Select(t => t with { }).ToImmutableArray();
        var proof = new CapeBrinkWaterEncounterCatalog(predecessor, copies);
        return proof;
    }
    public static bool MatchesDefinition(WaterEncounterTable? table) =>
        table is not null && SourceAdditions.Any(expected => WaterEncounterCatalog.SameDefinition(table, expected));
}

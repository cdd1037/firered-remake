using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Encounters;

/// <summary>Two exact cave Surf tables extend the current35 without relabelling any predecessor proof.</summary>
public sealed class CeruleanCaveWaterEncounterCatalog
{
    public FiveIslandWaterEncounterCatalog Predecessor { get; }
    public ImmutableDictionary<MapId, WaterEncounterTable> Tables { get; }
    private CeruleanCaveWaterEncounterCatalog(FiveIslandWaterEncounterCatalog predecessor, ImmutableArray<WaterEncounterTable> additions)
    { Predecessor = predecessor; Tables = predecessor.Tables.AddRange(additions.Select(t => KeyValuePair.Create(t.Map, t))); }
    public static ImmutableArray<WaterEncounterTable> SourceAdditions => CeruleanCaveEncounterTables.Water;

    public static CeruleanCaveWaterEncounterCatalog Authenticate(FiveIslandWaterEncounterCatalog predecessor,
        ImmutableArray<WaterEncounterTable> additions)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (predecessor.Tables.Count != 35 || additions.IsDefault || additions.Length != SourceAdditions.Length ||
            additions.Any(t => t is null) || additions.Select(t => t.Map).Distinct().Count() != SourceAdditions.Length)
            throw new ArgumentException("cerulean_cave_water_scope");
        // FiveIsland's private constructor authenticates its inherited tables; validate its own exact identities too.
        if (FiveIslandWaterEncounterCatalog.SourceAdditions.Any(t => !predecessor.Tables.TryGetValue(t.Map, out var actual) ||
            !FiveIslandWaterEncounterCatalog.MatchesDefinition(actual)))
            throw new ArgumentException("cerulean_cave_water_predecessor");
        foreach (var actual in additions)
        {
            var expected = SourceAdditions.SingleOrDefault(t => t.Map == actual.Map);
            if (expected is null || actual.Slots.IsDefault || !actual.Slots.SequenceEqual(expected.Slots) ||
                !WaterEncounterCatalog.SameDefinition(actual, expected))
                throw new ArgumentException("cerulean_cave_water_source");
        }
        var copies = additions.Select(t => t with { }).ToImmutableArray();
        var proof = new CeruleanCaveWaterEncounterCatalog(predecessor, copies);
        return proof;
    }
    public static bool MatchesDefinition(WaterEncounterTable? table) =>
        table is not null && SourceAdditions.Any(expected => WaterEncounterCatalog.SameDefinition(table, expected));
}

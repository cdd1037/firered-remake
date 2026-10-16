using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;
/// <summary>Six exact source land declarations. Map terrain decides whether a slot table can trigger.</summary>
public static class CinnabarLandEncounterTables
{
    public static ImmutableArray<EarlyLandEncounterTable> All { get; } = [
        Create("pokemon-mansion-1f",7,[("KOFFING", 28), ("RATICATE", 32), ("KOFFING", 30), ("RATICATE", 36), ("GROWLITHE", 30), ("RATTATA", 28), ("GRIMER", 28), ("WEEZING", 32), ("GROWLITHE", 32), ("RATTATA", 26), ("GROWLITHE", 32), ("RATTATA", 26)]),
        Create("pokemon-mansion-2f",7,[("KOFFING", 28), ("RATICATE", 32), ("KOFFING", 30), ("RATICATE", 36), ("GROWLITHE", 30), ("RATTATA", 28), ("GRIMER", 28), ("WEEZING", 32), ("GROWLITHE", 32), ("RATTATA", 26), ("GROWLITHE", 32), ("RATTATA", 26)]),
        Create("pokemon-mansion-3f",7,[("KOFFING", 28), ("RATICATE", 32), ("KOFFING", 30), ("RATICATE", 36), ("GROWLITHE", 30), ("RATTATA", 28), ("GRIMER", 28), ("WEEZING", 32), ("GROWLITHE", 32), ("RATTATA", 26), ("GROWLITHE", 32), ("RATTATA", 26)]),
        Create("pokemon-mansion-b1f",5,[("KOFFING", 28), ("RATICATE", 34), ("KOFFING", 30), ("DITTO", 30), ("GROWLITHE", 30), ("RATICATE", 38), ("GRIMER", 28), ("WEEZING", 34), ("GROWLITHE", 32), ("RATTATA", 26), ("GROWLITHE", 32), ("RATTATA", 26)]),
        Create("route-21-north",14,[("TANGELA", 22), ("TANGELA", 23), ("TANGELA", 24), ("TANGELA", 21), ("TANGELA", 25), ("TANGELA", 20), ("TANGELA", 19), ("TANGELA", 26), ("TANGELA", 18), ("TANGELA", 27), ("TANGELA", 17), ("TANGELA", 28)]),
        Create("route-21-south",14,[("TANGELA", 22), ("TANGELA", 23), ("TANGELA", 24), ("TANGELA", 21), ("TANGELA", 25), ("TANGELA", 20), ("TANGELA", 19), ("TANGELA", 26), ("TANGELA", 18), ("TANGELA", 27), ("TANGELA", 17), ("TANGELA", 28)]),
    ];
    public static ImmutableArray<SpeciesId> WildSpecies { get; } = All.SelectMany(t => t.Slots).Select(s => s.Species).Distinct().ToImmutableArray();
    public static bool IsCave(MapId map) => All.Any(t => t.Map == map && map.Value.StartsWith("map.pokemon-mansion-", StringComparison.Ordinal));
    private static EarlyLandEncounterTable Create(string map, int rate, (string Species, byte Level)[] rows) =>
        new(new("map." + map), rate,
            rows.Select((s,i) => new EarlyLandEncounterSlot(i,new("pret:SPECIES_"+s.Species),s.Level,s.Level,EarlyLandEncounterRules.SlotWeights[i])).ToImmutableArray());
}

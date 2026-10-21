using System.Text.Json;
using Game.Domain.Encounters;

namespace Game.Content;

/// <summary>Admits only Route3 grass. Route4 belongs to the full current composition.</summary>
public static class PretRoute3Encounters
{
    public static EarlyLandEncounterTable LoadProject(string projectRoot) => Load(PretCurrentWorld.Load(projectRoot));

    public static EarlyLandEncounterTable Load(PretCurrentWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var entries = PretEarlyFieldData.Read(world.ProjectRoot, "data/encounters/early-land.json").EnumerateArray()
            .Where(e => e.GetProperty("map").GetString() == "MAP_ROUTE3").ToArray();
        if (entries.Length != 1 || !world.Maps.ContainsKey(EarlyLandEncounterRules.Route3Table.Map))
            throw new InvalidDataException("The single mounted Route3 encounter table is required.");
        var result = PretWildEncounterTables.Land(JsonSerializer.SerializeToElement(entries), world.Resolve).Single();
        EarlyLandEncounterRules.ValidateTable(result);
        return result;
    }
}

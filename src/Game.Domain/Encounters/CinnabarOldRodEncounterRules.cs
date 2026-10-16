using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

/// <summary>Authenticated Cinnabar continuation of the existing Old Rod task and ordinary creation.</summary>
public static class CinnabarOldRodEncounterRules
{
    public const string RulesVersion = "firered-cinnabar-old-rod-v1";
    public static ImmutableArray<OldRodEncounterTable> SourceAdditions { get; } =
    [
        Table("route-21-north", 20),
        Table("route-21-south", 20),
        Table("cinnabar-island", 10)
    ];
    public static ImmutableArray<OldRodEncounterTable> Tables { get; } =
        [.. OldRodEncounterRules.Tables, .. SourceAdditions];

    public static FishingTaskProfile TaskProfile { get; } = FishingTaskProfile.ForRod(RulesVersion, FishingRod.Old, Tables, OldRodEncounterRules.TaskProfile.EmptyMaps.Select(m => m.Map));
    public static bool IsAdmittedMap(MapId map) => Tables.Any(t => t.Map == map) ||
        OldRodEncounterRules.MapsWithoutFishingMons.Contains(map);

    private static OldRodEncounterTable Table(string map, int rate) =>
        new(new("map." + map), rate,
            [new(0, OldRodEncounterRules.Magikarp, 5, 5, 70), new(1, OldRodEncounterRules.Magikarp, 5, 5, 30)]);
}

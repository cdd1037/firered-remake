using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

/// <summary>Authenticated first-Sevii continuation of the existing Old Rod task and ordinary creation.</summary>
public static class FirstSeviiOldRodEncounterRules
{
    public const string RulesVersion = "firered-first-sevii-old-rod-v1";
    public static ImmutableArray<OldRodEncounterTable> SourceAdditions { get; } = FirstSeviiEncounterTables.FishingSource.Select(t => t with {Slots=t.Slots.Take(2).ToImmutableArray()}).ToImmutableArray();
    public static ImmutableArray<OldRodEncounterTable> Tables { get; } =
        [.. CinnabarOldRodEncounterRules.Tables, .. SourceAdditions];

    public static ImmutableArray<MapId> MapsWithoutFishingMons { get; } = [
        new("map.one-island-harbor"),new("map.one-island-pokemon-center-1f"),
        new("map.two-island"),new("map.two-island-harbor"),new("map.two-island-joyful-game-corner"),new("map.two-island-pokemon-center-1f"),
        new("map.three-island"),new("map.three-island-harbor"),new("map.three-island-port"),new("map.three-island-pokemon-center-1f"),new("map.three-island-mart")];
    public static FishingTaskProfile TaskProfile { get; } = FishingTaskProfile.ForRod(RulesVersion, FishingRod.Old, Tables, CinnabarOldRodEncounterRules.TaskProfile.EmptyMaps.Select(m => m.Map) .Concat(MapsWithoutFishingMons));
    public static bool IsAdmittedMap(MapId map) => Tables.Any(t => t.Map == map) ||
        MapsWithoutFishingMons.Contains(map) || OldRodEncounterRules.MapsWithoutFishingMons.Contains(map);
}

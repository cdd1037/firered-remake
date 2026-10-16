using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

/// <summary>Authenticated first-Sevii continuation of the existing Old Rod task and ordinary creation.</summary>
public static class VictoryRoadOldRodEncounterRules
{
    public const string RulesVersion = "firered-victory-road-old-rod-v1";
    public static ImmutableArray<OldRodEncounterTable> SourceAdditions { get; } = VictoryRoadEncounterTables.FishingSource.Select(t => t with {Slots=t.Slots.Take(2).ToImmutableArray()}).ToImmutableArray();
    public static ImmutableArray<OldRodEncounterTable> Tables { get; } =
        [.. FirstSeviiOldRodEncounterRules.Tables, .. SourceAdditions];

    public static ImmutableArray<MapId> MapsWithoutFishingMons { get; } = [new("map.victory-road-1f"), new("map.victory-road-2f"), new("map.victory-road-3f"), new("map.indigo-plateau-exterior")];
    public static FishingTaskProfile TaskProfile { get; } = FishingTaskProfile.ForRod(RulesVersion, FishingRod.Old, Tables, FirstSeviiOldRodEncounterRules.TaskProfile.EmptyMaps.Select(m => m.Map) .Concat(MapsWithoutFishingMons));
    public static bool IsAdmittedMap(MapId map) => Tables.Any(t=>t.Map==map) || MapsWithoutFishingMons.Contains(map) || FirstSeviiOldRodEncounterRules.IsAdmittedMap(map);
}

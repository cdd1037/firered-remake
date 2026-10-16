using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

/// <summary>Authenticated Six Island continuation of the existing Old Rod task and ordinary creation.</summary>
public static class FiveIslandOldRodEncounterRules
{
    public const string RulesVersion = "firered-five-island-old-rod-v1";
    public static ImmutableArray<OldRodEncounterTable> SourceAdditions { get; } = FiveIslandEncounterTables.FishingSource.Select(t => t with {Slots=t.Slots.Take(2).ToImmutableArray()}).ToImmutableArray();
    public static ImmutableArray<OldRodEncounterTable> Tables { get; } =
        [.. SixIslandOldRodEncounterRules.Tables, .. SourceAdditions];

    public static ImmutableArray<MapId> MapsWithoutFishingMons { get; } = [
        new("map.five-island-harbor"),new("map.five-island-pokemon-center-1f"),new("map.five-island-rocket-warehouse")];
    public static FishingTaskProfile TaskProfile { get; } = FishingTaskProfile.ForRod(RulesVersion, FishingRod.Old, Tables, SixIslandOldRodEncounterRules.TaskProfile.EmptyMaps.Select(m => m.Map) .Concat(MapsWithoutFishingMons));
    public static bool IsAdmittedMap(MapId map) => Tables.Any(t=>t.Map==map) || MapsWithoutFishingMons.Contains(map) || SixIslandOldRodEncounterRules.IsAdmittedMap(map);
}

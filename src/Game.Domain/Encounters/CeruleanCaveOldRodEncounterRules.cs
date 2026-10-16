using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

/// <summary>Authenticated Cerulean Cave continuation of the existing Old Rod task and ordinary creation.</summary>
public static class CeruleanCaveOldRodEncounterRules
{
    public const string RulesVersion = FiveIslandOldRodEncounterRules.RulesVersion;
    public static ImmutableArray<OldRodEncounterTable> SourceAdditions { get; } = CeruleanCaveEncounterTables.FishingSource.Select(t => t with {Slots=t.Slots.Take(2).ToImmutableArray()}).ToImmutableArray();
    public static ImmutableArray<OldRodEncounterTable> Tables { get; } =
        [.. FiveIslandOldRodEncounterRules.Tables, .. SourceAdditions];

    public static ImmutableArray<MapId> MapsWithoutFishingMons { get; } = [
        new("map.cerulean-cave-2f")];
    public static FishingTaskProfile TaskProfile { get; } = FishingTaskProfile.ForRod(RulesVersion, FishingRod.Old, Tables, FiveIslandOldRodEncounterRules.TaskProfile.EmptyMaps.Select(m => m.Map) .Concat(MapsWithoutFishingMons));
    public static bool IsAdmittedMap(MapId map) => Tables.Any(t=>t.Map==map) || MapsWithoutFishingMons.Contains(map) || FiveIslandOldRodEncounterRules.IsAdmittedMap(map);
}

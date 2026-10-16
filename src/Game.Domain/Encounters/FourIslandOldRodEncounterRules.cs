using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

/// <summary>Authenticated Four Island continuation of the existing Old Rod task and ordinary creation.</summary>
public static class FourIslandOldRodEncounterRules
{
    public const string RulesVersion = "firered-four-island-old-rod-v1";
    public static ImmutableArray<OldRodEncounterTable> SourceAdditions { get; } = FourIslandEncounterTables.FishingSource.Select(t => t with {Slots=t.Slots.Take(2).ToImmutableArray()}).ToImmutableArray();
    public static ImmutableArray<OldRodEncounterTable> Tables { get; } =
        [.. SeviiRubyOldRodEncounterRules.Tables, .. SourceAdditions];

    public static ImmutableArray<MapId> MapsWithoutFishingMons { get; } = [
        new("map.four-island-harbor"),new("map.four-island-icefall-cave-1f"),new("map.four-island-icefall-cave-b1f"),
        new("map.four-island-pokemon-center-1f"),new("map.four-island-mart")];
    public static FishingTaskProfile TaskProfile { get; } = FishingTaskProfile.ForRod(RulesVersion, FishingRod.Old, Tables, SeviiRubyOldRodEncounterRules.TaskProfile.EmptyMaps.Select(m => m.Map) .Concat(MapsWithoutFishingMons));
    public static bool IsAdmittedMap(MapId map) => Tables.Any(t=>t.Map==map) || MapsWithoutFishingMons.Contains(map) || SeviiRubyOldRodEncounterRules.IsAdmittedMap(map);
}

using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

/// <summary>Authenticated Six Island continuation of the existing Old Rod task and ordinary creation.</summary>
public static class SixIslandOldRodEncounterRules
{
    public const string RulesVersion = "firered-six-island-old-rod-v1";
    public static ImmutableArray<OldRodEncounterTable> SourceAdditions { get; } = SixIslandEncounterTables.FishingSource.Select(t => t with {Slots=t.Slots.Take(2).ToImmutableArray()}).ToImmutableArray();
    public static ImmutableArray<OldRodEncounterTable> Tables { get; } =
        [.. FourIslandOldRodEncounterRules.Tables, .. SourceAdditions];

    public static ImmutableArray<MapId> MapsWithoutFishingMons { get; } = [
        new("map.six-island"),new("map.six-island-harbor"),new("map.six-island-pokemon-center-1f"),new("map.six-island-mart"),
        new("map.six-island-dotted-hole-1f"),new("map.six-island-dotted-hole-b1f"),new("map.six-island-dotted-hole-b2f"),
        new("map.six-island-dotted-hole-b3f"),new("map.six-island-dotted-hole-b4f"),new("map.six-island-dotted-hole-sapphire-room")];
    public static FishingTaskProfile TaskProfile { get; } = FishingTaskProfile.ForRod(RulesVersion, FishingRod.Old, Tables, FourIslandOldRodEncounterRules.TaskProfile.EmptyMaps.Select(m => m.Map) .Concat(MapsWithoutFishingMons));
    public static bool IsAdmittedMap(MapId map) => Tables.Any(t=>t.Map==map) || MapsWithoutFishingMons.Contains(map) || FourIslandOldRodEncounterRules.IsAdmittedMap(map);
}

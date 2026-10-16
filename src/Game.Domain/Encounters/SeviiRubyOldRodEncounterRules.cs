using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

/// <summary>Authenticated Ruby continuation of the existing Old Rod task and ordinary creation.</summary>
public static class SeviiRubyOldRodEncounterRules
{
    public const string RulesVersion = "firered-sevii-ruby-old-rod-v1";
    public static ImmutableArray<OldRodEncounterTable> SourceAdditions { get; } = SeviiRubyEncounterTables.FishingSource.Select(t => t with {Slots=t.Slots.Take(2).ToImmutableArray()}).ToImmutableArray();
    public static ImmutableArray<OldRodEncounterTable> Tables { get; } =
        [.. VictoryRoadOldRodEncounterRules.Tables, .. SourceAdditions];

    public static ImmutableArray<MapId> MapsWithoutFishingMons { get; } = [
        new("map.mt-ember-exterior"),new("map.mt-ember-ruby-path-1f"),new("map.mt-ember-ruby-path-b1f"),
        new("map.mt-ember-ruby-path-b2f"),new("map.mt-ember-ruby-path-b3f"),new("map.mt-ember-ruby-path-b4f"),
        new("map.mt-ember-ruby-path-b5f"),new("map.mt-ember-ruby-path-b1f-stairs"),new("map.mt-ember-ruby-path-b2f-stairs"),
        new("map.one-island-kindle-road-ember-spa")];
    public static FishingTaskProfile TaskProfile { get; } = FishingTaskProfile.ForRod(RulesVersion, FishingRod.Old, Tables, VictoryRoadOldRodEncounterRules.TaskProfile.EmptyMaps.Select(m => m.Map) .Concat(MapsWithoutFishingMons));
    public static bool IsAdmittedMap(MapId map) => Tables.Any(t=>t.Map==map) || MapsWithoutFishingMons.Contains(map) || VictoryRoadOldRodEncounterRules.IsAdmittedMap(map);
}

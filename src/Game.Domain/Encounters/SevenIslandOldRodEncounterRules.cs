using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
namespace Game.Domain.Encounters;

/// <summary>Exact Old Rod table continuation. Frame task admission remains in the shared fishing owner.</summary>
public static class SevenIslandOldRodEncounterRules
{
    public const string RulesVersion = CeruleanCaveOldRodEncounterRules.RulesVersion;
    public static ImmutableArray<OldRodEncounterTable> SourceAdditions => SevenIslandEncounterTables.OldRod;
    public static ImmutableArray<OldRodEncounterTable> Tables { get; } = [.. CeruleanCaveOldRodEncounterRules.Tables, .. SourceAdditions, .. SeafoamEncounterTables.OldRod, .. CapeBrinkEncounterTables.OldRod, .. MemorialPillarEncounterTables.OldRod, .. LostCaveEncounterTables.OldRod];
    public static ImmutableArray<MapId> MapsWithoutFishingMons { get; } = [
        new("map.seven-island"), new("map.seven-island-harbor"), new("map.seven-island-house-room1"),
        new("map.seven-island-mart"), new("map.seven-island-pokemon-center-1f"),
        new("map.seven-island-sevault-canyon"), new("map.seven-island-sevault-canyon-entrance"),
        new("map.seven-island-sevault-canyon-house"), new("map.seven-island-sevault-canyon-tanoby-key"),
        new("map.seven-island-tanoby-ruins-monean-chamber"), new("map.seven-island-tanoby-ruins-liptoo-chamber"),
        new("map.seven-island-tanoby-ruins-weepth-chamber"), new("map.seven-island-tanoby-ruins-dilford-chamber"),
        new("map.seven-island-tanoby-ruins-scufib-chamber"), new("map.seven-island-tanoby-ruins-rixy-chamber"),
        new("map.seven-island-tanoby-ruins-viapois-chamber")];
    public static FishingTaskProfile TaskProfile { get; } = FishingTaskProfile.ForRod(RulesVersion, FishingRod.Old, Tables, CeruleanCaveOldRodEncounterRules.TaskProfile.EmptyMaps.Select(m => m.Map) .Concat(MapsWithoutFishingMons));
    public static bool IsAdmittedMap(MapId map) => Tables.Any(t => t.Map == map) || MapsWithoutFishingMons.Contains(map) || CeruleanCaveOldRodEncounterRules.IsAdmittedMap(map);
}

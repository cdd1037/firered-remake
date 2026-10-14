using System.Collections.Immutable;

namespace Game.Domain.Encounters;

/// <summary>Exact source tables shared by current walking admission and wild provenance.
/// Registering a table does not admit an unmounted Application map or change its slots.</summary>
public static class SourceTableEncounterRegistry
{
    public static ImmutableArray<EarlyLandEncounterTable> Land { get; } = CapeBrinkEncounterTables.Land
        .Add(Route16LandEncounterTables.Route16).AddRange(MemorialPillarEncounterTables.Land)
        .AddRange(LostCaveEncounterTables.Land);
    public static ImmutableArray<WaterEncounterTable> Water { get; } = CapeBrinkEncounterTables.Water
        .AddRange(MemorialPillarEncounterTables.Water).AddRange(LostCaveEncounterTables.Water);
    public static ImmutableArray<OldRodEncounterTable> OldRod { get; } = CapeBrinkEncounterTables.OldRod
        .AddRange(MemorialPillarEncounterTables.OldRod).AddRange(LostCaveEncounterTables.OldRod);
}

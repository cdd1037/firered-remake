using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Encounters;

/// <summary>Exact pinned Memorial Pillar rows. Fishing preserves original ten-slot indices.</summary>
public static class MemorialPillarEncounterTables
{
    public static MapId Map { get; } = new("map.five-island-memorial-pillar");
    public static ImmutableArray<EarlyLandEncounterTable> Land { get; } = [
        new(Map, 21, [
            new(0, new("pret:SPECIES_HOPPIP"), 10, 10, 20),
            new(1, new("pret:SPECIES_HOPPIP"), 12, 12, 20),
            new(2, new("pret:SPECIES_HOPPIP"), 8, 8, 10),
            new(3, new("pret:SPECIES_HOPPIP"), 14, 14, 10),
            new(4, new("pret:SPECIES_HOPPIP"), 10, 10, 10),
            new(5, new("pret:SPECIES_HOPPIP"), 12, 12, 10),
            new(6, new("pret:SPECIES_HOPPIP"), 16, 16, 5),
            new(7, new("pret:SPECIES_HOPPIP"), 6, 6, 5),
            new(8, new("pret:SPECIES_HOPPIP"), 8, 8, 4),
            new(9, new("pret:SPECIES_HOPPIP"), 14, 14, 4),
            new(10, new("pret:SPECIES_HOPPIP"), 8, 8, 1),
            new(11, new("pret:SPECIES_HOPPIP"), 14, 14, 1),
        ])
    ];
    public static ImmutableArray<WaterEncounterTable> Water { get; } = [
        new(Map, 2, [
            new(0, new("pret:SPECIES_TENTACOOL"), 5, 35, 60),
            new(1, new("pret:SPECIES_HOPPIP"), 5, 15, 30),
            new(2, new("pret:SPECIES_TENTACOOL"), 35, 40, 5),
            new(3, new("pret:SPECIES_TENTACRUEL"), 35, 40, 4),
            new(4, new("pret:SPECIES_TENTACRUEL"), 35, 40, 1),
        ])
    ];
    public static ImmutableArray<OldRodEncounterTable> OldRod { get; } = [
        new(Map, 20, [
            new(0, new("pret:SPECIES_MAGIKARP"), 5, 5, 70),
            new(1, new("pret:SPECIES_MAGIKARP"), 5, 5, 30),
        ])
    ];

    // Already in the pinned global Super Rod table set; do not duplicate or renumber its rows.
    public static OldRodEncounterTable SuperRod => SuperRodEncounterRules.Tables.Single(t => t.Map == Map);
}

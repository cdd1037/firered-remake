using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Encounters;

/// <summary>Exact Cape Brink rows from the pinned FireRed source; rod slots keep the original ten-slot indices.</summary>
public static class CapeBrinkEncounterTables
{
    public static MapId Map { get; } = new("map.two-island-cape-brink");
    public const string SourceMapSymbol = "MAP_TWO_ISLAND_CAPE_BRINK";
    public const string SourceSymbol = "sTwoIslandCapeBrink_FireRed";
    public const string SourceSha256 = "a6399199987e1bdf8502e9bfdcfc9d8ad57d74df206d22ba20f020492bd3bf7c";
    public static ImmutableArray<EarlyLandEncounterTable> Land { get; } = [
        new(Map, EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, SourceSymbol, SourceSha256, 21, [
            new(0, new("pret:SPECIES_SPEAROW"), 31, 31, 20),
            new(1, new("pret:SPECIES_ODDISH"), 30, 30, 20),
            new(2, new("pret:SPECIES_ODDISH"), 32, 32, 10),
            new(3, new("pret:SPECIES_GLOOM"), 36, 36, 10),
            new(4, new("pret:SPECIES_FEAROW"), 36, 36, 10),
            new(5, new("pret:SPECIES_MEOWTH"), 31, 31, 10),
            new(6, new("pret:SPECIES_GLOOM"), 38, 38, 5),
            new(7, new("pret:SPECIES_PSYDUCK"), 31, 31, 5),
            new(8, new("pret:SPECIES_GOLDUCK"), 37, 37, 4),
            new(9, new("pret:SPECIES_PERSIAN"), 37, 37, 4),
            new(10, new("pret:SPECIES_GOLDUCK"), 40, 40, 1),
            new(11, new("pret:SPECIES_PERSIAN"), 40, 40, 1),
        ])
    ];
    public static ImmutableArray<WaterEncounterTable> Water { get; } = [
        new(Map, SourceMapSymbol, EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, SourceSymbol, SourceSha256, 2, [
            new(0, new("pret:SPECIES_PSYDUCK"), 5, 20, 60),
            new(1, new("pret:SPECIES_PSYDUCK"), 20, 35, 30),
            new(2, new("pret:SPECIES_PSYDUCK"), 35, 40, 5),
            new(3, new("pret:SPECIES_GOLDUCK"), 35, 40, 4),
            new(4, new("pret:SPECIES_GOLDUCK"), 35, 40, 1),
        ])
    ];
    public static ImmutableArray<OldRodEncounterTable> OldRod { get; } = [
        new(Map, EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, SourceSymbol, SourceSha256, 20, [
            new(0, new("pret:SPECIES_MAGIKARP"), 5, 5, 70),
            new(1, new("pret:SPECIES_MAGIKARP"), 5, 5, 30),
        ])
    ];
    public static ImmutableArray<OldRodEncounterTable> GoodRod { get; } = [
        new(Map, EarlyLandEncounterRules.SourceCommit, EarlyLandEncounterRules.SourcePath, SourceSymbol, SourceSha256, 20, [
            new(2, new("pret:SPECIES_POLIWAG"), 5, 15, 60),
            new(3, new("pret:SPECIES_MAGIKARP"), 5, 15, 20),
            new(4, new("pret:SPECIES_GOLDEEN"), 5, 15, 20),
        ])
    ];
}

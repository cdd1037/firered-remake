using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class SevenIslandOpponentPokemonCatalog
{
    public const string Profile = "firered-seven-island-opponents-v1";
    public static ImmutableArray<SpeciesId> TrainerOnlySpecies { get; } =
        [new("pret:SPECIES_GIRAFARIG"), new("pret:SPECIES_HITMONTOP"), new("pret:SPECIES_MILTANK"),
         new("pret:SPECIES_PINECO"), new("pret:SPECIES_SMEARGLE"), new("pret:SPECIES_URSARING")];
}

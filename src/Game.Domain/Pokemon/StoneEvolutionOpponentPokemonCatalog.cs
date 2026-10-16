using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class StoneEvolutionOpponentPokemonCatalog
{
    public const string Profile = "firered-stone-evolution-opponents-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_POLIWRATH"), new("pret:SPECIES_VAPOREON"), new("pret:SPECIES_FLAREON")];
}

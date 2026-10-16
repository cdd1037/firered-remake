using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Trainers;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class VsSeekerOpponentPokemonCatalog
{
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_MAREEP"), new("pret:SPECIES_FLAAFFY"), new("pret:SPECIES_MARILL")];
}

using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class SaffronGymOpponentPokemonCatalog
{
    public const string Profile = "firered-saffron-gym-opponents-v1";
    public static SpeciesId Slowbro { get; } = new("pret:SPECIES_SLOWBRO");
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } = [Slowbro];
}

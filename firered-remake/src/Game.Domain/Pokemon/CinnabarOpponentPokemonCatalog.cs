using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class CinnabarOpponentPokemonCatalog
{
    public const string Profile = "firered-cinnabar-opponents-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_NINETALES"), new("pret:SPECIES_ARCANINE"), new("pret:SPECIES_RAPIDASH"), new("pret:SPECIES_CLOYSTER")];
}

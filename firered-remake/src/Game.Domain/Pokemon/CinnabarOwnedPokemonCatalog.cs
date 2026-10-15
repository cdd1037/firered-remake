using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class CinnabarOwnedPokemonCatalog
{
    public const string Profile = "firered-cinnabar-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_KRABBY"), new("pret:SPECIES_KINGLER")];
}

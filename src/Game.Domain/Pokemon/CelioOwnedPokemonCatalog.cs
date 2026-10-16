using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class CelioOwnedPokemonCatalog
{
    public const string Profile = "firered-celio-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_ENTEI"), new("pret:SPECIES_RAIKOU"), new("pret:SPECIES_SUICUNE")];
}

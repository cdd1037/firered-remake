using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class SeafoamOwnedPokemonCatalog
{
    public const string Profile = "firered-seafoam-owned-pokemon-v1";
    public static SpeciesId ArticunoId { get; } = new("pret:SPECIES_ARTICUNO");
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } = [ArticunoId];
}

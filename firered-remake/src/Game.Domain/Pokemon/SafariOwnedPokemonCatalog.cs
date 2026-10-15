using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class SafariOwnedPokemonCatalog
{
    public const string Profile = "firered-safari-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_CHANSEY"), new("pret:SPECIES_SCYTHER"), new("pret:SPECIES_TAUROS")];
}

using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class SixIslandOwnedPokemonCatalog
{
    public const string Profile = "firered-six-island-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_SENTRET"), new("pret:SPECIES_FURRET"), new("pret:SPECIES_NATU"),
         new("pret:SPECIES_XATU"), new("pret:SPECIES_YANMA"), new("pret:SPECIES_WOBBUFFET"), new("pret:SPECIES_BELLOSSOM")];
}

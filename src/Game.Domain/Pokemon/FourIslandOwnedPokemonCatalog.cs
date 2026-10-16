using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class FourIslandOwnedPokemonCatalog
{
    public const string Profile = "firered-four-island-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_SEEL"), new("pret:SPECIES_DEWGONG"), new("pret:SPECIES_WOOPER"),
         new("pret:SPECIES_QUAGSIRE"), new("pret:SPECIES_SWINUB"), new("pret:SPECIES_PILOSWINE"), new("pret:SPECIES_DELIBIRD")];
}

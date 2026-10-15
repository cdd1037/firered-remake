using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class PowerPlantOwnedPokemonCatalog
{
    public const string Profile = "firered-power-plant-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_ELECTABUZZ"), new("pret:SPECIES_JOLTEON"), new("pret:SPECIES_ZAPDOS")];
}

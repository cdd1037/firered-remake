using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class BreedingOwnedPokemonCatalog
{
    public const string Profile = "firered-breeding-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_PICHU"), new("pret:SPECIES_CLEFFA"), new("pret:SPECIES_IGGLYBUFF"), new("pret:SPECIES_TYROGUE"),
         new("pret:SPECIES_SMOOCHUM"), new("pret:SPECIES_ELEKID"), new("pret:SPECIES_WYNAUT"), new("pret:SPECIES_HITMONTOP")];
}

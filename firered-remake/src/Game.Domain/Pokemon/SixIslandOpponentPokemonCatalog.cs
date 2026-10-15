using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class SixIslandOpponentPokemonCatalog
{
    public static System.Collections.Immutable.ImmutableArray<Game.Domain.Ids.SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_SUNKERN"), new("pret:SPECIES_SUNFLORA"), new("pret:SPECIES_CHINCHOU"), new("pret:SPECIES_LANTURN"), new("pret:SPECIES_SUDOWOODO")];
    public const string Profile = "firered-six-island-opponents-v1";
}

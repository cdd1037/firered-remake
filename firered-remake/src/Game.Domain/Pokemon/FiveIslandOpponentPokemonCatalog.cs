using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class FiveIslandOpponentPokemonCatalog
{
    public static System.Collections.Immutable.ImmutableArray<Game.Domain.Ids.SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_HOUNDOUR"), new("pret:SPECIES_HOUNDOOM"), new("pret:SPECIES_PORYGON")];
    public const string Profile = "firered-five-island-opponents-v1";
}

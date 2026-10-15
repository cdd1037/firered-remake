using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class GameCornerOwnedPokemonCatalog
{
    public const string Profile = "firered-game-corner-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_DRATINI"), new("pret:SPECIES_DRAGONAIR"), new("pret:SPECIES_DRAGONITE"), new("pret:SPECIES_PORYGON")];
}

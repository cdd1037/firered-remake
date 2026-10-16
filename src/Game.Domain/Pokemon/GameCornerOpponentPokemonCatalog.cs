using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class GameCornerOpponentPokemonCatalog
{
    public const string Profile = "firered-game-corner-opponents-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } = [GameCornerOwnedPokemonCatalog.AdditionalSpecies[0]];
}

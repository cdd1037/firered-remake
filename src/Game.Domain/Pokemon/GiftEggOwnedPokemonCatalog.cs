using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class GiftEggOwnedPokemonCatalog
{
    public const string Profile = "firered-gift-egg-owned-pokemon-v1";
    public const string OpponentProfile = "firered-gift-egg-opponents-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_TOGEPI"), new("pret:SPECIES_TOGETIC")];
}

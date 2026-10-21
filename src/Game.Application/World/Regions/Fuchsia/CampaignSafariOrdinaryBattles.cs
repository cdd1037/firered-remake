using Game.Domain.Pokemon;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private GamePokemonCatalog SafariOrdinaryOwnership => _binding.Ownership;
    // Encounter tables constrain species and levels; the catalog supplies current owned definitions.
    private PokemonCatalog OrdinaryEncounterPokemon => _binding.Ownership.Catalog;

}

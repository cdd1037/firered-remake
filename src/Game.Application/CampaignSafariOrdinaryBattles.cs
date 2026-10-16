using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private GamePokemonCatalog SafariOrdinaryOwnership => _binding.Ownership;
    // Encounter tables constrain species and levels; the catalog supplies current owned definitions.
    private PokemonCatalog OrdinaryEncounterPokemon => _binding.Ownership.Catalog;

    private PokemonCatalog OrdinaryExecutionPokemon(WildBattleState battle)
    {
        if (battle.SourceCampaignRulesVersion is null) return BattlePokemon;
        if (SafariOrdinaryOwnership is { } owned &&
            SourceBattleAdmission.IsOwnedCampaign(owned, battle)) return owned.Mechanics.Catalog;
        throw new InvalidDataException("ordinary_battle_campaign_admission");
    }
}

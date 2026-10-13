using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private OwnedBattlePokemonCatalog SafariOrdinaryOwnership => _binding.Ownership;
    // Encounter tables and original trainer factories retain their historical species scope.
    private PokemonCatalog OrdinaryEncounterPokemon => SafariOrdinaryOwnership?.Predecessor ?? BattlePokemon;

    private PokemonCatalog OrdinaryExecutionPokemon(WildBattleState battle)
    {
        if (battle.SourceCampaignRulesVersion is null) return BattlePokemon;
        if (SafariOrdinaryOwnership is { } owned &&
            SourceBattleAdmission.IsOwnedCampaign(owned, battle)) return owned.Mechanics.Catalog;
        throw new InvalidDataException("ordinary_battle_campaign_admission");
    }
}

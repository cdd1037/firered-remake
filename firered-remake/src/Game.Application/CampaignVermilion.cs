using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private string? ValidateVermilionProgress(CampaignState state)
    {
        if (!_content.VermilionEnabled) return null;
        // Celadon Tea and the Saffron route are not admitted by this corridor.
        if (!_content.RegionalServicesEnabled && (state.Script.Flags.GetValueOrDefault("FLAG_GOT_TEA") ||
            state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_ROUTE5_ROUTE6_ROUTE7_ROUTE8_GATES") != 0))
            return "save_vermilion_saffron_gate_unsupported";
        return null;
    }

    private CampaignState FinishVermilionWildRewards(CampaignState state)
    {
        var battle = state.WildBattle!;
        if (!_content.VermilionEnabled || battle.PostBattleItemsSettled)
            return BeginWildEvolution(state);
        var money = SourceBattleMoneyRules.SettlePayDay(state.Money, battle.State.PayDayMoney, battle.State.Outcome,battle.State.SourceMoneyMultiplier);
        var pickup = SafariOrdinaryOwnership is {} safari?VermilionPickupRules.ApplySafariOwned(safari,state.Party,state.World.Rng,battle.State.Outcome):VermilionPickupRules.Apply(BattlePokemon, state.Party, state.World.Rng, battle.State.Outcome,ssAnneRules:_content.SsAnneEnabled,route11Rules:_content.Route11Enabled,rockTunnelRules:_content.RockTunnelEnabled,celadonApproachRules:_content.CeladonApproachEnabled,celadonGymRules:_content.CeladonGymEnabled,rocketHideoutRules:_content.RocketHideoutEnabled,pokemonTowerRules:_content.PokemonTowerEnabled,snorlaxRules:_content.SnorlaxEnabled,fuchsiaRules:_content.FuchsiaRouteEnabled);
        state = state with
        {
            Money = money.Money, Party = pickup.Party, World = state.World with { Rng = pickup.Rng },
            WildBattle = battle with
            {
                PostBattleItemsSettled = true, State = battle.State with { Rng = pickup.Rng }, RandomTrace = pickup.Trace,
                Pages = money.Amount == 0 ? [] : [new($"{state.PlayerName} picked up\n¥{money.DisplayAmount}!")], PageIndex = 0
            }
        };
        return money.Amount == 0 ? BeginWildEvolution(state) : state;
    }
}

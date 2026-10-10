using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private string? ValidateVermilionProgress(CampaignState state)
    {
        if (!_content.VermilionEnabled) return null;
        // Celadon Tea and the Saffron route are not admitted by this corridor.
        if (state.Script.Flags.GetValueOrDefault("FLAG_GOT_TEA") ||
            state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_ROUTE5_ROUTE6_ROUTE7_ROUTE8_GATES") != 0)
            return "save_vermilion_saffron_gate_unsupported";
        return null;
    }

    private CampaignState FinishVermilionWildRewards(CampaignState state)
    {
        var battle = state.WildBattle!;
        if (!_content.VermilionEnabled || battle.PostBattleItemsSettled)
            return BeginWildEvolution(state);
        var money = VermilionPayDayRules.Settle(state.Money, battle.State.PayDayMoney, battle.State.Outcome);
        var pickup = VermilionPickupRules.Apply(BattlePokemon, state.Party, state.World.Rng, battle.State.Outcome,ssAnneRules:_content.SsAnneEnabled);
        state = state with
        {
            Money = money.Money, Party = pickup.Party, World = state.World with { Rng = pickup.Rng },
            WildBattle = battle with
            {
                PostBattleItemsSettled = true, State = battle.State with { Rng = pickup.Rng }, RandomTrace = pickup.Trace,
                Pages = money.Collected == 0 ? [] : [new($"{state.PlayerName} picked up\n¥{money.Collected}!")], PageIndex = 0
            }
        };
        return money.Collected == 0 ? BeginWildEvolution(state) : state;
    }
}

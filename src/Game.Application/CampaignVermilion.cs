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
        return null;
    }

    private CampaignState FinishVermilionWildRewards(CampaignState state)
    {
        var battle = state.WildBattle!;
        if (battle.PostBattleItemsSettled)
            return BeginWildEvolution(state);
        var money = SourceBattleMoneyRules.SettlePayDay(state.Money, battle.Frame!.Field.PayDayMoney, battle.Frame!.Outcome,battle.Frame!.Field.MoneyMultiplier);
        var pickup = VermilionPickupRules.Apply(_binding.Ownership, state.Party, state.World.Rng, battle.Frame!.Outcome);
        state = state with
        {
            Money = money.Money, Party = pickup.Party, World = state.World with { Rng = pickup.Rng },
            WildBattle = battle with
            {
                PostBattleItemsSettled = true, Frame = battle.Frame! with { Rng = pickup.Rng,
                    Parties = battle.Frame.Parties.SetItem(0,battle.Frame.Parties[0] with{Members=battle.Frame.Parties[0].Members.Select((m,i)=>m with{PersistentHeldItemOverride=pickup.Party[i].HeldItem==(m.KnockedOffItem??m.HeldItem)?null:pickup.Party[i].HeldItem}).ToImmutableArray()}) }, RandomTrace = pickup.Trace,
                Pages = money.Amount == 0 ? [] : [new($"{state.PlayerName} picked up\n¥{money.DisplayAmount}!")], PageIndex = 0
            }
        };
        return money.Amount == 0 ? BeginWildEvolution(state) : state;
    }
}

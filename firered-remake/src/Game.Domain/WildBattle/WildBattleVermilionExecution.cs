using Game.Domain.Battle;

namespace Game.Domain.WildBattle;

public sealed record VermilionPayDaySettlement(int Money,int Awarded,int Collected);
public static class VermilionPayDayRules
{
    /// <summary>givepaydaymoney is reached only on local victory; no represented money-doubling item.</summary>
    public static VermilionPayDaySettlement Settle(int money,int payDayMoney,WildBattleOutcome outcome)
    {
        if(money is <0 or >999999 || payDayMoney is <0 or >65535)throw new ArgumentOutOfRangeException(nameof(money));
        if(!Enum.IsDefined(outcome) || outcome==WildBattleOutcome.Ongoing)throw new ArgumentException("Settlement requires a terminal outcome.");
        int collected=outcome==WildBattleOutcome.Won?payDayMoney:0,after=Math.Min(999999,money+collected);
        return new(after,after-money,collected);
    }
}
public static partial class WildBattleRules
{
    internal sealed partial class Execution
    {
        private void VermilionPayDay(WildBattleSide side)
        {
            int before=State.PayDayMoney;
            int after=side==WildBattleSide.Player?Math.Min(65535,before+Get(side).Pokemon.Level*5):before;
            State=State with{PayDayMoney=after};Add(n=>new WildBattlePayDayEvent(n,State.Turn,side,before,after));
        }
        // MOVEEND_IMMUNITY_ABILITIES scans battler IDs. Entry/faint replacement alone does not run it.
        // Voluntary switching explicitly runs this case before the other battler's selected action.
        internal void VermilionImmunityAbilities()
        {
            if (!Vermilion) return;
            foreach (var side in new[]{WildBattleSide.Player,WildBattleSide.Opponent})
            {
                var mon = Get(side); string ability = WildBattleEffective.Ability(mon);
                var cure = SourceImmunityRules.Resolve(ability, mon.Status, mon.ConfusionTurnsRemaining,
                    mon.InfatuationSource is not null, expanded: State.RulesVersion == FuchsiaRulesVersion, poison: Snorlax);
                if (cure.Cure == SourceImmunityCure.None) continue;
                RecordAbility?.Invoke(side, ability);
                Add(n => new WildBattleAbilityActivatedEvent(n, State.Turn, side, ability));
                if (cure.Cure == SourceImmunityCure.PrimaryStatus) ChangeStatus(side, WildBattleStatus.None, cure.Reason);
                else if (cure.Cure == SourceImmunityCure.Confusion) ChangeConfusion(side, 0, cure.Reason);
                else ChangeInfatuation(side, null, cure.Reason);
            }
        }
        // battle_util.c ITEMEFFECT_MOVE_END scans battler IDs, not speed order. Oran only runs at residuals.
        private void VermilionMoveEndBerries()
        {
            VermilionHeldBerry(WildBattleSide.Player, SourceBerryPhase.MoveEnd);VermilionHeldBerry(WildBattleSide.Opponent, SourceBerryPhase.MoveEnd);
        }
        private void VermilionHeldBerry(WildBattleSide side, SourceBerryPhase phase = SourceBerryPhase.NormalMoveTurn)
        {
            if (SourceCanonicalHeldItem(side, phase)) return;
            var mon=Get(side);
            // Preserve historical profile boundaries while sharing the actual berry recipe.
            var effect=SourceHeldBerryRules.Resolve(mon,SourceBerryPhase.NormalMoveTurn,
                freeze:Route11,toxic:SourceCalledFieldActive);
            bool confusion=effect.CureConfusion,status=effect.CureStatus;
            if(!confusion&&!status)return;
            if(confusion)ChangeConfusion(side,0,"persim-berry");
            if(status)ChangeStatus(side,WildBattleStatus.None,"held-berry");
            Set(side,Get(side) with{HeldItem="ITEM_NONE"});
            Add(n=>new WildBattleHeldItemConsumedEvent(n,State.Turn,side,mon.HeldItem,mon.Pokemon.CurrentHp,mon.Pokemon.CurrentHp,0));
        }
    }
}

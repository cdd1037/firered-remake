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
                if (Snorlax && Get(side).Pokemon.AbilityId == "ABILITY_IMMUNITY" && (Get(side).Status & 0xF88u) != 0)
                { RecordAbility?.Invoke(side,"ABILITY_IMMUNITY"); Add(n => new WildBattleAbilityActivatedEvent(n,State.Turn,side,"ABILITY_IMMUNITY")); ChangeStatus(side,WildBattleStatus.None,"immunity"); }
                if (Get(side).Pokemon.AbilityId == "ABILITY_LIMBER" && Get(side).Status == WildBattleStatus.Paralysis)
                {
                    RecordAbility?.Invoke(side,"ABILITY_LIMBER");
                    Add(n=>new WildBattleAbilityActivatedEvent(n,State.Turn,side,"ABILITY_LIMBER"));
                    ChangeStatus(side,WildBattleStatus.None,"limber");
                }
        }
        }
        // battle_util.c ITEMEFFECT_MOVE_END scans battler IDs, not speed order. Oran only runs at residuals.
        private void VermilionMoveEndBerries()
        {
            VermilionHeldBerry(WildBattleSide.Player);VermilionHeldBerry(WildBattleSide.Opponent);
        }
        private void VermilionHeldBerry(WildBattleSide side)
        {
            var mon=Get(side);if(mon.Pokemon.CurrentHp==0)return;
            bool confusion=mon.HeldItem=="ITEM_PERSIM_BERRY" && mon.ConfusionTurnsRemaining>0;
            bool status=mon.HeldItem switch {
                "ITEM_CHERI_BERRY"=>mon.Status==WildBattleStatus.Paralysis,
                "ITEM_CHESTO_BERRY"=>WildBattleStatus.IsSleeping(mon.Status),
                "ITEM_PECHA_BERRY"=>mon.Status==WildBattleStatus.Poison,
                "ITEM_RAWST_BERRY"=>mon.Status==WildBattleStatus.Burn,
                "ITEM_ASPEAR_BERRY"=>Route11 && mon.Status==WildBattleStatus.Freeze,
                _=>false};
            if(!confusion&&!status)return;
            if(confusion)ChangeConfusion(side,0,"persim-berry");
            if(status)ChangeStatus(side,WildBattleStatus.None,"held-berry");
            Set(side,Get(side) with{HeldItem="ITEM_NONE"});
            Add(n=>new WildBattleHeldItemConsumedEvent(n,State.Turn,side,mon.HeldItem,mon.Pokemon.CurrentHp,mon.Pokemon.CurrentHp,0));
        }
    }
}

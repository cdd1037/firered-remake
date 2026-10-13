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

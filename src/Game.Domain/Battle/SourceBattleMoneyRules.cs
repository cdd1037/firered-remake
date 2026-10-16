using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>The source wallet award and text buffer are separate: Pay Day prints a u16
/// even though AddMoney receives the full u32 product.</summary>
public sealed record SourceBattleMoneySettlement(int Money, int Awarded, int Amount, int DisplayAmount);

/// <summary>Battle-local Amulet Coin latch and the two source money reward commands.
/// Unmultiplied trainer prize already includes any double-battle prize factor.</summary>
public static class SourceBattleMoneyRules
{
    public const int MaximumMoney = 999999;

    // Omitted is the source initialization value 1. Only the latched value 2 is encoded.
    public static bool ValidMultiplier(int? multiplier) => multiplier is null or 2;

    public static int? EntryMultiplier(int? multiplier, string holdEffect)
    {
        if (!ValidMultiplier(multiplier)) throw new ArgumentOutOfRangeException(nameof(multiplier));
        // ItemBattleEffects(ON_SWITCH_IN) has no side or HP guard and consumes no item.
        return holdEffect == "HOLD_EFFECT_DOUBLE_PRIZE" ? 2 : multiplier;
    }

    public static SourceFieldRegisters ApplyEntry(SourceFieldRegisters field, string holdEffect)
    {
        int? multiplier = EntryMultiplier(field.MoneyMultiplier, holdEffect);
        return multiplier == field.MoneyMultiplier ? field : field with { MoneyMultiplier = multiplier };
    }

    public static SourceBattleMoneySettlement SettleTrainerPrize(int money, int unmultipliedPrize,
        WildBattleOutcome outcome, int? multiplier = null)
    {
        if (unmultipliedPrize < 0) throw new ArgumentOutOfRangeException(nameof(unmultipliedPrize));
        Validate(money, outcome, multiplier);
        int amount = outcome == WildBattleOutcome.Won ? checked(unmultipliedPrize * (multiplier ?? 1)) : 0;
        return Settle(money, amount, amount);
    }

    public static SourceBattleMoneySettlement SettlePayDay(int money, int payDayMoney,
        WildBattleOutcome outcome, int? multiplier = null)
    {
        if (payDayMoney is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(payDayMoney));
        Validate(money, outcome, multiplier);
        int amount = outcome == WildBattleOutcome.Won ? payDayMoney * (multiplier ?? 1) : 0;
        return Settle(money, amount, unchecked((ushort)amount));
    }

    private static void Validate(int money, WildBattleOutcome outcome, int? multiplier)
    {
        if (money is < 0 or > MaximumMoney) throw new ArgumentOutOfRangeException(nameof(money));
        if (!ValidMultiplier(multiplier)) throw new ArgumentOutOfRangeException(nameof(multiplier));
        if (!Enum.IsDefined(outcome) || outcome == WildBattleOutcome.Ongoing)
            throw new ArgumentException("Settlement requires a terminal outcome.", nameof(outcome));
    }

    private static SourceBattleMoneySettlement Settle(int money, int amount, int displayAmount)
    {
        int awarded = Math.Min(MaximumMoney - money, amount);
        return new(money + awarded, awarded, amount, displayAmount);
    }
}

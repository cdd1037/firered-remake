using Game.Domain.Events;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // Service owners authenticate their source/context and price before using this shared presentation/debit body.
    private static CampaignState ApplySourceMoneyBox(CampaignState state, StoryEffect effect) => effect.Opcode switch
    {
        "showmoneybox" when effect.Args.SequenceEqual(new[] { "0", "0" }) => state with { MoneyBox = new(state.Money, 0, 0) },
        "hidemoneybox" when effect.Args.IsEmpty => state with { MoneyBox = null },
        "updatemoneybox" when effect.Args.IsEmpty && state.MoneyBox is {} box => state with { MoneyBox = box with { Amount = state.Money } },
        _ => throw new InvalidDataException("source_moneybox_context")
    };
    private static CampaignState SourceMoneyResult(CampaignState state, int amount) => amount < 0
        ? throw new InvalidDataException("source_money_amount")
        : state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", state.Money >= amount ? 1 : 0) } };
    private static CampaignState DebitSourceMoney(CampaignState state, int amount) => amount < 0 || state.Money < amount
        ? throw new InvalidDataException("source_money_payment") : state with { Money = state.Money - amount };
}

using System.Globalization;
using Game.Domain.Events;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private static bool IsHiddenCoinPickup(FieldPickupDefinition definition) =>
        definition.Hidden && definition.SourceItemId == 0 && definition.ItemSymbol == "ITEM_NONE";

    private bool TryApplyHiddenCoinEffect(CampaignState state, FieldPickupDefinition definition,
        CampaignFieldPickup context, StoryEffect effect, out CampaignState next)
    {
        next = state;
        bool capacity = effect.Opcode == "specialvar" && effect.Args.Length == 2 && effect.Args[1] == "CheckAddCoins";
        if (!(capacity || effect.Opcode is "checkcoins" or "addcoins" or "buffernumberstring" or "bufferstdstring")) return false;
        if (!state.Script.Locked || state.Script.Completed ||
            effect.Source.Path != "data/scripts/obtain_item.inc" || definition.Script != "EventScript_HiddenItemScript" ||
            definition.Quantity is < 1 or > 127 || state.Script.Flags.GetValueOrDefault(definition.Flag) ||
            state.Script.Variables.GetValueOrDefault("VAR_0x8004") != definition.SourceFlagId ||
            state.Script.Variables.GetValueOrDefault("VAR_0x8005") != 0 ||
            state.Script.Variables.GetValueOrDefault("VAR_0x8006") != definition.Quantity || ValidateGameCornerCoins(state) is not null)
            throw new InvalidDataException("hidden_coins_context");
        RequireCoinEffect(state, effect);
        var instruction = GameCornerCoinSource.Instruction(state.Script, _content)!;
        string symbol = effect.Source.Symbol;
        bool collecting = symbol == "EventScript_TryPickUpHiddenCoins";
        bool refused = symbol is "EventScript_HiddenCoinsButCaseIsFull" or "EventScript_NoCaseForHiddenCoins";
        CampaignState Result(int value) => state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", value) } };
        if (effect.Opcode == "buffernumberstring")
        {
            if (!(refused || symbol == "EventScript_FoundCoins") ||
                !instruction.Args.SequenceEqual(new[] { "STR_VAR_1", "VAR_0x8006" }))
                throw new InvalidDataException("hidden_coins_number_source");
            next = state with { TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", definition.Quantity.ToString(CultureInfo.InvariantCulture)) }; return true;
        }
        if (effect.Opcode == "bufferstdstring")
        {
            if (!(collecting || refused) || !instruction.Args.SequenceEqual(new[] { "STR_VAR_2", "STDSTRING_COINS" }))
                throw new InvalidDataException("hidden_coins_name_source");
            next = state with { TextBuffers = state.TextBuffers.SetItem("STR_VAR_2", "COINS") }; return true;
        }
        if (!collecting || context.InventoryAdded || !state.Script.Flags.GetValueOrDefault(GameCornerCoinSource.CaseFlag))
            throw new InvalidDataException("hidden_coins_credit_context");
        if (effect.Opcode == "checkcoins")
        {
            if (!instruction.Args.SequenceEqual(new[] { "VAR_RESULT" })) throw new InvalidDataException("hidden_coins_destination");
            next = Result(state.Coins); return true;
        }
        // Original CheckAddCoins uses the complete hidden amount and refuses overflow.
        // Unlike raw AddCoins saturation, this script never takes a partial pickup.
        bool fits = state.Coins + definition.Quantity <= GameCornerCoinSource.Maximum;
        if (capacity)
        {
            if (!instruction.Args.SequenceEqual(new[] { "VAR_RESULT", "CheckAddCoins" }) ||
                state.Script.Variables.GetValueOrDefault("VAR_RESULT") != state.Coins)
                throw new InvalidDataException("hidden_coins_capacity_source");
            next = Result(fits ? 1 : 0); return true;
        }
        if (!instruction.Args.SequenceEqual(new[] { "VAR_0x8006" }) || !fits || state.Script.Variables.GetValueOrDefault("VAR_RESULT") != 1)
            throw new InvalidDataException("hidden_coins_amount_or_capacity");
        var result = GameCornerCoinSource.Add(state.Coins, definition.Quantity);
        next = Result(result.ScriptResult) with { Coins = result.Coins, FieldPickup = context with { InventoryAdded = true } };
        return true;
    }
}

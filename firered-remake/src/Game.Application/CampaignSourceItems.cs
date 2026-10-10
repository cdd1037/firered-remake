using System.Globalization;
using Game.Domain.Events;
using Game.Domain.Items;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private const string RouteClerkScript = "Route1_EventScript_MartClerk";
    private const string RouteClerkSource = "data/maps/Route1/scripts.inc";

    private bool TryApplySourceItemEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Opcode is not ("checkitemspace" or "additem" or "bufferitemname")) return false;
        if (effect.Source.Path != RouteClerkSource || effect.Source.Symbol != RouteClerkScript)
            throw new InvalidDataException("source_item_script_not_supported: " + effect.Source.Symbol);
        var args = effect.Args;
        bool buffer = effect.Opcode == "bufferitemname";
        if (buffer ? args.Length != 2 : args.Length is < 1 or > 2)
            throw new InvalidDataException("source_item_effect_arity");
        int Value(string token) => int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n :
            token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(token[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out n) ? n :
            _content.TryConstant(token, out n) ? n : throw new InvalidDataException("source_item_value_unresolved: " + token);
        // Keep source symbols usable with the old opening catalog too, while validating the imported definition.
        var itemToken = args[buffer ? 1 : 0];
        if (itemToken.StartsWith("ITEM_", StringComparison.Ordinal) && itemToken != "ITEM_POTION")
            throw new InvalidDataException("inventory_item_unsupported");
        int itemId = itemToken == "ITEM_POTION" ? Items.Potion.SourceItemId : Value(itemToken);
        OpeningInventoryRules.ValidateCatalog(Items);
        if (itemId != Items.Potion.SourceItemId) throw new InvalidDataException("inventory_item_unsupported");
        if (buffer)
        {
            if (args[0] is not ("STR_VAR_1" or "STR_VAR_2" or "STR_VAR_3"))
                throw new InvalidDataException("string_buffer_unsupported");
            next = state with { TextBuffers = state.TextBuffers.SetItem(args[0], Items.Potion.Name) };
            return true;
        }
        var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
        int quantity = args.Length == 1 ? 1 : Value(args[1]);
        var result = CampaignPotionGift(inventory, quantity, effect.Opcode == "additem");
        // Only genuine capacity failures map to the original FALSE branch. Invalid/unsupported
        // inventory never masquerades as a full Bag and never partially commits.
        if (!result.Succeeded && result.Error is not ("inventory_stack_full" or "inventory_destination_full"))
            throw new InvalidDataException(result.Error);
        next = state with
        {
            Inventory = result.State,
            Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", result.Succeeded ? 1 : 0) }
        };
        return true;
    }

    private void PreflightSourceItemInteraction(CampaignState state, CampaignActor actor)
    {
        if (actor.ScriptId != RouteClerkScript) return;
        // The common story-entry preflight evaluates the actual remaining dialogue/effects.
        // Reserve more than this fixed source conversation's command count, because a dry
        // run intentionally does not advance CommandSession's authoritative revision.
        if (Revision > long.MaxValue - 128)
            throw new InvalidDataException("source_item_command_headroom_required");
        if (state.Script.Flags.GetValueOrDefault("FLAG_GOT_POTION_ON_ROUTE_1")) return;
        var error = ValidateCampaignInventory(state.Inventory);
        if (error is not null) throw new InvalidDataException(error);
    }
}

using Game.Domain.Events;
using Game.Domain.Items;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool TryApplySeviiRubyItemEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (state.FieldPickup is not null || !InSeviiRubyScript(state) || effect.Opcode is not
            ("checkitem" or "checkitemspace" or "additem" or "removeitem" or "checkitemtype" or "bufferitemname" or "bufferitemnameplural")) return false;
        bool buffer = effect.Opcode.StartsWith("bufferitem", StringComparison.Ordinal);
        var a = effect.Args;
        int id = SeviiValue(state, a[buffer ? 1 : 0]);
        var item = ShopCatalog.Items.SingleOrDefault(i => i.SourceItemId == id);
        if (item is null || item.SourceItemSymbol is not ("ITEM_RUBY" or "ITEM_RAINBOW_PASS" or "ITEM_TRI_PASS")) return false;
        if (item.Id == FirstSeviiShopCatalog.TriPassId && effect.Opcode != "removeitem") return false;
        bool pickup = state.World.Player.MapId.Value == "map.mt-ember-ruby-path-b5f" && item.Id == RubyItem;
        bool exchange = state.World.Player.MapId.Value == RubyCenterMap && state.Script.Stack.Any(f =>
            f.ProgramId is "OneIsland_PokemonCenter_1F_EventScript_GiveCelioRuby" or "OneIsland_PokemonCenter_1F_EventScript_GiveRainbowPass" or
                "OneIsland_PokemonCenter_1F_EventScript_DeclineHelpCelio");
        if ((!pickup && !exchange) || !state.Script.Locked || !state.Script.Flags.GetValueOrDefault("FLAG_GOT_RUBY") ||
            !(SeviiRubySource(effect.Source.Path) || effect.Source.Path is "data/scripts/obtain_item.inc" or "data/event_scripts.s"))
            throw new InvalidDataException("sevii_ruby_item_context");
        CampaignState Result(int value) => state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", value) } };
        if (buffer)
        {
            if (a[0] is not ("STR_VAR_1" or "STR_VAR_2" or "STR_VAR_3")) throw new InvalidDataException("sevii_ruby_item_buffer");
            next = state with { TextBuffers = state.TextBuffers.SetItem(a[0], item.Name) }; return true;
        }
        if (effect.Opcode == "checkitemtype") { next = Result(SeviiValue(state, item.Pocket)); return true; }
        int quantity = a.Length > 1 ? SeviiValue(state, a[1]) : 1;
        if (quantity != 1) throw new InvalidDataException("sevii_ruby_item_quantity");
        var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
        if (ValidateCampaignInventory(inventory) is { } error) throw new InvalidDataException(error);
        var view = ShopInventoryView(inventory);
        if (effect.Opcode == "checkitem")
        { next = Result(OpeningInventoryRules.Quantity(view.KeyItems ?? [], item.Id) >= 1 ? 1 : 0); return true; }
        bool remove = effect.Opcode == "removeitem";
        if (remove && (!exchange || effect.Source.Symbol != "OneIsland_PokemonCenter_1F_EventScript_GiveRainbowPass" ||
            item.Id != RubyItem && item.Id != FirstSeviiShopCatalog.TriPassId)) throw new InvalidDataException("sevii_ruby_remove_source");
        if (effect.Opcode == "additem" && (item.Id == FirstSeviiShopCatalog.TriPassId ||
            item.Id == RainbowPassItem && (!exchange || effect.Source.Symbol != "OneIsland_PokemonCenter_1F_EventScript_GiveRainbowPass" ||
                state.Script.Variables.GetValueOrDefault(RubyCenterScene) != 5))) throw new InvalidDataException("sevii_ruby_add_source");
        var result = remove ? EarlyInventoryAddRules.Remove(ShopCatalog, view, item.Id, quantity) :
            effect.Opcode == "checkitemspace" ? EarlyInventoryAddRules.CheckSpace(ShopCatalog, view, item.Id, quantity) :
            EarlyInventoryAddRules.Add(ShopCatalog, view, item.Id, quantity);
        if (!result.Succeeded && result.Error is not ("inventory_key_items_full" or "inventory_destination_full" or "inventory_stack_full" or "inventory_insufficient_quantity"))
            throw new InvalidDataException(result.Error);
        var changed = inventory;
        if (result.Succeeded && effect.Opcode != "checkitemspace")
        {
            changed = MergeShopInventory(inventory, result.State);
            int count = OpeningInventoryRules.Quantity(result.State.KeyItems ?? [], item.Id);
            var keys = (changed.KeyItems ?? []).RemoveAll(k => k.Item == item.Id);
            changed = changed with { KeyItems = count == 0 ? keys : keys.Add(new(item.Id, count)) };
        }
        next = Result(result.Succeeded ? 1 : 0) with { Inventory = changed }; return true;
    }
}

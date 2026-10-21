using Game.Domain.Events;
using Game.Domain.Items;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool TryApplyFiveIslandItemEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (state.FieldPickup is not null || !InFiveIslandScript(state) ||
            effect.Opcode is not ("checkitem" or "checkitemspace" or "additem" or "checkitemtype" or "bufferitemname" or "bufferitemnameplural")) return false;
        var a = effect.Args;
        bool buffer = effect.Opcode.StartsWith("bufferitem", StringComparison.Ordinal);
        int sourceId = SeviiValue(state, a[buffer ? 1 : 0]);
        var item = ShopCatalog.Items.SingleOrDefault(i => i.SourceItemId == sourceId);
        if (item?.Id != SapphireItem) return false;
        if (!state.Script.Locked || state.World.Player.MapId.Value != FiveIslandWarehouseMap ||
            !state.Script.Flags.GetValueOrDefault(FiveIslandSapphireFlag) ||
            !state.BattledTrainers.Contains("pret:TRAINER_SCIENTIST_GIDEON") ||
            !state.Script.Stack.Any(f => f.ProgramId == "FiveIsland_RocketWarehouse_EventScript_DefeatedGideon") ||
            effect.Source.Path is not (FiveIslandWarehouseSource or "data/scripts/obtain_item.inc" or "data/event_scripts.s"))
            throw new InvalidDataException("five_island_sapphire_source_context");
        CampaignState Result(int value) => state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", value) } };
        if (buffer)
        {
            if (a[0] is not ("STR_VAR_1" or "STR_VAR_2" or "STR_VAR_3") ||
                a.Length != (effect.Opcode == "bufferitemnameplural" ? 3 : 2) ||
                a.Length == 3 && SeviiValue(state, a[2]) != 1) throw new InvalidDataException("five_island_sapphire_buffer");
            next = state with { TextBuffers = state.TextBuffers.SetItem(a[0], item.Name) };
            return true;
        }
        if (effect.Opcode == "checkitemtype") { next = Result(SeviiValue(state, item.Pocket)); return true; }
        int quantity = a.Length > 1 ? SeviiValue(state, a[1]) : 1;
        if (quantity != 1) throw new InvalidDataException("five_island_sapphire_quantity");
        var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
        if (ValidateCampaignInventory(inventory) is {} error) throw new InvalidDataException(error);
        var view = inventory;
        if (effect.Opcode == "checkitem")
        { next = Result(OpeningInventoryRules.Quantity(view.KeyItems ?? [], item.Id) >= 1 ? 1 : 0); return true; }
        var result = effect.Opcode == "checkitemspace" ? EarlyInventoryAddRules.CheckSpace(ShopCatalog, view, item.Id, quantity) :
            EarlyInventoryAddRules.Add(ShopCatalog, view, item.Id, quantity);
        if (!result.Succeeded && result.Error is not ("inventory_key_items_full" or "inventory_destination_full" or "inventory_stack_full"))
            throw new InvalidDataException(result.Error);
        var changed = inventory;
        if (result.Succeeded && effect.Opcode == "additem")
        {
            changed = result.State;
            int count = OpeningInventoryRules.Quantity(result.State.KeyItems ?? [], item.Id);
            var keys = (changed.KeyItems ?? []).RemoveAll(k => k.Item == item.Id);
            changed = changed with { KeyItems = count == 0 ? keys : keys.Add(new(item.Id, count)) };
        }
        next = Result(result.Succeeded ? 1 : 0) with { Inventory = changed };
        return true;
    }
}

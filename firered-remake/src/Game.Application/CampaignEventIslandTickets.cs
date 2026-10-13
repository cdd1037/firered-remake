using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private static readonly ItemId EventAuroraTicket = new("pret:ITEM_AURORA_TICKET");
    private static readonly ItemId EventMysticTicket = new("pret:ITEM_MYSTIC_TICKET");

    // post_battle_event_funcs.c, selected REVISION >= 0xA branch. This runs only
    // inside EnterHallOfFame, after this entry's ribbon recipients are known.
    // The prepared Hall save/retry protocol never invokes it again.
    private CampaignState GrantEventIslandTicketsForHall(CampaignState state, bool gaveAtLeastOneRibbon)
    {
        if (!_content.EventIslandsEnabled || !gaveAtLeastOneRibbon ||
            OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], EventAuroraTicket) >= 1)
            return state;
        var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
        inventory = AddEventIslandTicket(inventory, EventAuroraTicket);
        var flags = state.Script.Flags.SetItem("FLAG_ENABLE_SHIP_BIRTH_ISLAND", true)
            .SetItem("FLAG_RECEIVED_AURORA_TICKET", true);
        inventory = AddEventIslandTicket(inventory, EventMysticTicket);
        flags = flags.SetItem("FLAG_ENABLE_SHIP_NAVEL_ROCK", true)
            .SetItem("FLAG_RECEIVED_MYSTIC_TICKET", true);
        return state with { Inventory = inventory, Script = state.Script with { Flags = flags } };
    }

    private InventoryState AddEventIslandTicket(InventoryState inventory, ItemId ticket)
    {
        var keys = inventory.KeyItems ?? [];
        int index = -1;
        for (int i = 0; i < keys.Length; i++) if (keys[i].Item == ticket) { index = i; break; }
        // The shop view excludes unrelated key items, but the source capacity
        // test counts the complete Bag pocket, including Town Map and TM Case.
        if (index < 0 && keys.Length >= ShopCatalog.QuestLimits.KeyItemSlots) return inventory;
        var result = EarlyInventoryAddRules.Add(ShopCatalog, ShopInventoryView(inventory), ticket, 1);
        // Both AddBagItem return values are intentionally ignored by the source:
        // failure leaves the Bag unchanged and still sets the following flags.
        if (!result.Succeeded) return inventory;
        int count = OpeningInventoryRules.Quantity(result.State.KeyItems ?? [], ticket);
        return inventory with { KeyItems = index < 0 ? keys.Add(new(ticket, count)) : keys.SetItem(index, new(ticket, count)) };
    }
}

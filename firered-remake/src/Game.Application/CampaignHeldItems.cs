using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Application;

public sealed partial class CampaignSession
{
    public bool CanGiveHeldItem(ItemId item) => _content.EarlyShop is {} shop && HeldItemTransferRules.CanHold(shop, item, _content.PewterMachines);

    private Decision<CampaignState> OpenPartyItemMenu(CampaignState state, CampaignCommand command)
    {
        if (_content.EarlyShop is null || !IsStableFieldForParty(state)) return new(CommandStatus.Rejected, state, "held_item_context_invalid");
        if (HeldItemTargetError(state, command) is {} error) return new(CommandStatus.Rejected, state, error);
        return new(CommandStatus.Accepted, state with { ItemMenu = new("party", "held") {
            TargetPartyIndex = command.PartyIndex, TargetInstanceId = command.TargetInstanceId } });
    }

    private static string? HeldItemTargetError(CampaignState state, CampaignCommand command) =>
        command.PartyIndex < 0 || command.PartyIndex >= state.Party.Length ? "item_party_index_invalid" :
        command.TargetInstanceId is null || command.TargetInstanceId != state.Party[command.PartyIndex].InstanceId ? "held_item_target_stale" : null;

    private Decision<CampaignState> DecideHeldItem(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (_content.EarlyShop is not {} shop || state.Inventory is null || state.ItemMenu is not {} menu)
            return No("held_item_context_invalid");
        if (HeldItemTargetError(state, command) is {} targetError) return No(targetError);
        if (menu.TargetInstanceId is not null && (menu.TargetInstanceId != command.TargetInstanceId || menu.TargetPartyIndex != command.PartyIndex))
            return No("held_item_target_stale");
        if (command.Kind == "held_bag_open")
            return menu is { Origin: "party", View: "held" }
                ? new(CommandStatus.Accepted, state with { ItemMenu = menu with { Origin = "bag", View = "bag" } })
                : No("held_item_context_invalid");
        bool take = command.Kind == "field_take", fromPc = command.Kind == "pc_give";
        if (take ? menu is not { Origin: "party", View: "held" } : fromPc ? menu is not { Origin: "pc", View: "withdraw" }
            : menu is not { Origin: "bag", View: "bag" or "berries" or "machines" }) return No("held_item_context_invalid");
        if (command.Quantity != 1) return No("held_item_quantity_invalid");
        if (!take && (string.IsNullOrEmpty(command.Text) || !CanGiveHeldItem(new(command.Text)))) return No("held_item_not_holdable");
        bool offeredMachine = !take && _content.PewterMachines is {} machines && PewterMachineRules.Definition(machines, new(command.Text!)) is not null;
        if (!take && (menu.View == "machines") != offeredMachine) return No("held_item_context_invalid");
        if (!take && menu.View == "berries" && shop.Items.FirstOrDefault(i => i.Id.Value == command.Text)?.Pocket != "POCKET_BERRY_POUCH")
            return No("berry_item_context_invalid");
        if (ValidateCampaignInventory(state.Inventory) is {} invalid) return No(invalid);
        var owned = state.Party[command.PartyIndex];
        // Full strict admission above precedes the legacy shop projection. Retain the
        // TM pocket and CASE for the shared transaction; restore every other Key Item below.
        var view = ShopInventoryView(state.Inventory);
        if (_content.PewterMachines is not null)
            view = view with { Machines = state.Inventory.Machines,
                KeyItems = (view.KeyItems ?? []).AddRange((state.Inventory.KeyItems ?? []).Where(s => s.Item == PewterMachineRules.TmCaseId)) };
        var result = take ? HeldItemTransferRules.Take(shop, view, owned, _content.PewterMachines)
            : HeldItemTransferRules.Give(shop, view, owned, new(command.Text!), fromPc, command.Choice == 1, _content.PewterMachines);
        if (!result.Succeeded) return No(result.Error!);
        var changed = MergeShopInventory(state.Inventory, result.Inventory) with { Machines = result.Inventory.Machines };
        if ((result.Inventory.KeyItems ?? []).Any(s => s.Item == PewterMachineRules.TmCaseId) &&
            !(changed.KeyItems ?? []).Any(s => s.Item == PewterMachineRules.TmCaseId))
        {
            // The narrowed shop view cannot account for all original Key Item slots.
            if ((changed.KeyItems?.Length ?? 0) >= _content.PewterMachines!.KeyItemSlots) return No("inventory_key_items_full");
            changed = changed with { KeyItems = (changed.KeyItems ?? []).Add(new(PewterMachineRules.TmCaseId, 1)) };
        }
        return new(CommandStatus.Accepted, state with { Inventory = changed,
            Party = state.Party.SetItem(command.PartyIndex, result.Pokemon),
            ItemMenu = !take && menu.TargetInstanceId is not null ? menu with { Origin = "party", View = "held" } : menu });
    }
}

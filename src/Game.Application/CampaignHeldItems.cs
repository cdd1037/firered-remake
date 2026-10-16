using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Application;

public sealed partial class CampaignSession
{
    public bool CanGiveHeldItem(ItemId item) => CanGiveMail(item) || _content.EarlyShop is {} shop && HeldItemTransferRules.CanHold(shop, item, _content.PewterMachines);

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
        if (!take && CanGiveMail(new(command.Text!)))
            return fromPc ? No("mail_give_from_bag_required") : OpenMailComposer(state, command, false);
        bool offeredMachine = !take && _content.PewterMachines is {} machines && MachineRules.Definition(machines, new(command.Text!)) is not null;
        if (!take && (menu.View == "machines") != offeredMachine) return No("held_item_context_invalid");
        if (!take && menu.View == "berries" && shop.Items.FirstOrDefault(i => i.Id.Value == command.Text)?.Pocket != "POCKET_BERRY_POUCH")
            return No("berry_item_context_invalid");
        if (ValidateCampaignInventory(state.Inventory) is {} invalid) return No(invalid);
        var owned = state.Party[command.PartyIndex];
        // Shared transactions consume the complete validated inventory directly.
        var inventory = state.Inventory;
        var result = take ? HeldItemTransferRules.Take(shop, inventory, owned, _content.PewterMachines)
            : HeldItemTransferRules.Give(shop, inventory, owned, new(command.Text!), fromPc, command.Choice == 1, _content.PewterMachines);
        if (!result.Succeeded) return No(result.Error!);
        return new(CommandStatus.Accepted, state with { Inventory = result.Inventory,
            Party = state.Party.SetItem(command.PartyIndex, result.Pokemon),
            ItemMenu = !take && menu.TargetInstanceId is not null ? menu with { Origin = "party", View = "held" } : menu });
    }
}

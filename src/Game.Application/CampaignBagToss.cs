using System.Globalization;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Application;

public enum CampaignBagTossPhase { Quantity, Confirm, Result }
public sealed record CampaignBagToss(string Ticket, long Revision, CampaignBagTossPhase Phase,
    ItemId Item, string Pocket, string Name, int Available, int Quantity, string InventoryHash)
{
    [System.Text.Json.Serialization.JsonIgnore] public CampaignBattleTossOwner? BattleOwner { get; init; }
    public string WaitId => Ticket + ":" + Revision.ToString(CultureInfo.InvariantCulture);
    // pret 037335f, src/strings.c:185-187. Preserve source line breaks and plurals.
    public string Message => Phase switch {
        CampaignBagTossPhase.Quantity => "Toss out how many\n" + Name + "(s)?",
        CampaignBagTossPhase.Confirm => "Throw away " + Quantity.ToString(CultureInfo.InvariantCulture) + " of\nthis item?",
        CampaignBagTossPhase.Result => "Threw away " + Quantity.ToString(CultureInfo.InvariantCulture) + "\n" + Name + "(s).",
        _ => throw new InvalidDataException("bag_toss_phase_invalid") };
}

public sealed partial class CampaignSession
{
    public bool CanTossBagItem(ItemId item) => _content.EarlyShop is {} shop && BagTossRules.CanToss(shop, item);

    private Decision<CampaignState> SelectBagPocket(CampaignState state, CampaignCommand command)
    {
        if (state.ItemMenu is not { Origin: "bag", View: "bag", Toss: null } menu ||
            state.MailComposer is not null || state.MailInteraction is not null || command.Choice is not (>= 0 and <= 2))
            return new(CommandStatus.Rejected, state, "bag_pocket_context_invalid");
        return new(CommandStatus.Accepted, state with { ItemMenu = menu with { Pocket = command.Choice.Value } });
    }

    private static string? CurrentTossPocket(CampaignItemMenu menu) => menu.View == "berries" ? "POCKET_BERRY_POUCH"
        : menu.View == "bag" ? menu.Pocket switch { 0 => "POCKET_ITEMS", 2 => "POCKET_POKE_BALLS", _ => null } : null;

    private string? BagTossContextError(CampaignState state)
    {
        if (_content.EarlyShop is null || state.ItemMenu is not { Origin: "bag" or "battle", View: "bag" or "berries", TargetInstanceId: null, TargetPartyIndex: null } ||
            state.MailComposer is not null || state.MailInteraction is not null)
            return "bag_toss_context_invalid";
        if (state.ItemMenu.Origin == "battle")
        {
            if (state.ItemMenu.View != "berries" || BattleTossOwner(state) is null ||
                state.ItemMenu.Toss is {} active && (active.BattleOwner is null || !ValidBattleTossOwner(state, active.BattleOwner)))
                return "battle_toss_context_invalid";
        }
        else if (!IsStableFieldForParty(state with { ItemMenu = null })) return "bag_toss_context_invalid";
        if (ValidateCampaignInventory(state.Inventory) is {} inventoryError) return inventoryError;
        if (ValidateCampaignMail(state) is {} mailError) return mailError;
        if (state.ItemMenu.View == "berries" && (OpeningInventoryRules.Quantity(state.Inventory!.KeyItems ?? [], SsAnneItemRules.BerryPouchId) != 1))
            return "berry_pouch_context_invalid";
        return null;
    }

    private Decision<CampaignState> OpenBagToss(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (BagTossContextError(state) is {} error) return No(error);
        var menu = state.ItemMenu!;
        if (menu.Toss is not null || string.IsNullOrEmpty(command.Text)) return No("bag_toss_context_invalid");
        var item = new ItemId(command.Text);
        if (!CanTossBagItem(item)) return No("bag_toss_item_not_tossable");
        var definition = _content.EarlyShop!.Items.Single(i => i.Id == item);
        if (CurrentTossPocket(menu) != definition.Pocket) return No("bag_toss_pocket_invalid");
        int available = BagTossRules.Available(_content.EarlyShop, state.Inventory!, item);
        if (available < 1) return No("inventory_insufficient_quantity");
        return new(CommandStatus.Accepted, state with { ItemMenu = menu with { Toss = new(
            "toss:" + (Revision + 1).ToString(CultureInfo.InvariantCulture), 0,
            available == 1 ? CampaignBagTossPhase.Confirm : CampaignBagTossPhase.Quantity,
            item, definition.Pocket, definition.Name, available, 1, CanonicalJson.Hash(state.Inventory)) } });
    }

    private Decision<CampaignState> DecideBagToss(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (BagTossContextError(state) is {} error) return No(error);
        var menu = state.ItemMenu!;
        if (menu.Toss is not {} toss) return No("bag_toss_context_invalid");
        if (command.Text != toss.WaitId) return No("bag_toss_stale_input");
        if (CurrentTossPocket(menu) != toss.Pocket) return No("bag_toss_pocket_invalid");
        var shop = _content.EarlyShop!;
        if (!BagTossRules.CanToss(shop, toss.Item) || shop.Items.Single(i => i.Id == toss.Item).Pocket != toss.Pocket)
            return No("bag_toss_item_not_tossable");
        if (toss.InventoryHash != CanonicalJson.Hash(state.Inventory) ||
            toss.Available != BagTossRules.Available(shop, state.Inventory!, toss.Item)) return No("bag_toss_inventory_changed");
        if (toss.Quantity < 1 || toss.Quantity > toss.Available) return No("bag_toss_quantity_invalid");
        Decision<CampaignState> Clear() => new(CommandStatus.Accepted, state with { ItemMenu = menu.Origin == "battle" ? null : menu with { Toss = null } });
        Decision<CampaignState> Set(CampaignBagToss changed) => new(CommandStatus.Accepted,
            state with { ItemMenu = menu with { Toss = changed with { Revision = toss.Revision + 1 } } });
        if (command.Kind == "bag_toss_back" && toss.Phase != CampaignBagTossPhase.Result) return Clear();
        if (toss.Phase == CampaignBagTossPhase.Quantity)
        {
            if (command.Kind == "bag_toss_quantity")
                return command.Quantity >= 1 && command.Quantity <= toss.Available
                    ? Set(toss with { Quantity = command.Quantity }) : No("bag_toss_quantity_invalid");
            return command.Kind == "bag_toss_select" ? Set(toss with { Phase = CampaignBagTossPhase.Confirm }) : No("bag_toss_quantity_required");
        }
        if (toss.Phase == CampaignBagTossPhase.Confirm)
            return command.Kind != "bag_toss_confirm" || command.Choice is not (0 or 1) ? No("bag_toss_confirmation_required")
                : command.Choice == 1 ? Clear() : Set(toss with { Phase = CampaignBagTossPhase.Result });
        if (toss.Phase != CampaignBagTossPhase.Result || command.Kind != "bag_toss_ack") return No("bag_toss_result_ack_required");
        // Original item_menu.c:1558-1578 / berry_pouch.c:1166-1184 removes only on
        // result A/B. All modal phases retain the original inventory and block other commands.
        var result = BagTossRules.Toss(shop, state.Inventory!, toss.Item, toss.Pocket, toss.Quantity);
        if (!result.Succeeded) return No(result.Error!);
        var inventory = result.State;
        if (ValidateCampaignInventory(inventory) is {} invalid) return No(invalid);
        return new(CommandStatus.Accepted, state with { Inventory = inventory, ItemMenu = menu.Origin == "battle" ? null : menu with { Toss = null } });
    }
}

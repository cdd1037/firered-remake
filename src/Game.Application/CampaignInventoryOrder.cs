using Game.Domain.Items;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private Decision<CampaignState> MoveInventoryStack(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (state.ItemMenu is not { Toss: null, TargetInstanceId: null } menu || state.MailComposer is not null ||
            state.MailInteraction is not null || command.Choice is not {} source || string.IsNullOrEmpty(command.Text))
            return No("inventory_move_context");
        InventoryOrderPocket pocket;
        if (menu is { Origin: "pc", View: "withdraw" } && state.PcPoweredOn) pocket = InventoryOrderPocket.Pc;
        else if (menu is { Origin: "bag", View: "bag" } && IsStableFieldForParty(state with { ItemMenu = null }))
            pocket = menu.Pocket switch { 0 => InventoryOrderPocket.Items, 1 => InventoryOrderPocket.KeyItems,
                2 => InventoryOrderPocket.Balls, _ => (InventoryOrderPocket)(-1) };
        else return No("inventory_move_context");
        var result = InventoryOrderRules.Move(_content.Inventory, state.Inventory!, pocket, source, command.Quantity, new(command.Text));
        return result.Succeeded ? new(CommandStatus.Accepted, state with { Inventory = result.State }) : No(result.Error!);
    }
}

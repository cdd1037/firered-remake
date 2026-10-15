using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private Decision<CampaignState> UseFieldPpBoost(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (state.ItemMenu is not { Origin: "bag", View: "bag" } menu ||
            state.Inventory is null || string.IsNullOrEmpty(command.Text) || command.Quantity != 1)
            return No("pp_boost_context_invalid");
        if (command.PartyIndex < 0 || command.PartyIndex >= state.Party.Length)
            return No("item_party_index_invalid");
        var target = state.Party[command.PartyIndex];
        if (command.TargetInstanceId is not > 0 || command.TargetInstanceId != target.InstanceId)
            return No("pp_boost_target_stale");
        if (ValidateCampaignInventory(state.Inventory) is { } inventoryError) return No(inventoryError);
        var result = SourceFieldPpBoostRules.Use(ShopCatalog, _content.PokemonContent!,
            state.Inventory, target, new ItemId(command.Text), command.MoveSlot,
            _content.MapDefinition(state.World.Player.MapId).RegionMapSection);
        if (!result.Succeeded)
            return result.Error == "pp_boost_no_effect"
                ? new(CommandStatus.Accepted, state with { ItemMenu = menu with
                    { View = "message", MessageSymbol = "gText_WontHaveEffect", ReturnView = "bag" } })
                : No(result.Error!);
        const string symbol = "gText_MovesPPIncreased";
        _ = _content.TextPages(symbol); // Resolve presentation before committing the transaction.
        string moveName = _content.BattleContent!.MoveNames[result.Pokemon.Pokemon.Moves[command.MoveSlot].Move.Value];
        return new(CommandStatus.Accepted, state with
        {
            Inventory = result.Inventory,
            Party = state.Party.SetItem(command.PartyIndex, result.Pokemon),
            TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", moveName),
            ItemMenu = menu with { View = "message", MessageSymbol = symbol, ReturnView = "bag" }
        });
    }
}

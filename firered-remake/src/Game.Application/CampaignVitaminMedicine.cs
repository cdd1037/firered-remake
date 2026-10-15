using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private Decision<CampaignState> UseFieldVitamin(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (state.ItemMenu is not { Origin: "bag", View: "bag" } menu ||
            state.Inventory is null || string.IsNullOrEmpty(command.Text) ||
            command.Quantity != 1 || command.MoveSlot != -1)
            return No("vitamin_context_invalid");
        if (command.PartyIndex < 0 || command.PartyIndex >= state.Party.Length)
            return No("item_party_index_invalid");
        var target = state.Party[command.PartyIndex];
        if (command.TargetInstanceId is not > 0 || command.TargetInstanceId != target.InstanceId)
            return No("vitamin_target_stale");
        if (ValidateCampaignInventory(state.Inventory) is { } inventoryError) return No(inventoryError);
        var item = new ItemId(command.Text);
        var result = SourceFieldVitaminRules.Use(ShopCatalog, _content.PokemonContent!,
            state.Inventory, target, item,
            _content.MapDefinition(state.World.Player.MapId).RegionMapSection);
        if (!result.Succeeded)
        {
            if (result.Error != "vitamin_no_effect") return No(result.Error!);
            _ = _content.TextPages("gText_WontHaveEffect");
            return new(CommandStatus.Accepted, state with { ItemMenu = menu with
                { View = "message", MessageSymbol = "gText_WontHaveEffect", ReturnView = "bag" } });
        }
        const string symbol = "gText_PkmnBaseVar2StatIncreased";
        string statSymbol = SourceFieldVitaminRules.Resolve(ShopCatalog, item)!.Stat switch
        {
            SourceVitaminStat.Hp => "gText_ItemEffect_HP",
            SourceVitaminStat.Attack => "gText_ItemEffect_Attack",
            SourceVitaminStat.Defense => "gText_ItemEffect_Defense",
            SourceVitaminStat.Speed => "gText_ItemEffect_Speed",
            SourceVitaminStat.SpecialAttack => "gText_ItemEffect_SpAtk",
            SourceVitaminStat.SpecialDefense => "gText_ItemEffect_SpDef",
            _ => throw new InvalidDataException("vitamin_stat_text")
        };
        // Resolve both source texts before inventory and permanent party commit together.
        _ = _content.TextPages(symbol);
        string statName = string.Join("\n", _content.TextPages(statSymbol));
        return new(CommandStatus.Accepted, state with
        {
            Inventory = result.Inventory,
            Party = state.Party.SetItem(command.PartyIndex, result.Pokemon),
            TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", result.Pokemon.Nickname)
                .SetItem("STR_VAR_2", statName),
            ItemMenu = menu with { View = "message", MessageSymbol = symbol, ReturnView = "bag" }
        });
    }
}

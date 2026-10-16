using System.Globalization;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // Shared Revive and PP medicine transaction. Validate the complete inventory first,
    // then merge only ordinary pockets from the narrow domain result.
    private Decision<CampaignState> UseMtMoonFieldMedicine(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (state.ItemMenu is not { Origin: "bag", View: "bag" or "berries" } menu ||
            state.Inventory is null || string.IsNullOrEmpty(command.Text))
            return No("field_medicine_context_invalid");
        if (!IsFieldMedicineContainer(menu,new ItemId(command.Text))) return No("berry_item_context_invalid");
        if (command.PartyIndex < 0 || command.PartyIndex >= state.Party.Length)
            return No("item_party_index_invalid");
        var inventoryError = ValidateCampaignInventory(state.Inventory);
        if (inventoryError is not null) return No(inventoryError);
        var result = MtMoonFieldMedicineRules.Use(ShopCatalog, _content.PokemonContent!,
            state.Inventory, state.Party[command.PartyIndex], new ItemId(command.Text),
            command.MoveSlot == -1 ? null : command.MoveSlot);
        if (!result.Succeeded)
            return result.Error == "medicine_no_effect"
                ? new(CommandStatus.Accepted, state with { ItemMenu = menu with
                    { View = "message", MessageSymbol = "gText_WontHaveEffect", ReturnView = menu.View } })
                : No(result.Error!);
        string symbol = result.PpRestored > 0 ? "gText_PPWasRestored" : "gText_PkmnHPRestoredByVar2";
        _ = _content.TextPages(symbol); // Fail before consuming if presentation data is missing.
        return new(CommandStatus.Accepted, state with
        {
            Inventory = result.Inventory,
            Party = state.Party.SetItem(command.PartyIndex, result.Pokemon),
            TextBuffers = result.HpRestored > 0 ? state.TextBuffers
                .SetItem("STR_VAR_1", result.Pokemon.Nickname)
                .SetItem("STR_VAR_2", result.HpRestored.ToString(CultureInfo.InvariantCulture)) : state.TextBuffers,
            ItemMenu = menu with { View = "message", MessageSymbol = symbol, ReturnView = menu.View }
        });
    }
}

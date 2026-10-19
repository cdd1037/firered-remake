using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Items;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // AddBagItem can acquire the Berry Pouch as part of another inventory transaction.
    // Keep its script flag in the same commit for gifts, pickups and held-item returns.
    private static CampaignState CommitInventory(CampaignState state, InventoryState inventory) => state with
    {
        Inventory = inventory,
        Script = OpeningInventoryRules.Quantity(inventory.KeyItems ?? [], SsAnneItemRules.BerryPouchId) == 1
            && OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], SsAnneItemRules.BerryPouchId) == 0
            ? state.Script with { Flags = state.Script.Flags.SetItem("FLAG_SYS_GOT_BERRY_POUCH", true) }
            : state.Script
    };

    private string? ValidateCampaignInventory(InventoryState? inventory)
    {
        var error = _content.Inventory.Validate(inventory);
        if (error is not null) return error;
        // The TM Case and machine stack rules are gameplay constraints, not region provenance.
        return _content.Machines is {} machines ? MachineRules.Validate(machines, inventory!) : null;
    }
    private InventoryState PotionView(InventoryState inventory)
    {
        if (_content.EarlyShop is null) return inventory;
        var error = ValidateCampaignInventory(inventory);
        if (error is not null) throw new InvalidDataException(error);
        return inventory with { BagItems = inventory.BagItems.Where(s => s.Item == OpeningInventoryRules.PotionId).ToImmutableArray(),
            PcItems = inventory.PcItems.Where(s => s.Item == OpeningInventoryRules.PotionId).ToImmutableArray() };
    }
    private static ImmutableArray<InventoryStack> MergePotion(ImmutableArray<InventoryStack> original, ImmutableArray<InventoryStack> changed)
    {
        int count = OpeningInventoryRules.Quantity(changed, OpeningInventoryRules.PotionId);
        int index = -1;
        for (int n = 0; n < original.Length; n++) if (original[n].Item == OpeningInventoryRules.PotionId) { index = n; break; }
        return index < 0 ? count == 0 ? original : original.Add(new(OpeningInventoryRules.PotionId, count))
            : count == 0 ? original.RemoveAt(index) : original.SetItem(index, new(OpeningInventoryRules.PotionId, count));
    }
    private InventoryState MergePotionView(InventoryState original, InventoryState changed) => _content.EarlyShop is null ? changed
        : original with { BagItems = MergePotion(original.BagItems, changed.BagItems), PcItems = MergePotion(original.PcItems, changed.PcItems) };
    private PotionUseResult UseCampaignPotion(InventoryState inventory, PokemonInstance pokemon, PotionUseContext context)
    {
        var result = OpeningPotionRules.Use(Items, BattlePokemon, PotionView(inventory), pokemon, context);
        return result with { Inventory = result.Succeeded ? MergePotionView(inventory, result.Inventory) : inventory };
    }
    private PartyPotionUseResult UseCampaignPartyPotion(InventoryState inventory, PartyPokemon pokemon, PotionUseContext context)
    {
        var result = OpeningPotionRules.UseOnParty(Items, _content.PokemonContent!, PotionView(inventory), pokemon, context);
        return result with { Inventory = result.Succeeded ? MergePotionView(inventory, result.Inventory) : inventory };
    }
    private InventoryTransactionResult CampaignPotionGift(InventoryState inventory, int quantity, bool add)
    {
        var view = PotionView(inventory);
        var result = add ? SourcePotionGiftRules.Add(Items, view, OpeningInventoryRules.PotionId, quantity)
            : SourcePotionGiftRules.CheckSpace(Items, view, OpeningInventoryRules.PotionId, quantity);
        return result with { State = result.Succeeded && add ? MergePotionView(inventory, result.State) : inventory };
    }
    private bool IsFieldMedicineContainer(CampaignItemMenu menu,ItemId item) =>
        menu.Origin=="bag" && _content.Inventory.Items.TryGetValue(item,out var definition) &&
        (menu.View=="berries" ? definition.Pocket=="POCKET_BERRY_POUCH" : menu.View=="bag" && definition.Pocket=="POCKET_ITEMS");

    private Decision<CampaignState> UseFieldMedicine(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (_content.EarlyShop is not { } shop || state.ItemMenu is not { Origin: "bag", View: "bag" or "berries" } menu || string.IsNullOrEmpty(command.Text)) return No("field_medicine_context_invalid");
        if (!IsFieldMedicineContainer(menu,new ItemId(command.Text))) return No("berry_item_context_invalid");
        if (command.PartyIndex < 0 || command.PartyIndex >= state.Party.Length) return No("item_party_index_invalid");
        var item = new ItemId(command.Text);
        var result = _content.PresetDoubleItems is {} sourceHpItems && SourceFieldHpMedicineRules.Supports(shop, sourceHpItems, item)
            ? SourceFieldHpMedicineRules.Use(shop, sourceHpItems, _content.PokemonContent!, state.Inventory!, state.Party[command.PartyIndex], item)
            : _content.PresetDoubleItems is {} sourceItems && SourceFieldStatusCureRules.Supports(shop, sourceItems, item)
            ? SourceFieldStatusCureRules.Use(shop, sourceItems, _content.PokemonContent!, state.Inventory!, state.Party[command.PartyIndex], item)
            : EarlyShopMedicineRules.Use(shop, Items, _content.PokemonContent!, state.Inventory!, state.Party[command.PartyIndex], item, PotionUseContext.Field);
        if (!result.Succeeded)
        {
            if (result.Error is "medicine_no_effect" or "potion_no_effect") return new(CommandStatus.Accepted, state with { ItemMenu = menu with { View = "message", MessageSymbol = "gText_WontHaveEffect", ReturnView = menu.View } });
            return No(result.Error!);
        }
        // Status cure returns directly to the Bag. HP restoration retains the existing source text.
        return new(CommandStatus.Accepted, state with { Inventory = result.Inventory, Party = state.Party.SetItem(command.PartyIndex, result.Pokemon),
            TextBuffers = result.HpRestored > 0 ? state.TextBuffers.SetItem("STR_VAR_1", PokemonName(result.Pokemon)).SetItem("STR_VAR_2", result.HpRestored.ToString(System.Globalization.CultureInfo.InvariantCulture)) : state.TextBuffers,
            ItemMenu = result.HpRestored > 0 ? menu with { View = "message", MessageSymbol = "gText_PkmnHPRestoredByVar2", ReturnView = menu.View } : menu });
    }
}

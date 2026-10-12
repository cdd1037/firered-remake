using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Items;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // The expanded profile is opt-in. Legacy rules retain their strict validators; adapters
    // validate the full inventory before narrowing and merge only the named changed stack.
    private string? ValidateCampaignInventory(InventoryState? inventory)
    {
        if(inventory is null)return "inventory_missing";
        var safariError=ValidateSafariInventory(inventory);if(safariError is not null)return safariError;
        var teaError=ValidateRegionalTeaInventory(inventory);if(teaError is not null)return teaError;
        var cityError=ValidateCityServiceInventory(inventory);if(cityError is not null)return cityError;
        if(_content.PewterMachines is {} machines){var error=PewterMachineRules.Validate(machines,inventory);if(error is not null)return error;}
        else if(inventory.Machines is not null)return "inventory_machine_profile_missing";
        if (_content.SsAnneEnabled) { var full=EarlyShopInventoryRules.Validate(_content.EarlyShop!,PewterMachineRules.WithoutMachines(inventory) with { KeyItems=inventory.KeyItems is {} all?all.Where(s=>s.Item!=PewterMachineRules.TmCaseId&&s.Item!=QuestItemRules.TownMapId&&(!_content.CityServicesEnabled||!IsCityServiceItem(s.Item))&&(!_content.RegionalServicesEnabled||s.Item!=TeaId)&&(!_content.SafariEnabled||s.Item!=GoldTeeth)).ToImmutableArray():null }); if(full is not null)return full; }
        else if (inventory.Berries is not null) return "inventory_berry_profile_missing";
        if(_content.QuestItems is {} quests){var error=ValidateQuestInventory(quests,inventory);if(error is not null)return error;}
        var view=ShopInventoryView(inventory);
        return _content.EarlyShop is {} shop?EarlyShopInventoryRules.Validate(shop,view):OpeningInventoryRules.Validate(Items,view);
    }
    private InventoryState ShopInventoryView(InventoryState inventory)
    {
        var view=WithoutCityServiceItems(_content.PewterMachines is not null?PewterMachineRules.WithoutMachines(inventory):inventory);
        // The historical shop catalog owns ordinary merchandise, not this new Key Item.
        // Validate the expanded quest pocket before narrowing; every shop/medicine merge
        // preserves the original Key Items pocket and therefore never drops Town Map.
        if(!_content.TownMapEnabled)return view;
        var error=ValidateQuestInventory(_content.QuestItems!,inventory);
        if(error is not null)throw new InvalidDataException(error);
        return view with{KeyItems=view.KeyItems is {} keys?keys.Where(s=>s.Item!=QuestItemRules.TownMapId).ToImmutableArray():null};
    }
    private static InventoryState MergeShopInventory(InventoryState original,InventoryState changed)
    {
        var keys=original.KeyItems;
        if((changed.KeyItems??[]).Any(s=>s.Item==SsAnneItemRules.BerryPouchId)&&!(keys??[]).Any(s=>s.Item==SsAnneItemRules.BerryPouchId))
            keys=(keys??[]).Add(new(SsAnneItemRules.BerryPouchId,1));
        if((changed.KeyItems??[]).Any(s=>s.Item==SilphShopCatalog.CardKeyId)&&!(keys??[]).Any(s=>s.Item==SilphShopCatalog.CardKeyId))
            keys=(keys??[]).Add(new(SilphShopCatalog.CardKeyId,1));
        return original with{BagItems=changed.BagItems,PcItems=changed.PcItems,Balls=changed.Balls,Berries=changed.Berries,KeyItems=keys};
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
    private InventoryTransactionResult TransferCampaignPotion(InventoryState inventory, int quantity, bool withdraw)
    {
        var view = PotionView(inventory);
        var result = withdraw ? OpeningInventoryRules.Withdraw(Items, view, OpeningInventoryRules.PotionId, quantity)
            : OpeningInventoryRules.Deposit(Items, view, OpeningInventoryRules.PotionId, quantity);
        return result with { State = result.Succeeded ? MergePotionView(inventory, result.State) : inventory };
    }
    private InventoryTransactionResult CampaignPotionGift(InventoryState inventory, int quantity, bool add)
    {
        var view = PotionView(inventory);
        var result = add ? SourcePotionGiftRules.Add(Items, view, OpeningInventoryRules.PotionId, quantity)
            : SourcePotionGiftRules.CheckSpace(Items, view, OpeningInventoryRules.PotionId, quantity);
        return result with { State = result.Succeeded && add ? MergePotionView(inventory, result.State) : inventory };
    }
    private Decision<CampaignState> UseFieldMedicine(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (_content.EarlyShop is not { } shop || state.ItemMenu is not { Origin: "bag", View: "bag" or "berries" } menu || string.IsNullOrEmpty(command.Text)) return No("field_medicine_context_invalid");
        if (menu.View=="berries" && (!_content.SsAnneEnabled||!(_content.CeladonApproachEnabled?LavenderItemRules.IsBerry(new(command.Text)):_content.RockTunnelEnabled?RockTunnelItemRules.IsBerry(new(command.Text)):_content.Route11Enabled?Route11ItemRules.IsBerry(new(command.Text)):SsAnneItemRules.IsBerry(new(command.Text))))) return No("berry_item_context_invalid");
        if (command.PartyIndex < 0 || command.PartyIndex >= state.Party.Length) return No("item_party_index_invalid");
        var result = EarlyShopMedicineRules.Use(shop, Items, _content.PokemonContent!, ShopInventoryView(state.Inventory!), state.Party[command.PartyIndex], new ItemId(command.Text), PotionUseContext.Field);
        if (!result.Succeeded)
        {
            if (result.Error is "medicine_no_effect" or "potion_no_effect") return new(CommandStatus.Accepted, state with { ItemMenu = menu with { View = "message", MessageSymbol = "gText_WontHaveEffect", ReturnView = menu.View } });
            return No(result.Error!);
        }
        // Status cure returns directly to the Bag. HP restoration retains the existing source text.
        return new(CommandStatus.Accepted, state with { Inventory = MergeShopInventory(state.Inventory!,result.Inventory), Party = state.Party.SetItem(command.PartyIndex, result.Pokemon),
            TextBuffers = result.HpRestored > 0 ? state.TextBuffers.SetItem("STR_VAR_1", result.Pokemon.Nickname).SetItem("STR_VAR_2", result.HpRestored.ToString(System.Globalization.CultureInfo.InvariantCulture)) : state.TextBuffers,
            ItemMenu = result.HpRestored > 0 ? menu with { View = "message", MessageSymbol = "gText_PkmnHPRestoredByVar2", ReturnView = menu.View } : menu });
    }
}

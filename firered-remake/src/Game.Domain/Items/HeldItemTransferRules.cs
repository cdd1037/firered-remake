using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

public sealed record HeldItemTransferResult(InventoryState Inventory, PartyPokemon Pokemon, bool Succeeded, string? Error);

/// <summary>Ordinary source Give/Take: bag removal precedes return-space checking on exchange.
/// Immutable results make the source rollback atomic; no creature regeneration or RNG.</summary>
public static class HeldItemTransferRules
{
    public static bool CanHold(EarlyShopCatalog catalog, ItemId item)
    {
        EarlyShopInventoryRules.ValidateCatalog(catalog);
        return EarlyShopInventoryRules.Find(catalog, item) is { Importance: 0 } definition &&
            definition.Pocket is "POCKET_ITEMS" or "POCKET_BERRY_POUCH" or "POCKET_POKE_BALLS" &&
            definition.SourceItemSymbol != "ITEM_ENIGMA_BERRY" && !PokemonStorageRules.IsMailItem(definition.SourceItemSymbol);
    }

    public static HeldItemTransferResult Give(EarlyShopCatalog catalog, InventoryState inventory,
        PartyPokemon pokemon, ItemId item, bool fromPc, bool exchangeConfirmed)
    {
        HeldItemTransferResult No(string error) => new(inventory, pokemon, false, error);
        if (EarlyShopInventoryRules.Validate(catalog, inventory) is {} invalid) return No(invalid);
        if (pokemon.IsEgg || pokemon.Mail != 255 || PokemonStorageRules.IsMailItem(pokemon.HeldItem)) return No("held_item_mail_or_egg_unsupported");
        if (!CanHold(catalog, item)) return No("held_item_not_holdable");
        var definition = EarlyShopInventoryRules.Find(catalog, item)!;
        int count = OpeningInventoryRules.Quantity(fromPc ? inventory.PcItems : EarlyShopInventoryRules.Pocket(inventory, definition), item);
        if (count == 0) return No("inventory_insufficient_quantity");
        if (pokemon.HeldItem != "ITEM_NONE" && !exchangeConfirmed) return No("held_item_exchange_confirmation_required");
        var removed = fromPc ? inventory with { PcItems = OpeningInventoryRules.SetQuantity(inventory.PcItems, item, count - 1) }
            : EarlyShopInventoryRules.SetQuantity(inventory, definition, count - 1);
        if (pokemon.HeldItem != "ITEM_NONE")
        {
            var returned = ReturnToBag(catalog, removed, pokemon);
            if (!returned.Succeeded) return No(returned.Error!);
            removed = returned.Inventory;
        }
        return new(removed, pokemon with { HeldItem = definition.SourceItemSymbol }, true, null);
    }

    public static HeldItemTransferResult Take(EarlyShopCatalog catalog, InventoryState inventory, PartyPokemon pokemon)
    {
        if (EarlyShopInventoryRules.Validate(catalog, inventory) is {} error) return new(inventory, pokemon, false, error);
        return ReturnToBag(catalog, inventory, pokemon);
    }

    private static HeldItemTransferResult ReturnToBag(EarlyShopCatalog catalog, InventoryState inventory, PartyPokemon pokemon)
    {
        HeldItemTransferResult No(string error) => new(inventory, pokemon, false, error);
        if (pokemon.IsEgg || pokemon.Mail != 255 || PokemonStorageRules.IsMailItem(pokemon.HeldItem)) return No("held_item_mail_or_egg_unsupported");
        if (pokemon.HeldItem == "ITEM_NONE") return No("held_item_none");
        var id = new ItemId("pret:" + pokemon.HeldItem);
        if (!CanHold(catalog, id)) return No("held_item_not_holdable");
        var definition = EarlyShopInventoryRules.Find(catalog, id)!;
        var pocket = EarlyShopInventoryRules.Pocket(inventory, definition);
        int count = OpeningInventoryRules.Quantity(pocket, id);
        if (count >= catalog.Limits.MaximumStack) return No("inventory_stack_full");
        int capacity = definition.Pocket switch { "POCKET_BERRY_POUCH" => SsAnneItemRules.BerrySlots,
            "POCKET_POKE_BALLS" => catalog.QuestLimits.BallSlots, _ => catalog.Limits.BagItemSlots };
        if (count == 0 && pocket.Length >= capacity) return No("inventory_destination_full");
        return new(EarlyShopInventoryRules.SetQuantity(inventory, definition, count + 1),
            pokemon with { HeldItem = "ITEM_NONE" }, true, null);
    }
}

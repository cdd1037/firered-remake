using Game.Domain.Ids;

namespace Game.Domain.Items;

/// <summary>Pure, atomic transactions. UI dialogue, script progress, quest gates and purchase history belong to Application.</summary>
public static class EarlyShopRules
{
    public static EarlyShopQuote QuoteBuy(EarlyShopCatalog catalog, InventoryState inventory, int money, string shopId, ItemId item)
    {
        string? error = EarlyShopInventoryRules.Validate(catalog, inventory);
        if (error is not null) return new(false, 0, 0, error);
        if (money < 0 || money > catalog.MaximumMoney) return new(false, 0, 0, "shop_money_invalid");
        var shop = catalog.Shops.FirstOrDefault(s => s.Id == shopId);
        if (shop is null) return new(false, 0, 0, "shop_unsupported");
        var definition = EarlyShopInventoryRules.Find(catalog, item);
        if (definition is null || !shop.Items.Contains(item)) return new(false, 0, 0, "shop_item_not_stocked");
        return QuoteBuyValidated(catalog, inventory, money, definition);
    }

    public static EarlyShopQuote QuoteBuy(EarlyShopCatalog catalog, InventoryState inventory, int money,
        AuthenticatedShopStock stock, ItemId item)
    {
        string? error = EarlyShopInventoryRules.Validate(catalog, inventory);
        if (error is not null) return new(false, 0, 0, error);
        if (money < 0 || money > catalog.MaximumMoney) return new(false, 0, 0, "shop_money_invalid");
        ArgumentNullException.ThrowIfNull(stock);
        if (stock is not (DepartmentStoreOrdinaryStock or PokemonLeagueMartStock))
            throw new NotSupportedException("shop_stock_authentication");
        stock.Validate(catalog);
        var definition = EarlyShopInventoryRules.Find(catalog, item);
        if (definition is null || !stock.Items.Contains(item)) return new(false, 0, 0, "shop_item_not_stocked");
        return QuoteBuyValidated(catalog, inventory, money, definition);
    }

    private static EarlyShopQuote QuoteBuyValidated(EarlyShopCatalog catalog, InventoryState inventory, int money,
        EarlyShopItemDefinition definition)
    {
        var pocket = EarlyShopInventoryRules.Pocket(inventory, definition);
        int count = OpeningInventoryRules.Quantity(pocket, definition.Id);
        int capacity = definition.Pocket == "POCKET_POKE_BALLS" ? catalog.QuestLimits.BallSlots : catalog.Limits.BagItemSlots;
        int remaining = count == 0 && pocket.Length >= capacity ? 0 : catalog.Limits.MaximumStack - count;
        int maximum = Math.Min(catalog.MaximumTransactionQuantity, money / definition.Price);
        return new(true, maximum, definition.Price, null) { MaximumExecutableQuantity = Math.Min(maximum, remaining) };
    }

    public static EarlyShopQuote QuoteSell(EarlyShopCatalog catalog, InventoryState inventory, int money, ItemId item)
    {
        string? error = EarlyShopInventoryRules.Validate(catalog, inventory);
        if (error is not null) return new(false, 0, 0, error);
        if (money < 0 || money > catalog.MaximumMoney) return new(false, 0, 0, "shop_money_invalid");
        var definition = EarlyShopInventoryRules.Find(catalog, item);
        if (definition is null || definition.Price <= 0 || definition.Importance != 0 || definition.Pocket == "POCKET_KEY_ITEMS")
            return new(false, 0, 0, "shop_item_not_sellable");
        int count = OpeningInventoryRules.Quantity(EarlyShopInventoryRules.Pocket(inventory, definition), item);
        int maximum = Math.Min(catalog.MaximumTransactionQuantity, count);
        return new(true, maximum, definition.Price / 2, null) { MaximumExecutableQuantity = maximum };
    }

    public static EarlyShopTransactionResult Buy(EarlyShopCatalog catalog, InventoryState inventory, int money,
        string shopId, ItemId item, int quantity)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        return BuyQuoted(catalog, inventory, money, item, quantity, QuoteBuy(catalog, inventory, money, shopId, item));
    }

    public static EarlyShopTransactionResult Buy(EarlyShopCatalog catalog, InventoryState inventory, int money,
        AuthenticatedShopStock stock, ItemId item, int quantity)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        return BuyQuoted(catalog, inventory, money, item, quantity, QuoteBuy(catalog, inventory, money, stock, item));
    }

    private static EarlyShopTransactionResult BuyQuoted(EarlyShopCatalog catalog, InventoryState inventory, int money,
        ItemId item, int quantity, EarlyShopQuote quote)
    {
        EarlyShopTransactionResult Fail(string error) => new(inventory, money, false, 0, 0, 0, error);
        if (!quote.Succeeded) return Fail(quote.Error!);
        if (quantity < 1 || quantity > catalog.MaximumTransactionQuantity) return Fail("shop_quantity_invalid");
        var definition = EarlyShopInventoryRules.Find(catalog, item)!;
        int total = checked(quote.UnitPrice * quantity); // Both factors are authenticated and bounded.
        if (money < total) return Fail("shop_insufficient_money");
        var pocket = EarlyShopInventoryRules.Pocket(inventory, definition);
        int count = OpeningInventoryRules.Quantity(pocket, item);
        if (count > catalog.Limits.MaximumStack - quantity) return Fail("inventory_stack_full");
        int capacity = definition.Pocket == "POCKET_POKE_BALLS" ? catalog.QuestLimits.BallSlots : catalog.Limits.BagItemSlots;
        if (count == 0 && pocket.Length >= capacity) return Fail("inventory_destination_full");
        return new(EarlyShopInventoryRules.SetQuantity(inventory, definition, count + quantity), money - total,
            true, quantity, total, -total, null);
    }

    public static EarlyShopTransactionResult Sell(EarlyShopCatalog catalog, InventoryState inventory, int money, ItemId item, int quantity)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        EarlyShopTransactionResult Fail(string error) => new(inventory, money, false, 0, 0, 0, error);
        var quote = QuoteSell(catalog, inventory, money, item);
        if (!quote.Succeeded) return Fail(quote.Error!);
        if (quantity < 1 || quantity > catalog.MaximumTransactionQuantity) return Fail("shop_quantity_invalid");
        if (quantity > quote.MaximumQuantity) return Fail("inventory_insufficient_quantity");
        var definition = EarlyShopInventoryRules.Find(catalog, item)!;
        int count = OpeningInventoryRules.Quantity(EarlyShopInventoryRules.Pocket(inventory, definition), item);
        int total = checked(quote.UnitPrice * quantity);
        // Source AddMoney caps the credit, even when the player is already at the cap. The sale still consumes items.
        int afterMoney = (int)Math.Min(catalog.MaximumMoney, (long)money + total);
        return new(EarlyShopInventoryRules.SetQuantity(inventory, definition, count - quantity), afterMoney,
            true, quantity, total, afterMoney - money, null);
    }
}

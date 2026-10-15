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
        if (stock is not (DepartmentStoreOrdinaryStock or DepartmentStoreCounterStock or PokemonLeagueMartStock or PokeDollShopStock))
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
        return QuoteBuyValidated(catalog, money, definition.Price, remaining);
    }

    private static EarlyShopQuote QuoteBuyValidated(EarlyShopCatalog catalog, int money, int price, int remaining)
    {
        int maximum = Math.Min(catalog.MaximumTransactionQuantity, money / price);
        return new(true, maximum, price, null) { MaximumExecutableQuantity = Math.Min(maximum, remaining) };
    }

    /// <summary>Machine stock shares pricing and atomic money commit; machine rules own its pocket and CASE insertion.</summary>
    public static EarlyShopQuote QuoteBuy(EarlyShopCatalog catalog, InventoryState inventory, int money,
        AuthenticatedMachineShopStock stock, ItemId item)
    {
        ArgumentNullException.ThrowIfNull(stock); stock.Validate();
        string? error = ValidateMachineShop(catalog, stock.Machines, inventory, money);
        if (error is not null) return new(false, 0, 0, error);
        var definition = MachineRules.Definition(stock.Machines, item);
        if (definition is null || !stock.Items.Contains(item)) return new(false, 0, 0, "shop_item_not_stocked");
        int count = OpeningInventoryRules.Quantity(inventory.Machines ?? [], item);
        int remaining = count == 0 && (inventory.Machines?.Length ?? 0) >= stock.Machines.MachineSlots
            ? 0 : stock.Machines.MaximumStack - count;
        if (OpeningInventoryRules.Quantity(inventory.KeyItems ?? [], MachineRules.TmCaseId) == 0 &&
            (inventory.KeyItems?.Length ?? 0) >= stock.Machines.KeyItemSlots) remaining = 0;
        return QuoteBuyValidated(catalog, money, definition.Machine.Item.Price, remaining);
    }

    public static EarlyShopQuote QuoteSell(EarlyShopCatalog catalog, MachineCatalog machines,
        InventoryState inventory, int money, ItemId item)
    {
        string? error = ValidateMachineShop(catalog, machines, inventory, money);
        if (error is not null) return new(false, 0, 0, error);
        var definition = MachineRules.Definition(machines, item)?.Machine.Item;
        if (definition is null || definition.Price <= 0 || !definition.CanSell)
            return new(false, 0, 0, "shop_item_not_sellable");
        int maximum = Math.Min(catalog.MaximumTransactionQuantity, OpeningInventoryRules.Quantity(inventory.Machines ?? [], item));
        return new(true, maximum, definition.Price / 2, null) { MaximumExecutableQuantity = maximum };
    }

    private static string? ValidateMachineShop(EarlyShopCatalog catalog, MachineCatalog machines,
        InventoryState inventory, int money)
    {
        // The caller validates the full campaign inventory. This service validates the
        // machine/CASE pockets it owns and preserves every unrelated pocket verbatim.
        string? error = EarlyShopInventoryRules.Validate(catalog, inventory) ?? MachineRules.Validate(machines, inventory);
        if (error is not null) return error;
        if (inventory.Limits != catalog.Limits) return "inventory_limits";
        return money < 0 || money > catalog.MaximumMoney ? "shop_money_invalid" : null;
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

    public static EarlyShopTransactionResult Buy(EarlyShopCatalog catalog, InventoryState inventory, int money,
        AuthenticatedMachineShopStock stock, ItemId item, int quantity)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var quote = QuoteBuy(catalog, inventory, money, stock, item);
        return BuyQuoted(catalog, inventory, money, quantity, quote,
            () => MachineRules.AddMachine(stock.Machines, inventory, item, quantity));
    }

    private static EarlyShopTransactionResult BuyQuoted(EarlyShopCatalog catalog, InventoryState inventory, int money,
        ItemId item, int quantity, EarlyShopQuote quote) => BuyQuoted(catalog, inventory, money, quantity, quote, () =>
    {
        var definition = EarlyShopInventoryRules.Find(catalog, item)!;
        var pocket = EarlyShopInventoryRules.Pocket(inventory, definition);
        int count = OpeningInventoryRules.Quantity(pocket, item);
        if (count > catalog.Limits.MaximumStack - quantity) return new(inventory, false, "inventory_stack_full");
        int capacity = definition.Pocket == "POCKET_POKE_BALLS" ? catalog.QuestLimits.BallSlots : catalog.Limits.BagItemSlots;
        if (count == 0 && pocket.Length >= capacity) return new(inventory, false, "inventory_destination_full");
        return new(EarlyShopInventoryRules.SetQuantity(inventory, definition, count + quantity), true, null);
    });

    private static EarlyShopTransactionResult BuyQuoted(EarlyShopCatalog catalog, InventoryState inventory, int money,
        int quantity, EarlyShopQuote quote, Func<InventoryTransactionResult> addItems)
    {
        EarlyShopTransactionResult Fail(string error) => new(inventory, money, false, 0, 0, 0, error);
        if (!quote.Succeeded) return Fail(quote.Error!);
        if (quantity < 1 || quantity > catalog.MaximumTransactionQuantity) return Fail("shop_quantity_invalid");
        int total = checked(quote.UnitPrice * quantity); // Both factors are authenticated and bounded.
        if (money < total) return Fail("shop_insufficient_money");
        var added = addItems();
        if (!added.Succeeded) return Fail(added.Error!);
        return new(added.State, money - total, true, quantity, total, -total, null);
    }

    public static EarlyShopTransactionResult Sell(EarlyShopCatalog catalog, InventoryState inventory, int money, ItemId item, int quantity)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var quote = QuoteSell(catalog, inventory, money, item);
        return SellQuoted(catalog, inventory, money, quantity, quote, () =>
        {
            var definition = EarlyShopInventoryRules.Find(catalog, item)!;
            int count = OpeningInventoryRules.Quantity(EarlyShopInventoryRules.Pocket(inventory, definition), item);
            return EarlyShopInventoryRules.SetQuantity(inventory, definition, count - quantity);
        });
    }

    /// <summary>All priced, authenticated TMs can be sold from any shop; HMs have no sale price.</summary>
    public static EarlyShopTransactionResult Sell(EarlyShopCatalog catalog, MachineCatalog machines,
        InventoryState inventory, int money, ItemId item, int quantity)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var quote = QuoteSell(catalog, machines, inventory, money, item);
        return SellQuoted(catalog, inventory, money, quantity, quote, () => inventory with {
            Machines = OpeningInventoryRules.SetQuantity(inventory.Machines ?? [], item,
                OpeningInventoryRules.Quantity(inventory.Machines ?? [], item) - quantity)
        });
    }

    private static EarlyShopTransactionResult SellQuoted(EarlyShopCatalog catalog, InventoryState inventory, int money,
        int quantity, EarlyShopQuote quote, Func<InventoryState> removeItems)
    {
        EarlyShopTransactionResult Fail(string error) => new(inventory, money, false, 0, 0, 0, error);
        if (!quote.Succeeded) return Fail(quote.Error!);
        if (quantity < 1 || quantity > catalog.MaximumTransactionQuantity) return Fail("shop_quantity_invalid");
        if (quantity > quote.MaximumQuantity) return Fail("inventory_insufficient_quantity");
        int total = checked(quote.UnitPrice * quantity);
        // Source AddMoney caps the credit, even when the player is already at the cap. The sale still consumes items.
        int afterMoney = (int)Math.Min(catalog.MaximumMoney, (long)money + total);
        return new(removeItems(), afterMoney, true, quantity, total, afterMoney - money, null);
    }
}

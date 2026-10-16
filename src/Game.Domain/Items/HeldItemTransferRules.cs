using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

public sealed record HeldItemTransferResult(InventoryState Inventory, PartyPokemon Pokemon, bool Succeeded, string? Error);

/// <summary>Source ordinary-item and authenticated TM Give/Take: bag removal precedes return-space checking on exchange.
/// Immutable results make the source rollback atomic; no creature regeneration or RNG.</summary>
public static class HeldItemTransferRules
{
    public static bool CanHold(EarlyShopCatalog catalog, ItemId item, MachineCatalog? machines = null)
    {
        EarlyShopInventoryRules.ValidateCatalog(catalog);
        if (machines is not null)
        {
            MachineRules.ValidateCatalog(machines);
            if (MachineRules.Definition(machines, item) is {} machine)
                // tm_case.c IS_HM uses source importance, not the move or item name.
                return machine.Machine.Item is { Importance: 0, Pocket: "POCKET_TM_CASE" };
        }
        return EarlyShopInventoryRules.Find(catalog, item) is { Importance: 0 } definition &&
            definition.Pocket is "POCKET_ITEMS" or "POCKET_BERRY_POUCH" or "POCKET_POKE_BALLS" &&
            definition.SourceItemSymbol != "ITEM_ENIGMA_BERRY" && !PokemonStorageRules.IsMailItem(definition.SourceItemSymbol);
    }

    public static HeldItemTransferResult Give(EarlyShopCatalog catalog, InventoryState inventory,
        PartyPokemon pokemon, ItemId item, bool fromPc, bool exchangeConfirmed, MachineCatalog? machines = null)
    {
        HeldItemTransferResult No(string error) => new(inventory, pokemon, false, error);
        if (ValidateInventory(catalog, inventory, machines) is {} invalid) return No(invalid);
        if (pokemon.IsEgg || pokemon.Mail != 255 || PokemonStorageRules.IsMailItem(pokemon.HeldItem)) return No("held_item_mail_or_egg_unsupported");
        if (!CanHold(catalog, item, machines)) return No("held_item_not_holdable");
        var machine = machines is null ? null : MachineRules.Definition(machines, item);
        // The existing PC service admits ordinary stacks only. TM CASE is a Bag pocket.
        if (machine is not null && fromPc) return No("held_item_not_holdable");
        var definition = machine is null ? EarlyShopInventoryRules.Find(catalog, item) : null;
        int count = OpeningInventoryRules.Quantity(fromPc ? inventory.PcItems : machine is not null
            ? inventory.Machines ?? [] : EarlyShopInventoryRules.Pocket(inventory, definition!), item);
        if (count == 0) return No("inventory_insufficient_quantity");
        if (pokemon.HeldItem != "ITEM_NONE" && !exchangeConfirmed) return No("held_item_exchange_confirmation_required");
        var removed = fromPc ? inventory with { PcItems = OpeningInventoryRules.SetQuantity(inventory.PcItems, item, count - 1) }
            : machine is not null ? inventory with { Machines = OpeningInventoryRules.SetQuantity(inventory.Machines ?? [], item, count - 1) }
            : EarlyShopInventoryRules.SetQuantity(inventory, definition!, count - 1);
        if (pokemon.HeldItem != "ITEM_NONE")
        {
            var returned = ReturnToBag(catalog, removed, pokemon, machines);
            if (!returned.Succeeded) return No(returned.Error!);
            removed = returned.Inventory;
        }
        return new(removed, pokemon with { HeldItem = machine?.Machine.Item.SourceItemSymbol ?? definition!.SourceItemSymbol }, true, null);
    }

    public static HeldItemTransferResult Take(EarlyShopCatalog catalog, InventoryState inventory, PartyPokemon pokemon, MachineCatalog? machines = null)
    {
        if (ValidateInventory(catalog, inventory, machines) is {} error) return new(inventory, pokemon, false, error);
        return ReturnToBag(catalog, inventory, pokemon, machines);
    }

    internal static string? ValidateInventory(EarlyShopCatalog catalog, InventoryState inventory, MachineCatalog? machines)
    {
        if (machines is null) return EarlyShopInventoryRules.Validate(catalog, inventory);
        return MachineRules.Validate(machines, inventory) ??
            EarlyShopInventoryRules.Validate(catalog, inventory);
    }

    private static HeldItemTransferResult ReturnToBag(EarlyShopCatalog catalog, InventoryState inventory, PartyPokemon pokemon, MachineCatalog? machines)
    {
        HeldItemTransferResult No(string error) => new(inventory, pokemon, false, error);
        if (pokemon.IsEgg || pokemon.Mail != 255 || PokemonStorageRules.IsMailItem(pokemon.HeldItem)) return No("held_item_mail_or_egg_unsupported");
        if (pokemon.HeldItem == "ITEM_NONE") return No("held_item_none");
        var added = ReturnItemToBag(catalog, inventory, new ItemId("pret:" + pokemon.HeldItem), machines);
        return added.Succeeded ? new(added.State, pokemon with { HeldItem = "ITEM_NONE" }, true, null) : No(added.Error!);
    }

    /// <summary>Returns one already-owned held item to its source Bag pocket, including the TM CASE.
    /// Shared by party TAKE and the transient PC item cursor; it never creates a Pokemon or item stock.</summary>
    public static InventoryTransactionResult ReturnItemToBag(EarlyShopCatalog catalog, InventoryState inventory,
        ItemId item, MachineCatalog? machines = null)
    {
        InventoryTransactionResult No(string error) => new(inventory, false, error);
        if (ValidateInventory(catalog, inventory, machines) is {} invalid) return No(invalid);
        if (!CanHold(catalog, item, machines)) return No("held_item_not_holdable");
        if (machines is not null && MachineRules.Definition(machines, item) is not null)
            return MachineRules.AddMachine(machines, inventory, item);
        var definition = EarlyShopInventoryRules.Find(catalog, item)!;
        var pocket = EarlyShopInventoryRules.Pocket(inventory, definition);
        int count = OpeningInventoryRules.Quantity(pocket, item);
        if (count >= catalog.Limits.MaximumStack) return No("inventory_stack_full");
        int capacity = definition.Pocket switch { "POCKET_BERRY_POUCH" => SsAnneItemRules.BerrySlots,
            "POCKET_POKE_BALLS" => catalog.QuestLimits.BallSlots, _ => catalog.Limits.BagItemSlots };
        if (count == 0 && pocket.Length >= capacity) return No("inventory_destination_full");
        return new(EarlyShopInventoryRules.SetQuantity(inventory, definition, count + 1), true, null);
    }
}

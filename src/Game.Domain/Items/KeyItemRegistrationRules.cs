namespace Game.Domain.Items;

public sealed record KeyItemRegistrationResult(bool Succeeded, string? RegisteredItem, string? Error = null);

/// <summary>Source key-item registration. The caller owns field/menu admission;
/// this transaction owns source registrability, possession and the toggle.</summary>
public static class KeyItemRegistrationRules
{
    public static bool CanRegister(QuestItemDefinition? item) =>
        item is { Registrability: 1, Pocket: "POCKET_KEY_ITEMS" };

    public static KeyItemRegistrationResult Toggle(InventoryState inventory,
        QuestItemDefinition? item, string? registeredItem)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        if (!CanRegister(item) || OpeningInventoryRules.Quantity(inventory.KeyItems ?? [], item!.Id) != 1)
            return new(false, registeredItem, "key_item_registration_context_invalid");
        return new(true, registeredItem == item.Id.Value ? null : item.Id.Value);
    }
}

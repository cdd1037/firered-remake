using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Source-authenticated field capability for the same six-stone evolution service.
/// Historical pickup-only UseSupport declarations remain unchanged; this adapter performs no evolution.</summary>
public static class SourceFieldEvolutionRules
{

    public static bool Supports(EarlyShopCatalog catalog, ItemId item)
    {
        if (catalog is null || catalog.Items.IsDefaultOrEmpty || catalog.Shops.IsDefault ||
            catalog.Items.Any(i => i is null || i.EffectBytes.IsDefault)) return false;
        try { EarlyShopInventoryRules.ValidateCatalog(catalog); }
        catch (Exception error) when (error is ArgumentException or NotSupportedException) { return false; }
        var definition = EarlyShopInventoryRules.Find(catalog, item);
        return definition is { Pocket: "POCKET_ITEMS", Type: "ITEM_TYPE_PARTY_MENU", Importance: 0,
                FieldUseFunction: "FieldUseFunc_EvoItem", BattleUsage: 0, BattleUseFunction: "NULL" } &&
            ItemEvolutionRules.SourceItemId(definition.SourceItemSymbol) is { } number &&
            item.Value == "pret:" + definition.SourceItemSymbol && definition.SourceItemId == number &&
            (definition.EffectBytes.IsEmpty || definition.EffectBytes.SequenceEqual(new[] { 0, 0, 0, 0, 128, 0 }));
    }
}

using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>Source-authenticated field capability for the same six-stone evolution service.
/// Historical pickup-only UseSupport declarations remain unchanged; this adapter performs no evolution.</summary>
public static class SourceFieldEvolutionRules
{
    public const string ItemSourceSha256 = "d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489";

    public static bool Supports(EarlyShopCatalog catalog, ItemId item)
    {
        if (catalog is null || catalog.Items.IsDefaultOrEmpty || catalog.Shops.IsDefault ||
            catalog.Items.Any(i => i is null || i.EffectBytes.IsDefault)) return false;
        try { EarlyShopInventoryRules.ValidateCatalog(catalog); }
        catch (Exception error) when (error is ArgumentException or NotSupportedException) { return false; }
        var definition = EarlyShopInventoryRules.Find(catalog, item);
        return catalog.SourceCommit == PokemonFactory.SourceCommit &&
            definition is { Pocket: "POCKET_ITEMS", Type: "ITEM_TYPE_PARTY_MENU", Importance: 0,
                FieldUseFunction: "FieldUseFunc_EvoItem", BattleUsage: 0, BattleUseFunction: "NULL" } &&
            definition.Source is { Path: "src/data/items.json", Sha256: ItemSourceSha256 } &&
            ItemEvolutionRules.SourceItemId(definition.SourceItemSymbol) is { } number &&
            item.Value == "pret:" + definition.SourceItemSymbol && definition.SourceItemId == number &&
            definition.Source.Symbol == definition.SourceItemSymbol &&
            (definition.EffectBytes.IsEmpty ? definition.EffectSource is null :
                definition.EffectBytes.SequenceEqual(new[] { 0, 0, 0, 0, 128, 0 }) &&
                definition.EffectSource is { Path: "src/data/pokemon/item_effects.h",
                    Sha256: "d249420ab4751c60b1ae246895dcee5bdfb5bae18f52a9c37975c6f7fb5d0d25" });
    }
}

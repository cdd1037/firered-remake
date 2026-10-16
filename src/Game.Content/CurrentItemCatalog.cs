using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Items;

namespace Game.Content;

/// <summary>The current game's ordered item use and Mart availability, independent of regional construction history.</summary>
public static class CurrentItemCatalog
{
    public static EarlyShopCatalog Load(string root) => Load(root, SharedItemCatalog.LoadProject(root));

    public static EarlyShopCatalog Load(string root, SharedItemCatalog items) => Parse(
        File.ReadAllText(Path.Combine(root, "content/generated/data/available-items.json")), items);

    public static EarlyShopCatalog Parse(string json, SharedItemCatalog items)
    {
        var options = items.CreateJsonOptions();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        var catalog = ContentJson.ParseStrict<EarlyShopCatalog>(json, options);
        if (catalog.Limits != OpeningInventoryRules.SourceLimits ||
            catalog.QuestLimits != QuestItemRules.SourceLimits || catalog.MaximumTransactionQuantity != 99 ||
            catalog.MaximumMoney != 999999 || catalog.Items.IsDefaultOrEmpty || catalog.Shops.IsDefaultOrEmpty)
            throw new InvalidDataException("current_item_catalog_shape");
        if (catalog.Items.Any(item => item is null || item.Item is null || string.IsNullOrWhiteSpace(item.Description) || !Enum.IsDefined(item.UseSupport) ||
                item.EffectBytes.IsDefault || item.EffectBytes.Any(value => value is < 0 or > 255) ||
                item.HealHp < 0 || item.UseSupport != EarlyShopUseSupport.Potion && item.HealHp != 0 ||
                item.UseSupport != EarlyShopUseSupport.StatusCure && item.CureStatus != 0) ||
            catalog.Items.Select(item => item.Id).Distinct().Count() != catalog.Items.Length ||
            catalog.Shops.Any(shop => shop is null))
            throw new InvalidDataException("current_item_catalog_admission");
        var admitted = catalog.Items.ToDictionary(item => item.Id);
        if (catalog.Shops.Select(shop => shop.Id).Distinct(StringComparer.Ordinal).Count() != catalog.Shops.Length ||
            catalog.Shops.Any(shop => string.IsNullOrWhiteSpace(shop.Id) || shop.Items.IsDefaultOrEmpty ||
                shop.Items.Distinct().Count() != shop.Items.Length ||
                shop.Items.Any(id => !admitted.TryGetValue(id, out var item) || item.Price <= 0)))
            throw new InvalidDataException("current_item_catalog_admission");
        foreach (var item in catalog.Items)
        {
            if (item.UseSupport == EarlyShopUseSupport.Potion &&
                (item.FieldUseFunction != "FieldUseFunc_Medicine" || item.EffectBytes.Length < 7 ||
                 item.EffectBytes[4] != 4 || item.HealHp == 0 || item.HealHp != item.EffectBytes[6]))
                throw new InvalidDataException("current_item_heal_effect");
            if (item.UseSupport == EarlyShopUseSupport.StatusCure &&
                (item.FieldUseFunction != "FieldUseFunc_Medicine" || item.EffectBytes.Length < 6 ||
                 item.CureStatus != (item.EffectBytes[3] switch { 16 => 8u, 8 => 16u, 4 => 32u, 2 => 64u, _ => 0u }) ||
                 item.CureStatus == 0))
                throw new InvalidDataException("current_item_status_effect");
        }
        return catalog;
    }
}

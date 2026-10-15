using System.Collections.Immutable;
using Game.Domain.World;
namespace Game.Domain.Items;
public sealed record FirstSeviiSourceItem(QuestItemDefinition Item, string Description);
public sealed record FirstSeviiRenewableGroup(string MapSymbol, ImmutableArray<string?> Rare,
    ImmutableArray<string?> Uncommon, ImmutableArray<string?> Common, OpeningItemSourceRef Source);
/// <summary>Exact source declarations. Renewable hidden-item reset is an explicit field-service integration requirement.</summary>
public sealed record FirstSeviiItemCatalog(ImmutableArray<FirstSeviiSourceItem> Items,
    ImmutableArray<EarlyShopItemDefinition> Additions, ImmutableArray<EarlyShopStock> Shops,
    ImmutableArray<FieldPickupDefinition> Pickups, ImmutableArray<FirstSeviiRenewableGroup> RenewableGroups,
    int RenewableStepThreshold)
{
    public const string Version = "firered-first-sevii-items-v1";
    public void Validate()
    {
        if (Items.IsDefault || Additions.IsDefault || Shops.IsDefault || Pickups.IsDefault ||
            RenewableGroups.IsDefault || RenewableStepThreshold != 1500)
            throw new NotSupportedException("first_sevii_item_source");
        var itemIds = Items.Select(entry => entry.Item.Id).ToHashSet();
        if (itemIds.Count != Items.Length || Additions.Select(entry => entry.Id).Distinct().Count() != Additions.Length ||
            Shops.Select(shop => shop.Id).Distinct(StringComparer.Ordinal).Count() != Shops.Length ||
            Pickups.Select(pickup => pickup.Id).Distinct(StringComparer.Ordinal).Count() != Pickups.Length ||
            RenewableGroups.Select(group => group.MapSymbol).Distinct(StringComparer.Ordinal).Count() != RenewableGroups.Length ||
            Shops.Any(shop => string.IsNullOrWhiteSpace(shop.Id) || shop.Items.IsDefaultOrEmpty ||
                shop.Items.Distinct().Count() != shop.Items.Length || shop.Items.Any(item => !itemIds.Contains(item))) ||
            Additions.Any(entry => !itemIds.Contains(entry.Id)) ||
            Pickups.Any(pickup => pickup.Quantity <= 0 || !itemIds.Contains(pickup.Item)))
            throw new NotSupportedException("first_sevii_item_membership");
    }
}

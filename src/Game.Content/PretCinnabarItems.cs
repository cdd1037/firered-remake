using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretCinnabarItemsDocument(int SchemaVersion, ImmutableArray<PretBillSourceItem> Items, EarlyShopStock Mart);

public static class PretCinnabarItems
{
    public const string Profile = "firered-cinnabar-items-v1";
    public static ItemId SecretKeyId => ItemIds.SecretKeyId;
    public static ItemId Tm14Id => new("pret:ITEM_TM14");
    public static ItemId Tm22Id => new("pret:ITEM_TM22");
    public static ItemId Tm38Id => new("pret:ITEM_TM38");

    public static PretCinnabarItemsDocument LoadProject(string root)
    {
        var source = new SilphSourcePackage(root,"data/items/cinnabar");
        var items = source.Parse<PretCinnabarItemsDocument>("items.json");
        SilphSourcePackage.Need(items.SchemaVersion == 1 && !items.Items.IsDefault &&
            items.Items.Select(i => i.Metadata.Item.Id).SequenceEqual([SecretKeyId, Tm14Id, Tm22Id, Tm38Id]),
            "Cinnabar item declaration scope differs.");
        foreach (var item in items.Items)
        {
            SilphSourcePackage.Need(item.EffectBytes.IsEmpty, "Cinnabar item effect scope differs.");
        }
        return items;
    }

    public static EarlyShopItemDefinition Definition(PretCinnabarItemsDocument document, ItemId id) =>
        PretSsAnneItems.Definition(document.Items.Single(i => i.Metadata.Item.Id == id));

    public static EarlyShopStock MartStock(PretCinnabarItemsDocument document) => document.Mart;

}

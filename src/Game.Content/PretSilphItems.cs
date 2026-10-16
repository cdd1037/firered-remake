using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Content;
public sealed record PretSilphItemsDocument(int SchemaVersion, ImmutableArray<PretBillSourceItem> Items);

public static class PretSilphItems
{
    public const string Profile = "firered-silph-items-v1";
    public static readonly ItemId CardKeyId = new("pret:ITEM_CARD_KEY");
    public static readonly ItemId MasterBallId = new("pret:ITEM_MASTER_BALL");
    public static readonly ItemId UltraBallId = new("pret:ITEM_ULTRA_BALL");
    public static PretSilphItemsDocument LoadProject(string root)
    {
        var source = SilphSourcePackage.Catalogs(root); var d = source.Parse<PretSilphItemsDocument>("items.json");
        string[] names = ["CARD_KEY","MASTER_BALL","ULTRA_BALL","TM01","TM08","TM41","HYPER_POTION","MAX_REVIVE","ESCAPE_ROPE","FULL_HEAL","PROTEIN","HP_UP","X_SPECIAL","CALCIUM","CARBOS","RARE_CANDY","IRON","ZINC","ELIXIR","PP_UP","NUGGET","MAX_POTION","REVIVE"];
        SilphSourcePackage.Need(d.SchemaVersion == 1 &&
            d.Items.Select(i => i.Metadata.Item.Id.Value).SequenceEqual(names.Select(n => "pret:ITEM_" + n)),
            "Silph exact23 item scope differs.");
        foreach (var item in d.Items)
        {
            var i = item.Metadata.Item;             SilphSourcePackage.Need(item.EffectBytes.IsEmpty && i.CanGive == (i.Importance == 0) && i.CanToss == (i.Importance == 0) && i.CanSell == (i.Price != 0),
                "Silph key/item declaration differs.");
        }
        return d;
    }
    public static EarlyShopItemDefinition Definition(PretBillSourceItem item)
    {
        var definition = PretSsAnneItems.Definition(item);
        return item.Metadata.Item.Id == MasterBallId || item.Metadata.Item.Id == UltraBallId ? definition with { UseSupport = EarlyShopUseSupport.SeparateCapture } : definition;
    }
    public static EarlyShopItemDefinition Definition(PretSilphItemsDocument d, ItemId id) => Definition(d.Items.Single(i => i.Metadata.Item.Id == id));

}

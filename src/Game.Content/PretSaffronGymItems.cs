using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretSaffronGymItemsDocument(int SchemaVersion, ImmutableArray<PretBillSourceItem> Items, ImmutableArray<PretPewterMachineIcon> Icons);

public static class PretSaffronGymItems
{
    public const string Profile = "firered-saffron-gym-items-v1";
    public static readonly ItemId Tm04Id = new("pret:ITEM_TM04");
    public static readonly ItemId MaxRepelId = new("pret:ITEM_MAX_REPEL");
    public static PretSaffronGymItemsDocument LoadProject(string root)
    {
        var source = SaffronGymSource.Catalogs(root); var d = source.Parse<PretSaffronGymItemsDocument>("items.json");
        SilphSourcePackage.Need(d.SchemaVersion == 1 &&
            d.Items.Select(i => i.Metadata.Item.Id).SequenceEqual([Tm04Id, MaxRepelId]) && d.Icons.Length == 1, "Saffron Gym item scope differs.");
        foreach (var item in d.Items)
        {
            var i = item.Metadata.Item;             SilphSourcePackage.Need(item.EffectBytes.IsEmpty && i.Importance == 0 && i.Registrability == 0 &&
                i.HoldEffect == "HOLD_EFFECT_NONE" && i.BattleUsage == 0 && i.BattleUseFunction == "NULL" && i.SecondaryId == 0 &&
                i.CanGive && i.CanToss && i.CanSell, "Saffron Gym item declaration differs.");
        }
        var tm = d.Items[0].Metadata.Item; var max = d.Items[1].Metadata.Item;
        SilphSourcePackage.Need(tm.SourceItemSymbol == "ITEM_TM04" && tm.SourceItemId == 292 && tm.Name == "TM04" && tm.Price == 3000 &&
            tm.Pocket == "POCKET_TM_CASE" && tm.Type == "ITEM_TYPE_PARTY_MENU" && tm.HoldEffectParam == 0 && tm.FieldUseFunction == "NULL" &&
            max.SourceItemSymbol == "ITEM_MAX_REPEL" && max.SourceItemId == 84 && max.Name == "MAX REPEL" && max.Price == 700 &&
            max.Pocket == "POCKET_ITEMS" && max.Type == "ITEM_TYPE_BAG_MENU" && max.HoldEffectParam == 250 && max.FieldUseFunction == "FieldUseFunc_Repel" &&
            d.Items[1].Metadata.Description == "Prevents weak wild POKéMON from\nappearing for 250 steps.", "Saffron Gym TM04/Max Repel source values differ.");
        var icon = d.Icons[0];
        SilphSourcePackage.Need(icon.Item == Tm04Id && icon.Width == 24 && icon.Height == 24 && icon.Region is not null , "TM04 source icon differs.");
PretPewterMachines.ValidateIcon(icon);
        return d;
    }
    public static EarlyShopItemDefinition Definition(PretSaffronGymItemsDocument document, ItemId id)
    {
        var definition = PretSsAnneItems.Definition(document.Items.Single(i => i.Metadata.Item.Id == id));
        return id == MaxRepelId ? definition with { UseSupport = EarlyShopUseSupport.DeferredRepel } : definition;
    }
    public static EarlyShopStock MartStock(PretSaffronGymRegion region)
    {
        return new("SaffronCity_Mart_Items",
            region.Contracts.GetProperty("martStock").EnumerateArray().Select(item => new ItemId("pret:" + item.GetString()!)).ToImmutableArray());
    }
    /// <summary>Exactly one ordinary item, retaining all predecessor objects by reference. Caller authenticates successor shop proof.</summary>

}

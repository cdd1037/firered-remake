using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Content;
public sealed record PretSilphItemsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretBillSourceItem> Items, ImmutableArray<string> Limitations)
{
    [JsonIgnore] public SilphMachinesDocument Machines { get; internal init; } = null!;
}
public sealed record SilphMachinesDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ItemId> InheritedItemIds, ImmutableArray<SafariMachineAddition> Additions,
    ImmutableArray<MachineDefinition> NewMachines, ImmutableArray<string> Limitations);
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
            d.Items.Select(i => i.Metadata.Item.Id.Value).SequenceEqual(names.Select(n => "pret:ITEM_" + n)) && !d.Limitations.IsDefaultOrEmpty,
            "Silph exact23 item scope differs.");
        foreach (var item in d.Items)
        {
            var i = item.Metadata.Item;             SilphSourcePackage.Need(item.EffectBytes.IsEmpty && i.CanGive == (i.Importance == 0) && i.CanToss == (i.Importance == 0) && i.CanSell == (i.Price != 0),
                "Silph key/item declaration differs.");
        }
        var machines = source.Parse<SilphMachinesDocument>("machines.json");
        SilphSourcePackage.Need(machines.SchemaVersion == 1 &&
            machines.InheritedItemIds.Length == 24 && machines.Additions.Length == 24 && machines.NewMachines.Select(m => m.Machine.Item.Id.Value).SequenceEqual(["pret:ITEM_TM01","pret:ITEM_TM08","pret:ITEM_TM41"]),
            "Silph exact27 machine scope differs.");
        foreach (var m in machines.Additions) { SilphSourcePackage.Need(m.Compatibility.Length == 1 && m.Compatibility[0].Species.Value == "pret:SPECIES_LAPRAS", "Silph inherited row delta differs."); }
        foreach (var m in machines.NewMachines)
        {
            SilphSourcePackage.Need(m.Compatibility.Length == 113 && m.Compatibility.Select(c => c.Species).Distinct().Count() == 113, "Silph machine owned set differs.");
        }
        return d with { Machines = machines};
    }
    public static EarlyShopItemDefinition Definition(PretBillSourceItem item)
    {
        var definition = PretSsAnneItems.Definition(item);
        return item.Metadata.Item.Id == MasterBallId || item.Metadata.Item.Id == UltraBallId ? definition with { UseSupport = EarlyShopUseSupport.SeparateCapture } : definition;
    }
    public static EarlyShopItemDefinition Definition(PretSilphItemsDocument d, ItemId id) => Definition(d.Items.Single(i => i.Metadata.Item.Id == id));

}
public static class PretSilphMachines
{
    public const string Profile = "firered-silph-teaching-v1";
    public static SilphMachinesDocument LoadProject(string root) => PretSilphItems.LoadProject(root).Machines;
}

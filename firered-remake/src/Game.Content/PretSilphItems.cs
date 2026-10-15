using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Content;
public sealed record PretSilphItemsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretBillSourceItem> Items, ImmutableArray<OpeningItemSourceRef> RulesSources, ImmutableArray<string> Limitations)
{
    [JsonIgnore] public SilphMachinesDocument Machines { get; internal init; } = null!;
}
public sealed record SilphMachinesDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ItemId> InheritedItemIds, ImmutableArray<SafariMachineAddition> Additions,
    ImmutableArray<PewterAdditionalMachine> NewMachines, ImmutableArray<string> Limitations);
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
            d.Items.Select(i => i.Metadata.Item.Id.Value).SequenceEqual(names.Select(n => "pret:ITEM_" + n)) && !d.RulesSources.IsDefaultOrEmpty && !d.Limitations.IsDefaultOrEmpty,
            "Silph exact23 item scope differs.");
        foreach (var item in d.Items)
        {
            var i = item.Metadata.Item;             SilphSourcePackage.Need(item.EffectBytes.IsEmpty && item.EffectSource is null && i.CanGive == (i.Importance == 0) && i.CanToss == (i.Importance == 0) && i.CanSell == (i.Price != 0),
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
    public static EarlyShopCatalog Extend(EarlyShopCatalog previous, PretSilphItemsDocument items)
    {
        var ids = previous.Items.Select(i => i.Id).ToHashSet();
        return previous with { Profile = "firered-silph-inventory-v1",Items = previous.Items.AddRange(items.Items.Where(i => !ids.Contains(i.Metadata.Item.Id)).Select(Definition)) };
    }
}
public static class PretSilphMachines
{
    public const string Profile = "firered-silph-teaching-v1";
    public static SilphMachinesDocument LoadProject(string root) => PretSilphItems.LoadProject(root).Machines;
    public static PewterMachineCatalog Extend(SurfMachineCatalog previous, PretSilphPokemon pokemon, PretSilphItemsDocument items) => Extend(previous.Catalog, pokemon, items);
    public static PewterMachineCatalog Extend(PewterMachineCatalog previous, PretSilphPokemon pokemon, PretSilphItemsDocument items)
    {
        var source = items.Machines;
        SilphSourcePackage.Need(pokemon.Catalog.Species.Count == 113 && previous.Compatibility.Length == 112 && previous.AdditionalMachines?.Length == 23 &&
            new[] { previous.Tm39.Item.Id }.Concat(previous.AdditionalMachines!.Value.Select(m => m.Machine.Item.Id)).SequenceEqual(source.InheritedItemIds), "Silph machine predecessor scope differs.");
        ImmutableArray<PewterMachineCompatibility> Rows(ItemId id, ImmutableArray<PewterMachineCompatibility> old)
        {
            var added = source.Additions.Single(a => a.Item == id).Compatibility; var row = pokemon.Document.Machines.Single(m => m.Species.Value == "pret:SPECIES_LAPRAS");
            SilphSourcePackage.Need(added[0].CanLearn == row.MachineSymbols.Any(s => s.StartsWith(id.Value[10..] + "_", StringComparison.Ordinal)), "Silph Lapras machine compatibility differs.");
            return old.AddRange(added);
        }
        var newMachines = source.NewMachines.Select(m =>
        {
            SilphSourcePackage.Need(m.Compatibility.Select(c => c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys), "Silph machine source113 ownership differs.");
            // New matrices retain their authenticated source order; only inherited rows append Lapras.
            return m;
        });
        return previous with { Profile = Profile,Compatibility = Rows(previous.Tm39.Item.Id, previous.Compatibility),AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with { Compatibility = Rows(m.Machine.Item.Id, m.Compatibility) }).Concat(newMachines).ToImmutableArray() };
    }
}

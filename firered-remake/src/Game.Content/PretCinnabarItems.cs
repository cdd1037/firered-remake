using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretCinnabarItemsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretBillSourceItem> Items, EarlyShopStock Mart,
    ImmutableArray<string> Limitations)
{
    [JsonIgnore] public CinnabarMachinesDocument Machines { get; internal init; } = null!;
}

public sealed record CinnabarMachinesDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ItemId> InheritedItemIds, ImmutableArray<SafariMachineAddition> Additions,
    ImmutableArray<MachineDefinition> NewMachines, ImmutableArray<string> Limitations);

/// <summary>Independent pinned declarations. All ordinary pickups except Secret Key already exist in Saffron.</summary>
public static class PretCinnabarItems
{
    public const string Profile = "firered-cinnabar-items-v1";
    public static ItemId SecretKeyId => ItemIds.SecretKeyId;
    public static ItemId Tm14Id => new("pret:ITEM_TM14");
    public static ItemId Tm22Id => new("pret:ITEM_TM22");
    public static ItemId Tm38Id => new("pret:ITEM_TM38");

    public static PretCinnabarItemsDocument LoadProject(string root)
    {
        var source = new SilphSourcePackage(root,"cinnabar-items");
        var items = source.Parse<PretCinnabarItemsDocument>("items.json");
        SilphSourcePackage.Need(items.SchemaVersion == 1 && !items.Items.IsDefault &&
            items.Items.Select(i => i.Metadata.Item.Id).SequenceEqual([SecretKeyId, Tm14Id, Tm22Id, Tm38Id]) &&
            !items.Limitations.IsDefaultOrEmpty,
            "Cinnabar item declaration scope differs.");
        foreach (var item in items.Items)
        {
            SilphSourcePackage.Need(item.EffectBytes.IsEmpty, "Cinnabar item effect scope differs.");
        }
        var machines = source.Parse<CinnabarMachinesDocument>("machines.json");
        SilphSourcePackage.Need(machines.SchemaVersion == 1 && machines.InheritedItemIds.Length == 28 &&
            machines.InheritedItemIds.Distinct().Count() == 28 && machines.Additions.Length == 28 &&
            machines.Additions.Select(a => a.Item).SequenceEqual(machines.InheritedItemIds) &&
            machines.NewMachines.Select(m => m.Machine.Item.Id).SequenceEqual([Tm14Id, Tm22Id, Tm38Id]) &&
            !machines.Limitations.IsDefaultOrEmpty, "Cinnabar teaching scope differs.");
        foreach (var addition in machines.Additions)
        {
            SilphSourcePackage.Need(addition.Compatibility.Select(c => c.Species.Value).SequenceEqual(
                ["pret:SPECIES_KRABBY", "pret:SPECIES_KINGLER"]), "Cinnabar inherited machine delta differs.");
        }
        foreach (var machine in machines.NewMachines)
        {
            SilphSourcePackage.Need(machine.Machine == items.Items.Single(i => i.Metadata.Item.Id == machine.Machine.Item.Id).Metadata &&
                machine.Compatibility.Length == 115 && machine.Compatibility.Select(c => c.Species).Distinct().Count() == 115 &&
                machine.Compatibility.Select(c => c.SourceSpeciesId).SequenceEqual(machine.Compatibility.Select(c => c.SourceSpeciesId).Order()) &&
                !machine.Compatibility.Any(c => c.SourceSpeciesId is 65 or 80 or 122), "Cinnabar source machine ownership differs.");
        }
        return items with { Machines = machines};
    }

    public static EarlyShopItemDefinition Definition(PretCinnabarItemsDocument document, ItemId id) =>
        PretSsAnneItems.Definition(document.Items.Single(i => i.Metadata.Item.Id == id));

    public static EarlyShopStock MartStock(PretCinnabarItemsDocument document) => document.Mart;

}

public static class PretCinnabarMachines
{
    public const string Profile = "firered-cinnabar-teaching-v1";
    public static CinnabarMachinesDocument LoadProject(string root) => PretCinnabarItems.LoadProject(root).Machines;
}

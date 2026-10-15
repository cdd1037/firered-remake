using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretCinnabarItemsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretBillSourceItem> Items, EarlyShopStock Mart,
    ImmutableArray<OpeningItemSourceRef> RulesSources, ImmutableArray<string> Limitations)
{
    [JsonIgnore] public CinnabarMachinesDocument Machines { get; internal init; } = null!;
}

public sealed record CinnabarMachinesDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ItemId> InheritedItemIds, ImmutableArray<SafariMachineAddition> Additions,
    ImmutableArray<PewterAdditionalMachine> NewMachines, ImmutableArray<string> Limitations);

/// <summary>Independent pinned declarations. All ordinary pickups except Secret Key already exist in Saffron.</summary>
public static class PretCinnabarItems
{
    public const string Profile = "firered-cinnabar-items-v1";
    public static ItemId SecretKeyId => CinnabarShopCatalog.SecretKeyId;
    public static ItemId Tm14Id => CinnabarMachineCatalog.Tm14Id;
    public static ItemId Tm22Id => CinnabarMachineCatalog.Tm22Id;
    public static ItemId Tm38Id => CinnabarMachineCatalog.Tm38Id;

    public static PretCinnabarItemsDocument LoadProject(string root)
    {
        var source = new SilphSourcePackage(root,"cinnabar-items",            "scripts/cinnabar-items.lock.json");
        var items = source.Parse<PretCinnabarItemsDocument>("items.json");
        SilphSourcePackage.Need(items.SchemaVersion == 1 && !items.Items.IsDefault &&
            items.Items.Select(i => i.Metadata.Item.Id).SequenceEqual([SecretKeyId, Tm14Id, Tm22Id, Tm38Id]) &&
            !items.RulesSources.IsDefaultOrEmpty && !items.Limitations.IsDefaultOrEmpty,
            "Cinnabar item declaration scope differs.");
        foreach (var item in items.Items)
        {
            SilphSourcePackage.Need(item.EffectBytes.IsEmpty && item.EffectSource is null, "Cinnabar item effect scope differs.");
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

    public static EarlyShopCatalog Extend(EarlyShopCatalog previous, PretCinnabarItemsDocument items)
    {
        SaffronGymShopCatalog.ValidateCatalog(previous);
        return previous with { Profile = CinnabarShopCatalog.Profile,Items = previous.Items.Add(Definition(items, SecretKeyId)),Shops = previous.Shops.Add(items.Mart) };
    }
}

public static class PretCinnabarMachines
{
    public const string Profile = CinnabarMachineCatalog.Profile;
    public static CinnabarMachinesDocument LoadProject(string root) => PretCinnabarItems.LoadProject(root).Machines;
    public static PewterMachineCatalog Extend(PewterMachineCatalog previous, PretCinnabarItemsDocument items)
    {
        var proof = SaffronGymMachineCatalog.For(previous) ?? throw new InvalidDataException("Cinnabar machine predecessor is unauthenticated.");
        var source = items.Machines;
        SilphSourcePackage.Need(previous.Compatibility.Length == 113 && previous.AdditionalMachines?.Length == 27 &&
            new[] { previous.Tm39.Item.Id }.Concat(previous.AdditionalMachines!.Value.Select(m => m.Machine.Item.Id)).SequenceEqual(source.InheritedItemIds),
            "Cinnabar machine predecessor scope differs.");
        var expected = proof.Pokemon.Catalog.Species.Keys.Concat(Game.Domain.Pokemon.CinnabarOwnedPokemonCatalog.AdditionalSpecies).ToHashSet();
        foreach (var machine in source.NewMachines)
            SilphSourcePackage.Need(machine.Compatibility.Select(c => c.Species).ToHashSet().SetEquals(expected), "Cinnabar source matrix ownership differs.");
        ImmutableArray<PewterMachineCompatibility> Rows(ItemId id, ImmutableArray<PewterMachineCompatibility> before) =>
            before.AddRange(source.Additions.Single(a => a.Item == id).Compatibility);
        return previous with { Profile = Profile,Compatibility = Rows(previous.Tm39.Item.Id, previous.Compatibility),AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with { Compatibility = Rows(m.Machine.Item.Id, m.Compatibility) })
                .Concat(source.NewMachines).ToImmutableArray() };
    }
}

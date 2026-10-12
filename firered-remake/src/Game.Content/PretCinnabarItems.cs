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
    [JsonIgnore] public ImmutableArray<string> AdditionalSourceHashes { get; internal init; } = [];
}

public sealed record CinnabarMachinesDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ItemId> InheritedItemIds, ImmutableArray<SafariMachineAddition> Additions,
    ImmutableArray<PewterAdditionalMachine> NewMachines, ImmutableArray<string> Limitations);

/// <summary>Independent pinned declarations. All ordinary pickups except Secret Key already exist in Saffron.</summary>
public static class PretCinnabarItems
{
    public const string Profile = "firered-cinnabar-items-v1";
    public const string GeneratedSha256 = "39d946126043241e6f7bd18288c990bd31a38de41bbaad32c7824e0aa9a63e44";
    public static ItemId SecretKeyId => CinnabarShopCatalog.SecretKeyId;
    public static ItemId Tm14Id => CinnabarMachineCatalog.Tm14Id;
    public static ItemId Tm22Id => CinnabarMachineCatalog.Tm22Id;
    public static ItemId Tm38Id => CinnabarMachineCatalog.Tm38Id;

    public static PretCinnabarItemsDocument LoadProject(string root)
    {
        var source = new SilphSourcePackage(root, "cinnabar-items", "firered-cinnabar-items-source-v1",
            "scripts/cinnabar-items.lock.json", "376c84d363b74d4255f75b7ddcba22932182e373ea8f7c21acc458263fa869d9", 2863,
            "d7758a4ab00ce39fc790aaafe5b77b9f84ec33f2c3bdf70b6ea4e0a96caf0b65", 758, 9, 2);
        var items = source.Parse<PretCinnabarItemsDocument>("items.json");
        SilphSourcePackage.Need(items.SchemaVersion == 1 && items.Profile == Profile &&
            items.SourceCommit == SilphSourcePackage.SourceCommit && !items.Items.IsDefault &&
            items.Items.Select(i => i.Metadata.Item.Id).SequenceEqual([SecretKeyId, Tm14Id, Tm22Id, Tm38Id]) &&
            !items.RulesSources.IsDefaultOrEmpty && !items.Limitations.IsDefaultOrEmpty,
            "Cinnabar item declaration scope differs.");
        foreach (var item in items.Items)
        {
            source.Reference(item.Metadata.Item.Source); source.Reference(item.Metadata.DescriptionSource);
            SilphSourcePackage.Need(item.EffectBytes.IsEmpty && item.EffectSource is null, "Cinnabar item effect scope differs.");
        }
        foreach (var reference in items.RulesSources) source.Reference(reference);
        source.Reference(items.Mart.Source);
        var machines = source.Parse<CinnabarMachinesDocument>("machines.json");
        SilphSourcePackage.Need(machines.SchemaVersion == 1 && machines.Profile == "firered-cinnabar-machines-v1" &&
            machines.SourceCommit == items.SourceCommit && machines.InheritedItemIds.Length == 28 &&
            machines.InheritedItemIds.Distinct().Count() == 28 && machines.Additions.Length == 28 &&
            machines.Additions.Select(a => a.Item).SequenceEqual(machines.InheritedItemIds) &&
            machines.NewMachines.Select(m => m.Machine.Item.Id).SequenceEqual([Tm14Id, Tm22Id, Tm38Id]) &&
            !machines.Limitations.IsDefaultOrEmpty, "Cinnabar teaching scope differs.");
        foreach (var addition in machines.Additions)
        {
            SilphSourcePackage.Need(addition.Compatibility.Select(c => c.Species.Value).SequenceEqual(
                ["pret:SPECIES_KRABBY", "pret:SPECIES_KINGLER"]), "Cinnabar inherited machine delta differs.");
            foreach (var row in addition.Compatibility) source.Reference(row.Source);
        }
        foreach (var machine in machines.NewMachines)
        {
            SilphSourcePackage.Need(machine.Machine == items.Items.Single(i => i.Metadata.Item.Id == machine.Machine.Item.Id).Metadata &&
                machine.Compatibility.Length == 115 && machine.Compatibility.Select(c => c.Species).Distinct().Count() == 115 &&
                machine.Compatibility.Select(c => c.SourceSpeciesId).SequenceEqual(machine.Compatibility.Select(c => c.SourceSpeciesId).Order()) &&
                !machine.Compatibility.Any(c => c.SourceSpeciesId is 65 or 80 or 122), "Cinnabar source machine ownership differs.");
            foreach (var row in machine.Compatibility) source.Reference(row.Source);
        }
        return items with { Machines = machines, AdditionalSourceHashes = ["cinnabar-items:" + GeneratedSha256,
            .. source.Files.Select(f => "cinnabar-item-source:" + f.Path + ":" + f.Sha256)] };
    }

    public static EarlyShopItemDefinition Definition(PretCinnabarItemsDocument document, ItemId id) =>
        PretSsAnneItems.Definition(document.Items.Single(i => i.Metadata.Item.Id == id));

    public static EarlyShopStock MartStock(PretCinnabarItemsDocument document) => document.Mart;

    public static EarlyShopCatalog Extend(EarlyShopCatalog previous, PretCinnabarItemsDocument items)
    {
        SaffronGymShopCatalog.ValidateCatalog(previous);
        return previous with { Profile = CinnabarShopCatalog.Profile,
            Items = previous.Items.Add(Definition(items, SecretKeyId)), Shops = previous.Shops.Add(items.Mart) };
    }
}

public static class PretCinnabarMachines
{
    public const string Profile = CinnabarMachineCatalog.Profile;
    public const string GeneratedSha256 = "5ddf581260c05519aeb8f8e5fc45a556f973aee1b3e769915a6c8f17a6e17d72";
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
        return previous with { Profile = Profile, Compatibility = Rows(previous.Tm39.Item.Id, previous.Compatibility),
            AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with { Compatibility = Rows(m.Machine.Item.Id, m.Compatibility) })
                .Concat(source.NewMachines).ToImmutableArray() };
    }
}

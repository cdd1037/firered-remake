using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretSaffronGymItemsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretBillSourceItem> Items, ImmutableArray<PretPewterMachineIcon> Icons,
    ImmutableArray<OpeningItemSourceRef> RulesSources, ImmutableArray<string> Limitations)
{
    [JsonIgnore] public SaffronGymMachinesDocument Machines { get; internal init; } = null!;
    [JsonIgnore] public ImmutableArray<string> AdditionalSourceHashes { get; internal init; } = [];
}
public sealed record SaffronGymMachinesDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ItemId> InheritedItemIds, ImmutableArray<SafariMachineAddition> Additions,
    ImmutableArray<PewterAdditionalMachine> NewMachines, ImmutableArray<string> Limitations)
{
    [JsonIgnore] public ImmutableArray<PewterAdditionalMachine> Machines => NewMachines;
}

/// <summary>Source TM04 and MAX_REPEL metadata. Application grants and field use remain separate.</summary>
public static class PretSaffronGymItems
{
    public const string Profile = "firered-saffron-gym-items-v1";
    public const string GeneratedSha256 = "ca7a95b983ab2d79a66192033ad2240ed9e55689295a0d473042652c5b24091b";
    public static readonly ItemId Tm04Id = new("pret:ITEM_TM04");
    public static readonly ItemId MaxRepelId = new("pret:ITEM_MAX_REPEL");
    public static PretSaffronGymItemsDocument LoadProject(string root)
    {
        var source = SaffronGymSource.Catalogs(root); var d = source.Parse<PretSaffronGymItemsDocument>("items.json");
        SilphSourcePackage.Need(d.SchemaVersion == 1 && d.Profile == Profile && d.SourceCommit == SilphSourcePackage.SourceCommit &&
            d.Items.Select(i => i.Metadata.Item.Id).SequenceEqual([Tm04Id, MaxRepelId]) && d.Icons.Length == 1 &&
            !d.RulesSources.IsDefaultOrEmpty && !d.Limitations.IsDefaultOrEmpty, "Saffron Gym item scope differs.");
        foreach (var item in d.Items)
        {
            var i = item.Metadata.Item; source.Reference(i.Source); source.Reference(item.Metadata.DescriptionSource);
            SilphSourcePackage.Need(item.EffectBytes.IsEmpty && item.EffectSource is null && i.Importance == 0 && i.Registrability == 0 &&
                i.HoldEffect == "HOLD_EFFECT_NONE" && i.BattleUsage == 0 && i.BattleUseFunction == "NULL" && i.SecondaryId == 0 &&
                i.CanGive && i.CanToss && i.CanSell, "Saffron Gym item declaration differs.");
        }
        foreach (var r in d.RulesSources) source.Reference(r);
        var tm = d.Items[0].Metadata.Item; var max = d.Items[1].Metadata.Item;
        SilphSourcePackage.Need(tm.SourceItemSymbol == "ITEM_TM04" && tm.SourceItemId == 292 && tm.Name == "TM04" && tm.Price == 3000 &&
            tm.Pocket == "POCKET_TM_CASE" && tm.Type == "ITEM_TYPE_PARTY_MENU" && tm.HoldEffectParam == 0 && tm.FieldUseFunction == "NULL" &&
            max.SourceItemSymbol == "ITEM_MAX_REPEL" && max.SourceItemId == 84 && max.Name == "MAX REPEL" && max.Price == 700 &&
            max.Pocket == "POCKET_ITEMS" && max.Type == "ITEM_TYPE_BAG_MENU" && max.HoldEffectParam == 250 && max.FieldUseFunction == "FieldUseFunc_Repel" &&
            d.Items[1].Metadata.Description == "Prevents weak wild POKéMON from\nappearing for 250 steps.", "Saffron Gym TM04/Max Repel source values differ.");
        var icon = d.Icons[0];
        SilphSourcePackage.Need(icon.Item == Tm04Id && icon.Width == 24 && icon.Height == 24 && icon.PngPath == "item_tm04.png" &&
            SafariSource.Hash(source.Outputs[icon.PngPath]) == icon.PngSha256 &&
            source.Files.Any(f => f.Path == icon.Source.Path && f.Sha256 == icon.Source.Sha256) &&
            source.Files.Any(f => f.Path == icon.PaletteSource.Path && f.Sha256 == icon.PaletteSource.Sha256), "TM04 source icon differs.");
        source.Reference(icon.BindingSource); _ = PretPewterMachines.IconRgba(icon);
        var machines = source.Parse<SaffronGymMachinesDocument>("machines.json");
        SilphSourcePackage.Need(machines.SchemaVersion == 1 && machines.Profile == "firered-saffron-gym-machines-v1" && machines.SourceCommit == d.SourceCommit &&
            machines.InheritedItemIds.Length == 27 && machines.InheritedItemIds.Distinct().Count() == 27 && machines.Additions.IsEmpty && machines.NewMachines.Length == 1 &&
            !machines.Limitations.IsDefaultOrEmpty, "Saffron Gym machine scope differs.");
        var machine = machines.NewMachines[0];
        SilphSourcePackage.Need(machine.Machine == d.Items[0].Metadata && machine.Move.Value == "pret:MOVE_CALM_MIND" && machine.SourceMoveId == 347 && machine.MovePp == 20 &&
            machine.Compatibility.Length == 113 && machine.Compatibility.Select(c => c.Species).Distinct().Count() == 113 && machine.Compatibility.Count(c => c.CanLearn) == 10 &&
            machine.Compatibility.Select(c => c.SourceSpeciesId).SequenceEqual(machine.Compatibility.Select(c => c.SourceSpeciesId).Order()) &&
            !machine.Compatibility.Any(c => c.SourceSpeciesId is 65 or 80 or 122), "TM04 source mapping/ownership differs.");
        foreach (var c in machine.Compatibility) source.Reference(c.Source);
        return d with { Machines = machines, AdditionalSourceHashes = ["saffron-gym-items:" + GeneratedSha256,
            .. source.Files.Select(f => "saffron-gym-item-source:" + f.Path + ":" + f.Sha256)] };
    }
    public static EarlyShopItemDefinition Definition(PretSaffronGymItemsDocument document, ItemId id)
    {
        var definition = PretSsAnneItems.Definition(document.Items.Single(i => i.Metadata.Item.Id == id));
        return id == MaxRepelId ? definition with { UseSupport = EarlyShopUseSupport.DeferredRepel } : definition;
    }
    public static EarlyShopStock MartStock(PretSaffronGymRegion region)
    {
        var source = region.Scripts.Single(s => s.Path == "data/maps/SaffronCity_Mart/scripts.inc");
        return new("SaffronCity_Mart_Items", [new("pret:ITEM_GREAT_BALL"), new("pret:ITEM_HYPER_POTION"), new("pret:ITEM_REVIVE"),
            new("pret:ITEM_FULL_HEAL"), new("pret:ITEM_ESCAPE_ROPE"), new("pret:ITEM_MAX_REPEL")],
            new(source.Path, "SaffronCity_Mart_Items", 24, 31, source.SourceRef.Sha256!));
    }
    /// <summary>Exactly one ordinary item, retaining all predecessor objects by reference. Caller authenticates successor shop proof.</summary>
    public static EarlyShopCatalog Extend(EarlyShopCatalog previous, PretSaffronGymItemsDocument items)
    {
        SilphShopCatalog.ValidateCatalog(previous);
        SilphSourcePackage.Need(previous.Items.All(i => i.Id != MaxRepelId), "Max Repel already exists in predecessor.");
        return previous with { Profile = "firered-saffron-gym-inventory-v1", Items = previous.Items.Add(Definition(items, MaxRepelId)) };
    }
}
public static class PretSaffronGymMachines
{
    public const string Profile = "firered-saffron-gym-teaching-v1";
    public const string GeneratedSha256 = "daf2f0d6c2d8fcf13a807e536a91076fe907f58a34bcfe1a67e846f92939f286";
    public static SaffronGymMachinesDocument LoadProject(string root) => PretSaffronGymItems.LoadProject(root).Machines;
    public static PewterMachineCatalog Extend(PewterMachineCatalog previous, PretSaffronGymItemsDocument items)
    {
        var proof = SilphMachineCatalog.For(previous) ?? throw new InvalidDataException("Saffron Gym machine predecessor is unauthenticated.");
        var source = items.Machines;
        SilphSourcePackage.Need(previous.Compatibility.Length == 113 && previous.AdditionalMachines?.Length == 26 &&
            new[] { previous.Tm39.Item.Id }.Concat(previous.AdditionalMachines!.Value.Select(m => m.Machine.Item.Id)).SequenceEqual(source.InheritedItemIds) &&
            source.NewMachines[0].Compatibility.Select(c => c.Species).ToHashSet().SetEquals(proof.Pokemon.Catalog.Species.Keys), "Saffron Gym machine predecessor differs.");
        return previous with { Profile = Profile, AdditionalMachines = previous.AdditionalMachines!.Value.Add(source.NewMachines.Single()) };
    }
}

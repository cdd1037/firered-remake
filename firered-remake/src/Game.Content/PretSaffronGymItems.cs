using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretSaffronGymItemsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretBillSourceItem> Items, ImmutableArray<PretPewterMachineIcon> Icons,
    ImmutableArray<string> Limitations)
{
    [JsonIgnore] public SaffronGymMachinesDocument Machines { get; internal init; } = null!;
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
    public static readonly ItemId Tm04Id = new("pret:ITEM_TM04");
    public static readonly ItemId MaxRepelId = new("pret:ITEM_MAX_REPEL");
    public static PretSaffronGymItemsDocument LoadProject(string root)
    {
        var source = SaffronGymSource.Catalogs(root); var d = source.Parse<PretSaffronGymItemsDocument>("items.json");
        SilphSourcePackage.Need(d.SchemaVersion == 1 &&
            d.Items.Select(i => i.Metadata.Item.Id).SequenceEqual([Tm04Id, MaxRepelId]) && d.Icons.Length == 1 &&
            !d.Limitations.IsDefaultOrEmpty, "Saffron Gym item scope differs.");
        foreach (var item in d.Items)
        {
            var i = item.Metadata.Item;             SilphSourcePackage.Need(item.EffectBytes.IsEmpty && item.EffectSource is null && i.Importance == 0 && i.Registrability == 0 &&
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
        var machines = source.Parse<SaffronGymMachinesDocument>("machines.json");
        SilphSourcePackage.Need(machines.SchemaVersion == 1 &&
            machines.InheritedItemIds.Length == 27 && machines.InheritedItemIds.Distinct().Count() == 27 && machines.Additions.IsEmpty && machines.NewMachines.Length == 1 &&
            !machines.Limitations.IsDefaultOrEmpty, "Saffron Gym machine scope differs.");
        var machine = machines.NewMachines[0];
        SilphSourcePackage.Need(machine.Machine == d.Items[0].Metadata && machine.Move.Value == "pret:MOVE_CALM_MIND" && machine.SourceMoveId == 347 && machine.MovePp == 20 &&
            machine.Compatibility.Length == 113 && machine.Compatibility.Select(c => c.Species).Distinct().Count() == 113 && machine.Compatibility.Count(c => c.CanLearn) == 10 &&
            machine.Compatibility.Select(c => c.SourceSpeciesId).SequenceEqual(machine.Compatibility.Select(c => c.SourceSpeciesId).Order()) &&
            !machine.Compatibility.Any(c => c.SourceSpeciesId is 65 or 80 or 122), "TM04 source mapping/ownership differs.");
        return d with { Machines = machines};
    }
    public static EarlyShopItemDefinition Definition(PretSaffronGymItemsDocument document, ItemId id)
    {
        var definition = PretSsAnneItems.Definition(document.Items.Single(i => i.Metadata.Item.Id == id));
        return id == MaxRepelId ? definition with { UseSupport = EarlyShopUseSupport.DeferredRepel } : definition;
    }
    public static EarlyShopStock MartStock(PretSaffronGymRegion region)
    {
        return new("SaffronCity_Mart_Items",
            region.Contracts.GetProperty("martStock").EnumerateArray().Select(item => new ItemId("pret:" + item.GetString()!)).ToImmutableArray(),
            new("data/maps/SaffronCity_Mart/scripts.inc", "SaffronCity_Mart_Items", 24, 31));
    }
    /// <summary>Exactly one ordinary item, retaining all predecessor objects by reference. Caller authenticates successor shop proof.</summary>
    public static EarlyShopCatalog Extend(EarlyShopCatalog previous, PretSaffronGymItemsDocument items)
    {
        SilphShopCatalog.ValidateCatalog(previous);
        SilphSourcePackage.Need(previous.Items.All(i => i.Id != MaxRepelId), "Max Repel already exists in predecessor.");
        return previous with { Profile = "firered-saffron-gym-inventory-v1",Items = previous.Items.Add(Definition(items, MaxRepelId)) };
    }
}
public static class PretSaffronGymMachines
{
    public const string Profile = "firered-saffron-gym-teaching-v1";
    public static SaffronGymMachinesDocument LoadProject(string root) => PretSaffronGymItems.LoadProject(root).Machines;
    public static PewterMachineCatalog Extend(PewterMachineCatalog previous, PretSaffronGymItemsDocument items)
    {
        var proof = SilphMachineCatalog.For(previous) ?? throw new InvalidDataException("Saffron Gym machine predecessor is unauthenticated.");
        var source = items.Machines;
        SilphSourcePackage.Need(previous.Compatibility.Length == 113 && previous.AdditionalMachines?.Length == 26 &&
            new[] { previous.Tm39.Item.Id }.Concat(previous.AdditionalMachines!.Value.Select(m => m.Machine.Item.Id)).SequenceEqual(source.InheritedItemIds) &&
            source.NewMachines[0].Compatibility.Select(c => c.Species).ToHashSet().SetEquals(proof.Pokemon.Catalog.Species.Keys), "Saffron Gym machine predecessor differs.");
        return previous with { Profile = Profile,AdditionalMachines = previous.AdditionalMachines!.Value.Add(source.NewMachines.Single()) };
    }
}

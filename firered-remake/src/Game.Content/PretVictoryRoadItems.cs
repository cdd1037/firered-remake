using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record VictoryRoadPickup(string Id, MapId Map, int LocalId, int X, int Y, int Elevation,
    ItemId Item, int Quantity, string Flag, bool Hidden, bool Underfoot, string ScriptEntry, OpeningItemSourceRef Source);
public sealed record VictoryRoadItemsDocument(string SourceCommit, string Profile, ImmutableArray<PewterMachineItem> Items,
    ImmutableArray<VictoryRoadPickup> Pickups, bool Renewable, ImmutableArray<string> Limitations);
public sealed record VictoryRoadMachinesDocument(string SourceCommit, string Profile,
    ImmutableArray<PewterAdditionalMachine> Machines);
public sealed record VictoryRoadItemContent(VictoryRoadItemsDocument Items, VictoryRoadMachinesDocument Machines);

/// <summary>Pinned source declarations plus two ordinary inventory additions and four machine additions.</summary>
public static class PretVictoryRoadItems
{

    public static VictoryRoadItemContent LoadProject(string root)
    {
        root = Path.GetFullPath(root);
        var items = SafariSource.Parse<VictoryRoadItemsDocument>(SafariSource.Read(root,            "content/generated/victory-road-rules/items.json"));
        var machines = SafariSource.Parse<VictoryRoadMachinesDocument>(SafariSource.Read(root,            "content/generated/victory-road-rules/machines.json"), root);
        SafariSource.Require(items.Items.Length == 16 &&
            items.Pickups.Length == 18 && !items.Renewable && !items.Limitations.IsDefaultOrEmpty &&
            items.Items.Select(i => i.Item.Id).Distinct().Count() == 16 &&
            items.Pickups.Select(p => p.Flag).Distinct().Count() == 18 && items.Pickups.Count(p => p.Hidden) == 10 &&
            items.Pickups.All(p => p.Quantity == 1 && !p.Underfoot && items.Items.Any(i => i.Item.Id == p.Item)),
            "Victory Road item scope differs.");
        SafariSource.Require(machines.Machines.Length == 4 &&
            machines.Machines.Select(m => m.Machine.Item.Id).SequenceEqual(VictoryRoadMachineCatalog.AdditionalIds) , "Victory Road machine scope differs.");
        foreach (var machine in machines.Machines)
        {
            SafariSource.Require(machine.Machine == items.Items.Single(i => i.Item.Id == machine.Machine.Item.Id) &&
                machine.Compatibility.Length == 115 && machine.Compatibility.Select(c => c.Species).Distinct().Count() == 115,
                "Victory Road machine metadata differs.");
        }
        return new(items, machines);
    }

    public static EarlyShopCatalog ExtendShop(FirstSeviiShopCatalog previous, VictoryRoadItemContent content)
    {
        ArgumentNullException.ThrowIfNull(previous); ArgumentNullException.ThrowIfNull(content);
        return previous.Catalog with { Profile = VictoryRoadShopCatalog.Profile,Items = previous.Catalog.Items.AddRange(NewInventoryDefinitions(content.Items)) };
    }
    public static PewterMachineCatalog ExtendMachines(ViridianGymMachineCatalog previous, VictoryRoadItemContent content)
    {
        ArgumentNullException.ThrowIfNull(previous); ArgumentNullException.ThrowIfNull(content);
        return previous.Catalog with { Profile = VictoryRoadMachineCatalog.Profile,AdditionalMachines = previous.Catalog.AdditionalMachines!.Value.AddRange(content.Machines.Machines) };
    }

    public static ImmutableArray<EarlyShopItemDefinition> NewInventoryDefinitions(VictoryRoadItemsDocument document)
    {
        EarlyShopItemDefinition Define(ItemId id, ImmutableArray<int> effect, string symbol, int start, int end,
            EarlyShopUseSupport use, int heal)
        {
            var metadata = document.Items.Single(i => i.Item.Id == id); var item = metadata.Item;
            return new(item.Id, item.SourceItemSymbol, item.SourceItemId, item.Name, metadata.Description, item.Price,
                item.Pocket, item.Importance, item.Type, item.FieldUseFunction, item.BattleUsage, item.BattleUseFunction,
                item.HoldEffectParam, heal, 0, effect, use, item.Source,
                new("src/data/pokemon/item_effects.h",symbol,start,end)) { SourceMetadata = item };
        }
        return [
            Define(VictoryRoadShopCatalog.GuardSpecId, [0,0,0,128,0,96,1,1], "sItemEffect_GuardSpec", 229, 233, EarlyShopUseSupport.NoFieldUse, 0),
            Define(VictoryRoadShopCatalog.SitrusBerryId, [0,0,0,0,4,0,30], "sItemEffect_SitrusBerry", 333, 336, EarlyShopUseSupport.Potion, 30)
        ];
    }
}

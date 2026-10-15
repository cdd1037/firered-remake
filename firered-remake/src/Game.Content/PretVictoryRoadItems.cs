using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record VictoryRoadPickup(string Id, MapId Map, int LocalId, int X, int Y, int Elevation,
    ItemId Item, int Quantity, string Flag, bool Hidden, bool Underfoot, string ScriptEntry, OpeningItemSourceRef Source);
public sealed record VictoryRoadItemsDocument(ImmutableArray<MachineItem> Items,
    ImmutableArray<VictoryRoadPickup> Pickups, bool Renewable);
public sealed record VictoryRoadMachinesDocument(ImmutableArray<MachineDefinition> Machines);
public sealed record VictoryRoadItemContent(VictoryRoadItemsDocument Items, VictoryRoadMachinesDocument Machines);

/// <summary>Pinned source declarations plus two ordinary inventory additions and four machine additions.</summary>
public static class PretVictoryRoadItems
{

    public static VictoryRoadItemContent LoadProject(string root)
    {
        root = Path.GetFullPath(root);
        var items = SafariSource.Parse<VictoryRoadItemsDocument>(SafariSource.Read(root,            "content/generated/data/regions/victory-road/items.json"), root);
        var machines = SafariSource.Parse<VictoryRoadMachinesDocument>(SafariSource.Read(root,            "content/generated/data/regions/victory-road/machines.json"), root);
        SafariSource.Require(items.Items.Length == 16 &&
            items.Pickups.Length == 18 && !items.Renewable &&
            items.Items.Select(i => i.Item.Id).Distinct().Count() == 16 &&
            items.Pickups.Select(p => p.Flag).Distinct().Count() == 18 && items.Pickups.Count(p => p.Hidden) == 10 &&
            items.Pickups.All(p => p.Quantity == 1 && !p.Underfoot && items.Items.Any(i => i.Item.Id == p.Item)),
            "Victory Road item scope differs.");
        SafariSource.Require(machines.Machines.Length == 4 &&
            machines.Machines.Select(m => m.Machine.Item.Id).SequenceEqual(new ItemId[] { new("pret:ITEM_TM02"), new("pret:ITEM_TM07"), new("pret:ITEM_TM37"), new("pret:ITEM_TM50") }) , "Victory Road machine scope differs.");
        foreach (var machine in machines.Machines)
        {
            SafariSource.Require(machine.Machine == items.Items.Single(i => i.Item.Id == machine.Machine.Item.Id) &&
                machine.Compatibility.Length == 115 && machine.Compatibility.Select(c => c.Species).Distinct().Count() == 115,
                "Victory Road machine metadata differs.");
        }
        return new(items, machines);
    }

    public static ImmutableArray<EarlyShopItemDefinition> NewInventoryDefinitions(VictoryRoadItemsDocument document)
    {
        EarlyShopItemDefinition Define(ItemId id, ImmutableArray<int> effect, EarlyShopUseSupport use, int heal)
        {
            var metadata = document.Items.Single(i => i.Item.Id == id); var item = metadata.Item;
            return new(item, metadata.Description, heal, 0, effect, use);
        }
        return [
            Define(ItemIds.GuardSpecId, [0,0,0,128,0,96,1,1], EarlyShopUseSupport.NoFieldUse, 0),
            Define(ItemIds.SitrusBerryId, [0,0,0,0,4,0,30], EarlyShopUseSupport.Potion, 30)
        ];
    }
}

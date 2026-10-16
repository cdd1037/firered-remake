using Game.Content;
using Game.Domain.Events;
using Game.Domain.Ids;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class FuchsiaMartContentTests
{
    [TestMethod]
    public void MartPreservesOriginalDoorActorsAndExecutablePrograms()
    {
        var catalog = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
        var id = new MapId("map.fuchsia-city-mart");
        var map = catalog.SourceMaps[id];
        Assert.AreEqual((11, 9), (map.Width, map.Height));
        Assert.AreEqual("LAYOUT_MART", map.Events.GetProperty("layout").GetString());
        var entrance = catalog.SourceMaps[new("map.fuchsia-city")].FieldWarps[2];
        Assert.AreEqual((11, 15, id, 1), (entrance.X, entrance.Y, entrance.DestinationMapId, entrance.DestinationWarpId));
        CollectionAssert.AreEqual(new[] { 3, 4, 5 }, map.FieldWarps.Select(warp => warp.X).ToArray());
        foreach (var warp in map.FieldWarps)
        {
            Assert.AreEqual(7, warp.Y);
            Assert.AreEqual(new MapId("map.fuchsia-city"), warp.DestinationMapId);
            Assert.AreEqual(2, warp.DestinationWarpId);
        }
        var actors = catalog.MapDefinition(id).Actors;
        CollectionAssert.AreEqual(new[] { "Clerk", "Gentleman", "CooltrainerF" },
            actors.Select(actor => actor.ScriptId.Replace("FuchsiaCity_Mart_EventScript_", "", StringComparison.Ordinal)).ToArray());
        foreach (var actor in actors)
        {
            var program = catalog.FindProgram(actor.ScriptId);
            Assert.IsNotNull(program);
            Assert.IsFalse(program.Instructions.Any(instruction => instruction.Op == StoryOp.Unsupported));
        }
        Assert.IsFalse(catalog.LandEncounters!.ContainsKey(id));
        Assert.IsFalse(map.Events.GetProperty("allow_cycling").GetBoolean());
        Assert.IsFalse(map.Events.GetProperty("allow_escaping").GetBoolean());
    }

    [TestMethod]
    public void MartBindsSourceCounterToSharedOrderedStockAndPrices()
    {
        var catalog = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
        var id = new MapId("map.fuchsia-city-mart");
        var clerk = catalog.MapDefinition(id).Actors.Single(actor => actor.SourceLocalId == 1);
        var counter = catalog.OrdinaryMarts.ByActor[(id, clerk.LocalId)];
        Assert.AreEqual(clerk, counter.Actor);
        Assert.AreEqual("FuchsiaCity_Mart_Items", counter.Stock.Id);
        Assert.AreSame(catalog.EarlyShop!.Shops.Single(shop => shop.Id == counter.Stock.Id), counter.Stock);
        Assert.AreEqual("data/maps/FuchsiaCity_Mart/scripts.inc", counter.Instruction.Source.Path);
        Assert.AreEqual(clerk.ScriptId, counter.Instruction.Source.Symbol);
        CollectionAssert.AreEqual(new[] { "ULTRA_BALL", "GREAT_BALL", "SUPER_POTION", "REVIVE", "FULL_HEAL", "MAX_REPEL" },
            counter.Stock.Items.Select(item => item.Value.Replace("pret:ITEM_", "", StringComparison.Ordinal)).ToArray());
        CollectionAssert.AreEqual(new[] { 1200, 600, 700, 1500, 600, 700 },
            counter.Stock.Items.Select(item => catalog.Inventory.Items[item].Price).ToArray());
        Assert.IsFalse(catalog.OrdinaryMarts.ByActor.Keys.Any(key => key.Map == id && key.Actor != clerk.LocalId));
    }
}

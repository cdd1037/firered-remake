using System.Text.Json;
using Game.Content;
using Game.Domain.World;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class WorldHiddenPickupTests
{
    [TestMethod]
    public void EverySelectedWorldHiddenEventHasExactlyOnePickupWithSharedItemMetadata()
    {
        var content = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
        var items = SharedItemCatalog.LoadProject(TestAssets.Root);
        var pickups = content.FieldPickups.Where(p => p.Hidden).ToArray();
        var events = content.SourceMaps.Values.SelectMany(map => map.Events.GetProperty("bg_events").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "hidden_item").Select(e => (map, row: e))).ToArray();
        Assert.IsTrue(events.Length > 0);
        Assert.AreEqual(events.Length, pickups.Length);
        foreach (var (map, row) in events)
        {
            var pickup = pickups.Single(p => p.MapSymbol == map.SourceMapSymbol && p.X == row.GetProperty("x").GetInt32() && p.Y == row.GetProperty("y").GetInt32());
            Assert.AreEqual(row.GetProperty("flag").GetString(), pickup.Flag);
            Assert.AreEqual(row.GetProperty("item").GetString(), pickup.ItemSymbol);
            Assert.AreEqual(row.GetProperty("quantity").GetInt32(), pickup.Quantity);
            Assert.AreEqual(row.GetProperty("elevation").GetInt32(), pickup.Elevation);
            Assert.AreEqual("EventScript_HiddenItemScript", pickup.Script);
            var trigger = content.MapDefinition(map.Id).BackgroundTriggers.Single(t => t.X == pickup.X && t.Y == pickup.Y);
            Assert.AreEqual(row.GetProperty("underfoot").GetBoolean(), trigger.Underfoot, pickup.Flag);
            if (pickup.ItemSymbol == "ITEM_NONE") continue;
            var item = items.Definition(pickup.Item);
            Assert.AreEqual((item.SourceItemId, item.Name, item.Pocket), (pickup.SourceItemId, pickup.ItemName, pickup.Pocket));
            Assert.IsTrue(content.Inventory.Items.ContainsKey(pickup.Item));
        }
    }

    [TestMethod]
    public void NorthernRouteBerriesAndRecoveryItemsAreNotFilteredOut()
    {
        var content = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
        foreach (var (map, x, y, item) in new[] {
            ("MAP_ROUTE24", 19, 4, "ITEM_PECHA_BERRY"), ("MAP_ROUTE25", 14, 2, "ITEM_ELIXIR"),
            ("MAP_ROUTE25", 58, 6, "ITEM_ETHER"), ("MAP_ROUTE25", 33, 8, "ITEM_ORAN_BERRY"),
            ("MAP_ROUTE25", 40, 3, "ITEM_BLUK_BERRY") })
            Assert.AreEqual(item, content.FieldPickups.Single(p => p.Hidden && p.MapSymbol == map && p.X == x && p.Y == y).ItemSymbol);
    }
    [TestMethod]
    public void NorthSouthTunnelUsesAllSevenExistingRenewableFlags()
    {
        var content = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
        const string map = "MAP_UNDERGROUND_PATH_NORTH_SOUTH_TUNNEL";
        var group = RenewableHiddenItemRules.Groups.Single(g => g.MapSymbol == map);
        CollectionAssert.AreEquivalent(group.Rare.Concat(group.Uncommon).Concat(group.Common).Distinct().ToArray(),
            content.FieldPickups.Where(p => p.Hidden && p.MapSymbol == map).Select(p => p.Flag).ToArray());
    }

    [TestMethod]
    public void CoinsKeepAmountsAndAllUnderfootSpotsUseItemfinderPrecedence()
    {
        var content = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
        var coins = content.FieldPickups.Where(p => p.Hidden && p.ItemSymbol == "ITEM_NONE").ToArray();
        Assert.AreEqual(12, coins.Length);
        Assert.AreEqual(260, coins.Sum(p => p.Quantity));
        Assert.IsTrue(coins.All(p => p.SourceItemId == 0 && p.ItemName == "COINS" && p.Pocket == ""));
        var source = content.ItemfinderSource!;
        var underfoot = content.SourceMaps.Values.SelectMany(map => content.MapDefinition(map.Id).BackgroundTriggers
            .Where(t => t.Underfoot).Select(t => (map, trigger: t))).ToArray();
        Assert.AreEqual(underfoot.Length, source.Underfoot.Length);
        foreach (var (map, trigger) in underfoot)
        {
            var response = ItemfinderRules.Scan(content, new(map.Id, trigger.X, trigger.Y, trigger.Elevation, Direction.South),
                new Dictionary<string, bool>(), source);
            Assert.IsNotNull(response.Target);
            Assert.IsTrue(response.Target.Underfoot);
            Assert.AreEqual(trigger.HiddenFlag, response.Target.Flag);
            Assert.AreEqual(3, response.Dings);
        }
    }

}

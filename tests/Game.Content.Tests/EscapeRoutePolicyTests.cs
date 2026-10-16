using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class EscapeRoutePolicyTests
{
    [TestMethod]
    public void CurrentRoutesKeepActualEntranceCoordinatesAndInteriorMembership()
    {
        var maps = new[] { "route-4", "mt-moon-1f", "mt-moon-b1f", "mt-moon-b2f", "route-2",
            "viridian-forest", "route-2-forest-south-gate", "route-2-forest-north-gate", "route-10", "power-plant" }
            .Select(n => new MapId("map." + n));
        var policy = CurrentEscapeRoutes.Create(maps);
        var moon = policy.RecordWarp(null, new(new("map.route-4"), 19, 5, 3, Direction.North), new("map.mt-moon-1f"));
        Assert.AreEqual(new EscapeRopeDestination(new("map.route-4"), 19, 6), moon);
        Assert.IsTrue(policy.Exits[new("map.mt-moon-b2f")].Contains(moon!));
        Assert.AreEqual(moon, policy.RecordWarp(moon, new(new("map.mt-moon-1f"), 1, 1, 3, Direction.North), new("map.mt-moon-b1f")));
        Assert.AreEqual(new EscapeRopeDestination(new("map.route-2"), 5, 13),
            policy.RecordWarp(moon, new(new("map.route-2"), 5, 13, 3, Direction.South), new("map.route-2-forest-north-gate")));
        Assert.AreEqual(new EscapeRopeDestination(new("map.route-10"), 2, 38),
            policy.RecordWarp(null, new(new("map.route-10"), 2, 37, 3, Direction.East), new("map.power-plant")));
        Assert.ThrowsExactly<NotSupportedException>(() => policy.RecordWarp(null,
            new(new("map.route-4"), 20, 5, 3, Direction.North), new("map.mt-moon-1f")));
        Assert.ThrowsExactly<NotSupportedException>(() => policy.RecordWarp(null,
            new(new("map.route-4"), 19, 5, 2, Direction.North), new("map.mt-moon-1f")));
        Assert.IsFalse(policy.Exits.ContainsKey(new("map.lost-cave-room1")));
    }

    [TestMethod]
    public void MissingMapsDoNotBecomeAdmittedByARegionOrShopProfile()
    {
        var policy = CurrentEscapeRoutes.Create([new("map.route-4")]);
        Assert.AreEqual(0, policy.Exits.Count);
        Assert.AreEqual(0, policy.Entrances.Length);
        Assert.AreEqual("escape_rope_destination_invalid", policy.ValidateDestination(new(new("map.route-4"), 19, 6)));
    }
}

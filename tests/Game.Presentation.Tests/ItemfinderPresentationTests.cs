using Game.Domain.Ids;
using Game.Domain.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Game.Presentation.Tests;

[TestClass]
public sealed class ItemfinderPresentationTests
{
    private static ItemfinderResponse Response(Direction? direction, bool underfoot = false, int dings = 4) =>
        new(new(new MapId("map"), 0, 0, "flag", "ITEM_POTION", underfoot), 0, 0, direction, dings);

    [TestMethod]
    public void SourcePulseIntervalsAndMessageDelay()
    {
        var response = Response(Direction.East);
        Assert.AreEqual(100, ItemfinderPresentation.MessageFrame(response));
        Assert.IsNull(ItemfinderPresentation.Sprite(response, Direction.South, 24, 1));
        Assert.AreEqual(1, ItemfinderPresentation.Sprite(response, Direction.South, 25, 1)!.Value.Frame);
        Assert.IsNull(ItemfinderPresentation.Sprite(response, Direction.South, 100, 4));
        Assert.AreEqual(50, ItemfinderPresentation.MessageFrame(Response(Direction.West, dings: 2)));
    }

    [TestMethod]
    public void SignedMotionAndBoundaryDestructionMatchSource()
    {
        var west = Response(Direction.West);
        Assert.AreEqual(119, ItemfinderPresentation.Sprite(west, Direction.South, 0, 0)!.Value.X);
        Assert.AreEqual(104, ItemfinderPresentation.Sprite(west, Direction.South, 38, 0)!.Value.X);
        Assert.IsNull(ItemfinderPresentation.Sprite(west, Direction.South, 39, 0));
        var east = Response(Direction.East);
        Assert.AreEqual(133, ItemfinderPresentation.Sprite(east, Direction.South, 33, 0)!.Value.X);
        Assert.IsNull(ItemfinderPresentation.Sprite(east, Direction.South, 34, 0));
    }

    [TestMethod]
    public void UnderfootStarAndOrdinarySameTileFacingAreDifferent()
    {
        var star = Response(null, true, 3);
        Assert.AreEqual(75, ItemfinderPresentation.MessageFrame(star));
        Assert.AreEqual(new ItemfinderSprite(4, 120, 75, 0), ItemfinderPresentation.Sprite(star, Direction.East, 0, 0));
        Assert.AreEqual(180, ItemfinderPresentation.Sprite(Response(null), Direction.East, 0, 0)!.Value.RotationDegrees);
        var none = new ItemfinderResponse(null, 0, 0, null, 0);
        Assert.AreEqual(0, ItemfinderPresentation.MessageFrame(none));
        Assert.IsNull(ItemfinderPresentation.Sprite(none, Direction.East, 0, 0));
    }
}

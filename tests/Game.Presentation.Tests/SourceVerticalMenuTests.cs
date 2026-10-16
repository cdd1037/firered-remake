namespace FireRed.Tests.Presentation;

[TestClass]
public sealed class SourceVerticalMenuTests
{
    [TestMethod]
    public void ThreeOptionsReachThirdRowWithoutWrapping()
    {
        var menu = new SourceVerticalMenuPresentation(["YES", "NO", "INFO"], 18, 6);
        Assert.IsFalse(menu.Move(-1));
        Assert.IsTrue(menu.Move(1)); Assert.IsTrue(menu.Move(1));
        Assert.AreEqual(2, menu.Cursor); Assert.IsFalse(menu.Move(1));
    }
    [TestMethod]
    public void FiveOptionsWrapAndHonorDefault()
    {
        var menu = new SourceVerticalMenuPresentation(["SINGLE", "DOUBLE", "KNOCKOUT", "MIXED", "EXIT"], 13, 3, 4);
        Assert.AreEqual(4, menu.Cursor); Assert.IsTrue(menu.Move(1)); Assert.AreEqual(0, menu.Cursor);
        Assert.IsTrue(menu.Move(-1)); Assert.AreEqual(4, menu.Cursor);
    }
    [TestMethod]
    public void PointerUsesTheSameRowsAfterRelocationAndRightEdgeClamp()
    {
        var menu = new SourceVerticalMenuPresentation(["YES", "NO", "INFO"], 27, 2);
        var frame = menu.Frame(40);
        Assert.AreEqual(240, frame.X + frame.Width);
        Assert.AreEqual(16, frame.Y);
        Assert.AreEqual(2, menu.RowAt(frame.X + 16, frame.Y + 8 + 28, 40));
        Assert.AreEqual(-1, menu.RowAt(frame.X, frame.Y, 40));
        Assert.IsTrue(menu.Select(2)); Assert.AreEqual(2, menu.Cursor);
    }
    [TestMethod]
    public void InvalidDefaultAndEmptyRowsFail()
    {
        Assert.Throws<ArgumentException>(() => new SourceVerticalMenuPresentation([], 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourceVerticalMenuPresentation(["YES"], 0, 0, 1));
    }

    [TestMethod]
    public void NativeListUsesFourRowsSourceScrollThresholdsAndNoWrap()
    {
        var menu = new SourceVerticalMenuPresentation(Enumerable.Range(0, 9).Select(i => "Topic " + i), 1, 1, 0, 4, 7);
        Assert.AreEqual((0, 0, 72), (menu.Frame(72).X, menu.Frame(72).Y, menu.Frame(72).Height));
        Assert.IsFalse(menu.Move(-1));
        menu.Move(1); menu.Move(1);
        Assert.AreEqual((2, 0), (menu.Cursor, menu.ScrollOffset));
        menu.Move(1);
        Assert.AreEqual((3, 1), (menu.Cursor, menu.ScrollOffset));
        menu.Select(8);
        Assert.AreEqual((8, 5), (menu.Cursor, menu.ScrollOffset));
        Assert.IsFalse(menu.CanScrollDown); Assert.IsFalse(menu.Move(1));
        menu.Move(-1); menu.Move(-1);
        Assert.AreEqual((6, 5), (menu.Cursor, menu.ScrollOffset));
        menu.Move(-1);
        Assert.AreEqual((5, 4), (menu.Cursor, menu.ScrollOffset));
        var frame = menu.Frame(72);
        Assert.AreEqual(6, menu.RowAt(frame.X + 16, frame.Y + 8 + 28, 72));
        Assert.AreEqual(-1, menu.RowAt(frame.X + 16, frame.Y + 8 + 56, 72));
        // Returning to a topic with the same selection retains its scroll offset.
        Assert.IsFalse(menu.Select(5)); Assert.AreEqual(4, menu.ScrollOffset);
        menu.Select(0); Assert.AreEqual((0, 0), (menu.Cursor, menu.ScrollOffset));
    }

    [TestMethod]
    public void OneRowNativeListScrollsOnEveryMove()
    {
        var menu = new SourceVerticalMenuPresentation(["A", "B", "Exit"], 1, 1, 0, 1, 2);
        menu.Move(1); Assert.AreEqual((1, 1), (menu.Cursor, menu.ScrollOffset));
        menu.Move(1); Assert.AreEqual((2, 2), (menu.Cursor, menu.ScrollOffset));
        Assert.IsFalse(menu.Move(1));
        menu.Move(-1); Assert.AreEqual((1, 1), (menu.Cursor, menu.ScrollOffset));
    }
}

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
}

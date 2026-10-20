using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class SourceDialogPageCursorTests
{
    [TestMethod]
    [DataRow(1)]
    [DataRow(3)]
    public void OnlyFinalCompletedVisualPageAcknowledgesOnce(int count)
    {
        var cursor = new SourceDialogPageCursor();
        cursor.Reset(count);
        for (int page = 0; page < count; page++)
        {
            Assert.AreEqual(page, cursor.Page);
            Assert.IsFalse(cursor.TryAdvanceVisual(false));
            Assert.IsFalse(cursor.TryTakeSemanticAcknowledgement(false));
            if (page + 1 < count)
            {
                Assert.IsFalse(cursor.TryTakeSemanticAcknowledgement(true));
                Assert.IsTrue(cursor.TryAdvanceVisual(true));
            }
        }
        Assert.IsFalse(cursor.TryAdvanceVisual(true));
        Assert.IsTrue(cursor.TryTakeSemanticAcknowledgement(true));
        Assert.IsFalse(cursor.TryTakeSemanticAcknowledgement(true));
        cursor.Reset(count);
        Assert.AreEqual(0, cursor.Page);
        Assert.IsFalse(cursor.Acknowledged);
        cursor.Clear();
        Assert.IsFalse(cursor.TryTakeSemanticAcknowledgement(true));
    }

    [TestMethod]
    public void EngineRevealCountIncludesLineBreaksAndCompleteUnicodeElements()
    {
        var printer = new SourceTextProgress();
        printer.Reset("宝𠮷\ne\u0301", 1);
        printer.AdvanceFrames(1, false, false);
        Assert.AreEqual(1, printer.VisibleCodepoints);
        printer.AdvanceFrames(1, false, false);
        Assert.AreEqual(2, printer.VisibleCodepoints);
        printer.AdvanceFrames(1, false, false);
        Assert.AreEqual(5, printer.VisibleCodepoints);
        Assert.AreEqual("宝𠮷\ne\u0301", printer.VisibleText);
        printer.AdvanceFrames(1, false, false);
        Assert.IsTrue(printer.Complete);
        printer.ContinuePage("下一页");
        Assert.AreEqual(0, printer.VisibleCodepoints);
    }
}

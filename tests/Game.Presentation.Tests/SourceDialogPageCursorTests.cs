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
    [TestMethod]
    [DataRow(1)]
    [DataRow(4)]
    [DataRow(8)]
    public void FieldVisualPagesKeepPrinterCadenceAndSemanticReadinessSeparate(int speed)
    {
        var cursor = new SourceDialogPageCursor();
        var printer = new SourceTextProgress();
        cursor.Reset(3);
        printer.Reset("第一页\n第二行\n", speed);
        int semanticPage = 4;
        for (int visual = 0; visual < 3; visual++)
        {
            Assert.IsFalse(cursor.TryAdvanceVisual(printer.Complete));
            Assert.IsFalse(printer.Complete && cursor.IsFinal);
            printer.AdvanceFrames(1, false, false);
            printer.AdvanceFrames(1, true, true);
            printer.AdvanceFrames(100, true, false);
            Assert.IsTrue(printer.Complete);
            Assert.AreEqual(speed > 1, printer.HasPrintBeenSpedUp);
            Assert.AreEqual(4, semanticPage);
            if (visual < 2)
            {
                Assert.IsFalse(cursor.TryTakeSemanticAcknowledgement(printer.Complete));
                Assert.IsTrue(cursor.TryAdvanceVisual(printer.Complete));
                printer.ContinuePage("下一页");
                Assert.IsFalse(printer.Complete);
                Assert.AreEqual(0, printer.VisibleCodepoints);
                Assert.AreEqual(speed > 1, printer.HasPrintBeenSpedUp);
                Assert.AreEqual(speed, printer.ConfiguredSpeed);
            }
        }
        Assert.IsTrue(printer.Complete && cursor.IsFinal);
        Assert.IsTrue(cursor.TryTakeSemanticAcknowledgement(printer.Complete));
        semanticPage++;
        Assert.IsFalse(cursor.TryTakeSemanticAcknowledgement(printer.Complete));
        Assert.AreEqual(5, semanticPage);
        // Repeated identical source pages are new semantic owners, not a cache hit.
        cursor.Reset(1);
        printer.Reset("下一页", speed);
        Assert.IsFalse(printer.Complete);
        Assert.IsFalse(printer.HasPrintBeenSpedUp);
        Assert.IsFalse(cursor.Acknowledged);
    }

}

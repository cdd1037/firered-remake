using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class SourceMessageCursorTests
{
    private static float Measure(string text) => StringInfo.ParseCombiningCharacters(text).Length * 12;

    [TestMethod]
    public void IdenticalTextOnDifferentSemanticPagesRestartsVisualCursor()
    {
        var cursor = new SourceMessageCursor();
        const string text = "一二三四五六七八九";
        Assert.IsTrue(cursor.Synchronize(("battle", 0), text, 24, Measure));
        Assert.IsTrue(cursor.TryAdvance());
        Assert.AreEqual(1, cursor.Sample.VisualPage);
        Assert.IsFalse(cursor.Synchronize(("battle", 0), text, 24, Measure));
        Assert.AreEqual(1, cursor.Sample.VisualPage);
        Assert.IsTrue(cursor.Synchronize(("battle", 1), text, 24, Measure));
        Assert.AreEqual(0, cursor.Sample.VisualPage);
        Assert.AreEqual("一二\n三四", cursor.Sample.Text);
    }

    [TestMethod]
    public void NewOwnerOrChangedSourceTextRestartsEvenAtSameSourceIndex()
    {
        var cursor = new SourceMessageCursor();
        cursor.Synchronize(("first", 0), "一二三四五六七", 24, Measure);
        cursor.TryAdvance();
        Assert.IsTrue(cursor.Synchronize(("second", 0), "一二三四五六七", 24, Measure));
        Assert.AreEqual(0, cursor.Sample.VisualPage);
        cursor.TryAdvance();
        Assert.IsTrue(cursor.Synchronize(("second", 0), "一二三四五六八", 24, Measure));
        Assert.AreEqual(0, cursor.Sample.VisualPage);
    }

    [TestMethod]
    public void FontIdentityAndWidthArePartOfOwnerSynchronization()
    {
        var cursor = new SourceMessageCursor();
        object font = new(), otherFont = new();
        cursor.Synchronize(("page", font), "一二三四五六七", 24, Measure);
        cursor.TryAdvance();
        Assert.IsTrue(cursor.Synchronize(("page", font), "一二三四五六七", 36, Measure));
        Assert.AreEqual(0, cursor.Sample.VisualPage);
        cursor.TryAdvance();
        Assert.IsTrue(cursor.Synchronize(("page", otherFont), "一二三四五六七", 36, Measure));
        Assert.AreEqual(0, cursor.Sample.VisualPage);
    }

    [TestMethod]
    public void EnglishPathDoesNotMeasureReflowOrAddAcknowledgements()
    {
        var cursor = new SourceMessageCursor();
        const string text = "  RED  used TACKLE!\n\nLiteral {PLAYER}\\p$";
        cursor.Synchronize("english", text, 1, _ => throw new AssertFailedException("English was measured."));
        Assert.AreEqual(text, cursor.Sample.Text);
        Assert.AreEqual(1, cursor.Sample.PageCount);
        Assert.IsTrue(cursor.Sample.IsFinal);
        Assert.IsFalse(cursor.TryAdvance());
    }

    [TestMethod]
    public void ClearSeparatesEqualNoticesAndCannotAdvanceAbsentMessage()
    {
        var cursor = new SourceMessageCursor();
        Assert.IsFalse(cursor.TryAdvance());
        cursor.Synchronize("notice", "一二三四五六七", 24, Measure);
        cursor.TryAdvance();
        cursor.Clear();
        Assert.IsFalse(cursor.TryAdvance());
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = cursor.Sample);
        Assert.IsTrue(cursor.Synchronize("notice", "一二三四五六七", 24, Measure));
        Assert.AreEqual(0, cursor.Sample.VisualPage);
    }

    [TestMethod]
    public void FinalVisualSampleIsStableUntilOwnerChangesSemanticPage()
    {
        var cursor = new SourceMessageCursor();
        cursor.Synchronize("source-control-boundary", "一二三四五六七八九", 24, Measure);
        var samples = new List<string> { cursor.Sample.Text };
        while(cursor.TryAdvance()) samples.Add(cursor.Sample.Text);
        Assert.AreEqual(3, samples.Count);
        Assert.AreEqual("一二三四五六七八九", string.Concat(samples).Replace("\n", ""));
        var final = cursor.Sample;
        Assert.IsTrue(final.IsFinal);
        Assert.IsFalse(cursor.TryAdvance());
        Assert.AreEqual(final, cursor.Sample);
        Assert.IsFalse(cursor.Synchronize("source-control-boundary", "一二三四五六七八九", 24, Measure));
        Assert.AreEqual(final, cursor.Sample);
    }

    [TestMethod]
    [DataRow(174)] // PC/bag result panel, including its continuation cursor.
    [DataRow(198)] // Field/Oak dialogue.
    [DataRow(212)] // Battle text after its two-pixel inset.
    public void LongNamesRemainCompleteWithinActualContinuationBudgets(int width)
    {
        var cursor = new SourceMessageCursor();
        string text = "对手的" + new string('宝', 10) + "被" + new string('梦', 10) + "缠住了！\n下一行";
        cursor.Synchronize(("event", 2), text, width, Measure);
        var rows = new List<string>();
        do
        {
            string[] lines = cursor.Sample.Text.Split('\n');
            Assert.IsTrue(lines.Length <= 2);
            Assert.IsTrue(lines.All(line => Measure(line) <= width));
            rows.AddRange(lines);
        } while(cursor.TryAdvance());
        Assert.AreEqual(text.Replace("\n", ""), string.Concat(rows));
    }

    [TestMethod]
    public void FieldContinuationKeepsPrinterCadenceAndDefersWholeMessageCompletion()
    {
        var cursor = new SourceMessageCursor();
        var printer = new SourceTextProgress();
        cursor.Synchronize(("dialogue", 0), "一二三四五六七八九", 24, Measure);
        printer.Reset(cursor.Sample.Text, 4);
        printer.AdvanceFrames(1, false, false);
        printer.AdvanceFrames(1, true, true);
        printer.AdvanceFrames(100, true, false);
        Assert.IsTrue(printer.Complete);
        Assert.IsFalse(printer.Complete && cursor.Sample.IsFinal);
        int delay = printer.DelayCounter;
        Assert.IsTrue(cursor.TryAdvance());
        printer.ContinuePage(cursor.Sample.Text);
        Assert.IsTrue(printer.HasPrintBeenSpedUp);
        Assert.AreEqual(delay, printer.DelayCounter);
        Assert.AreEqual("", printer.VisibleText);
        Assert.IsFalse(printer.Complete);
        printer.AdvanceFrames(100, false, false);
        Assert.IsFalse(printer.Complete && cursor.Sample.IsFinal);
        Assert.IsTrue(cursor.TryAdvance());
        printer.ContinuePage(cursor.Sample.Text);
        printer.AdvanceFrames(100, false, false);
        Assert.IsTrue(printer.Complete && cursor.Sample.IsFinal);
    }
}

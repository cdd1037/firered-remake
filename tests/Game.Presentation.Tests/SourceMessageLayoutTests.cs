using System.Globalization;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class SourceMessageLayoutTests
{
    private static float Measure(string text)
    {
        var elements = StringInfo.GetTextElementEnumerator(text);
        float width = 0;
        while (elements.MoveNext())
            width += Rune.GetRuneAt((string)elements.Current, 0).Value >= 0x2E80 ? 12 : 6;
        return width;
    }

    [TestMethod]
    public void OneSourcePageHasSeparateBoundedVisualPages()
    {
        const string text = "欢迎来到宝可梦世界";
        var layout = SourceMessageLayout.Create(text, 36, Measure);
        Assert.AreEqual(text, layout.SourceText);
        Assert.AreEqual(2, layout.PageCount);
        Assert.AreEqual("欢迎来\n到宝可", layout.Sample(0).Text);
        Assert.AreEqual("梦世界", layout.Sample(1).Text);
        Assert.IsFalse(layout.Sample(0).IsFinal);
        Assert.IsTrue(layout.Sample(1).IsFinal);
        Assert.IsFalse(layout.TryGetSingleWindow(out string partial));
        Assert.AreEqual("", partial);
    }

    [TestMethod]
    public void LongLegalChineseNamesAreWrappedWithoutShortening()
    {
        string name = new('宝', 10);
        string text = "对手的" + name + "被" + name + "缠住了！";
        var layout = SourceMessageLayout.Create(text, 224, Measure);
        string reconstructed = string.Concat(Enumerable.Range(0, layout.PageCount)
            .Select(i => layout.Sample(i).Text.Replace("\n", "")));
        Assert.AreEqual(text, reconstructed);
        foreach (int i in Enumerable.Range(0, layout.PageCount))
        {
            var lines = layout.Sample(i).Text.Split('\n');
            Assert.IsTrue(lines.Length <= 2);
            Assert.IsTrue(lines.All(line => Measure(line) <= 224));
        }
    }

    [TestMethod]
    public void LiteralSpacesAndExplicitBlankLinesSurvive()
    {
        const string text = "  RED  \n\n 中文 \n";
        var layout = SourceMessageLayout.Create(text, 208, Measure);
        Assert.AreEqual(2, layout.PageCount);
        Assert.AreEqual("  RED  \n", layout.Sample(0).Text);
        Assert.AreEqual(" 中文 \n", layout.Sample(1).Text);
        Assert.AreEqual(text, string.Join('\n', Enumerable.Range(0, layout.PageCount).Select(i => layout.Sample(i).Text)));
    }

    [TestMethod]
    public void EmptySourcePageRetainsOneAcknowledgementBoundary()
    {
        var layout = SourceMessageLayout.Create("", 208, Measure);
        Assert.AreEqual(1, layout.PageCount);
        Assert.IsTrue(layout.Sample(0).IsFinal);
        Assert.IsTrue(layout.TryGetSingleWindow(out string text));
        Assert.AreEqual("", text);
    }

    [TestMethod]
    public void LiteralControlLookingNameDoesNotCreateSourcePages()
    {
        const string text = "中文{PLAYER}\\p$";
        var layout = SourceMessageLayout.Create(text, 208, Measure);
        Assert.AreEqual(1, layout.PageCount);
        Assert.AreEqual(text, layout.Sample(0).Text);
    }

    [TestMethod]
    public void SamplingNeverMutatesSourceIdentityOrCursor()
    {
        var layout = SourceMessageLayout.Create("一二三四五六七", 24, Measure);
        var first = layout.Sample(0);
        _ = layout.Sample(1);
        Assert.AreEqual(first, layout.Sample(0));
        Assert.IsTrue(layout.Matches("一二三四五六七"));
        Assert.IsFalse(layout.Matches("一二三四五六"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => layout.Sample(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => layout.Sample(layout.PageCount));
    }

    [TestMethod]
    public void TwoLinePromptFitDoesNotTruncateOrInventAnAck()
    {
        var fits = SourceMessageLayout.Create("请选择要放入电脑的道具。", 208, Measure);
        Assert.IsTrue(fits.TryGetSingleWindow(out string text));
        Assert.AreEqual(fits.SourceText, text);
        var tooLong = SourceMessageLayout.Create(new string('宝', 40), 208, Measure);
        Assert.IsFalse(tooLong.TryGetSingleWindow(out string missing));
        Assert.AreEqual("", missing);
    }

    [TestMethod]
    public void PrinterContinuationPreservesDelayAndSpeedLatchButNotSemanticAck()
    {
        var layout = SourceMessageLayout.Create("一二三四五六七", 24, Measure);
        var printer = new SourceTextProgress();
        printer.Reset(layout.Sample(0).Text, 4);
        printer.AdvanceFrames(1, false, false);
        printer.AdvanceFrames(1, true, true);
        printer.AdvanceFrames(100, true, false);
        Assert.IsTrue(printer.Complete);
        Assert.IsTrue(printer.HasPrintBeenSpedUp);
        Assert.IsFalse(layout.Sample(0).IsFinal);
        int delay = printer.DelayCounter;
        printer.ContinuePage(layout.Sample(1).Text);
        Assert.IsFalse(printer.Complete);
        Assert.IsTrue(printer.HasPrintBeenSpedUp);
        Assert.AreEqual(delay, printer.DelayCounter);
    }

    [TestMethod]
    public void InvalidTextAndImpossibleMetricsFailInsteadOfSilentlyClipping()
    {
        Assert.ThrowsExactly<ArgumentException>(() => SourceMessageLayout.Create("中文\f下一页", 208, Measure));
        Assert.ThrowsExactly<ArgumentException>(() => SourceMessageLayout.Create("\uD800", 208, Measure));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => SourceMessageLayout.Create("中文", 0, Measure));
        Assert.ThrowsExactly<ArgumentException>(() => SourceMessageLayout.Create("中文", 11, Measure));
    }
}

using System.Globalization;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class ChineseTextLayoutTests
{
    private static float Measure(string text)
    {
        var elements = StringInfo.GetTextElementEnumerator(text);
        float result = 0;
        while (elements.MoveNext())
            result += Rune.GetRuneAt((string)elements.Current, 0).Value >= 0x2E80 ? 12 : 6;
        return result;
    }

    [TestMethod]
    public void EnglishWordWrapAndExplicitEmptyLinesStayUnchanged()
    {
        CollectionAssert.AreEqual(new[] { "ONE TWO", "THREE", "", "FOUR" },
            SourceHelpTextLayout.Wrap("ONE TWO THREE\n\nFOUR", 42, Measure).ToArray());
    }

    [TestMethod]
    public void ChineseWithoutSpacesWrapsByMeasuredWholeCharacters()
    {
        const string text = "欢迎来到宝可梦世界";
        var lines = SourceHelpTextLayout.Wrap(text, 48, Measure);
        CollectionAssert.AreEqual(new[] { "欢迎来到", "宝可梦世", "界" }, lines.ToArray());
        Assert.AreEqual(text, string.Concat(lines));
        Assert.IsTrue(lines.All(line => Measure(line) <= 48));
    }

    [TestMethod]
    public void ClosingPunctuationStaysWithPreviousCharacter()
    {
        var lines = SourceHelpTextLayout.Wrap("你好，世界。", 24, Measure);
        CollectionAssert.AreEqual(new[] { "你", "好，", "世", "界。" }, lines.ToArray());
    }

    [TestMethod]
    public void OpeningPunctuationStaysWithFollowingCharacter()
    {
        var lines = SourceHelpTextLayout.Wrap("你好（世界）", 36, Measure);
        CollectionAssert.AreEqual(new[] { "你好", "（世", "界）" }, lines.ToArray());
    }

    [TestMethod]
    public void MixedWordsAndLiteralWhitespaceAreNeverDropped()
    {
        const string text = "RED 的宝可梦 HP 12/34";
        var lines = SourceHelpTextLayout.Wrap(text, 48, Measure);
        Assert.AreEqual(text, string.Concat(lines));
        Assert.IsTrue(lines.All(line => Measure(line) <= 48));
        Assert.IsTrue(lines.Any(line => line.Contains("RED", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void SupplementaryAndCombiningCharactersStayWhole()
    {
        var lines = SourceHelpTextLayout.Wrap("𠀀e\u0301中文", 18, Measure);
        CollectionAssert.AreEqual(new[] { "𠀀e\u0301", "中", "文" }, lines.ToArray());
    }

    [TestMethod]
    public void UnfitElementAndUnfitPunctuationFailInsteadOfOverflowing()
    {
        Assert.ThrowsExactly<ArgumentException>(() => SourceHelpTextLayout.Wrap("中文", 11, Measure));
        Assert.ThrowsExactly<ArgumentException>(() => SourceHelpTextLayout.Wrap("（中", 12, Measure));
    }

    [TestMethod]
    public void SourcePageControlsAreNotInterpretedByTheWrappingHelper()
    {
        const string literal = "中文{PLAYER}\\p$";
        Assert.AreEqual(literal, string.Concat(SourceHelpTextLayout.Wrap(literal, 48, Measure)));
    }
}

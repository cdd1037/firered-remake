using System.Globalization;
using Game.Content;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class SummaryFixedWindowTextTests
{
    private static Dictionary<string, string> Source() =>
        PretBattleText.Load(TestAssets.Root, SourceTextPack.Load(null))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    private static float Normal(string text) => StringInfo.ParseCombiningCharacters(text).Length * 12;
    private static float Small(string text) => StringInfo.ParseCombiningCharacters(text).Length * 10;

    [TestMethod]
    [DataRow(5, "AlmostReady", "从里面传出了声音，\\n即将孵化了！")]
    [DataRow(10, "Soon", "偶然会摇动，\\n应该快要孵化了。")]
    [DataRow(40, "SomeTime", "会孵化出什么呢？\\n还需要一段时间。")]
    [DataRow(255, "LongTime", "看起来，这个蛋还\\n需要很久才能孵化。")]
    public void ActualChineseHatchTextFitsWithoutEnglishRowPadding(int friendship, string suffix, string translated)
    {
        var source = Source(); source["gText_PokeSum_EggHatch_" + suffix] = translated;
        var lines = new SourceSummaryText(source, Normal, Small).EggHatchLines(friendship);
        Assert.AreEqual(2, lines.Length);
        Assert.AreEqual(translated.Replace("\\n", "\n", StringComparison.Ordinal), string.Join('\n', lines));
        Assert.IsTrue(lines.All(line => Normal(line) <= 113));
    }

    [TestMethod]
    [DataRow(true, true, false, "NicePlace", "在一个好地方得到\\n的罕见的宝可梦蛋。")]
    [DataRow(false, false, false, "Trade", "通过交换得到的\\n罕见的宝可梦蛋。")]
    [DataRow(false, true, true, "TravelingMan", "在一个旅行者那里得到的\\n不可思议的宝可梦蛋。")]
    [DataRow(false, true, false, "DayCare", "从培育屋夫妇那里发现的\\n不可思议的宝可梦蛋。")]
    public void ActualChineseOriginTextUsesSmallFontAndOriginalBranch(bool fateful, bool currentOwner,
        bool giftEgg, string suffix, string translated)
    {
        var source = Source(); source["gText_PokeSum_EggOrigin_" + suffix] = translated;
        var lines = new SourceSummaryText(source, _ => throw new AssertFailedException("Wrong font."), Small)
            .EggOriginLines(fateful, currentOwner, 4, giftEgg);
        Assert.AreEqual(2, lines.Length);
        Assert.AreEqual(translated.Replace("\\n", "\n", StringComparison.Ordinal), string.Join('\n', lines));
        Assert.IsTrue(lines.All(line => Small(line) <= 224));
    }

    [TestMethod]
    public void EnglishRowsAndExactCountFailuresRemainUnchangedWithoutMeasuring()
    {
        var source = Source();
        var text = new SourceSummaryText(source, _ => throw new AssertFailedException("English measured."),
            _ => throw new AssertFailedException("English measured."));
        Assert.AreEqual(source["gText_PokeSum_EggHatch_AlmostReady"].Replace("\\n", "\n", StringComparison.Ordinal),
            string.Join('\n', text.EggHatchLines(5)));
        source["gText_PokeSum_EggHatch_AlmostReady"] = "ONE\\nTWO";
        Assert.ThrowsExactly<InvalidDataException>(() => new SourceSummaryText(source, Normal, Small));
        source = Source(); source["gText_PokeSum_EggOrigin_DayCare"] = "ONE\\nTWO\\nTHREE";
        Assert.ThrowsExactly<InvalidDataException>(() => new SourceSummaryText(source, Normal, Small));
    }

    [TestMethod]
    [DataRow("中文\\p中文")]
    [DataRow("中文\\l中文")]
    [DataRow("中文{UNKNOWN}")]
    public void ChineseFixedTextDoesNotFlattenSemanticPagesOrUnknownControls(string replacement)
    {
        var source = Source(); source["gText_PokeSum_EggHatch_AlmostReady"] = replacement;
        Assert.ThrowsExactly<InvalidDataException>(() => new SourceSummaryText(source, Normal, Small));
    }

    [TestMethod]
    public void FixedWindowWrapRetainsAllCharactersAndOrdinaryLineBoundaries()
    {
        const string text = "一二三四五六七八九十\n孵化";
        var lines = SourceFixedWindowTextLayout.Create(text, 113, 3, Normal);
        CollectionAssert.AreEqual(new[] { "一二三四五六七八九", "十", "孵化" }, lines.ToArray());
        Assert.AreEqual(text.Replace("\n", "", StringComparison.Ordinal), string.Concat(lines));
        Assert.IsTrue(lines.All(line => Normal(line) <= 113));
    }

    [TestMethod]
    public void UnreadableTextFailsInsteadOfTruncatingOrAddingAHiddenPage()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => SourceFixedWindowTextLayout.Create(new string('蛋', 28), 113, 3, Normal));
        Assert.ThrowsExactly<ArgumentException>(() => SourceFixedWindowTextLayout.Create("蛋", 11, 3, Normal));
        Assert.ThrowsExactly<ArgumentException>(() => SourceFixedWindowTextLayout.Create("蛋\t蛋", 113, 3, Normal));
        Assert.ThrowsExactly<ArgumentException>(() => SourceFixedWindowTextLayout.Create("蛋", 113, 3, _ => float.NaN));
        var source = Source(); source["gText_PokeSum_EggHatch_AlmostReady"] = new string('蛋', 28);
        Assert.ThrowsExactly<InvalidDataException>(() => new SourceSummaryText(source, Normal, Small));
        source["gText_PokeSum_EggHatch_AlmostReady"] = "蛋";
        Assert.ThrowsExactly<InvalidDataException>(() => new SourceSummaryText(source));
    }
}

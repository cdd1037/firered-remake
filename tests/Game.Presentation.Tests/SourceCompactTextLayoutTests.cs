using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class SourceCompactTextLayoutTests
{
    [TestMethod]
    public void FittingLatinAndEmptyLabelsKeepIdentityScale()
    {
        Assert.AreEqual(1f, SourceCompactTextLayout.FitScale(48, 52));
        Assert.AreEqual(1f, SourceCompactTextLayout.FitScale(52, 52));
        Assert.AreEqual(1f, SourceCompactTextLayout.FitScale(0, 52));
        Assert.AreEqual(0, SourceCompactTextLayout.ScrollOffset(48, 52, 997));
    }

    [TestMethod]
    public void TenNormalChineseGlyphsFitTheActionWindowWithOnlyMildCompression()
    {
        float scale = SourceCompactTextLayout.FitScale(10 * 12, 110);
        Assert.AreEqual(110f / 120, scale);
        Assert.IsGreaterThan(.9f, scale);
        Assert.AreEqual(110f, 120 * scale, .0001f);
    }

    [TestMethod]
    public void LongHealthNameScrollsAtNativeSizeWithHeldEndpoints()
    {
        Assert.AreEqual(0, SourceCompactTextLayout.ScrollOffset(110, 50, 0));
        Assert.AreEqual(0, SourceCompactTextLayout.ScrollOffset(110, 50, 89));
        Assert.AreEqual(1, SourceCompactTextLayout.ScrollOffset(110, 50, 93));
        Assert.AreEqual(60, SourceCompactTextLayout.ScrollOffset(110, 50, 270));
        Assert.AreEqual(60, SourceCompactTextLayout.ScrollOffset(110, 50, 359));
        Assert.AreEqual(59, SourceCompactTextLayout.ScrollOffset(110, 50, 363));
        Assert.AreEqual(0, SourceCompactTextLayout.ScrollOffset(110, 50, 540));
    }

    [TestMethod]
    public void EveryPixelOfLongNameBecomesVisibleWithoutOvershoot()
    {
        int[] offsets = Enumerable.Range(0, 541).Select(t => SourceCompactTextLayout.ScrollOffset(110, 50, t)).ToArray();
        Assert.AreEqual(0, offsets.Min());
        Assert.AreEqual(60, offsets.Max());
        CollectionAssert.AreEqual(Enumerable.Range(0, 61).ToArray(), offsets.Distinct().Order().ToArray());
    }

    [TestMethod]
    public void FractionalWidthsStillExposeLastGlyphAndDoNotAccumulateDrift()
    {
        Assert.AreEqual(1, SourceCompactTextLayout.ScrollOffset(50.25f, 50, 94));
        Assert.AreEqual(SourceCompactTextLayout.ScrollOffset(50.25f, 50, 94),
            SourceCompactTextLayout.ScrollOffset(50.25f, 50, 94 + 186 * 1000));
    }

    [TestMethod]
    public void NativeNicknameEditorPreservesOriginalLatinPlacement()
    {
        var result = SourceCompactTextLayout.NameEntry(60, 80, 86, 168, 5);
        Assert.AreEqual(80f, result.SourceWidth);
        Assert.AreEqual(80f, result.DisplayWidth);
        Assert.AreEqual(1f, result.Scale);
        Assert.AreEqual(168f, result.GenderX);
    }

    [TestMethod]
    public void NativeChineseNicknameUsesSparePanelSpaceBeforeScaling()
    {
        var result = SourceCompactTextLayout.NameEntry(120, 80, 86, 168, 6);
        Assert.AreEqual(120f, result.SourceWidth);
        Assert.AreEqual(106f, result.DisplayWidth, .0001f);
        Assert.IsGreaterThan(.88f, result.Scale);
        Assert.AreEqual(194f, result.GenderX, .0001f);
        Assert.IsLessThanOrEqualTo(200f, result.GenderX + 6);
    }

    [TestMethod]
    public void SevenTrainerAndEightBoxChineseNamesDoNotNeedScaling()
    {
        var trainer = SourceCompactTextLayout.NameEntry(7 * 12, 7 * 8, 98, 168, 0);
        var box = SourceCompactTextLayout.NameEntry(8 * 12, 8 * 8, 94, 168, 0);
        Assert.AreEqual(1f, trainer.Scale);
        Assert.AreEqual(84f, trainer.DisplayWidth);
        Assert.AreEqual(1f, box.Scale);
        Assert.AreEqual(96f, box.DisplayWidth);
    }

    [TestMethod]
    public void ExcessivePreeditKeepsNativeScrollingInsteadOfUnreadableCompression()
    {
        var result = SourceCompactTextLayout.NameEntry(1000, 80, 86, 168, 6);
        Assert.AreEqual(.85f, result.Scale, .0001f);
        Assert.AreEqual(106f, result.DisplayWidth, .0001f);
        Assert.IsLessThan(1000f, result.SourceWidth);
    }

    [TestMethod]
    public void InvalidWidthsAndNegativeClockFailExplicitly()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => SourceCompactTextLayout.FitScale(float.NaN, 110));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => SourceCompactTextLayout.FitScale(20, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => SourceCompactTextLayout.ScrollOffset(110, 50, -1));
    }
}

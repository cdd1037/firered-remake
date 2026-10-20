using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class SourceNameGlyphTests
{
    private static bool Available(int codepoint) =>
        codepoint is 'A' or 'e' or 0x5B9D or 0x20BB7;

    [TestMethod]
    public void MappedSupplementaryHanIsOneSupportedScalar()
    {
        Assert.IsTrue(SourceNameGlyphs.CanRender("宝𠮷A", Available));
    }

    [TestMethod]
    public void ValidDomainEmojiDoesNotImplyRendererSupport()
    {
        Assert.IsTrue(Game.Domain.Pokemon.NameTextRules.IsValidName("😀", 10));
        Assert.IsFalse(SourceNameGlyphs.CanRender("😀", Available));
    }

    [TestMethod]
    public void CombiningTextMustHaveEveryExactGlyphWithoutNormalization()
    {
        const string text = "e\u0301";
        Assert.IsFalse(SourceNameGlyphs.CanRender(text, Available));
        Assert.IsTrue(SourceNameGlyphs.CanRender(text, cp => Available(cp) || cp == 0x0301));
    }

    [TestMethod]
    public void InvalidSurrogateCannotBecomeAReplacementCharacter()
    {
        Assert.IsFalse(SourceNameGlyphs.CanRender("\uD800", _ => true));
        Assert.IsFalse(SourceNameGlyphs.CanRender("\uDC00", _ => true));
    }

    [TestMethod]
    public void EmptyInputStillReachesExistingKeepOriginalBehavior()
    {
        Assert.IsTrue(SourceNameGlyphs.CanRender("", _ => false));
    }

    [TestMethod]
    public void FullTenElementNameIsNeverShortenedToFit()
    {
        string text = string.Concat(Enumerable.Repeat("𠮷", 10));
        Assert.IsTrue(SourceNameGlyphs.CanRender(text, Available));
        Assert.AreEqual(20, text.Length);
        Assert.AreEqual(10, Game.Domain.Pokemon.NameTextRules.TextElementCount(text));
    }
}

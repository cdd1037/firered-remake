using System.Text.Json;

namespace FireRed.Tests.Presentation;

[TestClass]
public sealed class BrailleTextPresentationTests
{
    [TestMethod]
    [DataRow("ABC", "Braille_Text_ABC", true)]
    [DataRow("NEW WORDS", "Braille_Text_ABC", true)]
    [DataRow("ABC", "unknown", false)]
    [DataRow("", "Braille_Text_ABC", false)]
    [DataRow("lowercase", "Braille_Text_ABC", false)]
    [DataRow("A\nB", "Braille_Text_ABC", false)]
    public void StoryTextOwnsWordsWhileSourceLabelAndAvailableGlyphsRemainRequired(string text, string symbol, bool valid)
    {
        using var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestAssets.Root,
            SeviiRubyPresentationAssets.Base, "manifest.json")));
        var braille = source.RootElement.GetProperty("braille");
        var message = braille.GetProperty("messages").EnumerateArray().Single(row =>
            row.GetProperty("label").GetString() == "Braille_Text_ABC");
        if (valid) Assert.AreEqual(text, SeviiRubyPresentationAssets.BrailleText(braille, message, symbol, text));
        else Assert.ThrowsExactly<InvalidDataException>(() =>
            SeviiRubyPresentationAssets.BrailleText(braille, message, symbol, text));
        Assert.AreEqual("ABC", message.GetProperty("text").GetString());
    }
}

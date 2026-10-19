using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class PresentationTextCatalogTests
{
    [TestMethod]
    public void NamedArgumentsCanReorderAndRepeatWithoutReinterpretingPlayerText()
    {
        var text = PresentationTextCatalog.Parse("""{"notice":"{Action}: {PlayerName}; {Action}"}""");
        Assert.AreEqual("SAVE: {Action}; SAVE", text.Get("notice", ("PlayerName", "{Action}"), ("Action", "SAVE")));
    }

    [TestMethod]
    public void MissingDuplicateAndUnexpectedArgumentsFailInsteadOfShowingBrokenText()
    {
        var text = PresentationTextCatalog.Parse("""{"notice":"{PlayerName} saved."}""");
        Assert.ThrowsExactly<ArgumentException>(() => text.Get("notice"));
        Assert.ThrowsExactly<ArgumentException>(() => text.Get("notice", ("PlayerName", "RED"), ("Extra", "x")));
        Assert.ThrowsExactly<ArgumentException>(() => text.Get("notice", ("PlayerName", "RED"), ("PlayerName", "BLUE")));
        Assert.ThrowsExactly<InvalidDataException>(() => text.Get("unknown"));
    }

    [TestMethod]
    public void EmptyAndMalformedCatalogsAreRejected()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => PresentationTextCatalog.Parse("{}"));
        Assert.ThrowsExactly<InvalidDataException>(() => PresentationTextCatalog.Parse("""{"notice":" "}"""));
        Assert.ThrowsExactly<InvalidDataException>(() => PresentationTextCatalog.Parse("""{"notice":"{PAUSE 60}"}"""));
    }

    [TestMethod]
    public void EnglishSaveMessagesShipInsideContentAssembly()
    {
        var text = PresentationTextCatalog.English("save");
        Assert.AreEqual("RED saved the game.", text.Get("success.save", ("PlayerName", "RED")));
        Assert.AreEqual("Loaded the verified backup.\nSave files were not changed.", text.Get("success.backup"));
        Assert.ThrowsExactly<InvalidDataException>(() => PresentationTextCatalog.English("missing"));
    }
}

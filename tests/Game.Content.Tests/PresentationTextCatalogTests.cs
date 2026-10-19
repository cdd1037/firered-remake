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
    public void LiteralLabelsReturnTheirTextButStillRejectUnexpectedParameters()
    {
        var text = PresentationTextCatalog.Parse("""{"label":"CANCEL"}""");
        Assert.AreEqual("CANCEL", text.Get("label"));
        Assert.ThrowsExactly<ArgumentException>(() => text.Get("label", ("Extra", "ignored")));
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

    [TestMethod]
    public void FieldMenuMessagesPreserveEnglishAndExistingLineBreaks()
    {
        var text = PresentationTextCatalog.English("field-menu");
        (string Id, string Expected)[] messages =
        [
            ("start.safari-retire", "RETIRE"),
            ("start.pokedex", "POKéDEX"),
            ("start.pokemon", "POKéMON"),
            ("start.bag", "BAG"),
            ("start.save", "SAVE"),
            ("start.option", "OPTION"),
            ("start.exit", "EXIT"),
            ("notice.switch-needs-party", "At least two POKéMON are\nneeded to SWITCH positions."),
            ("notice.switch-failed", "The party order could not\nbe changed here."),
            ("notice.cut-badge-required", "This can't be used until a new\nBADGE is obtained."),
            ("notice.cut-nothing", "There's nothing to CUT."),
            ("notice.cut-failed", "CUT could not be used here."),
            ("notice.cannot-use-here", "Can't use that here."),
            ("notice.party-empty", "You have no POKéMON\nin your party."),
            ("notice.bag-unavailable", "The BAG cannot be opened\nhere."),
            ("prompt.save-overwrite", "A save already exists.\nReplace it with this game?"),
            ("prompt.save", "Would you like to\nsave the game?"),
            ("choice.yes", "YES"),
            ("choice.no", "NO"),
            ("prompt.party-heal", "Use on which POKéMON?"),
            ("prompt.party-switch", "Move to which position?"),
            ("prompt.party-action", "Do what with this POKéMON?"),
            ("prompt.party-choose", "Choose a POKéMON.")
        ];
        foreach (var (id, expected) in messages) Assert.AreEqual(expected, text.Get(id), id);
    }

    [TestMethod]
    public void OptionsMessagesPreserveEnglishAndInputHelpSpacing()
    {
        var text = PresentationTextCatalog.English("options");
        (string Id, string Expected)[] messages =
        [
            ("title", "OPTION"),
            ("notice.unavailable", "Options cannot be changed\nhere."),
            ("notice.presentation-unavailable", "This option's presentation\nis not available yet."),
            ("notice.apply-failed", "Options could not be applied.\nReturn to a stable field."),
            ("input.help", "UP/DOWN PICK  LEFT/RIGHT SWITCH  A/B BACK")
        ];
        foreach (var (id, expected) in messages) Assert.AreEqual(expected, text.Get(id), id);
    }

    [TestMethod]
    public void PartialPackOverridesOnlyRequestedEntriesAndRetainsEnglishFallback()
    {
        using var pack = TestAssets.TemporaryOutput();
        File.WriteAllText(Path.Combine(pack.Path, "save.json"),
            """{"success.save":"Saved by {PlayerName}: {PlayerName}."}""");
        var text = PresentationTextCatalog.Load("save", pack.Path);
        Assert.AreEqual("Saved by {Action}: {Action}.", text.Get("success.save", ("PlayerName", "{Action}")));
        Assert.AreEqual(PresentationTextCatalog.English("save").Get("success.load"), text.Get("success.load"));
        Assert.AreEqual("OPTION", PresentationTextCatalog.Load("options", pack.Path).Get("title"));
        File.WriteAllText(Path.Combine(pack.Path, "save.json"), "{}");
        Assert.AreEqual("RED saved the game.", PresentationTextCatalog.Load("save", pack.Path).Get("success.save", ("PlayerName", "RED")));
        Assert.AreEqual("RED saved the game.", PresentationTextCatalog.Load("save").Get("success.save", ("PlayerName", "RED")));
    }

    [TestMethod]
    [DataRow("save", "{\"unknown\":\"No such message\"}")]
    [DataRow("save", "{\"success.save\":\"Saved.\"}")]
    [DataRow("save", "{\"success.save\":\"{PlayerName} {Extra}\"}")]
    [DataRow("save", "{\"success.save\":\"{PLAYERNAME}\"}")]
    [DataRow("save", "{\"success.load\":\" \"}")]
    [DataRow("save", "{\"success.load\":\"{PAUSE 60}\"}")]
    [DataRow("unknown-group", "{}")]
    public void InvalidOverridesFailInsteadOfSilentlyFallingBack(string group, string json)
    {
        using var pack = TestAssets.TemporaryOutput();
        File.WriteAllText(Path.Combine(pack.Path, group + ".json"), json);
        Assert.ThrowsExactly<InvalidDataException>(() => PresentationTextCatalog.Load("save", pack.Path));
    }

    [TestMethod]
    public void DuplicateOverrideIdsKeepTheSharedStrictJsonError()
    {
        using var pack = TestAssets.TemporaryOutput();
        File.WriteAllText(Path.Combine(pack.Path, "save.json"), """{"success.load":"a","success.load":"b"}""");
        var error = Assert.ThrowsExactly<ContentValidationException>(() => PresentationTextCatalog.Load("save", pack.Path));
        StringAssert.Contains(error.Message, "duplicate_property");
    }

    [TestMethod]
    public void ExplicitMissingPackFailsWithoutCreatingFiles()
    {
        using var root = TestAssets.TemporaryOutput();
        string missing = Path.Combine(root.Path, "missing");
        Assert.ThrowsExactly<DirectoryNotFoundException>(() => PresentationTextCatalog.Load("options", missing));
        Assert.IsFalse(Directory.Exists(missing));
    }

}

using System.Text.Json;
using Game.Content;
using Game.Domain.Events;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class SharedUiTextPresentationTests
{
    private static IReadOnlyDictionary<string, string> Source =>
        TestAssets.Cached(() => PretBattleText.Load(TestAssets.Root));

    [TestMethod]
    [DataRow("gText_Yes", "YES")]
    [DataRow("gText_No", "NO")]
    [DataRow("gText_CantUseHere", "Can't use that here.")]
    [DataRow("gText_RestoreWhichMove", "Restore which move?")]
    [DataRow("sText_AllyPkmnPrefix2", "Ally")]
    [DataRow("sText_FoePkmnPrefix3", "Foe")]
    [DataRow("gText_Coins_2", "COINS")]
    [DataRow("gText_NowOn", "Now on:")]
    [DataRow("gText_B1F", "B1F")]
    [DataRow("gText_B2F", "B2F")]
    [DataRow("gText_B4F", "B4F")]
    [DataRow("gText_1F", "1F")]
    [DataRow("gText_11F", "11F")]
    [DataRow("gText_5F_2", "5F")]
    [DataRow("gText_1F_2", "1F")]
    [DataRow("gOtherText_Exit", "EXIT")]
    [DataRow("gOtherText_Check", "CHECK")]
    [DataRow("gText_NoUse", "No use.")]
    [DataRow("gText_UseOnWhichPokemon", "Use on which POKéMON?")]
    public void SharedLabelsUseRealSourceSymbolsAndSparseOverrides(string symbol, string expected)
    {
        Assert.AreEqual(expected, SourceMenuLabelPresentation.Plain(Source, symbol));
        using var pack = TestAssets.TemporaryOutput();
        Directory.CreateDirectory(Path.Combine(pack.Path, "source"));
        File.WriteAllText(Path.Combine(pack.Path, "source", "battle.json"),
            JsonSerializer.Serialize(new Dictionary<string, string> { [symbol] = "LOCAL" }));
        var changed = PretBattleText.Load(TestAssets.Root, SourceTextPack.Load(pack.Path));
        Assert.AreEqual("LOCAL", SourceMenuLabelPresentation.Plain(changed, symbol));
        Assert.AreEqual(Source["gText_MoveInterfacePP"], changed["gText_MoveInterfacePP"]);
    }

    [TestMethod]
    [DataRow("gText_SafariBalls", "HIGHLIGHT 2", "SAFARI BALLS")]
    [DataRow("gText_HighlightRed_Left", "HIGHLIGHT 2", "Left: ")]
    [DataRow("gText_Cry", "START_BUTTON", "CRY")]
    public void SharedHudAndControlLabelsConsumeOnlyTheirKnownSourceControl(string symbol, string control, string expected)
    {
        Assert.AreEqual(expected, SourceMenuLabelPresentation.Plain(Source, symbol,
            new Dictionary<string, string> { [control] = "" }));
        Assert.ThrowsExactly<InvalidDataException>(() => SourceMenuLabelPresentation.Plain(Source, symbol));
    }

    [TestMethod]
    public void SourceCountersUseExistingTemplatesWithoutReinterpretingBoundValues()
    {
        Assert.AreEqual("IN BAG: 7", SourceMenuLabelPresentation.Plain(Source, "gText_InBagVar1",
            new Dictionary<string, string> { ["FONT_SMALL"] = "", ["STR_VAR_1"] = "7" }));
        Assert.AreEqual("  50 COINS", SourceMenuLabelPresentation.Plain(Source, "gText_Coins",
            new Dictionary<string, string> { ["STR_VAR_1"] = "  50" }));
        var stats = SourceBattleText.Pages(Source, "gText_MenuSafariStats", new Dictionary<string, string>
            { ["STR_VAR_1"] = "123", ["STR_VAR_2"] = "600", ["STR_VAR_3"] = " 7" });
        Assert.AreEqual("123/600\nBALLS   7", stats.Single());
        var literal = SourceBattleText.Pages(Source, "gText_MenuSafariStats", new Dictionary<string, string>
            { ["STR_VAR_1"] = "{STR_VAR_3}", ["STR_VAR_2"] = "600", ["STR_VAR_3"] = @"\p7" });
        Assert.AreEqual("{STR_VAR_3}/600\nBALLS  \\p7", literal.Single());
    }

    [TestMethod]
    [DataRow("field-menu", "naming.notice.length", "Use at most 10 characters.", "limit", "10")]
    [DataRow("field-menu", "pokedex.control.search", "R SEARCH", "search", "SEARCH")]
    [DataRow("inventory", "notice.used", "POTION was used.", "item", "POTION")]
    public void AuthoredUiUsesExistingGroupsAndOnePassNamedBindings(string group, string id,
        string expected, string parameter, string value)
    {
        var english = PresentationTextCatalog.English(group);
        Assert.AreEqual(expected, english.Get(id, (parameter, value)));
        using var pack = TestAssets.TemporaryOutput();
        File.WriteAllText(Path.Combine(pack.Path, group + ".json"),
            JsonSerializer.Serialize(new Dictionary<string, string> { [id] = "{" + parameter + "} / {" + parameter + "}" }));
        var changed = PresentationTextCatalog.Load(group, pack.Path);
        Assert.AreEqual("{other} / {other}", changed.Get(id, (parameter, "{other}")));
        Assert.AreEqual(english.Get(group == "field-menu" ? "party.summary" : "action.deposit"),
            changed.Get(group == "field-menu" ? "party.summary" : "action.deposit"));
    }

    [TestMethod]
    [DataRow("field-menu", "safari.notice.save-load", "You can't save or load\nduring a SAFARI GAME.")]
    [DataRow("field-menu", "safari.notice.action-unavailable", "That action is not available\nright now.")]
    [DataRow("field-menu", "naming.hint.back", "Tab: type/keys  Enter: OK  Esc: back")]
    [DataRow("field-menu", "naming.hint.keep", "Tab: type/keys  Enter: OK  Esc: keep")]
    [DataRow("field-menu", "naming.hint.delete", "Tab: type/keys  Enter: OK  B: delete")]
    [DataRow("field-menu", "naming.notice.failed", "That name could not be submitted.")]
    [DataRow("field-menu", "pokedex.control.pick-enter", "PICK / ENTER")]
    [DataRow("field-menu", "pokedex.control.previous", "PREV")]
    [DataRow("field-menu", "pokedex.control.search-numerical", "SEARCH / NUMERICAL")]
    [DataRow("field-menu", "pokedex.control.select", "SELECT")]
    [DataRow("field-menu", "pokedex.control.pick-page", "PICK / PAGE")]
    [DataRow("field-menu", "pokedex.control.data", "DATA")]
    [DataRow("field-menu", "pokedex.title.data-area", "DATA / ENTER AREA")]
    [DataRow("field-menu", "pokedex.notice.empty", "NO ENTRIES")]
    [DataRow("inventory", "notice.berry-data-unavailable", "This BERRY's effect data\nis not available.")]
    [DataRow("inventory", "notice.battle-use-unavailable", "This item can't be used\nin battle.")]
    [DataRow("inventory", "notice.none-left", "There are none left\nin the BAG.")]
    [DataRow("inventory", "notice.trainer-escape", "You can't flee from a\nTRAINER battle!")]
    [DataRow("inventory", "notice.choose-target-again", "Choose the target again.")]
    [DataRow("inventory", "notice.double-use-unavailable", "This item's double-battle\nuse is not available yet.")]
    [DataRow("inventory", "notice.item-unavailable", "That item can't be used\nright now.")]
    public void AuthoredNavigationAndNoticesKeepExistingEnglishAndAllowSparseReplacement(string group, string id, string expected)
    {
        Assert.AreEqual(expected, PresentationTextCatalog.English(group).Get(id));
        using var pack = TestAssets.TemporaryOutput();
        File.WriteAllText(Path.Combine(pack.Path, group + ".json"),
            JsonSerializer.Serialize(new Dictionary<string, string> { [id] = "LOCAL" }));
        Assert.AreEqual("LOCAL", PresentationTextCatalog.Load(group, pack.Path).Get(id));
    }

    [TestMethod]
    public void PokedexHeaderCanReorderLabelsCountsAndLiteralDisplayNames()
    {
        using var pack = TestAssets.TemporaryOutput();
        File.WriteAllText(Path.Combine(pack.Path, "field-menu.json"),
            """{"pokedex.header":"{ownedCount} {owned}; {seenCount} {seen}; {title}"}""");
        var text = PresentationTextCatalog.Load("field-menu", pack.Path);
        Assert.AreEqual("20 Owned:; 42 Seen:; {owned}", text.Get("pokedex.header", ("title", "{owned}"),
            ("seen", "Seen:"), ("seenCount", "42"), ("owned", "Owned:"), ("ownedCount", "20")));
    }
}

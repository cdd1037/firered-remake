using System.Text.Json;
using Game.Content;
using Game.Domain.Events;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class RuntimeFallbackTextTests
{
    [TestMethod]
    [DataRow("field-menu", "naming.notice.character-unavailable", "That character is not\navailable in this game.")]
    [DataRow("inventory", "notice.no-poke-balls", "There are no POKé BALLS\nin the BAG.")]
    [DataRow("inventory", "notice.no-potions", "There are no POTIONS\nin the BAG.")]
    [DataRow("inventory", "notice.capture-unavailable", "This capture is not supported.\nNo BALL was used.")]
    [DataRow("storage", "notice.capture-delivery-unavailable", "Your party has six POKéMON.\nPC delivery is not available yet.")]
    [DataRow("summary", "notice.learning-unsupported", "Effect not implemented.\nChoose CANCEL to continue.")]
    [DataRow("summary", "notice.battle-move-disabled", "That move is disabled.")]
    [DataRow("summary", "notice.battle-move-must-finish", "The current move must\nfinish first.")]
    [DataRow("summary", "notice.battle-move-effect-unsupported", "This move's effect is not\nimplemented yet.")]
    [DataRow("summary", "notice.battle-opponent-move-effect-unsupported", "The wild move's effect is\nnot implemented yet.")]
    [DataRow("summary", "notice.battle-replacement-required", "Choose a living POKéMON\nthat is not already in battle.")]
    public void RuntimeNoticesUseExistingAuthoredGroupsAndSparseOverrides(string group, string id, string english)
    {
        Assert.AreEqual(english, PresentationTextCatalog.English(group).Get(id));
        using var pack = TestAssets.TemporaryOutput();
        File.WriteAllText(Path.Combine(pack.Path, group + ".json"),
            JsonSerializer.Serialize(new Dictionary<string, string> { [id] = "中文提示" }));
        Assert.AreEqual("中文提示", PresentationTextCatalog.Load(group, pack.Path).Get(id));
    }

    [TestMethod]
    [DataRow("TACKLE", "GROWL", "Forget TACKLE\nand learn GROWL?")]
    [DataRow("{newMove}", @"\p", "Forget {newMove}\nand learn \\p?")]
    public void MoveReplacementConfirmationBindsBothNamesOnce(string oldMove, string newMove, string expected)
    {
        var english = PresentationTextCatalog.English("summary");
        Assert.AreEqual(expected, english.Get("prompt.learning-replace", ("forgottenMove", oldMove), ("newMove", newMove)));
        using var pack = TestAssets.TemporaryOutput();
        File.WriteAllText(Path.Combine(pack.Path, "summary.json"),
            """{"prompt.learning-replace":"学习{newMove}，忘记{forgottenMove}。"}""");
        Assert.AreEqual("学习" + newMove + "，忘记" + oldMove + "。",
            PresentationTextCatalog.Load("summary", pack.Path).Get("prompt.learning-replace", ("forgottenMove", oldMove), ("newMove", newMove)));
    }

    [TestMethod]
    [DataRow("gText_ChoosePokemon2", "Choose a POKéMON.")]
    [DataRow("gText_NotEnoughHp", "Not enough HP‥")]
    [DataRow("gText_UseOnWhichPokemon", "Use on which POKéMON?")]
    [DataRow("gText_Yes", "YES")]
    [DataRow("gText_No", "NO")]
    public void SharedSelectionAndHealingLabelsUseExistingSourceDecoder(string symbol, string english)
    {
        var original = PretBattleText.Load(TestAssets.Root, SourceTextPack.Load(null));
        Assert.AreEqual(english, SourceMenuLabelPresentation.Plain(original, symbol));
        using var pack = TestAssets.TemporaryOutput();
        Directory.CreateDirectory(Path.Combine(pack.Path, "source"));
        File.WriteAllText(Path.Combine(pack.Path, "source/battle.json"),
            JsonSerializer.Serialize(new Dictionary<string, string> { [symbol] = "中文" }));
        var translated = PretBattleText.Load(TestAssets.Root, SourceTextPack.Load(pack.Path));
        Assert.AreEqual("中文", SourceMenuLabelPresentation.Plain(translated, symbol));
    }

    [TestMethod]
    [DataRow("gText_EscapeFromHereAndReturnTo", "Want to escape from here and return\nto {literal}?")]
    [DataRow("gText_ReturnToHealingSpot", "Want to return to the healing spot\nused last in {literal}?")]
    [DataRow("gText_PkmnCantSwitchOut", "{literal} can't be switched\nout!")]
    [DataRow("gText_PkmnHPRestoredByVar2", "{literal}'s HP was restored\nby 5 point(s).")]
    public void FieldAndBattleSourceTemplatesBindDisplayValuesWithoutInterpretingThem(string symbol, string english)
    {
        var original = PretBattleText.Load(TestAssets.Root, SourceTextPack.Load(null));
        var values = new Dictionary<string, string> { ["STR_VAR_1"] = "{literal}", ["STR_VAR_2"] = "5" };
        Assert.AreEqual(english, string.Join('\n', SourceBattleText.Pages(original, symbol, values)));
    }
}

using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;

namespace FireRed.Tests.Presentation;

[TestClass]
public sealed class FacilitySourceLabelTests
{
    [TestMethod]
    [DataRow("gText_RegionMap_DPadMove", "DPAD_ANY", "MOVE")]
    [DataRow("gText_RegionMap_UpDownPick", "DPAD_UPDOWN", "PICK")]
    [DataRow("gText_RegionMap_AButtonCancel", "A_BUTTON", "CANCEL")]
    [DataRow("gText_RegionMap_AButtonSwitch", "A_BUTTON", "SWITCH")]
    [DataRow("gText_RegionMap_AButtonOK", "A_BUTTON", "OK")]
    public void MapControlLabelsUseCanonicalBattleSymbolsAndExistingControlDecoder(string symbol, string control, string english)
    {
        var originals = PretBattleText.Load(TestAssets.Root, SourceTextPack.Load(null));
        var values = ImmutableDictionary<string, string>.Empty.Add(control, "");
        Assert.AreEqual(english, SourceMenuLabelPresentation.Plain(originals, symbol, values));
        using var output = TestAssets.TemporaryOutput();
        Directory.CreateDirectory(Path.Combine(output.Path, "source"));
        File.WriteAllText(Path.Combine(output.Path, "source/battle.json"), JsonSerializer.Serialize(
            new Dictionary<string, string> { [symbol] = "{" + control + "}SELECT" }));
        Assert.AreEqual("SELECT", SourceMenuLabelPresentation.Plain(
            PretBattleText.Load(TestAssets.Root, SourceTextPack.Load(output.Path)), symbol, values));
    }

    [TestMethod]
    [DataRow("MULTICHOICE_YES_NO_INFO", 3)]
    [DataRow("MULTICHOICE_TRAINER_TOWER_MODE", 5)]
    [DataRow("MULTICHOICE_ROOFTOP_B1F", 3)]
    public void TowerFallbackMenusKeepRowIdentityWithCollidingLabels(string symbol, int count)
    {
        var menu = new TrainerTowerMenuPresentation(symbol, Enumerable.Repeat("SAME", count));
        Assert.IsTrue(menu.Select(count - 1));
        Assert.AreEqual(count - 1, menu.Cursor);
        Assert.IsTrue(menu.Select(0));
        Assert.AreEqual(0, menu.Cursor);
        Assert.ThrowsExactly<InvalidDataException>(() => new TrainerTowerMenuPresentation(symbol, ["SAME"]));
        Assert.ThrowsExactly<InvalidDataException>(() => new TrainerTowerMenuPresentation(symbol, Enumerable.Repeat(" ", count)));
    }

    [TestMethod]
    [DataRow(3, "gText_B1F", "B1F")]
    [DataRow(15, "gText_Rooftop", "ROOFTOP")]
    public void TowerFloorIdentitySelectsSourceText(int floor, string symbol, string expected)
    {
        Assert.AreEqual(symbol, TrainerTowerMenuPresentation.ElevatorFloorTextSymbol(floor));
        Assert.AreEqual(expected, SourceMenuLabelPresentation.Plain(
            PretBattleText.Load(TestAssets.Root, SourceTextPack.Load(null)), symbol));
        Assert.ThrowsExactly<InvalidDataException>(() => TrainerTowerMenuPresentation.ElevatorFloorTextSymbol(99));
    }
}

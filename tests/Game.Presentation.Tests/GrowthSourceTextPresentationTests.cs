using Game.Content;

namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Presentation")]
public sealed class GrowthSourceTextPresentationTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExistingSourceBankSuppliesAllStatLabelsAndEvolutionControl(bool translated)
    {
        using var directory=TestAssets.TemporaryOutput();
        Directory.CreateDirectory(Path.Combine(directory.Path,"source"));
        File.WriteAllText(Path.Combine(directory.Path,"source/battle.json"),translated ? """
            {"gText_LevelUp_MaxHP":"{FONT_SMALL}TOTAL{FONT_NORMAL} HP",
             "gText_LevelUp_Attack":"POWER","gText_LevelUp_Defense":"GUARD",
             "gText_LevelUp_SpAtk":"MIND","gText_LevelUp_SpDef":"WILL","gText_LevelUp_Speed":"PACE",
             "gText_BButtonCancel":"{B_BUTTON}STOP"}
            """ : "{}");
        var source=PretBattleText.Load(TestAssets.Root,SourceTextPack.Load(directory.Path))
            .ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        var text=new SourceGrowthText(source);
        CollectionAssert.AreEqual(translated
                ? new[]{"TOTAL HP","POWER","GUARD","MIND","WILL","PACE"}
                : new[]{"MAX. HP","ATTACK","DEFENSE","SP. ATK","SP. DEF","SPEED"},
            text.StatLabels.ToArray());
        Assert.AreEqual("B/Backspace: "+(translated?"STOP":"CANCEL"),text.EvolutionCancel);
        source["gText_LevelUp_Attack"]="CHANGED";
        source["gText_BButtonCancel"]="{UNKNOWN}";
        Assert.AreEqual(translated?"POWER":"ATTACK",text.StatLabels[1]);
        Assert.AreEqual("B/Backspace: "+(translated?"STOP":"CANCEL"),text.EvolutionCancel);
    }

    [TestMethod]
    [DataRow("gText_LevelUp_Attack",null)]
    [DataRow("gText_LevelUp_Speed","")]
    [DataRow("gText_LevelUp_Defense","FIRST\\nSECOND")]
    [DataRow("gText_LevelUp_SpAtk","{UNKNOWN}POWER")]
    [DataRow("gText_LevelUp_MaxHP","MAX. HP")]
    [DataRow("gText_BButtonCancel","{A_BUTTON}CANCEL")]
    [DataRow("gText_BButtonCancel","CANCEL")]
    public void MissingLabelsAndUnsupportedControlsFailAtComposition(string symbol,string? value)
    {
        var source=PretBattleText.Load(TestAssets.Root,SourceTextPack.Load(null))
            .ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        if(value is null)source.Remove(symbol);else source[symbol]=value;
        Assert.ThrowsExactly<InvalidDataException>(()=>new SourceGrowthText(source));
    }
}

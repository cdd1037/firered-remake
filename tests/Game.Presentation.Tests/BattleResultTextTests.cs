namespace FireRed.Tests.Presentation;

[TestClass]
public sealed class BattleResultTextTests
{
    [TestMethod]
    [DataRow("{FONT_NORMAL}")]
    [DataRow("{PLAY_BGM}{MUS_ENCOUNTER_GYM_LEADER}")]
    [DataRow("{PLAYER}\\p$")]
    public void FinalSingleDoubleAndTowerPagesOnlyScrollWithoutReinterpretingTheirWords(string literal)
    {
        Assert.AreSequenceEqual(new[] { literal }, SourceTrainerPresentationModel.ScrollResultPages(literal));
        Assert.AreSequenceEqual(new[] { "First\n" + literal, literal + "\nLast" },
            SourceTrainerPresentationModel.ScrollResultPages("First\n" + literal + "\nLast"));
    }
}

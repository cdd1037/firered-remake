using Game.Domain.Options;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class OptionEditingTests
{
    [TestMethod]
    [DataRow(-1)]
    [DataRow(1)]
    public void SceneAndSoundToggleBothWaysWithoutChangingOtherDraftFields(int direction)
    {
        var original = SourceGameOptions.Defaults with { TextSpeed = SourceTextSpeed.Fast, Frame = 7 };
        Assert.IsTrue(CampaignOptionEditing.IsEditable(1, false));
        Assert.IsTrue(CampaignOptionEditing.IsEditable(3, false));
        var scene = CampaignOptionEditing.Change(original, 1, direction, false);
        Assert.AreEqual(original with { BattleScene = SourceBattleScene.Off }, scene);
        Assert.AreEqual(original, CampaignOptionEditing.Change(scene, 1, direction, false));
        var sound = CampaignOptionEditing.Change(original, 3, direction, false);
        Assert.AreEqual(original with { Sound = SourceSound.Stereo }, sound);
        Assert.AreEqual(original, CampaignOptionEditing.Change(sound, 3, direction, false));
        Assert.AreEqual(SourceBattleScene.On, original.BattleScene);
        Assert.AreEqual(SourceSound.Mono, original.Sound);
    }

    [TestMethod]
    public void CancelStaysUnavailableAndFramesStillRequireAssets()
    {
        foreach (int row in new[] { -1, 6, 7 })
        {
            Assert.IsFalse(CampaignOptionEditing.IsEditable(row, true));
            Assert.AreSame(SourceGameOptions.Defaults, CampaignOptionEditing.Change(SourceGameOptions.Defaults, row, 1, true));
        }
        Assert.IsFalse(CampaignOptionEditing.IsEditable(5, false));
        Assert.AreSame(SourceGameOptions.Defaults, CampaignOptionEditing.Change(SourceGameOptions.Defaults, 5, 1, false));
        Assert.AreEqual(9, CampaignOptionEditing.Change(SourceGameOptions.Defaults, 5, -1, true).Frame);
        Assert.AreEqual(SourceTextSpeed.Slow, CampaignOptionEditing.Change(SourceGameOptions.Defaults with { TextSpeed = SourceTextSpeed.Fast }, 0, 1, true).TextSpeed);
        Assert.AreSame(SourceGameOptions.Defaults, CampaignOptionEditing.Change(SourceGameOptions.Defaults, 1, 0, true));
    }
    [TestMethod]
    public void ButtonModeCyclesAllThreeFunctionalModesAndWraps()
    {
        var help = SourceGameOptions.Defaults;
        Assert.IsTrue(CampaignOptionEditing.IsEditable(4, false));
        var lr = CampaignOptionEditing.Change(help, 4, 1, false);
        Assert.AreEqual(SourceButtonMode.Lr, lr.ButtonMode);
        var la = CampaignOptionEditing.Change(lr, 4, 1, false);
        Assert.AreEqual(SourceButtonMode.LEqualsA, la.ButtonMode);
        Assert.AreEqual(help, CampaignOptionEditing.Change(la, 4, 1, false));
        Assert.AreEqual(la, CampaignOptionEditing.Change(help, 4, -1, false));
    }
}

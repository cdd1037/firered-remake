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

    [TestMethod]
    public void AuthoredEnglishLabelsAndTypedValuesMatchExistingOptions()
    {
        var text = Game.Content.PresentationTextCatalog.English("options");
        CollectionAssert.AreEqual(new[] { "TEXT SPEED", "BATTLE SCENE", "BATTLE STYLE", "SOUND", "BUTTON MODE", "FRAME", "CANCEL" },
            Enumerable.Range(0, 7).Select(row => CampaignOptionPresentation.Label(row, text)).ToArray());
        foreach (var (speed, label) in new[] { (SourceTextSpeed.Slow, "SLOW"), (SourceTextSpeed.Mid, "MID"), (SourceTextSpeed.Fast, "FAST") })
            Assert.AreEqual(label, CampaignOptionPresentation.Value(SourceGameOptions.Defaults with { TextSpeed = speed }, 0, text));
        foreach (var (scene, label) in new[] { (SourceBattleScene.On, "ON"), (SourceBattleScene.Off, "OFF") })
            Assert.AreEqual(label, CampaignOptionPresentation.Value(SourceGameOptions.Defaults with { BattleScene = scene }, 1, text));
        foreach (var (style, label) in new[] { (SourceBattleStyle.Shift, "SHIFT"), (SourceBattleStyle.Set, "SET") })
            Assert.AreEqual(label, CampaignOptionPresentation.Value(SourceGameOptions.Defaults with { BattleStyle = style }, 2, text));
        foreach (var (sound, label) in new[] { (SourceSound.Mono, "MONO"), (SourceSound.Stereo, "STEREO") })
            Assert.AreEqual(label, CampaignOptionPresentation.Value(SourceGameOptions.Defaults with { Sound = sound }, 3, text));
        foreach (var (mode, label) in new[] { (SourceButtonMode.Help, "HELP"), (SourceButtonMode.Lr, "LR"), (SourceButtonMode.LEqualsA, "L=A") })
            Assert.AreEqual(label, CampaignOptionPresentation.Value(SourceGameOptions.Defaults with { ButtonMode = mode }, 4, text));
        for (int frame = 0; frame < SourceGameOptions.FrameCount; frame++)
            Assert.AreEqual("TYPE " + (frame + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                CampaignOptionPresentation.Value(SourceGameOptions.Defaults with { Frame = frame }, 5, text));
        foreach (int row in new[] { -1, 6, 7 })
            Assert.AreEqual("", CampaignOptionPresentation.Value(SourceGameOptions.Defaults, row, text));
    }

    [TestMethod]
    public void RelabeledAndCollidingOptionTextDoesNotChangeTypedEditing()
    {
        string[] keys = ["label.text-speed", "label.battle-scene", "label.battle-style", "label.sound", "label.button-mode",
            "value.text-speed.slow", "value.text-speed.mid", "value.text-speed.fast", "value.battle-scene.on", "value.battle-scene.off",
            "value.battle-style.shift", "value.battle-style.set", "value.sound.mono", "value.sound.stereo",
            "value.button-mode.help", "value.button-mode.lr", "value.button-mode.l-equals-a"];
        var text = Game.Content.PresentationTextCatalog.Parse(System.Text.Json.JsonSerializer.Serialize(keys.ToDictionary(key => key, _ => "CANCEL")));
        var original = SourceGameOptions.Defaults;
        SourceGameOptions[] expected =
        [
            original with { TextSpeed = SourceTextSpeed.Fast }, original with { BattleScene = SourceBattleScene.Off },
            original with { BattleStyle = SourceBattleStyle.Set }, original with { Sound = SourceSound.Stereo },
            original with { ButtonMode = SourceButtonMode.Lr }
        ];
        for (int row = 0; row < expected.Length; row++)
        {
            Assert.AreEqual("CANCEL", CampaignOptionPresentation.Label(row, text));
            Assert.AreEqual("CANCEL", CampaignOptionPresentation.Value(original, row, text));
            var changed = CampaignOptionEditing.Change(original, row, 1, true);
            Assert.AreEqual(expected[row], changed);
            Assert.AreEqual("CANCEL", CampaignOptionPresentation.Value(changed, row, text));
        }
        Assert.AreEqual(new SourceGameOptions(), original);
    }

    [TestMethod]
    public void FrameTemplateCanChangeWithoutChangingZeroBasedFrameIdentity()
    {
        var text = Game.Content.PresentationTextCatalog.Parse("""{"value.frame":"Window ({Number})"}""");
        var options = SourceGameOptions.Defaults with { Frame = 9 };
        Assert.AreEqual("Window (10)", CampaignOptionPresentation.Value(options, 5, text));
        Assert.AreEqual(9, options.Frame);
        Assert.AreEqual(0, CampaignOptionEditing.Change(options, 5, 1, true).Frame);
    }

    [TestMethod]
    public void UnknownEnumValuesAreRejectedRatherThanDerivedFromEnumNames()
    {
        var text = Game.Content.PresentationTextCatalog.English("options");
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CampaignOptionPresentation.Value(SourceGameOptions.Defaults with { TextSpeed = (SourceTextSpeed)99 }, 0, text));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CampaignOptionPresentation.Value(SourceGameOptions.Defaults with { BattleScene = (SourceBattleScene)99 }, 1, text));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CampaignOptionPresentation.Value(SourceGameOptions.Defaults with { BattleStyle = (SourceBattleStyle)99 }, 2, text));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CampaignOptionPresentation.Value(SourceGameOptions.Defaults with { Sound = (SourceSound)99 }, 3, text));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CampaignOptionPresentation.Value(SourceGameOptions.Defaults with { ButtonMode = (SourceButtonMode)99 }, 4, text));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CampaignOptionPresentation.Label(-1, text));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CampaignOptionPresentation.Label(7, text));
    }

}

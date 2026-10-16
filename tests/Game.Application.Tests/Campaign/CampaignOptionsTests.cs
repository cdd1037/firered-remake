using Game.Application;
using Game.Domain.Options;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class CampaignOptionsTests
{
    [TestMethod]
    public void NewGameAndAbsentSavedOptionsUseSourceDefaults()
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var fresh = CampaignSession.CreateCurrent(catalog);
        Assert.AreEqual(SourceGameOptions.Defaults, fresh.EffectiveOptions);
        Assert.IsNull(fresh.State.Options);
        var saved = CurrentCampaignFixture.Checkpoint;
        var restored = CampaignSession.RestoreCurrent(catalog, saved.State with { Options = null }, saved.Generation);
        Assert.AreEqual(SourceGameOptions.Defaults, restored.EffectiveOptions);
        Assert.AreEqual(SourceSound.Mono, restored.EffectiveOptions.Sound);
        Assert.AreEqual(SourceBattleScene.On, restored.EffectiveOptions.BattleScene);
    }

    [TestMethod]
    public void SceneAndSoundCommitTogetherAndSurviveValidatedSaveContinue()
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var saved = CurrentCampaignFixture.Checkpoint;
        var session = CampaignSession.RestoreCurrent(catalog, saved.State, saved.Generation);
        Assert.IsTrue(session.CanEditOptions);
        var chosen = SourceGameOptions.Defaults with { BattleScene = SourceBattleScene.Off, Sound = SourceSound.Stereo };
        var before = session.State;
        var result = session.Execute("options-test", session.Revision, new("options_save") { Options = chosen });
        Assert.AreEqual(CommandStatus.Accepted, result.Status, result.Error);
        Assert.AreEqual(chosen, session.EffectiveOptions);
        Assert.AreEqual(CanonicalJson.Hash(before with { Options = chosen }), CanonicalJson.Hash(session.State));
        var encoded = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, catalog.ContentVersion);
        var decoded = SaveCodec.Decode<CampaignState>(encoded, session.RulesVersion, catalog.ContentVersion, validate: session.ValidatePlayerSave);
        var continued = CampaignSession.RestoreCurrent(catalog, decoded.State, decoded.Generation);
        Assert.AreEqual(chosen, continued.EffectiveOptions);
        Assert.AreEqual(CanonicalJson.Hash(session.State), CanonicalJson.Hash(continued.State));
        var reset = continued.Execute("options-reset", continued.Revision, new("options_save") { Options = SourceGameOptions.Defaults });
        Assert.AreEqual(CommandStatus.Accepted, reset.Status, reset.Error);
        Assert.IsNull(continued.State.Options);
        Assert.AreEqual(SourceGameOptions.Defaults, continued.EffectiveOptions);
    }

    [TestMethod]
    public void InvalidSceneOrSoundCannotMutateSavedOptions()
    {
        var saved = CurrentCampaignFixture.Checkpoint;
        var session = CampaignSession.RestoreCurrent(CurrentCampaignFixture.Catalog, saved.State, saved.Generation);
        foreach (var invalid in new[] {
            SourceGameOptions.Defaults with { BattleScene = (SourceBattleScene)9 },
            SourceGameOptions.Defaults with { Sound = (SourceSound)9 } })
        {
            var before = session.State;
            var revision = session.Revision;
            var result = session.Execute("invalid-options-" + invalid.Sound, revision, new("options_save") { Options = invalid });
            Assert.AreEqual(CommandStatus.Rejected, result.Status);
            Assert.AreSame(before, session.State);
            Assert.AreEqual(revision, session.Revision);
        }
    }
}

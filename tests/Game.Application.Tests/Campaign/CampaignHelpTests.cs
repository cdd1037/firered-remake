using Game.Application;
using Game.Domain.Options;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class CampaignHelpTests
{
    [TestMethod]
    public void FirstIntroductionCommitsOnlySourceFlagAndSurvivesSaveContinue()
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var saved = CurrentCampaignFixture.Checkpoint;
        var initial = saved.State with { Options = SourceGameOptions.Defaults, Script = saved.State.Script with
            { Flags = saved.State.Script.Flags.Remove("FLAG_SYS_SAW_HELP_SYSTEM_INTRO") } };
        var session = CampaignSession.RestoreCurrent(catalog, initial, saved.Generation);
        Assert.IsFalse(session.SawHelpIntroduction); Assert.IsTrue(session.CanOpenHelp);
        var result = session.Execute("help-intro", session.Revision, new("help_intro_seen"));
        Assert.AreEqual(CommandStatus.Accepted, result.Status, result.Error);
        Assert.IsTrue(session.SawHelpIntroduction);
        Assert.AreEqual(CanonicalJson.Hash(initial with { Script = initial.Script with
            { Flags = initial.Script.Flags.SetItem("FLAG_SYS_SAW_HELP_SYSTEM_INTRO", true) } }), CanonicalJson.Hash(session.State));
        var data = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, catalog.ContentVersion);
        var loaded = SaveCodec.Decode<CampaignState>(data, session.RulesVersion, catalog.ContentVersion, validate: session.ValidatePlayerSave);
        var continued = CampaignSession.RestoreCurrent(catalog, loaded.State, loaded.Generation);
        Assert.IsTrue(continued.SawHelpIntroduction);
        var unchanged = continued.State;
        Assert.AreEqual(CommandStatus.Accepted, continued.Execute("help-seen-again", continued.Revision, new("help_intro_seen")).Status);
        Assert.AreSame(unchanged, continued.State);
    }
    [TestMethod]
    public void OtherButtonModesCannotMarkHelpIntroduction()
    {
        var catalog = CurrentCampaignFixture.Catalog; var saved = CurrentCampaignFixture.Checkpoint;
        foreach (var mode in new[] { SourceButtonMode.Lr, SourceButtonMode.LEqualsA })
        {
            var session = CampaignSession.RestoreCurrent(catalog, saved.State with { Options = SourceGameOptions.Defaults with { ButtonMode = mode } }, saved.Generation);
            Assert.IsFalse(session.CanOpenHelp); var before = session.State;
            var result = session.Execute("help-disabled", session.Revision, new("help_intro_seen"));
            Assert.AreEqual(CommandStatus.Rejected, result.Status); Assert.AreSame(before, session.State);
        }
    }
}

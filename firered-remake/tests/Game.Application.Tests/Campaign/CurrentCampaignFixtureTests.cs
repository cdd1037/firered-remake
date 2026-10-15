using Game.Application;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class CurrentCampaignFixtureTests
{
    [TestMethod]
    public void SharedLateGameInputKeepsSessionsAndFixtureIsolated()
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var checkpoint = CurrentCampaignFixture.Checkpoint;
        var first = CampaignSession.RestoreCurrent(catalog, checkpoint.State, checkpoint.Generation);
        var second = CampaignSession.RestoreCurrent(catalog, checkpoint.State, checkpoint.Generation);
        var secondState = second.State;
        var originalHash = CanonicalJson.Hash(checkpoint.State);

        var result = first.Execute("isolated-fixture", first.Revision, new("play_time_tick", Choice: 1));

        Assert.AreEqual(CommandStatus.Accepted, result.Status);
        Assert.AreEqual(checkpoint.Generation + 1, first.Revision);
        Assert.AreEqual(checkpoint.Generation, second.Revision);
        Assert.AreSame(secondState, second.State);
        Assert.AreSame(checkpoint.State, CurrentCampaignFixture.Checkpoint.State);
        Assert.AreEqual(originalHash, CanonicalJson.Hash(CurrentCampaignFixture.Checkpoint.State));
        Assert.AreEqual(originalHash, CanonicalJson.Hash(second.State));
        Assert.AreNotSame(first.State, second.State);
    }
}

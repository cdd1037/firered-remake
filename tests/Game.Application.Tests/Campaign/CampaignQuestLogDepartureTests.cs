using Game.Application;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogDepartureTests
{
    [TestMethod]
    public void GameCornerDepartureRequiresPlayedReelsAndUsesOriginalCaption()
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=CampaignSession.CreateCurrent(catalog).State;
        var pair=catalog.QuestLogSource.Departures.Single(d=>d.Kind=="GAME_CORNER");
        var before=state with{World=state.World with{Player=state.World.Player with{MapId=new(pair.Inside)}}};
        var after=before with{World=before.World with{Player=before.World.Player with{MapId=new(pair.Outside)}}};
        Assert.IsNull(CampaignQuestLogRecorder.Record(null,before,after,new("move"),catalog));
        var recorded=CampaignQuestLogRecorder.Record(null,before,after,new("move"),catalog,playedSlots:true)!;
        Assert.AreEqual(CampaignQuestLogEvent.Departed,recorded.Scenes.Single().Actions.Single().Event);
        Assert.AreEqual("PlayedGamesAtGameCorner",recorded.Scenes[0].Actions[0].TextKey);
        var playback=new CampaignQuestLogPlayback(recorded,"RED",catalog.QuestLogSource);
        Assert.AreEqual("Played a lot of games at the ROCKET\nGAME CORNER in CELADON CITY.",playback.Caption);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(recorded,catalog.QuestLogSource,catalog));
        Assert.AreEqual(state.World.Rng,after.World.Rng);
    }
}

using Game.Application;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogChampionTests
{
    [TestMethod]
    public void ChampionReplaysThreeSourceCaptionsFromOneBoundedEvent()
    {
        var source=CurrentCampaignFixture.Catalog.QuestLogSource;
        var action=new CampaignQuestLogAction(-1,0,0,Direction.South,false,CampaignQuestLogEvent.ChampionWon,127)
        {TextKey="PlayerBattledChampionRival",Arguments=["RED","BLUE","BLASTOISE","VENUSAUR","barely"]};
        var log=new CampaignQuestLog([new("map.pallet-town",[new(CampaignQuestLogRecorder.PlayerId,"player",6,8,Direction.South,true)],[action])]);
        string saved=System.Text.Json.JsonSerializer.Serialize(log);
        var playback=new CampaignQuestLogPlayback(log,"RED",source);
        Assert.AreEqual("RED had a marvelous battle\nwith the CHAMPION, BLUE.",playback.Caption);
        playback.Tick(255d/60);
        Assert.AreEqual("In the end, BLUE sent out BLASTOISE\nwhile RED countered with\nVENUSAUR.",playback.Caption);
        Assert.IsFalse(playback.Finished);
        playback.Tick(255d/60);
        Assert.AreEqual("And, as a result of battling with every\nbit of power, barely won\nthe match.",playback.Caption);
        Assert.IsFalse(playback.Finished);
        playback.Tick(255d/60);Assert.IsTrue(playback.Finished);
        Assert.AreEqual(saved,System.Text.Json.JsonSerializer.Serialize(log));
        Assert.AreEqual(source.EventSizes[33]+2,CampaignQuestLogRecorder.Budget(log.Scenes[0].Actions,source));
    }

    [TestMethod]
    public void NextSceneSkipsRemainingChampionCaptionsWithoutReplayingCommands()
    {
        var source=CurrentCampaignFixture.Catalog.QuestLogSource;
        var action=new CampaignQuestLogAction(-1,0,0,Direction.South,false,CampaignQuestLogEvent.ChampionWon,127)
        {TextKey="PlayerBattledChampionRival",Arguments=["RED","BLUE","BLASTOISE","VENUSAUR","coolly"]};
        var scene=new CampaignQuestLogScene("map.pallet-town",[new(CampaignQuestLogRecorder.PlayerId,"player",6,8,Direction.South,true)],[action]);
        var playback=new CampaignQuestLogPlayback(new([scene]),"RED",source);
        playback.NextScene();Assert.IsTrue(playback.Finished);
        playback.Tick(100);Assert.IsTrue(playback.Finished);
    }
}

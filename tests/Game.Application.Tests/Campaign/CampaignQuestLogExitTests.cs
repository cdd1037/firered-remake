using System.Text.Json;
using Game.Application;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogExitTests
{
    private static CampaignQuestLogPlayback Playback(CampaignState? state=null)
    {
        var catalog=CurrentCampaignFixture.Catalog;
        state??=CampaignSession.CreateCurrent(catalog).State;
        var arrival=new CampaignQuestLogAction(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Arrived,127)
            {TextKey="ArrivedInLocation",Arguments=["PALLET TOWN"]};
        var log=CampaignQuestLogRecorder.Record(null,state,state,new("move"),catalog,suppliedEvent:arrival)!;
        return new(log,"RED",catalog.QuestLogSource,CampaignQuestLogRecorder.SavedScene(state,catalog.QuestLogSource));
    }
    [TestMethod]
    public void NaturalCaptionAndExitMatchExtractedOriginalTaskEveryFrame()
    {
        var playback=Playback();playback.NextScene();
        Assert.IsNotNull(playback.Caption);
        var rows=JsonSerializer.Deserialize<int[][]>(File.ReadAllText(Path.Combine(TestAssets.Root,"tests","Reference","quest-exit-source-vectors.json")))!;
        foreach(var row in rows)
        {
            playback.Tick(1d/60);
            Assert.AreEqual(row[1]!=0,playback.Caption is not null,$"caption frame {row[0]}");
            Assert.AreEqual(row[2]!=0,playback.HeaderVisible,$"header frame {row[0]}");
            Assert.AreEqual(row[3],playback.BarHeight,$"bars frame {row[0]}");
            Assert.AreEqual(row[4]!=0,playback.Finished,$"controls frame {row[0]}");
        }
    }
    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public void DismissalKeepsControlsLockedThroughEntireExit(bool skip)
    {
        var playback=Playback();if(!skip)playback.NextScene();
        if(skip)playback.Skip();else playback.NextScene();
        Assert.IsTrue(playback.SavedGameVisible);Assert.IsTrue(playback.Ending);Assert.IsNull(playback.Caption);
        var actors=playback.Actors.ToArray();string map=playback.Map;
        for(int i=1;i<=51;i++)
        {
            playback.NextScene();playback.Skip();playback.Tick(1d/60);
            Assert.IsFalse(playback.Finished,$"frame {i}");
            Assert.AreEqual(map,playback.Map);CollectionAssert.AreEqual(actors,playback.Actors.ToArray());
        }
        playback.Tick(1d/60);Assert.IsTrue(playback.Finished);
    }
    [TestMethod]
    [DataRow(30)] [DataRow(60)] [DataRow(120)]
    public void FractionalClockHasSameNaturalReleaseAndDoesNotMutateSave(int hz)
    {
        var state=CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog).State;
        string before=JsonSerializer.Serialize(state);var playback=Playback(state);playback.NextScene();
        for(int i=0;i<hz*3-1;i++)playback.Tick(1d/hz);
        Assert.IsFalse(playback.Finished);playback.Tick(1d/hz);Assert.IsTrue(playback.Finished);
        Assert.AreEqual(before,JsonSerializer.Serialize(state));
    }
}

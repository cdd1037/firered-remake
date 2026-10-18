using System.Text.Json;
using Game.Application;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogTransitionPhaseTests
{
    private static CampaignQuestLog Record(CampaignQuestLogTransitionTiming? timing)
    {
        var player=new CampaignQuestLogActor(CampaignQuestLogRecorder.PlayerId,"player",3,4,Direction.East,true);
        return new([new("source",[player],
        [new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Arrived,127),
         new(0,4,4,Direction.East,true,CampaignQuestLogEvent.None,16),
         new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.None,timing?.EndFrame??1)
         {StartFrame=16,Transition=new("target",[player with{X=8,Y=9}],null,[]),TransitionTiming=timing}])]);
    }

    [TestMethod]
    public void RealizedWarpKeepsSourceUntilRecordedSwitchWithoutReadingAudio()
    {
        var log=Record(new(FieldTransitionCause.PlainWarp,50,74));string original=JsonSerializer.Serialize(log);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log));
        var playback=new CampaignQuestLogPlayback(log);
        playback.Tick(16d/60);Assert.AreEqual("source",playback.Map);Assert.AreEqual(0,playback.TransitionElapsedFrames,.00001);
        Assert.IsNotNull(playback.ActiveTransitionTiming);string? caption=playback.Caption;
        playback.Tick(49d/60);Assert.AreEqual("source",playback.Map);Assert.AreEqual(caption,playback.Caption);
        playback.Tick(1d/60);Assert.AreEqual("target",playback.Map);Assert.AreEqual(50,playback.TransitionElapsedFrames,.00001);
        Assert.IsNull(playback.Movement(CampaignQuestLogRecorder.PlayerId));
        playback.Tick(24d/60);Assert.IsNull(playback.ActiveTransitionTiming);
        Assert.AreEqual(original,JsonSerializer.Serialize(log));
    }

    [TestMethod]
    public void UnknownHistoricalMarkerRemainsImmediateAndNewConnectedMarkerHasNoFade()
    {
        foreach(var timing in new CampaignQuestLogTransitionTiming?[]{null,new(FieldTransitionCause.ConnectedEdge,0,1)})
        {
            var log=Record(timing);Assert.IsNull(CampaignQuestLogRecorder.Validate(log));
            var playback=new CampaignQuestLogPlayback(log);playback.Tick(16d/60);
            Assert.AreEqual("target",playback.Map);Assert.IsNull(playback.Movement(CampaignQuestLogRecorder.PlayerId));
            Assert.AreEqual(timing,playback.ActiveTransitionTiming);
        }
        var json=JsonSerializer.Serialize(Record(null));Assert.IsFalse(json.Contains("TransitionTiming"));
        Assert.IsNull(CampaignQuestLogRecorder.Validate(JsonSerializer.Deserialize<CampaignQuestLog>(json)));
    }

    [TestMethod]
    [DataRow(FieldTransitionCause.Unknown,24,48)]
    [DataRow(FieldTransitionCause.AnimatedDoor,24,48)]
    [DataRow(FieldTransitionCause.PlainWarp,23,47)]
    [DataRow(FieldTransitionCause.PlainWarp,24,49)]
    [DataRow(FieldTransitionCause.ConnectedEdge,1,2)]
    [DataRow(FieldTransitionCause.ConnectedEdge,0,2)]
    public void MalformedKnownPhaseContractIsRejected(FieldTransitionCause cause,int change,int end)
    {
        Assert.AreEqual("save_quest_log_transition_timing_invalid",CampaignQuestLogRecorder.Validate(Record(new(cause,change,end))));
    }

    [TestMethod]
    public void KnownTransitionRejectsOverlappingMovementButDoesNotRetimePreviousRows()
    {
        var log=Record(new(FieldTransitionCause.PlainWarp,24,48));var scene=log.Scenes[0];
        var invalid=new CampaignQuestLog([scene with{Actions=scene.Actions.Add(new(0,9,9,Direction.East,true,CampaignQuestLogEvent.None,16){StartFrame=63})}]);
        Assert.AreEqual("save_quest_log_transition_time_invalid",CampaignQuestLogRecorder.Validate(invalid));
        var valid=new CampaignQuestLog([scene with{Actions=scene.Actions.Add(new(0,9,9,Direction.East,true,CampaignQuestLogEvent.None,16){StartFrame=64})}]);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(valid));
        Assert.AreSame(scene.Actions[1],valid.Scenes[0].Actions[1]);Assert.AreEqual(0,scene.Actions[1].StartFrame);Assert.AreEqual(16,scene.Actions[1].Frames);
    }

    [TestMethod]
    public void SameMapAcceptedWarpStillCreatesAProjectionWhileMissingCauseDoesNot()
    {
        var catalog=CurrentCampaignFixture.Catalog;var before=CampaignSession.CreateCurrent(catalog).State;
        before=before with{QuestLog=new([CampaignQuestLogRecorder.SavedScene(before,catalog.QuestLogSource)])};
        var to=before.World.Player with{X=before.World.Player.X+1};
        var receipt=new FieldTransitionReceipt(FieldTransitionCause.PlainWarp,FieldTransitionDeparture.Immediate,FieldTransitionArrival.Plain,before.World.Player,to,to);
        var after=before with{World=before.World with{Player=to},FieldTransition=receipt};
        var recorded=CampaignQuestLogRecorder.Record(before.QuestLog,before,after,new("move"),catalog)!;
        Assert.AreEqual(1,recorded.Scenes[0].Actions.Count(a=>a.Transition is not null));
        Assert.IsNull(recorded.Scenes[0].Actions[^1].TransitionTiming);
        var unknown=CampaignQuestLogRecorder.Record(before.QuestLog,before,after with{FieldTransition=null},new("move"),catalog)!;
        Assert.AreEqual(0,unknown.Scenes[0].Actions.Count(a=>a.Transition is not null));
        Assert.IsNull(CampaignQuestLogRecorder.Validate(recorded,catalog.QuestLogSource,catalog));
    }
}

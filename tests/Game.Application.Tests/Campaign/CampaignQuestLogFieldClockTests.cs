using Game.Application;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogFieldClockTests
{
    [TestMethod]
    public void CountsObservedEligibleFieldStepsWithoutDuplicateOrElapsedTimeCatchup()
    {
        var clock=new CampaignQuestLogFieldClock();
        clock.Observe(10,true);clock.Observe(10,true);Assert.AreEqual(1,clock.Frame);
        clock.Observe(11,false);clock.Observe(11,true);Assert.AreEqual(1,clock.Frame);
        clock.Observe(1_000_000,true);Assert.AreEqual(2,clock.Frame);
        clock.Reset();Assert.AreEqual(0,clock.Frame);
        clock.Observe(1_000_000,true);Assert.AreEqual(0,clock.Frame);
        clock.Observe(1_000_001,true);Assert.AreEqual(1,clock.Frame);
    }
    [TestMethod]
    public void FixedClockBudgetStopsWithoutWrappingAndIncludesRecordedScriptDuration()
    {
        var clock=new CampaignQuestLogFieldClock();clock.AdvanceTo(160);clock.Observe(1,true);Assert.AreEqual(161,clock.Frame);
        clock.AdvanceTo(int.MaxValue);Assert.IsTrue(clock.Full);
        clock.Observe(2,true);Assert.AreEqual(CampaignQuestLogFieldClock.FrameLimit,clock.Frame);
        clock.Reset();Assert.IsFalse(clock.Full);Assert.AreEqual(0,clock.Frame);
    }
    [TestMethod]
    public void IdleDelayUsesExistingTimelineAndDoesNotBlockAnEarlierCaption()
    {
        var catalog=CurrentCampaignFixture.Catalog;var before=CampaignSession.CreateCurrent(catalog).State;
        var caption=new CampaignQuestLogAction(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Arrived,127){TextKey="ArrivedInLocation",Arguments=["PALLET TOWN"]};
        var log=CampaignQuestLogRecorder.Record(null,before,before,new("move"),catalog,suppliedEvent:caption)!;
        var after=before with{World=before.World with{Player=before.World.Player with{X=before.World.Player.X+1}}};
        log=CampaignQuestLogRecorder.Record(log,before,after,new("move"),catalog,recordingFrame:120)!;
        Assert.AreEqual(120,log.Scenes[0].Actions[^1].StartFrame);
        var playback=new CampaignQuestLogPlayback(log,"RED",catalog.QuestLogSource);
        playback.Tick(1);Assert.IsNull(playback.Movement(CampaignQuestLogRecorder.PlayerId));Assert.IsNotNull(playback.Caption);
        playback.Tick(1);Assert.IsNotNull(playback.Movement(CampaignQuestLogRecorder.PlayerId));Assert.IsNotNull(playback.Caption);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log,catalog.QuestLogSource,catalog));
    }
    [TestMethod]
    public void PublicFieldObservationChangesNoCampaignRevisionRewardsOrRngAndStopsAtSaveContinue()
    {
        var catalog=CurrentCampaignFixture.Catalog;var session=CampaignSession.CreateCurrent(catalog);int sequence=0;
        void Run(CampaignCommand command){var result=session.Execute("quest-clock:"+(++sequence),session.Revision,command);Assert.AreEqual(CommandStatus.Accepted,result.Status,result.Error);}
        void Drain(){for(int i=0;session.State.Script.Wait is not null;i++){Assert.IsTrue(i<100);Run(new("ack",Choice:1));}}
        void WalkTo(int x,int y)
        {
            var start=session.State.World.Player;var map=catalog.Maps[start.MapId];
            var queue=new Queue<(int X,int Y)>();var seen=new HashSet<(int,int)>{(start.X,start.Y)};
            var previous=new Dictionary<(int,int),((int X,int Y) From,Direction Direction)>();queue.Enqueue((start.X,start.Y));
            while(queue.TryDequeue(out var at))
            {
                if(at==(x,y))break;
                foreach(var(dx,dy,d)in new[]{(0,-1,Direction.North),(1,0,Direction.East),(0,1,Direction.South),(-1,0,Direction.West)})
                {
                    var next=(at.X+dx,at.Y+dy);
                    if(seen.Contains(next)||map.GetTile(next.Item1,next.Item2)is not{Blocked:false}||session.State.Actors.Values.Any(a=>a.Active&&a.X==next.Item1&&a.Y==next.Item2))continue;
                    seen.Add(next);previous[next]=(at,d);queue.Enqueue(next);
                }
            }
            Assert.IsTrue(seen.Contains((x,y)));var path=new List<Direction>();var cursor=(x,y);
            while(cursor!=(start.X,start.Y)){var step=previous[cursor];path.Add(step.Direction);cursor=step.From;}path.Reverse();
            foreach(var direction in path){Run(new("move",direction));Drain();}
        }
        Drain();WalkTo(10,2);Run(new("move",Direction.West));Drain();Run(new("move",Direction.West));Drain();
        WalkTo(4,8);Run(new("move",Direction.South));Drain();Run(new("move",Direction.South));Drain();
        Assert.AreEqual("map.pallet-town",session.State.World.Player.MapId.Value);Assert.IsNotNull(session.State.QuestLog);
        var state=session.State;long revision=session.Revision;
        for(ulong i=1;i<=120;i++)session.ObserveQuestLogFieldFrame(i);
        Assert.AreSame(state,session.State);Assert.AreEqual(revision,session.Revision);Assert.AreEqual(state.World.Rng,session.State.World.Rng);
        Run(new("move",Direction.South));Assert.IsTrue(session.State.QuestLog!.Scenes[^1].Actions[^1].StartFrame>=120);
        for(ulong i=121;i<=136;i++)session.ObserveQuestLogFieldFrame(i);
        Run(new("move",Direction.North));Assert.AreEqual(136,session.State.QuestLog!.Scenes[^1].Actions[^1].StartFrame);
        session.StopQuestLogRecording();var saved=session.State.QuestLog;
        session.ObserveQuestLogFieldFrame(137);Run(new("move",Direction.South));Assert.AreSame(saved,session.State.QuestLog);
        session=CampaignSession.RestoreCurrent(catalog,session.State,session.Revision);
        session.ObserveQuestLogFieldFrame(138);Run(new("move",Direction.North));Assert.AreSame(saved,session.State.QuestLog);
        for(int i=0;session.State.World.Player.MapId.Value=="map.pallet-town";i++){Assert.IsTrue(i<4);Run(new("move",Direction.North));Drain();}
        var map=catalog.Maps[session.State.World.Player.MapId];var exit=map.FieldWarps.First(w=>map.SourceBehaviorAt(w.X,w.Y)==0x65);WalkTo(exit.X,exit.Y);
        for(int i=0;session.State.World.Player.MapId.Value!="map.pallet-town";i++){Assert.IsTrue(i<4);Run(new("move",Direction.South));Drain();}
        Assert.AreSame(saved,session.State.QuestLog); // Same home departure stays suppressed after Continue.
        var lab=catalog.Maps[session.State.World.Player.MapId].FieldWarps.Single(w=>w.DestinationMapId.Value=="map.professor-oaks-lab");
        WalkTo(lab.X,lab.Y+1);
        for(int i=0;session.State.World.Player.MapId.Value=="map.pallet-town";i++){Assert.IsTrue(i<4);Run(new("move",Direction.North));Drain();}
        var labMap=catalog.Maps[session.State.World.Player.MapId];var labExit=labMap.FieldWarps.First(w=>labMap.SourceBehaviorAt(w.X,w.Y)==0x65);WalkTo(labExit.X,labExit.Y);
        for(int i=0;session.State.World.Player.MapId.Value!="map.pallet-town";i++){Assert.IsTrue(i<4);Run(new("move",Direction.South));Drain();}
        Assert.AreNotSame(saved,session.State.QuestLog);Assert.IsFalse(session.State.QuestLog!.Scenes[^1].Closed);
        var full=session.State.QuestLog;
        for(ulong i=200;i<66_000;i++)session.ObserveQuestLogFieldFrame(i);
        Run(new("move",Direction.South));Assert.AreSame(full,session.State.QuestLog);
    }
}

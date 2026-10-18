using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogDoorEvidenceTests
{
    private static CampaignQuestLogTransitionTiming Timing(bool departure,bool arrival)=>new(
        departure?FieldTransitionCause.AnimatedDoor:FieldTransitionCause.PlainWarp,
        departure?FieldDoorPresentation.MinimumDepartureFrames:FieldWarpPresentation.MinimumDepartureFrames,
        (departure?FieldDoorPresentation.MinimumDepartureFrames:FieldWarpPresentation.MinimumDepartureFrames)+
        (arrival?FieldDoorPresentation.AnimatedArrivalFrames:FieldWarpPresentation.PlainArrivalFrames))
    {
        Arrival=arrival?FieldTransitionArrival.AnimatedDoor:FieldTransitionArrival.Plain,
        DepartureDoor=departure?new(3,3):null,ArrivalDoor=arrival?new(8,8):null
    };
    private static CampaignQuestLog Record(CampaignQuestLogTransitionTiming timing)
    {
        var player=new CampaignQuestLogActor(CampaignQuestLogRecorder.PlayerId,"player",3,4,Direction.North,true);
        return new([new("source",[player],
            [new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.None,timing.EndFrame)
            {Transition=new("destination",[player with{X=8,Y=9,Facing=Direction.South}],null,[]),TransitionTiming=timing}])]);
    }
    private static CampaignQuestLog ReplaceTiming(CampaignQuestLog log,CampaignQuestLogTransitionTiming timing)
    {
        var scene=log.Scenes[0];var index=scene.Actions.Length-1;var marker=scene.Actions[index];
        return new([scene with{Actions=scene.Actions.SetItem(index,marker with{Frames=timing.EndFrame,TransitionTiming=timing})}]);
    }

    [TestMethod]
    public void PlainTimingRetainsTheExistingSerializedShapeAndDoorEvidenceRoundTrips()
    {
        using var plain=JsonDocument.Parse(JsonSerializer.Serialize(new CampaignQuestLogTransitionTiming(FieldTransitionCause.PlainWarp,24,48)));
        CollectionAssert.AreEquivalent(new[]{"Cause","SwitchFrame","EndFrame"},plain.RootElement.EnumerateObject().Select(p=>p.Name).ToArray());
        foreach(var (departure,arrival) in new[]{(true,false),(false,true),(true,true)})
        {
            var log=Record(Timing(departure,arrival));var json=JsonSerializer.Serialize(log);
            var decoded=JsonSerializer.Deserialize<CampaignQuestLog>(json);
            Assert.IsNotNull(decoded);Assert.IsNull(CampaignQuestLogRecorder.Validate(decoded));
            Assert.AreEqual(log.Scenes[0].Actions[0].TransitionTiming,decoded.Scenes[0].Actions[0].TransitionTiming);
        }
    }

    [TestMethod]
    [DataRow(false,false,8)]
    [DataRow(true,false,12)]
    [DataRow(false,true,13)]
    [DataRow(true,true,17)]
    public void EveryOptionalDoorFieldConsumesItsBoundedLogicalAllocation(bool departure,bool arrival,int bytes)
    {
        var scene=Record(Timing(departure,arrival)).Scenes[0];var marker=scene.Actions[0];
        var unknown=ImmutableArray.Create(marker with{Frames=1,TransitionTiming=null});
        Assert.AreEqual(bytes,CampaignQuestLogRecorder.Budget(scene.Actions)-CampaignQuestLogRecorder.Budget(unknown));
    }

    [TestMethod]
    public void MissingExtraOrMismatchedDoorEvidenceIsRejectedInsteadOfInferred()
    {
        var both=Timing(true,true);
        foreach(var malformed in new[]{
            both with{DepartureDoor=null},both with{ArrivalDoor=null},
            both with{DepartureDoor=new(4,3)},both with{DepartureDoor=new(3,4)},
            both with{ArrivalDoor=new(8,9)},both with{ArrivalDoor=new(9,8)},
            both with{DepartureDoor=new(-1,3)},both with{ArrivalDoor=new(8,1024)},
            both with{Cause=FieldTransitionCause.PlainWarp},both with{Arrival=FieldTransitionArrival.Plain},
            both with{Cause=FieldTransitionCause.ConnectedEdge,SwitchFrame=0,EndFrame=1},
            both with{Arrival=FieldTransitionArrival.NonAnimatedDoor},both with{Arrival=FieldTransitionArrival.Stair},
            both with{Arrival=(FieldTransitionArrival)99},
            both with{SwitchFrame=FieldDoorPresentation.MinimumDepartureFrames-1,EndFrame=both.EndFrame-1},
            both with{EndFrame=both.EndFrame+1}})
            Assert.AreEqual("save_quest_log_transition_timing_invalid",CampaignQuestLogRecorder.Validate(Record(malformed)));
        var plain=Timing(false,false) with{ArrivalDoor=new(8,8)};
        Assert.AreEqual("save_quest_log_transition_timing_invalid",CampaignQuestLogRecorder.Validate(Record(plain)));
    }

    [TestMethod]
    public void DoorEvidenceRequiresSourceNorthAndArrivalSouthFacing()
    {
        var log=Record(Timing(true,true));var scene=log.Scenes[0];var marker=scene.Actions[0];var projection=marker.Transition!;
        Assert.AreEqual("save_quest_log_transition_timing_invalid",CampaignQuestLogRecorder.Validate(new(
            [scene with{Actors=[scene.Actors[0] with{Facing=Direction.South}]}])));
        Assert.AreEqual("save_quest_log_transition_timing_invalid",CampaignQuestLogRecorder.Validate(new(
            [scene with{Actions=[marker with{Transition=projection with{Actors=[projection.Actors[0] with{Facing=Direction.North}]}}]}])));
    }

    [TestMethod]
    public void DepartureAnchorFollowsTheRecordedPlayerMovementAndCurrentMapSegment()
    {
        var source=new CampaignQuestLogActor(CampaignQuestLogRecorder.PlayerId,"player",1,1,Direction.South,true);
        var arrived=source with{X=3,Y=5};
        var timing=Timing(true,false);
        var marker=Record(timing).Scenes[0].Actions[0] with{StartFrame=17};
        var log=new CampaignQuestLog([new("earlier",[source],
            [new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.None,1){Transition=new("source",[arrived],null,[])},
             new(0,3,4,Direction.North,true,CampaignQuestLogEvent.None,16){StartFrame=1,SourceAction="walk_up"},marker])]);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log));
        Assert.AreEqual("save_quest_log_transition_timing_invalid",CampaignQuestLogRecorder.Validate(
            ReplaceTiming(log,timing with{DepartureDoor=new(arrived.X,arrived.Y-1)})));
        Assert.AreEqual("save_quest_log_transition_timing_invalid",CampaignQuestLogRecorder.Validate(
            ReplaceTiming(log,timing with{DepartureDoor=new(source.X,source.Y-1)})));
    }

    [TestMethod]
    public void KnownDoorTimingPreventsOverlappingNewMapMovement()
    {
        var timing=Timing(true,true);var scene=Record(timing).Scenes[0];
        var movement=new CampaignQuestLogAction(0,8,10,Direction.South,true,CampaignQuestLogEvent.None,16){StartFrame=timing.EndFrame};
        Assert.IsNull(CampaignQuestLogRecorder.Validate(new([scene with{Actions=scene.Actions.Add(movement)}])));
        Assert.AreEqual("save_quest_log_transition_time_invalid",CampaignQuestLogRecorder.Validate(new(
            [scene with{Actions=scene.Actions.Add(movement with{StartFrame=timing.EndFrame-1})}])));
    }
}

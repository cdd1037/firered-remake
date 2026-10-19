using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class CampaignQuestLogNonAnimatedDoorEvidenceTests
{
    private static CampaignQuestLogTransitionTiming Timing(FieldTransitionCause cause, FieldTransitionArrival arrival)
    {
        int departure = cause switch
        {
            FieldTransitionCause.AnimatedDoor => FieldDoorPresentation.MinimumDepartureFrames,
            FieldTransitionCause.NonAnimatedDoor => FieldDoorPresentation.MinimumStepDepartureFrames,
            _ => FieldWarpPresentation.MinimumDepartureFrames
        };
        int arrivalFrames = arrival switch
        {
            FieldTransitionArrival.AnimatedDoor => FieldDoorPresentation.AnimatedArrivalFrames,
            FieldTransitionArrival.NonAnimatedDoor => FieldDoorPresentation.NonAnimatedArrivalFrames,
            _ => FieldWarpPresentation.PlainArrivalFrames
        };
        return new(cause, departure, departure + arrivalFrames)
        {
            Arrival = arrival,
            DepartureDoor = cause is FieldTransitionCause.AnimatedDoor or FieldTransitionCause.NonAnimatedDoor ? new(3, 3) : null,
            ArrivalDoor = arrival is FieldTransitionArrival.AnimatedDoor or FieldTransitionArrival.NonAnimatedDoor ? new(8, 8) : null
        };
    }

    private static CampaignQuestLog Record(CampaignQuestLogTransitionTiming timing)
    {
        var player = new CampaignQuestLogActor(CampaignQuestLogRecorder.PlayerId, "player", 3, 4, Direction.North, true);
        return new([new("source", [player],
            [new(-1, 0, 0, Direction.South, false, CampaignQuestLogEvent.None, timing.EndFrame)
            {
                Transition = new("destination", [player with { X = 8, Y = 9, Facing = Direction.South }], null, []),
                TransitionTiming = timing
            }])]);
    }

    private static CampaignQuestLog ReplaceTiming(CampaignQuestLog log, CampaignQuestLogTransitionTiming timing)
    {
        var scene = log.Scenes[0];
        int index = scene.Actions.Length - 1;
        var marker = scene.Actions[index];
        return new([scene with { Actions = scene.Actions.SetItem(index, marker with { Frames = timing.EndFrame, TransitionTiming = timing }) }]);
    }

    [TestMethod]
    [DataRow(FieldTransitionCause.NonAnimatedDoor, FieldTransitionArrival.Plain, 12)]
    [DataRow(FieldTransitionCause.PlainWarp, FieldTransitionArrival.NonAnimatedDoor, 13)]
    [DataRow(FieldTransitionCause.NonAnimatedDoor, FieldTransitionArrival.NonAnimatedDoor, 17)]
    [DataRow(FieldTransitionCause.AnimatedDoor, FieldTransitionArrival.NonAnimatedDoor, 17)]
    [DataRow(FieldTransitionCause.NonAnimatedDoor, FieldTransitionArrival.AnimatedDoor, 17)]
    public void IndependentDoorBranchesRoundTripWithBoundedAnchorAllocation(FieldTransitionCause cause,
        FieldTransitionArrival arrival, int expectedBytes)
    {
        var timing = Timing(cause, arrival);
        var log = Record(timing);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log));
        string json = JsonSerializer.Serialize(log);
        var decoded = JsonSerializer.Deserialize<CampaignQuestLog>(json);
        Assert.IsNotNull(decoded);
        Assert.AreEqual(json, JsonSerializer.Serialize(decoded));
        Assert.IsNull(CampaignQuestLogRecorder.Validate(decoded));
        Assert.AreEqual(timing, decoded.Scenes[0].Actions[0].TransitionTiming);
        var actions = log.Scenes[0].Actions;
        var unknown = ImmutableArray.Create(actions[0] with { Frames = 1, TransitionTiming = null });
        Assert.AreEqual(expectedBytes, CampaignQuestLogRecorder.Budget(actions) - CampaignQuestLogRecorder.Budget(unknown));
        var playback = new CampaignQuestLogPlayback(decoded);
        for (int frame = 0; frame < timing.EndFrame; frame++)
        {
            Assert.AreEqual(timing, playback.ActiveTransitionTiming);
            Assert.AreEqual(frame < timing.SwitchFrame ? "source" : "destination", playback.Map);
            playback.Tick(1d / 60);
        }
        Assert.IsNull(playback.ActiveTransitionTiming);
        Assert.IsTrue(playback.Finished);
        Assert.AreEqual(json, JsonSerializer.Serialize(decoded));
    }

    [TestMethod]
    [DataRow(3, 3, Direction.North)]
    [DataRow(3, 3, Direction.East)]
    [DataRow(3, 5, Direction.North)]
    [DataRow(2, 4, Direction.East)]
    [DataRow(4, 4, Direction.West)]
    public void ActualAdjacentDepartureAnchorIsIndependentOfPriorFacing(int x, int y, Direction priorFacing)
    {
        var timing = Timing(FieldTransitionCause.NonAnimatedDoor, FieldTransitionArrival.Plain) with { DepartureDoor = new(x, y) };
        var scene = Record(timing).Scenes[0];
        var log = new CampaignQuestLog([scene with { Actors = [scene.Actors[0] with { Facing = priorFacing }] }]);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log));
        string json = JsonSerializer.Serialize(log);
        var playback = new CampaignQuestLogPlayback(log);
        Assert.AreEqual(priorFacing, playback.Actors.Single().Facing,
            "The logical origin pose remains recorded; presentation derives the step direction from its adjacent anchor.");
        playback.Tick(timing.SwitchFrame / 60d);
        Assert.AreEqual("destination", playback.Map);
        Assert.AreEqual(json, JsonSerializer.Serialize(log));
    }

    [TestMethod]
    public void MissingExtraOrNonAdjacentStepEvidenceAndInvalidPhaseLengthsAreRejected()
    {
        var both = Timing(FieldTransitionCause.NonAnimatedDoor, FieldTransitionArrival.NonAnimatedDoor);
        foreach (var malformed in new[]
        {
            both with { DepartureDoor = null }, both with { ArrivalDoor = null },
            both with { DepartureDoor = new(3, 4) }, both with { DepartureDoor = new(4, 3) },
            both with { DepartureDoor = new(3, 2) }, both with { DepartureDoor = new(-1, 4) },
            both with { DepartureDoor = new(1024, 4) }, both with { ArrivalDoor = new(8, 9) },
            both with { ArrivalDoor = new(9, 8) }, both with { ArrivalDoor = new(8, -1) },
            both with { ArrivalDoor = new(8, 1024) }, both with { Cause = FieldTransitionCause.PlainWarp },
            both with { Cause = FieldTransitionCause.Unknown }, both with { Cause = FieldTransitionCause.StairWarp },
            both with { Cause = (FieldTransitionCause)99 }, both with { Arrival = FieldTransitionArrival.Plain },
            both with { Arrival = FieldTransitionArrival.Stair }, both with { Arrival = (FieldTransitionArrival)99 },
            both with { Cause = FieldTransitionCause.ConnectedEdge, SwitchFrame = 0, EndFrame = 1 },
            both with { SwitchFrame = both.SwitchFrame - 1, EndFrame = both.EndFrame - 1 },
            both with { EndFrame = both.EndFrame - 1 }, both with { EndFrame = both.EndFrame + 1 },
            both with { EndFrame = both.SwitchFrame }, both with { SwitchFrame = -1 }
        })
        {
            Assert.AreEqual("save_quest_log_transition_timing_invalid", CampaignQuestLogRecorder.Validate(Record(malformed)),
                JsonSerializer.Serialize(malformed));
        }
    }

    [TestMethod]
    public void NonAnimatedArrivalStillRequiresTheSouthwardAutomaticExitGeometry()
    {
        var scene = Record(Timing(FieldTransitionCause.PlainWarp, FieldTransitionArrival.NonAnimatedDoor)).Scenes[0];
        var marker = scene.Actions[0];
        var projection = marker.Transition!;
        foreach (var destination in new[]
        {
            projection.Actors[0] with { Facing = Direction.North },
            projection.Actors[0] with { Facing = Direction.East },
            projection.Actors[0] with { X = 9 },
            projection.Actors[0] with { Y = 8 }
        })
        {
            var malformed = new CampaignQuestLog([scene with
            {
                Actions = [marker with { Transition = projection with { Actors = [destination] } }]
            }]);
            Assert.AreEqual("save_quest_log_transition_timing_invalid", CampaignQuestLogRecorder.Validate(malformed));
        }
    }

    [TestMethod]
    public void NonAnimatedDepartureAnchorUsesTheCurrentSegmentAndInterveningMovement()
    {
        var original = new CampaignQuestLogActor(CampaignQuestLogRecorder.PlayerId, "player", 1, 1, Direction.South, true);
        var arrived = original with { X = 3, Y = 5 };
        var timing = Timing(FieldTransitionCause.NonAnimatedDoor, FieldTransitionArrival.Plain);
        var marker = Record(timing).Scenes[0].Actions[0] with { StartFrame = 17 };
        var log = new CampaignQuestLog([new("earlier", [original],
            [new(-1, 0, 0, Direction.South, false, CampaignQuestLogEvent.None, 1)
                { Transition = new("source", [arrived], null, []) },
             new(0, 3, 4, Direction.North, true, CampaignQuestLogEvent.None, 16)
                { StartFrame = 1, SourceAction = "walk_up" }, marker])]);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log));
        Assert.AreEqual("save_quest_log_transition_timing_invalid", CampaignQuestLogRecorder.Validate(
            ReplaceTiming(log, timing with { DepartureDoor = new(arrived.X + 1, arrived.Y) })));
        Assert.AreEqual("save_quest_log_transition_timing_invalid", CampaignQuestLogRecorder.Validate(
            ReplaceTiming(log, timing with { DepartureDoor = new(original.X, original.Y - 1) })));
    }

    [TestMethod]
    public void ObservedNonAnimatedDoorPreventsMovementUntilItsFullArrivalRelease()
    {
        var timing = Timing(FieldTransitionCause.NonAnimatedDoor, FieldTransitionArrival.NonAnimatedDoor);
        var scene = Record(timing).Scenes[0];
        var movement = new CampaignQuestLogAction(0, 8, 10, Direction.South, true, CampaignQuestLogEvent.None, 16)
            { StartFrame = timing.EndFrame };
        Assert.IsNull(CampaignQuestLogRecorder.Validate(new([scene with { Actions = scene.Actions.Add(movement) }])));
        Assert.AreEqual("save_quest_log_transition_time_invalid", CampaignQuestLogRecorder.Validate(new(
            [scene with { Actions = scene.Actions.Add(movement with { StartFrame = timing.EndFrame - 1 }) }])));
    }

    [TestMethod]
    public void UnknownHistoricalMarkerRemainsImmediateWithoutInventingDoorEvidence()
    {
        var scene = Record(Timing(FieldTransitionCause.NonAnimatedDoor, FieldTransitionArrival.NonAnimatedDoor)).Scenes[0];
        var unknown = new CampaignQuestLog([scene with { Actions = [scene.Actions[0] with { Frames = 1, TransitionTiming = null }] }]);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(unknown));
        string json = JsonSerializer.Serialize(unknown);
        Assert.IsFalse(json.Contains("TransitionTiming", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("DepartureDoor", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("ArrivalDoor", StringComparison.Ordinal));
        var decoded = JsonSerializer.Deserialize<CampaignQuestLog>(json)!;
        var playback = new CampaignQuestLogPlayback(decoded);
        Assert.AreEqual("destination", playback.Map);
        Assert.IsNull(playback.ActiveTransitionTiming);
        playback.Tick(1d / 60);
        Assert.IsTrue(playback.Finished);
        Assert.AreEqual(json, JsonSerializer.Serialize(decoded));
    }

    [TestMethod]
    public void NonAnimatedDeparturePausesCaptionTimeAndClearsOldTextAtTheObservedSwitch()
    {
        var timing = Timing(FieldTransitionCause.NonAnimatedDoor, FieldTransitionArrival.Plain);
        var scene = Record(timing).Scenes[0];
        var first = new CampaignQuestLogAction(-1, 0, 0, Direction.South, false, CampaignQuestLogEvent.Arrived, 100);
        var second = new CampaignQuestLogAction(-1, 0, 0, Direction.South, false, CampaignQuestLogEvent.UsedItem, 20)
            { StartFrame = 15 };
        var log = new CampaignQuestLog([scene with { Actions = [first, scene.Actions[0] with { StartFrame = 10 }, second] }]);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log));
        string json = JsonSerializer.Serialize(log);
        var playback = new CampaignQuestLogPlayback(log);
        string? initialCaption = playback.Caption;
        Assert.IsNotNull(initialCaption);
        int lockedFrames = timing.EndFrame - FieldDoorPresentation.NormalWalkPixels;
        for (int frame = 0; frame < 120 + lockedFrames; frame++)
        {
            if (frame < 10 + timing.SwitchFrame) Assert.AreEqual(initialCaption, playback.Caption);
            else if (frame < 100 + lockedFrames) Assert.IsNull(playback.Caption);
            else Assert.IsNotNull(playback.Caption);
            playback.Tick(1d / 60);
        }
        Assert.IsNull(playback.Caption);
        Assert.IsTrue(playback.Finished);
        Assert.AreEqual(json, JsonSerializer.Serialize(log));
    }

    [TestMethod]
    public void CaptionCanBeginAndFinishDuringTheUnlockedDepartureStep()
    {
        var timing = Timing(FieldTransitionCause.NonAnimatedDoor, FieldTransitionArrival.Plain);
        var scene = Record(timing).Scenes[0];
        var caption = new CampaignQuestLogAction(-1, 0, 0, Direction.South, false, CampaignQuestLogEvent.Arrived, 4)
            { StartFrame = 15 };
        var log = new CampaignQuestLog([scene with { Actions = [scene.Actions[0] with { StartFrame = 10 }, caption] }]);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log));
        string json = JsonSerializer.Serialize(log);
        var playback = new CampaignQuestLogPlayback(log);
        for (int frame = 0; frame <= 10 + timing.EndFrame; frame++)
        {
            if (frame is >= 15 and < 19) Assert.IsNotNull(playback.Caption);
            else Assert.IsNull(playback.Caption);
            playback.Tick(1d / 60);
        }
        Assert.IsTrue(playback.Finished);
        Assert.AreEqual(json, JsonSerializer.Serialize(log));
    }
}

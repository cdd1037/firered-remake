using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Roaming;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>Controlled approaches; the ladder command, entry and recording are public gameplay.</summary>
[TestClass, TestCategory("Game")]
public sealed class CaveLadderTransitionTests : WildBattleFixture
{
    private const string FirstFloor = "map.mt-moon-1f";
    private const string Basement = "map.mt-moon-b1f";

    private CampaignSession AtLadder(bool fromBasement, Direction initialFacing)
    {
        var session = CampaignSession.CreateCurrent(content);
        var basis = FieldFixture();
        var map = new MapId(fromBasement ? Basement : FirstFloor);
        int x = fromBasement ? 3 : 5, y = fromBasement ? 4 : 7;
        var state = basis with
        {
            World = basis.World with { Player = new(map, x, y, 3, initialFacing) },
            Script = StoryState.Empty with
            {
                Flags = basis.Script.Flags.SetItem("FLAG_SYS_ON_CYCLING_ROAD", false)
                    .SetItem("FLAG_WORLD_MAP_MT_MOON_1F", true),
                Variables = basis.Script.Variables,
                EffectSequence = basis.Script.EffectSequence
            },
            Dialogue = null, ItemMenu = null, MonPicture = null, Motions = [],
            PresentationTicks = 0, PresentationEffects = [], EntryPrograms = [], ArrivalFacing = null,
            IsSurfing = false, IsCycling = false, BicycleSlope = false, MapEdits = null,
            MapLayout = null, FieldTransition = null, FlashLevel = 0, FieldWeather = null,
            PlayerVisible = true, Music = content.MapDefinition(map).Music, QuestLog = null
        };
        var enter = typeof(CampaignSession).GetMethod("EnterMap", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var pump = typeof(CampaignSession).GetMethod("Pump", BindingFlags.Instance | BindingFlags.NonPublic)!;
        state = (CampaignState)enter.Invoke(session, [state, null, RoamerTransition.Warp])!;
        state = (CampaignState)pump.Invoke(session, [state])!;
        typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, content.ContentVersion, session.Revision));
        Assert.IsTrue(state.Script.Completed);
        Assert.IsNull(state.Dialogue);
        Assert.AreEqual(new TilePosition(map, x, y, 3, initialFacing), state.World.Player);
        StrictRoundtrip(session);
        Assert.IsTrue(session.CanReorderFieldParty);
        Send(session, new("party_swap", PartyIndex: 1, Choice: 0));
        Assert.AreEqual(map.Value, session.State.QuestLog!.Scenes[^1].Map);
        Assert.AreEqual(CampaignQuestLogEvent.PartyOrder, session.State.QuestLog.Scenes[^1].Actions.Single().Event);
        return session;
    }

    private void AssertSourceRoute()
    {
        var first = new MapId(FirstFloor);
        var basement = new MapId(Basement);
        Assert.AreEqual(new FieldWarp(0, 5, 6, 3, basement, 0), content.Maps[first].FieldWarps[0]);
        Assert.AreEqual(new FieldWarp(0, 3, 3, 3, first, 0), content.Maps[basement].FieldWarps[0]);
        Assert.AreEqual(new FieldMetatileEdit(5, 6, 0x285, 0, 3), content.SourceMetatileAt(first, 5, 6));
        Assert.AreEqual(new FieldMetatileEdit(3, 3, 0x296, 0, 3), content.SourceMetatileAt(basement, 3, 3));
        foreach (var (map, x, y) in new[] { (first, 5, 6), (basement, 3, 3) })
        {
            var definition = content.MapDefinition(map);
            Assert.AreEqual("MAP_TYPE_UNDERGROUND", definition.MapType);
            Assert.AreEqual("MAPSEC_MT_MOON", definition.RegionMapSection);
            Assert.AreEqual("WEATHER_NONE", definition.Weather);
            Assert.AreEqual("MUS_MT_MOON", definition.Music);
            Assert.IsFalse(definition.RequiresFlash);
            Assert.AreEqual(0x61, content.Maps[map].SourceBehaviorAt(x, y));
            Assert.AreEqual(0x08, content.Maps[map].SourceBehaviorAt(x, y + 1));
            Assert.IsFalse(content.Maps[map].GetTile(x, y + 1)!.Blocked);
        }
        Assert.IsNull(content.MapDefinition(basement).Programs.OnTransition);
        Assert.AreEqual("MtMoon_1F_OnTransition", content.MapDefinition(first).Programs.OnTransition);
    }

    [TestMethod]
    [DataRow(false, Direction.North, 0)]
    [DataRow(false, Direction.East, 7)]
    [DataRow(true, Direction.North, 0)]
    [DataRow(true, Direction.East, 9)]
    public void PublicCaveLadderStepPreservesDirectionRecordsAndStrictlyContinues(bool fromBasement,
        Direction initialFacing, int musicWait)
    {
        AssertSourceRoute();
        var session = AtLadder(fromBasement, initialFacing);
        var origin = session.State;
        var from = origin.World.Player;
        var destination = new MapId(fromBasement ? FirstFloor : Basement);
        var target = fromBasement ? (X: 5, Y: 6) : (X: 3, Y: 3);
        var expectedRoamer = SourceRoamerRules.Transition(origin.Roamer!, RoamerTransition.Warp,
            content.CelioRoamerMaps![destination], origin.World.Rng);
        long commandRevision = session.Revision;
        Send(session, new("move", Direction.North));
        var state = session.State;
        var receipt = state.FieldTransition;
        Assert.IsNotNull(receipt);
        Assert.AreEqual(commandRevision + 1, session.Revision);
        Assert.AreEqual(FieldTransitionCause.PlainWarp, receipt.Cause);
        Assert.AreEqual(FieldTransitionDeparture.StepOntoWarp, receipt.Departure);
        Assert.AreEqual(FieldTransitionArrival.Plain, receipt.Arrival);
        Assert.AreEqual(from, receipt.From);
        Assert.AreEqual(new TilePosition(from.MapId, from.X, from.Y - 1, 3, Direction.North), receipt.DepartureStep);
        Assert.AreEqual(new TilePosition(destination, target.X, target.Y, 3, Direction.North), receipt.To);
        Assert.AreEqual(receipt.To, receipt.ArrivalAnchor, "Ladder arrival stays on its warp cell without an automatic exit step.");
        Assert.AreEqual(receipt.To, state.World.Player);
        Assert.AreEqual(initialFacing, receipt.From.Facing);
        Assert.IsNull(receipt.DepartureStairBehavior);
        Assert.IsNull(receipt.ArrivalStairBehavior);
        Assert.AreEqual(origin.World.StepCount + 1, state.World.StepCount);
        Assert.AreEqual(origin.World.Revision + 1, state.World.Revision);
        Assert.AreEqual(expectedRoamer.Rng, state.World.Rng);
        Assert.IsTrue(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expectedRoamer.State),
            JsonSerializer.SerializeToElement(state.Roamer)));
        Assert.IsTrue(state.Script.Completed);
        Assert.IsFalse(state.Script.Locked);
        Assert.IsNull(state.Script.Wait);
        Assert.IsTrue(state.EntryPrograms.IsEmpty);
        Assert.IsTrue(state.Motions.IsEmpty);
        Assert.IsNull(state.Dialogue);
        Assert.IsNull(state.WildBattle);
        Assert.IsTrue(session.CanPresentFieldTransition(origin, receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(receipt), "A ladder step requires the authentic stable walking origin.");
        Assert.IsFalse(session.CanPresentFieldTransition(origin, receipt with { }));
        Assert.IsFalse(session.CanPresentFieldTransition(origin with { IsCycling = true }, receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(origin with { IsSurfing = true }, receipt));

        var marker = state.QuestLog!.Scenes[^1].Actions.Last(action => action.Transition is not null);
        Assert.IsNull(marker.TransitionTiming);
        var unknown = new CampaignQuestLogPlayback(new([state.QuestLog.Scenes[^1]]));
        unknown.Tick(marker.StartFrame / 60d);
        Assert.IsNull(unknown.ActiveTransitionTiming);
        Assert.AreEqual(destination.Value, unknown.Map, "Unobserved historical ladder markers stay unknown.");
        int change = FieldDoorPresentation.MinimumStepDepartureFrames + musicWait;
        int end = change + FieldWarpPresentation.PlainArrivalFrames;
        Assert.AreEqual(FieldDoorPresentation.MinimumStepDepartureFrames,
            FieldDoorPresentation.DepartureFrames(receipt.Cause, receipt.Departure));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt with { }, change, end));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt,
            FieldDoorPresentation.MinimumStepDepartureFrames - 1,
            FieldDoorPresentation.MinimumStepDepartureFrames - 1 + FieldWarpPresentation.PlainArrivalFrames));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end - 1));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end + 1));
        long revision = session.Revision;
        string unchanged = JsonSerializer.Serialize(state);
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change + 1, end + 1));

        var saved = session.PrepareFieldSave();
        var recordedScene = saved.QuestLog!.Scenes[^1];
        var recordedMarker = recordedScene.Actions.Last(action => action.Transition is not null);
        var timing = recordedMarker.TransitionTiming;
        Assert.IsNotNull(timing);
        Assert.AreEqual(FieldTransitionCause.PlainWarp, timing.Cause);
        Assert.AreEqual(FieldTransitionDeparture.StepOntoWarp, timing.Departure);
        Assert.AreEqual(timing.Departure, timing.EffectiveDeparture);
        Assert.AreEqual(FieldTransitionArrival.Plain, timing.Arrival);
        Assert.AreEqual(new CampaignQuestLogDoorAnchor(from.X, from.Y - 1), timing.DepartureDoor);
        Assert.IsNull(timing.ArrivalDoor);
        Assert.IsNull(timing.DepartureStairBehavior);
        Assert.IsNull(timing.ArrivalStairBehavior);
        Assert.AreEqual(change, timing.SwitchFrame);
        Assert.AreEqual(end, timing.EndFrame);
        Assert.AreEqual(end, recordedMarker.Frames);
        Assert.IsTrue(JsonSerializer.Serialize(timing).Contains("\"Departure\":1", StringComparison.Ordinal));
        Assert.IsFalse(JsonSerializer.Serialize(timing).Contains("EffectiveDeparture", StringComparison.Ordinal));
        Assert.AreEqual(JsonSerializer.Serialize(state with { QuestLog = saved.QuestLog }), JsonSerializer.Serialize(saved));
        Assert.IsNull(CampaignQuestLogRecorder.Validate(saved.QuestLog, content.QuestLogSource, content));
        Assert.IsNull(session.ValidatePlayerSave(saved));

        int markerIndex = recordedScene.Actions.IndexOf(recordedMarker);
        foreach (var malformed in new[]
        {
            timing with { Departure = FieldTransitionDeparture.Immediate },
            timing with { Departure = (FieldTransitionDeparture)99 },
            timing with { DepartureDoor = null },
            timing with { DepartureDoor = new(from.X, from.Y) },
            timing with { DepartureDoor = new(from.X + 1, from.Y - 1) },
            timing with { ArrivalDoor = new(target.X, target.Y) },
            timing with { DepartureStairBehavior = 0x6c },
            timing with { Cause = FieldTransitionCause.NonAnimatedDoor },
            timing with { SwitchFrame = FieldDoorPresentation.MinimumStepDepartureFrames - 1,
                EndFrame = FieldDoorPresentation.MinimumStepDepartureFrames - 1 + FieldWarpPresentation.PlainArrivalFrames }
        })
        {
            var invalidScene = recordedScene with
            {
                Actions = recordedScene.Actions.SetItem(markerIndex, recordedMarker with
                    { Frames = malformed.EndFrame, TransitionTiming = malformed })
            };
            var invalidLog = saved.QuestLog with
                { Scenes = saved.QuestLog.Scenes.SetItem(saved.QuestLog.Scenes.Length - 1, invalidScene) };
            Assert.AreEqual("save_quest_log_transition_timing_invalid",
                CampaignQuestLogRecorder.Validate(invalidLog, content.QuestLogSource, content), JsonSerializer.Serialize(malformed));
        }

        var bytes = SaveCodec.Encode(saved, revision, session.RulesVersion, content.ContentVersion);
        var decoded = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, content.ContentVersion,
            validate: session.ValidatePlayerSave);
        var restored = CampaignSession.RestoreCurrent(content, decoded.State, decoded.Generation);
        Assert.IsTrue(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(saved), JsonSerializer.SerializeToElement(restored.State)));
        Assert.IsFalse(restored.CompleteFieldTransitionPresentation(receipt, change, end));
        var playback = new CampaignQuestLogPlayback(new([restored.State.QuestLog!.Scenes[^1]]));
        playback.Tick(recordedMarker.StartFrame / 60d);
        var departure = new FieldDoorPresentation.StepDeparture();
        var arrival = new FieldWarpPresentation.PlainArrival();
        for (int frame = 0; frame < end; frame++)
        {
            Assert.AreEqual(timing, playback.ActiveTransitionTiming);
            var expected = frame < change ? receipt.From : receipt.To;
            Assert.AreEqual(expected.MapId.Value, playback.Map);
            var player = playback.Actors.Single(actor => actor.Id == CampaignQuestLogRecorder.PlayerId);
            Assert.AreEqual((expected.X, expected.Y, expected.Facing), (player.X, player.Y, player.Facing));
            var live = frame < change ? departure.Sample : FieldDoorPresentation.FromPlain(arrival.Sample);
            Assert.AreEqual(live, FieldDoorPresentation.SampleRecorded(timing.Cause, timing.Arrival,
                change, frame, null, null, timing.EffectiveDeparture));
            Assert.IsTrue(live.PlayerVisible);
            Assert.IsFalse(live.WipeActive);
            Assert.IsFalse(live.DoorAnimationActive);
            Assert.AreEqual(-1, live.DoorFrame);
            if (frame < 16)
            {
                Assert.AreEqual(frame, live.WalkPixels);
                Assert.IsFalse(live.MusicFadeRequested);
                Assert.AreEqual(0, live.BackgroundBlend);
                Assert.AreEqual(0, live.ObjectBlend);
            }
            if (frame >= change) Assert.AreEqual(0, live.WalkPixels, "Ladder arrival cannot walk out like a nonanimated door.");
            if (frame < change) departure.Advance(frame + 1 >= change - 1);
            else arrival.Advance();
            playback.Tick(1d / 60);
        }
        Assert.IsTrue(departure.Complete);
        Assert.IsTrue(arrival.Complete);
        Assert.IsNull(playback.ActiveTransitionTiming);
        Assert.AreEqual(destination.Value, playback.Map);
        Assert.AreSame(state, session.State);
        Assert.AreEqual(revision, session.Revision);
        Assert.AreEqual(unchanged, JsonSerializer.Serialize(session.State));
        Assert.IsNull(marker.TransitionTiming);
    }

    [TestMethod]
    public void LadderTimingAllocatesOnlyItsExplicitDepartureByteAndKeepsLegacyDoorShape()
    {
        var player = new CampaignQuestLogActor(CampaignQuestLogRecorder.PlayerId, "player", 3, 4, Direction.North, true);
        var timing = new CampaignQuestLogTransitionTiming(FieldTransitionCause.PlainWarp, 40, 64)
            { Departure = FieldTransitionDeparture.StepOntoWarp, DepartureDoor = new(3, 3) };
        var marker = new CampaignQuestLogAction(-1, 0, 0, Direction.South, false, CampaignQuestLogEvent.None, 64)
            { Transition = new("destination", [player with { X = 5, Y = 6 }], null, []), TransitionTiming = timing };
        var log = new CampaignQuestLog([new("source", [player], [marker])]);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log));
        Assert.AreEqual(13, CampaignQuestLogRecorder.Budget([marker]) -
            CampaignQuestLogRecorder.Budget([marker with { Frames = 1, TransitionTiming = null }]));
        var legacy = timing with { Cause = FieldTransitionCause.NonAnimatedDoor, Departure = FieldTransitionDeparture.Immediate };
        string json = JsonSerializer.Serialize(legacy);
        Assert.IsFalse(json.Contains("\"Departure\":", StringComparison.Ordinal));
        var decoded = JsonSerializer.Deserialize<CampaignQuestLogTransitionTiming>(json)!;
        Assert.AreEqual(FieldTransitionDeparture.StepOntoWarp, decoded.EffectiveDeparture);
        Assert.AreEqual(json, JsonSerializer.Serialize(decoded));
        Assert.IsNull(CampaignQuestLogRecorder.Validate(new([new("source", [player], [marker with { TransitionTiming = decoded }])])));
        var plain = new CampaignQuestLogTransitionTiming(FieldTransitionCause.PlainWarp, 24, 48);
        string plainJson = JsonSerializer.Serialize(plain);
        Assert.IsFalse(plainJson.Contains("\"Departure\":", StringComparison.Ordinal));
        Assert.IsFalse(plainJson.Contains("DepartureDoor", StringComparison.Ordinal));
        var plainDecoded = JsonSerializer.Deserialize<CampaignQuestLogTransitionTiming>(plainJson)!;
        Assert.AreEqual(FieldTransitionDeparture.Immediate, plainDecoded.EffectiveDeparture);
        Assert.AreEqual(plainJson, JsonSerializer.Serialize(plainDecoded));
        Assert.IsNull(CampaignQuestLogRecorder.Validate(new([new("source", [player],
            [marker with { Frames = 48, TransitionTiming = plainDecoded }])])));
    }

    [TestMethod]
    public void LadderCaptionClockRunsDuringApproachAndLocksOnlyAfterTheCompletedStep()
    {
        var player = new CampaignQuestLogActor(CampaignQuestLogRecorder.PlayerId, "player", 3, 4, Direction.North, true);
        var timing = new CampaignQuestLogTransitionTiming(FieldTransitionCause.PlainWarp, 40, 64)
            { Departure = FieldTransitionDeparture.StepOntoWarp, DepartureDoor = new(3, 3) };
        var marker = new CampaignQuestLogAction(-1, 0, 0, Direction.South, false, CampaignQuestLogEvent.None, 64)
        {
            StartFrame = 10, Transition = new("destination", [player with { X = 5, Y = 6 }], null, []),
            TransitionTiming = timing
        };
        var shortCaption = new CampaignQuestLogAction(-1, 0, 0, Direction.South, false, CampaignQuestLogEvent.Arrived, 4)
            { StartFrame = 15 };
        var log = new CampaignQuestLog([new("source", [player], [marker, shortCaption])]);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log));
        var playback = new CampaignQuestLogPlayback(log);
        for (int frame = 0; frame <= 74; frame++)
        {
            if (frame is >= 15 and < 19) Assert.IsNotNull(playback.Caption);
            else Assert.IsNull(playback.Caption);
            playback.Tick(1d / 60);
        }
        Assert.IsTrue(playback.Finished);
    }

    [TestMethod]
    public void LadderCaptionClockPausesFromStepCompletionThroughStationaryArrival()
    {
        var player = new CampaignQuestLogActor(CampaignQuestLogRecorder.PlayerId, "player", 3, 4, Direction.North, true);
        var timing = new CampaignQuestLogTransitionTiming(FieldTransitionCause.PlainWarp, 40, 64)
            { Departure = FieldTransitionDeparture.StepOntoWarp, DepartureDoor = new(3, 3) };
        var marker = new CampaignQuestLogAction(-1, 0, 0, Direction.South, false, CampaignQuestLogEvent.None, 64)
        {
            StartFrame = 10, Transition = new("destination", [player with { X = 5, Y = 6 }], null, []),
            TransitionTiming = timing
        };
        var first = new CampaignQuestLogAction(-1, 0, 0, Direction.South, false, CampaignQuestLogEvent.Arrived, 100);
        var second = new CampaignQuestLogAction(-1, 0, 0, Direction.South, false, CampaignQuestLogEvent.UsedItem, 20)
            { StartFrame = 15 };
        var log = new CampaignQuestLog([new("source", [player], [first, marker, second])]);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log));
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
    }
}

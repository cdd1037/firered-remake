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

/// <summary>
/// Controlled placement on Route 4. Cave entry, departure latch, turn, EXIT,
/// recording and Continue use public gameplay; this is not a campaign replay.
/// </summary>
[TestClass, TestCategory("Game")]
public sealed class CaveExitTransitionTests : WildBattleFixture
{
    private const string Cave = "map.mt-moon-1f", Outside = "map.route-4";
    private const string Visited = "FLAG_WORLD_MAP_MT_MOON_1F";

    private static CampaignQuestLogAction LastTransition(CampaignState state) =>
        state.QuestLog!.Scenes[^1].Actions.Last(action => action.Transition is not null);

    private void SetState(CampaignSession session, CampaignState state) =>
        typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, content.ContentVersion, session.Revision));

    private CampaignSession AtOutsideDoor(Direction initialFacing)
    {
        var session = CampaignSession.CreateCurrent(content);
        var basis = FieldFixture();
        // Retain the checkpoint's acquired state. The public entrance below,
        // rather than fixture assignment, must establish the real departure latch.
        Assert.IsTrue(basis.Script.Flags.GetValueOrDefault(Visited));
        Assert.IsFalse(basis.Script.Flags.GetValueOrDefault("FLAG_SYS_ON_CYCLING_ROAD"));
        Assert.IsFalse(basis.Script.Flags.GetValueOrDefault(CampaignQuestLogDepartureCapture.PendingFlag));
        var map = new MapId(Outside);
        var state = basis with
        {
            World = basis.World with { Player = new(map, 19, 6, 3, initialFacing) },
            Script = StoryState.Empty with
            {
                Flags = basis.Script.Flags, Variables = basis.Script.Variables,
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
        SetState(session, state);
        AssertStable(state);
        Assert.AreEqual(new TilePosition(map, 19, 6, 3, initialFacing), state.World.Player);
        Assert.IsFalse(state.Script.Flags.GetValueOrDefault(CampaignQuestLogDepartureCapture.PendingFlag));
        StrictRoundtrip(session);
        Assert.IsTrue(session.CanReorderFieldParty);
        Send(session, new("party_swap", PartyIndex: 1, Choice: 0));
        Assert.AreEqual(Outside, session.State.QuestLog!.Scenes[^1].Map);
        Assert.AreEqual(CampaignQuestLogEvent.PartyOrder, session.State.QuestLog.Scenes[^1].Actions.Single().Event);
        return session;
    }

    private void AssertSourceRoute()
    {
        var cave = new MapId(Cave);
        var outside = new MapId(Outside);
        Assert.AreEqual(new FieldWarp(3, 18, 37, 3, outside, 0), content.Maps[cave].FieldWarps[3]);
        Assert.AreEqual(new FieldWarp(0, 19, 5, 3, cave, 3), content.Maps[outside].FieldWarps[0]);
        Assert.AreEqual(new FieldMetatileEdit(18, 37, 0x287, 0, 3), content.SourceMetatileAt(cave, 18, 37));
        Assert.AreEqual(new FieldMetatileEdit(19, 5, 0x0a9, 0, 3), content.SourceMetatileAt(outside, 19, 5));
        Assert.AreEqual(new FieldMetatileEdit(19, 6, 0x0d9, 0, 3), content.SourceMetatileAt(outside, 19, 6));
        Assert.AreEqual(0x65, content.Maps[cave].SourceBehaviorAt(18, 37));
        Assert.AreEqual(0x60, content.Maps[outside].SourceBehaviorAt(19, 5));
        Assert.AreEqual(0x00, content.Maps[outside].SourceBehaviorAt(19, 6));
        var from = content.MapDefinition(cave);
        var to = content.MapDefinition(outside);
        Assert.AreEqual("MAP_TYPE_UNDERGROUND", from.MapType);
        Assert.AreEqual("MAP_TYPE_ROUTE", to.MapType);
        Assert.AreEqual("MAPSEC_MT_MOON", from.RegionMapSection);
        Assert.AreEqual("MAPSEC_ROUTE_4", to.RegionMapSection);
        Assert.AreEqual("WEATHER_NONE", from.Weather);
        Assert.AreEqual("WEATHER_SUNNY", to.Weather);
        Assert.AreEqual("MUS_MT_MOON", from.Music);
        Assert.AreEqual("MUS_ROUTE3", to.Music);
        Assert.IsTrue(from.HasCavePreview);
        Assert.IsFalse(to.HasCavePreview);
        Assert.IsFalse(from.RequiresFlash);
        Assert.IsFalse(to.RequiresFlash);
        Assert.AreEqual("MtMoon_1F_OnTransition", from.Programs.OnTransition);
        Assert.IsNull(to.Programs.OnTransition);
        Assert.IsFalse(content.QuestLogSource.ArrivalFlags.Contains(Visited));
        var departure = content.QuestLogSource.Departures.Single(d => d.Inside == Cave);
        Assert.AreEqual(Outside, departure.Outside);
        Assert.AreEqual(9, departure.LocationId);
        Assert.AreEqual("QL_LOCATION_MT_MOON_1", departure.LocationSymbol);
        Assert.AreEqual("ManagedToGetOutOfLocation", departure.TextKey);
    }

    private static void AssertStable(CampaignState state)
    {
        Assert.IsTrue(state.Script.Completed);
        Assert.IsFalse(state.Script.Locked);
        Assert.IsNull(state.Script.Wait);
        Assert.IsTrue(state.EntryPrograms.IsEmpty);
        Assert.IsTrue(state.Motions.IsEmpty);
        Assert.IsNull(state.Dialogue);
        Assert.IsNull(state.WildBattle);
    }

    private void AssertWarpRules(CampaignState before, CampaignState after, int steps)
    {
        var expected = SourceRoamerRules.Transition(before.Roamer!, RoamerTransition.Warp,
            content.CelioRoamerMaps![after.World.Player.MapId], before.World.Rng);
        Assert.AreEqual(before.World.StepCount + steps, after.World.StepCount);
        Assert.AreEqual(before.World.Revision + 1, after.World.Revision);
        Assert.AreEqual(expected.Rng, after.World.Rng);
        Assert.IsTrue(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected.State),
            JsonSerializer.SerializeToElement(after.Roamer)));
        Assert.AreEqual(JsonSerializer.Serialize(before.Party), JsonSerializer.Serialize(after.Party));
        Assert.AreEqual(JsonSerializer.Serialize(before.Inventory), JsonSerializer.Serialize(after.Inventory));
        AssertStable(after);
    }

    private (CampaignSession Session, CampaignState BeforeExit, FieldTransitionReceipt Exit) PublicRoundTrip(
        Direction initialFacing = Direction.North)
    {
        AssertSourceRoute();
        var session = AtOutsideDoor(initialFacing);
        var outside = session.State;
        Send(session, new("move", Direction.North));
        var inside = session.State;
        var enter = inside.FieldTransition;
        Assert.IsNotNull(enter);
        Assert.AreEqual(outside.World.Player, enter.From);
        Assert.AreEqual(FieldTransitionCause.NonAnimatedDoor, enter.Cause);
        Assert.AreEqual(FieldTransitionDeparture.StepOntoWarp, enter.Departure);
        Assert.AreEqual(FieldTransitionArrival.Plain, enter.Arrival);
        Assert.AreEqual(new TilePosition(new(Outside), 19, 5, 3, Direction.North), enter.DepartureStep);
        Assert.AreEqual(new TilePosition(new(Cave), 18, 37, 3, Direction.North), enter.To);
        Assert.AreEqual(enter.To, enter.ArrivalAnchor);
        Assert.AreEqual(enter.To, inside.World.Player);
        AssertWarpRules(outside, inside, 1);
        Assert.IsTrue(inside.Script.Flags.GetValueOrDefault(Visited));
        Assert.IsTrue(inside.Script.Flags.GetValueOrDefault(CampaignQuestLogDepartureCapture.PendingFlag));
        Assert.AreEqual(9, inside.Script.Variables.GetValueOrDefault(CampaignQuestLogDepartureCapture.EntranceVariable));
        Assert.IsTrue(session.CanPresentFieldTransition(outside, enter),
            "The already-visited Mt. Moon destination still qualifies for its source cave preview.");
        Assert.AreEqual(FieldTransitionInterlude.CavePreviewEnter, session.FieldTransitionInterludeFor(enter));
        Assert.AreEqual(40, session.FieldTransitionPreviewDuration(outside, enter));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(enter,
            FieldDoorPresentation.MinimumStepDepartureFrames,
            FieldDoorPresentation.MinimumStepDepartureFrames + FieldWarpPresentation.PlainArrivalFrames));
        Assert.IsNull(LastTransition(inside).TransitionTiming);
        StrictRoundtrip(session);

        Send(session, new("turn", Direction.South));
        Assert.AreEqual(inside.World.Player with { Facing = Direction.South }, session.State.World.Player);
        Assert.AreEqual(inside.World.StepCount, session.State.World.StepCount);
        Assert.AreEqual(inside.World.Rng, session.State.World.Rng);
        // Let the real party-order caption finish using the ordinary field clock.
        // Keep it and the unknown preview marker intact in the recorded scene.
        for (ulong frame = 1; frame <= 256; frame++) session.ObserveQuestLogFieldFrame(frame);
        var beforeExit = session.State;
        Send(session, new("move", Direction.South));
        var exited = session.State;
        var exit = exited.FieldTransition;
        Assert.IsNotNull(exit);
        Assert.AreEqual(beforeExit.World.Player, exit.From);
        Assert.AreEqual(FieldTransitionCause.PlainWarp, exit.Cause);
        Assert.AreEqual(FieldTransitionDeparture.Immediate, exit.Departure);
        Assert.AreEqual(FieldTransitionArrival.NonAnimatedDoor, exit.Arrival);
        Assert.IsNull(exit.DepartureStep);
        Assert.IsNull(exit.DepartureStairBehavior);
        Assert.IsNull(exit.ArrivalStairBehavior);
        Assert.AreEqual(new TilePosition(new(Outside), 19, 5, 3, Direction.South), exit.ArrivalAnchor);
        Assert.AreEqual(new TilePosition(new(Outside), 19, 6, 3, Direction.South), exit.To);
        Assert.AreEqual(exit.To, exited.World.Player);
        AssertWarpRules(beforeExit, exited, 0);
        Assert.AreEqual(outside.World.StepCount + 1, exited.World.StepCount,
            "Only the public cave entrance is a gameplay step; cosmetic emergence cannot add one.");
        Assert.IsTrue(exited.Script.Flags.GetValueOrDefault(Visited));
        Assert.IsFalse(exited.Script.Flags.GetValueOrDefault(CampaignQuestLogDepartureCapture.PendingFlag));
        Assert.AreEqual(9, exited.Script.Variables.GetValueOrDefault(CampaignQuestLogDepartureCapture.EntranceVariable));
        Assert.IsNull(CampaignQuestLogRecorder.FirstArrival(beforeExit, exited, content.QuestLogSource));
        Assert.IsTrue(session.CanPresentFieldTransition(beforeExit, exit));
        Assert.IsFalse(session.CanPresentFieldTransition(exit));
        Assert.IsFalse(session.CanPresentFieldTransition(beforeExit, exit with { }));
        Assert.IsFalse(session.CanPresentFieldTransition(beforeExit with { IsCycling = true }, exit));
        Assert.IsFalse(session.CanPresentFieldTransition(beforeExit with { IsSurfing = true }, exit));
        Assert.AreEqual(FieldTransitionInterlude.CaveExit, session.FieldTransitionInterludeFor(exit));
        var scene = exited.QuestLog!.Scenes[^1];
        Assert.AreSame(LastTransition(exited), scene.Actions[^2]);
        var caption = scene.Actions[^1];
        Assert.AreEqual(CampaignQuestLogEvent.Departed, caption.Event);
        Assert.AreEqual("ManagedToGetOutOfLocation", caption.TextKey);
        Assert.AreEqual(9, caption.LocationId);
        Assert.AreEqual(127, caption.Frames);
        CollectionAssert.AreEqual(new[] { "MT. MOON", "MT. MOON" }, caption.Arguments.ToArray());
        Assert.AreEqual(LastTransition(exited).StartFrame + 1, caption.StartFrame);
        Assert.IsNull(caption.Transition);
        StrictRoundtrip(session);
        return (session, beforeExit, exit);
    }

    [TestMethod]
    [DataRow(Direction.North, 0)]
    [DataRow(Direction.North, 7)]
    [DataRow(Direction.East, 0)]
    [DataRow(Direction.East, 9)]
    public void PublicVisitedCaveEntryAndExitRecordTheNaturalDepartureCaptionAndStrictlyContinue(
        Direction initialFacing, int musicWait)
    {
        var (session, _, receipt) = PublicRoundTrip(initialFacing);
        var state = session.State;
        var original = JsonSerializer.Serialize(state);
        long revision = session.Revision;
        var scene = state.QuestLog!.Scenes[^1];
        var marker = LastTransition(state);
        var caption = scene.Actions[^1];
        Assert.IsNull(marker.TransitionTiming);
        var unknown = new CampaignQuestLogPlayback(new([scene]));
        unknown.Tick(marker.StartFrame / 60d);
        Assert.IsNull(unknown.ActiveTransitionTiming);
        Assert.AreEqual(Outside, unknown.Map, "An old unobserved exit marker remains unknown.");
        int departureEnd = FieldWarpPresentation.MinimumDepartureFrames + musicWait;
        int change = departureEnd + FieldCaveTransitionPresentation.ExitFrames;
        int end = change + FieldDoorPresentation.NonAnimatedArrivalFrames;
        Assert.AreEqual(62 + musicWait, change);
        Assert.AreEqual(103 + musicWait, end);
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt with { }, change, end));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, 61, 102));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end - 1));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end + 1));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change + 1, end + 1));
        var saved = session.PrepareFieldSave();
        var recordedScene = saved.QuestLog!.Scenes[^1];
        var recordedMarker = LastTransition(saved);
        var timing = recordedMarker.TransitionTiming;
        Assert.IsNotNull(timing);
        Assert.AreEqual(FieldTransitionInterlude.CaveExit, timing.Interlude);
        Assert.AreEqual(FieldTransitionCause.PlainWarp, timing.Cause);
        Assert.AreEqual(FieldTransitionDeparture.Immediate, timing.Departure);
        Assert.AreEqual(FieldTransitionArrival.NonAnimatedDoor, timing.Arrival);
        Assert.AreEqual(38, timing.InterludeFrames);
        Assert.AreEqual(departureEnd, timing.DepartureEndFrame);
        Assert.AreEqual(change, timing.SwitchFrame);
        Assert.AreEqual(end, timing.EndFrame);
        Assert.AreEqual(new CampaignQuestLogDoorAnchor(19, 5), timing.ArrivalDoor);
        Assert.IsNull(timing.DepartureDoor);
        Assert.IsNull(timing.DepartureStairBehavior);
        Assert.IsNull(timing.ArrivalStairBehavior);
        Assert.AreEqual(marker with { Frames = end, TransitionTiming = timing }, recordedMarker);
        Assert.AreEqual(caption with { StartFrame = marker.StartFrame + end }, recordedScene.Actions[^1]);
        Assert.AreEqual(scene.Actions.Length, recordedScene.Actions.Length);
        for (int i = 0; i < scene.Actions.Length - 2; i++) Assert.AreSame(scene.Actions[i], recordedScene.Actions[i]);
        Assert.AreEqual(JsonSerializer.Serialize(state with { QuestLog = saved.QuestLog }), JsonSerializer.Serialize(saved));
        Assert.AreEqual(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(session.PrepareFieldSave()));
        Assert.IsNull(CampaignQuestLogRecorder.Validate(saved.QuestLog, content.QuestLogSource, content));
        Assert.IsNull(session.ValidatePlayerSave(saved));
        string timingJson = JsonSerializer.Serialize(timing);
        Assert.IsTrue(timingJson.Contains("\"Interlude\":1", StringComparison.Ordinal));
        Assert.IsFalse(timingJson.Contains("InterludeFrames", StringComparison.Ordinal));
        Assert.IsFalse(timingJson.Contains("DepartureEndFrame", StringComparison.Ordinal));
        Assert.AreEqual(1, CampaignQuestLogRecorder.Budget([recordedMarker]) -
            CampaignQuestLogRecorder.Budget([recordedMarker with { TransitionTiming = timing with { Interlude = FieldTransitionInterlude.None } }]));

        int markerIndex = recordedScene.Actions.IndexOf(recordedMarker);
        foreach (var malformed in new[]
        {
            timing with { Interlude = (FieldTransitionInterlude)99 },
            timing with { Departure = FieldTransitionDeparture.StepOntoWarp },
            timing with { DepartureDoor = new(18, 36) },
            timing with { ArrivalDoor = null },
            timing with { ArrivalDoor = new(19, 6) },
            timing with { DepartureStairBehavior = 0x6c },
            timing with { ArrivalStairBehavior = 0x6c },
            timing with { SwitchFrame = 61, EndFrame = 102 },
            timing with { EndFrame = end - 1 }
        })
        {
            var invalidScene = recordedScene with
            {
                Actions = recordedScene.Actions.SetItem(markerIndex,
                    recordedMarker with { Frames = malformed.EndFrame, TransitionTiming = malformed })
            };
            var invalidLog = saved.QuestLog with
            {
                Scenes = saved.QuestLog.Scenes.SetItem(saved.QuestLog.Scenes.Length - 1, invalidScene)
            };
            Assert.AreEqual("save_quest_log_transition_timing_invalid",
                CampaignQuestLogRecorder.Validate(invalidLog, content.QuestLogSource, content),
                JsonSerializer.Serialize(malformed));
        }

        var bytes = SaveCodec.Encode(saved, revision, session.RulesVersion, content.ContentVersion);
        var decoded = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, content.ContentVersion,
            validate: session.ValidatePlayerSave);
        var restored = CampaignSession.RestoreCurrent(content, decoded.State, decoded.Generation);
        Assert.IsTrue(System.Text.Json.Nodes.JsonNode.DeepEquals(JsonSerializer.SerializeToNode(saved),
            JsonSerializer.SerializeToNode(restored.State)), "Strict Continue preserves the complete persisted payload.");
        Assert.IsFalse(restored.CompleteFieldTransitionPresentation(receipt, change, end));
        var playback = new CampaignQuestLogPlayback(new([restored.State.QuestLog!.Scenes[^1]]), state.PlayerName, content.QuestLogSource);
        playback.Tick(recordedMarker.StartFrame / 60d);
        var departure = new FieldWarpPresentation.Departure();
        var arrival = new FieldDoorPresentation.NonAnimatedArrival();
        for (int frame = 0; frame < end; frame++)
        {
            Assert.AreEqual(timing, playback.ActiveTransitionTiming);
            Assert.AreEqual(frame, playback.TransitionElapsedFrames, .00001);
            var expected = frame < change ? receipt.From : receipt.To;
            Assert.AreEqual(expected.MapId.Value, playback.Map);
            var player = playback.Actors.Single(a => a.Id == CampaignQuestLogRecorder.PlayerId);
            Assert.AreEqual((expected.X, expected.Y, expected.Facing), (player.X, player.Y, player.Facing));
            Assert.IsNull(playback.Caption, "The authenticated departure caption waits for the full exit release.");
            if (frame < departureEnd || frame >= change)
            {
                var live = frame < departureEnd ? FieldDoorPresentation.FromPlain(departure.Sample) : arrival.Sample;
                Assert.AreEqual(live, FieldDoorPresentation.SampleRecorded(timing.Cause, timing.Arrival, change, frame,
                    timing.DepartureStairBehavior, timing.ArrivalStairBehavior, timing.EffectiveDeparture, timing.Interlude));
                Assert.IsFalse(live.DoorAnimationActive);
                Assert.IsFalse(live.OpenDoorSoundRequested);
                Assert.IsFalse(live.WipeActive);
                if (frame < departureEnd) Assert.AreEqual(0, live.WalkPixels);
            }
            // The independent source-derived controller tests own the middle 38 callbacks.
            if (frame < departureEnd) departure.Advance(frame + 1 >= departureEnd - 1);
            else if (frame >= change) arrival.Advance();
            playback.Tick(1d / 60);
        }
        Assert.IsTrue(departure.Complete);
        Assert.IsTrue(arrival.Complete);
        Assert.AreEqual(16, arrival.Sample.WalkPixels);
        Assert.IsNull(playback.ActiveTransitionTiming);
        Assert.AreEqual(Outside, playback.Map);
        string expectedCaption = content.QuestLogSource.Expand(caption.TextKey!, state.PlayerName, caption.Arguments);
        Assert.AreEqual("Managed to get out of MT. MOON\nafter wandering about.", expectedCaption);
        Assert.AreEqual(expectedCaption, playback.Caption);
        // The original 127-frame record expands to the source's 255-frame text bucket.
        for (int frame = 1; frame < 255; frame++)
        {
            playback.Tick(1d / 60);
            Assert.AreEqual(expectedCaption, playback.Caption);
        }
        playback.Tick(1d / 60);
        Assert.IsNull(playback.Caption);
        Assert.IsTrue(playback.Finished);
        Assert.AreSame(state, session.State);
        Assert.AreEqual(original, JsonSerializer.Serialize(session.State));
        Assert.AreEqual(revision, session.Revision);
        Assert.IsNull(marker.TransitionTiming);

        Send(session, new("play_time_tick", Choice: 1));
        Assert.AreEqual(revision + 1, session.Revision);
        Assert.AreEqual(JsonSerializer.Serialize(saved.QuestLog), JsonSerializer.Serialize(session.State.QuestLog));
        Assert.AreEqual(state.World, session.State.World);
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end));
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void CaveExitOverflowRetainsTheWholeUnknownMarkerAndNaturalDepartureCaption()
    {
        var (session, _, receipt) = PublicRoundTrip();
        var state = session.State;
        long revision = session.Revision;
        string unchanged = JsonSerializer.Serialize(state);
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, 250, 291));
        var saved = session.PrepareFieldSave();
        Assert.AreEqual(JsonSerializer.Serialize(CampaignQuestLogRecorder.Close(state.QuestLog!)), JsonSerializer.Serialize(saved.QuestLog));
        Assert.IsTrue(saved.QuestLog!.Scenes[^1].Closed);
        Assert.AreSame(LastTransition(state), LastTransition(saved));
        Assert.AreSame(state.QuestLog!.Scenes[^1].Actions[^1], saved.QuestLog.Scenes[^1].Actions[^1]);
        Assert.IsNull(LastTransition(saved).TransitionTiming);
        Assert.AreEqual(state.World, saved.World);
        Assert.AreEqual(unchanged, JsonSerializer.Serialize(session.State));
        Assert.AreEqual(revision, session.Revision);
        Assert.IsNull(session.ValidatePlayerSave(saved));
        Send(session, new("play_time_tick", Choice: 1));
        Assert.AreEqual(JsonSerializer.Serialize(saved.QuestLog), JsonSerializer.Serialize(session.State.QuestLog));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, 250, 291));
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CaveExitCompletionRejectsAStaleReceiptOrReplacedImmutableScene(bool replaceScene)
    {
        var (session, _, receipt) = PublicRoundTrip();
        if (replaceScene)
        {
            var state = session.State;
            var log = state.QuestLog!;
            SetState(session, state with { QuestLog = new(log.Scenes.SetItem(log.Scenes.Length - 1, log.Scenes[^1] with { })) });
        }
        else Send(session, new("party_swap", PartyIndex: 1, Choice: 0));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, 62, 103));
        Assert.IsNull(LastTransition(session.State).TransitionTiming);
        Assert.AreEqual(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(session.PrepareFieldSave()));
    }

    [TestMethod]
    [DataRow("text")]
    [DataRow("argument")]
    [DataRow("location")]
    [DataRow("repeat")]
    [DataRow("start")]
    [DataRow("duration")]
    [DataRow("input")]
    [DataRow("second-caption")]
    [DataRow("missing-producer-event")]
    public void CaveExitObservationRejectsAChangedCaptionOrMissingProducerEvidence(string mutation)
    {
        var (session, before, receipt) = PublicRoundTrip();
        var state = session.State;
        var log = state.QuestLog!;
        var scene = log.Scenes[^1];
        var caption = scene.Actions[^1];
        CampaignQuestLogAction? producer = caption with { StartFrame = 0 };
        var actions = scene.Actions;
        switch (mutation)
        {
            case "text": actions = actions.SetItem(actions.Length - 1, caption with { TextKey = "SavedGameAtLocation" }); break;
            case "argument": actions = actions.SetItem(actions.Length - 1, caption with { Arguments = ["ROUTE 4", "MT. MOON"] }); break;
            case "location": actions = actions.SetItem(actions.Length - 1, caption with { LocationId = 10 }); break;
            case "repeat": actions = actions.SetItem(actions.Length - 1, caption with { Repeat = true }); break;
            case "start": actions = actions.SetItem(actions.Length - 1, caption with { StartFrame = caption.StartFrame - 1 }); break;
            case "duration": actions = actions.SetItem(actions.Length - 1, caption with { Frames = 126 }); break;
            case "input": actions = actions.Add(new(-1, 0, 0, Direction.South, false, CampaignQuestLogEvent.Input, 1) { StartFrame = caption.StartFrame }); break;
            case "second-caption": actions = actions.Add(caption with { }); break;
            case "missing-producer-event": producer = null; break;
            default: Assert.Fail("Unknown malformed departure-caption case."); break;
        }
        state = state with { QuestLog = new(log.Scenes.SetItem(log.Scenes.Length - 1, scene with { Actions = actions })) };
        SetState(session, state);
        // Fault injection targets the private observation boundary, with the real
        // public command receipt and its separate, unchanged producer event.
        typeof(CampaignSession).GetMethod("CaptureFieldTransitionObservation", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(session, [before, state, new CampaignCommand("move", Direction.South), producer]);
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, 62, 103));
        Assert.IsNull(LastTransition(state).TransitionTiming);
        Assert.AreEqual(JsonSerializer.Serialize(state), JsonSerializer.Serialize(session.PrepareFieldSave()));
    }
}

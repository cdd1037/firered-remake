using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Application;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class CampaignQuestLogOrdinaryDoorRouteTests
{
    // Only the initial stable field position/story context is controlled. Both
    // receipts, destination scripts and recording markers use public commands.
    // Door-art bindings and pixels are independently checked by Content/import tests.
    private static CampaignSession AtViridianDoor(int x, int doorY, bool visitedCity = true)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var session = CampaignSession.CreateCurrent(catalog);
        var map = catalog.MapFromSourceSymbol("MAP_VIRIDIAN_CITY");
        Assert.AreEqual("map.viridian-city", map.Value);
        Assert.AreEqual(0x69, catalog.Maps[map].SourceBehaviorAt(x, doorY));
        var state = session.State;
        state = state with
        {
            World = state.World with
            {
                Player = new(map, x, doorY + 1, catalog.Maps[map].GetTile(x, doorY + 1)!.Elevation, Direction.North)
            },
            Actors = ImmutableSortedDictionary<string, CampaignActor>.Empty,
            ObjectSlots = null,
            Script = state.Script with
            {
                Completed = true, Locked = false, Wait = null, Stack = [],
                Variables = state.Script.Variables.SetItem("VAR_MAP_SCENE_VIRIDIAN_CITY_OLD_MAN", 2),
                // A stable position already in Viridian normally follows its
                // OnTransition callback, which has set this first-arrival flag.
                Flags = state.Script.Flags.SetItem("FLAG_WORLD_MAP_VIRIDIAN_CITY", visitedCity)
            },
            EntryPrograms = [], ArrivalFacing = null, Dialogue = null, Motions = [],
            PresentationTicks = 0, PresentationEffects = [], MapLayout = null, MapEdits = null,
            PlayerVisible = true, IsCycling = false, IsSurfing = false, FlashLevel = 0,
            FieldWeather = null, Music = catalog.MapDefinition(map).Music
        };
        state = state with { QuestLog = new([CampaignQuestLogRecorder.SavedScene(state, catalog.QuestLogSource)]) };
        typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, catalog.ContentVersion, session.Revision));
        typeof(CampaignSession).GetField("_questLogRecordingOpen", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, true);
        return session;
    }

    private static CampaignQuestLogAction LastTransition(CampaignState state) =>
        state.QuestLog!.Scenes[^1].Actions.Last(action => action.Transition is not null);

    private static FieldTransitionReceipt MoveThroughDoor(CampaignSession session, Direction direction,
        MapId destination, FieldTransitionCause cause, FieldTransitionArrival arrival)
    {
        var before = session.State;
        var result = session.Execute("ordinary-door-move:" + session.Revision, session.Revision, new("move", direction));
        Assert.AreEqual(CommandStatus.Accepted, result.Status, result.Error);
        var receipt = result.State.FieldTransition;
        Assert.IsNotNull(receipt);
        Assert.AreEqual(before.World.Player, receipt.From);
        Assert.AreEqual(destination, receipt.To.MapId);
        Assert.AreEqual(receipt.To, result.State.World.Player);
        Assert.AreEqual(FieldTransitionDeparture.Immediate, receipt.Departure);
        Assert.AreEqual(cause, receipt.Cause);
        Assert.AreEqual(arrival, receipt.Arrival);
        Assert.IsTrue(session.CanPresentFieldTransition(before, receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(receipt), "Animated branches require the actual origin snapshot.");
        Assert.IsTrue(result.State.Script.Completed);
        Assert.IsFalse(result.State.Script.Locked);
        Assert.IsNull(result.State.Script.Wait);
        Assert.IsTrue(result.State.EntryPrograms.IsEmpty);
        Assert.IsNull(result.State.Dialogue);
        Assert.IsTrue(result.State.Motions.IsEmpty);
        Assert.IsNull(LastTransition(result.State).TransitionTiming);
        Assert.AreEqual(before.World.Rng, result.State.World.Rng);
        Assert.AreEqual(before.World.StepCount, result.State.World.StepCount);
        return receipt;
    }

    private static CampaignQuestLogTransitionTiming ObserveAndReplay(CampaignSession session, FieldTransitionReceipt receipt,
        CampaignQuestLogDoorAnchor? departureDoor, CampaignQuestLogDoorAnchor? arrivalDoor, int musicWait)
    {
        var state = session.State;
        var revision = session.Revision;
        var unchanged = JsonSerializer.Serialize(state);
        bool animatedDeparture = receipt.Cause == FieldTransitionCause.AnimatedDoor;
        bool animatedArrival = receipt.Arrival == FieldTransitionArrival.AnimatedDoor;
        int change = (animatedDeparture ? FieldDoorPresentation.MinimumDepartureFrames : FieldWarpPresentation.MinimumDepartureFrames) + musicWait;
        int end = change + (animatedArrival ? FieldDoorPresentation.AnimatedArrivalFrames : FieldWarpPresentation.PlainArrivalFrames);
        Assert.AreSame(LastTransition(state), state.QuestLog!.Scenes[^1].Actions[^1],
            "Completed timing requires the real map marker to be the last row, without a caption or script tail.");
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt with { }, change, end));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end));
        var saved = session.PrepareFieldSave();
        var marker = LastTransition(saved);
        var timing = marker.TransitionTiming;
        Assert.IsNotNull(timing);
        Assert.AreEqual(receipt.Cause, timing.Cause);
        Assert.AreEqual(receipt.Arrival, timing.Arrival);
        Assert.AreEqual(departureDoor, timing.DepartureDoor);
        Assert.AreEqual(arrivalDoor, timing.ArrivalDoor);
        Assert.AreEqual(change, timing.SwitchFrame);
        Assert.AreEqual(end, timing.EndFrame);
        Assert.AreEqual(end, marker.Frames);
        Assert.AreEqual(receipt.To, saved.World.Player);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(saved.QuestLog,
            CurrentCampaignFixture.Catalog.QuestLogSource, CurrentCampaignFixture.Catalog));

        var log = saved.QuestLog!;
        var recorded = JsonSerializer.Serialize(log);
        var playback = new CampaignQuestLogPlayback(log);
        playback.Tick(marker.StartFrame / 60d);
        var doorDeparture = new FieldDoorPresentation.Departure();
        var plainDeparture = new FieldWarpPresentation.Departure();
        var doorArrival = new FieldDoorPresentation.AnimatedArrival();
        var plainArrival = new FieldWarpPresentation.PlainArrival();
        for (int frame = 0; frame < end; frame++)
        {
            Assert.AreEqual(timing, playback.ActiveTransitionTiming);
            Assert.AreEqual(frame, playback.TransitionElapsedFrames, .00001);
            var expected = frame < change ? receipt.From : receipt.To;
            Assert.AreEqual(expected.MapId.Value, playback.Map);
            var player = playback.Actors.Single(actor => actor.Id == CampaignQuestLogRecorder.PlayerId);
            Assert.AreEqual((expected.X, expected.Y, expected.Facing), (player.X, player.Y, player.Facing));
            var live = frame < change
                ? animatedDeparture ? doorDeparture.Sample : FieldDoorPresentation.FromPlain(plainDeparture.Sample)
                : animatedArrival ? doorArrival.Sample : FieldDoorPresentation.FromPlain(plainArrival.Sample);
            Assert.AreEqual(live, FieldDoorPresentation.SampleRecorded(receipt.Cause, receipt.Arrival,
                timing.SwitchFrame, (int)playback.TransitionElapsedFrames));
            if (frame < change)
            {
                bool ready = frame + 1 >= change - 1;
                if (animatedDeparture) doorDeparture.Advance(ready); else plainDeparture.Advance(ready);
            }
            else if (animatedArrival) doorArrival.Advance(); else plainArrival.Advance();
            playback.Tick(1d / 60);
        }
        Assert.IsNull(playback.ActiveTransitionTiming);
        Assert.AreEqual(receipt.To.MapId.Value, playback.Map);
        Assert.AreEqual(recorded, JsonSerializer.Serialize(log));
        Assert.AreSame(state, session.State);
        Assert.AreEqual(unchanged, JsonSerializer.Serialize(session.State));
        Assert.AreEqual(revision, session.Revision);
        Assert.IsNull(LastTransition(session.State).TransitionTiming, "Observation is projected into saves without rewriting command state.");
        return timing;
    }

    [TestMethod]
    [DataRow("MAP_VIRIDIAN_CITY_HOUSE", "map.viridian-house", 25, 11, 4, 7)]
    [DataRow("MAP_VIRIDIAN_CITY_POKEMON_CENTER_1F", "map.viridian-pokemon-center-1f", 26, 26, 7, 8)]
    public void PublicViridianDoorRoundTripRecordsBothBranchesWithoutMovingTheCommittedArrivalTwice(
        string interiorSymbol, string interiorId, int doorX, int doorY, int roomX, int roomY)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var interior = catalog.MapFromSourceSymbol(interiorSymbol);
        Assert.AreEqual(interiorId, interior.Value);
        var city = catalog.MapFromSourceSymbol("MAP_VIRIDIAN_CITY");
        var outdoor = catalog.MapDefinition(city);
        var indoor = catalog.MapDefinition(interior);
        Assert.AreEqual("MAPSEC_VIRIDIAN_CITY", outdoor.RegionMapSection);
        Assert.AreEqual(outdoor.RegionMapSection, indoor.RegionMapSection);
        Assert.AreEqual("WEATHER_SUNNY", outdoor.Weather);
        Assert.AreEqual("WEATHER_NONE", indoor.Weather);
        Assert.IsFalse(outdoor.RequiresFlash);
        Assert.IsFalse(indoor.RequiresFlash);
        Assert.AreEqual(0x65, catalog.Maps[interior].SourceBehaviorAt(roomX, roomY));
        var sourceWarp = catalog.Maps[city].FieldWarps.Single(warp => warp.X == doorX && warp.Y == doorY);
        Assert.AreEqual(interior, sourceWarp.DestinationMapId);
        Assert.AreEqual(1, sourceWarp.DestinationWarpId);
        Assert.AreEqual((roomX, roomY), (catalog.Maps[interior].FieldWarps[1].X, catalog.Maps[interior].FieldWarps[1].Y));

        var session = AtViridianDoor(doorX, doorY);
        var initial = session.State.World;
        var enter = MoveThroughDoor(session, Direction.North, interior, FieldTransitionCause.AnimatedDoor, FieldTransitionArrival.Plain);
        Assert.AreEqual((roomX, roomY, Direction.North), (enter.To.X, enter.To.Y, enter.To.Facing));
        Assert.AreEqual((interior, roomX, roomY), (enter.ArrivalAnchor.MapId, enter.ArrivalAnchor.X, enter.ArrivalAnchor.Y));
        if (interiorSymbol == "MAP_VIRIDIAN_CITY_POKEMON_CENTER_1F")
            Assert.AreEqual("HEAL_LOCATION_VIRIDIAN_CITY", session.State.LastHealingLocation);
        var entered = ObserveAndReplay(session, enter, new(doorX, doorY), null, musicWait: 7);

        // Turn and leave through the actual south-arrow exit. No relocation or
        // synthetic receipt is used between entering and returning outside.
        var turn = session.Execute("ordinary-door-turn", session.Revision, new("turn", Direction.South));
        Assert.AreEqual(CommandStatus.Accepted, turn.Status, turn.Error);
        Assert.AreEqual(entered, LastTransition(turn.State).TransitionTiming);
        Assert.AreEqual(enter.To with { Facing = Direction.South }, turn.State.World.Player);
        var leave = MoveThroughDoor(session, Direction.South, city, FieldTransitionCause.PlainWarp, FieldTransitionArrival.AnimatedDoor);
        Assert.AreEqual((city, doorX, doorY), (leave.ArrivalAnchor.MapId, leave.ArrivalAnchor.X, leave.ArrivalAnchor.Y));
        Assert.AreEqual((doorX, doorY + 1, Direction.South), (leave.To.X, leave.To.Y, leave.To.Facing));
        ObserveAndReplay(session, leave, null, new(doorX, doorY), musicWait: 0);
        Assert.AreEqual(initial.Player with { Facing = Direction.South }, session.State.World.Player);
        Assert.AreEqual(initial.Rng, session.State.World.Rng);
        Assert.AreEqual(initial.StepCount, session.State.World.StepCount);
        Assert.AreEqual(2, session.PrepareFieldSave().QuestLog!.Scenes.SelectMany(scene => scene.Actions)
            .Count(action => action.TransitionTiming is not null));
    }

    private static (CampaignSession Session, CampaignState BeforeReturn, FieldTransitionReceipt Receipt)
        ReturnToUnvisitedViridian(string interiorSymbol = "MAP_VIRIDIAN_CITY_HOUSE", int doorX = 25, int doorY = 11)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        // Only the initial story flag is controlled. The destination OnTransition
        // script and its first-arrival caption are produced by the public return.
        var session = AtViridianDoor(doorX, doorY, visitedCity: false);
        var enter = MoveThroughDoor(session, Direction.North, catalog.MapFromSourceSymbol(interiorSymbol),
            FieldTransitionCause.AnimatedDoor, FieldTransitionArrival.Plain);
        ObserveAndReplay(session, enter, new(doorX, doorY), null, musicWait: 0);
        var turn = session.Execute("ordinary-door-first-arrival-turn", session.Revision, new("turn", Direction.South));
        Assert.AreEqual(CommandStatus.Accepted, turn.Status, turn.Error);
        Assert.IsFalse(turn.State.Script.Flags.GetValueOrDefault("FLAG_WORLD_MAP_VIRIDIAN_CITY"));
        // Let the existing saved-location caption finish before this return. Keep
        // that caption intact, using the ordinary observed controllable-field clock.
        for (ulong frame = 1; frame <= 256; frame++) session.ObserveQuestLogFieldFrame(frame);
        var beforeReturn = session.State;
        var leave = MoveThroughDoor(session, Direction.South, catalog.MapFromSourceSymbol("MAP_VIRIDIAN_CITY"),
            FieldTransitionCause.PlainWarp, FieldTransitionArrival.AnimatedDoor);
        return (session, beforeReturn, leave);
    }

    private static void SetState(CampaignSession session, CampaignState state) =>
        typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion,
                CurrentCampaignFixture.Catalog.ContentVersion, session.Revision));

    [TestMethod]
    [DataRow("MAP_VIRIDIAN_CITY_HOUSE", 25, 11)]
    [DataRow("MAP_VIRIDIAN_CITY_POKEMON_CENTER_1F", 26, 26)]
    public void FirstArrivalCaptionWaitsForObservedDoorReleaseAndReconcilesExactlyOnce(string interiorSymbol, int doorX, int doorY)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var source = catalog.QuestLogSource;
        var (session, beforeReturn, leave) = ReturnToUnvisitedViridian(interiorSymbol, doorX, doorY);
        var state = session.State;
        var unchanged = JsonSerializer.Serialize(state);
        var revision = session.Revision;
        Assert.IsTrue(source.ArrivalFlags.Contains("FLAG_WORLD_MAP_VIRIDIAN_CITY"));
        Assert.IsTrue(state.Script.Flags.GetValueOrDefault("FLAG_WORLD_MAP_VIRIDIAN_CITY"));
        var scene = state.QuestLog!.Scenes[^1];
        var marker = LastTransition(state);
        var caption = scene.Actions[^1];
        Assert.AreSame(marker, scene.Actions[^2]);
        Assert.AreEqual(1, marker.Frames);
        Assert.AreEqual(CampaignQuestLogEvent.Arrived, caption.Event);
        Assert.AreEqual("ArrivedInLocation", caption.TextKey);
        Assert.AreEqual(127, caption.Frames);
        CollectionAssert.AreEqual(new[] { "VIRIDIAN CITY" }, caption.Arguments.ToArray());
        Assert.AreEqual(marker.StartFrame + 1, caption.StartFrame);
        Assert.IsNull(caption.Transition);
        var sourceCaption = CampaignQuestLogRecorder.FirstArrival(beforeReturn, state, source);
        Assert.IsNotNull(sourceCaption);
        Assert.AreEqual(JsonSerializer.Serialize(sourceCaption with { StartFrame = caption.StartFrame }),
            JsonSerializer.Serialize(caption));

        int change = FieldWarpPresentation.MinimumDepartureFrames + 7;
        int end = change + FieldDoorPresentation.AnimatedArrivalFrames;
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(leave with { }, change, end));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(leave, change, end));
        var saved = session.PrepareFieldSave();
        var recordedMarker = LastTransition(saved);
        var timing = recordedMarker.TransitionTiming;
        Assert.IsNotNull(timing);
        Assert.AreEqual(FieldTransitionCause.PlainWarp, timing.Cause);
        Assert.AreEqual(FieldTransitionArrival.AnimatedDoor, timing.Arrival);
        Assert.IsNull(timing.DepartureDoor);
        Assert.AreEqual(new CampaignQuestLogDoorAnchor(doorX, doorY), timing.ArrivalDoor);
        Assert.AreEqual(change, timing.SwitchFrame);
        Assert.AreEqual(end, timing.EndFrame);
        Assert.AreEqual(marker with { Frames = end, TransitionTiming = timing }, recordedMarker);
        var recordedScene = saved.QuestLog!.Scenes[^1];
        var recordedCaption = recordedScene.Actions[^1];
        Assert.AreEqual(marker.StartFrame + end, recordedCaption.StartFrame);
        Assert.AreEqual(caption.StartFrame + end - 1, recordedCaption.StartFrame);
        Assert.AreEqual(caption with { StartFrame = recordedCaption.StartFrame }, recordedCaption,
            "Only eligibility time shifts; the authentic caption payload and duration remain unchanged.");
        Assert.AreEqual(scene.Actions.Length, recordedScene.Actions.Length);
        for (int i = 0; i < scene.Actions.Length - 2; i++) Assert.AreSame(scene.Actions[i], recordedScene.Actions[i]);
        Assert.AreEqual(JsonSerializer.Serialize(state with { QuestLog = saved.QuestLog }), JsonSerializer.Serialize(saved));
        Assert.IsNull(CampaignQuestLogRecorder.Validate(saved.QuestLog, source, catalog));
        var projected = JsonSerializer.Serialize(saved);
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(leave, change, end));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(leave, change + 1, end + 1));
        Assert.AreEqual(projected, JsonSerializer.Serialize(session.PrepareFieldSave()), "Repeated completion must not shift the caption again.");

        var recorded = JsonSerializer.Serialize(saved.QuestLog);
        var decoded = JsonSerializer.Deserialize<CampaignQuestLog>(recorded)!;
        Assert.IsNull(CampaignQuestLogRecorder.Validate(decoded, source, catalog));
        var playback = new CampaignQuestLogPlayback(decoded, state.PlayerName, source);
        for (int frame = 0; frame < marker.StartFrame; frame++) playback.Tick(1d / 60);
        for (int frame = 0; frame < end; frame++)
        {
            Assert.AreEqual(timing, playback.ActiveTransitionTiming);
            Assert.AreEqual(frame, playback.TransitionElapsedFrames, .00001);
            Assert.IsNull(playback.Caption, $"The first-arrival caption is not eligible during warp frame {frame}.");
            var expected = frame < change ? leave.From : leave.To;
            Assert.AreEqual(expected.MapId.Value, playback.Map);
            var player = playback.Actors.Single(actor => actor.Id == CampaignQuestLogRecorder.PlayerId);
            Assert.AreEqual((expected.X, expected.Y, expected.Facing), (player.X, player.Y, player.Facing));
            playback.Tick(1d / 60);
        }
        Assert.IsNull(playback.ActiveTransitionTiming);
        Assert.AreEqual(leave.To.MapId.Value, playback.Map);
        string expectedCaption = source.Expand(caption.TextKey!, state.PlayerName, caption.Arguments);
        Assert.AreEqual("Arrived in VIRIDIAN CITY.", expectedCaption);
        Assert.AreEqual(expectedCaption, playback.Caption,
            "The existing caption interpreter displays the arrival at the full observed release boundary.");
        for (int frame = 1; frame < caption.Frames; frame++)
        {
            playback.Tick(1d / 60);
            Assert.AreEqual(expectedCaption, playback.Caption);
        }
        playback.Tick(1d / 60);
        Assert.IsNull(playback.Caption, "Observation preserves the source caption's complete 127-frame duration.");
        Assert.IsTrue(playback.Finished);
        Assert.AreEqual(recorded, JsonSerializer.Serialize(decoded));
        Assert.AreEqual(unchanged, JsonSerializer.Serialize(session.State));
        Assert.AreSame(state, session.State);
        Assert.AreEqual(revision, session.Revision);
        Assert.IsNull(LastTransition(state).TransitionTiming);
        Assert.AreEqual(leave.To, state.World.Player);
        Assert.AreEqual(beforeReturn.World.Rng, state.World.Rng);
        Assert.AreEqual(beforeReturn.World.StepCount, state.World.StepCount);

        var committed = session.Execute("ordinary-door-first-arrival-reconcile", session.Revision, new("turn", Direction.East));
        Assert.AreEqual(CommandStatus.Accepted, committed.Status, committed.Error);
        Assert.AreEqual(revision + 1, session.Revision);
        var committedActions = committed.State.QuestLog!.Scenes[^1].Actions;
        Assert.AreEqual(recordedScene.Actions.Length + 1, committedActions.Length);
        Assert.AreEqual(JsonSerializer.Serialize(recordedScene.Actions), JsonSerializer.Serialize(committedActions.RemoveAt(committedActions.Length - 1)));
        Assert.AreEqual(marker.StartFrame + end, committedActions[^1].StartFrame);
        Assert.AreEqual(leave.To with { Facing = Direction.East }, committed.State.World.Player);
        Assert.AreEqual(state.World.Rng, committed.State.World.Rng);
        Assert.AreEqual(state.World.StepCount, committed.State.World.StepCount);
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(leave, change, end));
        var second = session.Execute("ordinary-door-first-arrival-clock", session.Revision, new("play_time_tick", Choice: 1));
        Assert.AreEqual(CommandStatus.Accepted, second.Status, second.Error);
        Assert.AreEqual(JsonSerializer.Serialize(committed.State.QuestLog), JsonSerializer.Serialize(second.State.QuestLog));
        Assert.IsNull(CampaignQuestLogRecorder.Validate(second.State.QuestLog, source, catalog));
    }

    [TestMethod]
    public void FirstArrivalDurationOverflowClosesTheEntireOriginalUnknownMarkerAndCaption()
    {
        var (session, _, receipt) = ReturnToUnvisitedViridian();
        var state = session.State;
        var unchanged = JsonSerializer.Serialize(state);
        var revision = session.Revision;
        var original = state.QuestLog!;
        var scene = original.Scenes[^1];
        int end = 250 + FieldDoorPresentation.AnimatedArrivalFrames;
        Assert.IsTrue(end > byte.MaxValue);
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, 250, end));
        var saved = session.PrepareFieldSave();
        Assert.AreEqual(JsonSerializer.Serialize(CampaignQuestLogRecorder.Close(original)), JsonSerializer.Serialize(saved.QuestLog));
        Assert.IsTrue(saved.QuestLog!.Scenes[^1].Closed);
        Assert.AreSame(scene.Actions[^2], saved.QuestLog.Scenes[^1].Actions[^2]);
        Assert.AreSame(scene.Actions[^1], saved.QuestLog.Scenes[^1].Actions[^1]);
        Assert.AreEqual(1, LastTransition(saved).Frames);
        Assert.IsNull(LastTransition(saved).TransitionTiming);
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, 250, end));
        Assert.AreEqual(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(session.PrepareFieldSave()));
        Assert.AreSame(state, session.State);
        Assert.AreEqual(unchanged, JsonSerializer.Serialize(session.State));
        Assert.AreEqual(revision, session.Revision);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(saved.QuestLog, CurrentCampaignFixture.Catalog.QuestLogSource, CurrentCampaignFixture.Catalog));
        var committed = session.Execute("ordinary-door-caption-overflow", session.Revision, new("play_time_tick", Choice: 1));
        Assert.AreEqual(CommandStatus.Accepted, committed.Status, committed.Error);
        Assert.AreEqual(JsonSerializer.Serialize(saved.QuestLog), JsonSerializer.Serialize(committed.State.QuestLog));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, 250, end));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FirstArrivalObservationRejectsAStaleReceiptOrReplacedImmutableScene(bool replaceScene)
    {
        var (session, _, receipt) = ReturnToUnvisitedViridian();
        if (replaceScene)
        {
            var state = session.State;
            var log = state.QuestLog!;
            SetState(session, state with { QuestLog = new(log.Scenes.SetItem(log.Scenes.Length - 1, log.Scenes[^1] with { })) });
        }
        else
        {
            var result = session.Execute("ordinary-door-stale-caption", session.Revision, new("turn", Direction.East));
            Assert.AreEqual(CommandStatus.Accepted, result.Status, result.Error);
        }
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, FieldWarpPresentation.MinimumDepartureFrames,
            FieldWarpPresentation.MinimumDepartureFrames + FieldDoorPresentation.AnimatedArrivalFrames));
        Assert.IsNull(LastTransition(session.State).TransitionTiming);
        Assert.AreEqual(1, LastTransition(session.State).Frames);
        Assert.AreEqual(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(session.PrepareFieldSave()));
    }

    [TestMethod]
    [DataRow("text")]
    [DataRow("argument")]
    [DataRow("repeat")]
    [DataRow("start")]
    [DataRow("duration")]
    [DataRow("input")]
    [DataRow("visual")]
    [DataRow("second-caption")]
    [DataRow("no-arrival-flag")]
    public void FirstArrivalObservationRejectsAnyUnauthenticatedCaptionTail(string mutation)
    {
        var (session, before, receipt) = ReturnToUnvisitedViridian();
        var state = session.State;
        var log = state.QuestLog!;
        var scene = log.Scenes[^1];
        var caption = scene.Actions[^1];
        var actions = scene.Actions;
        switch (mutation)
        {
            case "text": actions = actions.SetItem(actions.Length - 1, caption with { TextKey = "SavedGameAtLocation" }); break;
            case "argument": actions = actions.SetItem(actions.Length - 1, caption with { Arguments = ["PALLET TOWN"] }); break;
            case "repeat": actions = actions.SetItem(actions.Length - 1, caption with { Repeat = true }); break;
            case "start": actions = actions.SetItem(actions.Length - 1, caption with { StartFrame = caption.StartFrame - 1 }); break;
            case "duration": actions = actions.SetItem(actions.Length - 1, caption with { Frames = 126 }); break;
            case "input": actions = actions.Add(new(-1, 0, 0, Direction.South, false, CampaignQuestLogEvent.Input, 1) { StartFrame = caption.StartFrame }); break;
            case "visual":
                var projection = LastTransition(state).Transition!;
                int player = projection.Actors.IndexOf(projection.Actors.Single(actor => actor.Id == CampaignQuestLogRecorder.PlayerId));
                actions = actions.Add(new(player, receipt.To.X, receipt.To.Y, receipt.To.Facing, true, CampaignQuestLogEvent.None, 1) { StartFrame = caption.StartFrame });
                break;
            case "second-caption": actions = actions.Add(caption with { }); break;
            case "no-arrival-flag": before = before with { Script = before.Script with { Flags = before.Script.Flags.SetItem("FLAG_WORLD_MAP_VIRIDIAN_CITY", true) } }; break;
            default: Assert.Fail("Unknown malformed-tail test case."); break;
        }
        state = state with { QuestLog = new(log.Scenes.SetItem(log.Scenes.Length - 1, scene with { Actions = actions })) };
        SetState(session, state);
        // Re-run the observation boundary with the real public command's receipt.
        // Reflection only injects the malformed recording/story proof under test.
        typeof(CampaignSession).GetMethod("CaptureFieldTransitionObservation", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(session, [before, state, new CampaignCommand("move", Direction.South)]);
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, FieldWarpPresentation.MinimumDepartureFrames,
            FieldWarpPresentation.MinimumDepartureFrames + FieldDoorPresentation.AnimatedArrivalFrames));
        Assert.IsNull(LastTransition(session.State).TransitionTiming);
        Assert.AreEqual(1, LastTransition(session.State).Frames);
        Assert.AreEqual(JsonSerializer.Serialize(state), JsonSerializer.Serialize(session.PrepareFieldSave()));
    }
}

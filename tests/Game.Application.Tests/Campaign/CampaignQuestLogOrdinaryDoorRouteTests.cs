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
            Assert.AreEqual(live, FieldDoorPresentation.SampleRecorded(animatedDeparture, animatedArrival,
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

    [TestMethod]
    [DataRow("MAP_VIRIDIAN_CITY_HOUSE", 25, 11)]
    [DataRow("MAP_VIRIDIAN_CITY_POKEMON_CENTER_1F", 26, 26)]
    public void FirstArrivalCaptionAfterTheReturnMarkerLeavesDoorTimingUnknown(string interiorSymbol, int doorX, int doorY)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        // Deliberately isolate the unvisited-city story branch. The resulting
        // first-arrival caption is authentic, but is outside timing admission.
        var session = AtViridianDoor(doorX, doorY, visitedCity: false);
        var initial = session.State.World;
        var enter = MoveThroughDoor(session, Direction.North, catalog.MapFromSourceSymbol(interiorSymbol),
            FieldTransitionCause.AnimatedDoor, FieldTransitionArrival.Plain);
        ObserveAndReplay(session, enter, new(doorX, doorY), null, musicWait: 0);
        var turn = session.Execute("ordinary-door-first-arrival-turn", session.Revision, new("turn", Direction.South));
        Assert.AreEqual(CommandStatus.Accepted, turn.Status, turn.Error);
        Assert.IsFalse(turn.State.Script.Flags.GetValueOrDefault("FLAG_WORLD_MAP_VIRIDIAN_CITY"));
        var leave = MoveThroughDoor(session, Direction.South, catalog.MapFromSourceSymbol("MAP_VIRIDIAN_CITY"),
            FieldTransitionCause.PlainWarp, FieldTransitionArrival.AnimatedDoor);
        var state = session.State;
        var unchanged = JsonSerializer.Serialize(state);
        var revision = session.Revision;
        Assert.IsTrue(state.Script.Flags.GetValueOrDefault("FLAG_WORLD_MAP_VIRIDIAN_CITY"));
        var scene = state.QuestLog!.Scenes[^1];
        var marker = LastTransition(state);
        Assert.AreSame(marker, scene.Actions[^2]);
        Assert.AreEqual(CampaignQuestLogEvent.Arrived, scene.Actions[^1].Event);
        Assert.AreEqual("ArrivedInLocation", scene.Actions[^1].TextKey);
        Assert.IsNull(scene.Actions[^1].Transition);
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(leave, FieldWarpPresentation.MinimumDepartureFrames,
            FieldWarpPresentation.MinimumDepartureFrames + FieldDoorPresentation.AnimatedArrivalFrames));
        var saved = session.PrepareFieldSave();
        Assert.IsNull(LastTransition(saved).TransitionTiming);
        Assert.AreEqual(1, LastTransition(saved).Frames);
        Assert.AreEqual(unchanged, JsonSerializer.Serialize(session.State));
        Assert.AreSame(state, session.State);
        Assert.AreEqual(revision, session.Revision);
        Assert.AreEqual(initial.Player with { Facing = Direction.South }, state.World.Player);
        Assert.AreEqual(initial.Rng, state.World.Rng);
        Assert.AreEqual(initial.StepCount, state.World.StepCount);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(saved.QuestLog, catalog.QuestLogSource, catalog));
    }
}

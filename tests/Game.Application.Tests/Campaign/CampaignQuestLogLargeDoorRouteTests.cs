using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class CampaignQuestLogLargeDoorRouteTests
{
    // Pinned map events: SSAnne_1F_Room1 warp0 (2,1) <-> Corridor warp6
    // (5,10). field_door.c binds METATILE_SSAnne_Door (0x281) to the
    // ordinary DOOR_SIZE_1x2 animation. Content/import tests check its pixels.
    // Only the starting stable field context is controlled; no synthetic receipt,
    // destination relocation or Quest Log transition is injected into this route.
    private static CampaignSession AtRoomDoor()
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var session = CampaignSession.CreateCurrent(catalog);
        var room = catalog.MapFromSourceSymbol("MAP_SSANNE_1F_ROOM1");
        var state = session.State;
        state = state with
        {
            World = state.World with { Player = new(room, 2, 2, 3, Direction.North) },
            Actors = ImmutableSortedDictionary<string, CampaignActor>.Empty,
            ObjectSlots = null,
            Script = state.Script with
            {
                Completed = true, Locked = false, Wait = null, Stack = [],
                // A stable passenger-room position follows the exterior's
                // OnTransition visit flag in the ordinary boarding route.
                Flags = state.Script.Flags.SetItem("FLAG_WORLD_MAP_SSANNE_EXTERIOR", true)
            },
            EntryPrograms = [], ArrivalFacing = null, Dialogue = null, Motions = [],
            PresentationTicks = 0, PresentationEffects = [], MapLayout = null, MapEdits = null,
            PlayerVisible = true, IsCycling = false, IsSurfing = false, FlashLevel = 0,
            FieldWeather = null, Music = catalog.MapDefinition(room).Music
        };
        state = state with { QuestLog = new([CampaignQuestLogRecorder.SavedScene(state, catalog.QuestLogSource)]) };
        typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, catalog.ContentVersion, session.Revision));
        typeof(CampaignSession).GetField("_questLogRecordingOpen", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, true);
        return session;
    }

    private static CampaignQuestLogAction LastTransition(CampaignState state) =>
        state.QuestLog!.Scenes[^1].Actions.Last(action => action.Transition is not null);

    private static void AssertRouteGeometry(MapId room, MapId corridor)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        Assert.AreEqual("map.ssanne-1f-room-1", room.Value);
        Assert.AreEqual("map.ssanne-1f-corridor", corridor.Value);
        Assert.AreEqual((6, 8), (catalog.Maps[room].Width, catalog.Maps[room].Height));
        Assert.AreEqual((31, 21), (catalog.Maps[corridor].Width, catalog.Maps[corridor].Height));
        Assert.AreEqual(0x69, catalog.Maps[room].SourceBehaviorAt(2, 1));
        Assert.AreEqual(0x00, catalog.Maps[room].SourceBehaviorAt(2, 2));
        Assert.AreEqual(0x65, catalog.Maps[corridor].SourceBehaviorAt(5, 10));
        Assert.AreEqual(new FieldMetatileEdit(2, 1, 0x281, 1, 0), catalog.SourceMetatileAt(room, 2, 1));
        Assert.AreEqual(new FieldMetatileEdit(2, 0, 0x3bf, 1, 0), catalog.SourceMetatileAt(room, 2, 0));
        Assert.AreEqual(new FieldMetatileEdit(2, 2, 0x31e, 0, 3), catalog.SourceMetatileAt(room, 2, 2));
        Assert.AreEqual(new FieldMetatileEdit(5, 10, 0x395, 0, 3), catalog.SourceMetatileAt(corridor, 5, 10));
        Assert.AreEqual(new FieldWarp(0, 2, 1, 0, corridor, 6), catalog.Maps[room].FieldWarps.Single());
        Assert.AreEqual(new FieldWarp(6, 5, 10, 3, room, 0), catalog.Maps[corridor].FieldWarps[6]);

        foreach (var map in new[] { room, corridor })
        {
            var definition = catalog.MapDefinition(map);
            Assert.AreEqual("MAPSEC_S_S_ANNE", definition.RegionMapSection);
            Assert.AreEqual("MUS_SS_ANNE", definition.Music);
            Assert.AreEqual("MAP_TYPE_INDOOR", definition.MapType);
            Assert.AreEqual("WEATHER_NONE", definition.Weather);
            Assert.IsFalse(definition.RequiresFlash);
            Assert.IsNull(definition.Programs.OnTransition);
            Assert.IsNull(definition.Programs.OnLoad);
        }
        string layout = catalog.SourceMaps[room].Events.GetProperty("layout").GetString()!;
        Assert.AreEqual("LAYOUT_SSANNE_ROOM1", layout);
        var door = GeneratedFieldDoorArt.Load(Path.Combine(TestAssets.Root, "content/generated")).Find(layout, 0x281);
        Assert.IsNotNull(door, "The public route must exercise an admitted large door, not an unsupported fallback.");
        Assert.AreEqual((16, 32, "SE_DOOR"), (door.Width, door.Height, door.SoundEffect));
    }

    private static FieldTransitionReceipt Move(CampaignSession session, Direction direction,
        MapId destination, FieldTransitionCause cause, FieldTransitionArrival arrival)
    {
        var before = session.State;
        var result = session.Execute("large-door-move:" + session.Revision, session.Revision, new("move", direction));
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
        Assert.IsFalse(session.CanPresentFieldTransition(receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(before, receipt with { }));
        Assert.IsTrue(result.State.Script.Completed);
        Assert.IsFalse(result.State.Script.Locked);
        Assert.IsNull(result.State.Script.Wait);
        Assert.IsTrue(result.State.EntryPrograms.IsEmpty);
        Assert.IsNull(result.State.Dialogue);
        Assert.IsTrue(result.State.Motions.IsEmpty);
        Assert.IsTrue(result.State.Script.Flags.GetValueOrDefault("FLAG_WORLD_MAP_SSANNE_EXTERIOR"));
        Assert.AreEqual(before.World.Rng, result.State.World.Rng);
        Assert.AreEqual(before.World.StepCount, result.State.World.StepCount);
        Assert.IsNull(LastTransition(result.State).TransitionTiming);
        return receipt;
    }

    private static CampaignQuestLogTransitionTiming ObserveAndReplay(CampaignSession session,
        FieldTransitionReceipt receipt, int musicWait)
    {
        var state = session.State;
        long revision = session.Revision;
        string unchanged = JsonSerializer.Serialize(state);
        bool animatedDeparture = receipt.Cause == FieldTransitionCause.AnimatedDoor;
        bool animatedArrival = receipt.Arrival == FieldTransitionArrival.AnimatedDoor;
        int minimum = animatedDeparture ? FieldDoorPresentation.MinimumDepartureFrames : FieldWarpPresentation.MinimumDepartureFrames;
        int arrivalFrames = animatedArrival ? FieldDoorPresentation.AnimatedArrivalFrames : FieldWarpPresentation.PlainArrivalFrames;
        int change = minimum + musicWait, end = change + arrivalFrames;
        Assert.AreSame(LastTransition(state), state.QuestLog!.Scenes[^1].Actions[^1]);
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt with { }, change, end));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, minimum - 1, minimum - 1 + arrivalFrames));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end + 1));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end));
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, change, end));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change + 1, end + 1));

        var saved = session.PrepareFieldSave();
        var marker = LastTransition(saved);
        var timing = marker.TransitionTiming;
        Assert.IsNotNull(timing);
        Assert.AreEqual(receipt.Cause, timing.Cause);
        Assert.AreEqual(receipt.Arrival, timing.Arrival);
        Assert.AreEqual(animatedDeparture ? new CampaignQuestLogDoorAnchor(2, 1) : null, timing.DepartureDoor);
        Assert.AreEqual(animatedArrival ? new CampaignQuestLogDoorAnchor(2, 1) : null, timing.ArrivalDoor);
        Assert.AreEqual(change, timing.SwitchFrame);
        Assert.AreEqual(end, timing.EndFrame);
        Assert.AreEqual(end, marker.Frames);
        Assert.AreEqual(receipt.To, saved.World.Player);
        var catalog = CurrentCampaignFixture.Catalog;
        Assert.IsNull(CampaignQuestLogRecorder.Validate(saved.QuestLog, catalog.QuestLogSource, catalog));

        string recorded = JsonSerializer.Serialize(saved.QuestLog);
        var decoded = JsonSerializer.Deserialize<CampaignQuestLog>(recorded)!;
        Assert.AreEqual(recorded, JsonSerializer.Serialize(decoded));
        Assert.IsNull(CampaignQuestLogRecorder.Validate(decoded, catalog.QuestLogSource, catalog));
        var playback = new CampaignQuestLogPlayback(saved.QuestLog!);
        var restoredPlayback = new CampaignQuestLogPlayback(decoded);
        playback.Tick(marker.StartFrame / 60d);
        restoredPlayback.Tick(marker.StartFrame / 60d);
        var doorDeparture = new FieldDoorPresentation.Departure();
        var plainDeparture = new FieldWarpPresentation.Departure();
        var doorArrival = new FieldDoorPresentation.AnimatedArrival();
        var plainArrival = new FieldWarpPresentation.PlainArrival();
        int soundPulses = 0;
        for (int frame = 0; frame < end; frame++)
        {
            Assert.AreEqual(timing, playback.ActiveTransitionTiming);
            Assert.AreEqual(timing, restoredPlayback.ActiveTransitionTiming);
            Assert.AreEqual(frame, playback.TransitionElapsedFrames, .00001);
            Assert.AreEqual(frame, restoredPlayback.TransitionElapsedFrames, .00001);
            var expected = frame < change ? receipt.From : receipt.To;
            Assert.AreEqual(expected.MapId.Value, playback.Map);
            Assert.AreEqual(playback.Map, restoredPlayback.Map);
            var player = playback.Actors.Single(actor => actor.Id == CampaignQuestLogRecorder.PlayerId);
            Assert.AreEqual((expected.X, expected.Y, expected.Facing), (player.X, player.Y, player.Facing));
            CollectionAssert.AreEqual(playback.Actors.ToArray(), restoredPlayback.Actors.ToArray());
            var live = frame < change
                ? animatedDeparture ? doorDeparture.Sample : FieldDoorPresentation.FromPlain(plainDeparture.Sample)
                : animatedArrival ? doorArrival.Sample : FieldDoorPresentation.FromPlain(plainArrival.Sample);
            Assert.AreEqual(live, FieldDoorPresentation.SampleRecorded(animatedDeparture, animatedArrival, change, frame));
            if (live.OpenDoorSoundRequested) soundPulses++;
            // Size changes the BG replacement footprint, not the source player
            // walk or task callback schedule. Logical tile positions stay committed.
            if (frame < change && animatedDeparture)
            {
                Assert.AreEqual(Math.Clamp(frame - 20, 0, 16), live.WalkPixels);
                Assert.AreEqual(frame < 37, live.PlayerVisible);
                Assert.AreEqual(frame >= 58, live.MusicFadeRequested);
            }
            else if (frame > change && animatedArrival)
            {
                int arrivalFrame = frame - change;
                Assert.AreEqual(Math.Clamp(arrivalFrame - 45, 0, 16), live.WalkPixels);
                Assert.AreEqual(arrivalFrame >= 46, live.PlayerVisible);
                Assert.AreEqual(arrivalFrame < 43, live.WipeActive);
            }
            if (frame < change)
            {
                bool ready = frame + 1 >= change - 1;
                if (animatedDeparture) doorDeparture.Advance(ready); else plainDeparture.Advance(ready);
            }
            else if (animatedArrival) doorArrival.Advance(); else plainArrival.Advance();
            playback.Tick(1d / 60);
            restoredPlayback.Tick(1d / 60);
        }
        Assert.AreEqual(1, soundPulses);
        Assert.IsTrue(animatedDeparture ? doorDeparture.Complete : plainDeparture.Complete);
        Assert.IsTrue(animatedArrival ? doorArrival.Complete : plainArrival.Complete);
        Assert.IsNull(playback.ActiveTransitionTiming);
        Assert.IsNull(restoredPlayback.ActiveTransitionTiming);
        Assert.AreEqual(receipt.To.MapId.Value, playback.Map);
        Assert.AreEqual(recorded, JsonSerializer.Serialize(saved.QuestLog));
        Assert.AreEqual(recorded, JsonSerializer.Serialize(decoded));
        Assert.AreSame(state, session.State);
        Assert.AreEqual(unchanged, JsonSerializer.Serialize(session.State));
        Assert.AreEqual(revision, session.Revision);
        Assert.IsNull(LastTransition(session.State).TransitionTiming);
        return timing;
    }

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(7, 11)]
    public void PublicSsAnneLargeDoorRoundTripRecordsDepartureAndReverseArrival(int departureMusicWait, int returnMusicWait)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var room = catalog.MapFromSourceSymbol("MAP_SSANNE_1F_ROOM1");
        var corridor = catalog.MapFromSourceSymbol("MAP_SSANNE_1F_CORRIDOR");
        AssertRouteGeometry(room, corridor);
        var session = AtRoomDoor();
        var initial = session.State.World;
        var leave = Move(session, Direction.North, corridor, FieldTransitionCause.AnimatedDoor, FieldTransitionArrival.Plain);
        Assert.AreEqual(new TilePosition(corridor, 5, 10, 3, Direction.North), leave.To);
        Assert.AreEqual(leave.To, leave.ArrivalAnchor);
        var departed = ObserveAndReplay(session, leave, departureMusicWait);

        var turn = session.Execute("large-door-turn", session.Revision, new("turn", Direction.South));
        Assert.AreEqual(CommandStatus.Accepted, turn.Status, turn.Error);
        Assert.AreEqual(departed, LastTransition(turn.State).TransitionTiming);
        Assert.AreEqual(leave.To with { Facing = Direction.South }, turn.State.World.Player);
        var enter = Move(session, Direction.South, room, FieldTransitionCause.PlainWarp, FieldTransitionArrival.AnimatedDoor);
        Assert.AreEqual(new TilePosition(room, 2, 1, 0, Direction.South), enter.ArrivalAnchor);
        Assert.AreEqual(initial.Player with { Facing = Direction.South }, enter.To);
        ObserveAndReplay(session, enter, returnMusicWait);
        Assert.AreEqual(initial.Player with { Facing = Direction.South }, session.State.World.Player);
        Assert.AreEqual(initial.Rng, session.State.World.Rng);
        Assert.AreEqual(initial.StepCount, session.State.World.StepCount);
        var log = session.PrepareFieldSave().QuestLog!;
        Assert.AreEqual(1, log.Scenes.Length);
        Assert.AreEqual(2, log.Scenes[0].Actions.Count(action => action.TransitionTiming is not null));
        Assert.AreEqual(2, log.Scenes[0].Actions.Count(action => action.Transition is not null));
    }
}

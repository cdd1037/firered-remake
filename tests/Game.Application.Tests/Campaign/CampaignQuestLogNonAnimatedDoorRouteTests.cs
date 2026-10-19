using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Application;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class CampaignQuestLogNonAnimatedDoorRouteTests
{
    private const string ReviveState = "VAR_MAP_SCENE_CINNABAR_ISLAND_POKEMON_LAB_EXPERIMENT_ROOM_REVIVE_STATE";

    // The initial stable field position is controlled. All movement, destination
    // entry scripts, receipts and recording below use ordinary public commands.
    // The source Entrance warp3 is MB_NON_ANIM_DOOR (0x60); Lounge warp0 is
    // MB_SOUTH_ARROW_WARP (0x65). Neither endpoint needs animated-door art.
    private static CampaignSession AtLabDoor(Direction facing = Direction.North)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var session = CampaignSession.CreateCurrent(catalog);
        var entrance = catalog.MapFromSourceSymbol("MAP_CINNABAR_ISLAND_POKEMON_LAB_ENTRANCE");
        var state = session.State;
        state = state with
        {
            World = state.World with { Player = new(entrance, 13, 6, 3, facing) },
            Actors = ImmutableSortedDictionary<string, CampaignActor>.Empty,
            ObjectSlots = null,
            Script = state.Script with
            {
                Completed = true, Locked = false, Wait = null, Stack = [],
                Flags = state.Script.Flags.SetItem("FLAG_WORLD_MAP_CINNABAR_ISLAND", true),
                Variables = state.Script.Variables.SetItem(ReviveState, 0)
            },
            EntryPrograms = [], ArrivalFacing = null, Dialogue = null, Motions = [],
            PresentationTicks = 0, PresentationEffects = [], MapLayout = null, MapEdits = null,
            PlayerVisible = true, IsCycling = false, IsSurfing = false, FlashLevel = 0,
            FieldWeather = null, Music = catalog.MapDefinition(entrance).Music
        };
        state = state with { QuestLog = new([CampaignQuestLogRecorder.SavedScene(state, catalog.QuestLogSource)]) };
        SetState(session, state);
        typeof(CampaignSession).GetField("_questLogRecordingOpen", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, true);
        return session;
    }

    private static void SetState(CampaignSession session, CampaignState state) =>
        typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion,
                CurrentCampaignFixture.Catalog.ContentVersion, session.Revision));

    private static CampaignQuestLogAction LastTransition(CampaignState state) =>
        state.QuestLog!.Scenes[^1].Actions.Last(action => action.Transition is not null);

    private static void AssertRouteGeometry(MapId entrance, MapId lounge)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        Assert.AreEqual("map.cinnabar-island-pokemon-lab-entrance", entrance.Value);
        Assert.AreEqual("map.cinnabar-island-pokemon-lab-lounge", lounge.Value);
        Assert.AreEqual((28, 11), (catalog.Maps[entrance].Width, catalog.Maps[entrance].Height));
        Assert.AreEqual((15, 11), (catalog.Maps[lounge].Width, catalog.Maps[lounge].Height));
        Assert.AreEqual("LAYOUT_CINNABAR_ISLAND_POKEMON_LAB_ENTRANCE", catalog.SourceMaps[entrance].Events.GetProperty("layout").GetString());
        Assert.AreEqual("LAYOUT_CINNABAR_ISLAND_POKEMON_LAB_LOUNGE", catalog.SourceMaps[lounge].Events.GetProperty("layout").GetString());
        Assert.AreEqual(0x60, catalog.Maps[entrance].SourceBehaviorAt(13, 5));
        Assert.AreEqual(0x00, catalog.Maps[entrance].SourceBehaviorAt(13, 6));
        Assert.AreEqual(0x65, catalog.Maps[lounge].SourceBehaviorAt(7, 9));
        Assert.AreEqual(new FieldMetatileEdit(13, 5, 0x2c5, 0, 3), catalog.SourceMetatileAt(entrance, 13, 5));
        Assert.AreEqual(new FieldMetatileEdit(13, 6, 0x79, 0, 3), catalog.SourceMetatileAt(entrance, 13, 6));
        Assert.AreEqual(new FieldMetatileEdit(7, 9, 0x89, 0, 3), catalog.SourceMetatileAt(lounge, 7, 9));
        Assert.AreEqual(new FieldWarp(3, 13, 5, 0, lounge, 0), catalog.Maps[entrance].FieldWarps[3]);
        Assert.AreEqual(new FieldWarp(0, 7, 9, 3, entrance, 3), catalog.Maps[lounge].FieldWarps.Single());
        foreach (var map in new[] { entrance, lounge })
        {
            var definition = catalog.MapDefinition(map);
            Assert.AreEqual("MAPSEC_CINNABAR_ISLAND", definition.RegionMapSection);
            Assert.AreEqual("MUS_CINNABAR", definition.Music);
            Assert.AreEqual("MAP_TYPE_INDOOR", definition.MapType);
            Assert.AreEqual("WEATHER_NONE", definition.Weather);
            Assert.IsFalse(definition.RequiresFlash);
            Assert.IsNull(definition.Programs.OnLoad);
        }
        Assert.AreEqual("CinnabarIsland_PokemonLab_Entrance_OnTransition", catalog.MapDefinition(entrance).Programs.OnTransition);
        Assert.IsNull(catalog.MapDefinition(lounge).Programs.OnTransition);
    }

    private static FieldTransitionReceipt Move(CampaignSession session, Direction direction,
        MapId destination, FieldTransitionCause cause, FieldTransitionArrival arrival)
    {
        var before = session.State;
        var result = session.Execute("nonanimated-door-move:" + session.Revision, session.Revision, new("move", direction));
        Assert.AreEqual(CommandStatus.Accepted, result.Status, result.Error);
        var receipt = result.State.FieldTransition;
        Assert.IsNotNull(receipt);
        bool step = cause == FieldTransitionCause.NonAnimatedDoor;
        Assert.AreEqual(before.World.Player, receipt.From);
        Assert.AreEqual(destination, receipt.To.MapId);
        Assert.AreEqual(receipt.To, result.State.World.Player);
        Assert.AreEqual(step ? FieldTransitionDeparture.StepOntoWarp : FieldTransitionDeparture.Immediate, receipt.Departure);
        Assert.AreEqual(cause, receipt.Cause);
        Assert.AreEqual(arrival, receipt.Arrival);
        Assert.AreEqual(step ? new TilePosition(before.World.Player.MapId, 13, 5, 3, Direction.North) : null, receipt.DepartureStep);
        Assert.IsTrue(session.CanPresentFieldTransition(before, receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(receipt), "A walking branch needs the authentic stable origin context.");
        Assert.IsFalse(session.CanPresentFieldTransition(before, receipt with { }));
        Assert.IsTrue(result.State.Script.Completed);
        Assert.IsFalse(result.State.Script.Locked);
        Assert.IsNull(result.State.Script.Wait);
        Assert.IsTrue(result.State.EntryPrograms.IsEmpty);
        Assert.IsNull(result.State.Dialogue);
        Assert.IsTrue(result.State.Motions.IsEmpty);
        Assert.AreEqual(0, result.State.Script.Variables.GetValueOrDefault(ReviveState));
        Assert.AreEqual(before.World.Rng, result.State.World.Rng);
        Assert.AreEqual(before.World.StepCount + (step ? 1 : 0), result.State.World.StepCount,
            "Entering the nonanimated door completes one real step; automatic emergence is not a second step.");
        Assert.IsNull(LastTransition(result.State).TransitionTiming);
        return receipt;
    }

    private static CampaignQuestLogTransitionTiming ObserveAndReplay(CampaignSession session,
        FieldTransitionReceipt receipt, int musicWait)
    {
        var state = session.State;
        long revision = session.Revision;
        string unchanged = JsonSerializer.Serialize(state);
        bool stepDeparture = receipt.Cause == FieldTransitionCause.NonAnimatedDoor;
        bool stepArrival = receipt.Arrival == FieldTransitionArrival.NonAnimatedDoor;
        int minimum = stepDeparture ? FieldDoorPresentation.MinimumStepDepartureFrames : FieldWarpPresentation.MinimumDepartureFrames;
        int arrivalFrames = stepArrival ? FieldDoorPresentation.NonAnimatedArrivalFrames : FieldWarpPresentation.PlainArrivalFrames;
        int change = minimum + musicWait, end = change + arrivalFrames;
        Assert.AreSame(LastTransition(state), state.QuestLog!.Scenes[^1].Actions[^1]);
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt with { }, change, end));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, minimum - 1, minimum - 1 + arrivalFrames));
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end - 1));
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
        Assert.AreEqual(stepDeparture ? new CampaignQuestLogDoorAnchor(13, 5) : null, timing.DepartureDoor);
        Assert.AreEqual(stepArrival ? new CampaignQuestLogDoorAnchor(13, 5) : null, timing.ArrivalDoor);
        Assert.AreEqual(change, timing.SwitchFrame);
        Assert.AreEqual(end, timing.EndFrame);
        Assert.AreEqual(end, marker.Frames);
        Assert.AreEqual(receipt.To, saved.World.Player);
        Assert.AreEqual(JsonSerializer.Serialize(state with { QuestLog = saved.QuestLog }), JsonSerializer.Serialize(saved));
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
        var step = new FieldDoorPresentation.StepDeparture();
        var plainDeparture = new FieldWarpPresentation.Departure();
        var emergence = new FieldDoorPresentation.NonAnimatedArrival();
        var plainArrival = new FieldWarpPresentation.PlainArrival();
        int previousWalk = 0;
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
                ? stepDeparture ? step.Sample : FieldDoorPresentation.FromPlain(plainDeparture.Sample)
                : stepArrival ? emergence.Sample : FieldDoorPresentation.FromPlain(plainArrival.Sample);
            Assert.AreEqual(live, FieldDoorPresentation.SampleRecorded(receipt.Cause, receipt.Arrival, change, frame));
            Assert.AreEqual(-1, live.DoorFrame);
            Assert.IsFalse(live.DoorAnimationActive);
            Assert.IsFalse(live.OpenDoorSoundRequested);
            Assert.IsFalse(live.WipeActive);
            Assert.IsTrue(live.WalkPixels is >= 0 and <= 16);
            if (frame == change) previousWalk = 0;
            Assert.IsTrue(live.WalkPixels >= previousWalk);
            previousWalk = live.WalkPixels;
            if (frame < change)
            {
                bool ready = frame + 1 >= change - 1;
                if (stepDeparture) step.Advance(ready); else plainDeparture.Advance(ready);
            }
            else if (stepArrival) emergence.Advance(); else plainArrival.Advance();
            playback.Tick(1d / 60);
            restoredPlayback.Tick(1d / 60);
        }
        Assert.IsTrue(stepDeparture ? step.Complete : plainDeparture.Complete);
        Assert.IsTrue(stepArrival ? emergence.Complete : plainArrival.Complete);
        Assert.AreEqual(16, stepDeparture ? step.Sample.WalkPixels : emergence.Sample.WalkPixels);
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
    [DataRow(Direction.North, 0, 0)]
    [DataRow(Direction.North, 7, 11)]
    [DataRow(Direction.East, 0, 0)]
    [DataRow(Direction.East, 7, 11)]
    public void PublicLabDoorRoundTripRecordsActualStepAndAutomaticReverseArrival(Direction initialFacing,
        int departureMusicWait, int returnMusicWait)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var entrance = catalog.MapFromSourceSymbol("MAP_CINNABAR_ISLAND_POKEMON_LAB_ENTRANCE");
        var lounge = catalog.MapFromSourceSymbol("MAP_CINNABAR_ISLAND_POKEMON_LAB_LOUNGE");
        AssertRouteGeometry(entrance, lounge);
        var session = AtLabDoor(initialFacing);
        var initial = session.State.World;
        var enter = Move(session, Direction.North, lounge, FieldTransitionCause.NonAnimatedDoor, FieldTransitionArrival.Plain);
        Assert.AreEqual(initialFacing, enter.From.Facing);
        Assert.AreEqual(Direction.North, enter.DepartureStep!.Facing,
            "The accepted step direction is independent of the player's prior facing.");
        Assert.AreEqual(new TilePosition(lounge, 7, 9, 3, Direction.North), enter.To);
        Assert.AreEqual(enter.To, enter.ArrivalAnchor);
        var entered = ObserveAndReplay(session, enter, departureMusicWait);

        var turn = session.Execute("nonanimated-door-turn", session.Revision, new("turn", Direction.South));
        Assert.AreEqual(CommandStatus.Accepted, turn.Status, turn.Error);
        Assert.AreEqual(entered, LastTransition(turn.State).TransitionTiming);
        Assert.AreEqual(enter.To with { Facing = Direction.South }, turn.State.World.Player);
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(enter, entered.SwitchFrame, entered.EndFrame));
        var leave = Move(session, Direction.South, entrance, FieldTransitionCause.PlainWarp, FieldTransitionArrival.NonAnimatedDoor);
        Assert.AreEqual(new TilePosition(entrance, 13, 5, 0, Direction.South), leave.ArrivalAnchor);
        Assert.AreEqual(initial.Player with { Facing = Direction.South }, leave.To);
        ObserveAndReplay(session, leave, returnMusicWait);
        Assert.AreEqual(initial.Player with { Facing = Direction.South }, session.State.World.Player);
        Assert.AreEqual(initial.Rng, session.State.World.Rng);
        Assert.AreEqual(initial.StepCount + 1, session.State.World.StepCount);
        var log = session.PrepareFieldSave().QuestLog!;
        Assert.AreEqual(1, log.Scenes.Length);
        Assert.AreEqual(2, log.Scenes[0].Actions.Count(action => action.TransitionTiming is not null));
        Assert.AreEqual(2, log.Scenes[0].Actions.Count(action => action.Transition is not null));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NonAnimatedObservationRejectsStaleReceiptOrReplacedImmutableScene(bool replaceScene)
    {
        var session = AtLabDoor();
        var lounge = CurrentCampaignFixture.Catalog.MapFromSourceSymbol("MAP_CINNABAR_ISLAND_POKEMON_LAB_LOUNGE");
        var receipt = Move(session, Direction.North, lounge, FieldTransitionCause.NonAnimatedDoor, FieldTransitionArrival.Plain);
        if (replaceScene)
        {
            var state = session.State;
            var log = state.QuestLog!;
            SetState(session, state with { QuestLog = new(log.Scenes.SetItem(log.Scenes.Length - 1, log.Scenes[^1] with { })) });
        }
        else
        {
            var turn = session.Execute("nonanimated-door-stale", session.Revision, new("turn", Direction.South));
            Assert.AreEqual(CommandStatus.Accepted, turn.Status, turn.Error);
        }
        int change = FieldDoorPresentation.MinimumStepDepartureFrames;
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, change + FieldWarpPresentation.PlainArrivalFrames));
        Assert.IsNull(LastTransition(session.State).TransitionTiming);
        Assert.AreEqual(1, LastTransition(session.State).Frames);
        Assert.AreEqual(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(session.PrepareFieldSave()));
    }

    [TestMethod]
    [DataRow("missing-step")]
    [DataRow("wrong-map")]
    [DataRow("same-cell")]
    [DataRow("diagonal")]
    [DataRow("two-cells")]
    [DataRow("wrong-facing")]
    [DataRow("invalid-facing")]
    [DataRow("immediate-departure")]
    [DataRow("plain-cause")]
    public void CapabilityRejectsMalformedStepFactsEvenWhenReceiptIdentityIsCurrent(string mutation)
    {
        var session = AtLabDoor(Direction.East);
        var before = session.State;
        var lounge = CurrentCampaignFixture.Catalog.MapFromSourceSymbol("MAP_CINNABAR_ISLAND_POKEMON_LAB_LOUNGE");
        var receipt = Move(session, Direction.North, lounge, FieldTransitionCause.NonAnimatedDoor, FieldTransitionArrival.Plain);
        var step = receipt.DepartureStep!;
        var malformed = mutation switch
        {
            "missing-step" => receipt with { DepartureStep = null },
            "wrong-map" => receipt with { DepartureStep = step with { MapId = lounge } },
            "same-cell" => receipt with { DepartureStep = step with { Y = 6 } },
            "diagonal" => receipt with { DepartureStep = step with { X = 14 } },
            "two-cells" => receipt with { DepartureStep = step with { Y = 4 } },
            "wrong-facing" => receipt with { DepartureStep = step with { Facing = Direction.East } },
            "invalid-facing" => receipt with { DepartureStep = step with { Facing = (Direction)99 } },
            "immediate-departure" => receipt with { Departure = FieldTransitionDeparture.Immediate },
            "plain-cause" => receipt with { Cause = FieldTransitionCause.PlainWarp, Departure = FieldTransitionDeparture.Immediate },
            _ => throw new InvalidOperationException("Unknown malformed-step test case.")
        };
        // Only malformed evidence is injected here, never movement or a valid route.
        // This bypasses the separate reference-identity guard to exercise geometry.
        SetState(session, session.State with { FieldTransition = malformed });
        Assert.IsFalse(session.CanPresentFieldTransition(before, malformed));
        int change = FieldDoorPresentation.MinimumStepDepartureFrames;
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(malformed, change, change + FieldWarpPresentation.PlainArrivalFrames));
        Assert.IsNull(LastTransition(session.State).TransitionTiming);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void NonAnimatedWalkingCapabilityRequiresStableOriginAvatar(bool cycling)
    {
        var session = AtLabDoor();
        var before = session.State;
        var lounge = CurrentCampaignFixture.Catalog.MapFromSourceSymbol("MAP_CINNABAR_ISLAND_POKEMON_LAB_LOUNGE");
        var receipt = Move(session, Direction.North, lounge, FieldTransitionCause.NonAnimatedDoor, FieldTransitionArrival.Plain);
        Assert.IsFalse(session.State.IsCycling);
        Assert.IsFalse(session.State.IsSurfing);
        var unsupportedOrigin = before with { IsCycling = cycling, IsSurfing = !cycling };
        Assert.IsFalse(session.CanPresentFieldTransition(unsupportedOrigin, receipt),
            "Destination avatar resets cannot make an unsupported walking origin appear ordinary.");
    }

    [TestMethod]
    public void NonAnimatedObservationOverflowKeepsTheWholeUnknownMarkerAndRealStep()
    {
        var session = AtLabDoor();
        var lounge = CurrentCampaignFixture.Catalog.MapFromSourceSymbol("MAP_CINNABAR_ISLAND_POKEMON_LAB_LOUNGE");
        var receipt = Move(session, Direction.North, lounge, FieldTransitionCause.NonAnimatedDoor, FieldTransitionArrival.Plain);
        var state = session.State;
        long revision = session.Revision;
        string unchanged = JsonSerializer.Serialize(state);
        int end = 250 + FieldWarpPresentation.PlainArrivalFrames;
        Assert.IsTrue(session.CompleteFieldTransitionPresentation(receipt, 250, end));
        var saved = session.PrepareFieldSave();
        Assert.AreEqual(JsonSerializer.Serialize(CampaignQuestLogRecorder.Close(state.QuestLog!)), JsonSerializer.Serialize(saved.QuestLog));
        Assert.IsTrue(saved.QuestLog!.Scenes[^1].Closed);
        Assert.AreSame(LastTransition(state), LastTransition(saved));
        Assert.IsNull(LastTransition(saved).TransitionTiming);
        Assert.AreEqual(1, LastTransition(saved).Frames);
        Assert.AreEqual(state.World, saved.World);
        Assert.AreSame(state, session.State);
        Assert.AreEqual(revision, session.Revision);
        Assert.AreEqual(unchanged, JsonSerializer.Serialize(session.State));
        Assert.AreEqual(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(session.PrepareFieldSave()));
        var committed = session.Execute("nonanimated-door-overflow", session.Revision, new("play_time_tick", Choice: 1));
        Assert.AreEqual(CommandStatus.Accepted, committed.Status, committed.Error);
        Assert.AreEqual(JsonSerializer.Serialize(saved.QuestLog), JsonSerializer.Serialize(committed.State.QuestLog));
        Assert.AreEqual(state.World, committed.State.World);
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, 250, end));
        var catalog = CurrentCampaignFixture.Catalog;
        Assert.IsNull(CampaignQuestLogRecorder.Validate(committed.State.QuestLog, catalog.QuestLogSource, catalog));
    }
}

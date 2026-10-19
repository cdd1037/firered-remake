using System.Reflection;
using System.Text.Json;
using Game.Application;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Roaming;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed partial class CampaignElevatorTransitionRouteTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private sealed record ReturnDestination(MapId Map, int Warp, int X, int Y, int Elevation,
        int Floor, bool Window, int? Menu);

    private static (string Map, string Cabin, int X, int Y) Route(string kind) => kind switch
    {
        "department" => ("map.celadon-city-department-store-1f", "map.celadon-city-department-store-elevator", 6, 1),
        "silph" => ("map.silph-co-1f", "map.silph-co-elevator", 22, 3),
        "rocket" => ("map.rocket-hideout-b1f", "map.rocket-hideout-elevator", 24, 25),
        "tower" => ("map.trainer-tower-roof", "map.trainer-tower-elevator", 13, 5),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static ReturnDestination Destination(CampaignState state, string kind) => kind switch
    {
        "department" when state.DepartmentElevator is { } d => new(d.DestinationMapId, d.DestinationWarpId,
            d.X, d.Y, d.Elevation, d.Floor, d.FloorWindowVisible, d.MenuDefault),
        "silph" when state.SilphElevator is { } s => new(s.DestinationMapId, s.DestinationWarpId,
            s.X, s.Y, s.Elevation, s.Floor, s.FloorWindowVisible, s.MenuDefault),
        "rocket" when state.RocketElevator is { } r => new(r.DestinationMapId, r.DestinationWarpId,
            r.X, r.Y, r.Elevation, r.Floor, r.FloorWindowVisible, r.MenuDefault),
        "tower" when state.TrainerTowerElevator is { } t => new(t.DestinationMapId, t.DestinationWarpId,
            t.X, t.Y, t.Elevation, t.Floor, t.FloorWindowVisible, t.MenuDefault),
        _ => throw new InvalidOperationException("Public entry did not retain its elevator return: " + kind)
    };

    private static CampaignState InitializeMap(CampaignSession session, CampaignState state)
    {
        var enter = typeof(CampaignSession).GetMethod("EnterMap", Private)!;
        var entered = (CampaignState)enter.Invoke(session, [state, null, RoamerTransition.Warp])!;
        return (CampaignState)typeof(CampaignSession).GetMethod("Pump", Private)!.Invoke(session, [entered])!;
    }

    private static CampaignSession Continue(CampaignSession session)
    {
        var saved = session.PrepareFieldSave();
        Assert.IsNull(session.ValidatePlayerSave(saved));
        var catalog = CurrentCampaignFixture.Catalog;
        var bytes = SaveCodec.Encode(saved, session.Revision, session.RulesVersion, catalog.ContentVersion);
        var decoded = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, catalog.ContentVersion,
            validate: session.ValidatePlayerSave);
        var restored = CampaignSession.RestoreCurrent(catalog, decoded.State, decoded.Generation);
        Assert.AreEqual(session.Revision, restored.Revision);
        Assert.IsTrue(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(saved),
            JsonSerializer.SerializeToElement(restored.State)),
            "Continue must preserve every saved value; dictionary property order is not gameplay state.");
        Assert.IsNull(restored.State.FieldTransition, "Continue must not reconstruct a transient receipt.");
        return restored;
    }

    private static CampaignSession AtEntrance(string kind, bool lateGame = true)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var session = CampaignSession.CreateCurrent(catalog);
        var route = Route(kind);
        var id = new MapId(route.Map);
        var map = catalog.Maps[id];
        Assert.AreEqual(0x69, map.SourceBehaviorAt(route.X, route.Y));
        var basis = lateGame ? CurrentCampaignFixture.Checkpoint.State : session.State;
        var state = basis with
        {
            World = basis.World with { Player = new(id, route.X, route.Y + 1,
                map.GetTile(route.X, route.Y + 1)!.Elevation, Direction.North) },
            Script = basis.Script with { Completed = true, Locked = false, Wait = null, Stack = [] },
            Dialogue = null, EntryPrograms = [], Motions = [], PresentationEffects = [], PresentationTicks = 0,
            ArrivalFacing = null, MapLayout = null, MapEdits = null, PlayerVisible = true,
            IsCycling = false, IsSurfing = false, FieldWeather = null, FlashLevel = 0,
            FieldTransition = null, QuestLog = null, Music = catalog.MapDefinition(id).Music
        };
        // Controlled starting location only. Initialize its real map scripts and
        // actors; no elevator return, destination, receipt or script result is injected.
        state = InitializeMap(session, state);
        Assert.IsNull(session.ValidatePlayerSave(state));
        return CampaignSession.RestoreCurrent(catalog, state,
            lateGame ? CurrentCampaignFixture.Checkpoint.Generation : session.Revision);
    }

    private static void Send(CampaignSession session, CampaignCommand command)
    {
        long revision = session.Revision;
        var result = session.Execute("elevator-route:" + revision, revision, command);
        Assert.AreEqual(CommandStatus.Accepted, result.Status, command + ": " + result.Error);
        Assert.AreEqual(revision + 1, session.Revision);
    }

    private static void AssertStable(CampaignSession session)
    {
        Assert.IsTrue(session.State.Script.Completed);
        Assert.IsFalse(session.State.Script.Locked);
        Assert.IsNull(session.State.Script.Wait);
        Assert.IsNull(session.State.Dialogue);
        Assert.IsTrue(session.State.Motions.IsEmpty);
        Assert.IsTrue(session.State.EntryPrograms.IsEmpty);
        Assert.AreEqual(0, session.State.PresentationTicks);
    }

    private static void Drain(CampaignSession session)
    {
        for (int i = 0; i < 40 && session.State.Script.Wait is not null; i++)
            Send(session, session.State.Script.Wait.Kind == StoryWaitKind.ButtonPress
                ? new("button_press", Text: "A") : new("ack"));
        AssertStable(session);
    }

    private static void Face(CampaignSession session, Direction direction)
    {
        if (session.State.World.Player.Facing != direction) Send(session, new("turn", direction));
    }

    private static CampaignSession Enter(string kind, bool lateGame = true)
    {
        var session = AtEntrance(kind, lateGame);
        var route = Route(kind);
        var origin = session.State.World.Player;
        if (lateGame && kind != "tower") StartRecording(session);
        var warp = CurrentCampaignFixture.Catalog.Maps[origin.MapId].FieldWarps.Single(w =>
            w.X == route.X && w.Y == route.Y && w.DestinationMapId.Value == route.Cabin);
        Send(session, new("move", Direction.North));
        Drain(session);
        Assert.IsTrue(CurrentCampaignFixture.Catalog.QuestLogSource.DisabledMaps.Contains(route.Cabin));
        if (session.State.QuestLog is { } closed) Assert.IsTrue(closed.Scenes[^1].Closed,
            "Natural elevator entry closes the source-disabled Quest Log recording.");
        Assert.AreEqual(new TilePosition(new(route.Cabin), 2, 5, 3, Direction.North), session.State.World.Player);
        var target = Destination(session.State, kind);
        Assert.AreEqual((origin.MapId, warp.Index, route.X, route.Y), (target.Map, target.Warp, target.X, target.Y));
        Assert.IsFalse(target.Window);
        Assert.IsNull(target.Menu);
        return session;
    }

    private static void OpenMenu(CampaignSession session, string kind)
    {
        // All four real layouts have a blocked y=2 wall. Reach sign (0,2)
        // from its walkable southern cell (0,3), rather than relocating to it.
        Send(session, new("move", Direction.North));
        Send(session, new("move", Direction.North));
        Send(session, new("move", Direction.West));
        Send(session, new("move", Direction.West));
        Assert.AreEqual((0, 3), (session.State.World.Player.X, session.State.World.Player.Y));
        Face(session, Direction.North);
        Send(session, new("interact"));
        for (int i = 0; i < 10 && Destination(session.State, kind).Menu is null; i++) Send(session, new("ack"));
        var target = Destination(session.State, kind);
        Assert.IsTrue(target.Window);
        Assert.IsNotNull(target.Menu);
        Assert.IsTrue(session.State.Script.Locked);
        Assert.AreEqual(kind == "silph" ? StoryWaitKind.Presentation : StoryWaitKind.MenuChoice,
            session.State.Script.Wait!.Kind);
        Assert.IsNotNull(session.ValidatePlayerSave(session.State), "An open floor selector is not a stable save.");
    }

    private static void Choose(CampaignSession session, string kind, int choice)
    {
        Send(session, new(kind == "silph" ? "silph_elevator_choice" : "ack", Choice: choice));
        Drain(session);
        Assert.IsFalse(Destination(session.State, kind).Window);
        Assert.IsNull(Destination(session.State, kind).Menu);
        Send(session, new("move", Direction.East));
        Send(session, new("move", Direction.East));
        Send(session, new("move", Direction.South));
        Send(session, new("move", Direction.South));
        Assert.AreEqual((2, 5), (session.State.World.Player.X, session.State.World.Player.Y));
    }

    private static void StartRecording(CampaignSession session)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var state = session.State;
        state = state with { QuestLog = new([CampaignQuestLogRecorder.SavedScene(state, catalog.QuestLogSource)]) };
        typeof(CampaignSession).GetField("_commands", Private)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, catalog.ContentVersion, session.Revision));
        typeof(CampaignSession).GetField("_questLogRecordingOpen", Private)!.SetValue(session, true);
    }

    private static FieldTransitionReceipt Exit(CampaignSession session, string kind)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var before = session.State;
        var target = Destination(before, kind);
        var map = catalog.Maps[target.Map];
        var anchor = new TilePosition(target.Map, target.X, target.Y, target.Elevation, Direction.South);
        if (target.Warp != 255)
        {
            var warp = map.FieldWarps.Single(w => w.Index == target.Warp);
            anchor = anchor with { X = warp.X, Y = warp.Y, Elevation = warp.Elevation };
        }
        Assert.AreEqual(0x65, catalog.Maps[before.World.Player.MapId].SourceBehaviorAt(2, 5));
        Assert.AreEqual(Direction.South, before.World.Player.Facing);
        Assert.AreEqual(0x69, map.SourceBehaviorAt(anchor.X, anchor.Y));
        var expected = anchor with { Y = anchor.Y + 1, Elevation = map.GetTile(anchor.X, anchor.Y + 1)!.Elevation };
        long revision = session.Revision;
        Send(session, new("move", Direction.South));
        var receipt = session.State.FieldTransition;
        Assert.IsNotNull(receipt, "The accepted manual dynamic exit must publish its committed transition.");
        Assert.AreEqual(FieldTransitionCause.PlainWarp, receipt.Cause);
        Assert.AreEqual(FieldTransitionDeparture.Immediate, receipt.Departure);
        Assert.AreEqual(FieldTransitionArrival.AnimatedDoor, receipt.Arrival);
        Assert.AreEqual(before.World.Player, receipt.From);
        Assert.AreEqual(anchor, receipt.ArrivalAnchor);
        Assert.AreEqual(expected, receipt.To);
        Assert.AreEqual(expected, session.State.World.Player);
        Assert.IsNull(receipt.DepartureStep);
        Assert.AreEqual(before.World.Revision + 1, session.State.World.Revision);
        Assert.AreEqual(revision + 1, session.Revision);
        Assert.AreEqual(before.World.StepCount, session.State.World.StepCount);
        // The checkpoint's active roamer keeps its existing map-entry RNG work.
        // The receipt must introduce no additional random draws or route changes.
        var roamer = SourceRoamerRules.Transition(before.Roamer ?? RoamerState.Empty,
            RoamerTransition.Warp, catalog.CelioRoamerMaps![target.Map], before.World.Rng);
        Assert.AreEqual(roamer.Rng, session.State.World.Rng);
        Assert.AreEqual(JsonSerializer.Serialize(roamer.State), JsonSerializer.Serialize(session.State.Roamer));
        Assert.AreEqual(target, Destination(session.State, kind));
        AssertStable(session);
        Assert.IsTrue(session.CanPresentFieldTransition(before, receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(before, receipt with { }));
        return receipt;
    }

    private static void AssertLivePhasesDoNotInventQuestTiming(CampaignSession session,
        FieldTransitionReceipt receipt, int musicWait)
    {
        var state = session.State;
        long revision = session.Revision;
        string unchanged = JsonSerializer.Serialize(state);
        int change = FieldWarpPresentation.MinimumDepartureFrames + musicWait;
        int end = change + FieldDoorPresentation.AnimatedArrivalFrames;
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end),
            "Every elevator cabin is source-disabled for Quest recording; a live receipt cannot reopen it.");
        var departure = new FieldWarpPresentation.Departure();
        var arrival = new FieldDoorPresentation.AnimatedArrival();
        int sounds = 0;
        for (int frame = 0; frame < end; frame++)
        {
            var live = frame < change ? FieldDoorPresentation.FromPlain(departure.Sample) : arrival.Sample;
            // This compares the shared phase authority only. It does not inject a
            // recording or pretend a source-disabled elevator owns Quest timing.
            Assert.AreEqual(live, FieldDoorPresentation.SampleRecorded(receipt.Cause, receipt.Arrival, change, frame));
            if (live.OpenDoorSoundRequested) sounds++;
            if (frame > change)
            {
                int local = frame - change;
                Assert.AreEqual(Math.Clamp(local - 45, 0, 16), live.WalkPixels);
                Assert.AreEqual(local >= 46, live.PlayerVisible);
                Assert.AreEqual(local < 43, live.WipeActive);
            }
            if (frame < change) departure.Advance(frame + 1 >= change - 1); else arrival.Advance();
        }
        Assert.AreEqual(1, sounds);
        Assert.IsTrue(departure.Complete);
        Assert.IsTrue(arrival.Complete);
        Assert.AreSame(state, session.State);
        Assert.AreEqual(unchanged, JsonSerializer.Serialize(session.State));
        Assert.AreEqual(revision, session.Revision);
        Assert.AreEqual(receipt.To, session.PrepareFieldSave().World.Player);
        Assert.AreEqual(JsonSerializer.Serialize(state.QuestLog), JsonSerializer.Serialize(session.PrepareFieldSave().QuestLog));
        var restored = Continue(session);
        Assert.AreEqual(JsonSerializer.Serialize(state.QuestLog), JsonSerializer.Serialize(restored.State.QuestLog));
        Assert.IsFalse(restored.State.QuestLog?.Scenes.SelectMany(scene => scene.Actions)
            .Any(action => action.TransitionTiming is not null) ?? false);
        Assert.IsFalse(restored.CompleteFieldTransitionPresentation(receipt, change, end));
    }

    [TestMethod]
    [DataRow("department", -1, "map.celadon-city-department-store-1f", 6, 1, 0)]
    [DataRow("department", 127, "map.celadon-city-department-store-1f", 6, 1, 9)]
    [DataRow("department", 5, "map.celadon-city-department-store-1f", 6, 1, 0)]
    [DataRow("department", 4, "map.celadon-city-department-store-1f", 6, 1, 0)]
    [DataRow("department", 0, "map.celadon-city-department-store-5f", 6, 1, 13)]
    [DataRow("silph", -1, "map.silph-co-1f", 22, 3, 0)]
    [DataRow("silph", 127, "map.silph-co-1f", 22, 3, 7)]
    [DataRow("silph", 11, "map.silph-co-1f", 22, 3, 0)]
    [DataRow("silph", 10, "map.silph-co-1f", 22, 3, 0)]
    [DataRow("silph", 5, "map.silph-co-6f", 20, 3, 11)]
    [DataRow("rocket", -1, "map.rocket-hideout-b1f", 24, 25, 0)]
    [DataRow("rocket", 127, "map.rocket-hideout-b1f", 24, 25, 5)]
    [DataRow("rocket", 3, "map.rocket-hideout-b1f", 24, 25, 0)]
    [DataRow("rocket", 0, "map.rocket-hideout-b1f", 24, 25, 0)]
    [DataRow("rocket", 1, "map.rocket-hideout-b2f", 28, 16, 8)]
    [DataRow("tower", -1, "map.trainer-tower-roof", 13, 5, 0)]
    [DataRow("tower", 127, "map.trainer-tower-roof", 13, 5, 0)]
    [DataRow("tower", 2, "map.trainer-tower-roof", 13, 5, 0)]
    [DataRow("tower", 0, "map.trainer-tower-roof", 13, 5, 0)]
    public void PublicEntryChoiceAndContinuePreserveActualDynamicReturn(string kind, int choice,
        string destinationMap, int doorX, int doorY, int musicWait)
    {
        var session = Enter(kind);
        var enteredTarget = Destination(session.State, kind);
        if (choice >= 0)
        {
            OpenMenu(session, kind);
            Choose(session, kind, choice);
        }
        var target = Destination(session.State, kind);
        Assert.AreEqual((destinationMap, doorX, doorY), (target.Map.Value, target.X, target.Y));
        bool cancelled = choice is -1 or 127 || (kind, choice) is ("department", 5) or ("silph", 11) or ("rocket", 3);
        if (cancelled || kind == "tower") Assert.AreEqual(enteredTarget, target);
        else Assert.AreEqual(255, target.Warp, "A real floor choice switches to the source's coordinate return.");
        Face(session, Direction.South);
        session = Continue(session);
        Assert.AreEqual(target, Destination(session.State, kind));
        Assert.IsTrue(CurrentCampaignFixture.Catalog.QuestLogSource.DisabledMaps.Contains(Route(kind).Cabin));
        var receipt = Exit(session, kind);
        AssertLivePhasesDoNotInventQuestTiming(session, receipt, musicWait);
    }

    [TestMethod]
    [DataRow("department")]
    [DataRow("silph")]
    [DataRow("rocket")]
    [DataRow("tower")]
    public void ActiveFloorMenuRefusesInvalidChoiceAndMovementWithoutPublishingReceipt(string kind)
    {
        var session = Enter(kind);
        OpenMenu(session, kind);
        foreach (var command in new CampaignCommand[]
        {
            new(kind == "silph" ? "silph_elevator_choice" : "ack", Choice: 99),
            new("move", Direction.South)
        })
        {
            var before = session.State;
            long revision = session.Revision;
            var result = session.Execute("invalid-elevator:" + command.Kind, revision, command);
            // These two native floor menus reject in StoryRules.Continue;
            // CampaignSession.Advance exposes that unchanged refusal as Fault.
            // Silph/Tower preflight their choices and return Rejected directly.
            bool sourceMenuFault = command.Kind == "ack" && kind is "department" or "rocket";
            Assert.AreEqual(sourceMenuFault ? CommandStatus.Fault : CommandStatus.Rejected, result.Status, result.Error);
            if (sourceMenuFault) Assert.AreEqual(
                "event_menu_choice_required: Source menu choice or cancellation required.", result.Error);
            Assert.AreSame(before, result.State);
            Assert.AreEqual(revision, result.Revision);
            Assert.AreSame(before, session.State);
            Assert.AreEqual(revision, session.Revision);
            Assert.IsNull(session.State.FieldTransition);
        }
        Choose(session, kind, 127);
        Face(session, Direction.South);
        Exit(Continue(session), kind);
    }

    [TestMethod]
    public void UnstartedRoamerDynamicReturnDoesNotConsumeStepsOrRng()
    {
        var session = Enter("department", lateGame: false);
        Face(session, Direction.South);
        session = Continue(session);
        var before = session.State.World;
        var receipt = Exit(session, "department");
        Assert.AreEqual(before.Rng, session.State.World.Rng);
        Assert.AreEqual(before.StepCount, session.State.World.StepCount);
        Assert.IsNull(session.State.QuestLog);
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, 24, 105));
    }
}

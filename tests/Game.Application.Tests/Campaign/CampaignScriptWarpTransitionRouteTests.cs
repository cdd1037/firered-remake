using System.Text.Json;
using Game.Application;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Roaming;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

// Shares only the public route helpers in CampaignElevatorTransitionRouteTests.
// The controlled setup is the roof approach; every cabin/menu/exit step is real.
public sealed partial class CampaignElevatorTransitionRouteTests
{
    private static CampaignSession TowerLobbyChoiceBeforeWarp()
    {
        var session = Enter("tower");
        OpenMenu(session, "tower");
        var atPanel = session.State;
        Assert.AreEqual(new TilePosition(new("map.trainer-tower-elevator"), 0, 3, 3, Direction.North),
            atPanel.World.Player);
        Assert.AreEqual(15, Destination(atPanel, "tower").Floor);

        Send(session, new("ack", Choice: 1));
        AssertTowerPresentationWait(session, "waitse", 0);
        Assert.AreEqual(atPanel.World.Player, session.State.World.Player);
        Assert.IsNull(session.State.Dialogue);
        var target = Destination(session.State, "tower");
        Assert.AreEqual(("map.trainer-tower-lobby", 255, 17, 8, 3),
            (target.Map.Value, target.Warp, target.X, target.Y, target.Floor));
        Assert.IsFalse(target.Window);
        Assert.IsNull(target.Menu);

        Send(session, new("ack"));
        AssertTowerPresentationWait(session, "waitstate", 171);
        Assert.AreEqual(atPanel.World.Player, session.State.World.Player);
        Assert.IsTrue(session.State.PresentationEffects.Any(e =>
            e.Opcode == "special" && e.Args.SequenceEqual(new[] { "AnimateElevator" })));

        Send(session, new("ack"));
        AssertTowerPresentationWait(session, "delay", 25);
        Assert.AreEqual(atPanel.World.Player, session.State.World.Player);
        Assert.AreEqual(3, session.State.Script.Variables["VAR_ELEVATOR_FLOOR"]);

        Send(session, new("ack"));
        AssertTowerPresentationWait(session, "waitmovement", 0, hasMotion: true);
        var motion = session.State.Motions.Single();
        Assert.AreEqual("LOCALID_PLAYER", motion.ActorId);
        CollectionAssert.AreEqual(new[]
        {
            "walk_in_place_faster_down", "delay_16", "walk_down", "walk_right", "walk_right", "walk_down", "step_end"
        }, motion.Actions.ToArray());
        var p = atPanel.World.Player;
        CollectionAssert.AreEqual(new[]
        {
            p,
            p with { Facing = Direction.South },
            p with { Y = 4, Facing = Direction.South },
            p with { X = 1, Y = 4, Facing = Direction.East },
            p with { X = 2, Y = 4, Facing = Direction.East },
            p with { X = 2, Y = 5, Facing = Direction.South }
        }, motion.Path.ToArray());
        Assert.AreEqual(motion.Path[^1], session.State.World.Player);
        Assert.AreEqual(atPanel.World.Revision, session.State.World.Revision);
        Assert.AreEqual(atPanel.World.StepCount, session.State.World.StepCount);
        Assert.AreEqual(atPanel.World.Rng, session.State.World.Rng);
        Assert.AreEqual(JsonSerializer.Serialize(atPanel.Roamer), JsonSerializer.Serialize(session.State.Roamer));
        Assert.IsNull(session.State.QuestLog);
        Assert.IsNotNull(session.ValidatePlayerSave(session.State));
        return session;
    }

    private static void AssertTowerPresentationWait(CampaignSession session, string opcode,
        int ticks, bool hasMotion = false)
    {
        var state = session.State;
        Assert.AreEqual(StoryWaitKind.Presentation, state.Script.Wait?.Kind);
        Assert.IsFalse(state.Script.Completed);
        Assert.IsTrue(state.Script.Locked);
        Assert.IsFalse(state.Script.Stack.IsEmpty);
        Assert.IsTrue(state.EntryPrograms.IsEmpty);
        Assert.IsNull(state.ArrivalFacing);
        Assert.AreEqual(ticks, state.PresentationTicks);
        Assert.AreEqual(hasMotion ? 1 : 0, state.Motions.Length);
        Assert.AreEqual(opcode, state.PresentationEffects[^1].Opcode);
        Assert.IsNull(state.FieldTransition, "A source delay or movement cannot publish the later warp early.");
    }

    [TestMethod]
    public void TowerLobbyChoiceRunsActualCabinDelayAndMovementBeforeWarp()
    {
        var session = TowerLobbyChoiceBeforeWarp();
        var before = session.State;
        long revision = session.Revision;
        var blocked = session.Execute("tower-before-warp:move", revision, new("move", Direction.South));
        Assert.AreEqual(CommandStatus.Rejected, blocked.Status);
        Assert.AreSame(before, session.State);
        Assert.AreEqual(revision, session.Revision);
        Assert.IsNull(session.State.FieldTransition);
    }

    private static (CampaignSession Session, CampaignState Origin, FieldTransitionReceipt Receipt,
        CampaignState Arrival) TowerScriptedLobbyWarp()
    {
        var session = TowerLobbyChoiceBeforeWarp();
        var origin = session.State;
        long revision = session.Revision;
        Send(session, new("ack"));
        var committed = session.State;
        var receipt = committed.FieldTransition;
        Assert.IsNotNull(receipt, "The completed source movement ACK must publish the script-warp receipt.");
        Assert.AreEqual(revision + 1, session.Revision);
        Assert.IsTrue(session.TryGetFieldTransitionArrival(origin, receipt, out var arrival));
        Assert.AreNotSame(committed, arrival);
        Assert.AreSame(receipt, arrival.FieldTransition);
        Assert.AreEqual(FieldTransitionCause.PlainWarp, receipt.Cause);
        Assert.AreEqual(FieldTransitionDeparture.Immediate, receipt.Departure);
        Assert.AreEqual(FieldTransitionArrival.AnimatedDoor, receipt.Arrival);
        Assert.IsNull(receipt.DepartureStep);
        Assert.AreEqual(origin.World.Player, receipt.From);

        var catalog = CurrentCampaignFixture.Catalog;
        var lobby = catalog.Maps[new("map.trainer-tower-lobby")];
        var anchor = new TilePosition(lobby.Id, 17, 8, lobby.GetTile(17, 8)!.Elevation, Direction.South);
        var landing = anchor with { Y = 9, Elevation = lobby.GetTile(17, 9)!.Elevation };
        Assert.AreEqual(0x69, lobby.SourceBehaviorAt(17, 8));
        Assert.AreEqual(anchor, receipt.ArrivalAnchor);
        Assert.AreEqual(landing, receipt.To);
        Assert.AreEqual(landing, arrival.World.Player);
        Assert.AreEqual(landing with { Y = 11, Elevation = lobby.GetTile(17, 11)!.Elevation },
            committed.World.Player);
        Assert.AreNotEqual(receipt.To, committed.World.Player,
            "The arrival snapshot must not erase the already-committed destination script movement.");

        Assert.IsTrue(arrival.Script.Completed);
        Assert.IsFalse(arrival.Script.Locked);
        Assert.IsNull(arrival.Script.Wait);
        Assert.IsTrue(arrival.Script.Stack.IsEmpty);
        Assert.IsTrue(arrival.EntryPrograms.IsEmpty);
        Assert.IsTrue(arrival.Motions.IsEmpty);
        Assert.IsNull(arrival.Dialogue);
        Assert.IsNull(arrival.ArrivalFacing);
        Assert.AreEqual(0, arrival.PresentationTicks);
        Assert.IsTrue(arrival.PresentationEffects.Length > 0);
        Assert.IsTrue(committed.PresentationEffects.Length > arrival.PresentationEffects.Length);
        Assert.IsTrue(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(arrival.PresentationEffects),
            JsonSerializer.SerializeToElement(committed.PresentationEffects.Take(arrival.PresentationEffects.Length))),
            "The detached entry effects must exactly match the committed prefix, including sequence, map and arguments.");
        Assert.IsFalse(arrival.PresentationEffects.Any(effect => effect.Opcode == "applymovement"));
        Assert.IsTrue(committed.PresentationEffects.Skip(arrival.PresentationEffects.Length).Any(effect =>
            effect.Opcode == "applymovement" && effect.Args.SequenceEqual(new[]
            {
                "LOCALID_PLAYER", "TrainerTower_Lobby_Movement_ExitElevator"
            })));
        Assert.AreEqual(17, arrival.Script.Variables["VAR_TEMP_D"]);
        Assert.IsTrue(catalog.TryConstant("METATILE_TrainerTower_Floor_ShadeBottomLeft", out int openTile));
        Assert.AreEqual(new FieldMetatileEdit(17, 10, openTile, 0, lobby.GetTile(17, 10)!.Elevation),
            arrival.MapEdits!["17:10"]);
        Assert.AreEqual(arrival.MapEdits["17:10"], committed.MapEdits!["17:10"]);
        Assert.AreEqual("save_trainer_tower_lobby_transition", session.ValidatePlayerSave(arrival),
            "A detached arrival snapshot cannot bypass the existing pending-exit save rule.");

        Assert.AreEqual(StoryWaitKind.Presentation, committed.Script.Wait?.Kind);
        Assert.IsFalse(committed.Script.Completed);
        Assert.IsTrue(committed.Script.Locked);
        Assert.IsFalse(committed.Script.Stack.IsEmpty);
        Assert.IsTrue(committed.EntryPrograms.IsEmpty);
        Assert.IsNull(committed.ArrivalFacing);
        Assert.AreEqual(0, committed.PresentationTicks);
        Assert.AreEqual("waitmovement", committed.PresentationEffects[^1].Opcode);
        var motion = committed.Motions.Single();
        Assert.AreEqual("LOCALID_PLAYER", motion.ActorId);
        CollectionAssert.AreEqual(new[] { "walk_down", "walk_down", "step_end" }, motion.Actions.ToArray());
        CollectionAssert.AreEqual(new[] { landing, landing with { Y = 10 }, landing with { Y = 11 } },
            motion.Path.ToArray());
        Assert.IsNotNull(session.ValidatePlayerSave(committed));
        Assert.AreEqual(origin.World.StepCount, committed.World.StepCount);
        Assert.AreEqual(origin.World.Revision, committed.World.Revision,
            "The presentation receipt must not add a gameplay revision to the existing scripted warp.");
        Assert.AreEqual(arrival.World.Revision, committed.World.Revision);
        var expectedRoamer = SourceRoamerRules.Transition(origin.Roamer ?? RoamerState.Empty,
            RoamerTransition.Warp, catalog.CelioRoamerMaps![lobby.Id], origin.World.Rng);
        Assert.AreEqual(expectedRoamer.Rng, arrival.World.Rng);
        Assert.AreEqual(expectedRoamer.Rng, committed.World.Rng);
        Assert.AreEqual(JsonSerializer.Serialize(expectedRoamer.State), JsonSerializer.Serialize(arrival.Roamer));
        Assert.AreEqual(JsonSerializer.Serialize(expectedRoamer.State), JsonSerializer.Serialize(committed.Roamer));
        Assert.AreEqual(JsonSerializer.Serialize(origin.Party), JsonSerializer.Serialize(committed.Party));
        Assert.AreEqual(origin.Money, committed.Money);
        Assert.IsTrue(catalog.QuestLogSource.DisabledMaps.Contains(origin.World.Player.MapId.Value));
        Assert.IsTrue(catalog.QuestLogSource.DisabledMaps.Contains(lobby.Id.Value));
        Assert.IsNull(arrival.QuestLog);
        Assert.IsNull(committed.QuestLog);
        Assert.IsTrue(session.CanPresentFieldTransition(origin, receipt));
        Assert.IsFalse(session.CanPresentFieldTransition(receipt));
        return (session, origin, receipt, arrival);
    }

    [TestMethod]
    public void TowerScriptedLobbyWarpCapturesArrivalBeforeExistingFrameMotion()
    {
        var (session, origin, receipt, arrival) = TowerScriptedLobbyWarp();
        var committed = session.State;
        long revision = session.Revision;
        string unchanged = JsonSerializer.Serialize(committed);
        string arrivalUnchanged = JsonSerializer.Serialize(arrival);
        int change = FieldWarpPresentation.MinimumDepartureFrames;
        int end = change + FieldDoorPresentation.AnimatedArrivalFrames;
        Assert.IsFalse(session.CompleteFieldTransitionPresentation(receipt, change, end),
            "A source-disabled Tower warp never invents a recorded Quest transition.");
        var departure = new FieldWarpPresentation.Departure();
        var door = new FieldDoorPresentation.AnimatedArrival();
        for (int frame = 0; frame < end; frame++)
        {
            if (frame < change) departure.Advance(true); else door.Advance();
            Assert.AreSame(committed, session.State);
            Assert.AreEqual(revision, session.Revision);
        }
        Assert.IsTrue(departure.Complete);
        Assert.IsTrue(door.Complete);
        Assert.AreEqual(unchanged, JsonSerializer.Serialize(session.State));
        Assert.AreEqual(arrivalUnchanged, JsonSerializer.Serialize(arrival));
        for (int read = 0; read < 3; read++)
        {
            Assert.IsTrue(session.TryGetFieldTransitionArrival(origin, receipt, out var retained));
            Assert.AreSame(arrival, retained);
            Assert.AreSame(committed, session.State);
            Assert.AreEqual(revision, session.Revision);
            Assert.AreEqual(arrivalUnchanged, JsonSerializer.Serialize(retained));
        }
        Assert.AreEqual(committed.World.Player, session.PrepareFieldSave().World.Player);
        Assert.IsNotNull(session.ValidatePlayerSave(session.PrepareFieldSave()));

        // The ordinary two destination ACKs still own gameplay continuation.
        Send(session, new("ack"));
        AssertTowerPresentationWait(session, "waitse", 0);
        Assert.AreEqual(committed.World.Player, session.State.World.Player);
        Assert.AreEqual(17, session.State.Script.Variables["VAR_TEMP_D"]);
        Assert.IsTrue(CurrentCampaignFixture.Catalog.TryConstant("METATILE_TrainerTower_CounterBarrier", out int closedTile));
        Assert.AreEqual(closedTile, session.State.MapEdits!["17:10"].SourceMetatileId);
        Assert.AreEqual(3, session.State.MapEdits["17:10"].Collision);
        Assert.IsFalse(session.TryGetFieldTransitionArrival(origin, receipt, out _));
        Send(session, new("ack"));
        AssertStable(session);
        Assert.AreEqual(0, session.State.Script.Variables["VAR_TEMP_D"]);
        Assert.AreEqual(committed.World.Player, session.State.World.Player);
        Assert.AreEqual(committed.World.Rng, session.State.World.Rng);
        Assert.AreEqual(committed.World.StepCount, session.State.World.StepCount);
        Assert.AreEqual(committed.World.Revision, session.State.World.Revision);
        Assert.AreEqual(JsonSerializer.Serialize(committed.Roamer), JsonSerializer.Serialize(session.State.Roamer));
        Assert.IsNull(session.State.QuestLog);
        Assert.IsNull(session.ValidatePlayerSave(session.PrepareFieldSave()));
        var restored = Continue(session);
        Assert.AreEqual(committed.World.Player, restored.State.World.Player);
        Assert.IsNull(restored.State.QuestLog);
        Assert.IsFalse(restored.TryGetFieldTransitionArrival(origin, receipt, out _));
        Assert.IsFalse(restored.CompleteFieldTransitionPresentation(receipt, change, end));
    }

    [TestMethod]
    public void TowerScriptWarpArrivalRequiresExactAcceptedOriginAndReceipt()
    {
        var (session, origin, receipt, arrival) = TowerScriptedLobbyWarp();
        foreach (var falseOrigin in new[]
        {
            origin with { },
            origin with { Motions = [], Script = origin.Script with { Wait = null, Locked = false, Completed = true, Stack = [] } },
            origin with { IsCycling = true },
            origin with { IsSurfing = true },
            origin with { FieldWeather = "WEATHER_RAIN" }
        })
        {
            Assert.IsFalse(session.TryGetFieldTransitionArrival(falseOrigin, receipt, out _));
            Assert.IsFalse(session.CanPresentFieldTransition(falseOrigin, receipt));
        }
        Assert.IsFalse(session.TryGetFieldTransitionArrival(origin, receipt with { }, out _));
        Assert.IsFalse(session.CanPresentFieldTransition(origin, receipt with { }));
        Assert.IsFalse(session.TryGetFieldTransitionArrival(arrival, receipt, out _));
        var committed = session.State;
        long revision = session.Revision;
        foreach (var command in new CampaignCommand[] { new("move", Direction.South), new("party_swap", Choice: 0, PartyIndex: 1) })
        {
            var rejected = session.Execute("tower-warp-blocked:" + command.Kind, revision, command);
            Assert.AreEqual(CommandStatus.Rejected, rejected.Status, rejected.Error);
            Assert.AreSame(committed, session.State);
            Assert.AreEqual(revision, session.Revision);
            Assert.IsTrue(session.TryGetFieldTransitionArrival(origin, receipt, out var stillOwned),
                "A rejected command must not expire the accepted receipt's detached arrival.");
            Assert.AreSame(arrival, stillOwned);
        }
        Send(session, new("ack"));
        Assert.IsFalse(session.TryGetFieldTransitionArrival(origin, receipt, out _));
        Assert.IsFalse(session.CanPresentFieldTransition(origin, receipt));
    }
}

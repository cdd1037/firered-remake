using System.Reflection;
using System.Collections.Immutable;
using Game.Application;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.WildBattles;

/// <summary>Controlled locations retain public source scripts, collision, encounters and save validation.</summary>
[TestClass]
public sealed class CyclingRoadFlowTests : WildBattleFixture
{
    private const string RoadScene = "VAR_MAP_SCENE_ROUTE16";
    private const string Road = "FLAG_SYS_ON_CYCLING_ROAD";
    private static readonly ItemId Bike = new("pret:ITEM_BICYCLE");
    private static readonly ItemId Voucher = new("pret:ITEM_BIKE_VOUCHER");

    private void Set(CampaignSession session, CampaignState state) => typeof(CampaignSession)
        .GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!
        .SetValue(session, new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, content.ContentVersion, session.Revision));

    private void Place(CampaignSession session, CampaignState state, string id, int x, int y, Direction facing)
    {
        var map = new MapId(id);
        state = state with { World = state.World with { Player = new(map, x, y, content.Maps[map].GetTile(x, y)!.Elevation, facing) },
            Script = StoryState.Empty with { Flags = state.Script.Flags, Variables = state.Script.Variables, EffectSequence = state.Script.EffectSequence },
            Dialogue = null, ItemMenu = null, Motions = [], PresentationTicks = 0, PresentationEffects = [],
            EntryPrograms = [], ArrivalFacing = null, IsSurfing = false, BicycleSlope = false, MapEdits = null };
        var enter = typeof(CampaignSession).GetMethod("EnterMap", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var pump = typeof(CampaignSession).GetMethod("Pump", BindingFlags.Instance | BindingFlags.NonPublic)!;
        state = (CampaignState)enter.Invoke(session, [state, null, Game.Domain.Roaming.RoamerTransition.Warp])!;
        Set(session, (CampaignState)pump.Invoke(session, [state])!);
        DrainField(session);
    }

    private void DrainField(CampaignSession session)
    {
        for (int n = 0; n < 200 && (session.State.Script.Wait is not null || !session.State.Script.Completed ||
            session.State.Dialogue is not null || !session.State.EntryPrograms.IsEmpty); n++)
        {
            if (session.State.ForestTrainerBattle is not null || session.State.WildBattle is not null) return;
            Send(session, new("ack", Choice: session.State.Script.Wait?.Kind == StoryWaitKind.Choice ? 1 : null));
        }
    }

    private CampaignState WithoutBike(bool voucher)
    {
        var state = FieldFixture();
        var keys = state.Inventory!.KeyItems!.Value.RemoveAll(i => i.Item == Bike || i.Item == Voucher);
        if (voucher) keys = keys.Add(new(Voucher, 1));
        return state with { IsCycling = false, BicycleSlope = false,
            Inventory = state.Inventory with { KeyItems = keys },
            Script = state.Script with { Flags = state.Script.Flags.SetItem("FLAG_GOT_BICYCLE", false)
                .SetItem("FLAG_GOT_BIKE_VOUCHER", voucher).SetItem(Road, false),
                Variables = state.Script.Variables.SetItem(RoadScene, 0) } };
    }

    private CampaignSession ExchangeVoucher()
    {
        var session = CampaignSession.CreateCurrent(content);
        // The fixture supplies the voucher, never the bicycle or completion flag.
        Place(session, WithoutBike(true), "map.cerulean-city-bike-shop", 7, 3, Direction.East);
        Send(session, new("interact")); DrainField(session);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault("FLAG_GOT_BICYCLE"));
        Assert.AreEqual(1, OpeningInventoryRules.Quantity(session.State.Inventory!.KeyItems!.Value, Bike));
        Assert.AreEqual(0, OpeningInventoryRules.Quantity(session.State.Inventory.KeyItems.Value, Voucher));
        return session;
    }

    private CampaignSession EnterRoad(string gate = "map.route-16-north-entrance-1f", int row = 12)
    {
        var session = ExchangeVoucher();
        Place(session, session.State, gate, 11, row, Direction.West);
        Assert.AreEqual(1, session.State.Script.Variables.GetValueOrDefault("VAR_TEMP_1"));
        for (int n = 0; n < 14 && session.State.World.Player.MapId.Value == gate; n++)
        { Send(session, new("move", Direction.West)); DrainField(session); }
        Assert.AreEqual(gate.Contains("route-16") ? "map.route-16" : "map.route-18", session.State.World.Player.MapId.Value);
        Assert.IsTrue(session.State.IsCycling);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(Road));
        Assert.AreEqual(1, session.State.Script.Variables.GetValueOrDefault(RoadScene));
        return session;
    }

    [TestMethod]
    [DataRow("map.route-16-north-entrance-1f", 12, false)]
    [DataRow("map.route-16-north-entrance-1f", 12, true)]
    [DataRow("map.route-18-east-entrance-1f", 6, false)]
    [DataRow("map.route-18-east-entrance-1f", 6, true)]
    public void BothOriginalGuardsRejectPedestriansEvenWithVoucher(string gate, int row, bool voucher)
    {
        var session = CampaignSession.CreateCurrent(content);
        Place(session, WithoutBike(voucher), gate, 7, row, Direction.West);
        Send(session, new("move", Direction.West));
        Assert.IsNotNull(session.State.Dialogue, "source guard interrupts entry");
        DrainField(session);
        Assert.AreEqual(gate, session.State.World.Player.MapId.Value);
        Assert.AreEqual(7, session.State.World.Player.X);
        Assert.AreEqual(row - 2, session.State.World.Player.Y);
        Assert.IsFalse(session.State.IsCycling);
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(Road));
        Assert.AreEqual(0, session.State.Script.Variables.GetValueOrDefault(RoadScene));
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow("map.route-16-north-entrance-1f", 12)]
    [DataRow("map.route-18-east-entrance-1f", 6)]
    public void VoucherExchangeAndBothGatesMountLockDismountAndCleanScene(string gate, int row)
    {
        var session = EnterRoad(gate, row);
        StrictRoundtrip(session);
        Send(session, new("bag_open"));
        var before = CanonicalJson.Hash(session.State);
        var denied = session.Execute("road-cannot-dismount", session.Revision, new("field_bicycle", Text: Bike.Value));
        Assert.AreEqual("bicycle_cannot_dismount", denied.Error);
        Assert.AreEqual(before, CanonicalJson.Hash(session.State));
        Send(session, new("menu_back"));
        for (int n = 0; n < 4 && session.State.World.Player.MapId.Value != gate; n++)
        { Send(session, new("move", Direction.East)); DrainField(session); }
        Assert.AreEqual(gate, session.State.World.Player.MapId.Value);
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(Road));
        Assert.AreEqual(1, session.State.Script.Variables.GetValueOrDefault(RoadScene));
        for (int n = 0; n < 15 && session.State.World.Player.MapId.Value == gate; n++)
        { Send(session, new("move", Direction.East)); DrainField(session); }
        Assert.AreEqual(0, session.State.Script.Variables.GetValueOrDefault(RoadScene));
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(Road));
        Send(session, new("bag_open")); Send(session, new("field_bicycle", Text: Bike.Value));
        Assert.IsFalse(session.State.IsCycling);
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void RoadConnectionsWorkBothWaysAndNorthernUpstairsStaysDeferred()
    {
        var session = EnterRoad();
        foreach (var (origin, destination, direction) in new[] {
            ("map.route-16", "map.route-17", Direction.South),
            ("map.route-17", "map.route-18", Direction.South),
            ("map.route-18", "map.route-17", Direction.North),
            ("map.route-17", "map.route-16", Direction.North) })
        {
            var from = content.ConnectedFieldMaps![new(origin)];
            var to = content.ConnectedFieldMaps[new(destination)];
            int y = direction == Direction.South ? from.Height - 1 : 0;
            int targetY = direction == Direction.South ? 0 : to.Height - 1;
            int x = Enumerable.Range(1, Math.Min(from.Width, to.Width) - 2).First(x =>
                from.CellAt(x, y) is { Collision: 0, Elevation: 3 } &&
                to.CellAt(x, targetY) is { Collision: 0, Elevation: 3 });
            Place(session, session.State, origin, x, y, direction);
            bool slope = CyclingRoadRules.IsSlope(from.CellAt(x, y)!.SourceBehavior);
            Send(session, slope ? new("cycle_input", direction, Choice: 0) : new("move", direction));
            DrainField(session);
            Assert.AreEqual(destination, session.State.World.Player.MapId.Value);
            Assert.AreEqual(x, session.State.World.Player.X);
            Assert.AreEqual(targetY, session.State.World.Player.Y);
            Assert.IsTrue(session.State.IsCycling);
            Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(Road));
            Assert.AreEqual(1, session.State.Script.Variables.GetValueOrDefault(RoadScene));
            StrictRoundtrip(session);
        }
        Place(session, session.State, "map.route-16-north-entrance-1f", 9, 15, Direction.South);
        var before = CanonicalJson.Hash(session.State);
        var denied = session.Execute("unimplemented-north-upstairs", session.Revision, new("move", Direction.South));
        Assert.AreEqual(CommandStatus.Rejected, denied.Status);
        Assert.AreEqual("snorlax_warp_not_open", denied.Error);
        Assert.AreEqual(before, CanonicalJson.Hash(session.State));
    }

    [TestMethod]
    public void ExistingRoute18UpstairsCanSaveWhileRoadSceneRemainsArmed()
    {
        var session = EnterRoad("map.route-18-east-entrance-1f", 6);
        Place(session, session.State, "map.route-18-east-entrance-1f", 8, 10, Direction.East);
        Send(session, new("move", Direction.East)); DrainField(session);
        Send(session, new("move", Direction.East)); DrainField(session);
        Assert.AreEqual("map.route-18-east-entrance-2f", session.State.World.Player.MapId.Value);
        Assert.AreEqual(1, session.State.Script.Variables.GetValueOrDefault(RoadScene));
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(Road));
        Assert.IsFalse(session.State.IsCycling);
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void FlyClearsRoadSceneAndSlopeThroughOriginalTravelCallback()
    {
        var session = EnterRoad(); var cell = SlopeCell();
        Place(session, session.State, "map.route-17", cell.x, cell.y, Direction.South);
        Send(session, new("cycle_input", Direction.South, Choice: 0));
        Assert.IsTrue(session.State.BicycleSlope);
        int flyer = Enumerable.Range(0, session.State.Party.Length).First(session.CanUseFieldFly);
        Send(session, new("field_fly", PartyIndex: flyer));
        var destination = session.FieldFlyDestinations[0];
        Send(session, new("field_fly_choose", Text: destination.SectionId));
        Send(session, new("field_fly_ack", Text: session.State.FlyEffect!.WaitId));
        Send(session, new("field_fly_ack", Text: session.State.FlyEffect!.WaitId));
        DrainField(session);
        Assert.AreEqual(destination.MapId, session.State.World.Player.MapId);
        Assert.IsFalse(session.State.IsCycling);
        Assert.IsFalse(session.State.BicycleSlope);
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(Road));
        Assert.AreEqual(0, session.State.Script.Variables.GetValueOrDefault(RoadScene));
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void WildWhiteoutClearsRoadFlagsAndHealsThroughOriginalExit()
    {
        var session = EnterRoad(); var cell = SlopeCell(grass: true);
        Place(session, session.State, "map.route-17", cell.x, cell.y, Direction.North);
        // Exhaustion is the controlled input; the public battle resolves actual damage and recoil.
        var party = session.State.Party.Select((p, index) => p.IsEgg ? p : p with {
            Pokemon = p.Pokemon with { CurrentHp = index == 0 ? 1 : 0,
                Moves = p.Pokemon.Moves.Select(m => m with { CurrentPp = 0 }).ToImmutableArray() } }).ToImmutableArray();
        Set(session, session.State with { Party = party, Exploration = session.State.Exploration! with {
            EarlyEncounter = session.State.Exploration.EarlyEncounter! with {
                PreviousMetatileBehavior = 0xd1, StepsSinceLastEncounter = 21, EncounterRateBuff = ushort.MaxValue } } });
        Send(session, new("cycle_input", Direction.North, Choice: 1)); Drain(session);
        Assert.IsNotNull(session.State.WildBattle);
        Send(session, new("battle_move", MoveSlot: -1)); Drain(session); DrainField(session);
        Assert.IsNull(session.State.WildBattle);
        Assert.AreNotEqual("map.route-17", session.State.World.Player.MapId.Value);
        Assert.IsFalse(session.State.IsCycling);
        Assert.IsFalse(session.State.BicycleSlope);
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault(Road));
        Assert.AreEqual(0, session.State.Script.Variables.GetValueOrDefault(RoadScene));
        Assert.IsTrue(session.State.Party.Where(p => !p.IsEgg).All(p => p.Pokemon.CurrentHp == p.Pokemon.Stats.MaxHp));
        StrictRoundtrip(session);
    }

    private (int x, int y) SlopeCell(bool blocked = false, bool grass = false)
    {
        var map = content.ConnectedFieldMaps![new("map.route-17")];
        var actors = content.MapDefinition(map.Id).Actors;
        return (from y in Enumerable.Range(3, map.Height - 6)
                from x in Enumerable.Range(2, map.Width - 4)
                let here = map.CellAt(x, y)!
                let north = map.CellAt(x, y - 1)!
                let south = map.CellAt(x, y + 1)!
                where here.Collision == 0 && here.SourceBehavior == (grass ? 0xd1 : 0xd0) &&
                    north.Collision == 0 && north.SourceBehavior == here.SourceBehavior &&
                    (blocked ? south.Collision != 0 : south.Collision == 0 && south.SourceBehavior == here.SourceBehavior) &&
                    !actors.Any(a => Math.Abs(a.X - x) < 4 && Math.Abs(a.Y - y) < 5)
                select (x, y)).First();
    }

    [TestMethod]
    public void SlopePublicInputsBrakePullUphillAndKeepSaveState()
    {
        var session = EnterRoad(); var cell = SlopeCell();
        Place(session, session.State, "map.route-17", cell.x, cell.y, Direction.North);
        var start = session.State.World.Player;
        Send(session, new("cycle_input", Choice: 1, Text: "idle"));
        Assert.AreEqual(start, session.State.World.Player);
        Assert.IsFalse(session.State.BicycleSlope);
        Send(session, new("cycle_input", Choice: 0, Text: "idle"));
        Assert.AreEqual(start.Y + 1, session.State.World.Player.Y);
        Assert.AreEqual(Direction.South, session.State.World.Player.Facing);
        Assert.IsTrue(session.State.BicycleSlope);
        StrictRoundtrip(session);
        Send(session, new("cycle_input", Direction.North, Choice: 1));
        Assert.AreEqual(start.Y + 1, session.State.World.Player.Y, "changed slope direction turns before moving");
        Assert.IsFalse(session.State.BicycleSlope);
        Send(session, new("cycle_input", Direction.North, Choice: 1));
        Assert.AreEqual(start.Y, session.State.World.Player.Y);
        Assert.IsTrue(session.State.BicycleSlope);
        Send(session, new("cycle_input", Choice: 1, Text: "idle"));
        Assert.AreEqual(Direction.South, session.State.World.Player.Facing, "released input maps to source face-down");
        Assert.IsFalse(session.State.BicycleSlope);
        Send(session, new("cycle_input", Choice: 1, Text: "idle"));
        Assert.AreEqual(start.Y, session.State.World.Player.Y);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BlockedSlopeInEitherSpeedDoesNotMoveTurnOrDrawEncounterRng(bool brake)
    {
        var session = EnterRoad(); var cell = SlopeCell(blocked: true);
        Place(session, session.State, "map.route-17", cell.x, cell.y, Direction.North);
        var p = session.State.World.Player; var rng = session.State.World.Rng;
        var rate = session.State.Exploration!.EarlyEncounter!.RateRng;
        Send(session, brake ? new("cycle_input", Direction.South, Choice: 1) : new("cycle_input", Choice: 0, Text: "idle"));
        Assert.AreEqual(p, session.State.World.Player);
        Assert.AreEqual(rng, session.State.World.Rng);
        Assert.AreEqual(rate, session.State.Exploration!.EarlyEncounter!.RateRng);
    }

    [TestMethod]
    public void CompletedStepOffSlopeClearsItsTransientInputState()
    {
        var session = EnterRoad();
        var map = content.ConnectedFieldMaps![new("map.route-17")];
        var actors = content.MapDefinition(map.Id).Actors;
        var cell = (from y in Enumerable.Range(1, map.Height - 2)
                    from x in Enumerable.Range(1, map.Width - 2)
                    where map.CellAt(x, y) is { Collision: 0, SourceBehavior: 0xd0 } &&
                        map.CellAt(x, y + 1) is { Collision: 0, SourceBehavior: 0 } &&
                        !actors.Any(a => Math.Abs(a.X - x) < 4 && Math.Abs(a.Y - y) < 5)
                    select (x, y)).First();
        Place(session, session.State, "map.route-17", cell.x, cell.y, Direction.South);
        Send(session, new("cycle_input", Direction.South, Choice: 0));
        Assert.AreEqual(cell.y + 1, session.State.World.Player.Y);
        Assert.IsFalse(session.State.BicycleSlope);
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void SlopeGrassStartsOrdinaryBattleAndSettlementCanContinue()
    {
        var session = EnterRoad(); var cell = SlopeCell(grass: true);
        Place(session, session.State, "map.route-17", cell.x, cell.y, Direction.North);
        Set(session, session.State with { Exploration = session.State.Exploration! with {
            EarlyEncounter = session.State.Exploration.EarlyEncounter! with {
                PreviousMetatileBehavior = 0xd1, StepsSinceLastEncounter = 21, EncounterRateBuff = ushort.MaxValue } } });
        Send(session, new("cycle_input", Direction.North, Choice: 1)); Drain(session);
        Assert.IsNotNull(session.State.WildBattle);
        Assert.IsNull(session.State.SafariBattle);
        Assert.IsNull(session.State.Exploration!.EarlyEncounter!.Pending);
        for (int attempt = 0; attempt < 20 && session.State.WildBattle is not null; attempt++)
        { Send(session, new("battle_menu", Text: "run")); Drain(session); }
        Assert.IsNull(session.State.WildBattle);
        Assert.IsTrue(session.State.IsCycling);
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void OriginalTrainerSightStartsBattleThroughRegistry()
    {
        var session = EnterRoad();
        var target = content.MapDefinition(new("map.route-17")).Actors.Single(a => a.ScriptId == "Route17_EventScript_Jamal");
        // This source Cue Ball faces right at (2,61).
        Place(session, session.State, "map.route-17", target.X + 2, target.Y, Direction.South);
        Send(session, new("turn", Direction.North)); DrainField(session);
        Assert.IsNotNull(session.State.ForestTrainerBattle, "original trainer sight entry");
        Assert.AreEqual("Route17_EventScript_Jamal", session.State.LastTalked is { } id ? session.State.Actors[id].ScriptId : null);
        Assert.IsTrue(session.State.IsCycling);
        // Finish the real source battle so its occurrence history is validated by Continue.
        for (int budget = 0; budget < 400 && session.State.ForestTrainerBattle is { } battle; budget++)
        {
            if (battle.Stage == CampaignForestTrainerStage.Intro || battle.Presentation.Phase == CampaignBattlePhase.Events)
                Send(session, new("ack"));
            else if (battle.Presentation.Phase == CampaignBattlePhase.Results)
                Send(session, !battle.Presentation.PendingMoves.IsEmpty &&
                    battle.Presentation.PageIndex + 1 >= battle.Presentation.Pages.Length
                    ? new("wild_learn", MoveSlot: -1) : new("ack"));
            else if (battle.Combat.Phase == ForestTrainerCombatPhase.AwaitingShiftChoice)
                Send(session, new("trainer_shift", Choice: 0));
            else Send(session, new("battle_move", MoveSlot: 0));
        }
        DrainField(session);
        Assert.IsNull(session.State.ForestTrainerBattle);
        Assert.IsTrue(session.State.BattledTrainers.Contains("pret:TRAINER_CUE_BALL_JAMAL"));
        StrictRoundtrip(session);
    }
}

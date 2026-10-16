using System.Collections.Immutable;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>Controlled current-checkpoint positions exercise public travel, interactions, battles and strict Continue.</summary>
[TestClass, TestCategory("Game")]
public sealed class TreasureBeachTests : WildBattleFixture
{
    private const string Beach = "map.one-island-treasure-beach";
    private const string Island = "map.one-island";
    private const string SourceMap = "MAP_ONE_ISLAND_TREASURE_BEACH";
    private const string Amara = "pret:TRAINER_SWIMMER_FEMALE_AMARA";
    private static readonly (int Dx, int Dy, Direction Direction)[] Directions =
        [(0, -1, Direction.North), (1, 0, Direction.East), (0, 1, Direction.South), (-1, 0, Direction.West)];

    private CampaignSession Start(string mapName, int x, int y, Direction facing = Direction.South,
        bool surfing = false, int renewalCounter = 0, bool hiddenAvailable = false, bool repel = true)
    {
        var state = FieldFixture();
        var map = content.Maps[new(mapName)];
        var definition = content.MapDefinition(map.Id);
        var flags = state.Script.Flags;
        foreach (var pickup in content.FieldPickups.Where(p => p.MapSymbol == SourceMap))
            flags = flags.SetItem(pickup.Flag, !hiddenAvailable);
        var keys = state.Inventory!.KeyItems!.Value;
        foreach (var name in new[] { "OLD", "GOOD", "SUPER" })
        {
            var item = new ItemId("pret:ITEM_" + name + "_ROD");
            keys = keys.RemoveAll(k => k.Item == item).Add(new(item, 1));
            flags = flags.SetItem("FLAG_GOT_" + name + "_ROD", true);
        }
        state = state with {
            World = state.World with { Player = new(map.Id, x, y, surfing ? 1 : 3, facing) },
            Actors = definition.Actors.ToImmutableSortedDictionary(a => a.LocalId,
                a => new CampaignActor(a.LocalId, a.SourceLocalId, a.GraphicsId, a.X, a.Y, a.Elevation,
                    Direction.South, a.MovementType, a.ScriptId, a.HideFlag, !flags.GetValueOrDefault(a.HideFlag),
                    false, a.X, a.Y), StringComparer.Ordinal),
            ObjectSlots = null, LastTalked = null, SelectedObjectSlot = 0, MapEdits = null, MapLayout = null,
            FlashLevel = 0, IsSurfing = surfing, IsCycling = false,
            Music = surfing ? "MUS_SURF" : definition.Music,
            Inventory = state.Inventory with { KeyItems = keys, Balls = [new(new("pret:ITEM_MASTER_BALL"), 1)] },
            Script = state.Script with { Flags = flags, Variables = state.Script.Variables
                .SetItem(RenewableHiddenItemRules.CounterVariable, renewalCounter)
                .SetItem("VAR_REPEL_STEP_COUNT", repel ? 250 : 0) }
        };
        state = CampaignSession.InitializeObjectSlotsForProfileUpgrade(state);
        Assert.IsNull(validator.ValidatePlayerSave(state), "controlled current Treasure Beach fixture");
        return CampaignSession.RestoreCurrent(content, state, checkpoint.Generation);
    }

    private List<string> DrainField(CampaignSession session)
    {
        var labels = new List<string>();
        for (int n = 0; n < 200; n++)
        {
            Assert.IsNull(session.State.WildBattle, "field route uses Repel");
            Assert.IsNull(session.State.ForestTrainerBattle, "route avoids Amara's sight");
            if (session.State.SurfEffect is { } surf)
            {
                Send(session, new("field_surf_complete", Text: surf.WaitId));
                continue;
            }
            if (session.State.Dialogue is { } dialogue) labels.Add(dialogue.TextSymbol);
            if (session.State.Script.Completed && session.State.Script.Wait is null &&
                session.State.Dialogue is null && session.State.EntryPrograms.IsEmpty) return labels;
            Send(session, session.State.Script.Wait?.Kind == StoryWaitKind.ButtonPress
                ? new("button_press", Text: "A")
                : new("ack", Choice: session.State.Script.Wait?.Kind == StoryWaitKind.Choice ? 1 : null));
        }
        Assert.Fail("field presentation did not finish");
        return labels;
    }

    private void Step(CampaignSession session, Direction direction)
    {
        Send(session, new("move", Direction: direction));
        DrainField(session);
    }

    private void Face(CampaignSession session, Direction direction)
    {
        if (session.State.World.Player.Facing != direction) Send(session, new("turn", Direction: direction));
    }

    private void WalkTo(CampaignSession session, int x, int y)
    {
        var map = content.ConnectedFieldMaps![session.State.World.Player.MapId];
        var start = (session.State.World.Player.X, session.State.World.Player.Y);
        var target = (x, y);
        var queue = new Queue<(int X, int Y)>();
        var seen = new HashSet<(int, int)> { start };
        var previous = new Dictionary<(int, int), ((int, int) Cell, Direction Direction)>();
        queue.Enqueue(start);
        while (queue.TryDequeue(out var cell) && cell != target)
            foreach (var direction in Directions)
            {
                var next = (cell.X + direction.Dx, cell.Y + direction.Dy);
                var tile = map.CellAt(next.Item1, next.Item2);
                if (seen.Contains(next) || tile is not { Collision: 0 } || tile.Elevation != (session.State.IsSurfing ? 1 : 3) ||
                    session.State.Actors.Values.Any(a => a.Active && !a.Invisible &&
                        (a.X == next.Item1 && a.Y == next.Item2 || a.ScriptId == "OneIsland_TreasureBeach_EventScript_Amara" &&
                            Math.Abs(a.X - next.Item1) + Math.Abs(a.Y - next.Item2) <= 1))) continue;
                seen.Add(next); previous[next] = (cell, direction.Direction); queue.Enqueue(next);
            }
        Assert.IsTrue(seen.Contains(target), $"walkable path to {target}");
        var path = new Stack<Direction>();
        for (var cell = target; cell != start; cell = previous[cell].Cell) path.Push(previous[cell].Direction);
        foreach (var direction in path) Step(session, direction);
        Assert.AreEqual(target, (session.State.World.Player.X, session.State.World.Player.Y));
    }

    private void LandOnBeach(CampaignSession session)
    {
        WalkTo(session, 10, 17);
        Step(session, Direction.South);
        Assert.IsFalse(session.State.IsSurfing);
        Assert.AreEqual((10, 18, 3), (session.State.World.Player.X, session.State.World.Player.Y, session.State.World.Player.Elevation));
        Assert.AreEqual("MUS_ROUTE3", session.State.Music);
    }

    private CampaignSession Roundtrip(CampaignSession session)
    {
        StrictRoundtrip(session);
        var decoded = SaveCodec.Decode<CampaignState>(SaveCodec.Encode(session.State, session.Revision,
            session.RulesVersion, content.ContentVersion), session.RulesVersion, content.ContentVersion,
            validate: session.ValidatePlayerSave);
        return CampaignSession.RestoreCurrent(content, decoded.State, decoded.Generation);
    }

    private int Quantity(CampaignSession session, FieldPickupDefinition pickup) => OpeningInventoryRules.Quantity(
        pickup.Pocket == "POCKET_POKE_BALLS" ? session.State.Inventory!.Balls!.Value : session.State.Inventory!.BagItems, pickup.Item);

    private void Collect(CampaignSession session, FieldPickupDefinition pickup)
    {
        var map = content.ConnectedFieldMaps![session.State.World.Player.MapId];
        var adjacent = Directions.First(d => map.CellAt(pickup.X - d.Dx, pickup.Y - d.Dy) is { Collision: 0, Elevation: 3 } &&
            !session.State.Actors.Values.Any(a => a.Active && a.X == pickup.X - d.Dx && a.Y == pickup.Y - d.Dy));
        WalkTo(session, pickup.X - adjacent.Dx, pickup.Y - adjacent.Dy);
        Face(session, adjacent.Direction);
        int before = Quantity(session, pickup);
        Send(session, new("interact")); DrainField(session);
        Assert.AreEqual(before + 1, Quantity(session, pickup), pickup.ItemSymbol);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(pickup.Flag));
        var hash = CanonicalJson.Hash(session.State);
        var repeat = session.Execute("repeat-hidden:" + pickup.Id, session.Revision, new("interact"));
        Assert.AreEqual(CommandStatus.Rejected, repeat.Status);
        Assert.AreEqual("nothing_to_interact", repeat.Error);
        Assert.AreEqual(hash, CanonicalJson.Hash(session.State));
    }

    [TestMethod]
    public void PublicSouthConnectionSurfLandingBoyAndReturnSurviveContinue()
    {
        var session = Start(Island, 8, 19, surfing: true);
        Step(session, Direction.South);
        Assert.AreEqual(new TilePosition(new(Beach), 8, 0, 1, Direction.South), session.State.World.Player);
        Assert.IsTrue(session.State.IsSurfing);
        session = Roundtrip(session);
        LandOnBeach(session);
        WalkTo(session, 13, 19); Face(session, Direction.South);
        Send(session, new("interact"));
        Assert.IsTrue(DrainField(session).Contains("OneIsland_TreasureBeach_Text_GoodThingsWashUpOnBeach"));
        session = Roundtrip(session);
        WalkTo(session, 10, 18); Face(session, Direction.North);
        int surfer = Enumerable.Range(0, session.State.Party.Length)
            .First(i => FieldHmSourceRules.Knows(session.State.Party[i], FieldHmSourceRules.SurfMove));
        Send(session, new("field_surf", PartyIndex: surfer)); DrainField(session);
        Assert.IsTrue(session.State.IsSurfing);
        WalkTo(session, 8, 0); Step(session, Direction.North);
        Assert.AreEqual(new TilePosition(new(Island), 8, 19, 1, Direction.North), session.State.World.Player);
        Assert.AreEqual("MUS_SURF", session.State.Music);
        Roundtrip(session);
    }

    [TestMethod]
    public void AllEightHiddenItemsCollectOnceAndPersistAfterContinue()
    {
        var session = Start(Beach, 10, 18, hiddenAvailable: true);
        var pickups = content.FieldPickups.Where(p => p.MapSymbol == SourceMap).ToArray();
        Assert.AreEqual(8, pickups.Length);
        foreach (var pickup in pickups) Collect(session, pickup);
        session = Roundtrip(session);
        Assert.IsTrue(pickups.All(p => session.State.Script.Flags.GetValueOrDefault(p.Flag)));
    }

    [TestMethod]
    public void RenewalRequiresNextActualMapEntryAndNeverRunsDuringContinue()
    {
        var session = Start(Island, 8, 19, surfing: true, renewalCounter: 1499);
        var pickups = content.FieldPickups.Where(p => p.MapSymbol == SourceMap).ToArray();
        Step(session, Direction.South);
        Assert.AreEqual(1500, session.State.Script.Variables[RenewableHiddenItemRules.CounterVariable]);
        Assert.IsTrue(pickups.All(p => session.State.Script.Flags.GetValueOrDefault(p.Flag)),
            "camera-entry sampling precedes the boundary's completed-step increment");
        session = Roundtrip(session);
        Assert.IsTrue(pickups.All(p => session.State.Script.Flags.GetValueOrDefault(p.Flag)), "Continue is not map entry");
        Step(session, Direction.North); Step(session, Direction.South);
        Assert.AreEqual(1, session.State.Script.Variables[RenewableHiddenItemRules.CounterVariable]);
        Assert.IsTrue(pickups.Count(p => !session.State.Script.Flags.GetValueOrDefault(p.Flag)) is 2 or 4 or 6);
        var ultra = pickups.Single(p => p.X == 15 && p.Y == 22);
        Assert.IsFalse(session.State.Script.Flags[ultra.Flag], "every source rarity makes both Ultra Balls available");
        session = Roundtrip(session);
        LandOnBeach(session); Collect(session, ultra);
        Roundtrip(session);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PublicLandAndSurfEncounterCaptureUsesBeachOriginAndContinues(bool surfing)
    {
        var map = content.Maps[new(Beach)];
        var cell = surfing ? (X: 10, Y: 16) : (X: 9, Y: 28);
        var session = Start(Beach, cell.X, cell.Y, surfing: surfing, repel: false);
        var state = session.State with {
            World = session.State.World with { Rng = session.State.World.Rng with { Seed = 3 } },
            Exploration = session.State.Exploration! with { EarlyEncounter = new(new(0),
                (ushort)map.SourceBehaviorAt(cell.X, cell.Y)!, ushort.MaxValue, 21,
                RulesVersion: EarlyLandEncounterRules.FiveIslandRulesVersion) }
        };
        session = CampaignSession.RestoreCurrent(content, state, session.Revision);
        Send(session, new("turn", Direction: Direction.North)); Drain(session);
        Assert.IsNotNull(session.State.WildBattle);
        var enemy = session.State.WildBattle.Frame.Party(BattleSide.Opponent).Members[0].Pokemon;
        if (surfing) Assert.IsTrue(content.WaterEncounters![map.Id].Slots.Any(s => s.Species == enemy.Species &&
            enemy.Level >= s.MinimumLevel && enemy.Level <= s.MaximumLevel));
        else Assert.IsTrue(content.LandEncounters![map.Id].Slots.Any(s => s.Species == enemy.Species && enemy.Level == s.MinimumLevel));
        long id = session.State.NextPokemonId;
        Send(session, new("battle_ball", Text: "pret:ITEM_MASTER_BALL")); Drain(session);
        var caught = session.State.Party.Concat(session.State.Storage!.Boxes.SelectMany(b => b.Slots).OfType<PartyPokemon>())
            .Single(p => p.InstanceId == id);
        Assert.AreEqual("MAPSEC_TREASURE_BEACH", caught.MetLocation);
        Assert.AreEqual(enemy.Personality, caught.Pokemon.Personality);
        Assert.AreEqual(surfing, session.State.IsSurfing);
        Roundtrip(session);
    }

    [TestMethod]
    [DataRow(FishingRod.Old)]
    [DataRow(FishingRod.Good)]
    [DataRow(FishingRod.Super)]
    public void PublicShoreFishingCapturesEveryRodAndContinues(FishingRod rod)
    {
        var session = Start(Beach, 10, 18, Direction.North, repel: false);
        var position = session.State.World.Player;
        for (int cast = 0; cast < 30 && session.State.WildBattle is null; cast++)
        {
            Send(session, new("bag_open"));
            Send(session, new("field_fish", Text: "pret:ITEM_" + rod.ToString().ToUpperInvariant() + "_ROD"));
            for (int n = 0; session.State.Fishing is { } fishing && n < 600; n++)
            {
                if (fishing.State.SourceStep == 14 && !fishing.NoCatchAnimationFinished)
                    Send(session, new("fishing_animation_finished", Text: fishing.WaitId));
                else if (session.State.Dialogue is not null && !fishing.TextPrinted)
                    Send(session, new("fishing_text_finished", Text: fishing.WaitId));
                else if (session.State.Dialogue is not null && !fishing.MessageFinished)
                    Send(session, new("ack", Text: fishing.WaitId));
                else Send(session, new("fishing_tick"));
            }
            Assert.IsNull(session.State.Fishing);
        }
        Assert.IsNotNull(session.State.WildBattle);
        Assert.AreSame(content.FishingTables, session.State.WildBattle.Frame.WildEncounter!.FishingTables);
        Drain(session);
        Send(session, new("battle_ball", Text: "pret:ITEM_MASTER_BALL")); Drain(session);
        var caught = session.State.Exploration!.Captured!.Value[^1];
        Assert.AreEqual(new MapId(Beach), caught.Fishing!.Map);
        Assert.AreEqual(rod, caught.Fishing.Rod);
        Assert.IsNotNull(content.FishingTables.SourceSlot(caught.Fishing));
        Assert.AreEqual(position, session.State.World.Player);
        Roundtrip(session);
    }

    [TestMethod]
    public void AmaraPublicBattleSettlesOnceReturnsToSurfAndContinues()
    {
        var session = Start(Beach, 8, 12, Direction.North, surfing: true);
        Assert.IsFalse(session.State.BattledTrainers.Contains(Amara));
        Send(session, new("interact"));
        Assert.AreEqual(Amara, session.State.ForestTrainerBattle!.Trainer.Value);
        for (int n = 0; session.State.ForestTrainerBattle is { } battle && n < 300; n++)
        {
            var presentation = battle.Presentation;
            if (battle.Stage == CampaignForestTrainerStage.Intro || presentation.Phase == CampaignBattlePhase.Events)
                Send(session, new("ack"));
            else if (presentation.Phase == CampaignBattlePhase.Results)
                Send(session, !presentation.PendingMoves.IsEmpty && presentation.PageIndex + 1 >= presentation.Pages.Length
                    ? new("wild_learn", MoveSlot: -1) : new("ack"));
            else if (battle.Combat.Phase == ForestTrainerCombatPhase.AwaitingShiftChoice)
                Send(session, new("trainer_shift", Choice: 0));
            else Send(session, new("battle_move", MoveSlot: 0));
        }
        Assert.IsNull(session.State.ForestTrainerBattle);
        DrainField(session);
        Assert.IsTrue(session.State.BattledTrainers.Contains(Amara));
        Assert.IsTrue(session.State.IsSurfing);
        Assert.AreEqual("MUS_SURF", session.State.Music);
        var history = session.State.ForestTrainerHistory!.Value.Where(h => h.TrainerId == Amara).ToArray();
        Assert.AreEqual(1, history.Length);
        Assert.AreEqual(WildBattleOutcome.Won, history[0].Outcome);
        session = Roundtrip(session);
        Send(session, new("interact"));
        Assert.IsTrue(DrainField(session).Contains("OneIsland_TreasureBeach_Text_AmaraPostBattle"));
        Assert.AreEqual(1, session.State.ForestTrainerHistory!.Value.Count(h => h.TrainerId == Amara));
        Roundtrip(session);
    }
    [TestMethod]
    public void OutdoorBeachRejectsEscapeRopeAndAllowsPublicFlyDeparture()
    {
        var session = Start(Beach, 10, 18);
        var state = session.State with { Inventory = session.State.Inventory! with {
            BagItems = session.State.Inventory!.BagItems.RemoveAll(p => p.Item == EscapeRopeRules.Item)
                .Add(new(EscapeRopeRules.Item, 1)) } };
        session = CampaignSession.RestoreCurrent(content, state, session.Revision);
        Send(session, new("bag_open"));
        var before = CanonicalJson.Hash(session.State);
        var rejected = session.Execute("beach-escape-rope", session.Revision,
            new("field_escape_rope", Text: EscapeRopeRules.Item.Value));
        Assert.AreEqual(CommandStatus.Rejected, rejected.Status);
        Assert.AreEqual("escape_rope_map_not_allowed", rejected.Error);
        Assert.AreEqual(before, CanonicalJson.Hash(session.State));
        Send(session, new("menu_back"));
        int flyer = Enumerable.Range(0, session.State.Party.Length).First(session.CanUseFieldFly);
        Send(session, new("field_fly", PartyIndex: flyer));
        var destination = session.FieldFlyDestinations.First(d => d.MapId.Value == Island);
        Send(session, new("field_fly_choose", Text: destination.SectionId));
        Send(session, new("field_fly_ack", Text: session.State.FlyEffect!.WaitId));
        Send(session, new("field_fly_ack", Text: session.State.FlyEffect!.WaitId));
        DrainField(session);
        Assert.AreEqual(new MapId(Island), session.State.World.Player.MapId);
        Assert.AreEqual(1, OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems, EscapeRopeRules.Item));
        Roundtrip(session);
    }

}

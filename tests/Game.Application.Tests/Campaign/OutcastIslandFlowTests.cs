using System.Collections.Immutable;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>Only initial positions are controlled. Routes, field moves, encounters and Continue use public commands.</summary>
[TestClass, TestCategory("Game")]
public sealed class OutcastIslandFlowTests : WildBattleFixture
{
    private const string Green = "map.six-island-green-path";
    private const string Outcast = "map.six-island-outcast-island";
    private const string Cave = "map.six-island-altering-cave";
    private static readonly (int Dx, int Dy, Direction Direction)[] Directions =
        [(0, -1, Direction.North), (1, 0, Direction.East), (0, 1, Direction.South), (-1, 0, Direction.West)];

    private CampaignState Place(string mapName, int x, int y, Direction facing = Direction.North,
        bool surfing = false, bool repel = true)
    {
        var state = FieldFixture();
        var map = content.Maps[new(mapName)]; var definition = content.MapDefinition(map.Id);
        var flags = state.Script.Flags;
        foreach (var pickup in content.FieldPickups.Where(p => p.MapSymbol == "MAP_SIX_ISLAND_OUTCAST_ISLAND"))
            flags = flags.SetItem(pickup.Flag, false);
        var keys = state.Inventory!.KeyItems!.Value;
        foreach (string name in new[] { "OLD", "GOOD", "SUPER" })
        {
            var rod = new ItemId("pret:ITEM_" + name + "_ROD");
            keys = keys.RemoveAll(k => k.Item == rod).Add(new(rod, 1));
            flags = flags.SetItem("FLAG_GOT_" + name + "_ROD", true);
        }
        state = state with {
            World = state.World with { Player = new(map.Id, x, y, map.GetTile(x, y)!.Elevation, facing) },
            Actors = definition.Actors.ToImmutableSortedDictionary(a => a.LocalId,
                a => new CampaignActor(a.LocalId, a.SourceLocalId, a.GraphicsId, a.X, a.Y, a.Elevation,
                    Direction.South, a.MovementType, a.ScriptId, a.HideFlag,
                    !flags.GetValueOrDefault(a.HideFlag), false, a.X, a.Y), StringComparer.Ordinal),
            ObjectSlots = null, LastTalked = null, SelectedObjectSlot = 0, MapLayout = null, MapEdits = null,
            EntryPrograms = [], Dialogue = null, Motions = [], IsSurfing = surfing, IsCycling = false, FlashLevel = 0,
            Music = surfing ? "MUS_SURF" : definition.Music,
            Script = StoryState.Empty with { Flags = flags, Variables = state.Script.Variables.SetItem("VAR_REPEL_STEP_COUNT", repel ? 250 : 0),
                EffectSequence = state.Script.EffectSequence },
            Inventory = state.Inventory with { KeyItems = keys, Balls = [new(new("pret:ITEM_MASTER_BALL"), 1)],
                BagItems = state.Inventory.BagItems.RemoveAll(i => i.Item == EscapeRopeRules.Item || i.Item == ItemIds.MaxRepelId)
                    .Add(new(EscapeRopeRules.Item, 1)).Add(new(ItemIds.MaxRepelId, 2)) }
        };
        return CampaignSession.InitializeObjectSlotsForProfileUpgrade(state);
    }

    private CampaignSession Start(CampaignState state)
    {
        Assert.IsNull(validator.ValidatePlayerSave(state), "controlled Outcast/Altering Cave field fixture");
        return CampaignSession.RestoreCurrent(content, state, checkpoint.Generation);
    }

    private CampaignSession Continue(CampaignSession session)
    {
        StrictRoundtrip(session);
        var decoded = SaveCodec.Decode<CampaignState>(SaveCodec.Encode(session.State, session.Revision,
            session.RulesVersion, content.ContentVersion), session.RulesVersion, content.ContentVersion,
            validate: session.ValidatePlayerSave);
        return CampaignSession.RestoreCurrent(content, decoded.State, decoded.Generation);
    }

    private void DrainField(CampaignSession session)
    {
        for (int n = 0; n < 250; n++)
        {
            if (session.State.WildBattle is not null || session.State.ForestTrainerBattle is not null || session.State.DoubleBattle is not null) return;
            if (session.State.SurfEffect is { } surf)
            { Send(session, new("field_surf_complete", Text: surf.WaitId)); continue; }
            if (session.State.Script.Completed && session.State.Script.Wait is null &&
                session.State.Dialogue is null && session.State.EntryPrograms.IsEmpty) return;
            Send(session, session.State.Script.Wait?.Kind == StoryWaitKind.ButtonPress ? new("button_press", Text: "A") :
                new("ack", Choice: session.State.Script.Wait?.Kind == StoryWaitKind.Choice ? 1 : null));
        }
        Assert.Fail("field operation did not complete");
    }

    private void Step(CampaignSession session, Direction direction)
    {
        if (session.RepelSteps == 0)
        {
            Send(session, new("bag_open")); Send(session, new("field_repel", Text: ItemIds.MaxRepelId.Value));
            Send(session, new("repel_presented", Text: session.State.Repel!.WaitId));
            Send(session, new("menu_ack")); Send(session, new("menu_back"));
        }
        Send(session, new("move", Direction: direction)); DrainField(session);
        Assert.IsNull(session.State.WildBattle, "route uses a high-level lead and Repel");
        Assert.IsNull(session.State.ForestTrainerBattle, "route avoids source trainer sight");
        Assert.IsNull(session.State.DoubleBattle, "route avoids sibling trainer sight");
    }

    private void Face(CampaignSession session, Direction direction)
    {
        if (session.State.World.Player.Facing != direction) Send(session, new("turn", Direction: direction));
    }

    private void TravelTo(CampaignSession session, int x, int y)
    {
        var map = content.ConnectedFieldMaps![session.State.World.Player.MapId];
        var start = (session.State.World.Player.X, session.State.World.Player.Y); var target = (x, y);
        var actors = content.MapDefinition(map.Id).Actors;
        var warps = content.Maps[map.Id].FieldWarps.Select(w => (w.X, w.Y)).ToHashSet();
        var queue = new Queue<(int X, int Y)>(); queue.Enqueue(start);
        var previous = new Dictionary<(int, int), ((int, int) Cell, Direction Direction)>();
        var seen = new HashSet<(int, int)> { start };
        while (queue.TryDequeue(out var cell) && cell != target)
            foreach (var d in Directions)
            {
                var next = (cell.X + d.Dx, cell.Y + d.Dy); var tile = map.CellAt(next.Item1, next.Item2);
                if (seen.Contains(next) || warps.Contains(next) || tile is not { Collision: 0, Elevation: 1 or 3 } ||
                    tile.Elevation == 1 && content.TerrainAt(map.Id, next.Item1, next.Item2) != 2 ||
                    session.State.Actors.Values.Any(a => a.Active && !a.Invisible &&
                        (a.X == next.Item1 && a.Y == next.Item2 || actors.Any(source => source.LocalId == a.LocalId &&
                            source.TrainerType == "TRAINER_TYPE_NORMAL" &&
                            Math.Abs(a.X - next.Item1) + Math.Abs(a.Y - next.Item2) <= source.TrainerSight)))) continue;
                seen.Add(next); previous[next] = (cell, d.Direction); queue.Enqueue(next);
            }
        Assert.IsTrue(seen.Contains(target), $"physical path through {map.Id.Value} to {target}");
        var path = new Stack<Direction>();
        for (var cell = target; cell != start; cell = previous[cell].Cell) path.Push(previous[cell].Direction);
        foreach (var direction in path)
        {
            var d = Directions.Single(d => d.Direction == direction); var position = session.State.World.Player;
            var next = map.CellAt(position.X + d.Dx, position.Y + d.Dy)!;
            if (!session.State.IsSurfing && next.Elevation == 1)
            {
                Face(session, direction);
                int surfer = Enumerable.Range(0, session.State.Party.Length).First(session.CanUseFieldSurf);
                Send(session, new("field_surf", PartyIndex: surfer)); DrainField(session);
                Assert.IsTrue(session.State.IsSurfing);
            }
            else Step(session, direction);
            Assert.AreEqual(map.Id, session.State.World.Player.MapId);
        }
        Assert.AreEqual(target, (session.State.World.Player.X, session.State.World.Player.Y));
    }

    private void EnterCave(CampaignSession session)
    {
        TravelTo(session, 7, 22); Assert.IsFalse(session.State.IsSurfing);
        for (int n = 0; n < 2 && session.State.World.Player.MapId.Value == Outcast; n++) Step(session, Direction.North);
        Assert.AreEqual(Cave, session.State.World.Player.MapId.Value);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault("FLAG_WORLD_MAP_SIX_ISLAND_ALTERING_CAVE"));
        Assert.AreEqual(new EscapeRopeDestination(new(Outcast), 7, 22), session.State.EscapeDestination);
    }

    [TestMethod]
    public void GreenPathSurfRouteCaveInteriorAndReturnUseActualTerrainWithoutRelocation()
    {
        var session = Start(Place(Green, 11, 0, surfing: true));
        Step(session, Direction.North);
        Assert.AreEqual(new TilePosition(new(Outcast), 11, 79, 1, Direction.North), session.State.World.Player);
        Assert.IsTrue(session.State.IsSurfing);
        EnterCave(session); session = Continue(session);
        TravelTo(session, 7, 4); TravelTo(session, 4, 12); TravelTo(session, 27, 11); TravelTo(session, 18, 21);
        session = Continue(session);
        for (int n = 0; n < 3 && session.State.World.Player.MapId.Value == Cave; n++) Step(session, Direction.South);
        Assert.AreEqual(Outcast, session.State.World.Player.MapId.Value);
        TravelTo(session, 11, 79); Assert.IsTrue(session.State.IsSurfing);
        Step(session, Direction.South);
        Assert.AreEqual(new TilePosition(new(Green), 11, 0, 1, Direction.South), session.State.World.Player);
        Continue(session);
    }

    [TestMethod]
    [DataRow("ITEM_PP_UP")]
    [DataRow("ITEM_STAR_PIECE")]
    [DataRow("ITEM_NET_BALL")]
    public void EachSourcePickupCollectsOnlyOnceAndSurvivesContinue(string item)
    {
        var pickup = content.FieldPickups.Single(p => p.MapSymbol == "MAP_SIX_ISLAND_OUTCAST_ISLAND" && p.ItemSymbol == item);
        var map = content.ConnectedFieldMaps![new(Outcast)]; var actors = content.MapDefinition(map.Id).Actors;
        var adjacent = Directions.First(d => map.CellAt(pickup.X - d.Dx, pickup.Y - d.Dy) is { Collision: 0, Elevation: 3 } &&
            !actors.Any(a => a.X == pickup.X - d.Dx && a.Y == pickup.Y - d.Dy));
        var session = Start(Place(Outcast, pickup.X - adjacent.Dx, pickup.Y - adjacent.Dy, adjacent.Direction));
        int Quantity() => OpeningInventoryRules.Quantity(pickup.Pocket == "POCKET_POKE_BALLS"
            ? session.State.Inventory!.Balls!.Value : session.State.Inventory!.BagItems, pickup.Item);
        int before = Quantity();
        Send(session, new("interact")); DrainField(session);
        Assert.AreEqual(before + 1, Quantity());
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(pickup.Flag));
        session = Continue(session);
        var hash = CanonicalJson.Hash(session.State);
        var repeat = session.Execute("repeat-outcast-pickup", session.Revision, new("interact"));
        Assert.AreEqual(CommandStatus.Rejected, repeat.Status);
        Assert.AreEqual("nothing_to_interact", repeat.Error);
        Assert.AreEqual(hash, CanonicalJson.Hash(session.State));
        Assert.AreEqual(before + 1, Quantity());
    }

    [TestMethod]
    [DataRow("Tylor", "TRAINER_FISHERMAN_TYLOR")]
    [DataRow("Mymo", "TRAINER_SWIMMER_MALE_MYMO")]
    [DataRow("Nicole", "TRAINER_SWIMMER_FEMALE_NICOLE")]
    [DataRow("Ava", "TRAINER_SIS_AND_BRO_AVA_GEB")]
    [DataRow("Geb", "TRAINER_SIS_AND_BRO_AVA_GEB")]
    public void EveryAvailableSourceTrainerActorStartsItsCorrectSingleOrDoubleBattle(string name, string trainer)
    {
        var map = content.ConnectedFieldMaps![new(Outcast)]; var actors = content.MapDefinition(map.Id).Actors;
        var actor = actors.Single(a => a.ScriptId == "SixIsland_OutcastIsland_EventScript_" + name);
        var adjacent = Directions.First(d => map.CellAt(actor.X - d.Dx, actor.Y - d.Dy) is { Collision: 0 } cell &&
            cell.Elevation == actor.Elevation && !actors.Any(a => a.X == actor.X - d.Dx && a.Y == actor.Y - d.Dy));
        var session = Start(Place(Outcast, actor.X - adjacent.Dx, actor.Y - adjacent.Dy,
            adjacent.Direction, surfing: actor.Elevation == 1));
        Send(session, new("interact")); DrainField(session);
        if (name is "Ava" or "Geb")
        {
            Assert.IsNotNull(session.State.DoubleBattle);
            Assert.AreEqual("pret:" + trainer, session.State.DoubleBattle.Trainer.Value);
            Assert.AreEqual(SourceBattleLayout.PresetDoubles, session.State.DoubleBattle.Frame.Layout);
            Assert.IsNull(session.State.ForestTrainerBattle);
        }
        else
        {
            Assert.IsNotNull(session.State.ForestTrainerBattle);
            Assert.AreEqual("pret:" + trainer, session.State.ForestTrainerBattle.Trainer.Value);
            Assert.IsNull(session.State.DoubleBattle);
        }
        Assert.AreEqual(actor.LocalId, session.State.LastTalked);
        Assert.IsNull(session.State.WildBattle);
    }

    [TestMethod]
    public void CompletedRocketStoryHidesTheSourceActorAfterContinue()
    {
        // The current checkpoint already completed the Warehouse. No earlier valid checkpoint
        // is available, so do not fabricate pre-Warehouse progress to start this Rocket battle.
        var session = Start(Place(Outcast, 9, 25));
        var rocket = session.State.Actors.Values.Single(a => a.ScriptId.EndsWith("_Rocket", StringComparison.Ordinal));
        Assert.IsFalse(rocket.Active); session = Continue(session);
        var rejected = session.Execute("hidden-outcast-rocket", session.Revision, new("interact"));
        Assert.AreEqual(CommandStatus.Rejected, rejected.Status);
        Assert.AreEqual("nothing_to_interact", rejected.Error);
        Assert.IsNull(session.State.ForestTrainerBattle);
    }

    [TestMethod]
    public void AvaGebDoubleVictoryPaysOnceSharesDefeatStateAndContinuesBeforeTalkingToGeb()
    {
        const string trainerId = "pret:TRAINER_SIS_AND_BRO_AVA_GEB";
        var session = Start(Place(Outcast, 10, 45, surfing: true));
        Assert.IsFalse(session.State.BattledTrainers.Contains(trainerId));
        int money = session.State.Money;
        Send(session, new("interact")); DrainField(session);
        Assert.AreEqual(trainerId, session.State.DoubleBattle?.Trainer.Value);
        for (int n = 0; session.State.DoubleBattle is { } battle && n < 1500; n++)
        {
            if (battle.Stage is CampaignDoubleBattleStage.Intro or CampaignDoubleBattleStage.Finished || battle.CurrentEvent is not null)
                Send(session, new("ack"));
            else if (battle.LearningPending) Send(session, new("double_learn", MoveSlot: -1));
            else if (battle.ReplacementSlot is { } replacement)
                Send(session, new("double_switch", PartyIndex: replacement,
                    Choice: SourceBattleIdentityRules.LegalReserveIndices(battle.Frame, BattleSide.Player)[0]));
            else if (battle.SelectingSlot is { } slot)
            {
                int target = new[] { 1, 3 }.First(i => battle.Frame.CanAct(new(i)));
                Send(session, new("double_move", PartyIndex: slot, MoveSlot: 0, Choice: target));
            }
            else Send(session, new("ack"));
        }
        Assert.IsNull(session.State.DoubleBattle, "source sibling battle must settle through public commands");
        DrainField(session);
        var history = session.State.DoubleBattleHistory!.Value.Single(h => h.TrainerId == trainerId);
        var trainer = content.SixIslandTrainers!.Trainers[new(trainerId)];
        int reward = 8 * trainer.BaseMoney * trainer.Party[^1].Level;
        Assert.AreEqual(WildBattleOutcome.Won, history.Outcome);
        Assert.AreEqual(reward, history.MoneyGained); Assert.AreEqual(money + reward, session.State.Money);
        Assert.IsTrue(session.State.BattledTrainers.Contains(trainerId));
        Assert.IsTrue(session.State.IsSurfing); Assert.AreEqual("MUS_SURF", session.State.Music);
        session = Continue(session);
        Step(session, Direction.East); Face(session, Direction.North);
        Send(session, new("interact"));
        Assert.AreEqual("SixIsland_OutcastIsland_Text_GebPostBattle", session.State.Dialogue?.TextSymbol);
        DrainField(session);
        Assert.IsNull(session.State.DoubleBattle);
        Assert.AreEqual(1, session.State.DoubleBattleHistory!.Value.Count(h => h.TrainerId == trainerId));
        Assert.AreEqual(money + reward, session.State.Money);
        Continue(session);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CavePublicTurnOrStepProducesDefaultZubatCaptureAndContinue(bool walking)
    {
        var state = EncounterState(Cave, 18, 20, surfing: false);
        var session = Start(state);
        Send(session, new(walking ? "move" : "turn", Direction: walking ? Direction.East : Direction.North)); Drain(session);
        Assert.IsNotNull(session.State.WildBattle);
        var enemy = session.State.WildBattle.State.Opponent.Pokemon;
        Assert.AreEqual(new SpeciesId("pret:SPECIES_ZUBAT"), enemy.Species);
        Assert.AreEqual(41, enemy.SourceSpeciesId);
        Assert.IsTrue(content.LandEncounters![new(Cave)].Slots.Any(s => s.Species == enemy.Species && s.MinimumLevel == enemy.Level));
        Capture(session, "MAPSEC_ALTERING_CAVE");
    }

    private CampaignState EncounterState(string mapName, int x, int y, bool surfing)
    {
        var state = Place(mapName, x, y, Direction.South, surfing, repel: false);
        var map = content.Maps[new(mapName)];
        return state with {
            World = state.World with { Rng = state.World.Rng with { Seed = 3 } },
            Exploration = state.Exploration! with { EarlyEncounter = new(new(0), (ushort)map.SourceBehaviorAt(x, y)!,
                ushort.MaxValue, 21, RulesVersion: EarlyLandEncounterRules.FiveIslandRulesVersion) }
        };
    }

    private void Capture(CampaignSession session, string section)
    {
        Assert.IsNotNull(session.State.WildBattle); Assert.IsNull(session.State.SafariBattle);
        var enemy = session.State.WildBattle.State.Opponent.Pokemon; long instance = session.State.NextPokemonId;
        var position = session.State.World.Player; bool surfing = session.State.IsSurfing;
        Send(session, new("battle_ball", Text: "pret:ITEM_MASTER_BALL")); Drain(session);
        Assert.IsNull(session.State.WildBattle);
        var caught = session.State.Party.Concat(session.State.Storage!.Boxes.SelectMany(b => b.Slots).OfType<PartyPokemon>())
            .Single(p => p.InstanceId == instance);
        Assert.AreEqual(enemy.Species, caught.Pokemon.Species); Assert.AreEqual(enemy.Personality, caught.Pokemon.Personality);
        Assert.AreEqual(enemy.Ivs, caught.Pokemon.Ivs); Assert.AreEqual(section, caught.MetLocation);
        Assert.IsTrue(session.State.CaughtSpecies.Contains(enemy.SourceSpeciesId));
        Assert.AreEqual(position, session.State.World.Player); Assert.AreEqual(surfing, session.State.IsSurfing);
        Continue(session);
    }

    [TestMethod]
    public void OutcastPublicSurfEncounterCapturesAndReturnsToWaterAfterContinue()
    {
        var session = Start(EncounterState(Outcast, 4, 24, surfing: true));
        Send(session, new("turn", Direction: Direction.North)); Drain(session);
        Assert.IsNotNull(session.State.WildBattle);
        var enemy = session.State.WildBattle.State.Opponent.Pokemon;
        Assert.IsTrue(content.WaterEncounters![new(Outcast)].Slots.Any(s => s.Species == enemy.Species &&
            enemy.Level >= s.MinimumLevel && enemy.Level <= s.MaximumLevel));
        Capture(session, "MAPSEC_OUTCAST_ISLAND");
    }

    [TestMethod]
    [DataRow(FishingRod.Old)]
    [DataRow(FishingRod.Good)]
    [DataRow(FishingRod.Super)]
    public void OutcastPublicShoreFishingCapturesWithEachRodAndContinues(FishingRod rod)
    {
        var session = Start(Place(Outcast, 5, 25, Direction.South, repel: false));
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
        Assert.IsNotNull(session.State.WildBattle); Drain(session);
        Capture(session, "MAPSEC_OUTCAST_ISLAND");
        var caught = session.State.Exploration!.Captured!.Value[^1];
        Assert.AreEqual(new MapId(Outcast), caught.Fishing!.Map); Assert.AreEqual(rod, caught.Fishing.Rod);
        Assert.IsNotNull(content.FishingTables.SourceSlot(caught.Fishing));
    }

    [TestMethod]
    public void CaveRejectsFlyAndEscapeRopeUsesActualEntranceAfterContinue()
    {
        var session = Start(Place(Outcast, 7, 22)); EnterCave(session); session = Continue(session);
        int flyer = Enumerable.Range(0, session.State.Party.Length).First(i => FieldFlySourceRules.KnowsFly(session.State.Party[i]));
        var before = CanonicalJson.Hash(session.State);
        var fly = session.Execute("underground-outcast-fly", session.Revision, new("field_fly", PartyIndex: flyer));
        Assert.AreEqual(CommandStatus.Rejected, fly.Status); Assert.AreEqual("field_fly_cannot_use_here", fly.Error);
        Assert.AreEqual(before, CanonicalJson.Hash(session.State));
        Send(session, new("bag_open")); Send(session, new("field_escape_rope", Text: EscapeRopeRules.Item.Value));
        Assert.IsNotNull(session.State.EscapeRope); Assert.AreEqual(Cave, session.State.World.Player.MapId.Value);
        Send(session, new("menu_ack")); DrainField(session);
        Assert.AreEqual(new TilePosition(new(Outcast), 7, 22, 3, Direction.South), session.State.World.Player);
        Assert.AreEqual(0, OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems, EscapeRopeRules.Item));
        Continue(session);
    }

    [TestMethod]
    public void OutdoorOutcastRejectsEscapeRopeAndAllowsPublicFlyDeparture()
    {
        var session = Start(Place(Outcast, 7, 22)); Send(session, new("bag_open"));
        var before = CanonicalJson.Hash(session.State);
        var rope = session.Execute("outdoor-outcast-rope", session.Revision, new("field_escape_rope", Text: EscapeRopeRules.Item.Value));
        Assert.AreEqual(CommandStatus.Rejected, rope.Status); Assert.AreEqual("escape_rope_map_not_allowed", rope.Error);
        Assert.AreEqual(before, CanonicalJson.Hash(session.State)); Send(session, new("menu_back"));
        int flyer = Enumerable.Range(0, session.State.Party.Length).First(session.CanUseFieldFly);
        Send(session, new("field_fly", PartyIndex: flyer));
        var destination = session.FieldFlyDestinations.Single(d => d.MapId.Value == "map.six-island");
        Send(session, new("field_fly_choose", Text: destination.SectionId));
        Send(session, new("field_fly_ack", Text: session.State.FlyEffect!.WaitId));
        Send(session, new("field_fly_ack", Text: session.State.FlyEffect!.WaitId)); DrainField(session);
        Assert.AreEqual(new MapId("map.six-island"), session.State.World.Player.MapId);
        Assert.AreEqual(1, OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems, EscapeRopeRules.Item));
        Continue(session);
    }
}

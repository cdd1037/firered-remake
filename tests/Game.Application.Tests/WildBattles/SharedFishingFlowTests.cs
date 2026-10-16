using System.Collections.Immutable;
using System.Reflection;
using Game.Application;
using Game.Domain;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace FireRed.Tests.Application.WildBattles;

/// <summary>Controlled loaded shores use public casts, canonical captures and the real save contract.</summary>
[TestClass]
public sealed class SharedFishingFlowTests : WildBattleFixture
{
    private CampaignSession Shore(string id)
    {
        var session = CampaignSession.CreateCurrent(content); var state = FieldFixture();
        var map = new MapId(id); var grid = content.ConnectedFieldMaps![map];
        var actors = content.MapDefinition(map).Actors;
        var validateWorld = typeof(CampaignSession).GetMethod("ValidateCampaignWorld", BindingFlags.Instance | BindingFlags.NonPublic)!;
        bool CanStand(int x, int y, Direction facing) => validateWorld.Invoke(session,
            [state with { World = state.World with { Player = new(map, x, y, 3, facing) },
                IsSurfing = false, IsCycling = false, MapEdits = null }, content.Maps[map]]) is null;
        var shore = (from y in Enumerable.Range(1, grid.Height - 2)
                     from x in Enumerable.Range(1, grid.Width - 2)
                     from direction in new[] { (dx: 1, dy: 0, facing: Direction.East), (dx: -1, dy: 0, facing: Direction.West),
                         (dx: 0, dy: 1, facing: Direction.South), (dx: 0, dy: -1, facing: Direction.North) }
                     let here = grid.CellAt(x, y)
                     let front = grid.CellAt(x + direction.dx, y + direction.dy)
                     where here is { Collision: 0, Elevation: 3 } && front is { Collision: 0, Elevation: 1 } &&
                         content.TerrainAt(map, x + direction.dx, y + direction.dy) == 2 && front.SourceBehavior != 0x13 &&
                         !actors.Any(a => a.X == x && a.Y == y || a.X == x + direction.dx && a.Y == y + direction.dy) &&
                         CanStand(x, y, direction.facing)
                     select (x, y, direction.facing)).First();
        var keys = state.Inventory!.KeyItems!.Value;
        foreach (var rod in new[] { "OLD_ROD", "GOOD_ROD", "SUPER_ROD" })
        {
            var item = new ItemId("pret:ITEM_" + rod);
            keys = keys.RemoveAll(k => k.Item == item).Add(new(item, 1));
        }
        state = state with {
            World = state.World with { Player = new(map, shore.x, shore.y, 3, shore.facing) },
            Inventory = state.Inventory with { KeyItems = keys, Balls = [new(new("pret:ITEM_MASTER_BALL"), 1)] },
            Script = StoryState.Empty with { EffectSequence = state.Script.EffectSequence, Variables = state.Script.Variables,
                Flags = state.Script.Flags.SetItem("FLAG_GOT_OLD_ROD", true).SetItem("FLAG_GOT_GOOD_ROD", true).SetItem("FLAG_GOT_SUPER_ROD", true) },
            Dialogue = null, ItemMenu = null, Motions = [], PresentationTicks = 0, PresentationEffects = [],
            EntryPrograms = [], ArrivalFacing = null, IsSurfing = false, IsCycling = false, MapEdits = null };
        var enter = typeof(CampaignSession).GetMethod("EnterMap", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var pump = typeof(CampaignSession).GetMethod("Pump", BindingFlags.Instance | BindingFlags.NonPublic)!;
        state = (CampaignState)enter.Invoke(session, [state, null, Game.Domain.Roaming.RoamerTransition.Warp])!;
        state = (CampaignState)pump.Invoke(session, [state])!;
        typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, content.ContentVersion, session.Revision));
        return session;
    }

    private void Fish(CampaignSession session, FishingRod rod)
    {
        Assert.IsTrue(content.FishingTables.HasTable(rod, session.State.World.Player.MapId));
        var rateRng = session.State.Exploration!.EarlyEncounter!.RateRng;
        for (int cast = 0; cast < 30 && session.State.WildBattle is null; cast++)
        {
            Send(session, new("bag_open"));
            Send(session, new("field_fish", Text: "pret:ITEM_" + rod.ToString().ToUpperInvariant() + "_ROD"));
            Assert.AreEqual(content.FishingTables.RulesVersion, session.State.Fishing!.State.RulesVersion);
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
        Assert.IsNull(session.State.SafariBattle);
        Assert.AreEqual(rateRng, session.State.Exploration!.EarlyEncounter!.RateRng);
        Drain(session);
    }

    [TestMethod]
    [DataRow("map.one-island")]
    [DataRow("map.one-island-kindle-road")]
    [DataRow("map.three-island-bond-bridge")]
    [DataRow("map.three-island-berry-forest")]
    [DataRow("map.four-island")]
    [DataRow("map.four-island-icefall-cave-entrance")]
    [DataRow("map.four-island-icefall-cave-back")]
    [DataRow("map.five-island")]
    [DataRow("map.five-island-meadow")]
    [DataRow("map.six-island-water-path")]
    [DataRow("map.six-island-ruin-valley")]
    [DataRow("map.seven-island-tanoby-ruins")]
    [DataRow("map.seven-island-trainer-tower")]
    public void EveryNewGoodRodMapCapturesAndContinuesFromGeneratedDefinition(string map)
        => Capture(map, FishingRod.Good);

    [TestMethod]
    [DataRow("map.fuchsia-city", FishingRod.Old)]
    [DataRow("map.fuchsia-city", FishingRod.Good)]
    [DataRow("map.fuchsia-city", FishingRod.Super)]
    [DataRow("map.seafoam-islands-b3f", FishingRod.Old)]
    [DataRow("map.seafoam-islands-b3f", FishingRod.Good)]
    [DataRow("map.seafoam-islands-b3f", FishingRod.Super)]
    public void AllRodsUseSharedOriginAndContinueIncludingFormerRegionalReceiptMaps(string map, FishingRod rod)
        => Capture(map, rod);

    private void Capture(string map, FishingRod rod)
    {
        var session = Shore(map); Fish(session, rod);
        var battle = session.State.WildBattle!;
        Assert.AreSame(content.FishingTables, battle.Frame.WildEncounter!.FishingTables);
        var target = battle.Frame.Party(Game.Domain.Battle.BattleSide.Opponent).Members[0].Pokemon;
        var table = content.FishingTables.Table(rod, new(map));
        Assert.IsTrue(table.Slots.Any(slot => slot.Species == target.Species && target.Level >= slot.MinimumLevel && target.Level <= slot.MaximumLevel));
        Send(session, new("battle_ball", Text: "pret:ITEM_MASTER_BALL")); Drain(session);
        Assert.IsNull(session.State.WildBattle);
        var capture = session.State.Exploration!.Captured!.Value[^1];
        Assert.AreEqual(target.Personality, capture.Personality); Assert.AreEqual(target.Ivs, capture.Ivs);
        Assert.AreEqual("ITEM_MASTER_BALL", capture.Ball);
        Assert.IsNull(capture.Seafoam);
        Assert.IsNotNull(capture.Fishing);
        if (capture.Fishing is { } origin)
        {
            Assert.AreEqual(new MapId(map), origin.Map); Assert.AreEqual(rod, origin.Rod);
            Assert.IsNotNull(content.FishingTables.SourceSlot(origin));
        }
        StrictRoundtrip(session);
        if (map == "map.seafoam-islands-b3f" && rod is FishingRod.Old or FishingRod.Good)
        {
            // Equivalent pre-unification payloads must remain readable; new casts above never emit them.
            var firstGenerationDraw = battle.RandomTrace.First(draw => draw.Purpose == "wild.nature");
            var before = new RngState(firstGenerationDraw.Before, firstGenerationDraw.Cursor - 1);
            var legacyOrigin = new SeafoamWildOrigin(new(map), rod == FishingRod.Old
                ? SeafoamEncounterKind.OldRod : SeafoamEncounterKind.GoodRod, capture.Fishing!.SlotIndex);
            var made = SeafoamPokemonFactory.GenerateSourceSlot(content.PokemonContent!.Pokemon, legacyOrigin,
                checked((byte)target.Level), before, session.State.TrainerId, content.FishingTables);
            Assert.AreEqual(CanonicalJson.Hash(target), CanonicalJson.Hash(made.Pokemon));
            var exploration = session.State.Exploration!;
            var captured = exploration.Captured!.Value;
            var history = exploration.Battles.Single(row => row.Id == capture.BattleId);
            var legacy = session.State with { Exploration = exploration with {
                Battles = exploration.Battles.SetItem(exploration.Battles.IndexOf(history), history with {
                    Fishing = null, SeafoamReceipt = new(legacyOrigin, before, made.GlobalRng, made.Pokemon) }),
                Captured = captured.SetItem(captured.IndexOf(capture),
                    capture with { Fishing = null, Seafoam = legacyOrigin }) } };
            typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(session, new CommandSession<CampaignState, CampaignCommand>(legacy, session.RulesVersion, content.ContentVersion, session.Revision));
            StrictRoundtrip(session);
        }
    }
}

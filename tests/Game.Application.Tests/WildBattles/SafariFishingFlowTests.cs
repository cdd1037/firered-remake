using System.Collections.Immutable;
using System.Reflection;
using Game.Application;
using Game.Domain;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.Safari;
using Game.Domain.World;

namespace FireRed.Tests.Application.WildBattles;

/// <summary>Controlled shore/seed fixtures exercise public bag, fishing, Safari actions, exit and Continue.</summary>
[TestClass]
public sealed class SafariFishingFlowTests : WildBattleFixture
{
    private void Set(CampaignSession session, CampaignState state) => typeof(CampaignSession)
        .GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!
        .SetValue(session, new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, content.ContentVersion, session.Revision));

    private CampaignState Place(CampaignSession session, CampaignState state, MapId map, int x, int y, Direction direction)
    {
        state = state with { World = state.World with { Player = new(map, x, y, content.Maps[map].GetTile(x, y)!.Elevation, direction) },
            Script = StoryState.Empty with { Flags = state.Script.Flags, Variables = state.Script.Variables, EffectSequence = state.Script.EffectSequence },
            Dialogue = null, ItemMenu = null, Motions = [], PresentationTicks = 0, PresentationEffects = [],
            EntryPrograms = [], ArrivalFacing = null, IsSurfing = false, IsCycling = false, MapEdits = null };
        var enter = typeof(CampaignSession).GetMethod("EnterMap", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var pump = typeof(CampaignSession).GetMethod("Pump", BindingFlags.Instance | BindingFlags.NonPublic)!;
        state = (CampaignState)enter.Invoke(session, [state, null, Game.Domain.Roaming.RoamerTransition.Warp])!;
        return (CampaignState)pump.Invoke(session, [state])!;
    }

    private CampaignSession Shore(string area = "center", bool active = true)
    {
        var session = CampaignSession.CreateCurrent(content); var state = FieldFixture();
        var map = new MapId("map.safari-zone-" + area); var grid = content.ConnectedFieldMaps![map];
        var actors = content.MapDefinition(map).Actors;
        var shore = (from y in Enumerable.Range(1, grid.Height - 2)
                     from x in Enumerable.Range(1, grid.Width - 2)
                     from direction in new[] { (dx: 1, dy: 0, facing: Direction.East), (dx: -1, dy: 0, facing: Direction.West),
                         (dx: 0, dy: 1, facing: Direction.South), (dx: 0, dy: -1, facing: Direction.North) }
                     let here = grid.CellAt(x, y)
                     let front = grid.CellAt(x + direction.dx, y + direction.dy)
                     where here is { Collision: 0, Elevation: 3 } && front is { Collision: 0, Elevation: 1 } &&
                         content.TerrainAt(map, x + direction.dx, y + direction.dy) == 2 && front.SourceBehavior != 0x13 &&
                         !actors.Any(a => a.X == x && a.Y == y || a.X == x + direction.dx && a.Y == y + direction.dy)
                     select (x, y, direction.facing)).First();
        var keys = state.Inventory!.KeyItems!.Value;
        foreach (var rod in new[] { "OLD_ROD", "GOOD_ROD", "SUPER_ROD" })
        {
            var id = new ItemId("pret:ITEM_" + rod);
            keys = keys.RemoveAll(k => k.Item == id).Add(new(id, 1));
        }
        state = state with { Inventory = state.Inventory with { KeyItems = keys },
            Script = state.Script with { Flags = state.Script.Flags.SetItem("FLAG_SYS_SAFARI_MODE", active)
                .SetItem("FLAG_GOT_OLD_ROD", true).SetItem("FLAG_GOT_GOOD_ROD", true).SetItem("FLAG_GOT_SUPER_ROD", true) },
            SafariVisit = active ? new(30, 600, true) : null };
        state = Place(session, state, map, shore.x, shore.y, shore.facing);
        Set(session, state);
        return session;
    }

    private void Fish(CampaignSession session, FishingRod rod)
    {
        var before = session.State; int casts = 0;
        while (session.State.SafariBattle is null && casts++ < 30)
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
            Assert.IsNull(session.State.Fishing, "finite source fishing task");
        }
        Assert.IsNotNull(session.State.SafariBattle, "bounded deterministic casts reach a real encounter");
        Assert.IsNull(session.State.WildBattle);
        Assert.AreEqual(before.SafariVisit, session.State.SafariVisit, "casts consume neither steps nor balls");
        Assert.AreEqual(before.Exploration!.EarlyEncounter!.RateRng, session.State.Exploration!.EarlyEncounter!.RateRng, "fishing does not draw land encounter-rate RNG");
        Assert.AreEqual(rod, session.State.SafariBattle.Fishing!.Rod);
        Assert.IsNull(session.State.Exploration.EarlyEncounter.Pending);
        var battle = session.State.SafariBattle;
        CollectionAssert.AreEqual(new[] { "safari.entry.ai-setup.slot-0", "safari.entry.ai-setup.slot-1",
            "safari.entry.ai-setup.slot-2", "safari.entry.ai-setup.slot-3", "safari.fishing.held-item", "safari:turn-random" },
            battle.RandomTrace.TakeLast(6).Select(t => t.Purpose).ToArray());
        var heldDraw = battle.RandomTrace[^2];
        Assert.AreEqual(100, heldDraw.Bound);
        Assert.AreEqual(battle.State.Target.SourceSpeciesId == 148 && heldDraw.Result >= 95 ? "ITEM_DRAGON_FANG" : "ITEM_NONE", battle.HeldItem);
        for (int i = 1; i < battle.RandomTrace.Length; i++)
        {
            Assert.AreEqual(battle.RandomTrace[i - 1].After, battle.RandomTrace[i].Before);
            Assert.AreEqual(battle.RandomTrace[i - 1].Cursor + 1, battle.RandomTrace[i].Cursor);
        }
    }

    private void DrainSafari(CampaignSession session)
    {
        for (int n = 0; n < 100 && session.State.SafariBattle is { } battle; n++)
        {
            if (battle.Capture is { Stage: CampaignCaptureStage.NicknameChoice }) Send(session, new("capture_choice", Choice: 0));
            else if (battle.Phase == CampaignBattlePhase.ChooseMove && battle.Capture is null) return;
            else Send(session, new("ack"));
        }
    }

    private void Retire(CampaignSession session)
    {
        Send(session, new("safari_retire"));
        CompleteExit(session);
    }

    private void CompleteExit(CampaignSession session)
    {
        for (int n = 0; n < 150 && (session.State.SafariVisit is not null || session.State.Script.Wait is not null || !session.State.EntryPrograms.IsEmpty); n++)
            Send(session, new("ack", Choice: session.State.Script.Wait?.Kind == StoryWaitKind.Choice ? 1 : null));
        Assert.IsNull(session.State.SafariVisit);
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault("FLAG_SYS_SAFARI_MODE"));
    }

    [TestMethod]
    [DataRow("center", FishingRod.Old)]
    [DataRow("east", FishingRod.Good)]
    [DataRow("north", FishingRod.Super)]
    [DataRow("west", FishingRod.Super)]
    public void PublicRodEntryUsesSafariCommandsWithoutOrdinaryRewards(string area, FishingRod rod)
    {
        var session = Shore(area); var party = CanonicalJson.Hash(session.State.Party);
        int money = session.State.Money; var balls = CanonicalJson.Hash(session.State.Inventory!.Balls);
        Fish(session, rod); DrainSafari(session);
        var battle = session.State.SafariBattle!;
        var source = content.SafariFishing.SourceSlot(battle.Fishing);
        Assert.IsNotNull(source);
        var ordinary = session.Execute("safari-no-fight", session.Revision, new("fight", MoveSlot: 0));
        Assert.AreEqual(CommandStatus.Rejected, ordinary.Status);
        Send(session, new("safari_action", Text: "run")); DrainSafari(session);
        Assert.IsNull(session.State.SafariBattle);
        Assert.AreEqual(party, CanonicalJson.Hash(session.State.Party), "no HP/PP/EXP/EV changes");
        Assert.AreEqual(money, session.State.Money); Assert.AreEqual(balls, CanonicalJson.Hash(session.State.Inventory!.Balls));
        Assert.AreEqual(0, session.State.Exploration!.Battles[^1].ExperienceGained);
        Assert.IsTrue(session.State.Exploration.Battles[^1].Safari);
        Retire(session); StrictRoundtrip(session);
    }

    [TestMethod]
    public void InactiveVisitRejectsCastAtomicallyBeforeRng()
    {
        var session = Shore(active: false); Send(session, new("bag_open"));
        var before = CanonicalJson.Hash(session.State); var rng = session.State.World.Rng;
        var result = session.Execute("inactive-safari-fish", session.Revision, new("field_fish", Text: "pret:ITEM_SUPER_ROD"));
        Assert.AreEqual(CommandStatus.Rejected, result.Status); Assert.AreEqual("safari_fishing_not_active", result.Error);
        Assert.AreEqual(before, CanonicalJson.Hash(session.State)); Assert.AreEqual(rng, session.State.World.Rng);
    }

    [TestMethod]
    [DataRow(FishingRod.Old, 123u, 129)]
    [DataRow(FishingRod.Super, 32u, 147)]
    [DataRow(FishingRod.Super, 347u, 148)]
    public void CaptureUsesSafariBallPreservesOriginAndSurvivesExitContinue(FishingRod rod, uint rootSeed, int species)
    {
        var session = Shore();
        var party = session.State.Party.ToDictionary(p => p.InstanceId, p => CanonicalJson.Hash(p));
        Set(session, session.State with { World = session.State.World with { Rng = new(rootSeed, session.State.World.Rng.Cursor) } });
        Fish(session, rod); DrainSafari(session);
        Assert.AreEqual(species, session.State.SafariBattle!.State.Target.SourceSpeciesId);
        var battle = session.State.SafariBattle!;
        // Explicit controlled RNG chooses a successful legal Safari throw, never an injected outcome.
        uint seed = Enumerable.Range(0, 100000).Select(i => (uint)i).First(i =>
            SafariBattleRules.Apply(mechanics, battle.State with { Rng = new(i) }, SafariAction.Ball, true).State?.Outcome == SafariBattleOutcome.Caught);
        Set(session, session.State with { World = session.State.World with { Rng = new(seed, session.State.World.Rng.Cursor) },
            SafariBattle = battle with { State = battle.State with { Rng = new(seed, session.State.World.Rng.Cursor) } } });
        int count = session.State.Exploration!.Captured!.Value.Length;
        Send(session, new("safari_action", Text: "ball")); DrainSafari(session);
        Assert.IsNull(session.State.SafariBattle); Assert.AreEqual(29, session.State.SafariVisit!.Balls);
        var capture = session.State.Exploration!.Captured!.Value.Last();
        Assert.AreEqual(count + 1, session.State.Exploration.Captured.Value.Length);
        Assert.AreEqual("ITEM_SAFARI_BALL", capture.Ball); Assert.AreEqual(battle.Fishing, capture.Fishing);
        Assert.AreEqual("MAPSEC_KANTO_SAFARI_ZONE", capture.MetLocation);
        foreach (var member in session.State.Party.Where(p => party.ContainsKey(p.InstanceId)))
            Assert.AreEqual(party[member.InstanceId], CanonicalJson.Hash(member), "capture awards no HP/PP/EXP/EV changes");
        Assert.AreEqual(0, session.State.Exploration.Battles.Last().ExperienceGained);
        Retire(session); StrictRoundtrip(session);
        var invalid = session.State with { Exploration = session.State.Exploration with {
            Captured = session.State.Exploration.Captured.Value.SetItem(count, capture with { Fishing = capture.Fishing! with { Map = new("map.fuchsia-city") } }) } };
        Assert.IsNotNull(session.ValidatePlayerSave(invalid), "Continue rejects forged Safari fishing origin");
    }

    [TestMethod]
    public void RepeatedRunSettlementsDoNotGrowHistory()
    {
        var session = Shore(); int initial = session.State.Exploration!.Battles.Length;
        for (int i = 0; i < 3; i++) { Fish(session, FishingRod.Good); DrainSafari(session); Send(session, new("safari_action", Text: "run")); DrainSafari(session); }
        Assert.IsTrue(session.State.Exploration!.Battles.Length <= initial + 1);
        Retire(session); StrictRoundtrip(session);
    }
    [TestMethod]
    public void PaidEntryAndRetirementUseOriginalVisitScript()
    {
        var session = CampaignSession.CreateCurrent(content); var state = FieldFixture();
        state = state with { Script = state.Script with {
            Flags = state.Script.Flags.SetItem("FLAG_SYS_SAFARI_MODE", false),
            Variables = state.Script.Variables.SetItem("VAR_MAP_SCENE_FUCHSIA_CITY_SAFARI_ZONE_ENTRANCE", 0) }, SafariVisit = null };
        Set(session, Place(session, state, new("map.fuchsia-city-safari-zone-entrance"), 4, 4, Direction.North));
        int money = session.State.Money;
        Send(session, new("move", Direction.North));
        for (int n = 0; n < 200 && (session.State.World.Player.MapId.Value != "map.safari-zone-center" ||
            session.State.Script.Wait is not null || !session.State.EntryPrograms.IsEmpty); n++)
            Send(session, new("ack", Choice: session.State.Script.Wait?.Kind == StoryWaitKind.Choice ? 1 : null));
        Assert.AreEqual("map.safari-zone-center", session.State.World.Player.MapId.Value);
        Assert.AreEqual(money - 500, session.State.Money);
        Assert.AreEqual(new CampaignSafariVisit(30, 600, true), session.State.SafariVisit);
        Retire(session); StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LastBallFishingCompletesOriginalExitAndContinue(bool caught)
    {
        var session = Shore(); Fish(session, FishingRod.Old); DrainSafari(session);
        var battle = session.State.SafariBattle!;
        var targetOutcome = caught ? SafariBattleOutcome.Caught : SafariBattleOutcome.OutOfBalls;
        uint seed = Enumerable.Range(0, 100000).Select(i => (uint)i).First(i =>
            SafariBattleRules.Apply(mechanics, battle.State with { BallsRemaining = 1, Rng = new(i) }, SafariAction.Ball, true).State?.Outcome == targetOutcome);
        Set(session, session.State with { World = session.State.World with { Rng = new(seed, session.State.World.Rng.Cursor) },
            SafariVisit = session.State.SafariVisit! with { Balls = 1 },
            SafariBattle = battle with { State = battle.State with { BallsRemaining = 1, Rng = new(seed, session.State.World.Rng.Cursor) } } });
        Send(session, new("safari_action", Text: "ball")); DrainSafari(session); CompleteExit(session);
        Assert.IsNull(session.State.SafariBattle); StrictRoundtrip(session);
    }

}

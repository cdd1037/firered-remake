using System.Collections.Immutable;
using System.Reflection;
using Game.Application;
using Game.Content;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>
/// Controlled positions and pre-Celio flags exercise the real scripts with public commands.
/// High Hall of Fame counts are isolated transition fixtures, never fabricated league history.
/// Only the unchanged recorded count is claimed to pass strict save/Continue.
/// </summary>
[TestClass, TestCategory("Game")]
public sealed class LoreleiHouseTests
{
    private const string Town = "map.four-island";
    private const string House = "map.four-island-loreleis-house";
    private const string HideLorelei = "FLAG_HIDE_LORELEI_IN_HER_HOUSE";
    private const string Warehouse = "FLAG_DEFEATED_ROCKETS_IN_WAREHOUSE";
    private const string Talked = "FLAG_TALKED_TO_LORELEI_AFTER_WAREHOUSE";
    private const string Hof = "GAME_STAT_ENTERED_HOF";
    private const string Prefix = "FourIsland_LoreleisHouse_Text_";
    private static PretCampaignCatalog Content => CurrentCampaignFixture.Catalog;
    private static readonly (int Minimum, int Actor, string Flag)[] Unlocks =
    [
        (25, 8, "FLAG_HIDE_LORELEI_HOUSE_MEOWTH_DOLL"),
        (50, 9, "FLAG_HIDE_LORELEI_HOUSE_CHANSEY_DOLL"),
        (75, 11, "FLAG_HIDE_LORELEIS_HOUSE_NIDORAN_F_DOLL"),
        (100, 10, "FLAG_HIDE_LORELEI_HOUSE_JIGGLYPUFF_DOLL"),
        (125, 12, "FLAG_HIDE_LORELEIS_HOUSE_NIDORAN_M_DOLL"),
        (150, 14, "FLAG_HIDE_LORELEIS_HOUSE_FEAROW_DOLL"),
        (175, 13, "FLAG_HIDE_LORELEIS_HOUSE_PIDGEOT_DOLL"),
        (200, 15, "FLAG_HIDE_LORELEIS_HOUSE_LAPRAS_DOLL")
    ];
    private int commandId;

    private void Set(CampaignSession session, CampaignState state) => typeof(CampaignSession)
        .GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!
        .SetValue(session, new CommandSession<CampaignState, CampaignCommand>(state,
            session.RulesVersion, Content.ContentVersion,
            Math.Max(session.Revision, CurrentCampaignFixture.Checkpoint.Generation)));

    private CampaignSession Visit(CampaignState? state = null, string mapName = House,
        int x = 4, int y = 6, Direction facing = Direction.North)
    {
        var session = CampaignSession.CreateCurrent(Content);
        Place(session, state ?? CurrentCampaignFixture.Checkpoint.State, mapName, x, y, facing);
        return session;
    }

    private void Place(CampaignSession session, CampaignState state, string mapName,
        int x, int y, Direction facing)
    {
        var map = new MapId(mapName);
        Assert.IsFalse(Content.Maps[map].GetTile(x, y)!.Blocked, "controlled position must be walkable");
        state = state with
        {
            World = state.World with { Player = new(map, x, y, Content.Maps[map].GetTile(x, y)!.Elevation, facing) },
            Script = StoryState.Empty with { Flags = state.Script.Flags, Variables = state.Script.Variables,
                EffectSequence = state.Script.EffectSequence },
            Dialogue = null, ItemMenu = null, MonPicture = null, Motions = [], PresentationTicks = 0,
            PresentationEffects = [], EntryPrograms = [], ArrivalFacing = null,
            IsSurfing = false, IsCycling = false, BicycleSlope = false, MapEdits = null
        };
        var enter = typeof(CampaignSession).GetMethod("EnterMap", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var pump = typeof(CampaignSession).GetMethod("Pump", BindingFlags.Instance | BindingFlags.NonPublic)!;
        state = (CampaignState)enter.Invoke(session, [state, null, Game.Domain.Roaming.RoamerTransition.Warp])!;
        Set(session, (CampaignState)pump.Invoke(session, [state])!);
        Finish(session);
    }

    private void Send(CampaignSession session, CampaignCommand command)
    {
        string id = "lorelei-house:" + commandId++;
        var revision = session.Revision;
        var result = session.Execute(id, revision, command);
        Assert.AreEqual(CommandStatus.Accepted, result.Status, command + ": " + result.Error);
        var stale = session.Execute(id, revision, command);
        Assert.AreEqual(CommandStatus.Rejected, stale.Status);
        Assert.AreEqual("stale_revision", stale.Error);
        Assert.AreSame(result.State, stale.State);
    }

    private HashSet<string> Finish(CampaignSession session)
    {
        var labels = new HashSet<string>(StringComparer.Ordinal);
        for (int n = 0; n < 150; n++)
        {
            if (session.State.Dialogue is { } dialogue) labels.Add(dialogue.TextSymbol);
            if (session.State.Dialogue is null && session.State.Script.Completed && session.State.EntryPrograms.IsEmpty)
                return labels;
            Send(session, session.State.Script.Wait?.Kind == StoryWaitKind.ButtonPress
                ? new("button_press", Text: "A") : new("ack"));
        }
        Assert.Fail("Lorelei house source script did not finish.");
        return labels;
    }

    private CampaignSession Continue(CampaignSession session)
    {
        Assert.IsNull(session.ValidatePlayerSave(session.State), "strict player save");
        var bytes = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, Content.ContentVersion);
        var decoded = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, Content.ContentVersion,
            validate: session.ValidatePlayerSave);
        var restored = CampaignSession.RestoreCurrent(Content, decoded.State, decoded.Generation);
        Assert.AreEqual(session.Revision, restored.Revision);
        Assert.AreEqual(CanonicalJson.Hash(session.State), CanonicalJson.Hash(restored.State), "exact Continue payload");
        return restored;
    }

    [TestMethod]
    public void PhysicalDoorAndExitRoundTripContinueWithRecordedLowCount()
    {
        var session = Visit(mapName: Town, x: 33, y: 24);
        for (int n = 0; n < 2 && session.State.World.Player.MapId.Value == Town; n++)
        { Send(session, new("move", Direction.North)); Finish(session); }
        Assert.AreEqual((House, 4, 7),
            (session.State.World.Player.MapId.Value, session.State.World.Player.X, session.State.World.Player.Y));
        Assert.IsFalse(session.State.IsCycling);
        Assert.IsFalse(session.State.IsSurfing);
        Assert.AreEqual(6, session.State.Actors.Values.Count(a => a.Visible));
        session = Continue(session);
        for (int n = 0; n < 3 && session.State.World.Player.MapId.Value == House; n++)
        { Send(session, new("move", Direction.South)); Finish(session); }
        Assert.AreEqual((Town, 33, 24),
            (session.State.World.Player.MapId.Value, session.State.World.Player.X, session.State.World.Player.Y));
        session = Continue(session);
        for (int n = 0; n < 2 && session.State.World.Player.MapId.Value == Town; n++)
        { Send(session, new("move", Direction.North)); Finish(session); }
        Assert.AreEqual(House, session.State.World.Player.MapId.Value);
        Continue(session);
    }

    [TestMethod]
    public void InitialSourceFlagsHideLoreleiAndExtraDollsBeforeIcefall()
    {
        // Place the actual new-game state in the house only to inspect its initial visibility.
        // This deliberately bypasses the travel prerequisites, not the reset script or hide flags.
        var initial = CampaignSession.CreateCurrent(Content).State;
        Assert.IsTrue(initial.Script.Flags.GetValueOrDefault(HideLorelei));
        foreach (var row in Unlocks) Assert.IsTrue(initial.Script.Flags.GetValueOrDefault(row.Flag), row.Flag);
        var session = Visit(initial, x: 7, y: 5);
        Assert.IsFalse(session.State.Actors.Values.Single(a => a.SourceLocalId == 1).Visible);
        Assert.AreEqual(6, session.State.Actors.Values.Count(a => a.Visible));
        var before = session.State;
        var result = session.Execute("hidden-before-icefall", session.Revision, new("interact"));
        Assert.AreEqual(CommandStatus.Rejected, result.Status);
        Assert.AreEqual("nothing_to_interact", result.Error);
        Assert.AreSame(before, result.State);
    }

    [TestMethod]
    public void CompletedCelioReceiptKeepsLoreleiHiddenAfterContinue()
    {
        var original = CurrentCampaignFixture.Checkpoint.State;
        Assert.IsNotNull(original.CelioHandoff?.Initialization);
        Assert.IsTrue(original.Script.Flags.GetValueOrDefault(HideLorelei));
        var session = Continue(Visit(original, x: 7, y: 5));
        Assert.IsFalse(session.State.Actors.Values.Single(a => a.SourceLocalId == 1).Visible);
        var result = session.Execute("hidden-after-celio", session.Revision, new("interact"));
        Assert.AreEqual(CommandStatus.Rejected, result.Status);
        Assert.AreEqual("nothing_to_interact", result.Error);
        Continue(session);
    }

    [TestMethod]
    [DataRow(false, false, "IfAnythingWereToHappenToIsland")]
    [DataRow(true, false, "IllReturnToLeagueInShortWhile")]
    [DataRow(true, true, "WillDoWhatICanHereAndNow")]
    [DataRow(false, true, "WillDoWhatICanHereAndNow")]
    public void LoreleiUsesSourceDialogueOrderAndOneTimeTalkedFlag(bool warehouse, bool talked, string text)
    {
        var state = CurrentCampaignFixture.Checkpoint.State;
        state = state with { Script = state.Script with { Flags = state.Script.Flags
            .SetItem(HideLorelei, false).SetItem(Warehouse, warehouse).SetItem(Talked, talked) } };
        var session = Visit(state, x: 7, y: 5);
        Assert.IsTrue(session.State.Actors.Values.Single(a => a.SourceLocalId == 1).Visible);
        var inventory = CanonicalJson.Hash(session.State.Inventory);
        var stats = CanonicalJson.Hash(session.State.GameStats);
        var rng = session.State.World.Rng;
        Send(session, new("interact"));
        Assert.AreEqual(Prefix + text, session.State.Dialogue?.TextSymbol);
        CollectionAssert.AreEquivalent(new[] { Prefix + text }, Finish(session).ToArray());
        Assert.AreEqual(talked || warehouse, session.State.Script.Flags.GetValueOrDefault(Talked));
        Assert.AreEqual(inventory, CanonicalJson.Hash(session.State.Inventory));
        Assert.AreEqual(stats, CanonicalJson.Hash(session.State.GameStats));
        Assert.AreEqual(rng, session.State.World.Rng);
        if (warehouse && !talked)
        {
            Send(session, new("interact"));
            CollectionAssert.AreEquivalent(new[] { Prefix + "WillDoWhatICanHereAndNow" }, Finish(session).ToArray());
        }
    }

    [TestMethod]
    [DataRow(0)] [DataRow(24)] [DataRow(25)] [DataRow(49)] [DataRow(50)]
    [DataRow(74)] [DataRow(75)] [DataRow(99)] [DataRow(100)] [DataRow(124)]
    [DataRow(125)] [DataRow(149)] [DataRow(150)] [DataRow(174)] [DataRow(175)]
    [DataRow(199)] [DataRow(200)] [DataRow(201)]
    public void MapTransitionClearsExactlyTheOriginalThresholdFlagsBeforeSpawning(int count)
    {
        var state = CurrentCampaignFixture.Checkpoint.State;
        var flags = state.Script.Flags;
        foreach (var row in Unlocks) flags = flags.SetItem(row.Flag, true);
        state = state with { Script = state.Script with { Flags = flags }, GameStats = state.GameStats!.SetItem(Hof, count) };
        var session = Visit(state);
        foreach (var row in Unlocks)
        {
            Assert.AreEqual(count < row.Minimum, session.State.Script.Flags[row.Flag], row.Flag);
            Assert.AreEqual(count >= row.Minimum, session.State.Actors.Values.Single(a => a.SourceLocalId == row.Actor).Visible,
                "spawn must follow OnTransition: " + row.Flag);
        }
        Assert.AreEqual(6 + Unlocks.Count(r => count >= r.Minimum), session.State.Actors.Values.Count(a => a.Visible));
        Assert.AreEqual(state.World.Rng, session.State.World.Rng);
        Assert.AreEqual(CanonicalJson.Hash(state.GameStats), CanonicalJson.Hash(session.State.GameStats));
        Assert.AreEqual(CanonicalJson.Hash(state.HallOfFame), CanonicalJson.Hash(session.State.HallOfFame));
    }

    [TestMethod]
    public void ThresholdFixtureCannotMasqueradeAsRecordedHallOfFameSave()
    {
        var state = CurrentCampaignFixture.Checkpoint.State;
        var session = Visit(state with { GameStats = state.GameStats!.SetItem(Hof, 200) });
        Assert.AreEqual("save_pokemon_league_hof_state", session.ValidatePlayerSave(session.State));
        Assert.ThrowsExactly<InvalidDataException>(() => CampaignSession.RestoreCurrent(Content, session.State, session.Revision));
    }

    [TestMethod]
    public void ReentryNeverHidesPreviouslyUnlockedDollsWhenTheCountIsLower()
    {
        var state = CurrentCampaignFixture.Checkpoint.State;
        var session = Visit(state with { GameStats = state.GameStats!.SetItem(Hof, 200) });
        Place(session, session.State with { GameStats = state.GameStats }, House, 4, 6, Direction.North);
        foreach (var row in Unlocks)
        {
            Assert.IsFalse(session.State.Script.Flags[row.Flag]);
            Assert.IsTrue(session.State.Actors.Values.Single(a => a.SourceLocalId == row.Actor).Visible);
        }
        // Source flags are persistent clear-only rewards. This state uses the real recorded
        // statistic and does not add a collection counter or invented league completions.
        session = Continue(session);
        Assert.AreEqual(14, session.State.Actors.Values.Count(a => a.Visible));
    }

    [TestMethod]
    [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)] [DataRow(6)] [DataRow(7)]
    [DataRow(8)] [DataRow(9)] [DataRow(10)] [DataRow(11)] [DataRow(12)] [DataRow(15)]
    public void EachReachableSourceDollRunsTheSharedMessageWithoutRewardOrProgress(int sourceLocalId)
    {
        var state = CurrentCampaignFixture.Checkpoint.State;
        var definition = Content.MapDefinition(new(House)).Actors.Single(a => a.SourceLocalId == sourceLocalId);
        // Isolate each doll with its own original hide flag cleared, preserving all permanent dolls.
        if (definition.HideFlag != "0")
            state = state with { Script = state.Script with { Flags = state.Script.Flags.SetItem(definition.HideFlag, false) } };
        var session = Visit(state);
        var map = Content.Maps[new(House)];
        var positions = new[] { (0, 1, Direction.North), (1, 0, Direction.West), (-1, 0, Direction.East), (0, -1, Direction.South) };
        var approach = positions.Select(p => (X: definition.X + p.Item1, Y: definition.Y + p.Item2, Facing: p.Item3))
            .First(p => map.GetTile(p.X, p.Y) is { Blocked: false } &&
                !session.State.Actors.Values.Any(a => a.Visible && a.X == p.X && a.Y == p.Y));
        Place(session, session.State, House, approach.X, approach.Y, approach.Facing);
        var inventory = CanonicalJson.Hash(session.State.Inventory);
        var stats = CanonicalJson.Hash(session.State.GameStats);
        var flags = CanonicalJson.Hash(session.State.Script.Flags);
        var rng = session.State.World.Rng;
        Send(session, new("interact"));
        Assert.AreEqual(sourceLocalId, session.State.Actors[session.State.LastTalked!].SourceLocalId);
        CollectionAssert.AreEquivalent(new[] { Prefix + "StuffedMonDollsGalore" }, Finish(session).ToArray());
        Assert.AreEqual(inventory, CanonicalJson.Hash(session.State.Inventory));
        Assert.AreEqual(stats, CanonicalJson.Hash(session.State.GameStats));
        Assert.AreEqual(flags, CanonicalJson.Hash(session.State.Script.Flags));
        Assert.AreEqual(rng, session.State.World.Rng);
        Continue(session);
    }

    [TestMethod]
    [DataRow(13, 9)]
    [DataRow(14, 10)]
    public void OriginalBirdDollsRemainVisibleOnTheirUnreachableWallShelf(int sourceLocalId, int x)
    {
        var state = CurrentCampaignFixture.Checkpoint.State;
        var actor = Content.MapDefinition(new(House)).Actors.Single(a => a.SourceLocalId == sourceLocalId);
        state = state with { Script = state.Script with { Flags = state.Script.Flags.SetItem(actor.HideFlag, false) } };
        var session = Visit(state, x: x, y: 2);
        Assert.AreEqual((x, 0), (actor.X, actor.Y));
        Assert.IsTrue(session.State.Actors[actor.LocalId].Visible);
        var map = Content.Maps[new(House)];
        foreach (var (dx, dy) in new[] { (0, 1), (1, 0), (-1, 0), (0, -1) })
            Assert.IsFalse(map.GetTile(actor.X + dx, actor.Y + dy) is { Blocked: false },
                "source shelf does not expose an adjacent standing tile");
        Assert.AreEqual(0x81, map.SourceBehaviorAt(x, 1), "source bookshelf, not MB_COUNTER (0x80)");
        // field_control_avatar.c::GetInteractedObjectEventScript extends reach only
        // across MB_COUNTER. The adjacent bookshelf handles A instead of the bird.
        Send(session, new("interact"));
        Assert.IsNull(session.State.LastTalked);
        var labels = Finish(session);
        Assert.IsFalse(labels.Contains(Prefix + "StuffedMonDollsGalore"));
        Assert.AreEqual(1, labels.Count, "source bookshelf interaction remains executable");
        Continue(session);
    }
}

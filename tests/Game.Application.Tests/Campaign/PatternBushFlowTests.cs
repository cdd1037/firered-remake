using System.Collections.Immutable;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>Controlled initial locations; travel, scripts, battles, capture and Continue use public commands.</summary>
[TestClass, TestCategory("Game")]
public sealed class PatternBushFlowTests : WildBattleFixture
{
    private const string Water = "map.six-island-water-path";
    private const string Green = "map.six-island-green-path";
    private const string Bush = "map.six-island-pattern-bush";

    private CampaignState Place(string mapName, int x, int y, Direction facing = Direction.North)
    {
        var state = FieldFixture();
        var map = content.Maps[new(mapName)];
        var definition = content.MapDefinition(map.Id);
        state = state with {
            World = state.World with { Player = new(map.Id, x, y, map.GetTile(x, y)!.Elevation, facing) },
            Actors = definition.Actors.ToImmutableSortedDictionary(a => a.LocalId,
                a => new CampaignActor(a.LocalId, a.SourceLocalId, a.GraphicsId, a.X, a.Y, a.Elevation,
                    Direction.South, a.MovementType, a.ScriptId, a.HideFlag,
                    !state.Script.Flags.GetValueOrDefault(a.HideFlag), false, a.X, a.Y), StringComparer.Ordinal),
            ObjectSlots = null, LastTalked = null, SelectedObjectSlot = 0, MapLayout = null,
            MapEdits = null, EntryPrograms = [], Dialogue = null, Motions = [],
            Script = state.Script,
            IsSurfing = false, IsCycling = false, FlashLevel = 0
        };
        return CampaignSession.InitializeObjectSlotsForProfileUpgrade(state);
    }

    private CampaignSession Start(CampaignState state)
    {
        Assert.IsNull(validator.ValidatePlayerSave(state), "controlled field placement must be a valid save");
        return CampaignSession.RestoreCurrent(content, state, checkpoint.Generation);
    }

    private void DrainField(CampaignSession session)
    {
        for (int n = 0; n < 200; n++)
        {
            if (session.State.ForestTrainerBattle is not null || session.State.WildBattle is not null) return;
            if (session.State.Script.Completed && session.State.Script.Wait is null &&
                session.State.Dialogue is null && session.State.EntryPrograms.IsEmpty) return;
            Send(session, new("ack", Choice: session.State.Script.Wait?.Kind == StoryWaitKind.Choice ? 1 : null));
        }
        Assert.Fail("field presentation did not finish");
    }

    private void Step(CampaignSession session, Direction direction)
    {
        Send(session, new("move", Direction: direction));
        DrainField(session);
        Assert.IsNull(session.State.WildBattle, "route follows clear ground");
        Assert.IsNull(session.State.ForestTrainerBattle, "route avoids trainer sight");
    }

    [TestMethod]
    public void WaterPathWalksIntoGreenPathAndPatternBushAndReturnsWithoutRelocation()
    {
        var session = Start(Place(Water, 0, 11, Direction.West));
        Step(session, Direction.West);
        Assert.AreEqual(Green, session.State.World.Player.MapId.Value);
        Assert.AreEqual((71, 11), (session.State.World.Player.X, session.State.World.Player.Y));
        for (int x = 71; x > 63; x--) Step(session, Direction.West);
        for (int n = 0; n < 2 && session.State.World.Player.MapId.Value == Green; n++) Step(session, Direction.North);
        Assert.AreEqual(Bush, session.State.World.Player.MapId.Value);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault("FLAG_WORLD_MAP_SIX_ISLAND_PATTERN_BUSH"));
        Assert.AreEqual(new EscapeRopeDestination(new(Green), 64, 10), session.State.EscapeDestination);
        StrictRoundtrip(session);
        for (int n = 0; n < 3 && session.State.World.Player.MapId.Value == Bush; n++) Step(session, Direction.South);
        Assert.AreEqual(Green, session.State.World.Player.MapId.Value);
        if (session.State.World.Player.Y == 10) Step(session, Direction.South);
        for (int n = 0; n < 12 && session.State.World.Player.MapId.Value == Green; n++) Step(session, Direction.East);
        Assert.AreEqual(Water, session.State.World.Player.MapId.Value);
        Assert.AreEqual((0, 11), (session.State.World.Player.X, session.State.World.Player.Y));
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow(45, 45)]
    [DataRow(46, 45)]
    [DataRow(63, 64)]
    [DataRow(64, 64)]
    public void AllGreenPathEntrancesSelectOriginalEscapeSideAndReturn(int entranceX, int escapeX)
    {
        var session = Start(Place(Green, entranceX, 11));
        for (int n = 0; n < 2 && session.State.World.Player.MapId.Value == Green; n++) Step(session, Direction.North);
        Assert.AreEqual(Bush, session.State.World.Player.MapId.Value);
        Assert.AreEqual(new EscapeRopeDestination(new(Green), escapeX, 10), session.State.EscapeDestination);
        StrictRoundtrip(session);
        for (int n = 0; n < 3 && session.State.World.Player.MapId.Value == Bush; n++) Step(session, Direction.South);
        Assert.AreEqual(Green, session.State.World.Player.MapId.Value);
        Assert.IsTrue(entranceX < 50 ? session.State.World.Player.X is 45 or 46 : session.State.World.Player.X is 63 or 64);
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow(45, 45)]
    [DataRow(63, 64)]
    public void EscapeRopeAfterContinueUsesTheSideActuallyEntered(int entranceX, int escapeX)
    {
        var state = Place(Green, entranceX, 11);
        state = state with { Inventory = state.Inventory! with {
            BagItems = state.Inventory!.BagItems.RemoveAll(item => item.Item == EscapeRopeRules.Item)
                .Add(new(EscapeRopeRules.Item, 1)) } };
        var session = Start(state);
        for (int n = 0; n < 2 && session.State.World.Player.MapId.Value == Green; n++) Step(session, Direction.North);
        Assert.AreEqual(Bush, session.State.World.Player.MapId.Value);
        var bytes = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, content.ContentVersion);
        var decoded = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, content.ContentVersion,
            validate: session.ValidatePlayerSave);
        session = CampaignSession.RestoreCurrent(content, decoded.State, decoded.Generation);
        Send(session, new("bag_open"));
        Send(session, new("field_escape_rope", Text: EscapeRopeRules.Item.Value));
        Assert.IsNotNull(session.State.EscapeRope);
        Assert.AreEqual(Bush, session.State.World.Player.MapId.Value);
        Send(session, new("menu_ack")); DrainField(session);
        Assert.IsNull(session.State.EscapeRope);
        Assert.AreEqual(new TilePosition(new(Green), escapeX, 10, 3, Direction.South), session.State.World.Player);
        Assert.AreEqual(0, OpeningInventoryRules.Quantity(session.State.Inventory!.BagItems, EscapeRopeRules.Item));
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void GreenPathHiddenUltraBallIsOneShotAndSurvivesContinue()
    {
        const string flag = "FLAG_HIDDEN_ITEM_SIX_ISLAND_GREEN_PATH_ULTRA_BALL";
        var ball = new ItemId("pret:ITEM_ULTRA_BALL");
        var state = Place(Green, 12, 8, Direction.South);
        state = state with { Script = state.Script with { Flags = state.Script.Flags.SetItem(flag, false) } };
        var session = Start(state);
        int before = OpeningInventoryRules.Quantity(session.State.Inventory!.Balls!.Value, ball);
        Send(session, new("interact")); DrainField(session);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault(flag));
        Assert.AreEqual(before + 1, OpeningInventoryRules.Quantity(session.State.Inventory!.Balls!.Value, ball));
        var bytes = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, content.ContentVersion);
        var decoded = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, content.ContentVersion,
            validate: session.ValidatePlayerSave);
        session = CampaignSession.RestoreCurrent(content, decoded.State, decoded.Generation);
        var hash = CanonicalJson.Hash(session.State);
        var repeated = session.Execute("repeat-green-hidden-ball", session.Revision, new("interact"));
        Assert.AreEqual(CommandStatus.Rejected, repeated.Status);
        Assert.AreEqual("nothing_to_interact", repeated.Error);
        Assert.AreEqual(hash, CanonicalJson.Hash(session.State));
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow(40, "SixIsland_GreenPath_Text_RightRouteSign")]
    [DataRow(69, "SixIsland_GreenPath_Text_LeftRouteSign")]
    public void GreenPathSignsUseOriginalText(int x, string text)
    {
        var session = Start(Place(Green, x, 11));
        Send(session, new("interact"));
        Assert.AreEqual(text, session.State.Dialogue?.TextSymbol);
        DrainField(session);
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow(Green, "Jaclyn", "TRAINER_PSYCHIC_JACLYN")]
    [DataRow(Bush, "Bethany", "TRAINER_PKMN_BREEDER_BETHANY")]
    [DataRow(Bush, "Allison", "TRAINER_PKMN_BREEDER_ALLISON")]
    [DataRow(Bush, "Garret", "TRAINER_BUG_CATCHER_GARRET")]
    [DataRow(Bush, "Jonah", "TRAINER_BUG_CATCHER_JONAH")]
    [DataRow(Bush, "Vance", "TRAINER_BUG_CATCHER_VANCE")]
    [DataRow(Bush, "Nash", "TRAINER_YOUNGSTER_NASH")]
    [DataRow(Bush, "Cordell", "TRAINER_YOUNGSTER_CORDELL")]
    [DataRow(Bush, "Dalia", "TRAINER_LASS_DALIA")]
    [DataRow(Bush, "Joana", "TRAINER_LASS_JOANA")]
    [DataRow(Bush, "Riley", "TRAINER_CAMPER_RILEY")]
    [DataRow(Bush, "Marcy", "TRAINER_PICNICKER_MARCY")]
    [DataRow(Bush, "Layton", "TRAINER_RUIN_MANIAC_LAYTON")]
    public void EverySourceTrainerStartsThroughPublicInteraction(string mapName, string name, string trainer)
    {
        var map = content.Maps[new(mapName)];
        var actor = content.MapDefinition(map.Id).Actors.Single(a => a.ScriptId.EndsWith("_" + name, StringComparison.Ordinal));
        var adjacent = new[] { (actor.X, actor.Y + 1, Direction.North), (actor.X - 1, actor.Y, Direction.East),
            (actor.X + 1, actor.Y, Direction.West), (actor.X, actor.Y - 1, Direction.South) }
            .First(p => map.GetTile(p.Item1, p.Item2) is { Blocked: false });
        var session = Start(Place(mapName, adjacent.Item1, adjacent.Item2, adjacent.Item3));
        Send(session, new("interact")); DrainField(session);
        Assert.IsNotNull(session.State.ForestTrainerBattle, actor.ScriptId);
        Assert.AreEqual("pret:" + trainer, session.State.ForestTrainerBattle.Trainer.Value);
        Assert.AreEqual(actor.LocalId, session.State.LastTalked);
        Assert.IsNull(session.State.WildBattle);
    }

    private CampaignState GrassState(uint seed)
    {
        var map = content.Maps[new(Bush)];
        var actors = content.MapDefinition(map.Id).Actors;
        var cell = Enumerable.Range(1, map.Height - 2).SelectMany(y => Enumerable.Range(1, map.Width - 2).Select(x => (x, y)))
            .First(p => map.GetTile(p.x, p.y) is { Blocked: false } && map.SourceEncounterTypeAt(p.x, p.y) == 1 &&
                map.GetTile(p.x + 1, p.y) is { Blocked: false } && map.SourceEncounterTypeAt(p.x + 1, p.y) == 1 &&
                !actors.Any(a => Math.Abs(a.X - p.x) < 7 && Math.Abs(a.Y - p.y) < 7));
        var state = Place(Bush, cell.x, cell.y, Direction.South);
        state = state with {
            World = state.World with { Rng = state.World.Rng with { Seed = seed } },
            Inventory = state.Inventory! with { Balls = [new(new("pret:ITEM_MASTER_BALL"), 1)] },
            Exploration = state.Exploration! with { EarlyEncounter = new(new(0), (ushort)map.SourceBehaviorAt(cell.x, cell.y)!,
                ushort.MaxValue, 21, RulesVersion: EarlyLandEncounterRules.FiveIslandRulesVersion) }
        };
        return state;
    }

    private CampaignSession EnterGrassBattle(CampaignState state, bool walking)
    {
        var session = Start(state);
        Send(session, new(walking ? "move" : "turn", Direction: walking ? Direction.East : Direction.North));
        Drain(session);
        Assert.IsNotNull(session.State.WildBattle, "actual bush grass must start an ordinary wild battle");
        Assert.IsNull(session.State.SafariBattle);
        var enemy = session.State.WildBattle.State.Opponent.Pokemon;
        Assert.IsTrue(content.LandEncounters![new(Bush)].Slots.Any(slot => slot.Species == enemy.Species && slot.MinimumLevel == enemy.Level));
        return session;
    }

    private void CaptureAndContinue(CampaignSession session)
    {
        var enemy = session.State.WildBattle!.State.Opponent.Pokemon;
        long instance = session.State.NextPokemonId;
        Send(session, new("battle_ball", Text: "pret:ITEM_MASTER_BALL")); Drain(session);
        Assert.IsNull(session.State.WildBattle);
        var caught = session.State.Party.Concat(session.State.Storage!.Boxes.SelectMany(b => b.Slots).OfType<PartyPokemon>())
            .Single(p => p.InstanceId == instance);
        Assert.AreEqual(enemy.Species, caught.Pokemon.Species);
        Assert.AreEqual(enemy.Personality, caught.Pokemon.Personality);
        Assert.AreEqual(enemy.Ivs, caught.Pokemon.Ivs);
        Assert.AreEqual("MAPSEC_PATTERN_BUSH", caught.MetLocation);
        Assert.IsTrue(session.State.CaughtSpecies.Contains(enemy.SourceSpeciesId));
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BushGrassPublicTurnOrStepCapturesAndContinues(bool walking)
        => CaptureAndContinue(EnterGrassBattle(GrassState(3), walking));

    [TestMethod]
    [DataRow("SPINARAK", 167)]
    [DataRow("LEDYBA", 165)]
    [DataRow("HERACROSS", 214)]
    public void NewlyAdmittedBushSpeciesEnterPublicBattleCaptureAndContinue(string symbol, int sourceSpecies)
    {
        var species = new SpeciesId("pret:SPECIES_" + symbol);
        var basis = GrassState(0);
        // Only the initial global RNG seed varies. Every candidate is selected by the public field
        // command and the loaded weighted table; no pending encounter or battle state is injected.
        for (uint seed = 0; seed < 256; seed++)
        {
            var state = basis with { World = basis.World with { Rng = basis.World.Rng with { Seed = seed } } };
            var session = EnterGrassBattle(state, walking: false);
            var enemy = session.State.WildBattle!.State.Opponent.Pokemon;
            if (enemy.Species != species) continue;
            Assert.AreEqual(sourceSpecies, enemy.SourceSpeciesId);
            Console.WriteLine($"{symbol}: public encounter seed {seed}, level {enemy.Level}");
            CaptureAndContinue(session);
            return;
        }
        Assert.Fail($"No public {symbol} encounter found within the deterministic 256-seed fixture search.");
    }
}

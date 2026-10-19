using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Roaming;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>
/// A bounded public route after controlled real map entry, not a campaign replay.
/// Retains the maintained checkpoint's earned party, battles and RNG, including its
/// genuine depleted PP. The shared fixture's lifetime hatch statistic is test-only.
/// No damaged Pokemon, trainer victories, encounter seed or healing guard is invented.
/// </summary>
[TestClass, TestCategory("Game")]
public sealed class TowerPurificationRouteTests : WildBattleFixture
{
    private static readonly MapId Tower = new("map.pokemon-tower-5f");
    private const string Source = "data/maps/PokemonTower_5F/scripts.inc";
    private const string Entry = "PokemonTower_5F_EventScript_PurifiedZone";
    private const string Exit = "PokemonTower_5F_EventScript_ExitPurifiedZone";
    private const string Text = "PokemonTower_5F_Text_PurifiedZoneMonsFullyHealed";
    private const string Guard = "VAR_TEMP_1";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private static void Same(object? expected, object? actual, string message) =>
        Assert.IsTrue(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected),
            JsonSerializer.SerializeToElement(actual)), message);

    [TestMethod]
    public void SourcePurifiedCellsBindGuardedHealingAndExitResetWithoutEncounterTerrain()
    {
        var map = content.Maps[Tower];
        var definition = content.MapDefinition(Tower);
        Assert.AreEqual("WEATHER_FOG_HORIZONTAL", definition.Weather);
        var entries = definition.CoordTriggers.Where(t => t.ScriptId == Entry).ToArray();
        var exits = definition.CoordTriggers.Where(t => t.ScriptId == Exit).ToArray();
        Assert.AreEqual(17, definition.CoordTriggers.Length);
        Assert.AreSequenceEqual(new[] { (10, 8, 3), (11, 8, 3), (12, 8, 3), (10, 9, 3),
            (12, 9, 3), (10, 10, 3), (11, 10, 3), (12, 10, 0) },
            entries.Select(t => (t.X, t.Y, t.Elevation)));
        Assert.AreSequenceEqual(new[] { (9, 8, 3), (9, 9, 3), (9, 10, 3), (10, 11, 3),
            (11, 11, 3), (12, 11, 0), (13, 8, 0), (13, 9, 0), (13, 10, 0) },
            exits.Select(t => (t.X, t.Y, t.Elevation)));
        Assert.IsTrue(entries.All(t => t.Variable == Guard && t.Expected == 0));
        Assert.IsTrue(exits.All(t => t.Variable == Guard && t.Expected == 1));
        for (int y = 8; y <= 10; y++)
        for (int x = 10; x <= 12; x++)
        {
            Assert.AreEqual(0, map.SourceEncounterTypeAt(x, y));
            Assert.AreEqual(0, map.SourceBehaviorAt(x, y));
            Assert.IsFalse(map.GetTile(x, y)!.Blocked);
        }
        // The center has no entry trigger; the top-right cell holds the Channeler.
        Assert.IsFalse(entries.Any(t => t.X == 11 && t.Y == 9));
        Assert.IsTrue(definition.Actors.Any(a => a.X == 12 && a.Y == 8 &&
            a.ScriptId == "PokemonTower_5F_EventScript_Channeler"));
        var program = content.FindProgram(Entry)!;
        Assert.AreSequenceEqual(new[] { "lockall", "fadescreen", "special", "fadescreen",
            "msgbox", "setvar", "releaseall", "end" }, program.Instructions.Select(i => i.Opcode));
        Assert.IsTrue(program.Instructions.All(i => i.Source.Path == Source && i.Source.Symbol == Entry));
        Assert.AreSequenceEqual(new[] { "HealPlayerParty" }, program.Instructions[2].Args);
        Assert.AreSequenceEqual(new[] { Guard, "1" }, program.Instructions[5].Args);
        var reset = content.FindProgram(Exit)!;
        Assert.AreSequenceEqual(new[] { "lockall", "setvar", "releaseall", "end" },
            reset.Instructions.Select(i => i.Opcode));
        Assert.AreSequenceEqual(new[] { Guard, "0" }, reset.Instructions[1].Args);
    }

    [TestMethod]
    public void PublicEntryInteriorExitAndReentryHealEarnedPartyAndContinueOnlyAfterRelease()
    {
        var session = PlaceAtWestEntry();
        var origin = session.State;
        Move(session, Direction.East, 10, 10);
        AssertHealedAfterSourceWaits(session, origin);
        session = Continue(session);
        long healedSequence = session.State.Script.EffectSequence;

        // Both real moves land on guarded entry cells. The second uses source elevation 0.
        foreach (int x in new[] { 11, 12 })
        {
            Move(session, Direction.East, x, 10);
            Stable(session.State);
            Assert.AreEqual(1, session.State.Script.Variables[Guard]);
            Assert.AreEqual(healedSequence, session.State.Script.EffectSequence,
                "Walking inside the guarded zone cannot emit another fade, heal or dialogue.");
            Assert.AreEqual(0, session.FieldEncounterTypeAt(x, 10));
        }
        session = Continue(session);
        Move(session, Direction.South, 12, 11);
        Stable(session.State);
        Assert.AreEqual(0, session.State.Script.Variables[Guard]);
        Assert.AreEqual(healedSequence + 2, session.State.Script.EffectSequence,
            "Exit emits only lockall/releaseall; setvar resets the guard synchronously.");
        Assert.AreEqual(1, session.FieldEncounterTypeAt(12, 11),
            "This is real cave floor; its coordinate reset preempts the encounter check.");
        session = Continue(session);
        var beforeReentry = session.State;
        Move(session, Direction.North, 12, 10);
        AssertHealedAfterSourceWaits(session, beforeReentry);
        session = Continue(session);

        Assert.AreEqual(origin.World.StepCount + 5, session.State.World.StepCount);
        Assert.AreEqual(origin.World.Rng, session.State.World.Rng);
        Same(origin.Exploration!.EarlyEncounter!.RateRng, session.State.Exploration!.EarlyEncounter!.RateRng,
            "Neither global nor encounter-rate RNG is consumed by this five-step route.");
        Assert.AreEqual(origin.Exploration.EarlyEncounter.StepsSinceLastEncounter,
            session.State.Exploration.EarlyEncounter.StepsSinceLastEncounter);
        AssertHealed(origin, session.State);
        AssertEarnedState(origin, session.State);
    }

    private CampaignSession PlaceAtWestEntry()
    {
        var basis = checkpoint.State;
        var session = CampaignSession.CreateCurrent(content);
        var position = new TilePosition(Tower, 9, 10, 3, Direction.East);
        var state = basis with
        {
            World = basis.World with { Player = position },
            Script = StoryState.Empty with { Flags = basis.Script.Flags, Variables = basis.Script.Variables,
                EffectSequence = basis.Script.EffectSequence },
            Dialogue = null, ItemMenu = null, MonPicture = null, Motions = [],
            PresentationTicks = 0, PresentationEffects = [], EntryPrograms = [], ArrivalFacing = null,
            IsSurfing = false, IsCycling = false, BicycleSlope = false, MapEdits = null,
            MapLayout = null, FieldTransition = null, FieldWeather = null,
            PlayerVisible = true, ScreenIsBlack = false, ScreenIsWhite = false,
            Music = content.MapDefinition(Tower).Music, QuestLog = null
        };
        // EnterMap owns actors and temporary-variable reset. Only positioning is controlled.
        state = (CampaignState)typeof(CampaignSession).GetMethod("EnterMap", Private)!
            .Invoke(session, [state, null, RoamerTransition.Warp])!;
        state = (CampaignState)typeof(CampaignSession).GetMethod("Pump", Private)!.Invoke(session, [state])!;
        typeof(CampaignSession).GetField("_commands", Private)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion,
                content.ContentVersion, checkpoint.Generation));
        Stable(state);
        Assert.AreEqual(position, state.World.Player);
        Assert.AreEqual(basis.World.StepCount, state.World.StepCount);
        Assert.AreEqual(basis.World.Rng, state.World.Rng);
        Assert.AreEqual(0, state.Script.Variables.GetValueOrDefault(Guard));
        Same(basis.Party, state.Party, "Placement must retain the actual injured or healthy party unchanged.");
        AssertEarnedState(basis, state);
        return Continue(session);
    }

    private void Move(CampaignSession session, Direction direction, int x, int y)
    {
        var before = session.State;
        Send(session, new("move", direction));
        Assert.AreEqual(new TilePosition(Tower, x, y, 3, direction), session.State.World.Player);
        Assert.AreEqual(before.World.StepCount + 1, session.State.World.StepCount);
        Assert.AreEqual(before.World.Rng, session.State.World.Rng);
        Assert.IsNull(session.State.WildBattle);
        Assert.IsNull(session.State.ForestTrainerBattle);
        Assert.IsNull(session.State.DoubleBattle);
        Assert.IsNull(session.State.FieldTransition);
    }

    private void AssertHealedAfterSourceWaits(CampaignSession session, CampaignState before)
    {
        AssertFade(session, fromWhite: false);
        Same(before.Party, session.State.Party, "Healing cannot run before the TO_WHITE presentation acknowledgement.");
        var white = session.State;
        Send(session, new("ack"));
        AssertFade(session, fromWhite: true);
        AssertHealed(before, session.State);
        Assert.AreSequenceEqual(new[] { "special", "fadescreen" },
            session.State.PresentationEffects.Select(e => e.Opcode));
        Assert.AreSequenceEqual(new[] { "HealPlayerParty" }, session.State.PresentationEffects[0].Args);
        Assert.AreEqual(white.World, session.State.World);
        Send(session, new("ack"));
        Assert.AreEqual(StoryWaitKind.Dialog, session.State.Script.Wait?.Kind);
        Assert.AreEqual(Text, session.State.Dialogue?.TextSymbol);
        Assert.AreEqual(0, session.State.PresentationTicks);
        Assert.IsFalse(session.State.ScreenIsWhite);
        AssertModalSaveRefusal(session);
        int pages = session.State.Dialogue!.Pages.Length;
        Assert.AreEqual(2, pages);
        for (int page = 0; page < pages; page++)
        {
            Assert.AreEqual(page, session.State.Dialogue!.Page);
            Assert.AreEqual(0, session.State.Script.Variables.GetValueOrDefault(Guard),
                "The source commits its guard only after the last dialogue page.");
            Send(session, new("ack"));
        }
        Stable(session.State);
        Assert.AreEqual(1, session.State.Script.Variables[Guard]);
        Assert.AreEqual(white.World, session.State.World);
        AssertHealed(before, session.State);
        AssertEarnedState(before, session.State);
    }

    private void AssertFade(CampaignSession session, bool fromWhite)
    {
        var state = session.State;
        Assert.AreEqual(StoryWaitKind.Presentation, state.Script.Wait?.Kind);
        Assert.IsNull(state.Dialogue);
        Assert.AreEqual(!fromWhite, state.ScreenIsWhite);
        Assert.IsFalse(state.ScreenIsBlack);
        // Independent pinned-C script-resume vectors, relative to command frame:
        // TO_WHITE resumes at 23; fog FROM_WHITE resumes at 2 while weather keeps fading.
        Assert.AreEqual(fromWhite ? 2 : 23, state.PresentationTicks);
        var effect = state.PresentationEffects[^1];
        Assert.AreEqual("fadescreen", effect.Opcode);
        Assert.AreEqual(Tower, effect.MapId);
        Assert.AreSequenceEqual(new[] { fromWhite ? "FADE_FROM_WHITE" : "FADE_TO_WHITE" }, effect.Args);
        var frame = state.Script.Stack.Single();
        Assert.AreEqual(Entry, frame.ProgramId);
        var instruction = content.FindProgram(frame.ProgramId)!.Instructions[frame.InstructionIndex - 1];
        Assert.AreEqual(new StorySource(Source, Entry, fromWhite ? 12 : 10), instruction.Source);
        Assert.AreEqual(StoryOp.ScreenFade, instruction.Op);
        Assert.AreSequenceEqual(instruction.Args, effect.Args);
        Assert.AreEqual("event:" + effect.Sequence, state.Script.Wait!.CorrelationId);
        Assert.AreEqual(0, state.Script.Variables.GetValueOrDefault(Guard));
        AssertModalSaveRefusal(session);
    }

    private void AssertModalSaveRefusal(CampaignSession session)
    {
        var state = session.State;
        long revision = session.Revision;
        Assert.IsFalse(state.Script.Completed);
        Assert.IsTrue(state.Script.Locked);
        Assert.AreEqual("save_event_active", session.ValidatePlayerSave(session.PrepareFieldSave()));
        var bytes = SaveCodec.Encode(state, revision, session.RulesVersion, content.ContentVersion);
        Assert.ThrowsExactly<InvalidDataException>(() => SaveCodec.Decode<CampaignState>(bytes,
            session.RulesVersion, content.ContentVersion, validate: session.ValidatePlayerSave));
        var blocked = session.Execute("purification-locked:" + commands++, revision, new("move", Direction.West));
        Assert.AreEqual(CommandStatus.Rejected, blocked.Status);
        Assert.AreEqual("event_active", blocked.Error);
        Assert.AreSame(state, session.State);
        Assert.AreEqual(revision, session.Revision);
    }

    private void AssertHealed(CampaignState before, CampaignState after)
    {
        // Independent source HealPlayerParty formula, including existing PP Ups.
        var expected = before.Party.Select(p => p with
        {
            Status = 0,
            Pokemon = p.Pokemon with { CurrentHp = p.Pokemon.Stats.MaxHp,
                Moves = p.Pokemon.Moves.Select(m => m with { CurrentPp =
                    catalog.Moves[m.Move].Pp + catalog.Moves[m.Move].Pp * 20 * m.PpUps / 100 }).ToImmutableArray() }
        }).ToImmutableArray();
        Same(expected, after.Party, "Only source HP/PP/status healing may change earned individual data.");
        Assert.AreEqual(before.LastHealingLocation, after.LastHealingLocation,
            "Purification does not set the Pokemon Center respawn location.");
    }

    private static void AssertEarnedState(CampaignState before, CampaignState after)
    {
        object Earned(CampaignState s) => new { s.Inventory, s.Storage, s.Money, s.Coins, s.NextPokemonId,
            s.NextBattleId, s.BattledTrainers, s.SettledBattles, s.BattleHistory, s.ForestTrainerHistory,
            s.DoubleBattleHistory, s.TrainerTowerHistory, s.SeenSpecies, s.CaughtSpecies, s.Roamer,
            s.Exploration!.Battles, s.Exploration.Captured, s.Exploration.NextWildBattleId };
        Same(Earned(before), Earned(after), "Placement and purification cannot fabricate or discard earned history.");
    }

    private static void Stable(CampaignState state)
    {
        Assert.IsTrue(state.Script.Completed);
        Assert.IsFalse(state.Script.Locked);
        Assert.IsTrue(state.Script.Stack.IsEmpty);
        Assert.IsNull(state.Script.Wait);
        Assert.IsNull(state.Dialogue);
        Assert.IsNull(state.WildBattle);
        Assert.IsNull(state.FieldTransition);
        Assert.IsTrue(state.EntryPrograms.IsEmpty);
        Assert.IsTrue(state.Motions.IsEmpty);
        Assert.IsFalse(state.ScreenIsWhite);
        Assert.AreEqual(0, state.PresentationTicks);
    }

    private CampaignSession Continue(CampaignSession session)
    {
        var state = session.PrepareFieldSave();
        Assert.IsNull(session.ValidatePlayerSave(state));
        var decoded = SaveCodec.Decode<CampaignState>(SaveCodec.Encode(state, session.Revision,
            session.RulesVersion, content.ContentVersion), session.RulesVersion, content.ContentVersion,
            validate: session.ValidatePlayerSave);
        Assert.AreEqual(session.Revision, decoded.Generation);
        var restored = CampaignSession.RestoreCurrent(content, decoded.State, decoded.Generation);
        Same(state, restored.State, "Strict Continue retains the complete settled route state, including its temporary guard.");
        return restored;
    }
}

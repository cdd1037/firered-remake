using System.Collections.Immutable;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class MtEmberSummitFlowTests : WildBattleFixture
{
    private const string Summit = "map.mt-ember-summit";
    private CampaignState Place(CampaignState state, string mapName, int x, int y, Direction facing = Direction.North)
    {
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
            IsSurfing = false, IsCycling = false, FlashLevel = 0
        };
        return CampaignSession.InitializeObjectSlotsForProfileUpgrade(state);
    }

    private CampaignSession StartMoltres(bool teleport = false, int rngAdvance = 0)
    {
        var state = Place(FieldFixture(), Summit, 9, 7);
        state = state with { Inventory = state.Inventory! with { Balls = [new(new ItemId("pret:ITEM_MASTER_BALL"), 1)] } };
        if (teleport)
        {
            var deposit = PokemonStorageRules.Deposit(content.PokemonContent, state.Party, state.Storage!, 0, 0);
            Assert.AreEqual(PokemonStorageOutcome.Deposited, deposit.Outcome);
            var withdraw = PokemonStorageRules.Withdraw(content.PokemonContent, deposit.Party, deposit.Storage, 0, 2);
            Assert.AreEqual(PokemonStorageOutcome.Withdrawn, withdraw.Outcome);
            var abra = withdraw.Party[^1];
            Assert.AreEqual(63, abra.Pokemon.SourceSpeciesId);
            state = state with { Party = withdraw.Party.RemoveAt(withdraw.Party.Length - 1).Insert(0, abra),
                Storage = withdraw.Storage };
        }
        for (int n = 0; n < rngAdvance; n++)
            state = state with { World = state.World with { Rng = Gen3Rng.Next(state.World.Rng, "fixture.next-seed").State } };
        Assert.IsNull(validator.ValidatePlayerSave(state));
        var session = CampaignSession.RestoreCurrent(content, state, checkpoint.Generation);
        Send(session, new("interact"));
        for (int n = 0; n < 120 && session.State.WildBattle is null; n++)
            Send(session, session.State.Script.Wait?.Kind == StoryWaitKind.ButtonPress ? new("button_press", Text: "A") : new("ack"));
        Assert.IsNotNull(session.State.WildBattle);
        Drain(session);
        var battle = session.State.WildBattle!;
        Assert.AreEqual(146, battle.State.Opponent.Pokemon.SourceSpeciesId);
        Assert.AreEqual(50, battle.State.Opponent.Pokemon.Level);
        Assert.AreEqual(SourceTableEncounterKind.Scripted, battle.SourceTableReceipt!.Origin.Kind);
        Assert.IsFalse(battle.RandomTrace.Any(t => t.Purpose == "wild-battle.held-item"), "legendary entry skips held-item RNG");
        return session;
    }

    private void DrainField(CampaignSession session)
    {
        for (int n = 0; n < 150 && (!session.State.Script.Completed || session.State.Dialogue is not null || !session.State.EntryPrograms.IsEmpty); n++)
            Send(session, new("ack"));
        Assert.IsTrue(session.State.Script.Completed);
    }

    [TestMethod]
    public void PublicMoltresCapturePreservesIdentityFlagsCollectionAndContinue()
    {
        var session = StartMoltres();
        var initial = session.State.WildBattle!.SourceTableReceipt!.InitialPokemon;
        Send(session, new("battle_ball", Text: "pret:ITEM_MASTER_BALL")); Drain(session); DrainField(session);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault("FLAG_FOUGHT_MOLTRES"));
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault("FLAG_HIDE_MOLTRES"));
        Assert.IsFalse(session.State.Actors.Values.Single(a => a.SourceLocalId == 1).Active);
        Assert.IsTrue(session.State.CaughtSpecies.Contains(146));
        var capture = session.State.Exploration!.Captured!.Value.Single(c => c.SourceSpeciesId == 146);
        Assert.AreEqual(initial.Personality, capture.Personality);
        Assert.AreEqual(initial.Ivs, capture.Ivs);
        Assert.AreEqual(SourceTableEncounterKind.Scripted, capture.SourceTable!.Kind);
        StrictRoundtrip(session);
    }

    [TestMethod]
    public void PublicRunHidesOnlyUntilSourceTransitionAndReappearsAfterRetry()
    {
        var session = StartMoltres();
        for (int n = 0; n < 12 && session.State.WildBattle is not null; n++)
        { Send(session, new("battle_menu", Text: "run")); Drain(session); }
        Assert.IsNull(session.State.WildBattle); DrainField(session);
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault("FLAG_FOUGHT_MOLTRES"));
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault("FLAG_HIDE_MOLTRES"));
        Assert.AreEqual(WildBattleOutcome.Escaped, session.State.Exploration!.Battles[^1].Outcome);
        StrictRoundtrip(session);
        var continued = ReenterSummit(session);
        Assert.IsFalse(continued.State.Script.Flags.GetValueOrDefault("FLAG_HIDE_MOLTRES"));
        // The summit entry is outside Moltres's source object-spawn window.
        continued = CampaignSession.RestoreCurrent(content, Place(continued.State, Summit, 9, 7), continued.Revision);
        Assert.IsTrue(continued.State.Actors.Values.Single(a => a.SourceLocalId == 1).Active);
        Send(continued, new("interact"));
        for (int n = 0; n < 120 && continued.State.WildBattle is null; n++)
            Send(continued, continued.State.Script.Wait?.Kind == StoryWaitKind.ButtonPress ? new("button_press", Text: "A") : new("ack"));
        Drain(continued);
        Assert.IsNotNull(continued.State.WildBattle);
        Send(continued, new("battle_ball", Text: "pret:ITEM_MASTER_BALL")); Drain(continued); DrainField(continued);
        Assert.IsTrue(continued.State.Script.Flags.GetValueOrDefault("FLAG_FOUGHT_MOLTRES"));
        StrictRoundtrip(continued);
    }
    [TestMethod]
    public void PublicKnockoutIsTerminalAndCannotReappearAfterContinue()
    {
        var session = StartMoltres();
        for (int n = 0; n < 20 && session.State.WildBattle is not null; n++)
        { Send(session, new("battle_move", MoveSlot: 0)); Drain(session); }
        Assert.IsNull(session.State.WildBattle); DrainField(session);
        Assert.AreEqual(WildBattleOutcome.Won, session.State.Exploration!.Battles[^1].Outcome);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault("FLAG_FOUGHT_MOLTRES"));
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault("FLAG_HIDE_MOLTRES"));
        StrictRoundtrip(session);
        var continued = ReenterSummit(session);
        Assert.IsFalse(continued.State.Actors.Values.Single(a => a.SourceLocalId == 1).Active);
        StrictRoundtrip(continued);
    }

    [TestMethod]
    public void PublicTeleportUsesSourceRetryBranchRatherThanTerminalFoughtFlag()
    {
        CampaignSession? session = null;
        // Use the already-owned level-9 Abra. Seek a deterministic source roll where
        // Moltres uses Agility/Endure rather than KO'ing Abra before Teleport can act.
        for (int seed = 0; seed < 32; seed++)
        {
            var attempt = StartMoltres(teleport: true, rngAdvance: seed);
            Send(attempt, new("battle_move", MoveSlot: 0)); Drain(attempt);
            if (attempt.State.WildBattle is not null) continue;
            session = attempt; break;
        }
        Assert.IsNotNull(session, "at least one real public Teleport must complete");
        DrainField(session);
        Assert.IsNull(session.State.WildBattle);
        Assert.AreEqual(WildBattleOutcome.Escaped, session.State.Exploration!.Battles[^1].Outcome);
        Assert.AreEqual(5, session.State.Script.Variables["VAR_RESULT"], "source B_OUTCOME_PLAYER_TELEPORTED");
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault("FLAG_FOUGHT_MOLTRES"));
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault("FLAG_HIDE_MOLTRES"));
        StrictRoundtrip(session);
    }

    private CampaignSession ReenterSummit(CampaignSession previous)
    {
        // Controlled relocation to the original exit, preserving the complete settled receipt/flags.
        // Both outgoing and incoming transitions are then public movement, running source OnTransition.
        var state = Place(previous.State, Summit, 9, 14, Direction.South);
        var session = CampaignSession.RestoreCurrent(content, state, previous.Revision);
        for (int n = 0; n < 3 && session.State.World.Player.MapId.Value == Summit; n++)
        { Send(session, new("move", Direction: Direction.South)); DrainField(session); }
        Assert.AreEqual("map.mt-ember-exterior", session.State.World.Player.MapId.Value);
        for (int n = 0; n < 3 && session.State.World.Player.MapId.Value != Summit; n++)
        { Send(session, new("move", Direction: Direction.North)); DrainField(session); }
        Assert.AreEqual(Summit, session.State.World.Player.MapId.Value);
        return session;
    }

}

using System.Collections.Immutable;
using System.Reflection;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class BattleMoveOrderApplicationTests : WildBattleFixture
{
    private static CampaignCommand Swap(CampaignSession session, int first = 0, int second = 1)
    {
        var current = session.BattleMoveOrder!;
        return new("battle_move_swap") { TargetInstanceId = current.InstanceId, BattleMoveSwap = current.Swap(first, second) };
    }

    [TestMethod]
    public void WildSwapChangesOnlyMoveOrderAndRevisionThenNextTurnUsesTheChosenSlot()
    {
        var session = Controlled([Owned("PIDGEY", 20, 101, "GROWL", "TACKLE")], Owned("SNORLAX", 25, 201, "GROWL"));
        var before = session.State; var old = before.WildBattle!; var frame = old.Frame;
        var command = Swap(session);
        Send(session, command);
        var after = session.State; var battle = after.WildBattle!;
        CollectionAssert.AreEqual(old.State.Player.Pokemon.Moves.Reverse().ToArray(), battle.State.Player.Pokemon.Moves.ToArray());
        Assert.AreEqual(frame.Revision + 1, battle.Frame.Revision);
        Assert.AreEqual(frame.Turn, battle.Frame.Turn); Assert.AreEqual(frame.Rng, battle.Frame.Rng);
        Assert.AreEqual(frame.EventSequence, battle.Frame.EventSequence); Assert.AreEqual(before.World, after.World);
        Assert.AreEqual(old.Events, battle.Events); Assert.AreEqual(old.RandomTrace, battle.RandomTrace);
        Assert.AreEqual(old.EventIndex, battle.EventIndex); Assert.AreEqual(old.Phase, battle.Phase);
        Assert.AreEqual(battle.State.Player.Pokemon, battle.DisplayPlayer);
        Assert.AreEqual(battle.Frame.Party(BattleSide.Player).Members[0].Pokemon, after.Party[0].Pokemon);
        var stale = session.Execute("replayed-selection", session.Revision, command);
        Assert.AreEqual(CommandStatus.Rejected, stale.Status); Assert.AreSame(after, session.State);
        Send(session, new("battle_move", MoveSlot: 0));
        Assert.IsTrue(session.State.WildBattle!.Events.OfType<WildBattleMoveUsedEvent>().Any(e => e.Move.Value == "pret:MOVE_TACKLE"));
    }

    [TestMethod]
    public void BadIdentityRevisionBattleOrSlotIsAtomicAndInspectionDoesNotMutate()
    {
        var session = Controlled([Owned("PIDGEY", 20, 101, "GROWL", "TACKLE")], Owned("SNORLAX", 25, 201, "GROWL"));
        var state = session.State; var revision = session.Revision; var valid = Swap(session);
        for (int i = 0; i < 3; i++) _ = session.BattleMoveOrder;
        var request = valid.BattleMoveSwap!;
        CampaignCommand[] invalid = [ valid with { TargetInstanceId = 102 }, valid with { BattleMoveSwap = request with { BattleId = "other" } },
            valid with { BattleMoveSwap = request with { BattleRevision = request.BattleRevision - 1 } },
            valid with { BattleMoveSwap = request with { ActorSlot = 2 } }, valid with { BattleMoveSwap = request with { Actor = request.Actor!.Value with { EntrySerial = request.Actor.Value.EntrySerial + 1 } } },
            valid with { BattleMoveSwap = request with { SecondSlot = 2 } }, new("battle_move_swap") ];
        foreach (var command in invalid)
        {
            var result = session.Execute("bad-swap:" + commands++, revision, command);
            Assert.AreEqual(CommandStatus.Rejected, result.Status); Assert.AreSame(state, session.State); Assert.AreEqual(revision, session.Revision);
        }
    }

    [TestMethod]
    public void TrainerAdapterPreservesEventQueuesAndProjection()
    {
        var session = Controlled([Owned("PIDGEY", 20, 101, "GROWL", "TACKLE")], Owned("SNORLAX", 25, 201, "GROWL"));
        var wild = session.State.WildBattle!;
        var trainer = new CampaignForestTrainerBattle("trainer-test", new("pret:TRAINER_BUG_CATCHER_RICK"), "fixture", wild,
            CampaignForestTrainerStage.Combat, [20], [], 0, 0, [0]);
        SetState(session, session.State with { WildBattle = null, ForestTrainerBattle = trainer });
        Send(session, Swap(session));
        var after = session.State.ForestTrainerBattle!;
        Assert.AreEqual(trainer.TrainerEvents, after.TrainerEvents); Assert.AreEqual(trainer.Presentation.Events, after.Presentation.Events);
        Assert.AreEqual(trainer.Frame.Rng, after.Frame.Rng); Assert.AreEqual(trainer.Frame.Turn, after.Frame.Turn);
        Assert.AreEqual(after.Frame.Party(BattleSide.Player).Members[0].Pokemon, session.State.Party[0].Pokemon);
        Assert.AreEqual("pret:MOVE_TACKLE", after.Presentation.DisplayPlayer.Moves[0].Move.Value);
    }

    [TestMethod]
    public void DoublesReorderPreservesPartnersSelectedActionAndRejectsWrongActor()
    {
        var lead = Owned("PIDGEY", 20, 101, "GROWL", "TACKLE");
        var partner = Owned("RATTATA", 20, 102, "TAIL_WHIP", "TACKLE");
        var foe = Owned("SNORLAX", 25, 201, "GROWL");
        var session = Controlled([lead, partner], foe);
        WildBattleCombatant[] bodies = [new(lead.Pokemon), new(foe.Pokemon), new(partner.Pokemon), new(foe.Pokemon)];
        var frame = new SourceBattleFrame { Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version, BattleKind = WildBattleCalledBattleKind.Trainer, Rng = session.State.World.Rng,
            Parties = [new(BattleSide.Player, [new(lead.Pokemon), new(partner.Pokemon)]), new(BattleSide.Opponent, [new(foe.Pokemon), new(foe.Pokemon)])],
            Slots = Enumerable.Range(0,4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray() };
        Assert.IsNull(SourceFourActionTurns.ValidateCapability(catalog, frame));
        var selected = new SourceActionSelection(frame.Get(new(0)).Occupant!.Value, SourceActionKind.Move, 1, new(1));
        var doubles = new CampaignDoubleBattle("double-test", new("pret:TRAINER_TWINS_ELI_ANNE"), "fixture", frame, CampaignDoubleBattleStage.Combat,
            [], 0, [selected], [20,20], 0, 0, bodies.ToImmutableArray(), frame.Slots.Select(s => s.Member!.Value).ToImmutableArray(), [], [], 0);
        SetState(session, session.State with { WildBattle = null, DoubleBattle = doubles });
        Assert.AreEqual(2, session.BattleMoveOrder!.ActorSlot);
        Send(session, Swap(session));
        var after = session.State.DoubleBattle!;
        Assert.AreEqual(doubles.PlayerSelections, after.PlayerSelections); Assert.AreSame(frame.Slots[0], after.Frame.Slots[0]);
        Assert.AreEqual(frame.Rng, after.Frame.Rng); Assert.AreEqual(frame.Turn, after.Frame.Turn);
        Assert.AreEqual("pret:MOVE_TACKLE", after.DisplayedSlots[2].Pokemon.Moves[0].Move.Value);
        Assert.AreEqual(after.Frame.Party(BattleSide.Player).Members[1].Pokemon, session.State.Party[1].Pokemon);
    }

    [TestMethod]
    public void ClockCommitIsProvenTimeOnlyAndDoesNotInvalidateBattleIdentity()
    {
        var session = Controlled([Owned("PIDGEY", 20, 101, "GROWL", "TACKLE")], Owned("SNORLAX", 25, 201, "GROWL"));
        var before = session.State; var context = session.BattleMoveOrder;
        Send(session, new("play_time_tick", Choice: 60));
        Assert.AreEqual(CanonicalJson.Hash(before), CanonicalJson.Hash(session.State with { PlayTime = before.PlayTime }));
        Assert.AreEqual(context, session.BattleMoveOrder);
        Send(session, Swap(session));
    }

    private void SetState(CampaignSession session, CampaignState state) => typeof(CampaignSession)
        .GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, content.ContentVersion, session.Revision));
}

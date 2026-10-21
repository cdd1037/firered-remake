using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class FutureSightMoveAnimationDispatchTests
{
    private static MoveId Future => new("pret:MOVE_FUTURE_SIGHT");
    private static SourceMechanicsCatalog Mechanics => TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("future-sight", new("test"), "test", new(), CampaignDoubleBattleStage.Combat, events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("future-sight", null!, new(), events, index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []);

    private static WildBattleCombatant NaturalMon(string species, int level, string name)
    {
        var move = new MoveId("pret:MOVE_" + name);
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level));
        int pp = Mechanics.Catalog.Moves[move].Pp;
        return new(pokemon with { Moves = [new(move, pp, pp)] });
    }

    private static WildBattleCombatant[] Bodies(int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL") with { SpeedStage = 0 }).ToArray();
        bodies[actor] = NaturalMon("KADABRA", 30, "FUTURE_SIGHT") with { SpeedStage = 12, AccuracyStage = 12 };
        return bodies;
    }

    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies, bool single = false)
    {
        static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
            new(body.Pokemon, body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
        var frame = new SourceBattleFrame {
            Layout = single ? SourceBattleLayout.Singles : SourceBattleLayout.PresetDoubles,
            KoReplacementTiming = single ? KoReplacementTiming.SourceImmediate : KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version,
            BattleKind = single ? WildBattleCalledBattleKind.Wild : WildBattleCalledBattleKind.Trainer,
            SourceAbsentMask = (byte)(single ? 12 : 0), TurnStartAbsentMask = (byte)(single ? 12 : 0), Rng = new(31),
            Parties = [new(BattleSide.Player, single ? [Member(bodies[0])] : [Member(bodies[0]), Member(bodies[2])]),
                new(BattleSide.Opponent, single ? [Member(bodies[1])] : [Member(bodies[1]), Member(bodies[3])])],
            Slots = Enumerable.Range(0, 4).Select(i => single && i >= 2 ? new SourceBattleSlot(new(i), null, 0, null)
                : new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray()
        };
        var entry = SourceFourEntryAbilities.InitializeEntry(Mechanics.Catalog, frame, frame.Revision);
        Assert.IsTrue(entry.Succeeded, entry.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(entry.State));
        return entry.State with { Rng = new(0) };
    }

    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor = 0, int target = 1)
    {
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty); return result;
    }

    private static SourceFourActionResult Begin(SourceBattleFrame frame, int actor, int target)
    {
        var choices = frame.Slots.Take(frame.BattlerCount).Select(c => new SourceActionSelection(c.Occupant!.Value,
            SourceActionKind.Move, 0, new(c.Slot.Value == actor ? target : c.Slot.Value))).ToImmutableArray();
        return SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision, choices);
    }

    private static SourceFourActionResult Next(SourceFourActionResult result)
    {
        Assert.IsNotNull(result.Continuation, "Expected a retained public turn continuation.");
        var next = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
        Assert.IsTrue(next.Succeeded, next.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(next.State)); return next;
    }

    private static void AssertProjection(ImmutableArray<SourceBattleEvent> events, SourceBattleFrame state,
        ImmutableArray<RandomTrace> trace, bool expected, int actor, int target, bool single = false)
    {
        string before = JsonSerializer.Serialize(events), frame = JsonSerializer.Serialize(state), random = JsonSerializer.Serialize(trace);
        var used = events.OfType<SourceMoveUsedEvent>().Single(e => e.Move == Future);
        var attack = events.OfType<SourceAttackStringEvent>().Single(e => e.CurrentMove == Future);
        var pp = events.OfType<SourcePpCommandEvent>().Single(e => e.CurrentMove == Future);
        Assert.AreEqual(actor, used.Actor.Value); Assert.AreEqual(target, used.Target.Value);
        Assert.AreEqual(actor, attack.Actor.Value); Assert.AreEqual(Future, attack.ChosenMove);
        Assert.AreEqual(actor, pp.Actor.Value); Assert.AreEqual(actor, pp.PpOwner.Value);
        Assert.AreEqual(target, pp.Target.Value); Assert.AreEqual(Future, pp.ChosenMove);
        Assert.AreEqual(used.Slot, pp.PpSlot);
        Assert.AreEqual(used.PpBefore, pp.Before); Assert.AreEqual(used.PpAfter, pp.After); Assert.AreEqual(1, pp.CommandCost);
        int dispatched = 0;
        for (int i = 0; i < events.Length; i++)
        {
            CampaignMoveAnimationDecision Project(SourceBattleScene scene) => single
                ? AnimationDispatchFixture.AtAttackText(Single(events, i), true, scene)
                : AnimationDispatchFixture.AtAttackText(Double(events, i), scene);
            var decision = Project(SourceBattleScene.On);
            Assert.AreEqual(decision, Project(SourceBattleScene.On));
            Assert.IsNull(Project(SourceBattleScene.Off).Dispatch);
            if (single) Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, i), false, SourceBattleScene.On).Dispatch);
            var hit = single ? AnimationDispatchFixture.AtResult(Single(events, i), true, SourceBattleScene.On)
                : AnimationDispatchFixture.AtResult(Double(events, i), SourceBattleScene.On);
            Assert.IsNull(hit.Dispatch);
            if (expected && events[i] == used)
            {
                var scheduled = events.OfType<SourceCalledDelayedEvent>().Single(e => e.Move == Future && e.Reason == "scheduled");
                Assert.AreEqual(0, scheduled.Before); Assert.AreEqual(3, scheduled.After); Assert.AreEqual(target, scheduled.Target.Value);
                var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); dispatched++;
                Assert.AreEqual(248, cue.SourceMoveId); Assert.AreEqual(actor, cue.Actor.Value); Assert.AreEqual(target, cue.Target.Value);
                Assert.AreEqual(used.Sequence, cue.Sequence); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
                Assert.AreEqual(pp.Sequence, cue.PpSequence); Assert.AreEqual(scheduled.Sequence, cue.ResultSequence);
                Assert.IsTrue(cue.AcknowledgesCurrentReceipt); Assert.AreEqual((byte)0,cue.MoveTurn); Assert.IsFalse(cue.HitsSubstitute);
            }
            else Assert.IsNull(decision.Dispatch);
        }
        Assert.AreEqual(expected ? 1 : 0, dispatched);
        Assert.AreEqual(before, JsonSerializer.Serialize(events)); Assert.AreEqual(frame, JsonSerializer.Serialize(state));
        Assert.AreEqual(random, JsonSerializer.Serialize(trace));
    }

    [TestMethod]
    public void NaturalKadabraSchedulesSinglesSetupWithExactReceipt()
    {
        Assert.IsTrue(Mechanics.Catalog.Species[new("pret:SPECIES_KADABRA")].LevelUpMoves.Any(m => m.Move == Future && m.Level == 30));
        var result = Resolve(Frame(Bodies(), single: true));
        Assert.AreEqual(15, result.Events.OfType<SourcePpCommandEvent>().Single().Before);
        Assert.AreEqual(14, result.Events.OfType<SourcePpCommandEvent>().Single().After);
        AssertProjection(result.Events, result.State, result.Trace, true, 0, 1, single: true);
        AssertScheduledState(result, 0, 1);
    }

    private static void AssertScheduledState(SourceKernelResult result, int actor, int target)
    {
        var future = result.State.Slots[target].Position.FutureAttack;
        Assert.IsNotNull(future); Assert.AreEqual(Future, future.Move); Assert.AreEqual(actor, future.Attacker.Value);
        Assert.AreEqual(3, future.TurnsRemaining); Assert.IsTrue(future.Damage > 0);
        Assert.AreEqual(0, result.Events.OfType<SourceHpChangedEvent>().Count());
        Assert.AreEqual(0, result.Events.OfType<SourceSubstituteDamagedEvent>().Count());
        Assert.AreEqual(0, result.Events.OfType<SourceMoveMissedEvent>().Count());
    }

    [TestMethod]
    public void OrderedNaturalSetupWorksAtAllFourPositionsAndSelectedAllies()
    {
        Assert.AreEqual("MOVE_TARGET_SELECTED", Mechanics.Catalog.Moves[Future].Target);
        for (int actor = 0; actor < 4; actor++) foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        {
            var result = Begin(Frame(Bodies(actor)), actor, target);
            for (int step = 0; !result.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Move == Future) && step < 12; step++) result = Next(result);
            Assert.IsTrue(result.Succeeded, result.Fault);
            AssertProjection(result.Events, result.State, result.Trace, true, actor, target);
            Assert.AreEqual(actor, result.State.Slots[target].Position.FutureAttack!.Attacker.Value);
        }
    }

    [TestMethod]
    public void ExistingTimerFromActualSetupRejectsSecondSetupAfterSpendingPp()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1;
            var first = Resolve(Frame(Bodies(actor)), actor, target);
            var second = Resolve(first.State, actor, target);
            Assert.AreEqual(first.State.Slots[target].Position.FutureAttack, second.State.Slots[target].Position.FutureAttack);
            Assert.AreEqual("future-slot-occupied", second.Events.OfType<SourceMoveFailedEvent>().Single().Reason);
            Assert.AreEqual(0, second.Events.OfType<SourceCalledDelayedEvent>().Count());
            Assert.AreEqual(14, second.Events.OfType<SourcePpCommandEvent>().Single().Before);
            Assert.AreEqual(13, second.Events.OfType<SourcePpCommandEvent>().Single().After);
            AssertProjection(second.Events, second.State, second.Trace, false, actor, target);
        }
    }

    [TestMethod]
    public void ProtectSubstituteAndMinimumAccuracyDoNotGateSetup()
    {
        for (int actor = 0; actor < 4; actor++) foreach (bool protect in new[] { false, true })
        {
            int target = actor ^ 1; var bodies = Bodies(actor);
            bodies[actor] = bodies[actor] with { AccuracyStage = 0 };
            bodies[target] = bodies[target] with { Protected = protect, EvasionStage = 12, CalledVolatile = new() { SubstituteHp = 1 } };
            var result = Resolve(Frame(bodies), actor, target);
            AssertScheduledState(result, actor, target);
            Assert.AreEqual(1, result.State.Slots[target].Body!.CalledVolatile!.SubstituteHp);
            AssertProjection(result.Events, result.State, result.Trace, true, actor, target);
        }
    }

    [TestMethod]
    public void SetupNeedsTheExactCommittedPositionTimerReceipt()
    {
        var result=Resolve(Frame(Bodies()));var events=result.Events;
        var scheduled=events.OfType<SourceCalledDelayedEvent>().Single();
        int at=events.IndexOf(scheduled),used=events.IndexOf(events.OfType<SourceMoveUsedEvent>().Single());
        Assert.IsNotNull(AnimationDispatchFixture.AtAttackText(Double(events,used),SourceBattleScene.On).Dispatch);
        foreach(var invalid in new[]{scheduled with{Target=new(0)},scheduled with{Move=new("pret:MOVE_WISH")},
            scheduled with{Before=1},scheduled with{After=2},scheduled with{Reason="resolved"},scheduled with{SourcePartyIndex=0}})
        {
            var changed=events.SetItem(at,invalid);
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(changed,used),SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(changed,used),true,SourceBattleScene.On).Dispatch);
        }
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events.RemoveAt(at),used),SourceBattleScene.On).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events.Add(scheduled with{Sequence=events[^1].Sequence+1}),used),SourceBattleScene.On).Dispatch);
    }

    [TestMethod]
    public void ActualThreeTurnDelayedImpactNeverReplaysSetupAnimation()
    {
        var frame = Frame(Bodies()); int scheduled = 0, resolved = 0, impacts = 0, setupCues = 0;
        for (int turn = 0; turn < 3; turn++)
        {
            var result = Begin(frame, 0, 1);
            for (int step = 0; ; step++)
            {
                Assert.IsTrue(step < 32, "Three ordinary turns must drain within the bounded public lifecycle.");
                Assert.IsTrue(result.Succeeded, result.Fault);
                string before = JsonSerializer.Serialize(result);
                scheduled += result.Events.OfType<SourceCalledDelayedEvent>().Count(e => e.Move == Future && e.Reason == "scheduled");
                resolved += result.Events.OfType<SourceCalledDelayedEvent>().Count(e => e.Move == Future && e.Reason == "resolved" && e.Before == 1 && e.After == 0);
                impacts += result.Events.OfType<SourceHpChangedEvent>().Count(e => e.Move == Future && e.Damage > 0);
                for (int i = 0; i < result.Events.Length; i++)
                {
                    var battle = Double(result.Events, i);
                    var cue = AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On).Dispatch;
                    if (cue?.SourceMoveId == 248)
                    {
                        setupCues++; Assert.AreEqual(0, turn);
                        Assert.IsInstanceOfType<SourceMoveUsedEvent>(result.Events[i]);
                        Assert.IsTrue(result.Events.OfType<SourceCalledDelayedEvent>().Any(e => e.Reason == "scheduled" && e.Sequence == cue.ResultSequence));
                    }
                    Assert.AreNotEqual(248, AnimationDispatchFixture.AtResult(battle, SourceBattleScene.On).Dispatch?.SourceMoveId ?? 0);
                }
                Assert.AreEqual(before, JsonSerializer.Serialize(result));
                if (result.CompletedCleanTurn) { frame = result.State; break; }
                result = Next(result);
            }
        }
        Assert.AreEqual(1, scheduled); Assert.AreEqual(1, resolved); Assert.AreEqual(1, impacts); Assert.AreEqual(1, setupCues);
        Assert.IsNull(frame.Slots[1].Position.FutureAttack);
    }
}

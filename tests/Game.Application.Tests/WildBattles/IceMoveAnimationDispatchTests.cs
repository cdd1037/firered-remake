using static FireRed.Tests.Application.WildBattles.DirectHitAnimationFixture;
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

// Source contract: actual natural learners and public four-slot kernel receipts only.
// One attack-text root owns the first successful spread pass and THAT pass's PP receipt.
// Later damage/Speed receipts must not replay the root. Projection is observational.
// Secondary-stat wave admission is a separate shared integration gate; its absence is
// deliberately not asserted as correct here.
[TestClass, TestCategory("Game")]
public sealed class IceMoveAnimationDispatchTests
{
    private static readonly (string Name, int Id, string Species, int Level, int Pp)[] Moves =
        [("BLIZZARD", 59, "JYNX", 67, 5), ("ICY_WIND", 196, "SEEL", 100, 15)];

    private static SourceMechanicsCatalog Mechanics => TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static SourceBattleEvent[] Hits(ImmutableArray<SourceBattleEvent> events) =>
        events.Where(e => e is SourceHpChangedEvent or SourceSubstituteDamagedEvent).Where(hit => {
            var target = HitTarget(hit);
            var pass = events.OfType<SourcePpCommandEvent>().Last(p => p.Sequence < hit.Sequence);
            var next = events.OfType<SourcePpCommandEvent>().FirstOrDefault(p => p.Sequence > pass.Sequence);
            // Immunity may still write a zero-loss HP receipt. Test source outcome markers,
            // not HP delta: Endure at one HP remains an eligible animation result.
            return !events.Any(e => e.Sequence > pass.Sequence && (next is null || e.Sequence < next.Sequence) &&
                (e is SourceAbilityPreventedEvent a && a.Target == target && a.Move == pass.CurrentMove &&
                    a.Ability is "ABILITY_WONDER_GUARD" or "ABILITY_LEVITATE" ||
                 e is SourceEffectivenessEvent f && f.Target == target && f.Move == pass.CurrentMove &&
                    f.Effectiveness == WildBattleEffectiveness.NoEffect));
        }).ToArray();
    private static BattlerSlot HitTarget(SourceBattleEvent e) => e switch {
        SourceHpChangedEvent h => h.Target, SourceSubstituteDamagedEvent s => s.Target,
        _ => throw new InvalidOperationException("Expected an actual damage receipt.")
    };
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("ice-spread", new("test"), "test", new(), CampaignDoubleBattleStage.Combat, events, index, [], [], 0, 0, [], [], [], [], 0);
    private static void AssertProjection(SourceKernelResult result, int id, int actor, int initialTarget) =>
        AssertProjection(result.Events, result.State, result.Trace, id, actor, initialTarget);
    private static void AssertProjection(ImmutableArray<SourceBattleEvent> events, SourceBattleFrame state,
        ImmutableArray<RandomTrace> trace, int id, int actor, int initialTarget)
    {
        string before = JsonSerializer.Serialize(events), frame = JsonSerializer.Serialize(state), random = JsonSerializer.Serialize(trace);
        var move = Moves.Single(m => m.Id == id);
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var passes = events.OfType<SourcePpCommandEvent>().ToArray();
        Assert.AreEqual(Id(move.Name), used.Move);
        Assert.AreEqual(actor, used.Actor.Value); Assert.AreEqual(initialTarget, used.Target.Value);
        Assert.AreEqual(2, passes.Length, "Both living opposing slots must receive actual source PP-command visits.");
        CollectionAssert.AreEqual(new[] { initialTarget, initialTarget ^ 2 }, passes.Select(p => p.Target.Value).ToArray());
        Assert.AreEqual(move.Pp, passes[0].Before); Assert.AreEqual(move.Pp - 1, passes[0].After);
        // CommandCost reports the calculated cost even when NoAttackString suppresses spending.
        // The actual second-pass PP delta, rather than a synthetic zero cost, proves no extra spend.
        Assert.AreEqual(1, passes[0].CommandCost); Assert.AreEqual(1, passes[1].CommandCost);
        Assert.AreEqual(passes[0].After, passes[1].Before); Assert.AreEqual(passes[1].Before, passes[1].After);
        foreach (var pp in passes)
        {
            Assert.AreEqual(used.Actor, pp.Actor); Assert.AreEqual(used.Actor, pp.PpOwner);
            Assert.AreEqual(used.Move, pp.CurrentMove); Assert.AreEqual(attack.ChosenMove, pp.ChosenMove);
            Assert.AreEqual(used.Slot, pp.PpSlot); Assert.IsTrue(pp.Sequence > used.Sequence);
        }
        Assert.AreEqual(move.Pp - 1, state.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);
        var hits = Hits(events); int dispatched = 0;
        foreach (var hit in hits)
            Assert.IsTrue(passes.Single(p => p.Target == HitTarget(hit)).Sequence < hit.Sequence);
        for (int i = 0; i < events.Length; i++)
        {
            var battle = Double(events, i);
            var decision = AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On);
            Assert.AreEqual(decision, AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On));
            Assert.IsNull(AnimationDispatchFixture.AtResult(battle, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.Off).Dispatch);
            if (hits.Length > 0 && events[i] == used)
            {
                var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); dispatched++;
                var first = hits[0]; var target = HitTarget(first); var owner = passes.Single(p => p.Target == target);
                Assert.AreEqual(id, cue.SourceMoveId); Assert.AreEqual(actor, cue.Actor.Value); Assert.AreEqual(target, cue.Target);
                Assert.AreEqual(used.Sequence, cue.Sequence); Assert.AreEqual(first.Sequence, cue.ResultSequence);
                Assert.AreEqual(attack.Sequence, cue.AttackSequence); Assert.AreEqual(owner.Sequence, cue.PpSequence);
                Assert.IsTrue(cue.AcknowledgesCurrentReceipt); Assert.IsNull(cue.MoveTurn);
                Assert.AreEqual(first is SourceSubstituteDamagedEvent, cue.HitsSubstitute);
            }
            else Assert.IsNull(decision.Dispatch, $"Receipt {events[i].GetType().Name} must not create another Ice root.");
        }
        Assert.AreEqual(hits.Length > 0 ? 1 : 0, dispatched);
        Assert.AreEqual(before, JsonSerializer.Serialize(events)); Assert.AreEqual(frame, JsonSerializer.Serialize(state));
        Assert.AreEqual(random, JsonSerializer.Serialize(trace));
    }

    private static WildBattleCombatant[] Bodies(int id, int actor)
    {
        var move = Moves.Single(m => m.Id == id);
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL") with { SpeedStage = 0 }).ToArray();
        // Neutral Speed is important for the later Icy Wind reduction receipts.
        bodies[actor ^ 1] = bodies[actor ^ 1] with { SpeedStage = 6 };
        bodies[actor ^ 3] = bodies[actor ^ 3] with { SpeedStage = 6 };
        bodies[actor] = NaturalMon(move.Species, move.Level, move.Name) with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }

    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor = 0, int target = 1)
    {
        Assert.IsNull(SourceBattleIdentityRules.Validate(frame));
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty); return result;
    }

    private static (uint Seed, SourceKernelResult Result) Find(WildBattleCombatant[] bodies,
        int actor, int target, Func<SourceKernelResult, bool> predicate, string branch)
    {
        var entered = Frame(bodies, 0);
        // Bounded integration discovery only. Missing branches fail; no product-output golden is generated.
        for (uint seed = 0; seed < 1024; seed++)
        {
            var result = Resolve(entered with { Rng = new(seed) }, actor, target);
            if (predicate(result)) return (seed, result);
        }
        throw new InvalidOperationException($"No observed {branch} in 1024 action-start seeds.");
    }
    [TestMethod]
    public void NaturalIceMovesUsePublicOrderedTurnsFromAllFourActors()
    {
        foreach (var move in Moves)
        {
            Assert.AreEqual("MOVE_TARGET_BOTH", Mechanics.Catalog.Moves[Id(move.Name)].Target);
            Assert.IsTrue(Mechanics.Catalog.Species[new("pret:SPECIES_" + move.Species)].LevelUpMoves
                .Any(m => m.Move == Id(move.Name) && m.Level == (move.Id == 59 ? 67 : 17)));
            for (int actor = 0; actor < 4; actor++) foreach (int target in new[] { actor ^ 1, actor ^ 3 })
            {
                var result = Ordered(Frame(Bodies(move.Id, actor), 0), actor, target);
                Assert.AreEqual(2, Hits(result.Events).Length);
                AssertProjection(result.Events, result.State, result.Trace, move.Id, actor, target);
            }
        }
    }

    [TestMethod]
    public void ActualMixedAccuracyPassesChooseTheFirstSuccessfulReceipt()
    {
        foreach (var move in Moves) for (int actor = 0; actor < 4; actor++) foreach (bool firstMisses in new[] { false, true })
        {
            int target = actor ^ 1; var bodies = Bodies(move.Id, actor);
            bodies[actor] = bodies[actor] with { AccuracyStage = 0 };
            var result = Find(bodies, actor, target, r => Hits(r.Events).Length == 1 &&
                r.Events.OfType<SourceMoveMissedEvent>().Any(e => e.Target.Value == (firstMisses ? target : target ^ 2)) &&
                HitTarget(Hits(r.Events)[0]).Value == (firstMisses ? target ^ 2 : target), "Ice mixed hit/miss").Result;
            AssertProjection(result, move.Id, actor, target);
        }
    }

    [TestMethod]
    public void ActualWonderGuardPassesCannotOwnTheIceRoot()
    {
        var species = Mechanics.Catalog.Species[new("pret:SPECIES_SHEDINJA")];
        Assert.AreEqual("TYPE_BUG", species.Type1); Assert.AreEqual("TYPE_GHOST", species.Type2);
        Assert.AreEqual("ABILITY_WONDER_GUARD", species.Ability1);
        foreach (var move in Moves) for (int actor = 0; actor < 4; actor++) foreach (bool firstBlocked in new[] { false, true })
        {
            int target = actor ^ 1, blocked = firstBlocked ? target : target ^ 2; var bodies = Bodies(move.Id, actor);
            bodies[blocked] = NaturalMon("SHEDINJA", 100, "HARDEN");
            var result = Resolve(Frame(bodies, 0), actor, target);
            var guard = result.Events.OfType<SourceAbilityPreventedEvent>().Single(e => e.Ability == "ABILITY_WONDER_GUARD");
            Assert.AreEqual(blocked, guard.Target.Value); Assert.AreEqual(1, Hits(result.Events).Length);
            Assert.AreEqual(blocked ^ 2, HitTarget(Hits(result.Events)[0]).Value);
            Assert.AreEqual(1, result.State.Slots[blocked].Body!.Pokemon.CurrentHp);
            AssertProjection(result, move.Id, actor, target);
        }
    }

    [TestMethod]
    public void ActualBothWonderGuardTargetsDoNotCreateAnIceRoot()
    {
        foreach (var move in Moves) for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(move.Id, actor);
            bodies[target] = NaturalMon("SHEDINJA", 100, "HARDEN");
            bodies[target ^ 2] = NaturalMon("SHEDINJA", 100, "HARDEN");
            var result = Resolve(Frame(bodies, 0), actor, target);
            Assert.AreEqual(2, result.Events.OfType<SourceAbilityPreventedEvent>().Count(e => e.Ability == "ABILITY_WONDER_GUARD"));
            Assert.AreEqual(0, Hits(result.Events).Length);
            AssertProjection(result, move.Id, actor, target);
        }
    }

    [TestMethod]
    public void ActualZeroLossEndurePassOwnsTheRootEvenBesideAnImmuneTarget()
    {
        foreach (var move in Moves) for (int actor = 0; actor < 4; actor++) foreach (bool endureFirst in new[] { false, true })
        {
            int target = actor ^ 1, endured = endureFirst ? target : target ^ 2, immune = endured ^ 2;
            var bodies = Bodies(move.Id, actor);
            bodies[endured] = bodies[endured] with { Enduring = true, Pokemon = bodies[endured].Pokemon with { CurrentHp = 1 } };
            bodies[immune] = NaturalMon("SHEDINJA", 100, "HARDEN");
            var result = Resolve(Frame(bodies, 0), actor, target);
            var adjustment = result.Events.OfType<SourceDamageAdjustedEvent>().Single(a => a.Target.Value == endured);
            Assert.IsTrue(adjustment.Endured); Assert.IsTrue(adjustment.DamageBefore > 0); Assert.AreEqual(0, adjustment.DamageAfter);
            var successful = Hits(result.Events); Assert.AreEqual(1, successful.Length);
            var hit = (SourceHpChangedEvent)successful[0]; Assert.AreEqual(endured, hit.Target.Value);
            Assert.AreEqual(1, hit.HpBefore); Assert.AreEqual(1, hit.HpAfter);
            AssertProjection(result, move.Id, actor, target);
        }
    }

    [TestMethod]
    public void ActualSubstituteBreakAndKoKeepOneIceRootAcrossBothPasses()
    {
        foreach (var move in Moves) for (int actor = 0; actor < 4; actor++) foreach (bool substituteFirst in new[] { false, true })
        {
            int target = actor ^ 1, substitute = substituteFirst ? target : target ^ 2, ko = substitute ^ 2;
            var bodies = Bodies(move.Id, actor);
            bodies[substitute] = bodies[substitute] with { CalledVolatile = new() { SubstituteHp = 1 } };
            bodies[ko] = bodies[ko] with { Pokemon = bodies[ko].Pokemon with { CurrentHp = 1 } };
            var result = Resolve(Frame(bodies, 0), actor, target);
            var broken = result.Events.OfType<SourceSubstituteDamagedEvent>().Single();
            Assert.AreEqual(substitute, broken.Target.Value); Assert.AreEqual(0, broken.HpAfter);
            var fainted = result.Events.OfType<SourceHpChangedEvent>().Single();
            Assert.AreEqual(ko, fainted.Target.Value); Assert.AreEqual(0, fainted.HpAfter);
            Assert.IsTrue(result.Events.OfType<SourceFaintedEvent>().Any(e => e.Side.Value == ko));
            AssertProjection(result, move.Id, actor, target);
        }
    }

    [TestMethod]
    public void ActualAllMissOrAllProtectedPassesDoNotCreateAnIceRoot()
    {
        foreach (var move in Moves) for (int actor = 0; actor < 4; actor++) foreach (bool protect in new[] { false, true })
        {
            int target = actor ^ 1; var bodies = Bodies(move.Id, actor);
            if (protect)
            {
                bodies[target] = bodies[target] with { Protected = true };
                bodies[target ^ 2] = bodies[target ^ 2] with { Protected = true };
            }
            else bodies[actor] = bodies[actor] with { AccuracyStage = 0 };
            var result = protect ? Resolve(Frame(bodies, 0), actor, target) :
                Find(bodies, actor, target, r => r.Events.OfType<SourceMoveMissedEvent>().Count() == 2, "Ice double miss").Result;
            Assert.AreEqual(0, Hits(result.Events).Length);
            AssertProjection(result, move.Id, actor, target);
        }
    }

    [TestMethod]
    public void ActualIcyWindSpeedReceiptsDoNotReplayTheAttackRoot()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var result = Resolve(Frame(Bodies(196, actor), 0), actor, target);
            var speed = result.Events.OfType<SourceStatChangedEvent>().Where(e => e.Stat == WildBattleStat.Speed).ToArray();
            Assert.AreEqual(2, speed.Length, "Both surviving natural defenders must actually lose Speed.");
            foreach (var reduction in speed)
            {
                Assert.AreEqual(actor, reduction.Actor.Value); Assert.AreEqual(6, reduction.Before); Assert.AreEqual(5, reduction.After);
                Assert.AreEqual(5, result.State.Slots[reduction.Target.Value].Body!.SpeedStage);
                Assert.IsTrue(Hits(result.Events).Any(e => HitTarget(e) == reduction.Target && e.Sequence < reduction.Sequence));
                int index = result.Events.IndexOf(reduction);
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(result.Events, index), SourceBattleScene.On).Dispatch);
            }
            AssertProjection(result, 196, actor, target);
        }
    }
}

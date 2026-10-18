using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Called = FireRed.Tests.Application.WildBattles.CalledLeafAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

// Natural source learners and actual public-kernel receipts. Controlled stages,
// HP and substitute state are integration fixtures, not campaign-route evidence.
[TestClass, TestCategory("Game")]
public sealed class SludgeSparkAnimationKernelTests
{
    private static (string Name, string Species, string Effect, string Type, int Power) Spec(int id) => id switch {
        124 => ("SLUDGE", "KOFFING", "EFFECT_POISON_HIT", "TYPE_POISON", 65),
        188 => ("SLUDGE_BOMB", "GRIMER", "EFFECT_POISON_HIT", "TYPE_POISON", 90),
        209 => ("SPARK", "ELECTRIKE", "EFFECT_PARALYZE_HIT", "TYPE_ELECTRIC", 65),
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
    private static MoveId Move(int id) => Kernel.Id(Spec(id).Name);
    private static uint Status(int id) => id == 209 ? WildBattleStatus.Paralysis : WildBattleStatus.Poison;
    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        var spec = Spec(id);
        bodies[actor] = Kernel.Mon(spec.Species, spec.Name) with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }
    private static bool Qualifying(SourceFourActionResult result) =>
        result.Trace.Single(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal)).Raw % 100 <= 30;
    private static void Check(SourceFourActionResult result, int id, bool single, SourceBattleEvent? owner) =>
        Check(result.Events, result, id, single, owner);
    private static void Check(ImmutableArray<SourceBattleEvent> events, object result, int id, bool single, SourceBattleEvent? owner)
    {
        string before = JsonSerializer.Serialize(result);
        Assert.IsTrue(AnimationDispatchFixture.Supports(id, Move(id).Value), "Require the canonical generated ID/label pair.");
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        Assert.AreEqual(Move(id), used.Move); Assert.AreEqual(used.Move, attack.CurrentMove); Assert.AreEqual(used.Move, attack.ChosenMove);
        Assert.AreEqual(used.Actor, attack.Actor); Assert.AreEqual(used.Actor, pp.Actor); Assert.AreEqual(used.Actor, pp.PpOwner);
        Assert.AreEqual(used.Target, pp.Target); Assert.AreEqual(used.Move, pp.CurrentMove); Assert.AreEqual(used.Move, pp.ChosenMove);
        Assert.AreEqual(used.Slot, pp.PpSlot); Assert.AreEqual(used.PpBefore, pp.Before); Assert.AreEqual(used.PpAfter, pp.After);
        Assert.IsTrue(pp.CommandCost > 0); Assert.AreEqual(Math.Max(0, pp.Before - pp.CommandCost), pp.After);
        Assert.IsTrue(attack.Sequence < used.Sequence && used.Sequence < pp.Sequence);
        if (owner is not null)
        {
            Assert.IsTrue(events.Contains(owner)); Assert.IsTrue(owner.Sequence > pp.Sequence);
            Assert.AreEqual(used.Turn, owner.Turn);
        }
        int count = 0;
        for (int i = 0; i < events.Length; i++)
        {
            var decision = Kernel.Project(events, i, single);
            Assert.AreEqual(decision, Kernel.Project(events, i, single));
            Assert.IsNull(Kernel.Project(events, i, single, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(Called.Project(events, i, single, result: true).Dispatch);
            Assert.IsNull(single ? AnimationDispatchFixture.StatAtResult(Envelope.Single(events, i), true)
                : AnimationDispatchFixture.StatAtResult(Envelope.Double(events, i)));
            if (single)
            {
                var exported = Envelope.Single(events, i) with { SourceEvents = [] };
                Assert.AreEqual(decision, AnimationDispatchFixture.AtAttackText(exported, true, SourceBattleScene.On));
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(exported, false, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtResult(exported, true, SourceBattleScene.On).Dispatch);
            }
            if (owner is null || events[i] != used) { Assert.IsNull(decision.Dispatch); continue; }
            var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); count++;
            Assert.AreEqual(id, cue.SourceMoveId); Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(used.Target, cue.Target);
            Assert.AreEqual(used.Sequence, cue.Sequence); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
            Assert.AreEqual(pp.Sequence, cue.PpSequence); Assert.AreEqual(pp.Sequence, cue.SnapshotSequence);
            Assert.AreEqual(owner.Sequence, cue.ResultSequence); Assert.AreEqual(owner is SourceSubstituteDamagedEvent, cue.HitsSubstitute);
            Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText, cue.Boundary); Assert.IsTrue(cue.AcknowledgesCurrentReceipt);
            Assert.IsNull(cue.MoveTurn); Assert.IsNull(cue.MoveDamage); Assert.IsNull(cue.MovePower);
        }
        Assert.AreEqual(owner is null ? 0 : 1, count);
        Assert.AreEqual(before, JsonSerializer.Serialize(result), "Projection preserves committed state, RNG, trace and receipts.");
    }

    [TestMethod, DataRow(124), DataRow(188), DataRow(209)]
    public void NaturalSelectedHitsOwnOneRootAtEveryActorAndSelectedAlly(int id)
    {
        var spec = Spec(id); var definition = Kernel.Mechanics.Catalog.Moves[Move(id)];
        Assert.AreEqual(id, definition.SourceMoveId); Assert.AreEqual(spec.Effect, definition.Effect);
        Assert.AreEqual(spec.Type, definition.Type); Assert.AreEqual(spec.Power, definition.Power);
        Assert.AreEqual("MOVE_TARGET_SELECTED", definition.Target); Assert.AreEqual(100, definition.Accuracy);
        Assert.AreEqual(30, definition.SecondaryEffectChance);
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in single ? new[] { actor ^ 1 } : new[] { actor ^ 1, actor ^ 2 })
        {
            var result = Kernel.Ordered(Kernel.Frame(Bodies(id, actor), single), actor, target);
            var hit = result.Events.OfType<SourceHpChangedEvent>().Single();
            Assert.IsTrue(hit.Damage > 0 && hit.HpAfter > 0);
            Assert.AreEqual(actor, hit.Actor.Value); Assert.AreEqual(target, hit.Target.Value);
            Check(result, id, single, hit);
        }
    }

    [TestMethod, DataRow(124, false), DataRow(124, true), DataRow(188, false), DataRow(188, true), DataRow(209, false), DataRow(209, true)]
    public void SuccessfulAndFailedSecondaryRollsKeepTheHitAsTheOnlyVisualOwner(int id, bool applies)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            int target = actor ^ 1;
            var result = Kernel.Find(Kernel.Frame(Bodies(id, actor), single), actor, target,
                r => Qualifying(r) == applies, Spec(id).Name + " secondary=" + applies);
            var hit = result.Events.OfType<SourceHpChangedEvent>().Single();
            var statuses = result.Events.OfType<SourceStatusChangedEvent>().ToArray();
            Assert.AreEqual(applies ? 1 : 0, statuses.Length);
            Assert.AreEqual(applies ? Status(id) : WildBattleStatus.None, result.State.Slots[target].Body!.Status);
            if (applies)
            {
                Assert.AreEqual(target, statuses[0].Side.Value); Assert.AreEqual(WildBattleStatus.None, statuses[0].Before);
                Assert.AreEqual(Status(id), statuses[0].After); Assert.AreEqual("move", statuses[0].Reason);
                Assert.IsTrue(statuses[0].Sequence > hit.Sequence);
            }
            Check(result, id, single, hit);
        }
    }

    [TestMethod, DataRow(124), DataRow(188), DataRow(209)]
    public void BrokenAndSurvivingSubstitutesBlockQualifyingStatusButRetainTheirActualHit(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int substituteHp in new[] { 1, 200 }) // Source substitute HP is bounded to an unsigned byte.
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor);
            bodies[target] = bodies[target] with { CalledVolatile = new() { SubstituteHp = substituteHp } };
            var result = Kernel.Find(Kernel.Frame(bodies, single), actor, target, Qualifying, Spec(id).Name + " substitute secondary");
            var hit = result.Events.OfType<SourceSubstituteDamagedEvent>().Single();
            Assert.AreEqual(substituteHp, hit.HpBefore); Assert.IsTrue(hit.Damage > 0);
            Assert.AreEqual(substituteHp == 1, hit.HpAfter == 0);
            Assert.AreEqual(bodies[target].Pokemon.CurrentHp, result.State.Slots[target].Body!.Pokemon.CurrentHp);
            Assert.IsFalse(result.Events.OfType<SourceHpChangedEvent>().Any());
            Assert.IsFalse(result.Events.OfType<SourceStatusChangedEvent>().Any());
            Assert.AreEqual(WildBattleStatus.None, result.State.Slots[target].Body!.Status);
            Check(result, id, single, hit);
        }
    }

    [TestMethod, DataRow(124), DataRow(188), DataRow(209)]
    public void ActualEarlierProtectAndAccuracyMissesCannotAuthorizeTheHitRoot(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor);
            bodies[target] = Kernel.Mon("SQUIRTLE", "PROTECT", 100) with { SpeedStage = 0 };
            var protect = Kernel.Ordered(Kernel.Frame(bodies, single), target, target);
            Assert.IsTrue(protect.Events.OfType<SourceProtectAttemptEvent>().Single().Succeeded);
            Assert.IsTrue(protect.State.Slots[target].Body!.Protected); Assert.IsNotNull(protect.Continuation);
            var blocked = SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog, protect.State, protect.Continuation.Value);
            Assert.IsTrue(blocked.Succeeded, blocked.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(blocked.State));
            Assert.AreEqual(actor, blocked.Events.OfType<SourceMoveUsedEvent>().Single().Actor.Value);
            Assert.IsTrue(blocked.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "protected"));
            Assert.IsFalse(Kernel.Hits(blocked.Events).Any()); Check(blocked, id, single, null);

            bodies = Bodies(id, actor); bodies[actor] = bodies[actor] with { AccuracyStage = 0 };
            bodies[target] = bodies[target] with { EvasionStage = 12 };
            var missed = Kernel.Find(Kernel.Frame(bodies, single), actor, target,
                r => r.Events.OfType<SourceMoveMissedEvent>().Any(), Spec(id).Name + " accuracy miss");
            Assert.IsFalse(Kernel.Hits(missed.Events).Any()); Check(missed, id, single, null);
        }
    }

    [TestMethod, DataRow(124), DataRow(188), DataRow(209)]
    public void RealTypeImmunityDeniesEvenZeroLossReceiptsBehindSubstitute(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (bool substitute in new[] { false, true })
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor);
            bodies[target] = id == 209 ? Kernel.Mon("DIGLETT", "SCRATCH", 100) : Kernel.Mon("MAGNEMITE", "TACKLE", 100);
            bodies[target] = bodies[target] with { SpeedStage = 0, CalledVolatile = new() { SubstituteHp = substitute ? 50 : 0 } };
            var result = Kernel.Ordered(Kernel.Frame(bodies, single), actor, target);
            Assert.IsTrue(result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
            Assert.AreEqual(bodies[target].Pokemon.CurrentHp, result.State.Slots[target].Body!.Pokemon.CurrentHp);
            Assert.AreEqual(substitute ? 50 : 0, result.State.Slots[target].Body!.CalledVolatile!.SubstituteHp);
            Assert.IsFalse(result.Events.OfType<SourceStatusChangedEvent>().Any()); Check(result, id, single, null);
        }
    }

    [TestMethod, DataRow(124), DataRow(188), DataRow(209)]
    public void SilentAbilityStatusPreventionDoesNotSuppressSuccessfulDamage(int id)
    {
        foreach (bool single in new[] { false, true }) foreach (bool shieldDust in new[] { false, true })
        {
            var bodies = Bodies(id);
            bodies[1] = (shieldDust ? Kernel.Mon("CATERPIE", "STRING_SHOT", 100)
                : id == 209 ? Kernel.Mon("PERSIAN", "SCRATCH", 100) : Kernel.Mon("SNORLAX", "DEFENSE_CURL", 100)) with { SpeedStage = 0 };
            string ability = shieldDust ? "ABILITY_SHIELD_DUST" : id == 209 ? "ABILITY_LIMBER" : "ABILITY_IMMUNITY";
            Assert.AreEqual(ability, WildBattleEffective.Ability(bodies[1]));
            var result = Kernel.Find(Kernel.Frame(bodies, single), 0, 1, Qualifying, Spec(id).Name + " prevented secondary " + ability);
            var hit = result.Events.OfType<SourceHpChangedEvent>().Single(); Assert.IsTrue(hit.Damage > 0 && hit.HpAfter > 0);
            Assert.IsFalse(result.Events.OfType<SourceStatusChangedEvent>().Any());
            Assert.IsFalse(result.Events.OfType<SourceAbilityPreventedEvent>().Any(), "Ordinary secondary prevention is silent.");
            Assert.AreEqual(WildBattleStatus.None, result.State.Slots[1].Body!.Status); Check(result, id, single, hit);
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void SparkVoltAbsorbRecoveryCannotMasqueradeAsAHit(bool substitute)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(209); var target = Kernel.Mon("CHINCHOU", "WATER_GUN", 100);
            Assert.AreEqual("ABILITY_VOLT_ABSORB", WildBattleEffective.Ability(target));
            bodies[1] = target with { SpeedStage = 0, Pokemon = target.Pokemon with { CurrentHp = target.Pokemon.Stats.MaxHp - 20 },
                CalledVolatile = new() { SubstituteHp = substitute ? 50 : 0 } };
            var result = Kernel.Ordered(Kernel.Frame(bodies, single));
            Assert.AreEqual("ABILITY_VOLT_ABSORB", result.Events.OfType<SourceAbilityPreventedEvent>().Single().Ability);
            Assert.AreEqual(target.Pokemon.Stats.MaxHp, result.State.Slots[1].Body!.Pokemon.CurrentHp);
            Assert.IsTrue(result.Events.OfType<SourceRecoveredEvent>().Any());
            Assert.IsFalse(Kernel.Hits(result.Events).Any()); Assert.IsFalse(result.Events.OfType<SourceStatusChangedEvent>().Any());
            Assert.AreEqual(substitute ? 50 : 0, result.State.Slots[1].Body!.CalledVolatile!.SubstituteHp);
            Check(result, 209, single, null);
        }
    }

    [TestMethod, DataRow(124), DataRow(188), DataRow(209)]
    public void ActualMirrorMoveHistoryAuthenticatesTheSelectedLeafAndItsCallerPp(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(id, 1); bodies[0] = Kernel.Mon("PIDGEY", "MIRROR_MOVE", 100) with { AccuracyStage = 12 };
            var prior = Envelope.Find(Kernel.Frame(bodies, single), 1, 0,
                r => r.Events.OfType<SourceHpChangedEvent>().Any() && !r.Events.OfType<SourceStatusChangedEvent>().Any(),
                Spec(id).Name + " real prior hit for Mirror Move");
            Assert.AreEqual(Move(id), prior.State.Slots[0].Registers.History!.LastTakenMove);
            Assert.IsTrue(prior.State.Slots[0].Body!.Pokemon.CurrentHp > 0);
            var result = Kernel.Direct(prior.State, 0, 1);
            var cue = Called.Check(result, single, Kernel.Id("MIRROR_MOVE"), id);
            Assert.AreEqual(0, cue.Actor.Value); Assert.AreEqual(1, cue.Target.Value);
            var selection = result.Events.OfType<SourceCallerSelectionEvent>().Single();
            var transition = result.Events.OfType<SourceCalledMoveTransitionEvent>().Single();
            Assert.AreEqual(Move(id), selection.Result);
            Assert.AreEqual(Kernel.Id("MIRROR_MOVE"), result.State.Slots[0].Body!.Pokemon.Moves[0].Move);
            Called.Deny(result.Events.Remove(selection), single, id);
            Called.Deny(result.Events.Remove(transition), single, id);
        }
    }

    [TestMethod, DataRow(124), DataRow(188), DataRow(209)]
    public void GeneratedScriptsAndAuthenticAttackPpMetadataRemainRequired(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = Kernel.Ordered(Kernel.Frame(Bodies(id), single)); var events = result.Events;
            var used = events.OfType<SourceMoveUsedEvent>().Single(); int index = events.IndexOf(used);
            var attack = events.OfType<SourceAttackStringEvent>().Single(); var pp = events.OfType<SourcePpCommandEvent>().Single();
            Check(result, id, single, events.OfType<SourceHpChangedEvent>().Single());
            foreach (var bad in new[] { events.Remove(attack), events.Remove(pp),
                events.SetItem(events.IndexOf(attack), attack with { ChosenMove = Kernel.Id("METRONOME") }),
                events.SetItem(events.IndexOf(pp), pp with { PpOwner = new(1) }),
                events.SetItem(events.IndexOf(pp), pp with { CommandCost = 0 }) })
                Envelope.Deny(bad, single, "Missing or foreign invocation proof cannot authorize a Sludge/Spark root.");
            var moves = Kernel.Mechanics.Catalog.Moves; var definition = moves[Move(id)];
            foreach (var metadata in new[] { null, moves.Remove(Move(id)), moves.SetItem(Move(id), definition with { SourceMoveId = 33 }) })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Envelope.Single(events, index), true, SourceBattleScene.On, metadata).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, index), SourceBattleScene.On, metadata).Dispatch);
            Assert.IsNull(single ? CampaignMoveAnimationPresentation.AtAttackText(Envelope.Single(events, index), true, SourceBattleScene.On, moves, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(events, index), SourceBattleScene.On, moves, (_, _) => false).Dispatch);
        }
    }
}

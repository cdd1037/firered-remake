using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using static FireRed.Tests.Application.WildBattles.ConditionalRecoveryAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class RecoveryAnimationKernelTests
{
    [TestMethod, DataRow(156), DataRow(234), DataRow(236)]
    public void ActualRecoveryOwnsItsAuthoredBoundaryForEveryActorAndSinglesExport(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (uint status in new[] { WildBattleStatus.None, WildBattleStatus.Burn })
        {
            var bodies = Bodies(id, actor); bodies[actor] = bodies[actor] with { Status = status,
                Pokemon = bodies[actor].Pokemon with { CurrentHp = 1 } };
            var result = Kernel.Ordered(Kernel.Frame(bodies, single), actor, actor);
            var recovery = result.Events.OfType<SourceRecoveredEvent>().Single();
            Assert.AreEqual(actor, recovery.Side.Value); Assert.AreEqual(Move(id), recovery.Move);
            Assert.AreEqual(1, recovery.HpBefore); Assert.IsTrue(recovery.Recovered > 0);
            if (id == 156)
            {
                var sleep = result.Events.OfType<SourceStatusChangedEvent>().Single(e => e.Reason == "rest");
                Assert.AreEqual(status, sleep.Before); Assert.AreEqual(3u, sleep.After); Assert.AreEqual(actor, sleep.Side.Value);
                Assert.AreEqual(result.Events.IndexOf(sleep) + 1, result.Events.IndexOf(recovery));
                Assert.AreEqual(bodies[actor].Pokemon.Stats.MaxHp, recovery.HpAfter);
                Assert.AreEqual(3u, result.State.Slots[actor].Body!.Status);
            }
            else Assert.AreEqual(status, result.State.Slots[actor].Body!.Status);
            Check(result.Events, result, id, single, recovery);
        }
    }

    [TestMethod, DataRow(156), DataRow(234), DataRow(236)]
    public void FullHpFailureSpendsPpButOwnsNoRecoveryAnimation(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(id); bodies[0] = bodies[0] with { Status = WildBattleStatus.Burn };
            var result = Kernel.Ordered(Kernel.Frame(bodies, single), 0, 0);
            Assert.AreEqual(1, result.Events.OfType<SourcePpCommandEvent>().Count());
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "already-full-hp"));
            Assert.IsFalse(result.Events.OfType<SourceRecoveredEvent>().Any()); Assert.IsFalse(result.Events.OfType<SourceStatusChangedEvent>().Any());
            Check(result.Events, result, id, single, null);
        }
    }

    [TestMethod]
    public void SleepingRestSelectionIsCancelledBeforePpRatherThanInventingARecipeFailure()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(156); bodies[0] = bodies[0] with { Status = 3, Pokemon = bodies[0].Pokemon with { CurrentHp = 1 } };
            var result = Kernel.Direct(Kernel.Frame(bodies, single), 0, 0);
            Assert.IsTrue(result.Events.OfType<SourceActionBlockedEvent>().Any());
            var countdown = result.Events.OfType<SourceStatusChangedEvent>().Single();
            Assert.AreEqual("sleep-countdown", countdown.Reason); Assert.AreEqual(3u, countdown.Before); Assert.AreEqual(2u, countdown.After);
            Assert.IsFalse(result.Events.OfType<SourcePpCommandEvent>().Any()); Assert.IsFalse(result.Events.OfType<SourceRecoveredEvent>().Any());
            Deny(result.Events, single);
        }
    }

    [TestMethod, DataRow("DROWZEE", "POUND", "ABILITY_INSOMNIA"), DataRow("MANKEY", "SCRATCH", "ABILITY_VITAL_SPIRIT")]
    public void RestSleepPreventionKeepsActualPpWithoutAnyRecoveryRoot(string species, string natural, string ability)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(156); var body = MachineMon(species, "TM44", "REST", natural);
            bodies[0] = body with { SpeedStage = 12, Pokemon = body.Pokemon with { CurrentHp = 1 } };
            Assert.AreEqual(ability, WildBattleEffective.Ability(bodies[0]));
            var result = Kernel.Ordered(Kernel.Frame(bodies, single), 0, 0);
            Assert.IsTrue(result.Events.OfType<SourceAbilityPreventedEvent>().Any(e => e.Ability == ability));
            Assert.IsFalse(result.Events.OfType<SourceRecoveredEvent>().Any()); Check(result.Events, result, 156, single, null);
        }
    }

    [TestMethod]
    public void AnActualUproarLockPreventsRestWithoutForgedTurnRegisters()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(156); bodies[0] = Kernel.Mon("SNORLAX", "REST", 100);
            bodies[0] = bodies[0] with { Pokemon = bodies[0].Pokemon with { CurrentHp = bodies[0].Pokemon.Stats.MaxHp - 1 } };
            bodies[1] = Kernel.Mon("DODRIO", "UPROAR", 100) with { AccuracyStage = 12 };
            var uproar = Kernel.Direct(Kernel.Frame(bodies, single), 1, 0);
            Assert.AreEqual(WildBattleCalledLockKind.Uproar, uproar.State.Slots[1].Registers.Turn!.Lock!.Kind);
            Assert.IsTrue(uproar.State.Slots[0].Body!.Pokemon.CurrentHp < bodies[0].Pokemon.Stats.MaxHp);
            var result = Kernel.Direct(uproar.State, 0, 0);
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "uproar"));
            Assert.IsFalse(result.Events.OfType<SourceRecoveredEvent>().Any()); Check(result.Events, result, 156, single, null);
        }
    }

    [TestMethod, DataRow("ITEM_LUM_BERRY"), DataRow("ITEM_CHESTO_BERRY")]
    public void LaterBerryCureCannotMoveRestBeforeItsSleepTextOrCancelItsRecoveryBoundary(string item)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(156); bodies[0] = bodies[0] with { HeldItem = item, Pokemon = bodies[0].Pokemon with { CurrentHp = 1 } };
            var result = Kernel.Ordered(Kernel.Frame(bodies, single), 0, 0); var recovery = result.Events.OfType<SourceRecoveredEvent>().Single();
            var rest = result.Events.OfType<SourceStatusChangedEvent>().Single(e => e.Reason == "rest");
            var cure = result.Events.OfType<SourceStatusChangedEvent>().Single(e => e.Reason == "held-berry");
            Assert.IsTrue(rest.Sequence < recovery.Sequence && recovery.Sequence < cure.Sequence);
            Assert.AreEqual(3u, cure.Before); Assert.AreEqual(0u, cure.After);
            Assert.AreEqual(item, result.Events.OfType<SourceHeldItemConsumedEvent>().Single().Item);
            Assert.AreEqual(0u, result.State.Slots[0].Body!.Status); Assert.AreEqual("ITEM_NONE", result.State.Slots[0].Body!.HeldItem);
            Check(result.Events, result, 156, single, recovery);
        }
    }

    [TestMethod, DataRow(234), DataRow(236)]
    public void WeatherAndSuppressionCommitTheHealingAmountBeforeReadOnlyAnimationProjection(int id)
    {
        foreach (bool single in new[] { false, true })
        foreach (string weather in new[] { "none", "sun", "rain", "sandstorm", "hail", "suppressed-sun", "suppressed-rain", "clipped" })
        {
            var bodies = Bodies(id); int maximum = bodies[0].Pokemon.Stats.MaxHp;
            bodies[0] = bodies[0] with { Pokemon = bodies[0].Pokemon with { CurrentHp = weather == "clipped" ? maximum - 1 : 1 } };
            bool suppressed = weather.StartsWith("suppressed-", StringComparison.Ordinal);
            if (suppressed) bodies[1] = Kernel.Mon("RAYQUAZA", "FLY", 100);
            var frame = Kernel.Frame(bodies, single); string field = weather.Replace("suppressed-", "", StringComparison.Ordinal);
            if (field is not ("none" or "clipped")) frame = frame with { Field = Kernel.Temporary(frame.Field, field, 5) };
            var result = Kernel.Direct(frame, 0, 0); var recovery = result.Events.OfType<SourceRecoveredEvent>().Single();
            int expected = weather == "clipped" ? 1 : suppressed || field == "none" ? maximum / 2 : field == "sun" ? 20 * maximum / 30 : maximum / 4;
            Assert.AreEqual(expected, recovery.Recovered); Assert.AreEqual(recovery.HpBefore + expected, recovery.HpAfter);
            Assert.AreEqual(frame.Field, result.State.Field); Check(result.Events, result, id, single, recovery);
        }
    }

    [TestMethod, DataRow(156), DataRow(234), DataRow(236)]
    public void RecoveryRequiresItsOwnedAttackPpLivingHpAndUnambiguousSourcePrefix(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(id); bodies[0] = bodies[0] with { Pokemon = bodies[0].Pokemon with { CurrentHp = 1 } };
            var result = Kernel.Ordered(Kernel.Frame(bodies, single), 0, 0); var events = result.Events;
            var attack = events.OfType<SourceAttackStringEvent>().Single(); var used = events.OfType<SourceMoveUsedEvent>().Single();
            var pp = events.OfType<SourcePpCommandEvent>().Single(); var recovery = events.OfType<SourceRecoveredEvent>().Single();
            int ri = events.IndexOf(recovery); int cursor = id == 156 ? ri : events.IndexOf(used);
            foreach (var bad in new[] { attack with { Actor = new(1) }, attack with { ChosenMove = Kernel.Id("METRONOME") }, attack with { Turn = attack.Turn + 1 } })
                Deny(events.SetItem(events.IndexOf(attack), bad), single);
            foreach (var bad in new[] { pp with { PpOwner = new(1) }, pp with { Target = new(1) }, pp with { PpSlot = 3 }, pp with { CommandCost = 0 } })
                Deny(events.SetItem(events.IndexOf(pp), bad), single);
            foreach (var bad in new[] { recovery with { Side = new(1) }, recovery with { Move = Kernel.Id("RECOVER") }, recovery with { Turn = recovery.Turn + 1 },
                recovery with { HpBefore = 0, Recovered = recovery.HpAfter }, recovery with { HpAfter = recovery.HpBefore, Recovered = 0 }, recovery with { Recovered = recovery.Recovered + 1 } })
                Deny(events.SetItem(ri, bad), single);
            Deny(events.Remove(attack), single); Deny(events.Remove(pp), single); Deny(events.Remove(recovery), single);
            Deny(events.Insert(ri, recovery), single);
            Deny(events.Add(recovery with { Side = new(1), Sequence = events[^1].Sequence + 1 }), single);
            if (id == 156)
            {
                var status = events.OfType<SourceStatusChangedEvent>().Single(e => e.Reason == "rest"); int si = events.IndexOf(status);
                foreach (var bad in new[] { status with { Side = new(1) }, status with { Reason = "move" }, status with { After = 2 },
                    status with { Before = 3 }, status with { Turn = status.Turn + 1 } }) Deny(events.SetItem(si, bad), single);
                Deny(events.Remove(status), single); Deny(events.Insert(si, status), single);
                Deny(events.SetItem(si, recovery with { Sequence = status.Sequence }).SetItem(ri, status with { Sequence = recovery.Sequence }), single);
            }
            else
            {
                // A different receipt between PP and healing is not this source script.
                Deny(events.SetItem(ri, new SourceStatusChangedEvent(recovery.Sequence, recovery.Turn, used.Actor, 0, 3, "rest"))
                    .Insert(ri + 1, recovery with { Sequence = recovery.Sequence + 1 }), single);
            }
            var moves = Kernel.Mechanics.Catalog.Moves;
            foreach (var metadata in new[] { null, moves.Remove(Move(id)), moves.SetItem(Move(id), moves[Move(id)] with { SourceMoveId = 1 }) })
                Assert.IsNull(id == 156 ? single
                    ? AnimationDispatchFixture.AtResult(Envelope.Single(events, cursor), true, SourceBattleScene.On, metadata).Dispatch
                    : AnimationDispatchFixture.AtResult(Envelope.Double(events, cursor), SourceBattleScene.On, metadata).Dispatch
                    : single ? AnimationDispatchFixture.AtAttackText(Envelope.Single(events, cursor), true, SourceBattleScene.On, metadata).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, cursor), SourceBattleScene.On, metadata).Dispatch);
            Assert.IsNull(id == 156 ? single
                ? CampaignMoveAnimationPresentation.AtResult(Envelope.Single(events, cursor), true, SourceBattleScene.On, moves, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtResult(Envelope.Double(events, cursor), SourceBattleScene.On, moves, (_, _) => false).Dispatch
                : single ? CampaignMoveAnimationPresentation.AtAttackText(Envelope.Single(events, cursor), true, SourceBattleScene.On, moves, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(events, cursor), SourceBattleScene.On, moves, (_, _) => false).Dispatch);
        }
    }

    [TestMethod, DataRow(156), DataRow(234), DataRow(236)]
    public void ActualCalledRecoveryLeavesRemainOutsideDirectAnimationAdmission(int id)
    {
        uint? seed = null; for (uint candidate = 0; candidate < 65536; candidate++)
            if (WildBattleSourceCallers.Metronome(new(candidate)).SourceMoveId == id) { seed = candidate; break; }
        Assert.IsNotNull(seed);
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(id); var caller = Kernel.Mon("TOGETIC", "METRONOME", 29);
            bodies[0] = caller with { Pokemon = caller.Pokemon with { CurrentHp = 1 } };
            var result = Kernel.Direct(Kernel.Frame(bodies, single) with { Rng = new(seed.Value) });
            Assert.IsTrue(result.Events.OfType<SourceCallerSelectionEvent>().Any(e => e.Caller == Kernel.Id("METRONOME") && e.Result == Move(id)));
            Assert.IsTrue(result.Events.OfType<SourceRecoveredEvent>().Any(e => e.Move == Move(id))); Deny(result.Events, single);
        }
    }
}

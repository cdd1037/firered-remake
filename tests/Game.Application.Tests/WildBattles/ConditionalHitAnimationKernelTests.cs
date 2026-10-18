using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using static FireRed.Tests.Application.WildBattles.ConditionalRecoveryAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class ConditionalHitAnimationKernelTests
{
    [TestMethod, DataRow(161), DataRow(263), DataRow(265), DataRow(348), DataRow(62)]
    public void SelectedHitsUseEveryActorSinglesExportsAndSelectedAllies(int id)
    {
        Assert.AreEqual("MOVE_TARGET_SELECTED", Kernel.Mechanics.Catalog.Moves[Move(id)].Target);
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in single ? new[] { actor ^ 1 } : new[] { actor ^ 1, actor ^ 2 })
        {
            var result = Kernel.Ordered(Kernel.Frame(Bodies(id, actor), single), actor, target);
            Check(result.Events, result, id, single, Kernel.Hits(result.Events).Single());
        }
    }

    [TestMethod, DataRow(161), DataRow(263), DataRow(265), DataRow(348), DataRow(62)]
    public void SubstituteBreakKoAndTrueZeroLossEndureStillOwnOnlyOneHitRoot(int id)
    {
        foreach (bool single in new[] { false, true }) foreach (string branch in new[] { "substitute", "ko", "endure" })
        {
            var bodies = Bodies(id); bodies[1] = branch == "substitute" ? bodies[1] with { CalledVolatile = new() { SubstituteHp = 1 } }
                : bodies[1] with { Enduring = branch == "endure", Pokemon = bodies[1].Pokemon with { CurrentHp = 1 } };
            var result = Kernel.Ordered(Kernel.Frame(bodies, single));
            if (branch == "substitute")
            {
                Assert.AreEqual(0, result.Events.OfType<SourceSubstituteDamagedEvent>().Single().HpAfter);
                Assert.AreEqual(bodies[1].Pokemon.CurrentHp, result.State.Slots[1].Body!.Pokemon.CurrentHp);
            }
            else if (branch == "ko") Assert.IsTrue(result.Events.OfType<SourceFaintedEvent>().Any(e => e.Side.Value == 1));
            else
            {
                var hp = result.Events.OfType<SourceHpChangedEvent>().Single(); Assert.AreEqual(1, hp.HpBefore); Assert.AreEqual(1, hp.HpAfter);
                var adjustment = result.Events.OfType<SourceDamageAdjustedEvent>().Single();
                Assert.IsTrue(adjustment.Endured); Assert.IsTrue(adjustment.DamageBefore > 0); Assert.AreEqual(0, adjustment.DamageAfter);
            }
            Check(result.Events, result, id, single, Kernel.Hits(result.Events).Single());
        }
    }

    [TestMethod, DataRow(161), DataRow(263), DataRow(265), DataRow(348), DataRow(62)]
    public void ProtectAccuracyAndTypeOrWonderGuardPreventionCannotBorrowRoots(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(id); bodies[1] = bodies[1] with { Protected = true };
            var blocked = Kernel.Ordered(Kernel.Frame(bodies, single));
            Assert.IsTrue(blocked.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "protected")); Deny(blocked.Events, single);
            bodies[1] = bodies[1] with { Protected = false, EvasionStage = 12 }; bodies[0] = bodies[0] with { AccuracyStage = 0 };
            var missed = Kernel.Find(Kernel.Frame(bodies, single), 0, 1, r => r.Events.OfType<SourceMoveMissedEvent>().Any(), Spec(id).Name + " miss");
            Deny(missed.Events, single);
            bodies = Bodies(id); bodies[1] = id is 348 or 62 ? Kernel.Mon("SHEDINJA", "HARDEN", 100) : Kernel.Mon("GASTLY", "LICK", 100);
            // Level-100 Gastly otherwise outspeeds even the +6 level-50 Makuhita.
            bodies[1] = bodies[1] with { SpeedStage = 5 };
            // Ice is neutral against Bug/Ghost; Grass is resisted. Neither bypasses Wonder Guard.
            var immune = Kernel.Ordered(Kernel.Frame(bodies, single));
            Assert.IsTrue(immune.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect) ||
                immune.Events.OfType<SourceAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_WONDER_GUARD"));
            Assert.AreEqual(bodies[1].Pokemon.CurrentHp, immune.State.Slots[1].Body!.Pokemon.CurrentHp); Deny(immune.Events, single);
        }
    }

    [TestMethod, DataRow(161), DataRow(263), DataRow(265), DataRow(348), DataRow(62)]
    public void ActualCriticalAndOrdinaryDamageHaveTheSameSingleRootOwnership(int id)
    {
        foreach (bool critical in new[] { false, true })
        {
            var result = Kernel.Find(Kernel.Frame(Bodies(id)), 0, 1,
                r => r.Events.OfType<SourceHpChangedEvent>().Single().Critical == critical, Spec(id).Name + " critical=" + critical);
            Assert.AreEqual(critical, result.Events.OfType<SourceCriticalHitEvent>().Any());
            Check(result.Events, result, id, false, Kernel.Hits(result.Events).Single());
        }
    }

    [TestMethod, DataRow(-1), DataRow(0), DataRow(1), DataRow(2)]
    public void TriAttackOwnsHitWithNoSecondaryOrEachActualSourceStatusChoice(int choice)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = Kernel.Find(Kernel.Frame(Bodies(161), single), 0, 1,
                r => choice < 0 ? !r.Trace.Any(t => t.Purpose.EndsWith(".tri-attack-status", StringComparison.Ordinal))
                    : r.Trace.Any(t => t.Purpose.EndsWith(".tri-attack-status", StringComparison.Ordinal) && t.Raw % 3 == choice),
                "Tri Attack choice=" + choice);
            var status = result.Events.OfType<SourceStatusChangedEvent>().ToArray();
            if (choice < 0) Assert.AreEqual(0, status.Length);
            else
            {
                Assert.AreEqual(1, status.Length); Assert.AreEqual(1, status[0].Side.Value);
                Assert.AreEqual(choice == 0 ? WildBattleStatus.Burn : choice == 1 ? WildBattleStatus.Freeze : WildBattleStatus.Paralysis, status[0].After);
                Assert.IsTrue(Kernel.Hits(result.Events).Single().Sequence < status[0].Sequence);
            }
            Check(result.Events, result, 161, single, Kernel.Hits(result.Events).Single());
        }
    }

    [TestMethod, DataRow("substitute"), DataRow("existing-status"), DataRow("shield-dust"), DataRow("safeguard")]
    public void TriAttackSecondaryPreventionPreservesSourceChoiceRngBoundaryAndHit(string branch)
    {
        var bodies = Bodies(161);
        if (branch == "substitute") bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = 1 } };
        if (branch == "existing-status") bodies[1] = bodies[1] with { Status = WildBattleStatus.Burn };
        if (branch == "shield-dust") bodies[1] = Kernel.Mon("CATERPIE", "STRING_SHOT", 100);
        var frame = Kernel.Frame(bodies);
        if (branch == "safeguard") frame = frame with { Sides = frame.Sides.SetItem(1, frame.Sides[1] with { SafeguardTurnsRemaining = 5 }) };
        var result = Kernel.Find(frame, 0, 1,
            r => r.Trace.Any(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal) && t.Raw % 100 <= 20), "Tri Attack prevented " + branch);
        Assert.IsFalse(result.Events.OfType<SourceStatusChangedEvent>().Any());
        Assert.AreEqual(branch == "safeguard", result.Trace.Any(t => t.Purpose.EndsWith(".tri-attack-status", StringComparison.Ordinal)));
        Check(result.Events, result, 161, false, Kernel.Hits(result.Events).Single());
    }

    [TestMethod, DataRow(WildBattleStatus.None), DataRow(WildBattleStatus.Poison), DataRow(WildBattleStatus.Burn), DataRow(WildBattleSourceFieldEffects.ToxicPoison)]
    public void FacadeUsesCommittedStatusMultiplierAndRetainsGenThreeBurnReduction(uint status)
    {
        var bodies = Bodies(263); bodies[0] = bodies[0] with { Status = status };
        Assert.AreEqual("ABILITY_RUN_AWAY", WildBattleEffective.Ability(bodies[0]));
        var result = Kernel.Ordered(Kernel.Frame(bodies)); var hp = result.Events.OfType<SourceHpChangedEvent>().Single();
        // Test recipe-to-shared-arithmetic wiring, not a second damage oracle.
        var expected = SourceBattleDamage.Calculate(Kernel.Mechanics.Catalog, bodies[0], bodies[1], Kernel.Mechanics.Catalog.Moves[Move(263)],
            hp.Critical, hp.RandomPercent, sourceDamageMultiplier: status == 0 ? 1 : 2, sourceDoubleBattle: true);
        Assert.AreEqual(expected.Damage, hp.Damage);
        if (status == WildBattleStatus.Burn)
        {
            var unburned = SourceBattleDamage.Calculate(Kernel.Mechanics.Catalog, bodies[0] with { Status = 0 }, bodies[1], Kernel.Mechanics.Catalog.Moves[Move(263)],
                hp.Critical, hp.RandomPercent, sourceDamageMultiplier: 2, sourceDoubleBattle: true);
            Assert.IsTrue(hp.Damage < unburned.Damage);
        }
        Check(result.Events, result, 263, false, hp);
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void FacadeParalysisEitherCommitsTheBoostedHitOrCancelsBeforePp(bool blocked)
    {
        var bodies = Bodies(263); bodies[0] = bodies[0] with { Status = WildBattleStatus.Paralysis };
        var result = Envelope.Find(Kernel.Frame(bodies, true), 0, 1,
            r => r.Events.OfType<SourceActionBlockedEvent>().Any() == blocked, "Facade paralysis blocked=" + blocked);
        if (blocked) { Assert.IsFalse(result.Events.OfType<SourcePpCommandEvent>().Any()); Deny(result.Events, true); }
        else
        {
            var hp = result.Events.OfType<SourceHpChangedEvent>().Single();
            Assert.AreEqual(SourceBattleDamage.Calculate(Kernel.Mechanics.Catalog, bodies[0], bodies[1], Kernel.Mechanics.Catalog.Moves[Move(263)],
                hp.Critical, hp.RandomPercent, sourceDamageMultiplier: 2).Damage, hp.Damage);
            Check(result.Events, result, 263, true, hp);
        }
    }

    [TestMethod, DataRow("healthy"), DataRow("paralyzed"), DataRow("substitute"), DataRow("ko"), DataRow("endure")]
    public void SmellingSaltCureFollowsTheRealHitAndCannotGateOrDuplicateItsRoot(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(265); if (branch != "healthy") bodies[1] = bodies[1] with { Status = WildBattleStatus.Paralysis };
            if (branch == "substitute") bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = 1 } };
            if (branch is "ko" or "endure") bodies[1] = bodies[1] with { Enduring = branch == "endure", Pokemon = bodies[1].Pokemon with { CurrentHp = 1 } };
            var result = Kernel.Ordered(Kernel.Frame(bodies, single)); var hit = Kernel.Hits(result.Events).Single();
            var cures = result.Events.OfType<SourceStatusChangedEvent>().Where(e => e.Reason == "smelling-salt").ToArray();
            Assert.AreEqual(branch is "paralyzed" or "endure" ? 1 : 0, cures.Length);
            if (cures.Length > 0)
            { Assert.AreEqual(WildBattleStatus.Paralysis, cures[0].Before); Assert.AreEqual(0u, cures[0].After); Assert.IsTrue(hit.Sequence < cures[0].Sequence); }
            if (branch == "substitute") Assert.AreEqual(WildBattleStatus.Paralysis, result.State.Slots[1].Body!.Status);
            if (branch is "healthy" or "paralyzed" or "substitute")
            {
                var values = hit is SourceHpChangedEvent hp ? (hp.Damage, hp.Critical, hp.RandomPercent)
                    : hit is SourceSubstituteDamagedEvent sub ? (sub.Damage, sub.Critical, sub.RandomPercent) : throw new InvalidOperationException();
                Assert.AreEqual(SourceBattleDamage.Calculate(Kernel.Mechanics.Catalog, bodies[0], bodies[1], Kernel.Mechanics.Catalog.Moves[Move(265)],
                    values.Critical, values.RandomPercent, sourceDamageMultiplier: branch == "paralyzed" ? 2 : 1,
                    sourceDoubleBattle: !single).Damage, values.Damage);
            }
            if (branch is "healthy" or "paralyzed" or "endure") Assert.IsFalse(result.Trace.Any(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal)));
            Check(result.Events, result, 265, single, hit);
        }
    }

    [TestMethod]
    public void SmellingSaltCurePrecedesAnActualContactStatusOnTheAttacker()
    {
        var bodies = Bodies(265); bodies[1] = Kernel.Mon("PIKACHU", "THUNDER_SHOCK", 100) with { Status = WildBattleStatus.Paralysis };
        var result = Kernel.Find(Kernel.Frame(bodies), 0, 1,
            r => r.Events.OfType<SourceStatusChangedEvent>().Any(e => e.Side.Value == 0 && e.After == WildBattleStatus.Paralysis), "Smelling Salt then Static");
        var cure = result.Events.OfType<SourceStatusChangedEvent>().Single(e => e.Reason == "smelling-salt");
        var contact = result.Events.OfType<SourceStatusChangedEvent>().Single(e => e.Side.Value == 0);
        Assert.IsTrue(cure.Sequence < contact.Sequence); Check(result.Events, result, 265, false, Kernel.Hits(result.Events).Single());
    }

    [TestMethod, DataRow(161), DataRow(263), DataRow(265), DataRow(348), DataRow(62)]
    public void KingsRockUsesTheSourceMoveFlagWithoutAddingAnotherMoveRoot(int id)
    {
        var bodies = Bodies(id); bodies[0] = bodies[0] with { HeldItem = "ITEM_KINGS_ROCK" };
        var result = Kernel.Find(Kernel.Frame(bodies), 0, 1,
            r => r.Trace.Any(t => t.Purpose.EndsWith(".kings-rock", StringComparison.Ordinal) && t.Raw % 100 < 10), Spec(id).Name + " King's Rock qualifying draw");
        bool enabled = Kernel.Mechanics.Catalog.Moves[Move(id)].Flags.Contains("FLAG_KINGS_ROCK_AFFECTED");
        Assert.AreEqual(enabled, result.Events.OfType<SourceFlinchEvent>().Any(e => e.Applied));
        Check(result.Events, result, id, false, Kernel.Hits(result.Events).Single());
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void AuroraBeamAttackDropOwnsOnlyItsExistingSecondaryWave(bool lowered)
    {
        var result = Kernel.Find(Kernel.Frame(Bodies(62)), 0, 1,
            r => r.Events.OfType<SourceStatChangedEvent>().Any() == lowered, "Aurora Beam attack drop=" + lowered);
        Check(result.Events, result, 62, false, Kernel.Hits(result.Events).Single()); int waves = 0;
        for (int i = 0; i < result.Events.Length; i++)
        {
            var wave = AnimationDispatchFixture.StatAtResult(Envelope.Double(result.Events, i));
            if (result.Events[i] is SourceStatChangedEvent stat)
            { Assert.AreEqual(WildBattleStat.Attack, stat.Stat); Assert.AreEqual(6, stat.Before); Assert.AreEqual(5, stat.After); Assert.IsNotNull(wave); Assert.AreEqual(62, wave.SourceMoveId); waves++; }
            else Assert.IsNull(wave);
        }
        Assert.AreEqual(lowered ? 1 : 0, waves);
    }

    [TestMethod, DataRow("capped"), DataRow("clear-body"), DataRow("shield-dust"), DataRow("substitute")]
    public void PreventedAuroraAttackDropKeepsTheHitWithoutAStatWave(string prevention)
    {
        var bodies = Bodies(62);
        if (prevention == "capped") bodies[1] = bodies[1] with { AttackStage = 0 };
        if (prevention == "clear-body") bodies[1] = Kernel.Mon("REGISTEEL", "METAL_CLAW", 100);
        if (prevention == "shield-dust") bodies[1] = Kernel.Mon("CATERPIE", "STRING_SHOT", 100);
        if (prevention == "substitute") bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = 1 } };
        var result = Kernel.Find(Kernel.Frame(bodies), 0, 1,
            r => r.Trace.Any(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal) && t.Raw % 100 <= 10), "Aurora Beam prevented " + prevention);
        Assert.IsFalse(result.Events.OfType<SourceStatChangedEvent>().Any());
        for (int i = 0; i < result.Events.Length; i++) Assert.IsNull(AnimationDispatchFixture.StatAtResult(Envelope.Double(result.Events, i)));
        Check(result.Events, result, 62, false, Kernel.Hits(result.Events).Single());
    }

    [TestMethod, DataRow(161), DataRow(263), DataRow(265), DataRow(348), DataRow(62)]
    public void AttackPpAndHitOwnershipCannotBeForgedOrBackedByMissingMetadata(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = Kernel.Ordered(Kernel.Frame(Bodies(id), single)); var events = result.Events;
            var attack = events.OfType<SourceAttackStringEvent>().Single(); var pp = events.OfType<SourcePpCommandEvent>().Single();
            var used = events.OfType<SourceMoveUsedEvent>().Single(); var hp = events.OfType<SourceHpChangedEvent>().Single(); int at = events.IndexOf(used);
            foreach (var bad in new[] { attack with { Actor = new(1) }, attack with { ChosenMove = Kernel.Id("METRONOME") }, attack with { Turn = attack.Turn + 1 } })
                Deny(events.SetItem(events.IndexOf(attack), bad), single);
            foreach (var bad in new[] { pp with { PpOwner = new(1) }, pp with { Target = new(0) }, pp with { PpSlot = 3 }, pp with { CommandCost = 0 } })
                Deny(events.SetItem(events.IndexOf(pp), bad), single);
            foreach (var bad in new[] { hp with { Actor = new(1) }, hp with { Target = new(0) }, hp with { Move = Kernel.Id("TACKLE") }, hp with { Turn = hp.Turn + 1 } })
                Deny(events.SetItem(events.IndexOf(hp), bad), single);
            Deny(events.Remove(attack), single); Deny(events.Remove(pp), single); Deny(events.Remove(hp), single);
            var moves = Kernel.Mechanics.Catalog.Moves;
            foreach (var metadata in new[] { null, moves.Remove(Move(id)), moves.SetItem(Move(id), moves[Move(id)] with { SourceMoveId = 1 }) })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Envelope.Single(events, at), true, SourceBattleScene.On, metadata).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, at), SourceBattleScene.On, metadata).Dispatch);
            Assert.IsNull(single ? CampaignMoveAnimationPresentation.AtAttackText(Envelope.Single(events, at), true, SourceBattleScene.On, moves, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(events, at), SourceBattleScene.On, moves, (_, _) => false).Dispatch);
        }
    }

    [TestMethod, DataRow(161), DataRow(263), DataRow(265), DataRow(348), DataRow(62)]
    public void ActualMetronomeLeavesStayOutsideDirectInvocationAdmission(int id)
    {
        uint? seed = null; for (uint candidate = 0; candidate < 65536; candidate++)
            if (WildBattleSourceCallers.Metronome(new(candidate)).SourceMoveId == id) { seed = candidate; break; }
        Assert.IsNotNull(seed, "Require an actual bounded source caller selection.");
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(id); bodies[0] = Kernel.Mon("TOGETIC", "METRONOME", 29);
            var result = Kernel.Direct(Kernel.Frame(bodies, single) with { Rng = new(seed.Value) });
            Assert.IsTrue(result.Events.OfType<SourceCallerSelectionEvent>().Any(e => e.Caller == Kernel.Id("METRONOME") && e.Result == Move(id)));
            Assert.IsTrue(result.Events.OfType<SourceAttackStringEvent>().Any(e => e.CurrentMove == Move(id) && e.ChosenMove == Kernel.Id("METRONOME")));
            Deny(result.Events, single);
        }
    }
}

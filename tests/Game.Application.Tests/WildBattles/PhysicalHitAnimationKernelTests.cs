using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using static FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class PhysicalHitAnimationKernelTests
{
    [TestMethod, DataRow(184), DataRow(137), DataRow(296), DataRow(114), DataRow(65), DataRow(238), DataRow(6), DataRow(140)]
    public void PublicOrderedTurnsPublishAuthenticRootOwnersWithoutSyntheticInvocationEvents(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = id == 114 ? actor : actor ^ 1; var bodies = Bodies(id, actor);
            // Actor-first ordering must not put Scary Face's target at the speed-stage floor.
            for (int slot = 0; slot < 4; slot++) bodies[slot] = bodies[slot] with { SpeedStage = slot == actor ? 12 : 6 };
            var frame = Frame(bodies);
            var choices = frame.Slots.Select(s => new SourceActionSelection(s.Occupant!.Value, SourceActionKind.Move, 0,
                s.Slot.Value == actor ? new(target) : s.Slot)).ToImmutableArray();
            var next = SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision, choices);
            bool found = false;
            for (int step = 0; step < 12; step++)
            {
                Assert.IsTrue(next.Succeeded, next.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(next.State));
                var used = next.Events.OfType<SourceMoveUsedEvent>().SingleOrDefault(e => e.Actor.Value == actor && e.Move == Move(id));
                if (used is not null)
                {
                    found = true; var events = next.Events; var pp = events.OfType<SourcePpCommandEvent>().Single(p => p.Actor == used.Actor && p.CurrentMove == used.Move);
                    var receipts = id == 140 ? Hits(events) : new SourceBattleEvent[] { used };
                    foreach (var receipt in receipts)
                    {
                        int cursor = events.IndexOf(receipt);
                        var decision = id == 140 ? AnimationDispatchFixture.AtResult(Double(events, cursor), SourceBattleScene.On)
                            : AnimationDispatchFixture.AtAttackText(Double(events, cursor), SourceBattleScene.On);
                        Assert.IsNotNull(decision.Dispatch, decision.Reason); Assert.AreEqual(id, decision.Dispatch.SourceMoveId);
                        Assert.AreEqual(actor, decision.Dispatch.Actor.Value); Assert.AreEqual(target, decision.Dispatch.Target.Value);
                        Assert.AreEqual(pp.Sequence, decision.Dispatch.PpSequence);
                    }
                    break;
                }
                Assert.IsNotNull(next.Continuation, "The selected natural move must occur in this public ordered turn.");
                next = SourceFourActionTurns.Advance(Mechanics.Catalog, next.State, next.Continuation.Value);
            }
            Assert.IsTrue(found, "The actual selected invocation must execute within the bounded source turn.");
        }
    }

    [TestMethod, DataRow(296), DataRow(65), DataRow(238), DataRow(6), DataRow(140)]
    public void NaturalHitRootsUseEveryActorSlotBothSinglesSidesAndSelectedAllies(int id)
    {
        var definition = Mechanics.Catalog.Moves[Move(id)];
        Assert.AreEqual(id, definition.SourceMoveId); Assert.AreEqual(Spec(id).Effect, definition.Effect);
        Assert.AreEqual("MOVE_TARGET_SELECTED", definition.Target); Assert.IsTrue(definition.Power > 0);
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in single ? new[] { actor ^ 1 } : new[] { actor ^ 1, actor ^ 2 })
        {
            var result = Resolve(Frame(Bodies(id, actor), single), actor, target);
            Assert.IsTrue(Hits(result.Events).Length > 0); Check(result, id, single);
        }
    }

    [TestMethod, DataRow(296), DataRow(65), DataRow(238), DataRow(6), DataRow(140)]
    public void ActualMissAndProtectionNeverInventHitAnimation(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (bool protect in new[] { false, true })
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor);
            if (protect) bodies[target] = bodies[target] with { Protected = true };
            else bodies[actor] = bodies[actor] with { AccuracyStage = 0 };
            var frame = Frame(bodies, single);
            var result = protect ? Resolve(frame, actor, target) : Find(frame, actor, target,
                r => r.Events.OfType<SourceMoveMissedEvent>().Any(), $"{Spec(id).Name} miss");
            Assert.AreEqual(0, Hits(result.Events).Length); Check(result, id, single, false);
        }
    }

    [TestMethod, DataRow(296, "UMBREON", "TACKLE"), DataRow(238, "GASTLY", "LICK"),
        DataRow(6, "GASTLY", "LICK"), DataRow(140, "GASTLY", "LICK")]
    public void TypeImmunityCannotBorrowZeroLossHpReceipts(int id, string species, string name)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor); bodies[target] = Mon(species, name);
            var result = Resolve(Frame(bodies, single), actor, target);
            Assert.IsTrue(result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
            Assert.AreEqual(bodies[target].Pokemon.CurrentHp, result.State.Slots[target].Body!.Pokemon.CurrentHp);
            Check(result, id, single, false);
        }
    }

    [TestMethod, DataRow(296), DataRow(65), DataRow(238), DataRow(6)]
    public void OrdinaryRootsRetainSubstituteKoAndRealZeroLossEndureOutcomes(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (string branch in new[] { "substitute", "ko", "endure" })
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor);
            bodies[target] = branch == "substitute" ? bodies[target] with { CalledVolatile = new() { SubstituteHp = 1 } }
                : bodies[target] with { Enduring = branch == "endure", Pokemon = bodies[target].Pokemon with { CurrentHp = 1 } };
            if (branch == "ko")
            {
                // Faint/reward ownership belongs to the public turn lifecycle in singles.
                bodies[actor] = bodies[actor] with { SpeedStage = 12 };
                var ko = Ordered(Frame(bodies, single), actor, target);
                Assert.AreEqual(0, ko.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);
                Assert.IsTrue(ko.Events.OfType<SourceFaintedEvent>().Any(f => f.Side.Value == target));
                Check(ko, id, single);
                continue;
            }
            var result = Resolve(Frame(bodies, single), actor, target);
            if (branch == "substitute")
            {
                Assert.AreEqual(0, result.Events.OfType<SourceSubstituteDamagedEvent>().Single().HpAfter);
                Assert.AreEqual(bodies[target].Pokemon.CurrentHp, result.State.Slots[target].Body!.Pokemon.CurrentHp);
            }
            else
            {
                var hp = result.Events.OfType<SourceHpChangedEvent>().Single(); Assert.AreEqual(1, hp.HpBefore); Assert.AreEqual(1, hp.HpAfter);
                var adjustment = result.Events.OfType<SourceDamageAdjustedEvent>().Single();
                Assert.IsTrue(adjustment.Endured); Assert.IsTrue(adjustment.DamageBefore > 0); Assert.AreEqual(0, adjustment.DamageAfter);
            }
            Check(result, id, single);
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void MistBallSecondaryChanceAddsOnlyItsCommittedSpecialAttackWave(bool changes)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            int target = actor ^ 1;
            var result = Find(Frame(Bodies(296, actor), single), actor, target,
                r => r.Events.OfType<SourceStatChangedEvent>().Any() == changes, $"Mist Ball secondary={changes}");
            Assert.AreEqual(50, Mechanics.Catalog.Moves[Move(296)].SecondaryEffectChance);
            if (changes)
            {
                var changed = result.Events.OfType<SourceStatChangedEvent>().Single(); Assert.AreEqual(WildBattleStat.SpecialAttack, changed.Stat);
                Assert.AreEqual(6, changed.Before); Assert.AreEqual(5, changed.After);
                Assert.IsTrue(Hits(result.Events).Single().Sequence < changed.Sequence);
            }
            Check(result, 296, single);
        }
    }

    [TestMethod, DataRow("cap"), DataRow("clear-body"), DataRow("white-smoke"), DataRow("mist")]
    public void PreventedMistBallSecondaryCannotSuppressTheActualHitRoot(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(296);
            bodies[1] = branch switch {
                "cap" => bodies[1] with { SpecialAttackStageDelta = -6 },
                "clear-body" => Mon("TENTACOOL", "ACID", 100),
                "white-smoke" => Mon("TORKOAL", "EMBER", 100),
                _ => bodies[1]
            };
            var frame = Frame(bodies, single);
            if (branch == "mist") frame = frame with { Sides = frame.Sides.SetItem(1, frame.Sides[1] with { MistTurnsRemaining = 5 }) };
            var result = Find(frame, 0, 1, r => r.Trace.Any(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal) && t.Raw % 100 <= 50),
                "Mist Ball qualifying but prevented secondary");
            Assert.IsFalse(result.Events.OfType<SourceStatChangedEvent>().Any()); Check(result, 296, single);
        }
    }

    [TestMethod]
    public void PayDayOpponentAndSubstituteHitsDoNotRequireMoneyAward()
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (bool substitute in new[] { false, true })
        {
            int target = actor ^ 1; var bodies = Bodies(6, actor);
            if (substitute) bodies[target] = bodies[target] with { CalledVolatile = new() { SubstituteHp = 1 } };
            var result = Resolve(Frame(bodies, single), actor, target);
            var awards = result.Events.OfType<SourcePayDayEvent>().ToArray();
            if (substitute) Assert.AreEqual(0, awards.Length, "Substitute blocks the separate Pay Day secondary.");
            else
            {
                Assert.AreEqual(1, awards.Length);
                Assert.AreEqual((actor & 1) == 0 ? 250 : 0, awards[0].After - awards[0].Before);
            }
            Check(result, 6, single);
        }
    }

    [TestMethod, DataRow(2), DataRow(3), DataRow(4), DataRow(5)]
    public void BarrageOnlyCommittedHitsOwnOrdinalsAndNeverAnAttackTextRoot(int planned)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            int target = actor ^ 1;
            var result = Find(Frame(Bodies(140, actor), single), actor, target,
                r => r.Events.OfType<SourceMultiHitEvent>().Any(m => m.PlannedHits == planned), $"Barrage {planned} hits");
            Assert.AreEqual(planned, result.Events.OfType<SourceMultiHitEvent>().Single().ActualHits);
            Check(result, 140, single);
        }
    }

    [TestMethod, DataRow("substitute-break"), DataRow("substitute-survives"), DataRow("ko"), DataRow("endure")]
    public void BarrageBreakKoAndEndurePreserveActualReceiptsInsteadOfPlannedVisuals(string branch)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(140, actor);
            if (branch.StartsWith("substitute", StringComparison.Ordinal)) bodies[target] = bodies[target] with {
                CalledVolatile = new() { SubstituteHp = branch == "substitute-break" ? 1 : bodies[target].Pokemon.Stats.MaxHp / 4 } };
            else bodies[target] = bodies[target] with { Enduring = branch == "endure", Pokemon = bodies[target].Pokemon with { CurrentHp = 1 } };
            if (branch == "ko")
            {
                bodies[actor] = bodies[actor] with { SpeedStage = 12 };
                var ko = FindOrdered(Frame(bodies, single), actor, target,
                    r => r.Events.OfType<SourceMultiHitEvent>().Any(m => m.PlannedHits == 5), "Barrage KO");
                var koSummary = ko.Events.OfType<SourceMultiHitEvent>().Single();
                Assert.AreEqual(5, koSummary.PlannedHits); Assert.AreEqual(1, koSummary.ActualHits);
                Assert.AreEqual(0, ko.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);
                Assert.IsTrue(ko.Events.OfType<SourceFaintedEvent>().Any(f => f.Side.Value == target));
                Check(ko, 140, single);
                continue;
            }
            var result = Find(Frame(bodies, single), actor, target,
                r => r.Events.OfType<SourceMultiHitEvent>().Any(m => m.PlannedHits == 5), $"Barrage {branch}");
            var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
            Assert.AreEqual(branch == "endure" ? 1 : 5, summary.ActualHits);
            if (branch == "substitute-break")
            {
                Assert.AreEqual(1, result.Events.OfType<SourceSubstituteDamagedEvent>().Count());
                Assert.AreEqual(4, result.Events.OfType<SourceHpChangedEvent>().Count());
            }
            if (branch == "substitute-survives")
            {
                Assert.AreEqual(5, result.Events.OfType<SourceSubstituteDamagedEvent>().Count());
                Assert.IsTrue(result.Events.OfType<SourceSubstituteDamagedEvent>().Last().HpAfter > 0);
            }
            if (branch == "endure")
            {
                var hp = result.Events.OfType<SourceHpChangedEvent>().Single(); Assert.AreEqual(1, hp.HpBefore); Assert.AreEqual(1, hp.HpAfter);
            }
            Check(result, 140, single);
        }
    }

    [TestMethod]
    public void BarrageRejectsMissingDuplicatedForgedOrPrematureSummary()
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Resolve(Frame(Bodies(140), single)).Events;
            var summary = events.OfType<SourceMultiHitEvent>().Single(); int at = events.IndexOf(summary);
            var hits = Hits(events); int first = events.IndexOf(hits[0]);
            foreach (var invalid in new[] { summary with { Actor = new(1) }, summary with { Target = new(0) },
                summary with { Move = Id("TACKLE") }, summary with { PlannedHits = 1 }, summary with { PlannedHits = 6 },
                summary with { ActualHits = summary.ActualHits + 1 }, summary with { ActualHits = 0 } })
                Deny(events.SetItem(at, invalid), single, "Invalid Barrage summary");
            Deny(events.RemoveAt(at), single, "Missing Barrage summary");
            Deny(events.Insert(at + 1, summary with { Sequence = summary.Sequence + 1 }), single, "Duplicate Barrage summary");
            Deny(events.RemoveAt(first), single, "Actual hit count must match summary");
            Deny(events.SetItem(first, summary with { Sequence = hits[0].Sequence })
                .SetItem(at, hits[0] with { Sequence = summary.Sequence }), single, "Summary must follow all actual hits");
        }
    }

    [TestMethod, DataRow(184), DataRow(137), DataRow(296), DataRow(114), DataRow(65), DataRow(238), DataRow(6), DataRow(140)]
    public void EveryNewRootRequiresOwnedAuthenticAttackAndPpAndGeneratedMetadata(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = Resolve(Frame(Bodies(id), single), 0, id == 114 ? 0 : 1); var events = result.Events;
            var attack = events.OfType<SourceAttackStringEvent>().Single(); int attackIndex = events.IndexOf(attack);
            var pp = events.OfType<SourcePpCommandEvent>().Single(); int ppIndex = events.IndexOf(pp);
            var used = events.OfType<SourceMoveUsedEvent>().Single();
            foreach (var invalid in new[] { attack with { Actor = new(1) }, attack with { ChosenMove = Id("METRONOME") },
                attack with { CurrentMove = Id("TACKLE") }, attack with { Turn = attack.Turn + 1 }, attack with { Sequence = used.Sequence } })
                Deny(events.SetItem(attackIndex, invalid), single, "Unauthenticated attack string");
            foreach (var invalid in new[] { pp with { Actor = new(1) }, pp with { PpOwner = new(1) }, pp with { Target = new(id == 114 ? 1 : 0) },
                pp with { CurrentMove = Id("TACKLE") }, pp with { ChosenMove = Id("METRONOME") },
                pp with { PpSlot = 3 }, pp with { Before = pp.Before + 1 }, pp with { After = pp.Before }, pp with { CommandCost = 0 } })
                Deny(events.SetItem(ppIndex, invalid), single, "Unauthenticated PP command");
            Deny(events.RemoveAt(attackIndex), single, "Missing attack string");
            Deny(events.RemoveAt(ppIndex), single, "Missing PP command");
            Deny(events.Insert(ppIndex + 1, pp with { Sequence = pp.Sequence + 1 }), single, "Duplicate PP command");
            int cursor = events.IndexOf(id == 140 ? Hits(events)[0] : used);
            var metadata = Mechanics.Catalog.Moves.ToDictionary(x => x.Key, x => x.Value);
            metadata[Move(id)] = metadata[Move(id)] with { SourceMoveId = id == 6 ? 65 : 6 };
            foreach (var moves in new[] { null, metadata })
            {
                var denied = single
                    ? id == 140 ? AnimationDispatchFixture.AtResult(Single(events, cursor), true, SourceBattleScene.On, moves)
                        : AnimationDispatchFixture.AtAttackText(Single(events, cursor), true, SourceBattleScene.On, moves)
                    : id == 140 ? AnimationDispatchFixture.AtResult(Double(events, cursor), SourceBattleScene.On, moves)
                        : AnimationDispatchFixture.AtAttackText(Double(events, cursor), SourceBattleScene.On, moves);
                Assert.IsNull(denied.Dispatch, "Missing definitions or cross-root source ID must fail closed.");
            }
            var missingRenderer = single
                ? id == 140 ? CampaignMoveAnimationPresentation.AtResult(Single(events, cursor), true, SourceBattleScene.On, Mechanics.Catalog.Moves, (_, _) => false)
                    : CampaignMoveAnimationPresentation.AtAttackText(Single(events, cursor), true, SourceBattleScene.On, Mechanics.Catalog.Moves, (_, _) => false)
                : id == 140 ? CampaignMoveAnimationPresentation.AtResult(Double(events, cursor), SourceBattleScene.On, Mechanics.Catalog.Moves, (_, _) => false)
                    : CampaignMoveAnimationPresentation.AtAttackText(Double(events, cursor), SourceBattleScene.On, Mechanics.Catalog.Moves, (_, _) => false);
            Assert.IsNull(missingRenderer.Dispatch);
        }
    }
}

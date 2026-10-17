using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class TripleKickAnimationDispatchTests
{
    private static readonly MoveId Kick = Id("TRIPLE_KICK");
    private static MoveId Id(string name) => new("pret:MOVE_" + name);
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index = 0) =>
        new("triple-kick", new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index = 0) =>
        new("triple-kick", null!, new(), events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(), index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []) { SourceEvents = events };
    private static CampaignMoveAnimationDecision At(ImmutableArray<SourceBattleEvent> events, int index) =>
        AnimationDispatchFixture.AtHitResult(Double(events, index), SourceBattleScene.On);
    private static ImmutableArray<SourceBattleEvent> Number(IEnumerable<SourceBattleEvent> events) =>
        events.Select((e, i) => e with { Sequence = i + 1 }).ToImmutableArray();
    private static SourceBattleEvent[] Hits(ImmutableArray<SourceBattleEvent> events) =>
        events.Where(e => e is SourceHpChangedEvent or SourceSubstituteDamagedEvent).ToArray();
    private static ImmutableArray<SourceBattleEvent> Group(int actual, int actor = 0, int? selectedTarget = null)
    {
        var a = new BattlerSlot(actor); var t = new BattlerSlot(selectedTarget ?? (actor ^ 1));
        List<SourceBattleEvent> events = [
            new SourceAttackStringEvent(0, 7, a, Kick, Kick),
            new SourceMoveUsedEvent(0, 7, a, t, 0, Kick, 10, 9),
            new SourcePpCommandEvent(0, 7, a, t, 0, Kick, Kick, 10, 9, 1, 0)];
        for (int hit = 0; hit < actual; hit++)
            events.Add(new SourceHpChangedEvent(0, 7, a, t, Kick, 100 - 5 * hit, 95 - 5 * hit, 5, false, 100));
        events.Add(new SourceMultiHitEvent(0, 7, a, t, Kick, 3, actual));
        return Number(events);
    }
    private static void AssertDenied(ImmutableArray<SourceBattleEvent> events, string reason)
    {
        for (int index = 0; index < events.Length; index++)
        {
            Assert.IsNull(At(events, index).Dispatch, $"{reason}: event {index}.");
            if (events[index] is SourceMoveUsedEvent)
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events, index), SourceBattleScene.On).Dispatch,
                    "Triple Kick must never acquire an extra attack-text animation.");
        }
    }
    private static void AssertDispatches(ImmutableArray<SourceBattleEvent> events, int actor, int target, int expectedHits)
    {
        Assert.IsTrue(expectedHits is >= 1 and <= 3);
        Assert.AreEqual(expectedHits, Hits(events).Length);
        var summary = events.OfType<SourceMultiHitEvent>().Single();
        Assert.AreEqual(3, summary.PlannedHits); Assert.AreEqual(expectedHits, summary.ActualHits);
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        string before = JsonSerializer.Serialize(events);
        int ordinal = 0;
        for (int index = 0; index < events.Length; index++)
        {
            var decision = At(events, index);
            if (events[index] is not (SourceHpChangedEvent or SourceSubstituteDamagedEvent))
            {
                Assert.IsNull(decision.Dispatch, events[index].GetType().Name);
                if (events[index] is SourceMoveUsedEvent)
                    Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events, index), SourceBattleScene.On).Dispatch);
                continue;
            }
            Assert.IsNotNull(decision.Dispatch, decision.Reason);
            Assert.AreEqual("triple-kick", decision.Dispatch.BattleId);
            Assert.AreEqual(167, decision.Dispatch.SourceMoveId);
            Assert.AreEqual(actor, decision.Dispatch.Actor.Value); Assert.AreEqual(target, decision.Dispatch.Target.Value);
            Assert.AreEqual(events[index].Sequence, decision.Dispatch.Sequence);
            Assert.AreEqual(events[index].Sequence, decision.Dispatch.ResultSequence);
            Assert.AreEqual(attack.Sequence, decision.Dispatch.AttackSequence);
            Assert.AreEqual(pp.Sequence, decision.Dispatch.PpSequence);
            Assert.AreEqual((byte)ordinal++, decision.Dispatch.MoveTurn);
            Assert.AreEqual(events[index] is SourceSubstituteDamagedEvent, decision.Dispatch.HitsSubstitute);
            Assert.IsFalse(decision.Dispatch.AcknowledgesCurrentReceipt);
            Assert.AreEqual(decision, At(events, index), "Projection cannot consume the authoritative receipt.");
        }
        Assert.AreEqual(expectedHits, ordinal);
        Assert.AreEqual(before, JsonSerializer.Serialize(events), "Projection cannot mutate event history.");
    }

    [TestMethod]
    public void FixedThreeHitPlanUsesOnlyCommittedOrdinalsAtAllFourPositionsAndSelectedAllies()
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        for (int actual = 1; actual <= 3; actual++)
            AssertDispatches(Group(actual, actor, target), actor, target, actual);
    }

    [TestMethod]
    public void SinglesAdapterPreservesReceiptsAndHonorsRulesScenePhaseAndCursorGates()
    {
        for (int actor = 0; actor < 2; actor++)
        for (int actual = 1; actual <= 3; actual++)
        {
            var events = Group(actual, actor); var single = Single(events);
            for (int index = 0; index < events.Length; index++)
                Assert.AreEqual(At(events, index).Dispatch,
                    AnimationDispatchFixture.AtHitResult(single with { EventIndex = index }, true, SourceBattleScene.On).Dispatch);
            var hit = single with { EventIndex = 3 };
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(hit, false, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(hit, true, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(hit with { Phase = CampaignBattlePhase.ChooseMove }, true, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(hit with { EventIndex = -1 }, true, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(hit with { EventIndex = events.Length }, true, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(single with { EventIndex = 1 }, true, SourceBattleScene.On).Dispatch);
        }
        var twice = Double(Group(3), 3);
        Assert.IsNull(AnimationDispatchFixture.AtHitResult(twice, SourceBattleScene.Off).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtHitResult(twice with { Stage = CampaignDoubleBattleStage.Finished }, SourceBattleScene.On).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtHitResult(twice with { EventIndex = -1 }, SourceBattleScene.On).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtHitResult(twice with { EventIndex = twice.Events.Length }, SourceBattleScene.On).Dispatch);
    }

    [TestMethod]
    public void WrongFixedPlanAndMissingDuplicateOrContradictorySummaryFailClosed()
    {
        // Planned 2/4/5 are valid for other multihits, but never for Triple Kick.
        foreach (int actual in new[] { 1, 2, 3 })
        foreach (int planned in new[] { 0, 1, 2, 4, 5, 6 })
        {
            var events = Group(actual); var summary = (SourceMultiHitEvent)events[^1];
            AssertDenied(events.SetItem(events.Length - 1, summary with { PlannedHits = planned }), $"planned {planned}");
        }
        var good = Group(3); var last = (SourceMultiHitEvent)good[^1];
        foreach (int actual in new[] { -1, 0, 1, 2, 4 })
            AssertDenied(good.SetItem(good.Length - 1, last with { ActualHits = actual }), $"actual {actual}");
        AssertDenied(good.RemoveAt(good.Length - 1), "missing summary");
        AssertDenied(Number(good.Add(last)), "duplicate summary");
        AssertDenied(Number(good.RemoveAt(good.Length - 1).Insert(4, last)), "summary before remaining hits");
        AssertDenied(Group(0), "no committed hit");
        var attack = (SourceAttackStringEvent)good[0]; var pp = (SourcePpCommandEvent)good[2];
        AssertDenied(good.SetItem(0, attack with { ChosenMove = Id("SLEEP_TALK") }), "called attack remains outside direct invocation");
        AssertDenied(good.SetItem(2, pp with { ChosenMove = Id("METRONOME") }), "called PP");
        AssertDenied(good.SetItem(2, pp with { PpOwner = new(2) }), "wrong PP owner");
        AssertDenied(Number(good.RemoveAt(2).Insert(3, pp)), "PP after first hit");
        AssertDenied(Number(good.Insert(4, new SourceMoveMissedEvent(0, 7, new(0), new(1), Kick))),
            "a real partial miss suppresses its miss event; contradictory receipts remain rejected");
    }

    private static SourceMechanicsCatalog Mechanics =>
        TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static WildBattleCombatant NaturalMon(string species, int level, string name)
    {
        var mechanics = Mechanics; var move = Id(name);
        var pokemon = SourcePokemonFactory.Create(mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        Assert.IsTrue(mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level),
            $"Natural source learnset fixture: {species}/{name}/{level}.");
        int pp = mechanics.Catalog.Moves[move].Pp;
        return new(pokemon with { Moves = [new(move, pp, pp)] });
    }
    private static WildBattleCombatant[] Bodies(int actor, bool certainAccuracy = true)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL")).ToArray();
        bodies[actor] = NaturalMon("HITMONTOP", 30, "TRIPLE_KICK") with { AccuracyStage = certainAccuracy ? 12 : 6 };
        return bodies;
    }
    private static SourceBattleFrame KernelFrame(WildBattleCombatant[] bodies, uint actionSeed)
    {
        static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
            new(body.Pokemon, body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
        var frame = new SourceBattleFrame {
            Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version, BattleKind = WildBattleCalledBattleKind.Trainer,
            Rng = new(31),
            Parties = [new(BattleSide.Player, [Member(bodies[0]), Member(bodies[2])]),
                new(BattleSide.Opponent, [Member(bodies[1]), Member(bodies[3])])],
            Slots = Enumerable.Range(0, 4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray()
        };
        // Natural Hitmontop has Intimidate. Use the real entry boundary rather than
        // manufacturing AppliedEntries. These fixtures control the later action-start RNG.
        var entry = SourceFourEntryAbilities.InitializeEntry(Mechanics.Catalog, frame, frame.Revision);
        Assert.IsTrue(entry.Succeeded, entry.Fault);
        return entry.State with { Rng = new(actionSeed) };
    }
    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor, int target)
    {
        Assert.IsNull(SourceBattleIdentityRules.Validate(frame));
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault);
        Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty);
        return result;
    }
    private static (uint Seed, SourceKernelResult Result) Find(WildBattleCombatant[] bodies,
        int actor, int target, Func<SourceKernelResult, bool> predicate, string branch)
    {
        var entered = KernelFrame(bodies, 0);
        // Bounded integration branch discovery, not a generated golden/original-source oracle.
        // Every sought branch must actually be observed; exhausting the bound fails the test.
        for (uint seed = 0; seed < 1024; seed++)
        {
            var result = Resolve(entered with { Rng = new(seed) }, actor, target);
            if (predicate(result)) return (seed, result);
        }
        throw new InvalidOperationException($"No observed {branch} in 1024 action-start seeds.");
    }

    [TestMethod]
    public void PublicKernelThreeHitsUseTenTwentyThirtyPowerAndOnePpAtAllPositionsAndSelectedAllies()
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        {
            var frame = KernelFrame(Bodies(actor), 31); var result = Resolve(frame, actor, target);
            var hits = result.Events.OfType<SourceHpChangedEvent>().ToArray(); Assert.AreEqual(3, hits.Length);
            for (int ordinal = 0; ordinal < hits.Length; ordinal++)
            {
                // Check the recipe's source power progression through the public damage API;
                // this does not duplicate the damage algorithm or claim an independent oracle.
                int expected = SourceBattleDamage.Calculate(Mechanics.Catalog, frame.Slots[actor].Body!, frame.Slots[target].Body!,
                    Mechanics.Catalog.Moves[Kick], hits[ordinal].Critical, hits[ordinal].RandomPercent,
                    dynamicPower: (ordinal + 1) * 10, sourceDoubleBattle: true).Damage;
                Assert.AreEqual(expected, hits[ordinal].Damage);
            }
            var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreEqual(10, pp.Before); Assert.AreEqual(9, pp.After); Assert.AreEqual(1, pp.CommandCost);
            Assert.AreEqual(3, result.Trace.Count(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)));
            AssertDispatches(result.Events, actor, target, 3);
        }
    }

    [TestMethod]
    public void PublicKernelFirstAndPartialAccuracyMissesUseOnlySuccessfulReceiptsAtEveryPosition()
    {
        foreach (int successful in new[] { 0, 1, 2 })
        {
            var found = Find(Bodies(0, false), 0, 1,
                r => r.Events.OfType<SourceMultiHitEvent>().Single().ActualHits == successful,
                $"{successful} successful kicks then accuracy miss");
            for (int actor = 0; actor < 4; actor++)
            {
                var result = Resolve(KernelFrame(Bodies(actor, false), found.Seed), actor, actor ^ 1);
                Assert.AreEqual(successful, result.Events.OfType<SourceMultiHitEvent>().Single().ActualHits);
                var accuracy = result.Trace.Where(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)).ToArray();
                Assert.AreEqual(successful + 1, accuracy.Length);
                Assert.IsTrue(accuracy.Take(successful).All(t => t.Raw % 100 + 1 <= 90));
                Assert.IsTrue(accuracy[^1].Raw % 100 + 1 > 90, "The cutoff must be an actual failed 90%-accuracy draw.");
                Assert.IsTrue(result.State.Slots[actor].Body!.Pokemon.CurrentHp > 0);
                Assert.IsTrue(result.State.Slots[actor ^ 1].Body!.Pokemon.CurrentHp > 0);
                Assert.AreEqual(successful == 0 ? 1 : 0, result.Events.OfType<SourceMoveMissedEvent>().Count());
                if (successful == 0) { Assert.AreEqual(0, Hits(result.Events).Length); AssertDenied(result.Events, "initial miss"); }
                else AssertDispatches(result.Events, actor, actor ^ 1, successful);
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PublicKernelImmunityOrProtectHasNoSuccessfulReceipt(bool protect)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(actor);
            bodies[target] = protect ? bodies[target] with { Protected = true } : NaturalMon("GASTLY", 100, "LICK");
            var result = Resolve(KernelFrame(bodies, 31), actor, target);
            var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
            Assert.AreEqual(3, summary.PlannedHits); Assert.AreEqual(0, summary.ActualHits);
            Assert.AreEqual(0, Hits(result.Events).Length);
            if (!protect) Assert.IsTrue(result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
            AssertDenied(result.Events, protect ? "protected" : "immune");
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PublicKernelSubstituteBreakThenBodyOrEarlyTargetKoKeepsActualOrdinals(bool ko)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(actor);
            bodies[target] = ko ? bodies[target] with { Pokemon = bodies[target].Pokemon with { CurrentHp = 1 } }
                : bodies[target] with { CalledVolatile = new() { SubstituteHp = 1 } };
            var result = Resolve(KernelFrame(bodies, 31), actor, target);
            if (ko)
            {
                Assert.AreEqual(0, result.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);
                Assert.AreEqual(target, result.Events.OfType<SourceFaintedEvent>().Single().Side.Value);
            }
            else
            {
                var substitute = result.Events.OfType<SourceSubstituteDamagedEvent>().Single();
                Assert.AreEqual(1, substitute.HpBefore); Assert.AreEqual(0, substitute.HpAfter);
                var bodyHits = result.Events.OfType<SourceHpChangedEvent>().ToArray(); Assert.AreEqual(2, bodyHits.Length);
                Assert.IsTrue(substitute.Sequence < bodyHits[0].Sequence);
            }
            AssertDispatches(result.Events, actor, target, ko ? 1 : 3);
        }
    }

    [TestMethod]
    public void PublicKernelRoughSkinActorKoStopsAfterItsOneCommittedKick()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(actor);
            bodies[actor] = bodies[actor] with { Pokemon = bodies[actor].Pokemon with { CurrentHp = 1 } };
            bodies[target] = NaturalMon("SHARPEDO", 100, "BITE");
            Assert.AreEqual("ABILITY_ROUGH_SKIN", bodies[target].Pokemon.AbilityId);
            var result = Resolve(KernelFrame(bodies, 31), actor, target);
            var hit = result.Events.OfType<SourceHpChangedEvent>().Single();
            var contact = result.Events.OfType<SourceAbilityDamageEvent>().Single();
            var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
            Assert.AreEqual(target, contact.Holder.Value); Assert.AreEqual(actor, contact.Target.Value);
            Assert.AreEqual(1, contact.HpBefore); Assert.AreEqual(0, contact.HpAfter);
            Assert.IsTrue(hit.Sequence < contact.Sequence && contact.Sequence < summary.Sequence);
            Assert.AreEqual(actor, result.Events.OfType<SourceFaintedEvent>().Single().Side.Value);
            AssertDispatches(result.Events, actor, target, 1);
        }
    }

    [TestMethod]
    public void PublicKernelEffectSporeSleepStopsBeforeAnotherAccuracyCheckAtEveryPosition()
    {
        WildBattleCombatant[] SporeBodies(int actor)
        {
            var bodies = Bodies(actor); bodies[actor ^ 1] = NaturalMon("SHROOMISH", 100, "ABSORB");
            Assert.AreEqual("ABILITY_EFFECT_SPORE", bodies[actor ^ 1].Pokemon.AbilityId); return bodies;
        }
        var found = Find(SporeBodies(0), 0, 1, r => WildBattleStatus.IsSleeping(r.State.Slots[0].Body!.Status) &&
            r.Events.OfType<SourceMultiHitEvent>().Single().ActualHits is 1 or 2, "contact-induced sleep before third kick");
        for (int actor = 0; actor < 4; actor++)
        {
            var result = Resolve(KernelFrame(SporeBodies(actor), found.Seed), actor, actor ^ 1);
            int committedHits = result.Events.OfType<SourceMultiHitEvent>().Single().ActualHits;
            Assert.IsTrue(committedHits is 1 or 2); Assert.IsTrue(WildBattleStatus.IsSleeping(result.State.Slots[actor].Body!.Status));
            var sleep = result.Events.OfType<SourceStatusChangedEvent>().Single(e => e.Side.Value == actor && WildBattleStatus.IsSleeping(e.After));
            Assert.AreEqual("effect-spore", sleep.Reason);
            Assert.IsTrue(Hits(result.Events)[^1].Sequence < sleep.Sequence);
            Assert.AreEqual(committedHits, result.Trace.Count(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)));
            Assert.AreEqual(0, result.Events.OfType<SourceMoveMissedEvent>().Count());
            AssertDispatches(result.Events, actor, actor ^ 1, committedHits);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PublicKernelZeroLossFocusBandOrEndureStillCommitsAnAnimation(bool endure)
    {
        WildBattleCombatant[] SurvivalBodies(int actor)
        {
            var bodies = Bodies(actor); int target = actor ^ 1;
            bodies[target] = bodies[target] with {
                Pokemon = bodies[target].Pokemon with { CurrentHp = 1 }, Enduring = endure,
                HeldItem = endure ? "ITEM_NONE" : "ITEM_FOCUS_BAND" };
            return bodies;
        }
        int expectedHits = endure ? 1 : 3;
        uint seed = endure ? 31 : Find(SurvivalBodies(0), 0, 1,
            r => r.Events.OfType<SourceMultiHitEvent>().Single().ActualHits == 3 &&
                r.Events.OfType<SourceHpChangedEvent>().All(h => h.HpBefore == 1 && h.HpAfter == 1 && h.Damage == 0),
            "sticky Focus Band zero-loss survival on all three kicks").Seed;
        for (int actor = 0; actor < 4; actor++)
        {
            var result = Resolve(KernelFrame(SurvivalBodies(actor), seed), actor, actor ^ 1);
            var hits = result.Events.OfType<SourceHpChangedEvent>().ToArray(); Assert.AreEqual(expectedHits, hits.Length);
            Assert.IsTrue(hits.All(h => h.HpBefore == 1 && h.HpAfter == 1 && h.Damage == 0));
            var adjustments = result.Events.OfType<SourceDamageAdjustedEvent>().ToArray(); Assert.AreEqual(expectedHits, adjustments.Length);
            Assert.IsTrue(adjustments.All(a => a.DamageBefore > 0 && a.DamageAfter == 0 && a.Endured == endure && a.HungOn != endure));
            Assert.AreEqual(0, result.Events.OfType<SourceFaintedEvent>().Count());
            AssertDispatches(result.Events, actor, actor ^ 1, expectedHits);
        }
    }
}

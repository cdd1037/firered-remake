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

// Nidoran M learns Double Kick naturally at level 12. HP, stat stages and action-start
// seeds below are controlled branch fixtures; the move definition and rules are unchanged.
[TestClass, TestCategory("Game")]
public sealed class DoubleKickMoveAnimationDispatchTests
{
    private static readonly DirectHitAnimationContract Contract = new("double-kick", 24, Id("DOUBLE_KICK"), 30, DirectHitOwnership.EachHit, 2, 2);
    private static readonly MoveId Kick = Id("DOUBLE_KICK");
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) => Contract.Double(events, index);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) => Contract.Single(events, index);

    private static ImmutableArray<SourceBattleEvent> Number(IEnumerable<SourceBattleEvent> events) =>
        events.Select((e, index) => e with { Sequence = index + 1 }).ToImmutableArray();

    private static WildBattleCombatant[] Bodies(int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL") with { SpeedStage = 0 }).ToArray();
        bodies[actor] = NaturalMon("NIDORAN_M", 12, "DOUBLE_KICK") with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }

    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies, uint seed = 31, bool single = false)
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
        return entry.State with { Rng = new(seed) };
    }

    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor = 0, int target = 1)
    {
        Assert.IsNull(SourceBattleIdentityRules.Validate(frame));
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty); return result;
    }

    private static SourceFourActionResult Ordered(SourceBattleFrame frame, int actor, int target)
    {
        var choices = frame.Slots.Take(frame.BattlerCount).Select(c => new SourceActionSelection(c.Occupant!.Value,
            SourceActionKind.Move, 0, new(c.Slot.Value == actor ? target : c.Slot.Value))).ToImmutableArray();
        var result = SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision, choices);
        for (int step = 0; step < 12; step++)
        {
            Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            if (result.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == actor && e.Move == Kick)) return result;
            Assert.IsNotNull(result.Continuation, "Natural Double Kick must execute in this public ordered turn.");
            result = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
        }
        throw new AssertFailedException("Double Kick did not execute within one bounded public turn.");
    }

    private static SourceKernelResult Find(SourceBattleFrame frame, int actor, int target,
        Func<SourceKernelResult, bool> predicate, string branch)
    {
        // Bounded discovery of actual kernel branches, not an original-source RNG oracle.
        for (uint seed = 0; seed < 1024; seed++)
        {
            var result = Resolve(frame with { Rng = new(seed) }, actor, target);
            if (predicate(result)) return result;
        }
        throw new AssertFailedException($"No observed {branch} in action-start seeds 0..1023.");
    }

    private static void AssertReadOnly(SourceBattleFrame state, ImmutableArray<RandomTrace> trace, Action projection)
    {
        var rng = state.Rng; long revision = state.Revision, sequence = state.EventSequence;
        string before = JsonSerializer.Serialize(state), random = JsonSerializer.Serialize(trace);
        projection(); projection();
        Assert.AreEqual(rng, state.Rng); Assert.AreEqual(revision, state.Revision); Assert.AreEqual(sequence, state.EventSequence);
        Assert.AreEqual(before, JsonSerializer.Serialize(state)); Assert.AreEqual(random, JsonSerializer.Serialize(trace));
    }

    private static void AssertDispatches(ImmutableArray<SourceBattleEvent> events, int actor, int target, int expectedHits)
    {
        var hits = Hits(events); Assert.AreEqual(expectedHits, hits.Length); Assert.IsTrue(expectedHits is 1 or 2);
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        var summary = events.OfType<SourceMultiHitEvent>().Single();
        Assert.AreEqual(Kick, used.Move); Assert.AreEqual(actor, used.Actor.Value); Assert.AreEqual(target, used.Target.Value);
        Assert.AreEqual(Kick, attack.ChosenMove); Assert.AreEqual(Kick, attack.CurrentMove);
        Assert.AreEqual(Kick, pp.ChosenMove); Assert.AreEqual(Kick, pp.CurrentMove);
        Assert.AreEqual(actor, pp.Actor.Value); Assert.AreEqual(actor, pp.PpOwner.Value); Assert.AreEqual(target, pp.Target.Value);
        Assert.AreEqual(30, pp.Before); Assert.AreEqual(29, pp.After); Assert.AreEqual(1, pp.CommandCost);
        Assert.AreEqual(Kick, summary.Move); Assert.AreEqual(2, summary.PlannedHits); Assert.AreEqual(expectedHits, summary.ActualHits);
        Assert.IsTrue(attack.Sequence < used.Sequence && used.Sequence < pp.Sequence && pp.Sequence < hits[0].Sequence);
        Assert.IsTrue(hits[^1].Sequence < summary.Sequence);
        Contract.AssertDispatches(events, actor, target);
    }

    private static void AssertDenied(ImmutableArray<SourceBattleEvent> events, string branch) =>
        Contract.AssertDenied(events, branch,
            !events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value > 1 || e.Target.Value > 1));

    [TestMethod]
    public void NaturalLearnerUsesSourceFixedTwoMetadataAndActualSinglesBothDirections()
    {
        var move = Mechanics.Catalog.Moves[Kick]; Assert.AreEqual(24, move.SourceMoveId);
        Assert.AreEqual("EFFECT_DOUBLE_HIT", move.Effect); Assert.AreEqual("MOVE_TARGET_SELECTED", move.Target);
        Assert.AreEqual("TYPE_FIGHTING", move.Type); Assert.AreEqual(30, move.Power); Assert.AreEqual(100, move.Accuracy);
        Assert.AreEqual(30, move.Pp); Assert.AreEqual(0, move.SecondaryEffectChance); Assert.AreEqual(0, move.Priority);
        Assert.IsTrue(move.Flags.Contains("FLAG_MAKES_CONTACT")); Assert.IsTrue(move.Flags.Contains("FLAG_KINGS_ROCK_AFFECTED"));
        Assert.IsTrue(move.Flags.Contains("FLAG_PROTECT_AFFECTED")); Assert.IsTrue(move.Flags.Contains("FLAG_MIRROR_MOVE_AFFECTED"));
        Assert.IsTrue(Mechanics.Catalog.Species[new("pret:SPECIES_NIDORAN_M")].LevelUpMoves.Any(m => m.Move == Kick && m.Level == 12));
        for (int actor = 0; actor < 2; actor++)
        {
            var result = Resolve(Frame(Bodies(actor), single: true), actor, actor ^ 1);
            Assert.AreEqual(SourceBattleLayout.Singles, result.State.Layout);
            Assert.AreEqual(1, result.Trace.Count(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)));
            Assert.IsFalse(result.Trace.Any(t => t.Purpose.Contains("multi-hit-count", StringComparison.Ordinal)));
            AssertReadOnly(result.State, result.Trace, () => AssertDispatches(result.Events, actor, actor ^ 1, 2));
        }
    }

    [TestMethod]
    public void OrderedDoublesOwnBothOpponentsAndSelectedAllyFromEveryActorSlot()
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 3, actor ^ 2 })
        {
            var result = Ordered(Frame(Bodies(actor)), actor, target);
            Assert.AreEqual(1, result.Trace.Count(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)));
            Assert.IsFalse(result.Trace.Any(t => t.Purpose.Contains("multi-hit-count", StringComparison.Ordinal)));
            AssertReadOnly(result.State, result.Trace, () => AssertDispatches(result.Events, actor, target, 2));
        }
    }

    [TestMethod]
    public void SurvivingSubstituteAndBreakThenBodyOwnExactlyTwoCommittedOrdinals()
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (bool survives in new[] { true, false })
        {
            int target = actor ^ 1; var bodies = Bodies(actor);
            bodies[target] = bodies[target] with { CalledVolatile = new() { SubstituteHp = survives ? bodies[target].Pokemon.Stats.MaxHp / 4 : 1 } };
            var result = Resolve(Frame(bodies), actor, target);
            var substitutes = result.Events.OfType<SourceSubstituteDamagedEvent>().ToArray();
            Assert.AreEqual(survives ? 2 : 1, substitutes.Length);
            Assert.AreEqual(survives ? 0 : 1, result.Events.OfType<SourceHpChangedEvent>().Count());
            if (survives) Assert.IsTrue(substitutes[^1].HpAfter > 0);
            else
            {
                Assert.AreEqual(0, substitutes[0].HpAfter);
                Assert.IsTrue(substitutes[0].Sequence < result.Events.OfType<SourceHpChangedEvent>().Single().Sequence);
            }
            AssertReadOnly(result.State, result.Trace, () => AssertDispatches(result.Events, actor, target, 2));
        }
    }

    [TestMethod]
    public void FirstOrSecondTargetKoAndOneHpEndureNeverAnimateAnUncommittedKick()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1;
            int firstDamage = Resolve(Frame(Bodies(actor)), actor, target).Events.OfType<SourceHpChangedEvent>().First().Damage;
            Assert.IsTrue(firstDamage > 0);
            foreach (string branch in new[] { "first-hit-ko", "second-hit-ko", "endure" })
            {
                var bodies = Bodies(actor); int expectedHits = branch == "second-hit-ko" ? 2 : 1;
                bodies[target] = bodies[target] with { Enduring = branch == "endure",
                    Pokemon = bodies[target].Pokemon with { CurrentHp = expectedHits == 2 ? firstDamage + 1 : 1 } };
                var result = Resolve(Frame(bodies), actor, target);
                var last = result.Events.OfType<SourceHpChangedEvent>().Last();
                if (branch == "endure")
                {
                    Assert.AreEqual(1, last.HpBefore); Assert.AreEqual(1, last.HpAfter); Assert.AreEqual(0, last.Damage);
                    Assert.IsTrue(result.Events.OfType<SourceDamageAdjustedEvent>().Single().Endured);
                    Assert.AreEqual(0, result.Events.OfType<SourceFaintedEvent>().Count());
                }
                else
                {
                    Assert.AreEqual(0, last.HpAfter);
                    Assert.AreEqual(target, result.Events.OfType<SourceFaintedEvent>().Single().Side.Value);
                }
                AssertReadOnly(result.State, result.Trace, () => AssertDispatches(result.Events, actor, target, expectedHits));
            }
        }
    }

    [TestMethod]
    public void RoughSkinActorKoAfterEitherContactKeepsOnlyItsCommittedKickReceipts()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var healthy = Bodies(actor); healthy[target] = NaturalMon("SHARPEDO", 100, "FOCUS_ENERGY");
            Assert.AreEqual("ABILITY_ROUGH_SKIN", healthy[target].Pokemon.AbilityId);
            var full = Resolve(Frame(healthy), actor, target);
            var firstContact = full.Events.OfType<SourceAbilityDamageEvent>().First();
            int contactDamage = firstContact.HpBefore - firstContact.HpAfter; Assert.IsTrue(contactDamage > 0);
            foreach (int expectedHits in new[] { 1, 2 })
            {
                var bodies = Bodies(actor); bodies[target] = healthy[target];
                bodies[actor] = bodies[actor] with { Pokemon = bodies[actor].Pokemon with { CurrentHp = expectedHits == 1 ? 1 : contactDamage + 1 } };
                var result = Resolve(Frame(bodies), actor, target);
                var contacts = result.Events.OfType<SourceAbilityDamageEvent>().ToArray(); Assert.AreEqual(expectedHits, contacts.Length);
                Assert.AreEqual(target, contacts[^1].Holder.Value); Assert.AreEqual(actor, contacts[^1].Target.Value);
                Assert.AreEqual(0, contacts[^1].HpAfter);
                Assert.IsTrue(Hits(result.Events)[^1].Sequence < contacts[^1].Sequence);
                Assert.AreEqual(actor, result.Events.OfType<SourceFaintedEvent>().Single().Side.Value);
                AssertReadOnly(result.State, result.Trace, () => AssertDispatches(result.Events, actor, target, expectedHits));
            }
        }
    }

    [TestMethod]
    public void EffectSporeSleepAfterFirstContactTruncatesTheSecondKickWithoutAMiss()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(actor); bodies[target] = NaturalMon("SHROOMISH", 100, "GROWTH");
            Assert.AreEqual("ABILITY_EFFECT_SPORE", bodies[target].Pokemon.AbilityId);
            var result = Find(Frame(bodies), actor, target, r => WildBattleStatus.IsSleeping(r.State.Slots[actor].Body!.Status) &&
                r.Events.OfType<SourceMultiHitEvent>().Single().ActualHits == 1, "first-contact Effect Spore sleep");
            var sleep = result.Events.OfType<SourceStatusChangedEvent>().Single(e => e.Side.Value == actor && WildBattleStatus.IsSleeping(e.After));
            Assert.AreEqual("effect-spore", sleep.Reason); Assert.IsTrue(Hits(result.Events).Single().Sequence < sleep.Sequence);
            Assert.AreEqual(0, result.Events.OfType<SourceMoveMissedEvent>().Count());
            Assert.AreEqual(1, result.Trace.Count(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)));
            AssertReadOnly(result.State, result.Trace, () => AssertDispatches(result.Events, actor, target, 1));
        }
    }

    [TestMethod]
    public void FocusBandZeroLossStillCommitsBothKickOrdinals()
    {
        var bodies = Bodies(); bodies[1] = bodies[1] with { HeldItem = "ITEM_FOCUS_BAND", Pokemon = bodies[1].Pokemon with { CurrentHp = 1 } };
        var result = Find(Frame(bodies), 0, 1, r => r.Events.OfType<SourceMultiHitEvent>().Single().ActualHits == 2 &&
            r.Events.OfType<SourceHpChangedEvent>().All(h => h.HpBefore == 1 && h.HpAfter == 1 && h.Damage == 0), "two zero-loss Focus Band hits");
        Assert.AreEqual(2, result.Events.OfType<SourceDamageAdjustedEvent>().Count(e => e.HungOn));
        AssertReadOnly(result.State, result.Trace, () => AssertDispatches(result.Events, 0, 1, 2));
    }

    [TestMethod]
    public void ProtectRealAccuracyMissGhostAndWonderGuardImmunityNeverScheduleAKick()
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (string branch in new[] { "protect", "miss", "ghost", "wonder-guard" })
        {
            int target = actor ^ 1; var bodies = Bodies(actor);
            if (branch == "protect") bodies[target] = bodies[target] with { Protected = true };
            if (branch == "miss") bodies[actor] = bodies[actor] with { AccuracyStage = 0 };
            if (branch == "ghost") bodies[target] = NaturalMon("GASTLY", 100, "LICK");
            if (branch == "wonder-guard") bodies[target] = NaturalMon("SHEDINJA", 100, "HARDEN");
            var frame = Frame(bodies);
            var result = branch == "miss" ? Find(frame, actor, target, r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "real accuracy miss")
                : Resolve(frame, actor, target);
            Assert.AreEqual(0, Hits(result.Events).Length);
            if (branch == "protect") Assert.AreEqual("protected", result.Events.OfType<SourceMoveFailedEvent>().Single().Reason);
            if (branch == "ghost") Assert.IsTrue(result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
            if (branch == "wonder-guard") Assert.IsTrue(result.Events.OfType<SourceAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_WONDER_GUARD"));
            if (branch is "ghost" or "wonder-guard") Assert.AreEqual(0, result.Events.OfType<SourceMultiHitEvent>().Single().ActualHits);
            else Assert.AreEqual(0, result.Events.OfType<SourceMultiHitEvent>().Count());
            AssertReadOnly(result.State, result.Trace, () => AssertDenied(result.Events, branch));
        }
    }

    [TestMethod]
    public void MissingOrContradictoryCommittedContextAndWrongFixedPlanFailClosed()
    {
        var result = Resolve(Frame(Bodies())); var good = result.Events;
        var summary = good.OfType<SourceMultiHitEvent>().Single(); int summaryIndex = good.IndexOf(summary);
        var pp = good.OfType<SourcePpCommandEvent>().Single(); int ppIndex = good.IndexOf(pp);
        var attack = good.OfType<SourceAttackStringEvent>().Single(); int attackIndex = good.IndexOf(attack);
        var used = good.OfType<SourceMoveUsedEvent>().Single(); int usedIndex = good.IndexOf(used);
        var invalid = new List<ImmutableArray<SourceBattleEvent>> {
            good.RemoveAt(summaryIndex), Number(good.Add(summary)), good.Remove(Hits(good)[^1]),
            good.SetItem(summaryIndex, summary with { ActualHits = 1 }), good.SetItem(summaryIndex, summary with { Target = new(0) }),
            Number(good.RemoveAt(summaryIndex).Insert(good.IndexOf(Hits(good)[0]), summary)),
            good.RemoveAt(ppIndex), Number(good.Insert(ppIndex, pp)),
            Number(good.RemoveAt(ppIndex).Insert(good.IndexOf(Hits(good)[0]) + 1, pp)),
            good.SetItem(ppIndex, pp with { PpOwner = new(1) }), good.SetItem(ppIndex, pp with { ChosenMove = Id("METRONOME") }),
            good.SetItem(ppIndex, pp with { After = pp.Before }), good.SetItem(ppIndex, pp with { PpSlot = 1 }),
            good.SetItem(attackIndex, attack with { ChosenMove = Id("SLEEP_TALK") }), good.RemoveAt(attackIndex),
            good.SetItem(usedIndex, used with { Actor = new(2) }), good.SetItem(usedIndex, used with { Target = used.Actor }),
            good.SetItem(good.IndexOf(Hits(good)[1]), Hits(good)[1] with { Sequence = Hits(good)[0].Sequence })
        };
        foreach (int planned in new[] { 0, 1, 3, 4, 5, 6 }) invalid.Add(good.SetItem(summaryIndex, summary with { PlannedHits = planned }));
        SourceBattleEvent[] blockers = [
            new SourceMoveMissedEvent(0, used.Turn, used.Actor, used.Target, Kick),
            new SourceMoveFailedEvent(0, used.Turn, used.Actor, used.Target, Kick, "protected"),
            new SourceAbilityPreventedEvent(0, used.Turn, used.Actor, used.Target, Kick, "ABILITY_WONDER_GUARD"),
            new SourceEffectivenessEvent(0, used.Turn, used.Actor, used.Target, Kick, WildBattleEffectiveness.NoEffect)
        ];
        foreach (var blocker in blockers) invalid.Add(Number(good.Insert(ppIndex + 1, blocker)));
        AssertReadOnly(result.State, result.Trace, () => {
            foreach (var events in invalid) AssertDenied(events, "malformed fixed-two invocation");
            AssertDispatches(good, 0, 1, 2);
        });
    }

    [TestMethod]
    public void IllegalSelfTargetFailsBeforeAnyReceiptOrRandomDrawInSinglesAndDoubles()
    {
        foreach (bool single in new[] { true, false })
        for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var frame = Frame(Bodies(actor), single: single); string before = JsonSerializer.Serialize(frame);
            var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
                new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(actor)));
            Assert.IsFalse(result.Succeeded); Assert.AreEqual("source_kernel_target_mode", result.Fault);
            Assert.IsTrue(result.Events.IsEmpty); Assert.IsTrue(result.Trace.IsEmpty);
            Assert.AreEqual(before, JsonSerializer.Serialize(result.State));
            Assert.AreEqual(before, JsonSerializer.Serialize(frame));
        }
    }

    [TestMethod]
    public void SceneRulesPhaseCursorAndCatalogRendererGatesRemainExplicit()
    {
        var result = Resolve(Frame(Bodies(), single: true)); var events = result.Events; int index = events.IndexOf(Hits(events)[0]);
        var single = Single(events, index); var twice = Double(events, index);
        AssertReadOnly(result.State, result.Trace, () => {
            Assert.IsNotNull(AnimationDispatchFixture.AtResult(single, true, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtResult(single, false, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtResult(single, true, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtResult(single with { Phase = CampaignBattlePhase.ChooseMove }, true, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtResult(twice, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtResult(twice with { Stage = CampaignDoubleBattleStage.Finished }, SourceBattleScene.On).Dispatch);
            foreach (int cursor in new[] { -1, events.Length })
            {
                Assert.IsNull(AnimationDispatchFixture.AtResult(single with { EventIndex = cursor }, true, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtResult(twice with { EventIndex = cursor }, SourceBattleScene.On).Dispatch);
            }
            foreach (IReadOnlyDictionary<MoveId, PokemonMoveDefinition>? moves in new IReadOnlyDictionary<MoveId, PokemonMoveDefinition>?[] {
                null, new Dictionary<MoveId, PokemonMoveDefinition>(),
                new Dictionary<MoveId, PokemonMoveDefinition> { [Kick] = Mechanics.Catalog.Moves[Kick] with { SourceMoveId = 25 } }
            })
            {
                Assert.IsNull(AnimationDispatchFixture.AtResult(single, true, SourceBattleScene.On, moves).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtResult(twice, SourceBattleScene.On, moves).Dispatch);
            }
            Assert.IsNull(CampaignMoveAnimationPresentation.AtResult(single, true, SourceBattleScene.On, Mechanics.Catalog.Moves, (_, _) => false).Dispatch);
            Assert.IsNull(CampaignMoveAnimationPresentation.AtResult(twice, SourceBattleScene.On, Mechanics.Catalog.Moves, (_, _) => false).Dispatch);
        });
    }

    [TestMethod]
    public void ReplayingBothHitProjectionsPreservesThePublicContinuationAndRequiresNoExtraAcknowledgement()
    {
        foreach (bool single in new[] { true, false })
        {
            var result = Ordered(Frame(Bodies(), single: single), 0, 1);
            Assert.IsNotNull(result.Continuation); var continuation = result.Continuation.Value;
            string before = JsonSerializer.Serialize(result);
            var baselineNext = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, continuation);
            Assert.IsTrue(baselineNext.Succeeded, baselineNext.Fault);
            AssertReadOnly(result.State, result.Trace, () => AssertDispatches(result.Events, 0, 1, 2));
            Assert.AreEqual(continuation, result.Continuation.Value);
            Assert.AreEqual(before, JsonSerializer.Serialize(result), "Projection cannot consume a continuation or add an ACK transaction.");
            var afterProjection = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, continuation);
            Assert.IsTrue(afterProjection.Succeeded, afterProjection.Fault);
            Assert.AreEqual(JsonSerializer.Serialize(baselineNext), JsonSerializer.Serialize(afterProjection));
            Assert.IsFalse(afterProjection.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Move == Kick), "Advancing once must not replay the committed Double Kick.");
            Assert.AreEqual(29, afterProjection.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp);
        }
    }
}

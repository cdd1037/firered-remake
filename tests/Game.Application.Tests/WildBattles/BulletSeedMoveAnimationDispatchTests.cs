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

// Natural source learner: Sunflora level 25 (level_up_learnsets.h:2703).
// Bullet Seed is the unchanged ordinary 2..5-hit source move (battle_moves.h:4306).
// Controlled action-start RNG, HP and volatile states are fixtures, not route evidence.
[TestClass, TestCategory("Game")]
public sealed class BulletSeedMoveAnimationDispatchTests
{
    private static readonly DirectHitAnimationContract Contract = new("bullet-seed", 331, Id("BULLET_SEED"), 30, DirectHitOwnership.EachHit, 2, 5);
    private static readonly MoveId BulletSeed = Id("BULLET_SEED");
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) => Contract.Double(events, index);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) => Contract.Single(events, index);

    private static void AssertDispatches(ImmutableArray<SourceBattleEvent> events, int actor, int target)
    {
        var hits = Hits(events); Assert.IsTrue(hits.Length > 0);
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        var summary = events.OfType<SourceMultiHitEvent>().Single();
        Assert.AreEqual(BulletSeed, used.Move); Assert.AreEqual(BulletSeed, summary.Move);
        Assert.AreEqual(BulletSeed, attack.ChosenMove); Assert.AreEqual(BulletSeed, attack.CurrentMove);
        Assert.AreEqual(BulletSeed, pp.ChosenMove); Assert.AreEqual(BulletSeed, pp.CurrentMove);
        Assert.AreEqual(actor, pp.PpOwner.Value); Assert.AreEqual(actor, pp.Actor.Value); Assert.AreEqual(target, pp.Target.Value);
        Assert.AreEqual(30, pp.Before); Assert.AreEqual(29, pp.After); Assert.AreEqual(1, pp.CommandCost);
        Assert.IsTrue(attack.Sequence < used.Sequence && used.Sequence < pp.Sequence && pp.Sequence < hits[0].Sequence);
        Assert.IsTrue(summary.PlannedHits is >= 2 and <= 5);
        Assert.IsTrue(summary.ActualHits >= 1 && summary.ActualHits <= summary.PlannedHits);
        Assert.AreEqual(hits.Length, summary.ActualHits); Assert.IsTrue(hits[^1].Sequence < summary.Sequence);
        Contract.AssertDispatches(events, actor, target);
    }
    private static void AssertDenied(ImmutableArray<SourceBattleEvent> events, string branch) =>
        Contract.AssertDenied(events, branch,
            !events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value > 1 || e.Target.Value > 1));
    private static void AssertReadOnly(SourceBattleFrame state, ImmutableArray<RandomTrace> trace, Action projection)
    {
        var rng = state.Rng; long revision = state.Revision, sequence = state.EventSequence;
        string frame = JsonSerializer.Serialize(state), random = JsonSerializer.Serialize(trace);
        projection(); projection();
        Assert.AreEqual(rng, state.Rng); Assert.AreEqual(revision, state.Revision); Assert.AreEqual(sequence, state.EventSequence);
        Assert.AreEqual(frame, JsonSerializer.Serialize(state)); Assert.AreEqual(random, JsonSerializer.Serialize(trace));
    }

    private static WildBattleCombatant[] Bodies(int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL") with { SpeedStage = 0 }).ToArray();
        bodies[actor] = NaturalMon("SUNFLORA", 25, "BULLET_SEED") with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }

    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor, int target)
    {
        Assert.IsNull(SourceBattleIdentityRules.Validate(frame));
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty); return result;
    }
    private static SourceFourActionResult Ordered(SourceBattleFrame frame, int actor, int target)
    {
        var choices = frame.Slots.Select(c => new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, 0,
            c.Slot.Value == actor ? new BattlerSlot(target) : c.Slot)).ToImmutableArray();
        var result = SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision, choices);
        for (int step = 0; step < 12; step++)
        {
            Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            if (result.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == actor && e.Move == BulletSeed)) return result;
            Assert.IsNotNull(result.Continuation, "Bullet Seed must execute within this public ordered turn.");
            result = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
        }
        throw new InvalidOperationException("The natural Bullet Seed did not execute in one bounded public turn.");
    }
    private static JsonElement[] SourceVectors()
    {
        using var source = JsonDocument.Parse(File.ReadAllBytes(TestAssets.GeneratedFile("tests/Game.Battle.Tests/Moves/source-vectors.json", "python tests/Game.Battle.Tests/Moves/source_probe.py content/raw/pokefirered/037335f4 tests/Game.Battle.Tests/Moves --fixtures-only")));
        Assert.AreEqual(PokemonFactory.SourceCommit, source.RootElement.GetProperty("sourceCommit").GetString());
        var vectors = source.RootElement.GetProperty("multihit").EnumerateArray().Select(v => v.Clone()).ToArray();
        Assert.IsTrue(vectors.Length > 0);
        CollectionAssert.AreEquivalent(new[] { 2, 3, 4, 5 }, vectors.Select(v => v.GetProperty("hits").GetInt32()).Distinct().ToArray());
        return vectors;
    }

    [TestMethod]
    public void NaturalBulletSeedUsesUnchangedSourceMetadataAndOrderedTurnsAtEverySlotAndSelectedAlly()
    {
        var move = Mechanics.Catalog.Moves[BulletSeed];
        Assert.AreEqual("EFFECT_MULTI_HIT", move.Effect); Assert.AreEqual("MOVE_TARGET_SELECTED", move.Target);
        Assert.AreEqual("TYPE_GRASS", move.Type); Assert.AreEqual(10, move.Power); Assert.AreEqual(100, move.Accuracy);
        Assert.AreEqual(30, move.Pp); Assert.AreEqual(0, move.SecondaryEffectChance);
        Assert.IsFalse(move.Flags.Contains("FLAG_MAKES_CONTACT")); Assert.IsTrue(move.Flags.Contains("FLAG_KINGS_ROCK_AFFECTED"));
        Assert.IsTrue(move.Flags.Contains("FLAG_PROTECT_AFFECTED"));
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        {
            var result = Ordered(Frame(Bodies(actor), 31), actor, target);
            Assert.AreEqual(1, result.Trace.Count(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)));
            Assert.AreEqual(1, result.Trace.Count(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal)));
            AssertReadOnly(result.State, result.Trace, () => AssertDispatches(result.Events, actor, target));
        }
    }

    [TestMethod]
    public void EveryOrdinarySourceHitCountRetainsIndependentRngAndCommittedOrdinalsAtEverySlot()
    {
        // Reuse one independent source vector for each count; do not generate a product-output golden.
        foreach (var vector in SourceVectors().GroupBy(v => v.GetProperty("hits").GetInt32()).Select(g => g.First()))
        for (int actor = 0; actor < 4; actor++)
        {
            var result = Resolve(Frame(Bodies(actor), vector.GetProperty("seed").GetUInt32()), actor, actor ^ 1);
            var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
            Assert.AreEqual(vector.GetProperty("hits").GetInt32(), summary.PlannedHits);
            Assert.AreEqual(summary.PlannedHits, summary.ActualHits);
            Assert.AreEqual(vector.GetProperty("draws").GetInt32(), result.Trace.Length);
            Assert.AreEqual(vector.GetProperty("finalSeed").GetUInt32(), result.State.Rng.Seed);
            Assert.AreEqual(1, result.Trace.Count(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)));
            AssertReadOnly(result.State, result.Trace, () => AssertDispatches(result.Events, actor, actor ^ 1));
        }
    }

    [TestMethod]
    public void SurvivingAndBrokenSubstitutesKeepTheSameCommittedHitOrdinalsAtEverySlot()
    {
        uint seed = SourceVectors().First(v => v.GetProperty("hits").GetInt32() == 5).GetProperty("seed").GetUInt32();
        for (int actor = 0; actor < 4; actor++)
        foreach (bool survives in new[] { true, false })
        {
            int target = actor ^ 1; var bodies = Bodies(actor);
            int substituteHp = survives ? bodies[target].Pokemon.Stats.MaxHp / 4 : 1;
            bodies[target] = bodies[target] with { CalledVolatile = new() { SubstituteHp = substituteHp } };
            var result = Resolve(Frame(bodies, seed), actor, target);
            var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
            Assert.AreEqual(5, summary.PlannedHits); Assert.AreEqual(5, summary.ActualHits);
            var substitutes = result.Events.OfType<SourceSubstituteDamagedEvent>().ToArray();
            if (survives)
            {
                Assert.AreEqual(5, substitutes.Length); Assert.IsTrue(substitutes[^1].HpAfter > 0);
                Assert.AreEqual(0, result.Events.OfType<SourceHpChangedEvent>().Count());
            }
            else
            {
                Assert.AreEqual(1, substitutes.Length); Assert.AreEqual(0, substitutes[0].HpAfter);
                Assert.AreEqual(4, result.Events.OfType<SourceHpChangedEvent>().Count());
                Assert.IsTrue(substitutes[0].Sequence < result.Events.OfType<SourceHpChangedEvent>().First().Sequence);
            }
            AssertReadOnly(result.State, result.Trace, () => AssertDispatches(result.Events, actor, target));
        }
    }

    [TestMethod]
    public void EarlyKoAndEndureTruncateOnlyUncommittedHitsAtEverySlot()
    {
        uint seed = SourceVectors().First(v => v.GetProperty("hits").GetInt32() == 5).GetProperty("seed").GetUInt32();
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1;
            var full = Resolve(Frame(Bodies(actor), seed), actor, target);
            int firstDamage = full.Events.OfType<SourceHpChangedEvent>().First().Damage;
            Assert.IsTrue(firstDamage > 0);
            foreach (string branch in new[] { "first-hit-ko", "second-hit-ko", "endure" })
            {
                var bodies = Bodies(actor); int hp = branch == "second-hit-ko" ? firstDamage + 1 : 1;
                bodies[target] = bodies[target] with { Enduring = branch == "endure", Pokemon = bodies[target].Pokemon with { CurrentHp = hp } };
                var result = Resolve(Frame(bodies, seed), actor, target);
                var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
                Assert.AreEqual(5, summary.PlannedHits); Assert.AreEqual(branch == "second-hit-ko" ? 2 : 1, summary.ActualHits);
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
                AssertReadOnly(result.State, result.Trace, () => AssertDispatches(result.Events, actor, target));
            }
        }
    }

    [TestMethod]
    public void ProtectAndRealAccuracyMissNeverScheduleBulletSeedAtAnySlot()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(actor);
            bodies[target] = bodies[target] with { Protected = true };
            var blocked = Resolve(Frame(bodies, 31), actor, target);
            Assert.AreEqual(0, Hits(blocked.Events).Length);
            Assert.AreEqual("protected", blocked.Events.OfType<SourceMoveFailedEvent>().Single().Reason);
            Assert.AreEqual(0, blocked.Events.OfType<SourceMultiHitEvent>().Count());
            AssertReadOnly(blocked.State, blocked.Trace, () => AssertDenied(blocked.Events, "Protect"));
            bodies = Bodies(actor); bodies[actor] = bodies[actor] with { AccuracyStage = 0 };
            var entered = Frame(bodies, 0); SourceKernelResult? missed = null;
            // Bounded branch discovery; failure to observe a real miss fails the test.
            for (uint seed = 0; seed < 1024; seed++)
            {
                var result = Resolve(entered with { Rng = new(seed) }, actor, target);
                if (result.Events.OfType<SourceMoveMissedEvent>().Any()) { missed = result; break; }
            }
            Assert.IsNotNull(missed, "No real Bullet Seed accuracy miss in seeds 0..1023.");
            Assert.AreEqual(0, Hits(missed.Events).Length);
            Assert.AreEqual(0, missed.Events.OfType<SourceMultiHitEvent>().Count());
            AssertReadOnly(missed.State, missed.Trace, () => AssertDenied(missed.Events, "accuracy miss"));
        }
    }

    [TestMethod]
    public void IncompleteOrMalformedCommittedGroupsFailClosedAndSceneCursorGatesRemainIntact()
    {
        var result = Resolve(Frame(Bodies(), 6), 0, 1); var events = result.Events;
        var summary = events.OfType<SourceMultiHitEvent>().Single(); int summaryIndex = events.IndexOf(summary);
        var pp = events.OfType<SourcePpCommandEvent>().Single(); int ppIndex = events.IndexOf(pp);
        ImmutableArray<SourceBattleEvent>[] malformed = [
            events.RemoveAt(summaryIndex), events.Remove(Hits(events)[^1]),
            events.SetItem(summaryIndex, summary with { PlannedHits = 1 }),
            events.SetItem(summaryIndex, summary with { PlannedHits = 6 }),
            events.SetItem(summaryIndex, summary with { ActualHits = summary.ActualHits - 1 }),
            events.SetItem(ppIndex, pp with { PpOwner = new(1) }),
            events.SetItem(ppIndex, pp with { ChosenMove = Id("METRONOME") })
        ];
        AssertReadOnly(result.State, result.Trace, () => {
            foreach (var invalid in malformed) AssertDenied(invalid, "incomplete or malformed Bullet Seed group");
            AssertDispatches(events, 0, 1);
            int index = events.IndexOf(Hits(events)[0]); var single = Single(events, index); var twice = Double(events, index);
            Assert.IsNotNull(AnimationDispatchFixture.AtResult(single, true, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtResult(single, false, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtResult(single, true, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtResult(single with { Phase = CampaignBattlePhase.ChooseMove }, true, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtResult(single with { EventIndex = -1 }, true, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtResult(single with { EventIndex = events.Length }, true, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtResult(twice, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtResult(twice with { Stage = CampaignDoubleBattleStage.Finished }, SourceBattleScene.On).Dispatch);
        });
    }
}

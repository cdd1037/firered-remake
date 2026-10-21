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

// Source-backed admission regressions. Natural level-up moves and unchanged catalog;
// controlled action-start RNG/HP/volatile state are fixtures, not natural-route evidence.
[TestClass, TestCategory("Game")]
public sealed class ArcProjectileAnimationDispatchTests
{
    private static readonly (string Name, int Id, string Species, int Level, int Pp)[] Moves =
        [("PIN_MISSILE", 42, "BEEDRILL", 35, 20), ("ICICLE_SPEAR", 333, "SHELLDER", 8, 30)];

    // Independent source identities and receipt ownership, not production metadata.
    private static DirectHitAnimationContract Contract(int id) => id switch
    {
        42 => new("arc-projectiles", 42, Id("PIN_MISSILE"), 20, DirectHitOwnership.EachHit, 2, 5),
        333 => new("arc-projectiles", 333, Id("ICICLE_SPEAR"), 30, DirectHitOwnership.EachHit, 2, 5),
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };

    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) => Contract(42).Double(events, index);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) => Contract(42).Single(events, index);
    private static CampaignMoveAnimationDecision AtText(ImmutableArray<SourceBattleEvent> events, int index) =>
        AnimationDispatchFixture.AtAttackText(Double(events, index), SourceBattleScene.On);
    private static CampaignMoveAnimationDecision AtHit(ImmutableArray<SourceBattleEvent> events, int index) =>
        AnimationDispatchFixture.AtResult(Double(events, index), SourceBattleScene.On);

    private static void AssertDispatches(ImmutableArray<SourceBattleEvent> events, int id, int actor = 0, int target = 1)
    {
        var hits = Hits(events); Assert.IsTrue(hits.Length > 0);
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        Assert.AreEqual(Id(Moves.Single(m => m.Id == id).Name), used.Move);
        Assert.AreEqual(used.Move, attack.ChosenMove); Assert.AreEqual(used.Move, attack.CurrentMove);
        Assert.AreEqual(used.Move, pp.ChosenMove); Assert.AreEqual(used.Move, pp.CurrentMove);
        Assert.AreEqual(actor, pp.PpOwner.Value); Assert.AreEqual(actor, pp.Actor.Value); Assert.AreEqual(target, pp.Target.Value);
        Assert.IsTrue(attack.Sequence < used.Sequence && used.Sequence < pp.Sequence && pp.Sequence < hits[0].Sequence);
        var summary = events.OfType<SourceMultiHitEvent>().Single();
        Assert.AreEqual(hits.Length, summary.ActualHits);
        Assert.IsTrue(summary.PlannedHits is >= 2 and <= 5);
        Assert.IsTrue(summary.ActualHits >= 1 && summary.ActualHits <= summary.PlannedHits);
        Assert.IsTrue(hits[^1].Sequence < summary.Sequence);
        Contract(id).AssertDispatches(events, actor, target);
    }
    private static void AssertDenied(ImmutableArray<SourceBattleEvent> events, string branch) =>
        Contract(42).AssertDenied(events, branch,
            singles: !events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value > 1 || e.Target.Value > 1));
    private static void AssertReadOnly(SourceBattleFrame state, ImmutableArray<RandomTrace> trace, Action projection)
    {
        var rng = state.Rng; long revision = state.Revision, sequence = state.EventSequence;
        string frame = JsonSerializer.Serialize(state), random = JsonSerializer.Serialize(trace);
        projection(); projection();
        Assert.AreEqual(rng, state.Rng); Assert.AreEqual(revision, state.Revision); Assert.AreEqual(sequence, state.EventSequence);
        Assert.AreEqual(frame, JsonSerializer.Serialize(state)); Assert.AreEqual(random, JsonSerializer.Serialize(trace));
    }

    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var move = Moves.Single(m => m.Id == id);
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL") with { SpeedStage = 0 }).ToArray();
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
    [DataRow(42)]
    [DataRow(333)]
    public void NaturalArcProjectilesUseOrderedTurnsAtEveryPositionAndSelectedAllies(int id)
    {
        var source = Moves.Single(m => m.Id == id); var definition = Mechanics.Catalog.Moves[Id(source.Name)];
        Assert.AreEqual("EFFECT_MULTI_HIT", definition.Effect); Assert.AreEqual("MOVE_TARGET_SELECTED", definition.Target);
        Assert.AreEqual(id == 42 ? "TYPE_BUG" : "TYPE_ICE", definition.Type);
        Assert.AreEqual(id == 42 ? 14 : 10, definition.Power); Assert.AreEqual(id == 42 ? 85 : 100, definition.Accuracy);
        Assert.IsFalse(definition.Flags.Contains("FLAG_MAKES_CONTACT"));
        Assert.IsTrue(definition.Flags.Contains("FLAG_KINGS_ROCK_AFFECTED")); Assert.AreEqual(0, definition.SecondaryEffectChance);
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        {
            var result = Ordered(Frame(Bodies(id, actor), 31), actor, target);
            var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreEqual(source.Pp, pp.Before); Assert.AreEqual(source.Pp - 1, pp.After); Assert.AreEqual(1, pp.CommandCost);
            Assert.AreEqual(1, result.Trace.Count(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)));
            Assert.AreEqual(1, result.Trace.Count(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal)),
                "The zero native effect still reaches one source Random call after the hit loop.");
            Assert.AreEqual(0, result.Events.OfType<SourceStatusChangedEvent>().Count());
            AssertReadOnly(result.State, result.Trace, () => AssertDispatches(result.Events, id, actor, target));
        }
    }

    [TestMethod]
    [DataRow(42)]
    [DataRow(333)]
    public void BothArcMovesRetainAllSourceCountAndRngVectorsAtEveryPosition(int id)
    {
        using var source = JsonDocument.Parse(File.ReadAllBytes(TestAssets.GeneratedFile("tests/Game.Battle.Tests/Moves/source-vectors.json", "python tests/Game.Battle.Tests/Moves/source_probe.py content/raw/pokefirered/037335f4 tests/Game.Battle.Tests/Moves --fixtures-only")));
        Assert.AreEqual(PokemonFactory.SourceCommit, source.RootElement.GetProperty("sourceCommit").GetString());
        var vectors = source.RootElement.GetProperty("multihit"); Assert.AreEqual(48, vectors.GetArrayLength());
        HashSet<int> plans = [];
        foreach (var vector in vectors.EnumerateArray())
        for (int actor = 0; actor < 4; actor++)
        {
            var result = Resolve(Frame(Bodies(id, actor), vector.GetProperty("seed").GetUInt32()), actor, actor ^ 1);
            var summary = result.Events.OfType<SourceMultiHitEvent>().Single(); plans.Add(summary.PlannedHits);
            Assert.AreEqual(vector.GetProperty("hits").GetInt32(), summary.PlannedHits);
            Assert.AreEqual(summary.PlannedHits, summary.ActualHits);
            Assert.AreEqual(vector.GetProperty("draws").GetInt32(), result.Trace.Length);
            Assert.AreEqual(vector.GetProperty("finalSeed").GetUInt32(), result.State.Rng.Seed);
            Assert.AreEqual(1, result.Trace.Count(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)),
                "Neither ordinary multihit move rechecks accuracy for later hits.");
            AssertDispatches(result.Events, id, actor, actor ^ 1);
        }
        CollectionAssert.AreEquivalent(new[] { 2, 3, 4, 5 }, plans.ToArray());
    }

    [TestMethod]
    [DataRow(42)]
    [DataRow(333)]
    public void SubstituteKoEndureAndFocusBandUseOnlyCommittedArcReceipts(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1;
            foreach (string branch in new[] { "substitute", "ko", "endure" })
            {
                var bodies = Bodies(id, actor);
                bodies[target] = branch == "substitute" ? bodies[target] with { CalledVolatile = new() { SubstituteHp = 1 } }
                    : bodies[target] with { Enduring = branch == "endure", Pokemon = bodies[target].Pokemon with { CurrentHp = 1 } };
                var result = Resolve(Frame(bodies, 31), actor, target);
                var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
                if (branch == "substitute")
                {
                    var substitute = result.Events.OfType<SourceSubstituteDamagedEvent>().Single();
                    Assert.AreEqual(1, substitute.HpBefore); Assert.AreEqual(0, substitute.HpAfter);
                    Assert.IsTrue(result.Events.OfType<SourceHpChangedEvent>().Any());
                    Assert.AreEqual(summary.PlannedHits, summary.ActualHits);
                }
                else
                {
                    var hp = result.Events.OfType<SourceHpChangedEvent>().Single(); Assert.AreEqual(1, summary.ActualHits);
                    Assert.AreEqual(branch == "ko" ? 0 : 1, hp.HpAfter);
                    if (branch == "ko") Assert.AreEqual(target, result.Events.OfType<SourceFaintedEvent>().Single().Side.Value);
                    else
                    {
                        Assert.AreEqual(1, hp.HpBefore); Assert.AreEqual(0, hp.Damage);
                        Assert.IsTrue(result.Events.OfType<SourceDamageAdjustedEvent>().Single().Endured);
                    }
                }
                AssertDispatches(result.Events, id, actor, target);
            }
            var band = Bodies(id, actor);
            band[target] = band[target] with { HeldItem = "ITEM_FOCUS_BAND", Pokemon = band[target].Pokemon with { CurrentHp = 1 } };
            var survived = Find(band, actor, target, r => {
                var summary = r.Events.OfType<SourceMultiHitEvent>().Single();
                return summary.ActualHits == summary.PlannedHits && r.Events.OfType<SourceHpChangedEvent>().All(h => h.HpAfter == 1);
            }, "Focus Band survives every arc hit").Result;
            Assert.IsTrue(survived.Events.OfType<SourceHpChangedEvent>().All(h => h.HpBefore == 1 && h.HpAfter == 1 && h.Damage == 0));
            AssertDispatches(survived.Events, id, actor, target);
        }
    }

    [TestMethod]
    [DataRow(42)]
    [DataRow(333)]
    public void ProtectMissAndWonderGuardRejectWhileOrdinaryGhostStillTakesArcHits(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor);
            bodies[target] = bodies[target] with { Protected = true };
            var blocked = Resolve(Frame(bodies, 31), actor, target);
            Assert.AreEqual(0, Hits(blocked.Events).Length); AssertDenied(blocked.Events, "Protect");
            bodies = Bodies(id, actor); bodies[actor] = bodies[actor] with { AccuracyStage = 0 };
            var missed = Find(bodies, actor, target, r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "ordinary accuracy miss").Result;
            Assert.AreEqual(0, Hits(missed.Events).Length); AssertDenied(missed.Events, "accuracy miss");
            bodies = Bodies(id, actor); bodies[target] = NaturalMon("SHEDINJA", 100, "HARDEN");
            var immune = Resolve(Frame(bodies, 31), actor, target);
            Assert.AreEqual(0, Hits(immune.Events).Length);
            Assert.AreEqual(0, immune.Events.OfType<SourceMultiHitEvent>().Single().ActualHits);
            Assert.IsTrue(immune.Events.OfType<SourceAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_WONDER_GUARD"));
            AssertDenied(immune.Events, "Wonder Guard");
            bodies = Bodies(id, actor); bodies[target] = NaturalMon("GASTLY", 100, "LICK");
            var ghost = Resolve(Frame(bodies, 31), actor, target);
            AssertDispatches(ghost.Events, id, actor, target);
        }
    }

    [TestMethod]
    [DataRow(42)]
    [DataRow(333)]
    public void NeitherArcMoveHasContactReactionsOrNativePoisonOrFreeze(int id)
    {
        foreach (var targetSource in new[] { (Species: "SHARPEDO", Move: "FOCUS_ENERGY", Ability: "ABILITY_ROUGH_SKIN"),
            (Species: "SHROOMISH", Move: "GROWTH", Ability: "ABILITY_EFFECT_SPORE") })
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor);
            bodies[actor] = bodies[actor] with { Pokemon = bodies[actor].Pokemon with { CurrentHp = 1 } };
            bodies[target] = NaturalMon(targetSource.Species, 100, targetSource.Move);
            Assert.AreEqual(targetSource.Ability, bodies[target].Pokemon.AbilityId);
            var result = Resolve(Frame(bodies, 31), actor, target);
            Assert.AreEqual(1, result.State.Slots[actor].Body!.Pokemon.CurrentHp);
            Assert.AreEqual(WildBattleStatus.None, result.State.Slots[actor].Body!.Status);
            Assert.AreEqual(WildBattleStatus.None, result.State.Slots[target].Body!.Status);
            Assert.AreEqual(0, result.Events.OfType<SourceAbilityDamageEvent>().Count());
            Assert.AreEqual(0, result.Events.OfType<SourceStatusChangedEvent>().Count());
            Assert.IsFalse(result.Trace.Any(t => t.Purpose.Contains("effect-spore", StringComparison.Ordinal)));
            var summary = result.Events.OfType<SourceMultiHitEvent>().Single(); Assert.AreEqual(summary.PlannedHits, summary.ActualHits);
            AssertDispatches(result.Events, id, actor, target);
        }
    }

    [TestMethod]
    [DataRow(42)]
    [DataRow(333)]
    public void OrderedKingsRockKeepsPerHitAndFinalItemPhasesWithoutExtraArcInvocations(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor);
            bodies[actor] = bodies[actor] with { HeldItem = "ITEM_KINGS_ROCK" };
            var entered = Frame(bodies, 0); SourceFourActionResult? found = null;
            for (uint seed = 0; seed < 128; seed++)
            {
                var result = Ordered(entered with { Rng = new(seed) }, actor, target);
                if (result.Events.OfType<SourceFlinchEvent>().Any(e => e.Side == new BattlerSlot(target))) { found = result; break; }
            }
            Assert.IsNotNull(found, "No ordered King's Rock flinch within source seeds 0..127.");
            var summary = found.Events.OfType<SourceMultiHitEvent>().Single();
            Assert.AreEqual(summary.PlannedHits, summary.ActualHits);
            Assert.AreEqual(summary.ActualHits + 1, found.Trace.Count(t => t.Purpose.EndsWith(".kings-rock", StringComparison.Ordinal)),
                "Ordinary multihit visits King's Rock after every hit and once at final move-end.");
            AssertReadOnly(found.State, found.Trace, () => AssertDispatches(found.Events, id, actor, target));
        }
    }
}

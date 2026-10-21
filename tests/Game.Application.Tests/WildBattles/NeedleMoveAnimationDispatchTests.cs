using static FireRed.Tests.Application.WildBattles.DirectHitAnimationFixture;
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

// Staged against the public Application projections and the public four-position
// kernel. The event-only fixtures are deliberately separate from natural content.
[TestClass, TestCategory("Game")]
public sealed class NeedleMoveAnimationDispatchTests
{
    private static DirectHitAnimationContract Contract(int id)
    {
        var move = Moves.Single(m => m.Id == id);
        return new("needle-moves", id, Id(move.Name), move.Pp,
            id == 40 ? DirectHitOwnership.AttackText : DirectHitOwnership.EachHit, 2, id == 131 ? 5 : 2);
    }
    private static readonly (string Name, int Id, string Species, int Level, int Pp)[] Moves =
        [("POISON_STING", 40, "WEEDLE", 1, 35), ("TWINEEDLE", 41, "BEEDRILL", 20, 20),
         ("SPIKE_CANNON", 131, "CLOYSTER", 43, 15)];

    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) => Contract(40).Double(events, index);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) => Contract(40).Single(events, index);
    private static CampaignMoveAnimationDecision AtText(ImmutableArray<SourceBattleEvent> events, int index) => Contract(40).AtText(events, index);
    private static CampaignMoveAnimationDecision AtHit(ImmutableArray<SourceBattleEvent> events, int index) => Contract(40).AtHit(events, index);

    private static void AssertDispatches(ImmutableArray<SourceBattleEvent> events, int id, int actor = 0, int target = 1) =>
        Contract(id).AssertDispatches(events, actor, target);
    private static void AssertDenied(ImmutableArray<SourceBattleEvent> events, string branch) =>
        Contract(40).AssertDenied(events, branch,
            !events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value > 1 || e.Target.Value > 1));
    private static ImmutableArray<SourceBattleEvent> Group(int id, int actor = 0, int? target = null, int planned = 2, int? actual = null) =>
        Contract(id).Group(actor, target, planned, actual);

    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var move = Moves.Single(m => m.Id == id);
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL") with { SpeedStage = 0 }).ToArray();
        bodies[actor] = NaturalMon(move.Species, move.Level, move.Name) with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }

    private static (uint Seed, SourceKernelResult Result) Find(WildBattleCombatant[] bodies,
        Func<SourceKernelResult, bool> predicate, string branch)
    {
        var entered = Frame(bodies, 0);
        // Bounded integration branch discovery, not an original-source golden.
        for (uint seed = 0; seed < 1024; seed++)
        {
            var result = Resolve(entered with { Rng = new(seed) });
            if (predicate(result)) return (seed, result);
        }
        throw new InvalidOperationException($"No observed {branch} in 1024 action-start seeds.");
    }

    [TestMethod]
    public void NaturalNeedleMovesUseActualOrderedTurnsAtEveryPositionAndSelectedAllies()
    {
        foreach (var move in Moves)
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        {
            var definition = Mechanics.Catalog.Moves[Id(move.Name)];
            Assert.IsFalse(definition.Flags.Contains("FLAG_MAKES_CONTACT"));
            Assert.AreEqual(move.Id == 131, definition.Flags.Contains("FLAG_KINGS_ROCK_AFFECTED"));
            Assert.AreEqual(move.Id == 40 ? 30 : move.Id == 41 ? 20 : 0, definition.SecondaryEffectChance);
            var result = Ordered(Frame(Bodies(move.Id, actor), 31), actor, target);
            var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreEqual(move.Pp, pp.Before); Assert.AreEqual(move.Pp - 1, pp.After); Assert.AreEqual(1, pp.CommandCost);
            AssertDispatches(result.Events, move.Id, actor, target);
        }
    }

    [TestMethod]
    public void EveryValidReceiptCountHasExactlyOneInvocationPerCommittedHit()
    {
        foreach (var move in Moves)
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        for (int planned = 2; planned <= (move.Id == 131 ? 5 : 2); planned++)
        for (int actual = 1; actual <= (move.Id == 40 ? 1 : planned); actual++)
            AssertDispatches(Group(move.Id, actor, target, planned, actual), move.Id, actor, target);
    }

    [TestMethod]
    public void SpikeCannonRetainsIndependentSourceCountAndRngVectorsAtAllFourPositions()
    {
        using var source = JsonDocument.Parse(File.ReadAllBytes(TestAssets.GeneratedFile("tests/Game.Battle.Tests/Moves/source-vectors.json", "python tests/Game.Battle.Tests/Moves/source_probe.py content/raw/pokefirered/037335f4 tests/Game.Battle.Tests/Moves --fixtures-only")));
        Assert.AreEqual(PokemonFactory.SourceCommit, source.RootElement.GetProperty("sourceCommit").GetString());
        var vectors = source.RootElement.GetProperty("multihit"); Assert.AreEqual(48, vectors.GetArrayLength());
        foreach (var vector in vectors.EnumerateArray())
        for (int actor = 0; actor < 4; actor++)
        {
            var result = Resolve(Frame(Bodies(131, actor), vector.GetProperty("seed").GetUInt32()), actor, actor ^ 1);
            var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
            Assert.AreEqual(vector.GetProperty("hits").GetInt32(), summary.PlannedHits);
            Assert.AreEqual(summary.PlannedHits, summary.ActualHits);
            Assert.AreEqual(vector.GetProperty("draws").GetInt32(), result.Trace.Length);
            Assert.AreEqual(vector.GetProperty("finalSeed").GetUInt32(), result.State.Rng.Seed);
            AssertDispatches(result.Events, 131, actor, actor ^ 1);
        }
    }

    [TestMethod]
    [DataRow(40)]
    [DataRow(41)]
    public void NaturalPoisonSecondaryIsAfterDamageAndAfterTwineedleSummary(int id)
    {
        WildBattleCombatant[] PoisonBodies(int actor)
        {
            var bodies = Bodies(id, actor); bodies[actor ^ 1] = NaturalMon("BLISSEY", 100, "DEFENSE_CURL"); return bodies;
        }
        var found = Find(PoisonBodies(0), r => r.Events.OfType<SourceStatusChangedEvent>().Any(e => e.Side.Value == 1 && e.After == WildBattleStatus.Poison), "natural poison secondary");
        for (int actor = 0; actor < 4; actor++)
        {
            var result = Resolve(Frame(PoisonBodies(actor), found.Seed), actor, actor ^ 1);
            var status = result.Events.OfType<SourceStatusChangedEvent>().Single(e => e.Side.Value == (actor ^ 1) && e.After == WildBattleStatus.Poison);
            Assert.AreEqual("move", status.Reason); Assert.IsTrue(Hits(result.Events)[^1].Sequence < status.Sequence);
            Assert.AreEqual(1, result.Trace.Count(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal)));
            if (id == 41)
            {
                var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
                Assert.AreEqual(2, summary.PlannedHits); Assert.AreEqual(2, summary.ActualHits);
                Assert.IsTrue(summary.Sequence < status.Sequence);
                Assert.IsFalse(result.Trace.Any(t => t.Purpose.Contains("multi-hit-count", StringComparison.Ordinal)));
            }
            AssertDispatches(result.Events, id, actor, actor ^ 1);
        }
    }

    [TestMethod]
    [DataRow(40, "SNORLAX", "DEFENSE_CURL", "ABILITY_IMMUNITY")]
    [DataRow(41, "SNORLAX", "DEFENSE_CURL", "ABILITY_IMMUNITY")]
    [DataRow(40, "CATERPIE", "TACKLE", "ABILITY_SHIELD_DUST")]
    [DataRow(41, "CATERPIE", "TACKLE", "ABILITY_SHIELD_DUST")]
    [DataRow(40, "NIDORAN_F", "GROWL", "ABILITY_POISON_POINT")]
    [DataRow(41, "NIDORAN_F", "GROWL", "ABILITY_POISON_POINT")]
    [DataRow(41, "MAGNEMITE", "TACKLE", "ABILITY_MAGNET_PULL")]
    public void NaturalStatusPreventionDoesNotCancelNeedleDamageOrEmitPrimaryFailure(int id, string species, string move, string ability)
    {
        var bodies = Bodies(id); bodies[1] = NaturalMon(species, 100, move);
        Assert.AreEqual(ability, bodies[1].Pokemon.AbilityId);
        var result = Find(bodies, r => {
            var draw = r.Trace.Single(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal));
            return id == 41 ? draw.Raw % 100 < 20 : draw.Raw % 100 <= 30;
        }, "qualifying poison roll against status prevention").Result;
        Assert.IsTrue(result.Events.OfType<SourceHpChangedEvent>().All(h => h.Damage > 0));
        Assert.AreEqual(WildBattleStatus.None, result.State.Slots[1].Body!.Status);
        Assert.AreEqual(0, result.Events.OfType<SourceAbilityPreventedEvent>().Count(), "Natural 20/30% secondaries are not CERTAIN; Immunity is silent here.");
        Assert.IsFalse(result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
        AssertDispatches(result.Events, id);
    }

    [TestMethod]
    [DataRow(40, "MAGNEMITE", "TACKLE", false)]
    [DataRow(40, "SHEDINJA", "HARDEN", true)]
    [DataRow(41, "SHEDINJA", "HARDEN", true)]
    [DataRow(131, "GASTLY", "LICK", false)]
    [DataRow(131, "SHEDINJA", "HARDEN", true)]
    public void RealDamageImmunityRejectsZeroHitReceiptsAndZeroActualSummaries(int id, string species, string move, bool wonderGuard)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var bodies = Bodies(id, actor); bodies[actor ^ 1] = NaturalMon(species, 100, move);
            var result = Resolve(Frame(bodies, 31), actor, actor ^ 1);
            if (id != 40)
            {
                var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
                Assert.AreEqual(0, summary.ActualHits); Assert.AreEqual(0, Hits(result.Events).Length);
            }
            else if (!wonderGuard)
            {
                var hit = result.Events.OfType<SourceHpChangedEvent>().Single();
                Assert.AreEqual(0, hit.Damage); Assert.AreEqual(hit.HpBefore, hit.HpAfter);
            }
            Assert.IsTrue(wonderGuard
                ? result.Events.OfType<SourceAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_WONDER_GUARD")
                : result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
            AssertDenied(result.Events, "damage immunity");
        }
    }

    [TestMethod]
    [DataRow(40)]
    [DataRow(41)]
    [DataRow(131)]
    public void ProtectAndRealAccuracyMissNeverCreateAnInvocation(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var bodies = Bodies(id, actor); bodies[actor ^ 1] = bodies[actor ^ 1] with { Protected = true };
            var blocked = Resolve(Frame(bodies, 31), actor, actor ^ 1);
            Assert.AreEqual(0, Hits(blocked.Events).Length); AssertDenied(blocked.Events, "Protect");
        }
        var inaccurate = Bodies(id); inaccurate[0] = inaccurate[0] with { AccuracyStage = 0 };
        var missed = Find(inaccurate, r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "accuracy miss").Result;
        Assert.AreEqual(0, Hits(missed.Events).Length); AssertDenied(missed.Events, "accuracy miss");
    }

    [TestMethod]
    [DataRow(40)]
    [DataRow(41)]
    [DataRow(131)]
    public void SubstituteBreakAndEarlyTargetKoRetainOnlyCommittedReceiptOwnership(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (bool ko in new[] { false, true })
        {
            var bodies = Bodies(id, actor); int target = actor ^ 1;
            bodies[target] = ko ? bodies[target] with { Pokemon = bodies[target].Pokemon with { CurrentHp = 1 } }
                : bodies[target] with { CalledVolatile = new() { SubstituteHp = 1 } };
            var result = Resolve(Frame(bodies, 31), actor, target);
            if (ko)
            {
                Assert.AreEqual(0, result.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);
                Assert.AreEqual(target, result.Events.OfType<SourceFaintedEvent>().Single().Side.Value);
                if (id != 40) Assert.AreEqual(1, result.Events.OfType<SourceMultiHitEvent>().Single().ActualHits);
            }
            else
            {
                var substitute = result.Events.OfType<SourceSubstituteDamagedEvent>().Single();
                Assert.AreEqual(1, substitute.HpBefore); Assert.AreEqual(0, substitute.HpAfter);
                if (id != 40)
                {
                    Assert.IsTrue(result.Events.OfType<SourceHpChangedEvent>().Any());
                    var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
                    Assert.AreEqual(summary.PlannedHits, summary.ActualHits);
                }
            }
            AssertDispatches(result.Events, id, actor, target);
        }
    }

    [TestMethod]
    [DataRow(40)]
    [DataRow(41)]
    [DataRow(131)]
    public void OneHpEndureCommitsOneZeroLossVisualAndStopsMultihit(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var bodies = Bodies(id, actor); int target = actor ^ 1;
            bodies[target] = bodies[target] with { Enduring = true, Pokemon = bodies[target].Pokemon with { CurrentHp = 1 } };
            var result = Resolve(Frame(bodies, 31), actor, target);
            var hp = result.Events.OfType<SourceHpChangedEvent>().Single();
            Assert.AreEqual(1, hp.HpBefore); Assert.AreEqual(1, hp.HpAfter); Assert.AreEqual(0, hp.Damage);
            Assert.IsTrue(result.Events.OfType<SourceDamageAdjustedEvent>().Single().Endured);
            if (id != 40) Assert.AreEqual(1, result.Events.OfType<SourceMultiHitEvent>().Single().ActualHits);
            AssertDispatches(result.Events, id, actor, target);
        }
    }

    [TestMethod]
    [DataRow(41)]
    [DataRow(131)]
    public void FocusBandZeroLossDoesNotReduceCommittedHitCount(int id)
    {
        var bodies = Bodies(id); bodies[1] = bodies[1] with { HeldItem = "ITEM_FOCUS_BAND", Pokemon = bodies[1].Pokemon with { CurrentHp = 1 } };
        var result = Find(bodies, r => {
            var summary = r.Events.OfType<SourceMultiHitEvent>().Single();
            return summary.ActualHits == summary.PlannedHits && r.Events.OfType<SourceHpChangedEvent>().All(h => h.HpAfter == 1);
        }, "Focus Band survives every committed needle hit").Result;
        Assert.IsTrue(result.Events.OfType<SourceHpChangedEvent>().All(h => h.HpBefore == 1 && h.HpAfter == 1 && h.Damage == 0));
        AssertDispatches(result.Events, id);
    }

    [TestMethod]
    [DataRow("SHARPEDO", "FOCUS_ENERGY", "ABILITY_ROUGH_SKIN")]
    [DataRow("SHROOMISH", "GROWTH", "ABILITY_EFFECT_SPORE")]
    public void AllThreeNaturalMovesAreNoncontactSoCannotCauseRoughSkinKoOrEffectSporeSleep(string species, string move, string ability)
    {
        foreach (var definition in Moves)
        for (int actor = 0; actor < 4; actor++)
        {
            var bodies = Bodies(definition.Id, actor);
            bodies[actor] = bodies[actor] with { Pokemon = bodies[actor].Pokemon with { CurrentHp = 1 } };
            bodies[actor ^ 1] = NaturalMon(species, 100, move);
            Assert.AreEqual(ability, bodies[actor ^ 1].Pokemon.AbilityId);
            var result = Resolve(Frame(bodies, 31), actor, actor ^ 1);
            Assert.AreEqual(1, result.State.Slots[actor].Body!.Pokemon.CurrentHp);
            Assert.AreEqual(WildBattleStatus.None, result.State.Slots[actor].Body!.Status);
            Assert.AreEqual(0, result.Events.OfType<SourceAbilityDamageEvent>().Count());
            Assert.IsFalse(result.Trace.Any(t => t.Purpose.Contains("effect-spore", StringComparison.Ordinal)));
            if (definition.Id != 40)
            {
                var summary = result.Events.OfType<SourceMultiHitEvent>().Single(); Assert.AreEqual(summary.PlannedHits, summary.ActualHits);
            }
            AssertDispatches(result.Events, definition.Id, actor, actor ^ 1);
        }
    }

    [TestMethod]
    public void PpProvenanceAndFixedOrRandomSummaryBoundsRemainFailClosed()
    {
        foreach (var move in Moves)
        {
            var good = Group(move.Id); var attack = (SourceAttackStringEvent)good[0]; var pp = (SourcePpCommandEvent)good[2];
            AssertDenied(good.SetItem(0, attack with { ChosenMove = Id("METRONOME") }), "called attack");
            AssertDenied(good.SetItem(2, pp with { ChosenMove = Id("SLEEP_TALK") }), "called PP");
            AssertDenied(good.SetItem(2, pp with { PpOwner = new(1) }), "wrong PP owner");
            AssertDenied(good.RemoveAt(2), "missing PP"); AssertDenied(Number(good.Insert(3, pp)), "duplicate PP");
            AssertDenied(Number(good.RemoveAt(2).Add(pp)), "PP after hit");
            if (move.Id == 40) continue;
            var summary = (SourceMultiHitEvent)good[^1];
            foreach (int plan in move.Id == 41 ? new[] { 0, 1, 3, 4, 5, 6 } : new[] { 0, 1, 6 })
                AssertDenied(good.SetItem(good.Length - 1, summary with { PlannedHits = plan }), "invalid planned hits");
            AssertDenied(good.RemoveAt(good.Length - 1), "missing summary");
            AssertDenied(Number(good.Add(summary)), "duplicate summary");
            AssertDenied(good.SetItem(good.Length - 1, summary with { ActualHits = 1 }), "actual mismatch");
        }
    }

    [TestMethod]
    public void ExplicitBlockingReceiptsRemainRejectedWithoutBroadeningTheAbilityGuard()
    {
        foreach (var definition in Moves)
        {
            var events = Group(definition.Id); var move = Id(definition.Name);
            SourceBattleEvent[] blocked = [
                new SourceMoveMissedEvent(0, 7, new(0), new(1), move),
                new SourceMoveFailedEvent(0, 7, new(0), new(1), move, "protected"),
                new SourceAbilityPreventedEvent(0, 7, new(0), new(1), move, "ABILITY_WONDER_GUARD"),
                new SourceEffectivenessEvent(0, 7, new(0), new(1), move, WildBattleEffectiveness.NoEffect)
            ];
            foreach (var receipt in blocked)
                AssertDenied(Number(events.Insert(3, receipt)), receipt.GetType().Name);
        }
    }

    [TestMethod]
    public void PressureAndClampedFinalPpUseTheSharedDirectInvocationContract()
    {
        foreach (var definition in Moves)
        foreach (int before in new[] { 20, 1 })
        {
            var events = Group(definition.Id); int after = Math.Max(0, before - 2);
            events = events.SetItem(1, ((SourceMoveUsedEvent)events[1]) with { PpBefore = before, PpAfter = after })
                .SetItem(2, ((SourcePpCommandEvent)events[2]) with { Before = before, After = after, CommandCost = 2 });
            AssertDispatches(events, definition.Id);
        }
    }

    [TestMethod]
    public void SinglesRulesScenePhaseAndCursorGatesPreserveBothOwnershipModels()
    {
        foreach (var move in Moves)
        {
            var events = Group(move.Id); int index = move.Id == 40 ? 1 : 3; var battle = Single(events, index);
            CampaignMoveAnimationDecision Project(CampaignWildBattle b, bool source = true, SourceBattleScene scene = SourceBattleScene.On) => move.Id == 40
                ? AnimationDispatchFixture.AtAttackText(b, source, scene) : AnimationDispatchFixture.AtResult(b, source, scene);
            Assert.IsNotNull(Project(battle).Dispatch);
            Assert.IsNull(Project(battle, false).Dispatch); Assert.IsNull(Project(battle, scene: SourceBattleScene.Off).Dispatch);
            Assert.IsNull(Project(battle with { Phase = CampaignBattlePhase.ChooseMove }).Dispatch);
            Assert.IsNull(Project(battle with { EventIndex = -1 }).Dispatch); Assert.IsNull(Project(battle with { EventIndex = events.Length }).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events, index), SourceBattleScene.Off).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtResult(Double(events, index), SourceBattleScene.Off).Dispatch);
        }
    }
}

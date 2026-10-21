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

[TestClass, TestCategory("Game")]
public sealed class BoneMoveAnimationDispatchTests
{
    private static readonly MoveId Club = Id("BONE_CLUB");
    private static readonly MoveId Rush = Id("BONE_RUSH");
    private static readonly DirectHitAnimationContract ClubContract = new("bone-moves", 125, Id("BONE_CLUB"), 20, DirectHitOwnership.AttackText);
    private static readonly DirectHitAnimationContract RushContract = new("bone-moves", 198, Id("BONE_RUSH"), 10, DirectHitOwnership.EachHit, 2, 5);

    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index = 0) => ClubContract.Double(events, index);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index = 0) => ClubContract.Single(events, index);
    private static CampaignMoveAnimationDecision AtHit(ImmutableArray<SourceBattleEvent> events, int index = 0) => ClubContract.AtHit(events, index);
    private static CampaignMoveAnimationDecision AtText(ImmutableArray<SourceBattleEvent> events, int index = 0) => ClubContract.AtText(events, index);

    private static ImmutableArray<SourceBattleEvent> Group(bool rush, int actor = 0, int? target = null, int planned = 3, int? actual = null) =>
        (rush ? RushContract : ClubContract).Group(actor, target, planned, actual);

    private static void AssertRush(ImmutableArray<SourceBattleEvent> events, int actor, int target) =>
        RushContract.AssertDispatches(events, actor, target);

    private static void AssertClub(ImmutableArray<SourceBattleEvent> events, int actor, int target) =>
        ClubContract.AssertDispatches(events, actor, target);

    private static void AssertDenied(ImmutableArray<SourceBattleEvent> events, string label, bool singles = false) =>
        ClubContract.AssertDenied(events, label, singles);

    [TestMethod]
    public void RushTwoThroughFivePlansUseOnlyActualReceiptOrdinalsForAllPositionsAndSelectedAllies()
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        for (int planned = 2; planned <= 5; planned++)
        for (int actual = 1; actual <= planned; actual++)
            AssertRush(Group(true, actor, target, planned, actual), actor, target);
    }

    [TestMethod]
    public void SinglesExportRetainsBothOwnershipModelsAndRulesScenePhaseCursorGates()
    {
        foreach (bool rush in new[] { false, true })
        for (int actor = 0; actor < 2; actor++)
        {
            var events = Group(rush, actor, planned: 5); var battle = Single(events);
            for (int index = 0; index < events.Length; index++)
            {
                var current = battle with { EventIndex = index };
                Assert.AreEqual(AtText(events, index).Dispatch,
                    AnimationDispatchFixture.AtAttackText(current, true, SourceBattleScene.On).Dispatch);
                Assert.AreEqual(AtHit(events, index).Dispatch,
                    AnimationDispatchFixture.AtResult(current, true, SourceBattleScene.On).Dispatch);
            }
            CampaignMoveAnimationDecision Project(CampaignWildBattle current, bool source = true, SourceBattleScene scene = SourceBattleScene.On) => rush
                ? AnimationDispatchFixture.AtResult(current, source, scene)
                : AnimationDispatchFixture.AtAttackText(current, source, scene);
            var eligible = battle with { EventIndex = rush ? 3 : 1 };
            Assert.IsNotNull(Project(eligible).Dispatch);
            Assert.IsNull(Project(eligible, false).Dispatch); Assert.IsNull(Project(eligible, scene: SourceBattleScene.Off).Dispatch);
            Assert.IsNull(Project(eligible with { Phase = CampaignBattlePhase.ChooseMove }).Dispatch);
            Assert.IsNull(Project(eligible with { EventIndex = -1 }).Dispatch);
            Assert.IsNull(Project(eligible with { EventIndex = events.Length }).Dispatch);
            var twice = Double(events, rush ? 3 : 1);
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(twice, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtResult(twice, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(twice with { Stage = CampaignDoubleBattleStage.Finished }, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtResult(twice with { Stage = CampaignDoubleBattleStage.Finished }, SourceBattleScene.On).Dispatch);
        }
    }

    [TestMethod]
    [DataRow(false, 20, 2, 18)]
    [DataRow(false, 1, 2, 0)]
    [DataRow(true, 20, 2, 18)]
    [DataRow(true, 1, 2, 0)]
    public void PressureCostAndClampedFinalPpRemainValidDirectInvocations(bool rush, int before, int cost, int after)
    {
        var events = Group(rush); var used = (SourceMoveUsedEvent)events[1]; var pp = (SourcePpCommandEvent)events[2];
        events = events.SetItem(1, used with { PpBefore = before, PpAfter = after })
            .SetItem(2, pp with { Before = before, After = after, CommandCost = cost });
        Assert.AreEqual(Math.Max(0, before - cost), after);
        if (rush) AssertRush(events, 0, 1); else AssertClub(events, 0, 1);
        for (int index = 0; index < events.Length; index++)
        {
            var single = Single(events, index);
            Assert.AreEqual(AtText(events, index).Dispatch,
                AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.On).Dispatch);
            Assert.AreEqual(AtHit(events, index).Dispatch,
                AnimationDispatchFixture.AtResult(single, true, SourceBattleScene.On).Dispatch);
        }
    }

    [TestMethod]
    public void BoneClubDirectInvocationRejectsCalledMovesAndMismatchedPpProvenance()
    {
        var good = Group(false); var attack = (SourceAttackStringEvent)good[0];
        var used = (SourceMoveUsedEvent)good[1]; var pp = (SourcePpCommandEvent)good[2];
        (string Label, ImmutableArray<SourceBattleEvent> Events)[] malformed = [
            ("missing attack", good.RemoveAt(0)), ("missing PP", good.RemoveAt(2)),
            ("nonadjacent attack", Number(good.Insert(1, new SourceAbilityActivatedEvent(0, 7, new(0), "ABILITY_ROCK_HEAD")))),
            ("Metronome attack", good.SetItem(0, attack with { ChosenMove = Id("METRONOME") })),
            ("Sleep Talk attack", good.SetItem(0, attack with { ChosenMove = Id("SLEEP_TALK") })),
            ("wrong current move", good.SetItem(0, attack with { CurrentMove = Rush })),
            ("wrong attack actor", good.SetItem(0, attack with { Actor = new(1) })),
            ("wrong attack turn", good.SetItem(0, attack with { Turn = 6 })),
            ("called PP", good.SetItem(2, pp with { ChosenMove = Id("METRONOME") })),
            ("wrong PP owner", good.SetItem(2, pp with { PpOwner = new(1) })),
            ("wrong PP actor", good.SetItem(2, pp with { Actor = new(1) })),
            ("wrong PP target", good.SetItem(2, pp with { Target = new(0) })),
            ("wrong PP current move", good.SetItem(2, pp with { CurrentMove = Rush })),
            ("wrong PP slot", good.SetItem(2, pp with { PpSlot = 1 })),
            ("wrong PP before", good.SetItem(2, pp with { Before = 19 })),
            ("wrong PP after", good.SetItem(2, pp with { After = 18 })),
            ("zero PP cost", good.SetItem(2, pp with { CommandCost = 0 })),
            ("mismatched PP cost", good.SetItem(2, pp with { CommandCost = 2 })),
            ("wrong PP turn", good.SetItem(2, pp with { Turn = 8 })),
            ("invalid used slot", good.SetItem(1, used with { Slot = -1 })),
            ("wrong used PP", good.SetItem(1, used with { PpBefore = 19 })),
            ("zero available PP", good.SetItem(1, used with { PpBefore = 0, PpAfter = 0 }).SetItem(2, pp with { Before = 0, After = 0 })),
            ("matching invalid slot", good.SetItem(1, used with { Slot = 4 }).SetItem(2, pp with { PpSlot = 4 })),
            ("duplicate PP", Number(good.Insert(3, pp))),
            ("PP after receipt", Number(good.RemoveAt(2).Add(pp))),
            ("attack sequence collision", good.SetItem(0, attack with { Sequence = used.Sequence })),
            ("PP sequence before used", good.SetItem(2, pp with { Sequence = 1 }))
        ];
        foreach (var invalid in malformed) AssertDenied(invalid.Events, invalid.Label, singles: true);
        AssertClub(good, 0, 1);
    }

    [TestMethod]
    public void ExplicitMissProtectAbilityAndNoEffectOverridePlausibleBoneReceipts()
    {
        foreach (bool rush in new[] { false, true })
        {
            var good = Group(rush); var move = rush ? Rush : Club;
            SourceBattleEvent[] failures = [
                new SourceMoveMissedEvent(0, 7, new(0), new(1), move),
                new SourceMoveFailedEvent(0, 7, new(0), new(1), move, "protected"),
                new SourceAbilityPreventedEvent(0, 7, new(0), new(1), move, "ABILITY_WONDER_GUARD"),
                new SourceEffectivenessEvent(0, 7, new(0), new(1), move, WildBattleEffectiveness.NoEffect)
            ];
            foreach (var failure in failures)
                AssertDenied(Number(good.Insert(3, failure)), failure.GetType().Name, singles: true);
        }
    }

    [TestMethod]
    public void RushMalformedSummaryAndInvocationCannotAuthorizeAnyReceipt()
    {
        var good = Group(true, planned: 2); var summary = (SourceMultiHitEvent)good[^1];
        var attack = (SourceAttackStringEvent)good[0]; var pp = (SourcePpCommandEvent)good[2];
        foreach (int planned in new[] { 0, 1, 6 })
            AssertDenied(good.SetItem(good.Length - 1, summary with { PlannedHits = planned }), "invalid plan " + planned, true);
        foreach (int actual in new[] { -1, 0, 1, 3 })
            AssertDenied(good.SetItem(good.Length - 1, summary with { ActualHits = actual }), "invalid actual " + actual, true);
        AssertDenied(good.RemoveAt(good.Length - 1), "missing summary", true);
        AssertDenied(Number(good.Add(summary)), "duplicate summary", true);
        AssertDenied(Number(good.RemoveAt(good.Length - 1).Insert(4, summary)), "summary before final receipt", true);
        AssertDenied(good.SetItem(0, attack with { ChosenMove = Id("SLEEP_TALK") }), "called Rush", true);
        AssertDenied(good.SetItem(2, pp with { PpOwner = new(1) }), "wrong PP owner", true);
        AssertDenied(good.SetItem(2, pp with { ChosenMove = Id("METRONOME") }), "wrong chosen PP move", true);
        AssertDenied(good.SetItem(good.Length - 1, summary with { Target = new(0) }), "wrong summary recipient", true);
        AssertDenied(good.SetItem(4, good[4] with { Sequence = good[3].Sequence }), "duplicate receipt sequence", true);
    }

    [TestMethod]
    public void ZeroLossAndInterleavedReceiptsDoNotChangeRushHitOrdinals()
    {
        var prefix = Group(true).Take(3); var a = new BattlerSlot(0); var t = new BattlerSlot(1);
        var events = Number(prefix.Concat(new SourceBattleEvent[] {
            new SourceSubstituteDamagedEvent(0, 7, a, t, Rush, 1, 0, 10, false, 100),
            new SourceMtMoonVolatileEvent(0, 7, t, "substitute", 1, 0, "faded"),
            new SourceHpChangedEvent(0, 7, a, t, Rush, 30, 20, 10, false, 100),
            new SourceHeldItemConsumedEvent(0, 7, t, "ITEM_ORAN_BERRY", 20, 30, 10),
            new SourceFlinchEvent(0, 7, t, true),
            new SourceHpChangedEvent(0, 7, a, t, Rush, 30, 20, 10, true, 92),
            new SourceCriticalHitEvent(0, 7, a, t),
            new SourceMultiHitEvent(0, 7, a, t, Rush, 3, 3)
        }));
        AssertRush(events, 0, 1);
        foreach (bool endure in new[] { false, true })
        {
            int count = endure ? 1 : 3; var zero = prefix.ToList();
            for (int i = 0; i < count; i++)
            {
                zero.Add(new SourceDamageAdjustedEvent(0, 7, t, Rush, endure ? "ITEM_NONE" : "ITEM_FOCUS_BAND", 10, 0, endure, !endure));
                zero.Add(new SourceHpChangedEvent(0, 7, a, t, Rush, 1, 1, 0, false, 100));
            }
            zero.Add(new SourceMultiHitEvent(0, 7, a, t, Rush, 5, count));
            AssertRush(Number(zero), 0, 1);
        }
        var club = Group(false);
        AssertClub(club.SetItem(3, ((SourceHpChangedEvent)club[3]) with { HpBefore = 1, HpAfter = 1, Damage = 0 }), 0, 1);
        AssertClub(club.SetItem(3, new SourceSubstituteDamagedEvent(4, 7, a, t, Club, 1, 0, 10, false, 100)), 0, 1);
    }

    private static WildBattleCombatant[] Bodies(bool rush, int actor)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL") with { SpeedStage = 0 }).ToArray();
        bodies[actor] = NaturalMon("CUBONE", rush ? 41 : 9, rush ? "BONE_RUSH" : "BONE_CLUB") with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }

    private static SourceKernelResult DirectRush(SourceBattleFrame frame, int actor, int target)
    {
        Assert.AreEqual(Rush, frame.Slots[actor].Body!.Pokemon.Moves[0].Move);
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        return result;
    }

    [TestMethod]
    public void NaturalCuboneBoneMovesUseActualOrderedTurnAtEveryPositionAndSelectedAllies()
    {
        foreach (bool rush in new[] { false, true })
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        {
            var result = Ordered(Frame(Bodies(rush, actor), 31), actor, target);
            Assert.AreEqual(rush ? 1 : 0, result.Events.OfType<SourceMultiHitEvent>().Count());
            var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreEqual(rush ? 10 : 20, pp.Before); Assert.AreEqual(rush ? 9 : 19, pp.After); Assert.AreEqual(1, pp.CommandCost);
            if (rush) AssertRush(result.Events, actor, target); else AssertClub(result.Events, actor, target);
        }
    }

    [TestMethod]
    public void NaturalCuboneRushMatchesIndependentSourceCountAndRngVectorsAtEveryPosition()
    {
        using var source = JsonDocument.Parse(File.ReadAllBytes(TestAssets.GeneratedFile("tests/Game.Battle.Tests/Moves/source-vectors.json", "python tests/Game.Battle.Tests/Moves/source_probe.py content/raw/pokefirered/037335f4 tests/Game.Battle.Tests/Moves --fixtures-only")));
        Assert.AreEqual(PokemonFactory.SourceCommit, source.RootElement.GetProperty("sourceCommit").GetString());
        var vectors = source.RootElement.GetProperty("multihit"); Assert.AreEqual(48, vectors.GetArrayLength());
        foreach (var vector in vectors.EnumerateArray())
        for (int actor = 0; actor < 4; actor++)
        {
            var result = DirectRush(Frame(Bodies(true, actor), vector.GetProperty("seed").GetUInt32()), actor, actor ^ 1);
            var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
            Assert.AreEqual(vector.GetProperty("hits").GetInt32(), summary.PlannedHits);
            Assert.AreEqual(summary.PlannedHits, summary.ActualHits);
            Assert.AreEqual(vector.GetProperty("draws").GetInt32(), result.Trace.Length);
            Assert.AreEqual(vector.GetProperty("finalSeed").GetUInt32(), result.State.Rng.Seed);
            var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreEqual(10, pp.Before); Assert.AreEqual(9, pp.After); Assert.AreEqual(1, pp.CommandCost);
            AssertRush(result.Events, actor, actor ^ 1);
        }
    }

    [TestMethod]
    public void NaturalRushSubstituteBreakAndSingleHitKoRetainOnlyActualReceipts()
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (bool ko in new[] { false, true })
        {
            int target = actor ^ 1; var bodies = Bodies(true, actor);
            bodies[target] = ko ? bodies[target] with { Pokemon = bodies[target].Pokemon with { CurrentHp = 1 } }
                : bodies[target] with { CalledVolatile = new() { SubstituteHp = 1 } };
            var result = DirectRush(Frame(bodies, 31), actor, target);
            var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
            if (ko)
            {
                Assert.AreEqual(1, summary.ActualHits); Assert.IsTrue(summary.PlannedHits > summary.ActualHits);
                Assert.AreEqual(0, result.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);
                Assert.AreEqual(target, result.Events.OfType<SourceFaintedEvent>().Single().Side.Value);
            }
            else
            {
                var substitute = result.Events.OfType<SourceSubstituteDamagedEvent>().Single();
                Assert.AreEqual(1, substitute.HpBefore); Assert.AreEqual(0, substitute.HpAfter);
                Assert.AreEqual(summary.PlannedHits, summary.ActualHits);
                Assert.AreEqual(summary.ActualHits - 1, result.Events.OfType<SourceHpChangedEvent>().Count());
            }
            AssertRush(result.Events, actor, target);
        }
    }

    [TestMethod]
    [DataRow("PIDGEY", "AGILITY", "flying")]
    [DataRow("GASTLY", "MEAN_LOOK", "levitate")]
    [DataRow("SHEDINJA", "HARDEN", "wonder-guard")]
    public void GroundImmunityRejectsClubZeroHpReceiptsAndRushZeroActualSummary(string species, string move, string branch)
    {
        foreach (bool rush in new[] { false, true })
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(rush, actor);
            bodies[target] = NaturalMon(species, 100, move) with { SpeedStage = 0 };
            if (branch == "levitate") Assert.AreEqual("ABILITY_LEVITATE", bodies[target].Pokemon.AbilityId);
            if (branch == "wonder-guard") Assert.AreEqual("ABILITY_WONDER_GUARD", bodies[target].Pokemon.AbilityId);
            var result = Ordered(Frame(bodies, 31), actor, target);
            if (rush)
            {
                var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
                Assert.IsTrue(summary.PlannedHits is >= 2 and <= 5); Assert.AreEqual(0, summary.ActualHits);
                Assert.AreEqual(0, Hits(result.Events).Length);
                Assert.IsFalse(result.Trace.Any(t => t.Purpose.EndsWith(".damage", StringComparison.Ordinal)));
            }
            else
            {
                var hp = result.Events.OfType<SourceHpChangedEvent>().Single();
                Assert.AreEqual(0, hp.Damage); Assert.AreEqual(hp.HpBefore, hp.HpAfter);
                Assert.AreEqual(0, result.Events.OfType<SourceMultiHitEvent>().Count());
            }
            Assert.IsTrue(branch == "wonder-guard"
                ? result.Events.OfType<SourceAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_WONDER_GUARD")
                : result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
            AssertDenied(result.Events, branch);
        }
    }

    [TestMethod]
    [DataRow("SHARPEDO", "FOCUS_ENERGY", "ABILITY_ROUGH_SKIN")]
    [DataRow("PARASECT", "GROWTH", "ABILITY_EFFECT_SPORE")]
    public void BothBoneMovesAreNoncontactEvenWithOneHpAttacker(string species, string move, string ability)
    {
        foreach (bool rush in new[] { false, true })
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(rush, actor);
            bodies[actor] = bodies[actor] with { Pokemon = bodies[actor].Pokemon with { CurrentHp = 1 } };
            bodies[target] = NaturalMon(species, 100, move) with { SpeedStage = 0 };
            Assert.AreEqual(ability, bodies[target].Pokemon.AbilityId);
            var result = Ordered(Frame(bodies, 31), actor, target);
            Assert.AreEqual(1, result.State.Slots[actor].Body!.Pokemon.CurrentHp);
            Assert.AreEqual(WildBattleStatus.None, result.State.Slots[actor].Body!.Status);
            Assert.AreEqual(0, result.Events.OfType<SourceAbilityDamageEvent>().Count());
            Assert.IsFalse(result.Trace.Any(t => t.Purpose.Contains("effect-spore", StringComparison.Ordinal)));
            if (rush)
            {
                var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
                Assert.AreEqual(summary.PlannedHits, summary.ActualHits); AssertRush(result.Events, actor, target);
            }
            else AssertClub(result.Events, actor, target);
        }
    }

    [TestMethod]
    public void OrderedFocusBandZeroLossRushStillDispatchesEveryCommittedHit()
    {
        var bodies = Bodies(true, 0); bodies[1] = bodies[1] with {
            HeldItem = "ITEM_FOCUS_BAND", Pokemon = bodies[1].Pokemon with { CurrentHp = 1 } };
        var entered = Frame(bodies, 0); SourceFourActionResult? found = null;
        // Bounded branch discovery is an integration fixture, not a source golden.
        for (uint seed = 0; seed < 128 && found is null; seed++)
        {
            var result = Ordered(entered with { Rng = new(seed) }, 0, 1);
            var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
            if (summary.ActualHits == summary.PlannedHits && result.Events.OfType<SourceHpChangedEvent>().All(h => h.HpAfter == 1)) found = result;
        }
        Assert.IsNotNull(found, "No actual ordered Focus Band survival branch in seeds0..127.");
        Assert.IsTrue(found.Events.OfType<SourceDamageAdjustedEvent>().Any(e => e.HungOn));
        Assert.IsTrue(found.Events.OfType<SourceHpChangedEvent>().All(h => h.HpBefore == 1 && h.HpAfter == 1 && h.Damage == 0));
        AssertRush(found.Events, 0, 1);
    }

    [TestMethod]
    public void SourceFlagsAndOrderedKingsRockKeepNoncontactAndMoveEligibilityDistinct()
    {
        Assert.IsFalse(Mechanics.Catalog.Moves[Club].Flags.Contains("FLAG_MAKES_CONTACT"));
        Assert.IsFalse(Mechanics.Catalog.Moves[Rush].Flags.Contains("FLAG_MAKES_CONTACT"));
        Assert.IsFalse(Mechanics.Catalog.Moves[Club].Flags.Contains("FLAG_KINGS_ROCK_AFFECTED"));
        Assert.IsTrue(Mechanics.Catalog.Moves[Rush].Flags.Contains("FLAG_KINGS_ROCK_AFFECTED"));
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(true, actor);
            bodies[actor] = bodies[actor] with { HeldItem = "ITEM_KINGS_ROCK" };
            var result = Ordered(Frame(bodies, 31), actor, target);
            var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
            Assert.AreEqual(summary.PlannedHits, summary.ActualHits);
            Assert.AreEqual(summary.ActualHits + 1, result.Trace.Count(t => t.Purpose.EndsWith(".kings-rock", StringComparison.Ordinal)),
                "Ordinary multi-hit runs the item phase per hit and once at final move-end.");
            AssertRush(result.Events, actor, target);
        }
        var clubBodies = Bodies(false, 0); clubBodies[0] = clubBodies[0] with { HeldItem = "ITEM_KINGS_ROCK" };
        var clubFrame = Frame(clubBodies, 0); SourceFourActionResult? clubBranch = null;
        for (uint seed = 0; seed < 128 && clubBranch is null; seed++)
        {
            var result = Ordered(clubFrame with { Rng = new(seed) }, 0, 1);
            var rock = result.Trace.Single(t => t.Purpose.EndsWith(".kings-rock", StringComparison.Ordinal));
            var secondary = result.Trace.Single(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal));
            if (rock.Raw % 100 < 10 && secondary.Raw % 100 > 10) clubBranch = result;
        }
        Assert.IsNotNull(clubBranch, "No ordered Club native-secondary miss plus King's Rock qualifying roll in seeds0..127.");
        Assert.AreEqual(0, clubBranch.Events.OfType<SourceFlinchEvent>().Count(),
            "The held-item roll occurs before the move flag, but Club cannot gain King's Rock flinch.");
        AssertClub(clubBranch.Events, 0, 1);
    }
}

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
public sealed class HornMoveAnimationDispatchTests
{
    private static readonly (string Name, int Id, int Level, int Pp)[] Moves =
        [("HORN_ATTACK", 30, 1, 25), ("FURY_ATTACK", 31, 15, 20), ("HORN_DRILL", 32, 100, 5), ("GUILLOTINE", 12, 100, 5)];
    private static MoveId Id(string name) => new("pret:MOVE_" + name);
    private static SourceMechanicsCatalog Mechanics =>
        TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("horn-moves", new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("horn-moves", null!, new(), events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(), index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []) { SourceEvents = events };
    private static CampaignMoveAnimationDecision AtText(ImmutableArray<SourceBattleEvent> events, int index) =>
        AnimationDispatchFixture.AtAttackText(Double(events, index), SourceBattleScene.On);
    private static CampaignMoveAnimationDecision AtHit(ImmutableArray<SourceBattleEvent> events, int index) =>
        AnimationDispatchFixture.AtResult(Double(events, index), SourceBattleScene.On);
    private static SourceBattleEvent[] Hits(ImmutableArray<SourceBattleEvent> events) =>
        events.Where(e => e is SourceHpChangedEvent or SourceSubstituteDamagedEvent).ToArray();

    private static void AssertDispatches(ImmutableArray<SourceBattleEvent> events, int id, int actor = 0, int target = 1)
    {
        bool perHit = id == 31;
        var hits = Hits(events); Assert.IsTrue(hits.Length > 0);
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        Assert.AreEqual(Id(Moves.Single(m => m.Id == id).Name), used.Move);
        Assert.AreEqual(used.Move, attack.ChosenMove); Assert.AreEqual(used.Move, attack.CurrentMove);
        Assert.AreEqual(used.Move, pp.ChosenMove); Assert.AreEqual(used.Move, pp.CurrentMove);
        Assert.AreEqual(actor, pp.PpOwner.Value); Assert.AreEqual(actor, pp.Actor.Value); Assert.AreEqual(target, pp.Target.Value);
        Assert.IsTrue(attack.Sequence < used.Sequence && used.Sequence < pp.Sequence && pp.Sequence < hits[0].Sequence);
        if (perHit)
        {
            var summary = events.OfType<SourceMultiHitEvent>().Single();
            Assert.AreEqual(hits.Length, summary.ActualHits);
            Assert.IsTrue(summary.PlannedHits is >= 2 and <= 5);
            Assert.IsTrue(summary.ActualHits >= 1 && summary.ActualHits <= summary.PlannedHits);
            Assert.IsTrue(hits[^1].Sequence < summary.Sequence);
        }
        else { Assert.AreEqual(1, hits.Length); Assert.AreEqual(0, events.OfType<SourceMultiHitEvent>().Count()); }
        string before = JsonSerializer.Serialize(events); int count = 0, acknowledgements = 0;
        for (int index = 0; index < events.Length; index++)
        {
            var text = AtText(events, index); var hit = AtHit(events, index);
            int ordinal = Array.FindIndex(hits, h => h.Sequence == events[index].Sequence);
            bool eligible = perHit ? ordinal >= 0 : events[index] == used;
            var decision = perHit ? hit : text;
            Assert.IsNull((perHit ? text : hit).Dispatch, "One source invocation cannot acquire both receipt ownership models.");
            if (eligible)
            {
                var dispatch = decision.Dispatch; Assert.IsNotNull(dispatch, decision.Reason); count++;
                var receipt = perHit ? hits[ordinal] : hits[0];
                Assert.AreEqual(id, dispatch.SourceMoveId); Assert.AreEqual("horn-moves", dispatch.BattleId);
                Assert.AreEqual(actor, dispatch.Actor.Value); Assert.AreEqual(target, dispatch.Target.Value);
                Assert.AreEqual(perHit ? receipt.Sequence : used.Sequence, dispatch.Sequence);
                Assert.AreEqual(receipt.Sequence, dispatch.ResultSequence);
                Assert.AreEqual(attack.Sequence, dispatch.AttackSequence); Assert.AreEqual(pp.Sequence, dispatch.PpSequence);
                Assert.AreEqual(receipt is SourceSubstituteDamagedEvent, dispatch.HitsSubstitute);
                Assert.AreEqual(!perHit, dispatch.AcknowledgesCurrentReceipt);
                if (dispatch.AcknowledgesCurrentReceipt) acknowledgements++;
                if (perHit) Assert.AreEqual((byte)ordinal, dispatch.MoveTurn); else Assert.IsNull(dispatch.MoveTurn);
                Assert.AreEqual(decision, perHit ? AtHit(events, index) : AtText(events, index),
                    "Repeated projection is observational; it does not consume or acknowledge authoritative receipts.");
            }
            else Assert.IsNull(decision.Dispatch, "Secondary, contact, summary, KO and result receipts cannot replay the root.");
            if (actor < 2 && target < 2)
            {
                var single = Single(events, index);
                Assert.AreEqual(text.Dispatch, AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.On).Dispatch);
                Assert.AreEqual(hit.Dispatch, AnimationDispatchFixture.AtResult(single, true, SourceBattleScene.On).Dispatch);
            }
        }
        Assert.AreEqual(perHit ? hits.Length : 1, count);
        Assert.AreEqual(perHit ? 0 : 1, acknowledgements, "Per-hit visuals never create an additional authoritative ACK.");
        Assert.AreEqual(before, JsonSerializer.Serialize(events), "Presentation may not mutate committed receipts.");
    }
    private static void AssertDenied(ImmutableArray<SourceBattleEvent> events, string branch)
    {
        string before = JsonSerializer.Serialize(events);
        for (int index = 0; index < events.Length; index++)
        {
            Assert.IsNull(AtText(events, index).Dispatch, branch + " text");
            Assert.IsNull(AtHit(events, index).Dispatch, branch + " hit");
            if (events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value > 1 || e.Target.Value > 1)) continue;
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, index), true, SourceBattleScene.On).Dispatch, branch);
            Assert.IsNull(AnimationDispatchFixture.AtResult(Single(events, index), true, SourceBattleScene.On).Dispatch, branch);
        }
        Assert.AreEqual(before, JsonSerializer.Serialize(events));
    }
    private static void AssertReadOnly(SourceBattleFrame state, ImmutableArray<RandomTrace> trace, Action projection)
    {
        var rng = state.Rng; long revision = state.Revision, sequence = state.EventSequence;
        string frame = JsonSerializer.Serialize(state), random = JsonSerializer.Serialize(trace);
        projection(); projection();
        Assert.AreEqual(rng, state.Rng); Assert.AreEqual(revision, state.Revision); Assert.AreEqual(sequence, state.EventSequence);
        Assert.AreEqual(frame, JsonSerializer.Serialize(state)); Assert.AreEqual(random, JsonSerializer.Serialize(trace));
    }
    private static WildBattleCombatant NaturalMon(string species, int level, string name)
    {
        var move = Id(name); var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level),
            $"Natural source learnset required: {species}/{level}/{name}.");
        int pp = Mechanics.Catalog.Moves[move].Pp;
        return new(pokemon with { Moves = [new(move, pp, pp)] });
    }
    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var move = Moves.Single(m => m.Id == id);
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL") with { SpeedStage = 0 }).ToArray();
        bodies[actor] = NaturalMon(id==12?"KRABBY":"RHYHORN", move.Level, move.Name) with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }
    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies, uint seed)
    {
        static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
            new(body.Pokemon, body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
        var frame = new SourceBattleFrame {
            Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version, BattleKind = WildBattleCalledBattleKind.Trainer, Rng = new(31),
            Parties = [new(BattleSide.Player, [Member(bodies[0]), Member(bodies[2])]), new(BattleSide.Opponent, [Member(bodies[1]), Member(bodies[3])])],
            Slots = Enumerable.Range(0, 4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray()
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
        var selected = frame.Slots[actor].Body!.Pokemon.Moves[0].Move;
        var choices = frame.Slots.Select(c => {
            var move = Mechanics.Catalog.Moves[c.Body!.Pokemon.Moves[0].Move];
            int recipient = c.Slot.Value == actor ? target : move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? c.Slot.Value : c.Slot.Value ^ 1;
            return new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, 0, new(recipient));
        }).ToImmutableArray();
        var result = SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision, choices);
        for (int step = 0; step < 12; step++)
        {
            Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            if (result.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == actor && e.Move == selected)) return result;
            Assert.IsNotNull(result.Continuation, "The natural horn move must execute within this public turn.");
            result = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
        }
        throw new InvalidOperationException("The selected horn move did not execute in one bounded public turn.");
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
    public void NaturalHornMovesUseActualOrderedTurnsAtAllFourPositionsAndSelectedAllies()
    {
        foreach (var move in Moves)
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        {
            var definition = Mechanics.Catalog.Moves[Id(move.Name)];
            Assert.AreEqual("MOVE_TARGET_SELECTED", definition.Target);
            Assert.AreEqual(move.Id == 30 ? "EFFECT_HIT" : move.Id == 31 ? "EFFECT_MULTI_HIT" : "EFFECT_OHKO", definition.Effect);
            Assert.IsTrue(definition.Flags.Contains("FLAG_MAKES_CONTACT"));
            Assert.AreEqual(move.Id is not(12 or 32), definition.Flags.Contains("FLAG_KINGS_ROCK_AFFECTED"));
            Assert.AreEqual(0, definition.SecondaryEffectChance);
            SourceFourActionResult? result = null;
            // Ordering consumes its own source RNG. Discover a successful OHKO only after
            // running the real selection/order boundary, never by rewriting its receipts.
            for (uint seed = 0; seed < (move.Id is 12 or 32 ? 1024U : 1U); seed++)
            {
                var candidate = Ordered(Frame(Bodies(move.Id, actor), seed), actor, target);
                if (Hits(candidate.Events).Length > 0) { result = candidate; break; }
            }
            Assert.IsNotNull(result, "The bounded real turn must observe a successful horn invocation.");
            var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreEqual(move.Pp, pp.Before); Assert.AreEqual(move.Pp - 1, pp.After); Assert.AreEqual(1, pp.CommandCost);
            if (move.Id is 12 or 32) Assert.AreEqual(0, result.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);
            AssertReadOnly(result.State, result.Trace, () => AssertDispatches(result.Events, move.Id, actor, target));
        }
    }

    [TestMethod]
    public void FuryRetainsAllIndependentOrdinaryMultiHitCountAndRngVectorsAtEveryPosition()
    {
        using var source = JsonDocument.Parse(File.ReadAllBytes(TestAssets.GeneratedFile("tests/Game.Battle.Tests/Moves/source-vectors.json", "python tests/Game.Battle.Tests/Moves/source_probe.py content/raw/pokefirered/037335f4 tests/Game.Battle.Tests/Moves --fixtures-only")));
        Assert.AreEqual(PokemonFactory.SourceCommit, source.RootElement.GetProperty("sourceCommit").GetString());
        var vectors = source.RootElement.GetProperty("multihit"); Assert.AreEqual(48, vectors.GetArrayLength());
        HashSet<int> plans = [];
        foreach (var vector in vectors.EnumerateArray())
        for (int actor = 0; actor < 4; actor++)
        {
            var result = Resolve(Frame(Bodies(31, actor), vector.GetProperty("seed").GetUInt32()), actor, actor ^ 1);
            var summary = result.Events.OfType<SourceMultiHitEvent>().Single(); plans.Add(summary.PlannedHits);
            Assert.AreEqual(vector.GetProperty("hits").GetInt32(), summary.PlannedHits);
            Assert.AreEqual(summary.PlannedHits, summary.ActualHits);
            Assert.AreEqual(vector.GetProperty("draws").GetInt32(), result.Trace.Length);
            Assert.AreEqual(vector.GetProperty("finalSeed").GetUInt32(), result.State.Rng.Seed);
            Assert.AreEqual(1, result.Trace.Count(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)),
                "Ordinary Fury Attack has one initial accuracy check, unlike Triple Kick.");
            AssertReadOnly(result.State, result.Trace, () => AssertDispatches(result.Events, 31, actor, actor ^ 1));
        }
        CollectionAssert.AreEquivalent(new[] { 2, 3, 4, 5 }, plans.ToArray());
    }

    [TestMethod]
    public void SubstituteBreakAndEarlyTargetKoRetainOnlyCommittedReceiptOwnership()
    {
        foreach (int id in new[] { 30, 31, 32, 12 })
        for (int actor = 0; actor < 4; actor++)
        foreach (bool ko in new[] { false, true })
        {
            var bodies = Bodies(id, actor); int target = actor ^ 1;
            bodies[target] = ko ? bodies[target] with { Pokemon = bodies[target].Pokemon with { CurrentHp = 1 } }
                : bodies[target] with { CalledVolatile = new() { SubstituteHp = 1 } };
            // Seed zero gives source tryKO roll 1; no accuracy-stage override can force an OHKO.
            var result = Resolve(Frame(bodies, 0), actor, target);
            if (ko)
            {
                Assert.AreEqual(0, result.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);
                Assert.AreEqual(target, result.Events.OfType<SourceFaintedEvent>().Single().Side.Value);
                if (id == 31) Assert.AreEqual(1, result.Events.OfType<SourceMultiHitEvent>().Single().ActualHits);
            }
            else
            {
                var substitute = result.Events.OfType<SourceSubstituteDamagedEvent>().Single();
                Assert.AreEqual(1, substitute.HpBefore); Assert.AreEqual(0, substitute.HpAfter);
                if (id == 31)
                {
                    Assert.IsTrue(result.Events.OfType<SourceHpChangedEvent>().Any());
                    var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
                    Assert.AreEqual(summary.PlannedHits, summary.ActualHits);
                }
                else Assert.AreEqual(bodies[target].Pokemon.CurrentHp, result.State.Slots[target].Body!.Pokemon.CurrentHp);
            }
            AssertReadOnly(result.State, result.Trace, () => AssertDispatches(result.Events, id, actor, target));
        }
    }

    [TestMethod]
    public void OneHpEndureCommitsAZeroLossVisualAndStopsOrdinaryMultihit()
    {
        foreach (int id in new[] { 30, 31, 32, 12 })
        for (int actor = 0; actor < 4; actor++)
        {
            var bodies = Bodies(id, actor); int target = actor ^ 1;
            bodies[target] = bodies[target] with { Enduring = true, Pokemon = bodies[target].Pokemon with { CurrentHp = 1 } };
            var result = Resolve(Frame(bodies, 0), actor, target);
            var hp = result.Events.OfType<SourceHpChangedEvent>().Single();
            Assert.AreEqual(1, hp.HpBefore); Assert.AreEqual(1, hp.HpAfter); Assert.AreEqual(0, hp.Damage);
            Assert.IsTrue(result.Events.OfType<SourceDamageAdjustedEvent>().Single().Endured);
            Assert.AreEqual(0, result.Events.OfType<SourceFaintedEvent>().Count());
            if (id == 31) Assert.AreEqual(1, result.Events.OfType<SourceMultiHitEvent>().Single().ActualHits);
            AssertReadOnly(result.State, result.Trace, () => AssertDispatches(result.Events, id, actor, target));
        }
    }

    [TestMethod]
    [DataRow(32),DataRow(12)]
    public void HornDrillEndureAppliesBehindSubstituteAndKeepsTheZeroLossSubstituteReceipt(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var bodies = Bodies(id, actor); int target = actor ^ 1;
            bodies[target] = bodies[target] with { Enduring = true, Pokemon = bodies[target].Pokemon with { CurrentHp = 1 },
                CalledVolatile = new() { SubstituteHp = 1 } };
            var result = Resolve(Frame(bodies, 0), actor, target);
            var sub = result.Events.OfType<SourceSubstituteDamagedEvent>().Single();
            Assert.AreEqual(1, sub.HpBefore); Assert.AreEqual(1, sub.HpAfter); Assert.AreEqual(0, sub.Damage);
            Assert.AreEqual(0, result.Events.OfType<SourceHpChangedEvent>().Count());
            Assert.IsTrue(result.Events.OfType<SourceDamageAdjustedEvent>().Single().Endured);
            AssertDispatches(result.Events, id, actor, target);
        }
    }

    [TestMethod]
    public void ProtectAndNaturalGhostImmunityNeverCreateAnInvocation()
    {
        foreach (int id in new[] { 30, 31, 32, 12 })
        for (int actor = 0; actor < 4; actor++)
        foreach (bool protect in new[] { false, true })
        {
            var bodies = Bodies(id, actor); int target = actor ^ 1;
            bodies[target] = protect ? bodies[target] with { Protected = true } : NaturalMon("GASTLY", 100, "LICK");
            var result = Resolve(Frame(bodies, 0), actor, target);
            if (!protect)
            {
                Assert.IsTrue(result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
                if (id == 30) Assert.AreEqual(0, result.Events.OfType<SourceHpChangedEvent>().Single().Damage);
                else Assert.AreEqual(0, Hits(result.Events).Length);
                if (id == 31) Assert.AreEqual(0, result.Events.OfType<SourceMultiHitEvent>().Single().ActualHits);
            }
            else Assert.AreEqual(0, Hits(result.Events).Length);
            if (id is 12 or 32) Assert.IsFalse(result.Trace.Any(t => t.Purpose.EndsWith(".ohko", StringComparison.Ordinal)),
                "Protect/type immunity branches before Cmd_tryKO.");
            AssertReadOnly(result.State, result.Trace, () => AssertDenied(result.Events, protect ? "Protect" : "Normal/Ghost immunity"));
        }
    }

    [TestMethod]
    public void OrdinaryAccuracyMissDoesNotCreateAHornInvocation()
    {
        foreach (int id in new[] { 30, 31 })
        for (int actor = 0; actor < 4; actor++)
        {
            var bodies = Bodies(id, actor); bodies[actor] = bodies[actor] with { AccuracyStage = 0 };
            var result = Find(bodies, actor, actor ^ 1,
                r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "ordinary horn accuracy miss").Result;
            Assert.AreEqual(0, Hits(result.Events).Length);
            Assert.AreEqual(0, result.Events.OfType<SourceMultiHitEvent>().Count(), "Initial miss precedes the ordinary multihit plan.");
            AssertDenied(result.Events, "initial accuracy miss");
        }
    }

    [TestMethod]
    [DataRow(32,0, 1, true),DataRow(12,0,1,true)]
    [DataRow(32,14, 29, true),DataRow(12,14,29,true)]
    [DataRow(32,193, 30, false),DataRow(12,193,30,false)]
    [DataRow(32,116, 100, false),DataRow(12,116,100,false)]
    public void HornDrillUsesStrictOriginalOhkoChanceAndNeverOrdinaryAccuracy(int id, int seed, int sourceRoll, bool succeeds)
    {
        // Source Random + verbatim Cmd_tryKO reference, recorded in horn-dispatch source audit.
        // Equal levels: Random()%100+1 < 30, not <=30. Accuracy stages are irrelevant.
        for (int actor = 0; actor < 4; actor++)
        foreach (int accuracyStage in new[] { 0, 12 })
        {
            var bodies = Bodies(id, actor); bodies[actor] = bodies[actor] with { AccuracyStage = accuracyStage };
            var result = Resolve(Frame(bodies, (uint)seed), actor, actor ^ 1);
            var ohko = result.Trace.Single(t => t.Purpose.EndsWith(".ohko", StringComparison.Ordinal));
            Assert.AreEqual(sourceRoll, ohko.Raw % 100 + 1);
            Assert.IsFalse(result.Trace.Any(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)));
            if (succeeds)
            {
                var hp = result.Events.OfType<SourceHpChangedEvent>().Single();
                Assert.AreEqual(hp.HpBefore, hp.Damage); Assert.AreEqual(0, hp.HpAfter);
                Assert.AreEqual(2, result.Trace.Length, "Successful tryKO reaches zero-chance seteffectwithchance.");
                AssertReadOnly(result.State, result.Trace, () => AssertDispatches(result.Events, id, actor, actor ^ 1));
            }
            else
            {
                Assert.AreEqual("ohko-miss", result.Events.OfType<SourceMoveFailedEvent>().Single().Reason);
                Assert.AreEqual(0, Hits(result.Events).Length); Assert.AreEqual(1, result.Trace.Length);
                AssertReadOnly(result.State, result.Trace, () => AssertDenied(result.Events, "OHKO chance failure"));
            }
        }
    }

    [TestMethod]
    [DataRow(32),DataRow(12)]
    public void HornDrillSturdyAndHigherTargetLevelAreFailedInvocations(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (bool sturdy in new[] { false, true })
        {
            var bodies = Bodies(id, actor); int target = actor ^ 1;
            if (sturdy)
            {
                bodies[target] = NaturalMon("PINECO", 100, "TACKLE");
                Assert.AreEqual("ABILITY_STURDY", bodies[target].Pokemon.AbilityId);
            }
            else bodies[actor] = NaturalMon(id==12?"KRABBY":"RHYHORN",id==12?34:38,id==12?"GUILLOTINE":"HORN_DRILL");
            var result = Resolve(Frame(bodies, 0), actor, target);
            Assert.AreEqual(sturdy ? "ohko-sturdy" : "ohko-level", result.Events.OfType<SourceMoveFailedEvent>().Single().Reason);
            Assert.AreEqual(0, Hits(result.Events).Length);
            Assert.AreEqual(sturdy ? 0 : 1, result.Trace.Count(t => t.Purpose.EndsWith(".ohko", StringComparison.Ordinal)));
            if (sturdy) Assert.AreEqual("ABILITY_STURDY", result.Events.OfType<SourceAbilityPreventedEvent>().Single().Ability);
            AssertReadOnly(result.State, result.Trace, () => AssertDenied(result.Events, sturdy ? "Sturdy" : "higher target level"));
        }
    }

    [TestMethod]
    public void ContactRoughSkinActorKoStopsFuryAfterItsOneCommittedHit()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(31, actor);
            bodies[actor] = bodies[actor] with { Pokemon = bodies[actor].Pokemon with { CurrentHp = 1 } };
            bodies[target] = NaturalMon("SHARPEDO", 100, "BITE");
            Assert.AreEqual("ABILITY_ROUGH_SKIN", bodies[target].Pokemon.AbilityId);
            var result = Resolve(Frame(bodies, 0), actor, target);
            var hit = result.Events.OfType<SourceHpChangedEvent>().Single();
            var contact = result.Events.OfType<SourceAbilityDamageEvent>().Single();
            var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
            Assert.AreEqual(target, contact.Holder.Value); Assert.AreEqual(actor, contact.Target.Value);
            Assert.AreEqual(1, contact.HpBefore); Assert.AreEqual(0, contact.HpAfter);
            Assert.IsTrue(hit.Sequence < contact.Sequence && contact.Sequence < summary.Sequence);
            Assert.AreEqual(1, summary.ActualHits); Assert.AreEqual(actor, result.Events.OfType<SourceFaintedEvent>().Single().Side.Value);
            AssertReadOnly(result.State, result.Trace, () => AssertDispatches(result.Events, 31, actor, target));
        }
    }

    [TestMethod]
    public void FuryFocusBandZeroLossDoesNotReduceCommittedHitCount()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var bodies = Bodies(31, actor); int target = actor ^ 1;
            bodies[target] = bodies[target] with { HeldItem = "ITEM_FOCUS_BAND", Pokemon = bodies[target].Pokemon with { CurrentHp = 1 } };
            var result = Find(bodies, actor, target, r => {
                var summary = r.Events.OfType<SourceMultiHitEvent>().Single();
                return summary.ActualHits == summary.PlannedHits && r.Events.OfType<SourceHpChangedEvent>().All(h => h.HpAfter == 1);
            }, "Focus Band survives all committed Fury Attack hits").Result;
            Assert.IsTrue(result.Events.OfType<SourceHpChangedEvent>().All(h => h.HpBefore == 1 && h.HpAfter == 1 && h.Damage == 0));
            AssertDispatches(result.Events, 31, actor, target);
        }
    }

}

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

// Natural source learners: Mankey16 and Makuhita10. Controlled hit/HP states are fixtures.
[TestClass, TestCategory("Game")]
public sealed class MultiStrikeMoveAnimationDispatchTests
{
    private static MoveId Move(int id)=>Id(id==154?"FURY_SWIPES":"ARM_THRUST");
    private static MoveId Id(string name) => new("pret:MOVE_" + name);
    private static SourceMechanicsCatalog Mechanics =>
        TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("multi-strike", new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("multi-strike", null!, new(), events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(), index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []) { SourceEvents = events };
    private static SourceBattleEvent[] Hits(ImmutableArray<SourceBattleEvent> events) =>
        events.Where(e => e is SourceHpChangedEvent or SourceSubstituteDamagedEvent).ToArray();

    private static void AssertDispatches(ImmutableArray<SourceBattleEvent> events, int actor, int target)
    {
        var hits = Hits(events); Assert.IsTrue(hits.Length > 0);
        var used = events.OfType<SourceMoveUsedEvent>().Single();var selected=used.Move;Assert.IsTrue(selected==Move(154)||selected==Move(292));
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        var summary = events.OfType<SourceMultiHitEvent>().Single();
        Assert.AreEqual(selected, summary.Move);
        Assert.AreEqual(selected, attack.ChosenMove); Assert.AreEqual(selected, attack.CurrentMove);
        Assert.AreEqual(selected, pp.ChosenMove); Assert.AreEqual(selected, pp.CurrentMove);
        Assert.AreEqual(actor, pp.PpOwner.Value); Assert.AreEqual(actor, pp.Actor.Value); Assert.AreEqual(target, pp.Target.Value);
        Assert.AreEqual(Mechanics.Catalog.Moves[selected].Pp, pp.Before); Assert.AreEqual(pp.Before-1, pp.After); Assert.AreEqual(1, pp.CommandCost);
        Assert.IsTrue(attack.Sequence < used.Sequence && used.Sequence < pp.Sequence && pp.Sequence < hits[0].Sequence);
        Assert.IsTrue(summary.PlannedHits is >= 2 and <= 5);
        Assert.IsTrue(summary.ActualHits >= 1 && summary.ActualHits <= summary.PlannedHits);
        Assert.AreEqual(hits.Length, summary.ActualHits); Assert.IsTrue(hits[^1].Sequence < summary.Sequence);
        string before = JsonSerializer.Serialize(events); int dispatchCount = 0;
        for (int index = 0; index < events.Length; index++)
        {
            var battle = Double(events, index);
            var text = AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On);
            var hit = AnimationDispatchFixture.AtHitResult(battle, SourceBattleScene.On);
            Assert.IsNull(text.Dispatch, "Attack text must not schedule an extra multi-strike root.");
            int ordinal = Array.FindIndex(hits, h => h.Sequence == events[index].Sequence);
            if (ordinal >= 0)
            {
                var dispatch = hit.Dispatch; Assert.IsNotNull(dispatch, hit.Reason); dispatchCount++;
                Assert.AreEqual(Mechanics.Catalog.Moves[selected].SourceMoveId, dispatch.SourceMoveId); Assert.AreEqual("multi-strike", dispatch.BattleId);
                Assert.AreEqual(actor, dispatch.Actor.Value); Assert.AreEqual(target, dispatch.Target.Value);
                Assert.AreEqual(hits[ordinal].Sequence, dispatch.Sequence);
                Assert.AreEqual(hits[ordinal].Sequence, dispatch.ResultSequence);
                Assert.AreEqual(attack.Sequence, dispatch.AttackSequence); Assert.AreEqual(pp.Sequence, dispatch.PpSequence);
                Assert.AreEqual((byte)ordinal, dispatch.MoveTurn);
                Assert.AreEqual(hits[ordinal] is SourceSubstituteDamagedEvent, dispatch.HitsSubstitute);
                Assert.IsFalse(dispatch.AcknowledgesCurrentReceipt, "Per-hit playback cannot submit an extra ACK.");
            }
            else Assert.IsNull(hit.Dispatch, "PP, used, summary, faint and result receipts cannot schedule another root.");
            Assert.AreEqual(hit, AnimationDispatchFixture.AtHitResult(battle, SourceBattleScene.On));
            if (actor < 2 && target < 2)
            {
                var single = Single(events, index);
                Assert.AreEqual(text.Dispatch, AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.On).Dispatch);
                Assert.AreEqual(hit.Dispatch, AnimationDispatchFixture.AtHitResult(single, true, SourceBattleScene.On).Dispatch);
            }
        }
        Assert.AreEqual(hits.Length, dispatchCount, "One invocation per committed hit, never per planned hit or visual sprite.");
        Assert.AreEqual(before, JsonSerializer.Serialize(events), "Projection cannot mutate committed events.");
    }
    private static void AssertDenied(ImmutableArray<SourceBattleEvent> events, string branch)
    {
        string before = JsonSerializer.Serialize(events);
        for (int index = 0; index < events.Length; index++)
        {
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events, index), SourceBattleScene.On).Dispatch, branch);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(Double(events, index), SourceBattleScene.On).Dispatch, branch);
            if (events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value > 1 || e.Target.Value > 1)) continue;
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, index), true, SourceBattleScene.On).Dispatch, branch);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(Single(events, index), true, SourceBattleScene.On).Dispatch, branch);
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
    private static WildBattleCombatant[] Bodies(int id,int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL") with { SpeedStage = 0 }).ToArray();
        bodies[actor] = (id==154?NaturalMon("MANKEY",16,"FURY_SWIPES"):NaturalMon("MAKUHITA",10,"ARM_THRUST")) with { AccuracyStage = 12, SpeedStage = 12 };
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
        var selected=frame.Slots[actor].Body!.Pokemon.Moves[0].Move;
        var choices = frame.Slots.Select(c => new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, 0,
            c.Slot.Value == actor ? new BattlerSlot(target) : c.Slot)).ToImmutableArray();
        var result = SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision, choices);
        for (int step = 0; step < 12; step++)
        {
            Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            if (result.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == actor && e.Move == selected)) return result;
            Assert.IsNotNull(result.Continuation, "multi-strike must execute within this public ordered turn.");
            result = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
        }
        throw new InvalidOperationException("The natural multi-strike did not execute in one bounded public turn.");
    }
    private static JsonElement[] SourceVectors()
    {
        using var source = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root, "tests/Game.Battle.Tests/Moves/source-vectors.json")));
        Assert.AreEqual(PokemonFactory.SourceCommit, source.RootElement.GetProperty("sourceCommit").GetString());
        var vectors = source.RootElement.GetProperty("multihit").EnumerateArray().Select(v => v.Clone()).ToArray();
        Assert.IsTrue(vectors.Length > 0);
        CollectionAssert.AreEquivalent(new[] { 2, 3, 4, 5 }, vectors.Select(v => v.GetProperty("hits").GetInt32()).Distinct().ToArray());
        return vectors;
    }

    [TestMethod][DataRow(154)][DataRow(292)]
    public void NaturalMultiStrikeUsesUnchangedSourceMetadataAndOrderedTurnsAtEverySlotAndSelectedAlly(int id)
    {
        var move = Mechanics.Catalog.Moves[Move(id)];
        Assert.AreEqual("EFFECT_MULTI_HIT", move.Effect); Assert.AreEqual("MOVE_TARGET_SELECTED", move.Target);
        Assert.AreEqual(id==154?"TYPE_NORMAL":"TYPE_FIGHTING",move.Type);Assert.AreEqual(id==154?18:15,move.Power);Assert.AreEqual(id==154?80:100,move.Accuracy);
        Assert.AreEqual(id==154?15:20,move.Pp); Assert.AreEqual(0, move.SecondaryEffectChance);
        Assert.IsTrue(move.Flags.Contains("FLAG_MAKES_CONTACT")); Assert.IsTrue(move.Flags.Contains("FLAG_KINGS_ROCK_AFFECTED"));
        Assert.IsTrue(move.Flags.Contains("FLAG_PROTECT_AFFECTED"));
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        {
            var result = Ordered(Frame(Bodies(id,actor), 31), actor, target);
            Assert.AreEqual(1, result.Trace.Count(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)));
            Assert.AreEqual(1, result.Trace.Count(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal)));
            AssertReadOnly(result.State, result.Trace, () => AssertDispatches(result.Events, actor, target));
        }
    }

    [TestMethod][DataRow(154)][DataRow(292)]
    public void EveryOrdinarySourceHitCountRetainsIndependentRngAndCommittedOrdinalsAtEverySlot(int id)
    {
        // Reuse one independent source vector for each count; do not generate a product-output golden.
        foreach (var vector in SourceVectors().GroupBy(v => v.GetProperty("hits").GetInt32()).Select(g => g.First()))
        for (int actor = 0; actor < 4; actor++)
        {
            var result = Resolve(Frame(Bodies(id,actor), vector.GetProperty("seed").GetUInt32()), actor, actor ^ 1);
            var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
            Assert.AreEqual(vector.GetProperty("hits").GetInt32(), summary.PlannedHits);
            Assert.AreEqual(summary.PlannedHits, summary.ActualHits);
            Assert.AreEqual(vector.GetProperty("draws").GetInt32(), result.Trace.Length);
            Assert.AreEqual(vector.GetProperty("finalSeed").GetUInt32(), result.State.Rng.Seed);
            Assert.AreEqual(1, result.Trace.Count(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)));
            AssertReadOnly(result.State, result.Trace, () => AssertDispatches(result.Events, actor, actor ^ 1));
        }
    }

    [TestMethod][DataRow(154)][DataRow(292)]
    public void SurvivingAndBrokenSubstitutesKeepTheSameCommittedHitOrdinalsAtEverySlot(int id)
    {
        uint seed = SourceVectors().First(v => v.GetProperty("hits").GetInt32() == 5).GetProperty("seed").GetUInt32();
        for (int actor = 0; actor < 4; actor++)
        foreach (bool survives in new[] { true, false })
        {
            int target = actor ^ 1; var bodies = Bodies(id,actor);
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

    [TestMethod][DataRow(154)][DataRow(292)]
    public void EarlyKoAndEndureTruncateOnlyUncommittedHitsAtEverySlot(int id)
    {
        uint seed = SourceVectors().First(v => v.GetProperty("hits").GetInt32() == 5).GetProperty("seed").GetUInt32();
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1;
            var full = Resolve(Frame(Bodies(id,actor), seed), actor, target);
            int firstDamage = full.Events.OfType<SourceHpChangedEvent>().First().Damage;
            Assert.IsTrue(firstDamage > 0);
            foreach (string branch in new[] { "first-hit-ko", "second-hit-ko", "endure" })
            {
                var bodies = Bodies(id,actor); int hp = branch == "second-hit-ko" ? firstDamage + 1 : 1;
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

    [TestMethod][DataRow(154)][DataRow(292)]
    public void ProtectAndRealAccuracyMissNeverScheduleMultiStrikeAtAnySlot(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(id,actor);
            bodies[target] = bodies[target] with { Protected = true };
            var blocked = Resolve(Frame(bodies, 31), actor, target);
            Assert.AreEqual(0, Hits(blocked.Events).Length);
            Assert.AreEqual("protected", blocked.Events.OfType<SourceMoveFailedEvent>().Single().Reason);
            Assert.AreEqual(0, blocked.Events.OfType<SourceMultiHitEvent>().Count());
            AssertReadOnly(blocked.State, blocked.Trace, () => AssertDenied(blocked.Events, "Protect"));
            bodies = Bodies(id,actor); bodies[actor] = bodies[actor] with { AccuracyStage = 0 };
            var entered = Frame(bodies, 0); SourceKernelResult? missed = null;
            // Bounded branch discovery; failure to observe a real miss fails the test.
            for (uint seed = 0; seed < 1024; seed++)
            {
                var result = Resolve(entered with { Rng = new(seed) }, actor, target);
                if (result.Events.OfType<SourceMoveMissedEvent>().Any()) { missed = result; break; }
            }
            Assert.IsNotNull(missed, "No real multi-strike accuracy miss in seeds 0..1023.");
            Assert.AreEqual(0, Hits(missed.Events).Length);
            Assert.AreEqual(0, missed.Events.OfType<SourceMultiHitEvent>().Count());
            AssertReadOnly(missed.State, missed.Trace, () => AssertDenied(missed.Events, "accuracy miss"));
        }
    }

    [TestMethod][DataRow(154)][DataRow(292)]
    public void IncompleteOrMalformedCommittedGroupsFailClosedAndSceneCursorGatesRemainIntact(int id)
    {
        var result = Resolve(Frame(Bodies(id), 6), 0, 1); var events = result.Events;
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
            foreach (var invalid in malformed) AssertDenied(invalid, "incomplete or malformed multi-strike group");
            AssertDispatches(events, 0, 1);
            int index = events.IndexOf(Hits(events)[0]); var single = Single(events, index); var twice = Double(events, index);
            Assert.IsNotNull(AnimationDispatchFixture.AtHitResult(single, true, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(single, false, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(single, true, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(single with { Phase = CampaignBattlePhase.ChooseMove }, true, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(single with { EventIndex = -1 }, true, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(single with { EventIndex = events.Length }, true, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(twice, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(twice with { Stage = CampaignDoubleBattleStage.Finished }, SourceBattleScene.On).Dispatch);
        });
    }
    [TestMethod][DataRow(154)][DataRow(292)]
    public void PrimaryGhostImmunityCannotBorrowZeroLossReceipts(int id)
    {
        for(int actor=0;actor<4;actor++)
        {
            int target=actor^1;var bodies=Bodies(id,actor);bodies[target]=NaturalMon("GASTLY",100,"MEAN_LOOK");var result=Resolve(Frame(bodies,31),actor,target);
            Assert.IsTrue(result.Events.OfType<SourceEffectivenessEvent>().Any(e=>e.Effectiveness==WildBattleEffectiveness.NoEffect));Assert.AreEqual(bodies[target].Pokemon.CurrentHp,result.State.Slots[target].Body!.Pokemon.CurrentHp);AssertReadOnly(result.State,result.Trace,()=>AssertDenied(result.Events,"Ghost immunity"));
        }
    }
    [TestMethod][DataRow(154)][DataRow(292)]
    public void FirstContactKnockoutStopsUncommittedHitsButKeepsTheCommittedOrdinal(int id)
    {
        uint seed=SourceVectors().First(v=>v.GetProperty("hits").GetInt32()==5).GetProperty("seed").GetUInt32();
        for(int actor=0;actor<4;actor++)
        {
            int target=actor^1;var bodies=Bodies(id,actor);bodies[actor]=bodies[actor] with{Pokemon=bodies[actor].Pokemon with{CurrentHp=1}};bodies[target]=NaturalMon("SHARPEDO",100,"FOCUS_ENERGY");var result=Resolve(Frame(bodies,seed),actor,target);
            var summary=result.Events.OfType<SourceMultiHitEvent>().Single();Assert.AreEqual(5,summary.PlannedHits);Assert.AreEqual(1,summary.ActualHits);var contact=result.Events.OfType<SourceAbilityDamageEvent>().Single();Assert.AreEqual(actor,contact.Target.Value);Assert.AreEqual(0,contact.HpAfter);AssertReadOnly(result.State,result.Trace,()=>AssertDispatches(result.Events,actor,target));
        }
    }
}

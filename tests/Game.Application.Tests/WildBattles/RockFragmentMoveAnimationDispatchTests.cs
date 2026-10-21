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

// Actual source-compatible learners and public kernel receipts. Rock Smash is an
// HM06-compatible Geodude, not an invented level-up learner; Rock Blast is learned
// at level 31. Controlled HP, stages, volatile state and RNG are integration fixtures,
// not natural-route evidence. No source receipt is synthesized for these cases.
[TestClass, TestCategory("Game")]
public sealed class RockFragmentMoveAnimationDispatchTests
{
    private static readonly (string Name, int Id, int Pp)[] Moves =
        [("ROCK_SMASH", 249, 15), ("ROCK_BLAST", 350, 10)];
    private static MoveId Id(string name) => new("pret:MOVE_" + name);
    private static SourceMechanicsCatalog Mechanics =>
        TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("rock-fragments", new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("rock-fragments", null!, new(), events, index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []);
    private static SourceBattleEvent[] Hits(ImmutableArray<SourceBattleEvent> events) =>
        events.Where(e => e is SourceHpChangedEvent or SourceSubstituteDamagedEvent).ToArray();
    private static SourceStatChangedEvent[] DefenseWaves(ImmutableArray<SourceBattleEvent> events, int actor, int target) =>
        events.OfType<SourceStatChangedEvent>().Where(s => s.Actor.Value == actor && s.Target.Value == target &&
            s.Stat == WildBattleStat.Defense).ToArray();
    private static void AssertProjection(SourceKernelResult result, int id, int actor, int target, bool blocked = false) =>
        AssertProjection(result.Events, result.State, result.Trace, id, actor, target, blocked);
    private static void AssertProjection(ImmutableArray<SourceBattleEvent> events, SourceBattleFrame state,
        ImmutableArray<RandomTrace> trace, int id, int actor, int target, bool blocked = false)
    {
        string before = JsonSerializer.Serialize(events), frame = JsonSerializer.Serialize(state), random = JsonSerializer.Serialize(trace);
        var rng = state.Rng; long revision = state.Revision, eventSequence = state.EventSequence;
        var move = Moves.Single(m => m.Id == id); bool perHit = id == 350;
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        Assert.AreEqual(Id(move.Name), used.Move); Assert.AreEqual(actor, used.Actor.Value); Assert.AreEqual(target, used.Target.Value);
        Assert.AreEqual(used.Move, attack.ChosenMove); Assert.AreEqual(used.Move, attack.CurrentMove);
        Assert.AreEqual(used.Move, pp.ChosenMove); Assert.AreEqual(used.Move, pp.CurrentMove);
        Assert.AreEqual(actor, pp.Actor.Value); Assert.AreEqual(actor, pp.PpOwner.Value); Assert.AreEqual(target, pp.Target.Value);
        Assert.AreEqual(move.Pp, pp.Before); Assert.AreEqual(move.Pp - 1, pp.After); Assert.AreEqual(1, pp.CommandCost);
        Assert.IsTrue(attack.Sequence < used.Sequence && used.Sequence < pp.Sequence);
        var hits = Hits(events); var waves = DefenseWaves(events, actor, target);
        Assert.IsTrue(waves.Length <= 1); if (perHit || blocked) Assert.AreEqual(0, waves.Length);
        if (!blocked)
        {
            Assert.IsTrue(hits.Length > 0); Assert.IsTrue(pp.Sequence < hits[0].Sequence);
            if (perHit)
            {
                var summary = events.OfType<SourceMultiHitEvent>().Single();
                Assert.IsTrue(summary.PlannedHits is >= 2 and <= 5);
                Assert.AreEqual(hits.Length, summary.ActualHits);
                Assert.IsTrue(summary.ActualHits >= 1 && summary.ActualHits <= summary.PlannedHits);
                Assert.IsTrue(hits[^1].Sequence < summary.Sequence);
            }
            else { Assert.AreEqual(1, hits.Length); Assert.AreEqual(0, events.OfType<SourceMultiHitEvent>().Count()); }
        }
        int roots = 0, acknowledgements = 0, statCount = 0;
        for (int index = 0; index < events.Length; index++)
        {
            // Empty presentation Frame: committed receipts alone determine ownership.
            var battle = Double(events, index);
            var text = AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On);
            var hit = AnimationDispatchFixture.AtResult(battle, SourceBattleScene.On);
            Assert.AreEqual(text, AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On));
            Assert.AreEqual(hit, AnimationDispatchFixture.AtResult(battle, SourceBattleScene.On));
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtResult(battle, SourceBattleScene.Off).Dispatch);
            int ordinal = Array.FindIndex(hits, h => h.Sequence == events[index].Sequence);
            bool eligible = !blocked && (perHit ? ordinal >= 0 : events[index] == used);
            var decision = perHit ? hit : text;
            Assert.IsNull((perHit ? text : hit).Dispatch, "One invocation cannot acquire both receipt ownership models.");
            if (eligible)
            {
                var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); roots++;
                var receipt = perHit ? hits[ordinal] : hits[0];
                Assert.AreEqual("rock-fragments", cue.BattleId); Assert.AreEqual(id, cue.SourceMoveId);
                Assert.AreEqual(actor, cue.Actor.Value); Assert.AreEqual(target, cue.Target.Value);
                Assert.AreEqual(perHit ? receipt.Sequence : used.Sequence, cue.Sequence);
                Assert.AreEqual(receipt.Sequence, cue.ResultSequence);
                Assert.AreEqual(attack.Sequence, cue.AttackSequence); Assert.AreEqual(pp.Sequence, cue.PpSequence);
                Assert.AreEqual(receipt is SourceSubstituteDamagedEvent, cue.HitsSubstitute);
                Assert.AreEqual(!perHit, cue.AcknowledgesCurrentReceipt);
                if (cue.AcknowledgesCurrentReceipt) acknowledgements++;
                if (perHit) Assert.AreEqual((byte)ordinal, cue.MoveTurn); else Assert.IsNull(cue.MoveTurn);
            }
            else Assert.IsNull(decision.Dispatch, "Miss, immunity, secondary, item, contact, summary and KO cannot replay the root.");

            var stat = AnimationDispatchFixture.StatAtResult(battle, moves: Mechanics.Catalog.Moves);
            Assert.AreEqual(stat, AnimationDispatchFixture.StatAtResult(battle, moves: Mechanics.Catalog.Moves));
            Assert.IsNull(AnimationDispatchFixture.StatAtResult(battle, true, Mechanics.Catalog.Moves));
            if (waves.Any(s => s.Sequence == events[index].Sequence))
            {
                Assert.IsNotNull(stat, "The actual Rock Smash target Defense receipt owns its independent shared wave."); statCount++;
                Assert.AreEqual(249, stat.SourceMoveId); Assert.AreEqual("rock-fragments", stat.BattleId);
                Assert.AreEqual(events[index].Sequence, stat.Sequence); Assert.AreEqual(target, stat.Affected.Value);
                Assert.AreEqual(23, stat.AnimationArgument); Assert.AreEqual(attack.Sequence, stat.AttackSequence);
                Assert.AreEqual(pp.Sequence, stat.PpSequence); Assert.IsTrue(hits[0].Sequence < stat.Sequence);
                Assert.AreEqual(-1, waves.Single().After - waves.Single().Before);
            }
            else Assert.IsNull(stat, "Only the actual move-owned Defense reduction may own this secondary wave.");
            if (actor < 2 && target < 2)
            {
                var single = Single(events, index);
                Assert.AreEqual(text.Dispatch, AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.On).Dispatch);
                Assert.AreEqual(hit.Dispatch, AnimationDispatchFixture.AtResult(single, true, SourceBattleScene.On).Dispatch);
                Assert.AreEqual(stat, AnimationDispatchFixture.StatAtResult(single, true, moves: Mechanics.Catalog.Moves));
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(single, false, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtResult(single, false, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.Off).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtResult(single, true, SourceBattleScene.Off).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.StatAtResult(single, false, moves: Mechanics.Catalog.Moves));
                Assert.IsNull(AnimationDispatchFixture.StatAtResult(single, true, true, Mechanics.Catalog.Moves));
            }
        }
        Assert.AreEqual(blocked ? 0 : perHit ? hits.Length : 1, roots);
        Assert.AreEqual(blocked || perHit ? 0 : 1, acknowledgements, "Per-hit playback adds no authoritative ACK.");
        Assert.AreEqual(waves.Length, statCount);
        Assert.AreEqual(before, JsonSerializer.Serialize(events)); Assert.AreEqual(frame, JsonSerializer.Serialize(state));
        Assert.AreEqual(random, JsonSerializer.Serialize(trace)); Assert.AreEqual(rng, state.Rng);
        Assert.AreEqual(revision, state.Revision); Assert.AreEqual(eventSequence, state.EventSequence);
    }
    private static WildBattleCombatant LearnedMon(string species, int level, string name)
    {
        var move = Id(name); var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        if (name == "ROCK_SMASH")
        {
            var machines = SharedMachineCatalog.LoadProject(TestAssets.Root); ItemId hm = new("pret:ITEM_HM06");
            var definition = machines.Definition(hm);
            Assert.AreEqual(move, definition.Move); Assert.AreEqual(249, definition.SourceMoveId); Assert.AreEqual(15, definition.MovePp);
            Assert.IsTrue(machines.Compatibility(hm, pokemon.Species).CanLearn, "The source must explicitly allow this HM06 learner.");
        }
        else Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level),
            $"Natural source level-up learnset required: {species}/{level}/{name}.");
        int pp = Mechanics.Catalog.Moves[move].Pp;
        // Select one legally learnable move; this does not claim to replay obtaining or teaching the HM.
        return new(pokemon with { Moves = [new(move, pp, pp)] });
    }
    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var move = Moves.Single(m => m.Id == id);
        var bodies = Enumerable.Range(0, 4).Select(_ => LearnedMon("SNORLAX", 100, "DEFENSE_CURL") with { SpeedStage = 0 }).ToArray();
        bodies[actor] = LearnedMon("GEODUDE", 31, move.Name) with { AccuracyStage = 12, SpeedStage = 12 };
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
        Assert.IsFalse(entry.Events.OfType<SourceHeldItemConsumedEvent>().Any(), "Test items must survive entry for their own suffix.");
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
            Assert.IsNotNull(result.Continuation, "The source-compatible rock move must execute in this public turn.");
            result = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
        }
        throw new InvalidOperationException("The selected rock move did not execute in one bounded public turn.");
    }
    private static SourceKernelResult Find(SourceBattleFrame frame, int actor, int target,
        Func<SourceKernelResult, bool> predicate, string branch)
    {
        // Bounded branch discovery only, never a product-output golden or synthetic receipt.
        for (uint seed = 0; seed < 1024; seed++)
        {
            var result = Resolve(frame with { Rng = new(seed) }, actor, target);
            if (predicate(result)) return result;
        }
        throw new InvalidOperationException($"No actual {branch} in 1024 action-start seeds.");
    }
    private static bool QualifyingSmashRoll(SourceKernelResult result) =>
        result.Trace.Single(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal)).Raw % 100 <= 50;

    [TestMethod]
    [DataRow(249)]
    [DataRow(350)]
    public void SourceCompatibleMovesOwnOrderedReceiptsAtEveryPositionAndSelectedAllies(int id)
    {
        var row = Moves.Single(m => m.Id == id); var move = Mechanics.Catalog.Moves[Id(row.Name)];
        Assert.AreEqual(id, move.SourceMoveId); Assert.AreEqual("MOVE_TARGET_SELECTED", move.Target);
        Assert.AreEqual(id == 249 ? "EFFECT_DEFENSE_DOWN_HIT" : "EFFECT_MULTI_HIT", move.Effect);
        Assert.AreEqual(id == 249 ? "TYPE_FIGHTING" : "TYPE_ROCK", move.Type);
        Assert.AreEqual(id == 249 ? 20 : 25, move.Power); Assert.AreEqual(id == 249 ? 100 : 80, move.Accuracy);
        Assert.AreEqual(id == 249 ? 50 : 0, move.SecondaryEffectChance);
        Assert.AreEqual(id == 249, move.Flags.Contains("FLAG_MAKES_CONTACT"));
        Assert.AreEqual(id == 350, move.Flags.Contains("FLAG_KINGS_ROCK_AFFECTED"));
        if (id == 350) Assert.IsTrue(Mechanics.Catalog.Species[new("pret:SPECIES_GEODUDE")].LevelUpMoves
            .Any(m => m.Move == move.Id && m.Level == 31));
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        {
            var result = Ordered(Frame(Bodies(id, actor), 31), actor, target);
            Assert.AreEqual(1, result.Trace.Count(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)));
            Assert.AreEqual(1, result.Trace.Count(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal)));
            AssertProjection(result.Events, result.State, result.Trace, id, actor, target);
        }
    }

    [TestMethod]
    public void RockBlastRetainsAllIndependentOrdinaryMultihitCountAndRngVectorsAtEveryPosition()
    {
        using var source = JsonDocument.Parse(File.ReadAllBytes(TestAssets.GeneratedFile("tests/Game.Battle.Tests/Moves/source-vectors.json", "python tests/Game.Battle.Tests/Moves/source_probe.py content/raw/pokefirered/037335f4 tests/Game.Battle.Tests/Moves --fixtures-only")));
        Assert.AreEqual(PokemonFactory.SourceCommit, source.RootElement.GetProperty("sourceCommit").GetString());
        var vectors = source.RootElement.GetProperty("multihit"); Assert.AreEqual(48, vectors.GetArrayLength());
        HashSet<int> plans = [];
        foreach (var vector in vectors.EnumerateArray())
        for (int actor = 0; actor < 4; actor++)
        {
            var result = Resolve(Frame(Bodies(350, actor), vector.GetProperty("seed").GetUInt32()), actor, actor ^ 1);
            var summary = result.Events.OfType<SourceMultiHitEvent>().Single(); plans.Add(summary.PlannedHits);
            Assert.AreEqual(vector.GetProperty("hits").GetInt32(), summary.PlannedHits);
            Assert.AreEqual(summary.PlannedHits, summary.ActualHits);
            Assert.AreEqual(vector.GetProperty("draws").GetInt32(), result.Trace.Length);
            Assert.AreEqual(vector.GetProperty("finalSeed").GetUInt32(), result.State.Rng.Seed);
            Assert.AreEqual(1, result.Trace.Count(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)),
                "Ordinary Rock Blast checks accuracy once, never once per hit.");
            AssertProjection(result, 350, actor, actor ^ 1);
        }
        CollectionAssert.AreEquivalent(new[] { 2, 3, 4, 5 }, plans.ToArray());
    }

    [TestMethod]
    public void ActualRockSmashTargetDefenseChangeOwnsOneSeparateWaveAtEveryPositionAndSelectedAlly()
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        {
            var result = Find(Frame(Bodies(249, actor), 0), actor, target,
                r => DefenseWaves(r.Events, actor, target).Length == 1, "Rock Smash Defense reduction");
            Assert.IsTrue(QualifyingSmashRoll(result));
            var stat = DefenseWaves(result.Events, actor, target).Single();
            Assert.AreEqual(6, stat.Before); Assert.AreEqual(5, stat.After);
            Assert.AreEqual(5, result.State.Slots[target].Body!.DefenseStage);
            AssertProjection(result, 249, actor, target);
        }
    }

    [TestMethod]
    [DataRow(249)]
    [DataRow(350)]
    public void ActualSubstituteBreakAndSurvivalRetainCommittedHitIdentity(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (bool breaks in new[] { false, true })
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor);
            bodies[target] = bodies[target] with { CalledVolatile = new() { SubstituteHp = breaks ? 1 : 100 } };
            var frame = Frame(bodies, 31);
            var result = id == 249 ? Find(frame, actor, target, QualifyingSmashRoll, "qualifying substitute secondary") : Resolve(frame, actor, target);
            var substitute = result.Events.OfType<SourceSubstituteDamagedEvent>().ToArray();
            Assert.IsTrue(substitute.Length > 0); Assert.AreEqual(breaks, substitute[^1].HpAfter == 0);
            Assert.AreEqual(0, DefenseWaves(result.Events, actor, target).Length);
            if (id == 350)
            {
                var summary = result.Events.OfType<SourceMultiHitEvent>().Single(); Assert.AreEqual(summary.PlannedHits, summary.ActualHits);
                Assert.AreEqual(breaks, result.Events.OfType<SourceHpChangedEvent>().Any());
            }
            else Assert.AreEqual(0, result.Events.OfType<SourceHpChangedEvent>().Count());
            AssertProjection(result, id, actor, target);
        }
    }

    [TestMethod]
    [DataRow(249)]
    [DataRow(350)]
    public void ActualEarlyTargetKoCannotCreateUncommittedRootsOrTargetDefenseWaves(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor);
            bodies[target] = bodies[target] with { Pokemon = bodies[target].Pokemon with { CurrentHp = 1 } };
            var frame = Frame(bodies, 31);
            var result = id == 249 ? Find(frame, actor, target, QualifyingSmashRoll, "qualifying KO secondary") : Resolve(frame, actor, target);
            Assert.AreEqual(0, result.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);
            Assert.AreEqual(target, result.Events.OfType<SourceFaintedEvent>().Single().Side.Value);
            Assert.AreEqual(0, DefenseWaves(result.Events, actor, target).Length);
            if (id == 350) Assert.AreEqual(1, result.Events.OfType<SourceMultiHitEvent>().Single().ActualHits);
            AssertProjection(result, id, actor, target);
        }
    }

    [TestMethod]
    [DataRow(249)]
    [DataRow(350)]
    public void ActualOneHpEndureOwnsAZeroLossRootAndOnlyAnActualSmashSecondary(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor);
            bodies[target] = bodies[target] with { Enduring = true, Pokemon = bodies[target].Pokemon with { CurrentHp = 1 } };
            var frame = Frame(bodies, 31);
            var result = id == 249 ? Find(frame, actor, target,
                r => DefenseWaves(r.Events, actor, target).Length == 1, "Endure Defense reduction") : Resolve(frame, actor, target);
            var hp = result.Events.OfType<SourceHpChangedEvent>().Single();
            Assert.AreEqual(1, hp.HpBefore); Assert.AreEqual(1, hp.HpAfter); Assert.AreEqual(0, hp.Damage);
            Assert.IsTrue(result.Events.OfType<SourceDamageAdjustedEvent>().Single().Endured);
            Assert.AreEqual(0, result.Events.OfType<SourceFaintedEvent>().Count());
            if (id == 350) Assert.AreEqual(1, result.Events.OfType<SourceMultiHitEvent>().Single().ActualHits);
            AssertProjection(result, id, actor, target);
        }
    }

    [TestMethod]
    [DataRow(249)]
    [DataRow(350)]
    public void ActualProtectAndInitialAccuracyMissProduceNeitherRootNorSecondary(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor);
            bodies[target] = bodies[target] with { Protected = true };
            var blocked = Resolve(Frame(bodies, 31), actor, target);
            Assert.AreEqual("protected", blocked.Events.OfType<SourceMoveFailedEvent>().Single().Reason);
            Assert.AreEqual(0, Hits(blocked.Events).Length); AssertProjection(blocked, id, actor, target, blocked: true);
            bodies = Bodies(id, actor); bodies[actor] = bodies[actor] with { AccuracyStage = 0 };
            var miss = Find(Frame(bodies, 0), actor, target, r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "initial accuracy miss");
            Assert.AreEqual(0, Hits(miss.Events).Length); Assert.AreEqual(0, miss.Events.OfType<SourceMultiHitEvent>().Count());
            AssertProjection(miss, id, actor, target, blocked: true);
        }
    }

    [TestMethod]
    public void NaturalGhostImmunityRejectsSmashWhileRockBlastReallyHitsWonderGuardShedinja()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(249, actor);
            bodies[target] = LearnedMon("GASTLY", 100, "LICK");
            var immune = Resolve(Frame(bodies, 31), actor, target);
            Assert.IsTrue(immune.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
            var zero = immune.Events.OfType<SourceHpChangedEvent>().Single();
            Assert.AreEqual(0, zero.Damage); Assert.AreEqual(zero.HpBefore, zero.HpAfter);
            AssertProjection(immune, 249, actor, target, blocked: true);
            // Rock has no natural type immunity in this source generation. Real
            // Shedinja is Rock-weak, so its Wonder Guard is not an immunity fixture.
            bodies = Bodies(350, actor); bodies[target] = LearnedMon("SHEDINJA", 100, "HARDEN");
            Assert.AreEqual("ABILITY_WONDER_GUARD", bodies[target].Pokemon.AbilityId);
            var effective = Resolve(Frame(bodies, 31), actor, target);
            Assert.AreEqual(0, effective.Events.OfType<SourceAbilityPreventedEvent>().Count());
            Assert.IsTrue(effective.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.SuperEffective));
            Assert.AreEqual(0, effective.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);
            Assert.AreEqual(1, effective.Events.OfType<SourceMultiHitEvent>().Single().ActualHits);
            AssertProjection(effective, 350, actor, target);
        }
    }

    [TestMethod]
    [DataRow("TENTACOOL", "BARRIER", "ABILITY_CLEAR_BODY")]
    [DataRow("TORKOAL", "IRON_DEFENSE", "ABILITY_WHITE_SMOKE")]
    [DataRow("CATERPIE", "STRING_SHOT", "ABILITY_SHIELD_DUST")]
    public void ActualQualifyingSmashSecondaryPreventionKeepsTheRootWithoutInventingAWave(string species, string move, string ability)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(249, actor); bodies[target] = LearnedMon(species, 100, move);
            Assert.AreEqual(ability, bodies[target].Pokemon.AbilityId);
            var result = Find(Frame(bodies, 0), actor, target, QualifyingSmashRoll, "qualifying " + ability);
            Assert.AreEqual(0, DefenseWaves(result.Events, actor, target).Length);
            Assert.AreEqual(0, result.Events.OfType<SourceAbilityPreventedEvent>().Count(), "The source secondary prevention is silent.");
            AssertProjection(result, 249, actor, target);
        }
    }

    [TestMethod]
    public void ActualFailedSmashRollDefenseFloorAndMistAreSilentWithoutCancellingTheRoot()
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (string branch in new[] { "roll", "floor", "mist" })
        {
            int target = actor ^ 1; var bodies = Bodies(249, actor);
            if (branch == "floor") bodies[target] = bodies[target] with { DefenseStage = 0 };
            var frame = Frame(bodies, 0);
            if (branch == "mist") frame = frame with { Sides = frame.Sides.SetItem(target & 1,
                frame.Sides[target & 1] with { MistTurnsRemaining = 5 }) };
            var result = Find(frame, actor, target, r => branch == "roll" ? !QualifyingSmashRoll(r) : QualifyingSmashRoll(r), "Smash " + branch);
            Assert.AreEqual(0, DefenseWaves(result.Events, actor, target).Length);
            Assert.AreEqual(0, result.Events.OfType<SourceStatAtMinimumEvent>().Count());
            Assert.AreEqual(0, result.Events.OfType<SourceMoveFailedEvent>().Count());
            AssertProjection(result, 249, actor, target);
        }
    }

    [TestMethod]
    [DataRow(249)]
    [DataRow(350)]
    public void SourceContactDistinctionCannotCreateExtraRootsWhenRoughSkinCouldKoTheActor(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor);
            bodies[actor] = bodies[actor] with { Pokemon = bodies[actor].Pokemon with { CurrentHp = 1 } };
            bodies[target] = LearnedMon("SHARPEDO", 100, "FOCUS_ENERGY");
            Assert.AreEqual("ABILITY_ROUGH_SKIN", bodies[target].Pokemon.AbilityId);
            var result = Resolve(Frame(bodies, 31), actor, target);
            Assert.AreEqual(id == 249 ? 0 : 1, result.State.Slots[actor].Body!.Pokemon.CurrentHp);
            Assert.AreEqual(id == 249 ? 1 : 0, result.Events.OfType<SourceAbilityDamageEvent>().Count());
            Assert.AreEqual(id == 249 ? 1 : 0, result.Events.OfType<SourceFaintedEvent>().Count(e => e.Side.Value == actor));
            AssertProjection(result, id, actor, target);
        }
    }

    [TestMethod]
    public void OrderedRockBlastKingsRockSuffixKeepsEveryActualHitAndNoExtraRoot()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(350, actor);
            bodies[actor] = bodies[actor] with { HeldItem = "ITEM_KINGS_ROCK" };
            var frame = Frame(bodies, 0); SourceFourActionResult? found = null;
            for (uint seed = 0; seed < 128; seed++)
            {
                var candidate = Ordered(frame with { Rng = new(seed) }, actor, target);
                if (candidate.Events.OfType<SourceFlinchEvent>().Any(e => e.Side.Value == target)) { found = candidate; break; }
            }
            Assert.IsNotNull(found, "Require a real ordered King's Rock flinch within 128 source seeds.");
            var summary = found.Events.OfType<SourceMultiHitEvent>().Single(); Assert.AreEqual(summary.PlannedHits, summary.ActualHits);
            Assert.AreEqual(summary.ActualHits + 1, found.Trace.Count(t => t.Purpose.EndsWith(".kings-rock", StringComparison.Ordinal)));
            AssertProjection(found.Events, found.State, found.Trace, 350, actor, target);
        }
    }

    [TestMethod]
    public void OrderedRockSmashWhiteHerbRestorationCannotBorrowTheMoveDefenseWave()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(249, actor);
            bodies[target] = bodies[target] with { SpeedStage = 6, HeldItem = "ITEM_WHITE_HERB" };
            var frame = Frame(bodies, 0); SourceFourActionResult? found = null;
            for (uint seed = 0; seed < 128; seed++)
            {
                var candidate = Ordered(frame with { Rng = new(seed) }, actor, target);
                if (candidate.Events.OfType<SourceHeldItemConsumedEvent>().Any(e => e.Item == "ITEM_WHITE_HERB")) { found = candidate; break; }
            }
            Assert.IsNotNull(found, "Require an actual ordered Rock Smash secondary followed by White Herb consumption.");
            var changed = DefenseWaves(found.Events, actor, target).Single();
            var restored = found.Events.OfType<SourceStatChangedEvent>().Single(s => s.Actor.Value == target && s.Target.Value == target && s.Stat == WildBattleStat.Defense);
            var item = found.Events.OfType<SourceHeldItemConsumedEvent>().Single(e => e.Item == "ITEM_WHITE_HERB");
            Assert.AreEqual(5, restored.Before); Assert.AreEqual(6, restored.After);
            Assert.IsNotNull(item.StatChanges); Assert.AreEqual(new SourceItemStatChange(WildBattleStat.Defense, 5, 6), item.StatChanges.Value.Single());
            Assert.IsTrue(changed.Sequence < restored.Sequence && restored.Sequence < item.Sequence);
            AssertProjection(found.Events, found.State, found.Trace, 249, actor, target);
            var held = CampaignHeldItemAnimationPresentation.AtResult(Double(found.Events, found.Events.IndexOf(restored)),
                SourceBattleScene.On, new MoveId?[4]);
            Assert.IsNotNull(held); Assert.AreEqual(restored.Sequence, held.Sequence); Assert.AreEqual(item.Sequence, held.ReceiptSequence);
        }
    }
}

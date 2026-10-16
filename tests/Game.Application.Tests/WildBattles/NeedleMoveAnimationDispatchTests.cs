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
    private static readonly (string Name, int Id, string Species, int Level, int Pp)[] Moves =
        [("POISON_STING", 40, "WEEDLE", 1, 35), ("TWINEEDLE", 41, "BEEDRILL", 20, 20),
         ("SPIKE_CANNON", 131, "CLOYSTER", 43, 15)];
    private static MoveId Id(string name) => new("pret:MOVE_" + name);
    private static SourceMechanicsCatalog Mechanics =>
        TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("needle-moves", new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("needle-moves", null!, new(), events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(), index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []) { SourceEvents = events };
    private static CampaignMoveAnimationDecision AtText(ImmutableArray<SourceBattleEvent> events, int index) =>
        CampaignMoveAnimationPresentation.AtAttackText(Double(events, index), SourceBattleScene.On);
    private static CampaignMoveAnimationDecision AtHit(ImmutableArray<SourceBattleEvent> events, int index) =>
        CampaignMoveAnimationPresentation.AtHitResult(Double(events, index), SourceBattleScene.On);
    private static SourceBattleEvent[] Hits(ImmutableArray<SourceBattleEvent> events) =>
        events.Where(e => e is SourceHpChangedEvent or SourceSubstituteDamagedEvent).ToArray();
    private static ImmutableArray<SourceBattleEvent> Number(IEnumerable<SourceBattleEvent> events) =>
        events.Select((e, i) => e with { Sequence = i + 1 }).ToImmutableArray();

    private static void AssertDispatches(ImmutableArray<SourceBattleEvent> events, int id, int actor = 0, int target = 1)
    {
        var hits = Hits(events); Assert.IsTrue(hits.Length > 0);
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        if (id == 40) { Assert.AreEqual(1, hits.Length); Assert.AreEqual(0, events.OfType<SourceMultiHitEvent>().Count()); }
        else
        {
            var summary = events.OfType<SourceMultiHitEvent>().Single();
            Assert.AreEqual(hits.Length, summary.ActualHits);
            Assert.IsTrue(summary.ActualHits is >= 1 && summary.ActualHits <= summary.PlannedHits);
            if (id == 41) Assert.AreEqual(2, summary.PlannedHits);
            else Assert.IsTrue(summary.PlannedHits is >= 2 and <= 5);
        }
        string before = JsonSerializer.Serialize(events); int count = 0;
        for (int index = 0; index < events.Length; index++)
        {
            var text = AtText(events, index); var hit = AtHit(events, index);
            int ordinal = Array.FindIndex(hits, h => h.Sequence == events[index].Sequence);
            bool eligible = id == 40 ? events[index] == used : ordinal >= 0;
            var decision = id == 40 ? text : hit;
            Assert.IsNull((id == 40 ? hit : text).Dispatch, "Only one receipt ownership model is allowed.");
            if (eligible)
            {
                var dispatch = decision.Dispatch; Assert.IsNotNull(dispatch, decision.Reason); count++;
                var receipt = id == 40 ? hits[0] : hits[ordinal];
                Assert.AreEqual(id, dispatch.SourceMoveId); Assert.AreEqual("needle-moves", dispatch.BattleId);
                Assert.AreEqual(actor, dispatch.Actor.Value); Assert.AreEqual(target, dispatch.Target.Value);
                Assert.AreEqual(id == 40 ? used.Sequence : receipt.Sequence, dispatch.Sequence);
                Assert.AreEqual(receipt.Sequence, dispatch.ResultSequence);
                Assert.AreEqual(attack.Sequence, dispatch.AttackSequence); Assert.AreEqual(pp.Sequence, dispatch.PpSequence);
                Assert.AreEqual(receipt is SourceSubstituteDamagedEvent, dispatch.HitsSubstitute);
                Assert.AreEqual(id == 40, dispatch.AcknowledgesCurrentReceipt);
                if (id == 40) Assert.IsNull(dispatch.MoveTurn); else Assert.AreEqual((byte)ordinal, dispatch.MoveTurn);
                Assert.AreEqual(decision, id == 40 ? AtText(events, index) : AtHit(events, index));
            }
            else Assert.IsNull(decision.Dispatch, "Secondary, summary and faint receipts cannot replay the needle root.");
            if (actor < 2 && target < 2)
            {
                var single = Single(events, index);
                Assert.AreEqual(text.Dispatch, CampaignMoveAnimationPresentation.AtAttackText(single, true, SourceBattleScene.On).Dispatch);
                Assert.AreEqual(hit.Dispatch, CampaignMoveAnimationPresentation.AtHitResult(single, true, SourceBattleScene.On).Dispatch);
            }
        }
        Assert.AreEqual(id == 40 ? 1 : hits.Length, count);
        Assert.AreEqual(before, JsonSerializer.Serialize(events), "Presentation may not mutate committed receipts.");
    }
    private static void AssertDenied(ImmutableArray<SourceBattleEvent> events, string branch)
    {
        for (int index = 0; index < events.Length; index++)
        {
            Assert.IsNull(AtText(events, index).Dispatch, branch + " text");
            Assert.IsNull(AtHit(events, index).Dispatch, branch + " hit");
            if (events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value > 1 || e.Target.Value > 1)) continue;
            Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(Single(events, index), true, SourceBattleScene.On).Dispatch, branch);
            Assert.IsNull(CampaignMoveAnimationPresentation.AtHitResult(Single(events, index), true, SourceBattleScene.On).Dispatch, branch);
        }
    }
    private static ImmutableArray<SourceBattleEvent> Group(int id, int actor = 0, int? target = null, int planned = 2, int? actual = null)
    {
        var definition = Moves.Single(m => m.Id == id); var move = Id(definition.Name);
        var a = new BattlerSlot(actor); var t = new BattlerSlot(target ?? (actor ^ 1));
        int count = id == 40 ? 1 : actual ?? planned;
        List<SourceBattleEvent> events = [
            new SourceAttackStringEvent(0, 7, a, move, move),
            new SourceMoveUsedEvent(0, 7, a, t, 0, move, definition.Pp, definition.Pp - 1),
            new SourcePpCommandEvent(0, 7, a, t, 0, move, move, definition.Pp, definition.Pp - 1, 1, 0) { PpOwner = a }];
        for (int i = 0; i < count; i++) events.Add(new SourceHpChangedEvent(0, 7, a, t, move, 100 - i * 5, 95 - i * 5, 5, false, 100));
        if (id != 40) events.Add(new SourceMultiHitEvent(0, 7, a, t, move, planned, count));
        return Number(events);
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
        bodies[actor] = NaturalMon(move.Species, move.Level, move.Name) with { AccuracyStage = 12, SpeedStage = 12 };
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
            Assert.IsNotNull(result.Continuation, "The natural needle move must execute within this public turn.");
            result = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
        }
        throw new InvalidOperationException("The selected needle move did not execute in one bounded public turn.");
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
        using var source = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root, "tests/Game.Battle.Tests/Moves/source-vectors.json")));
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
                ? CampaignMoveAnimationPresentation.AtAttackText(b, source, scene) : CampaignMoveAnimationPresentation.AtHitResult(b, source, scene);
            Assert.IsNotNull(Project(battle).Dispatch);
            Assert.IsNull(Project(battle, false).Dispatch); Assert.IsNull(Project(battle, scene: SourceBattleScene.Off).Dispatch);
            Assert.IsNull(Project(battle with { Phase = CampaignBattlePhase.ChooseMove }).Dispatch);
            Assert.IsNull(Project(battle with { EventIndex = -1 }).Dispatch); Assert.IsNull(Project(battle with { EventIndex = events.Length }).Dispatch);
            Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(Double(events, index), SourceBattleScene.Off).Dispatch);
            Assert.IsNull(CampaignMoveAnimationPresentation.AtHitResult(Double(events, index), SourceBattleScene.Off).Dispatch);
        }
    }
}

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
public sealed class WaterPulseMoveAnimationDispatchTests
{
    private static readonly MoveId Pulse = Id("WATER_PULSE");
    private static MoveId Id(string name) => new("pret:MOVE_" + name);
    private static SourceMechanicsCatalog Mechanics =>
        TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("water-pulse", new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("water-pulse", null!, new(), events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(), index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []) { SourceEvents = events };

    private static WildBattleCombatant NaturalMon(string species, int level, string name)
    {
        var move = Id(name);
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level),
            $"Natural source learnset required: {species}/{level}/{name}.");
        int pp = Mechanics.Catalog.Moves[move].Pp;
        return new(pokemon with { Moves = [new(move, pp, pp)] });
    }

    private static WildBattleCombatant[] Bodies(int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL")).ToArray();
        bodies[actor] = NaturalMon("MILOTIC", 20, "WATER_PULSE") with { AccuracyStage = 12 };
        return bodies;
    }

    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies)
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
        Assert.IsTrue(entry.Succeeded, entry.Fault);
        Assert.IsNull(SourceBattleIdentityRules.Validate(entry.State));
        return entry.State with { Rng = new(31) };
    }

    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor = 0, int target = 1)
    {
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault);
        Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        return result;
    }

    private static SourceKernelResult Find(SourceBattleFrame frame, Func<SourceKernelResult, bool> predicate, string branch)
    {
        for (uint seed = 0; seed < 256; seed++)
        {
            var result = Resolve(frame with { Rng = new(seed) });
            if (!predicate(result)) continue;
            Console.WriteLine($"Water Pulse observed {branch}: seed={seed}; " +
                string.Join(", ", result.Trace.Select(t => $"{t.Purpose}={t.Raw}")));
            return result;
        }
        throw new InvalidOperationException($"No observed {branch} in 256 actual Water Pulse action-start seeds.");
    }

    private static bool QualifyingSecondary(SourceKernelResult result) =>
        result.Trace.Any(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal) && t.Raw % 100 <= 20);
    private static bool HasDuration(SourceKernelResult result) =>
        result.Trace.Any(t => t.Purpose.EndsWith(".confusion-duration", StringComparison.Ordinal));

    private static void AssertProjection(SourceKernelResult result, int actor = 0, int target = 1)
    {
        string state = JsonSerializer.Serialize(result.State), trace = JsonSerializer.Serialize(result.Trace);
        var rng = result.State.Rng;
        AssertOne(result.Events, actor, target);
        AssertOne(result.Events, actor, target);
        Assert.AreEqual(state, JsonSerializer.Serialize(result.State));
        Assert.AreEqual(trace, JsonSerializer.Serialize(result.Trace));
        Assert.AreEqual(rng, result.State.Rng);
    }

    private static void AssertOne(ImmutableArray<SourceBattleEvent> events, int actor, int target)
    {
        var hit = events.Single(e => e is SourceHpChangedEvent or SourceSubstituteDamagedEvent);
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        string before = JsonSerializer.Serialize(events);
        int count = 0;
        Assert.AreEqual(0, events.OfType<SourceMultiHitEvent>().Count());
        for (int index = 0; index < events.Length; index++)
        {
            var battle = Double(events, index);
            var decision = AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On, moves: Mechanics.Catalog.Moves);
            Assert.AreEqual(decision, AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On),
                "Silent confusion prevention needs no move-specific exception or metadata bypass.");
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(battle, SourceBattleScene.On).Dispatch);
            if (events[index] == used)
            {
                var dispatch = decision.Dispatch;
                Assert.IsNotNull(dispatch, decision.Reason);
                count++;
                Assert.AreEqual(352, dispatch.SourceMoveId);
                Assert.AreEqual("water-pulse", dispatch.BattleId);
                Assert.AreEqual(actor, dispatch.Actor.Value);
                Assert.AreEqual(target, dispatch.Target.Value);
                Assert.AreEqual(used.Sequence, dispatch.Sequence);
                Assert.AreEqual(hit.Sequence, dispatch.ResultSequence);
                Assert.AreEqual(attack.Sequence, dispatch.AttackSequence);
                Assert.AreEqual(pp.Sequence, dispatch.PpSequence);
                Assert.IsNull(dispatch.MoveTurn);
                Assert.IsTrue(dispatch.AcknowledgesCurrentReceipt);
                Assert.AreEqual(hit is SourceSubstituteDamagedEvent, dispatch.HitsSubstitute);
            }
            else Assert.IsNull(decision.Dispatch, "HP, survival and confusion pages cannot replay the root.");
            if (actor < 2 && target < 2)
            {
                var single = Single(events, index);
                Assert.AreEqual(decision.Dispatch,
                    AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.On, moves: Mechanics.Catalog.Moves).Dispatch);
                Assert.AreEqual(decision.Dispatch,
                    AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtHitResult(single, true, SourceBattleScene.On).Dispatch);
            }
        }
        Assert.AreEqual(1, count);
        Assert.AreEqual(before, JsonSerializer.Serialize(events));
    }

    private static void AssertDenied(ImmutableArray<SourceBattleEvent> events, string label, bool singles = true)
    {
        string before = JsonSerializer.Serialize(events);
        for (int index = 0; index < events.Length; index++)
        {
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events, index), SourceBattleScene.On,
                moves: Mechanics.Catalog.Moves).Dispatch, label);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(Double(events, index), SourceBattleScene.On).Dispatch, label);
            if (!singles) continue;
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, index), true, SourceBattleScene.On,
                moves: Mechanics.Catalog.Moves).Dispatch, label);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(Single(events, index), true, SourceBattleScene.On).Dispatch, label);
        }
        Assert.AreEqual(before, JsonSerializer.Serialize(events), label);
    }

    [TestMethod]
    public void NaturalWaterPulseOwnsOneRootAtEveryPositionAndSelectedAlly()
    {
        var move = Mechanics.Catalog.Moves[Pulse];
        Assert.AreEqual(352, move.SourceMoveId);
        Assert.AreEqual("EFFECT_CONFUSE_HIT", move.Effect);
        Assert.AreEqual("TYPE_WATER", move.Type);
        Assert.AreEqual("MOVE_TARGET_SELECTED", move.Target);
        Assert.AreEqual(60, move.Power);
        Assert.AreEqual(100, move.Accuracy);
        Assert.AreEqual(20, move.Pp);
        Assert.AreEqual(20, move.SecondaryEffectChance);
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        {
            var result = Resolve(Frame(Bodies(actor)), actor, target);
            Assert.IsTrue(result.Events.OfType<SourceHpChangedEvent>().Single().Damage > 0);
            AssertProjection(result, actor, target);
        }
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(200)]
    public void SubstituteBreakAndSurvivalBlockActualQualifyingConfusion(int substituteHp)
    {
        var bodies = Bodies();
        bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = substituteHp } };
        int pokemonHp = bodies[1].Pokemon.CurrentHp;
        var result = Find(Frame(bodies), QualifyingSecondary, $"substitute HP {substituteHp} qualifying secondary");
        var hit = result.Events.OfType<SourceSubstituteDamagedEvent>().Single();
        Assert.AreEqual(substituteHp, hit.HpBefore);
        Assert.IsTrue(hit.Damage > 0);
        Assert.AreEqual(substituteHp == 1, hit.HpAfter == 0);
        Assert.AreEqual(pokemonHp, result.State.Slots[1].Body!.Pokemon.CurrentHp);
        Assert.AreEqual(0, result.Events.OfType<SourceHpChangedEvent>().Count());
        Assert.AreEqual(0, result.Events.OfType<SourceConfusionChangedEvent>().Count());
        Assert.IsFalse(HasDuration(result));
        AssertProjection(result);
    }

    [TestMethod]
    public void ActualAccuracyMissAndProtectRejectRootWithoutDamage()
    {
        var bodies = Bodies();
        bodies[1] = bodies[1] with { Protected = true };
        var protect = Resolve(Frame(bodies));
        Assert.IsTrue(protect.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "protected"));
        Assert.AreEqual(0, protect.Events.OfType<SourceHpChangedEvent>().Count());
        AssertDenied(protect.Events, "Protect");
        bodies = Bodies();
        bodies[0] = bodies[0] with { AccuracyStage = 0 };
        bodies[1] = bodies[1] with { EvasionStage = 12 };
        var miss = Find(Frame(bodies), r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "accuracy miss");
        Assert.AreEqual(0, miss.Events.OfType<SourceHpChangedEvent>().Count());
        AssertDenied(miss.Events, "accuracy miss");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PrimaryWaterAbsorbPreventionNeverBorrowsHealingOrSubstituteAsHit(bool substitute)
    {
        var bodies = Bodies();
        var lapras = NaturalMon("LAPRAS", 100, "SING");
        Assert.AreEqual("ABILITY_WATER_ABSORB", lapras.Pokemon.AbilityId);
        bodies[1] = lapras with { Pokemon = lapras.Pokemon with { CurrentHp = lapras.Pokemon.Stats.MaxHp - 20 } };
        if (substitute) bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = 50 } };
        var result = Resolve(Frame(bodies));
        var prevention = result.Events.OfType<SourceAbilityPreventedEvent>().Single();
        Assert.AreEqual("ABILITY_WATER_ABSORB", prevention.Ability);
        Assert.AreEqual(Pulse, prevention.Move);
        Assert.AreEqual(0, prevention.Actor.Value);
        Assert.AreEqual(1, prevention.Target.Value);
        Assert.IsTrue(prevention.Sequence > result.Events.OfType<SourcePpCommandEvent>().Single().Sequence);
        Assert.AreEqual(lapras.Pokemon.Stats.MaxHp, result.State.Slots[1].Body!.Pokemon.CurrentHp);
        Assert.IsTrue(result.Events.OfType<SourceRecoveredEvent>().Single().Sequence > prevention.Sequence);
        Assert.IsFalse(result.Events.Any(e => e is SourceHpChangedEvent or SourceSubstituteDamagedEvent or SourceConfusionChangedEvent));
        Assert.IsFalse(result.Trace.Any(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal)));
        if (substitute) Assert.AreEqual(50, result.State.Slots[1].Body!.CalledVolatile!.SubstituteHp);
        AssertDenied(result.Events, "primary Water Absorb");
    }

    [TestMethod]
    public void SuccessfulConfusionFollowsDamageAndOwnsNoAdditionalRoot()
    {
        var result = Find(Frame(Bodies()), r => r.Events.OfType<SourceConfusionChangedEvent>().Any(), "successful confusion");
        Assert.IsTrue(QualifyingSecondary(result));
        Assert.IsTrue(HasDuration(result));
        var changed = result.Events.OfType<SourceConfusionChangedEvent>().Single();
        Assert.AreEqual(1, changed.Side.Value);
        Assert.AreEqual(0, changed.Before);
        Assert.IsTrue(changed.After is >= 2 and <= 5);
        Assert.AreEqual("move", changed.Reason);
        Assert.IsTrue(changed.Sequence > result.Events.OfType<SourceHpChangedEvent>().Single().Sequence);
        Assert.AreEqual(changed.After, result.State.Slots[1].Body!.ConfusionTurnsRemaining);
        AssertProjection(result);
    }

    [TestMethod]
    [DataRow("SPINDA", "TACKLE", "ABILITY_OWN_TEMPO")]
    [DataRow("CATERPIE", "STRING_SHOT", "ABILITY_SHIELD_DUST")]
    public void RealLateConfusionAbilityPreventionIsSilentAfterSuccessfulDamage(string species, string move, string ability)
    {
        var bodies = Bodies();
        bodies[1] = NaturalMon(species, 100, move);
        Assert.AreEqual(ability, bodies[1].Pokemon.AbilityId);
        var result = Find(Frame(bodies), QualifyingSecondary, ability + " qualifying secondary prevention");
        var hit = result.Events.OfType<SourceHpChangedEvent>().Single();
        Assert.IsTrue(hit.Damage > 0 && hit.HpAfter > 0);
        Assert.AreEqual(0, result.Events.OfType<SourceAbilityPreventedEvent>().Count(),
            "SetMoveEffect silently skips Own Tempo and Shield Dust; a primary prevention receipt would be wrong.");
        Assert.AreEqual(0, result.Events.OfType<SourceConfusionChangedEvent>().Count());
        Assert.AreEqual(0, result.State.Slots[1].Body!.ConfusionTurnsRemaining);
        Assert.IsFalse(HasDuration(result));
        AssertProjection(result);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ZeroLossEndureKeepsOneRootForActualCriticalAndNoncriticalConfusion(bool critical)
    {
        var bodies = Bodies();
        bodies[1] = bodies[1] with { Pokemon = bodies[1].Pokemon with { CurrentHp = 1 }, Enduring = true };
        var result = Find(Frame(bodies), r => r.Events.OfType<SourceHpChangedEvent>().Single().Critical == critical &&
            r.Events.OfType<SourceConfusionChangedEvent>().Any(), $"zero-loss Endure critical={critical} confusion");
        var hit = result.Events.OfType<SourceHpChangedEvent>().Single();
        var adjustment = result.Events.OfType<SourceDamageAdjustedEvent>().Single();
        var survival = result.Events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "endure" && e.Reason == "endured");
        var confusion = result.Events.OfType<SourceConfusionChangedEvent>().Single();
        Assert.AreEqual(1, hit.HpBefore);
        Assert.AreEqual(1, hit.HpAfter);
        Assert.AreEqual(0, hit.Damage);
        Assert.IsTrue(adjustment.Endured && !adjustment.HungOn && adjustment.DamageBefore > 0);
        Assert.AreEqual(0, adjustment.DamageAfter);
        Assert.AreEqual(1, adjustment.Target.Value);
        Assert.AreEqual(Pulse, adjustment.Move);
        Assert.AreEqual(1, survival.Side.Value);
        Assert.IsTrue(adjustment.Sequence < hit.Sequence && hit.Sequence < survival.Sequence && survival.Sequence < confusion.Sequence);
        // The existing generic path retains HP.Critical but emits no separate critical
        // receipt for adjusted zero damage. Poison Fang uses a different hit recipe.
        Assert.AreEqual(0, result.Events.OfType<SourceCriticalHitEvent>().Count());
        Assert.IsTrue(QualifyingSecondary(result));
        AssertProjection(result);
    }

    [TestMethod]
    public void MalformedDirectHeadersPpAndHitOwnershipCannotAuthorizeWaterPulse()
    {
        var result = Resolve(Frame(Bodies()));
        AssertProjection(result);
        var events = result.Events;
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        var hit = events.OfType<SourceHpChangedEvent>().Single();
        foreach (var receipt in new SourceBattleEvent[] { attack, used, pp, hit })
            AssertDenied(events.Remove(receipt), "missing " + receipt.GetType().Name);
        AssertDenied(events.Replace(attack, attack with { CurrentMove = Id("SURF") }), "called current move");
        AssertDenied(events.Replace(attack, attack with { ChosenMove = Id("METRONOME") }), "called chosen move");
        AssertDenied(events.Replace(used, used with { Slot = -1 }), "invalid move slot");
        foreach (var bad in new[] {
            pp with { Actor = new(1) }, pp with { PpOwner = new(1) }, pp with { Target = new(0) },
            pp with { PpSlot = 1 }, pp with { ChosenMove = Id("SURF") }, pp with { CurrentMove = Id("SURF") },
            pp with { Before = pp.Before + 1 }, pp with { After = pp.After + 1 }, pp with { CommandCost = 0 },
            pp with { Sequence = used.Sequence }
        }) AssertDenied(events.Replace(pp, bad), "malformed PP ownership or command");
        foreach (var bad in new[] {
            hit with { Actor = new(1) }, hit with { Target = new(0) }, hit with { Move = Id("SURF") },
            hit with { Sequence = pp.Sequence }, hit with { Turn = hit.Turn + 1 }
        }) AssertDenied(events.Replace(hit, bad), "malformed hit ownership or sequence");
        AssertDenied(events.Replace(hit, hit with { Target = new(3) }), "other target hit", singles: false);
        AssertDenied(events.Add(pp with { Sequence = events[^1].Sequence + 1 }), "duplicate PP");
        AssertDenied(events.Add(hit with { Sequence = events[^1].Sequence + 1 }), "duplicate hit");
    }

    [TestMethod]
    public void SceneRulesPhaseAndCursorGatesLeaveCommittedStateEventsAndRngUntouched()
    {
        var result = Resolve(Frame(Bodies()));
        var events = result.Events;
        int index = events.IndexOf(events.OfType<SourceMoveUsedEvent>().Single());
        var single = Single(events, index);
        var doubles = Double(events, index);
        string before = JsonSerializer.Serialize(result);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(single, false, SourceBattleScene.On).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.Off).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(single with { Phase = CampaignBattlePhase.ChooseMove }, true, SourceBattleScene.On).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(doubles, SourceBattleScene.Off).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(doubles with { Stage = CampaignDoubleBattleStage.Finished }, SourceBattleScene.On).Dispatch);
        foreach (int cursor in new[] { -1, events.Length })
        {
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(single with { EventIndex = cursor }, true, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(doubles with { EventIndex = cursor }, SourceBattleScene.On).Dispatch);
        }
        AssertProjection(result);
        Assert.AreEqual(before, JsonSerializer.Serialize(result));
    }

    [TestMethod]
    public void RejectedPublicKernelInputProducesNoReceiptsOrRandomDraws()
    {
        var frame = Frame(Bodies());
        var valid = new SourceResolvedMoveAction(frame.Slots[0].Occupant!.Value, frame.Revision, 0, new(1));
        string before = JsonSerializer.Serialize(frame);
        foreach (var bad in new[] { valid with { Revision = frame.Revision + 1 }, valid with { ChosenSlot = 4 }, valid with { Target = new(0) } })
        {
            var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame, bad);
            Assert.IsFalse(result.Succeeded);
            Assert.IsNotNull(result.Fault);
            Assert.AreSame(frame, result.State);
            Assert.IsTrue(result.Events.IsEmpty);
            Assert.IsTrue(result.Trace.IsEmpty);
            Assert.AreEqual(frame.Rng, result.State.Rng);
            Assert.AreEqual(before, JsonSerializer.Serialize(result.State));
        }
    }
}

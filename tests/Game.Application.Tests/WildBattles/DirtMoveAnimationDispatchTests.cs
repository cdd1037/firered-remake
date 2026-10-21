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

[TestClass, TestCategory("Game")]
public sealed class DirtMoveAnimationDispatchTests
{
    // Natural level-up learners from pret/pokefirered 037335f4. These are controlled
    // public-kernel fixtures, not evidence of a replayed campaign route.
    private static readonly (string Name, int Id, string Species, int Level, int Pp)[] Moves =
        [("SAND_ATTACK", 28, "PIDGEY", 5, 15), ("MUD_SLAP", 189, "DIGLETT", 25, 10)];
    private static MoveId Id(string name) => new("pret:MOVE_" + name);
    private static SourceMechanicsCatalog Mechanics =>
        TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static SourceBattleEvent[] Hits(ImmutableArray<SourceBattleEvent> events) =>
        events.Where(e => e is SourceHpChangedEvent or SourceSubstituteDamagedEvent).ToArray();
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("dirt-moves", new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("dirt-moves", null!, new(), events, index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []);

    private static WildBattleCombatant NaturalMon(string species, int level, string name)
    {
        var move = Id(name);
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level),
            $"Natural source learnset required: {species}/{level}/{name}.");
        int pp = Mechanics.Catalog.Moves[move].Pp;
        pokemon = pokemon with { Moves = [new(move, pp, pp)] };
        Assert.IsNull(SourcePokemonValidator.ValidatePermanent(Mechanics, pokemon));
        return new(pokemon);
    }

    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var move = Moves.Single(m => m.Id == id);
        var bodies = Enumerable.Range(0, 4)
            .Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL") with { SpeedStage = 0 }).ToArray();
        bodies[actor] = NaturalMon(move.Species, move.Level, move.Name) with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }

    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies, uint seed = 31)
    {
        static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
            new(body.Pokemon, body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
        var frame = new SourceBattleFrame {
            Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version, BattleKind = WildBattleCalledBattleKind.Trainer, Rng = new(31),
            Parties = [new(BattleSide.Player, [Member(bodies[0]), Member(bodies[2])]),
                new(BattleSide.Opponent, [Member(bodies[1]), Member(bodies[3])])],
            Slots = Enumerable.Range(0, 4).Select(i =>
                new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray()
        };
        var entry = SourceFourEntryAbilities.InitializeEntry(Mechanics.Catalog, frame, frame.Revision);
        Assert.IsTrue(entry.Succeeded, entry.Fault);
        Assert.IsNull(SourceBattleIdentityRules.Validate(entry.State));
        return entry.State with { Rng = new(seed) };
    }

    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor, int target)
    {
        Assert.IsNull(SourceBattleIdentityRules.Validate(frame));
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault);
        Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty);
        return result;
    }

    private static SourceFourActionResult Ordered(SourceBattleFrame frame, int actor, int target)
    {
        var selected = frame.Slots[actor].Body!.Pokemon.Moves[0].Move;
        var choices = frame.Slots.Select(c => {
            var move = Mechanics.Catalog.Moves[c.Body!.Pokemon.Moves[0].Move];
            int recipient = c.Slot.Value == actor ? target
                : move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? c.Slot.Value : c.Slot.Value ^ 1;
            return new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, 0, new(recipient));
        }).ToImmutableArray();
        var result = SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision, choices);
        for (int step = 0; step < 12; step++)
        {
            Assert.IsTrue(result.Succeeded, result.Fault);
            Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            if (result.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == actor && e.Move == selected)) return result;
            Assert.IsNotNull(result.Continuation, "The selected natural dirt move must execute in this public turn.");
            result = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
        }
        throw new InvalidOperationException("The selected dirt move did not execute within one bounded public turn.");
    }

    private static SourceKernelResult Find(SourceBattleFrame entered, int actor, int target,
        Func<SourceKernelResult, bool> predicate, string branch)
    {
        // Bounded discovery of actual receipts, never a source-golden generator.
        for (uint seed = 0; seed < 1024; seed++)
        {
            var result = Resolve(entered with { Rng = new(seed) }, actor, target);
            if (predicate(result)) return result;
        }
        throw new InvalidOperationException($"No observed {branch} in 1024 action-start seeds.");
    }

    private static void AssertProjection(SourceKernelResult result, int id, bool expected, int actor, int target,
        int? ppBefore = null, int cost = 1) =>
        AssertProjection(result.Events, result.State, result.Trace, id, expected, actor, target, ppBefore, cost);

    private static void AssertProjection(ImmutableArray<SourceBattleEvent> events, SourceBattleFrame state,
        ImmutableArray<RandomTrace> trace, int id, bool expected, int actor, int target, int? ppBefore = null, int cost = 1)
    {
        string receipts = JsonSerializer.Serialize(events), committed = JsonSerializer.Serialize(state), random = JsonSerializer.Serialize(trace);
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        var move = Moves.Single(m => m.Id == id);
        int before = ppBefore ?? move.Pp, after = Math.Max(0, before - cost);
        Assert.AreEqual(Id(move.Name), used.Move);
        Assert.AreEqual(actor, used.Actor.Value); Assert.AreEqual(target, used.Target.Value);
        Assert.AreEqual(before, used.PpBefore); Assert.AreEqual(after, used.PpAfter);
        Assert.AreEqual(used.Move, attack.CurrentMove); Assert.AreEqual(used.Move, attack.ChosenMove);
        Assert.AreEqual(used.Move, pp.CurrentMove); Assert.AreEqual(used.Move, pp.ChosenMove);
        Assert.AreEqual(actor, pp.Actor.Value); Assert.AreEqual(actor, pp.PpOwner.Value); Assert.AreEqual(target, pp.Target.Value);
        Assert.AreEqual(0, pp.PpSlot); Assert.AreEqual(before, pp.Before); Assert.AreEqual(after, pp.After);
        Assert.AreEqual(cost, pp.CommandCost); Assert.AreEqual(after, state.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);
        Assert.IsTrue(attack.Sequence < used.Sequence && used.Sequence < pp.Sequence);
        Assert.AreEqual(0, events.OfType<SourceMultiHitEvent>().Count());
        SourceBattleEvent? resultReceipt = expected ? id == 28
            ? events.OfType<SourceStatChangedEvent>().Single(s => s.Actor.Value == actor && s.Target.Value == target && s.Stat == WildBattleStat.Accuracy)
            : Hits(events).Single() : null;
        var statReceipt = events.OfType<SourceStatChangedEvent>().SingleOrDefault(s =>
            s.Actor.Value == actor && s.Target.Value == target && s.Stat == WildBattleStat.Accuracy && s.After < s.Before);
        int roots = 0, waves = 0;
        for (int index = 0; index < events.Length; index++)
        {
            var battle = Double(events, index);
            var decision = AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On);
            Assert.AreEqual(decision, AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On));
            Assert.IsNull(AnimationDispatchFixture.AtResult(battle, SourceBattleScene.On).Dispatch,
                "A single dirt root cannot be replayed by a damage, secondary, cap or faint page.");
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.Off).Dispatch);
            if (expected && events[index] == used)
            {
                var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); roots++;
                Assert.AreEqual(id, cue.SourceMoveId); Assert.AreEqual("dirt-moves", cue.BattleId);
                Assert.AreEqual(actor, cue.Actor.Value); Assert.AreEqual(target, cue.Target.Value);
                Assert.AreEqual(used.Sequence, cue.Sequence); Assert.AreEqual(resultReceipt!.Sequence, cue.ResultSequence);
                Assert.AreEqual(attack.Sequence, cue.AttackSequence); Assert.AreEqual(pp.Sequence, cue.PpSequence);
                Assert.IsTrue(cue.AcknowledgesCurrentReceipt); Assert.IsNull(cue.MoveTurn);
                Assert.AreEqual(resultReceipt is SourceSubstituteDamagedEvent, cue.HitsSubstitute);
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(
                    battle with { Stage = CampaignDoubleBattleStage.Finished }, SourceBattleScene.On).Dispatch);
            }
            else Assert.IsNull(decision.Dispatch, "Only the matching MoveUsed page can own this root.");
            {
                var wave = AnimationDispatchFixture.StatAtResult(battle, moves: Mechanics.Catalog.Moves);
                Assert.AreEqual(wave, AnimationDispatchFixture.StatAtResult(battle, moves: Mechanics.Catalog.Moves));
                Assert.IsNull(AnimationDispatchFixture.StatAtResult(battle, true, Mechanics.Catalog.Moves));
                if (statReceipt is not null && events[index] == statReceipt)
                {
                    Assert.IsNotNull(wave); waves++;
                    Assert.AreEqual(id, wave.SourceMoveId); Assert.AreEqual(27, wave.AnimationArgument);
                    Assert.AreEqual(target, wave.Affected.Value); Assert.AreEqual(statReceipt!.Sequence, wave.Sequence);
                    Assert.AreEqual(attack.Sequence, wave.AttackSequence); Assert.AreEqual(pp.Sequence, wave.PpSequence);
                }
                else Assert.IsNull(wave, "Only the committed accuracy result may authorize General_StatsChange.");
            }
            if (actor < 2 && target < 2)
            {
                var single = Single(events, index);
                Assert.AreEqual(decision.Dispatch, AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtResult(single, true, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(single, false, SourceBattleScene.On).Dispatch);
                Assert.AreEqual(AnimationDispatchFixture.StatAtResult(battle, moves: Mechanics.Catalog.Moves),
                    AnimationDispatchFixture.StatAtResult(single, true, moves: Mechanics.Catalog.Moves));
                Assert.IsNull(AnimationDispatchFixture.StatAtResult(single, false, moves: Mechanics.Catalog.Moves));
            }
        }
        Assert.AreEqual(expected ? 1 : 0, roots);
        Assert.AreEqual(statReceipt is null ? 0 : 1, waves);
        Assert.AreEqual(receipts, JsonSerializer.Serialize(events), "Dispatch must not mutate committed receipts.");
        Assert.AreEqual(committed, JsonSerializer.Serialize(state), "Dispatch must not mutate authoritative state or gameplay RNG.");
        Assert.AreEqual(random, JsonSerializer.Serialize(trace), "Dispatch must not consume or rewrite gameplay random draws.");
    }

    [TestMethod]
    [DataRow(28)]
    [DataRow(189)]
    public void NaturalDirtMovesOwnOneOrderedRootAtEveryPositionIncludingSelectedAllies(int id)
    {
        var move = Moves.Single(m => m.Id == id); var definition = Mechanics.Catalog.Moves[Id(move.Name)];
        Assert.AreEqual("MOVE_TARGET_SELECTED", definition.Target); Assert.AreEqual("TYPE_GROUND", definition.Type);
        Assert.IsFalse(definition.Flags.Contains("FLAG_MAKES_CONTACT"));
        Assert.AreEqual(id == 28 ? "EFFECT_ACCURACY_DOWN" : "EFFECT_ACCURACY_DOWN_HIT", definition.Effect);
        Assert.AreEqual(id == 28 ? 0 : 100, definition.SecondaryEffectChance);
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        {
            var result = Ordered(Frame(Bodies(id, actor)), actor, target);
            var stat = result.Events.OfType<SourceStatChangedEvent>().Single();
            Assert.AreEqual(actor, stat.Actor.Value); Assert.AreEqual(target, stat.Target.Value);
            Assert.AreEqual(WildBattleStat.Accuracy, stat.Stat); Assert.AreEqual(6, stat.Before); Assert.AreEqual(5, stat.After);
            Assert.AreEqual(5, result.State.Slots[target].Body!.AccuracyStage);
            if (id == 28) Assert.AreEqual(0, Hits(result.Events).Length);
            else
            {
                var hit = result.Events.OfType<SourceHpChangedEvent>().Single();
                Assert.IsTrue(hit.Damage > 0); Assert.IsTrue(hit.Sequence < stat.Sequence);
                Assert.AreEqual(1, result.Trace.Count(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal)));
            }
            AssertProjection(result.Events, result.State, result.Trace, id, true, actor, target);
        }
    }

    [TestMethod]
    public void SandAttackLastAvailableStageStillOwnsItsRootAndAccuracyWave27()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(28, actor); bodies[target] = bodies[target] with { AccuracyStage = 1 };
            var result = Resolve(Frame(bodies), actor, target); var stat = result.Events.OfType<SourceStatChangedEvent>().Single();
            Assert.AreEqual(1, stat.Before); Assert.AreEqual(0, stat.After); AssertProjection(result, 28, true, actor, target);
        }
    }

    [TestMethod]
    [DataRow(28)]
    [DataRow(189)]
    public void ActualProtectAndAccuracyMissDenyEveryRootPageAtAllFourPositions(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor); bodies[target] = bodies[target] with { Protected = true };
            var protect = Resolve(Frame(bodies), actor, target);
            Assert.IsTrue(protect.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "protected"));
            Assert.AreEqual(0, Hits(protect.Events).Length); Assert.AreEqual(0, protect.Events.OfType<SourceStatChangedEvent>().Count());
            AssertProjection(protect, id, false, actor, target);
            bodies = Bodies(id, actor); bodies[actor] = bodies[actor] with { AccuracyStage = 0 };
            bodies[target] = bodies[target] with { EvasionStage = 12 };
            var miss = Find(Frame(bodies), actor, target, r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "dirt accuracy miss");
            Assert.AreEqual(0, Hits(miss.Events).Length); Assert.AreEqual(0, miss.Events.OfType<SourceStatChangedEvent>().Count());
            AssertProjection(miss, id, false, actor, target);
        }
    }

    [TestMethod]
    public void SandAttackSubstituteFailurePrecedesAccuracyAndDoesNotDamageTheSubstitute()
    {
        // BattleScript_EffectStatDown: jumpifstatus2 precedes accuracycheck.
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(28, actor);
            bodies[actor] = bodies[actor] with { AccuracyStage = 0 };
            bodies[target] = bodies[target] with { EvasionStage = 12, CalledVolatile = new() { SubstituteHp = 100 } };
            var result = Resolve(Frame(bodies), actor, target);
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "substitute"));
            Assert.AreEqual(0, result.Trace.Count(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)));
            Assert.AreEqual(0, result.Events.OfType<SourceMoveMissedEvent>().Count()); Assert.AreEqual(0, Hits(result.Events).Length);
            Assert.AreEqual(100, result.State.Slots[target].Body!.CalledVolatile!.SubstituteHp);
            Assert.AreEqual(6, result.State.Slots[target].Body!.AccuracyStage); AssertProjection(result, 28, false, actor, target);
        }
    }

    [TestMethod]
    public void SandAttackAtMinimumSpendsPpButCannotOwnEitherAnimation()
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        {
            var bodies = Bodies(28, actor); bodies[target] = bodies[target] with { AccuracyStage = 0 };
            var result = Resolve(Frame(bodies), actor, target); var cap = result.Events.OfType<SourceStatAtMinimumEvent>().Single();
            Assert.AreEqual(actor, cap.Actor.Value); Assert.AreEqual(target, cap.Target.Value); Assert.AreEqual(WildBattleStat.Accuracy, cap.Stat);
            Assert.IsTrue(result.Events.OfType<SourcePpCommandEvent>().Single().Sequence < cap.Sequence);
            Assert.AreEqual(0, result.Events.OfType<SourceStatChangedEvent>().Count()); AssertProjection(result, 28, false, actor, target);
        }
    }

    [TestMethod]
    [DataRow("TENTACOOL", "BARRIER", "ABILITY_CLEAR_BODY")]
    [DataRow("TORKOAL", "IRON_DEFENSE", "ABILITY_WHITE_SMOKE")]
    [DataRow("HITMONCHAN", "AGILITY", "ABILITY_KEEN_EYE")]
    public void SandAttackPrimaryAbilityPreventionDenyRootAndWave(string species, string move, string ability)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(28, actor); bodies[target] = NaturalMon(species, 100, move);
            Assert.AreEqual(ability, bodies[target].Pokemon.AbilityId); var result = Resolve(Frame(bodies), actor, target);
            var blocked = result.Events.OfType<SourceAbilityPreventedEvent>().Single();
            Assert.AreEqual(ability, blocked.Ability); Assert.AreEqual(actor, blocked.Actor.Value); Assert.AreEqual(target, blocked.Target.Value);
            Assert.AreEqual(6, result.State.Slots[target].Body!.AccuracyStage); Assert.AreEqual(0, Hits(result.Events).Length);
            AssertProjection(result, 28, false, actor, target);
        }
    }

    [TestMethod]
    public void SandAttackMistProtectsTheSelectedSideIncludingAnAlly()
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        {
            var frame = Frame(Bodies(28, actor)); frame = frame with { Sides = frame.Sides.SetItem(target & 1,
                frame.Sides[target & 1] with { MistTurnsRemaining = 5 }) };
            var result = Resolve(frame, actor, target);
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "mist" && e.Target.Value == target));
            Assert.AreEqual(6, result.State.Slots[target].Body!.AccuracyStage); AssertProjection(result, 28, false, actor, target);
        }
    }

    [TestMethod]
    [DataRow("ZUBAT", "CONFUSE_RAY", "ABILITY_INNER_FOCUS")]
    [DataRow("GASTLY", "LICK", "ABILITY_LEVITATE")]
    public void SandAttackDoesNotBorrowGroundDamageImmunity(string species, string move, string ability)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(28, actor); bodies[target] = NaturalMon(species, 100, move);
            Assert.AreEqual(ability, bodies[target].Pokemon.AbilityId); var result = Resolve(Frame(bodies), actor, target);
            Assert.AreEqual(5, result.State.Slots[target].Body!.AccuracyStage);
            Assert.AreEqual(0, result.Events.OfType<SourceAbilityPreventedEvent>().Count());
            AssertProjection(result, 28, true, actor, target);
        }
    }

    [TestMethod]
    [DataRow("ZUBAT", "CONFUSE_RAY", false)]
    [DataRow("GASTLY", "LICK", true)]
    public void MudSlapFlyingAndLevitateImmunityDenyTheDamageRoot(string species, string move, bool levitate)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(189, actor); bodies[target] = NaturalMon(species, 100, move);
            var result = Resolve(Frame(bodies), actor, target);
            Assert.IsTrue(result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
            if (levitate) Assert.IsTrue(result.Events.OfType<SourceAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_LEVITATE"));
            Assert.AreEqual(bodies[target].Pokemon.CurrentHp, result.State.Slots[target].Body!.Pokemon.CurrentHp);
            Assert.AreEqual(6, result.State.Slots[target].Body!.AccuracyStage);
            Assert.IsFalse(result.Events.OfType<SourceHpChangedEvent>().Any(e => e.Damage > 0));
            AssertProjection(result, 189, false, actor, target);
        }
    }

    [TestMethod]
    public void MudSlapSurvivingAndBrokenSubstitutesKeepOneDamageRootAndBlockTheSecondary()
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (int hp in new[] { 1, 100 })
        {
            int target = actor ^ 1; var bodies = Bodies(189, actor);
            bodies[target] = bodies[target] with { CalledVolatile = new() { SubstituteHp = hp } };
            var result = Resolve(Frame(bodies), actor, target); var hit = result.Events.OfType<SourceSubstituteDamagedEvent>().Single();
            Assert.AreEqual(hp, hit.HpBefore); Assert.AreEqual(hp == 1, hit.HpAfter == 0);
            Assert.AreEqual(0, result.Events.OfType<SourceHpChangedEvent>().Count());
            Assert.AreEqual(bodies[target].Pokemon.CurrentHp, result.State.Slots[target].Body!.Pokemon.CurrentHp);
            Assert.AreEqual(6, result.State.Slots[target].Body!.AccuracyStage);
            Assert.AreEqual(0, result.Events.OfType<SourceStatChangedEvent>().Count()); AssertProjection(result, 189, true, actor, target);
        }
    }

    [TestMethod]
    [DataRow("TENTACOOL", "BARRIER", "ABILITY_CLEAR_BODY")]
    [DataRow("TORKOAL", "IRON_DEFENSE", "ABILITY_WHITE_SMOKE")]
    [DataRow("HITMONCHAN", "AGILITY", "ABILITY_KEEN_EYE")]
    [DataRow("CATERPIE", "STRING_SHOT", "ABILITY_SHIELD_DUST")]
    public void MudSlapAbilityPreventionSilencesOnlyItsAccuracySecondary(string species, string move, string ability)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(189, actor); bodies[target] = NaturalMon(species, 100, move);
            Assert.AreEqual(ability, bodies[target].Pokemon.AbilityId); var result = Resolve(Frame(bodies), actor, target);
            Assert.IsTrue(result.Events.OfType<SourceHpChangedEvent>().Single().Damage > 0);
            Assert.IsTrue(result.State.Slots[target].Body!.Pokemon.CurrentHp > 0);
            Assert.AreEqual(6, result.State.Slots[target].Body!.AccuracyStage);
            Assert.AreEqual(0, result.Events.OfType<SourceStatChangedEvent>().Count());
            Assert.AreEqual(0, result.Events.OfType<SourceAbilityPreventedEvent>().Count(),
                "Secondary stat protection must not masquerade as a primary damage immunity.");
            Assert.AreEqual(0, result.Events.OfType<SourceMoveFailedEvent>().Count()); AssertProjection(result, 189, true, actor, target);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MudSlapStatCapAndMistPreserveTheCommittedDamageRoot(bool mist)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(189, actor);
            if (!mist) bodies[target] = bodies[target] with { AccuracyStage = 0 };
            var frame = Frame(bodies);
            if (mist) frame = frame with { Sides = frame.Sides.SetItem(target & 1,
                frame.Sides[target & 1] with { MistTurnsRemaining = 5 }) };
            var result = Resolve(frame, actor, target);
            Assert.IsTrue(result.Events.OfType<SourceHpChangedEvent>().Single().Damage > 0);
            Assert.AreEqual(mist ? 6 : 0, result.State.Slots[target].Body!.AccuracyStage);
            Assert.AreEqual(0, result.Events.OfType<SourceStatChangedEvent>().Count());
            Assert.AreEqual(0, result.Events.OfType<SourceStatAtMinimumEvent>().Count());
            Assert.AreEqual(0, result.Events.OfType<SourceMoveFailedEvent>().Count()); AssertProjection(result, 189, true, actor, target);
        }
    }

    [TestMethod]
    public void MudSlapTargetKoDoesNotEraseOrReplayItsAlreadyCommittedRoot()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(189, actor);
            bodies[target] = bodies[target] with { Pokemon = bodies[target].Pokemon with { CurrentHp = 1 } };
            var result = Resolve(Frame(bodies), actor, target); var hit = result.Events.OfType<SourceHpChangedEvent>().Single();
            Assert.AreEqual(0, hit.HpAfter);
            Assert.IsTrue(result.Events.OfType<SourceFaintedEvent>().Any(e => e.Side.Value == target && e.Sequence > hit.Sequence));
            Assert.AreEqual(0, result.Events.OfType<SourceStatChangedEvent>().Count()); AssertProjection(result, 189, true, actor, target);
        }
    }

    [TestMethod]
    [DataRow(28)]
    [DataRow(189)]
    public void NaturalPressureAndClampedFinalPpRetainTheActualActorAsPpOwner(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (bool finalPp in new[] { false, true })
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor); bodies[target] = NaturalMon("DUSCLOPS", 100, "FORESIGHT");
            Assert.AreEqual("ABILITY_PRESSURE", bodies[target].Pokemon.AbilityId);
            int before = finalPp ? 1 : Moves.Single(m => m.Id == id).Pp;
            var pokemon = bodies[actor].Pokemon;
            bodies[actor] = bodies[actor] with { Pokemon = pokemon with { Moves = pokemon.Moves.SetItem(0, pokemon.Moves[0] with { CurrentPp = before }) } };
            var result = Resolve(Frame(bodies), actor, target); AssertProjection(result, id, true, actor, target, before, 2);
            Assert.AreEqual(bodies[target].Pokemon.Moves[0].CurrentPp, result.State.Slots[target].Body!.Pokemon.Moves[0].CurrentPp);
        }
    }

    [TestMethod]
    [DataRow(28)]
    [DataRow(189)]
    public void RealReceiptRootsCannotBorrowMissingPpForeignOwnershipOrCalledMoveMetadata(int id)
    {
        var result = Resolve(Frame(Bodies(id)), 0, 1); var good = result.Events;
        int ppIndex = Array.FindIndex(good.ToArray(), e => e is SourcePpCommandEvent);
        int attackIndex = Array.FindIndex(good.ToArray(), e => e is SourceAttackStringEvent);
        var pp = (SourcePpCommandEvent)good[ppIndex]; var attack = (SourceAttackStringEvent)good[attackIndex];
        ImmutableArray<SourceBattleEvent>[] corrupt = [good.RemoveAt(ppIndex),
            good.SetItem(ppIndex, pp with { PpOwner = new(1) }),
            good.SetItem(ppIndex, pp with { Target = new(0) }),
            good.SetItem(attackIndex, attack with { ChosenMove = Id("TACKLE") })];
        foreach (var events in corrupt)
        for (int index = 0; index < events.Length; index++)
        {
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events, index), SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, index), true, SourceBattleScene.On).Dispatch);
        }
    }
}

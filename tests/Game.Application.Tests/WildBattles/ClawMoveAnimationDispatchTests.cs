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
public sealed class ClawMoveAnimationDispatchTests
{
    private static readonly (string Name, int Id, string Species, int Level)[] Moves =
        [("METAL_CLAW", 232, "CHARMANDER", 13), ("CRUSH_CLAW", 306, "ZANGOOSE", 31), ("DRAGON_CLAW", 337, "BAGON", 49)];
    private static MoveId Id(string name) => new("pret:MOVE_" + name);
    private static SourceMechanicsCatalog Mechanics =>
        TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("claw-moves", new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("claw-moves", null!, new(), events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(), index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []) { SourceEvents = events };

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
        return result;
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
            Assert.IsNotNull(result.Continuation, "The selected natural claw move must execute.");
            result = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
        }
        throw new InvalidOperationException("The claw move did not execute within one public turn.");
    }
    private static SourceKernelResult Find(WildBattleCombatant[] bodies, Func<SourceKernelResult, bool> predicate, string branch)
    {
        var entered = Frame(bodies, 0);
        // Bounded branch discovery exercises real receipts; it is not an independent source golden.
        for (uint seed = 0; seed < 1024; seed++)
        {
            var result = Resolve(entered with { Rng = new(seed) });
            if (predicate(result)) return result;
        }
        throw new InvalidOperationException($"No observed {branch} in 1024 action-start seeds.");
    }
    private static void AssertOne(ImmutableArray<SourceBattleEvent> events, int id, int actor = 0, int target = 1)
    {
        var hit = events.Single(e => e is SourceHpChangedEvent or SourceSubstituteDamagedEvent);
        var used = events.OfType<SourceMoveUsedEvent>().Single(); var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single(); string before = JsonSerializer.Serialize(events); int count = 0;
        Assert.AreEqual(0, events.OfType<SourceMultiHitEvent>().Count());
        for (int index = 0; index < events.Length; index++)
        {
            var battle = Double(events, index); var decision = AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(battle, SourceBattleScene.On).Dispatch);
            if (events[index] == used)
            {
                var dispatch = decision.Dispatch; Assert.IsNotNull(dispatch, decision.Reason); count++;
                Assert.AreEqual(id, dispatch.SourceMoveId); Assert.AreEqual(actor, dispatch.Actor.Value); Assert.AreEqual(target, dispatch.Target.Value);
                Assert.AreEqual(used.Sequence, dispatch.Sequence); Assert.AreEqual(hit.Sequence, dispatch.ResultSequence);
                Assert.AreEqual(attack.Sequence, dispatch.AttackSequence); Assert.AreEqual(pp.Sequence, dispatch.PpSequence);
                Assert.IsNull(dispatch.MoveTurn); Assert.IsTrue(dispatch.AcknowledgesCurrentReceipt);
                Assert.AreEqual(hit is SourceSubstituteDamagedEvent, dispatch.HitsSubstitute);
                Assert.AreEqual(decision, AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On));
            }
            else Assert.IsNull(decision.Dispatch, "A secondary stat/status/faint page cannot replay the claw root.");
            if (actor < 2 && target < 2)
            {
                var single = Single(events, index);
                Assert.AreEqual(decision.Dispatch, AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtHitResult(single, true, SourceBattleScene.On).Dispatch);
            }
        }
        Assert.AreEqual(1, count); Assert.AreEqual(before, JsonSerializer.Serialize(events));
    }
    private static void AssertDenied(ImmutableArray<SourceBattleEvent> events)
    {
        for (int index = 0; index < events.Length; index++)
        {
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events, index), SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(Double(events, index), SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, index), true, SourceBattleScene.On).Dispatch);
        }
    }

    [TestMethod]
    public void NaturalClawsUseOneOrderedRootAtAllFourPositionsAndSelectedAllies()
    {
        foreach (var move in Moves)
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        {
            Assert.IsTrue(Mechanics.Catalog.Moves[Id(move.Name)].Flags.Contains("FLAG_MAKES_CONTACT"));
            var result = Ordered(Frame(Bodies(move.Id, actor), 31), actor, target);
            AssertOne(result.Events, move.Id, actor, target);
        }
    }

    [TestMethod]
    [DataRow(232, WildBattleStat.Attack, 0, 10)]
    [DataRow(306, WildBattleStat.Defense, 1, 50)]
    public void ActualSecondaryStatSuccessAndCapsStillOwnExactlyOneDamageRoot(int id, WildBattleStat stat, int recipient, int chance)
    {
        var successful = Find(Bodies(id), r => r.Events.OfType<SourceStatChangedEvent>().Any(s => s.Target.Value == recipient && s.Stat == stat), "claw secondary stat change");
        var changed = successful.Events.OfType<SourceStatChangedEvent>().Single();
        Assert.AreEqual(stat, changed.Stat); Assert.AreEqual(recipient, changed.Target.Value);
        Assert.AreEqual(id == 232 ? 1 : -1, changed.After - changed.Before); AssertOne(successful.Events, id);
        var capped = Bodies(id); capped[recipient] = id == 232 ? capped[recipient] with { AttackStage = 12 } : capped[recipient] with { DefenseStage = 0 };
        var blocked = Find(capped, r => r.Trace.Single(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal)).Raw % 100 <= chance, "qualifying roll at a stat cap");
        Assert.AreEqual(0, blocked.Events.OfType<SourceStatChangedEvent>().Count());
        Assert.AreEqual(0, blocked.Events.OfType<SourceAbilityPreventedEvent>().Count()); AssertOne(blocked.Events, id);
    }

    [TestMethod]
    [DataRow("TENTACOOL", "BARRIER", "ABILITY_CLEAR_BODY")]
    [DataRow("TORKOAL", "IRON_DEFENSE", "ABILITY_WHITE_SMOKE")]
    [DataRow("CATERPIE", "STRING_SHOT", "ABILITY_SHIELD_DUST")]
    public void CrushClawSecondaryPreventionIsSilentAndDoesNotCancelTheRoot(string species, string move, string ability)
    {
        var bodies = Bodies(306); bodies[1] = NaturalMon(species, 100, move);
        Assert.AreEqual(ability, bodies[1].Pokemon.AbilityId);
        var result = Find(bodies, r => r.Trace.Single(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal)).Raw % 100 <= 50, "ability-blocked qualifying Crush Claw secondary");
        Assert.IsTrue(result.State.Slots[1].Body!.Pokemon.CurrentHp > 0);
        Assert.AreEqual(6, result.State.Slots[1].Body!.DefenseStage);
        Assert.AreEqual(0, result.Events.OfType<SourceStatChangedEvent>().Count());
        Assert.AreEqual(0, result.Events.OfType<SourceAbilityPreventedEvent>().Count(), "Native secondary prevention must not masquerade as primary ability immunity.");
        AssertOne(result.Events, 306);
    }

    [TestMethod]
    public void MistSilentlyBlocksOnlyTheSecondaryDefenseLoss()
    {
        var entered = Frame(Bodies(306), 0);
        entered = entered with { Sides = entered.Sides.SetItem(1, entered.Sides[1] with { MistTurnsRemaining = 5 }) };
        SourceKernelResult? qualifying = null;
        for (uint seed = 0; seed < 1024; seed++)
        {
            var result = Resolve(entered with { Rng = new(seed) });
            if (result.Trace.Single(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal)).Raw % 100 <= 50)
            { qualifying = result; break; }
        }
        Assert.IsNotNull(qualifying); Assert.AreEqual(6, qualifying.State.Slots[1].Body!.DefenseStage);
        Assert.AreEqual(0, qualifying.Events.OfType<SourceStatChangedEvent>().Count());
        Assert.AreEqual(0, qualifying.Events.OfType<SourceMoveFailedEvent>().Count());
        AssertOne(qualifying.Events, 306);
    }

    [TestMethod]
    public void ProtectMissAndPrimaryImmunityNeverAuthorizeClawPlayback()
    {
        foreach (var move in Moves)
        {
            var protectedBodies = Bodies(move.Id); protectedBodies[1] = protectedBodies[1] with { Protected = true };
            var protect = Resolve(Frame(protectedBodies, 31)); Assert.IsTrue(protect.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "protected")); AssertDenied(protect.Events);
            var missedBodies = Bodies(move.Id); missedBodies[0] = missedBodies[0] with { AccuracyStage = 0 }; missedBodies[1] = missedBodies[1] with { EvasionStage = 12 };
            var miss = Find(missedBodies, r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "actual claw accuracy miss"); AssertDenied(miss.Events);
            var immuneBodies = Bodies(move.Id); immuneBodies[1] = NaturalMon("SHEDINJA", 100, "HARDEN");
            var immune = Resolve(Frame(immuneBodies, 31));
            Assert.IsTrue(immune.Events.OfType<SourceAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_WONDER_GUARD") ||
                immune.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
            AssertDenied(immune.Events);
        }
    }

    [TestMethod]
    public void SubstituteSurvivalAndBreakingHitBothKeepTheirSingleSubstituteReceipt()
    {
        foreach (var move in Moves)
        foreach (int hp in new[] { 1, 100 })
        {
            var bodies = Bodies(move.Id); bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = hp } };
            var result = Resolve(Frame(bodies, 31)); var hit = result.Events.OfType<SourceSubstituteDamagedEvent>().Single();
            Assert.AreEqual(hp, hit.HpBefore); Assert.AreEqual(hp == 1, hit.HpAfter == 0);
            Assert.AreEqual(bodies[1].Pokemon.CurrentHp, result.State.Slots[1].Body!.Pokemon.CurrentHp);
            Assert.AreEqual(0, result.Events.OfType<SourceHpChangedEvent>().Count()); AssertOne(result.Events, move.Id);
        }
    }

    [TestMethod]
    public void ContactKoAndContactSleepDoNotEraseTheAlreadyCommittedClawRoot()
    {
        foreach (var move in Moves)
        {
            var bodies = Bodies(move.Id); bodies[0] = bodies[0] with { Pokemon = bodies[0].Pokemon with { CurrentHp = 1 } };
            bodies[1] = NaturalMon("SHARPEDO", 100, "FOCUS_ENERGY"); Assert.AreEqual("ABILITY_ROUGH_SKIN", bodies[1].Pokemon.AbilityId);
            var ko = Resolve(Frame(bodies, 31)); var contact = ko.Events.OfType<SourceAbilityDamageEvent>().Single();
            Assert.AreEqual(0, contact.HpAfter); Assert.AreEqual(0, contact.Target.Value);
            Assert.IsTrue(ko.Events.OfType<SourceHpChangedEvent>().Single().Sequence < contact.Sequence); AssertOne(ko.Events, move.Id);
            var sleepingBodies = Bodies(move.Id); sleepingBodies[1] = NaturalMon("SHROOMISH", 100, "GROWTH");
            Assert.AreEqual("ABILITY_EFFECT_SPORE", sleepingBodies[1].Pokemon.AbilityId);
            var sleep = Find(sleepingBodies, r => WildBattleStatus.IsSleeping(r.State.Slots[0].Body!.Status), "contact-induced claw sleep");
            var status = sleep.Events.OfType<SourceStatusChangedEvent>().Single(e => e.Side.Value == 0 && WildBattleStatus.IsSleeping(e.After));
            Assert.AreEqual("effect-spore", status.Reason);
            Assert.IsTrue(sleep.Events.OfType<SourceHpChangedEvent>().Single().Sequence < status.Sequence); AssertOne(sleep.Events, move.Id);
        }
    }
}

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
public sealed class SpinningMoveAnimationDispatchTests
{
    // Natural source learners from pret/pokefirered 037335f4. These are controlled
    // public-kernel fixtures, not a claim that a campaign route was replayed.
    private static readonly (string Name, int Id, string Species, int Level, int Pp)[] Moves =
        [("MEGA_PUNCH", 5, "HITMONCHAN", 38, 20),
         ("MEGA_KICK", 25, "HITMONLEE", 46, 5),
         ("BLAZE_KICK", 299, "BLAZIKEN", 36, 10)];
    private static DirectHitAnimationContract Contract(int id) => id switch
    {
        5 => new("spinning-moves", 5, Id("MEGA_PUNCH"), 20, DirectHitOwnership.AttackText),
        25 => new("spinning-moves", 25, Id("MEGA_KICK"), 5, DirectHitOwnership.AttackText),
        299 => new("spinning-moves", 299, Id("BLAZE_KICK"), 10, DirectHitOwnership.AttackText),
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
    private static MoveId Id(string name) => new("pret:MOVE_" + name);
    private static SourceMechanicsCatalog Mechanics =>
        TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static SourceBattleEvent[] Hits(ImmutableArray<SourceBattleEvent> events) =>
        events.Where(e => e is SourceHpChangedEvent or SourceSubstituteDamagedEvent).ToArray();
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) => Contract(5).Double(events, index);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) => Contract(5).Single(events, index);

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
            Assert.IsNotNull(result.Continuation, "The selected natural spinning move must execute in this public turn.");
            result = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
        }
        throw new InvalidOperationException("The selected spinning move did not execute within one bounded public turn.");
    }

    private static SourceKernelResult Find(SourceBattleFrame entered, int actor, int target,
        Func<SourceKernelResult, bool> predicate, string branch)
    {
        // Bounded real-receipt discovery only; this does not generate source goldens.
        for (uint seed = 0; seed < 1024; seed++)
        {
            var result = Resolve(entered with { Rng = new(seed) }, actor, target);
            if (predicate(result)) return result;
        }
        throw new InvalidOperationException($"No observed {branch} in 1024 action-start seeds.");
    }

    private static bool QualifyingBurn(SourceKernelResult result) =>
        result.Trace.Single(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal)).Raw % 100 <= 10;

    private static void AssertProjection(SourceKernelResult result, int id, bool expected, int actor, int target) =>
        AssertProjection(result.Events, result.State, result.Trace, id, expected, actor, target);

    private static void AssertProjection(ImmutableArray<SourceBattleEvent> events, SourceBattleFrame state,
        ImmutableArray<RandomTrace> trace, int id, bool expected, int actor, int target)
    {
        string receipts = JsonSerializer.Serialize(events), committed = JsonSerializer.Serialize(state), random = JsonSerializer.Serialize(trace);
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        var move = Moves.Single(m => m.Id == id);
        Assert.AreEqual(Id(move.Name), used.Move);
        Assert.AreEqual(actor, used.Actor.Value); Assert.AreEqual(target, used.Target.Value);
        Assert.AreEqual(used.Move, attack.CurrentMove); Assert.AreEqual(used.Move, attack.ChosenMove);
        Assert.AreEqual(used.Move, pp.CurrentMove); Assert.AreEqual(used.Move, pp.ChosenMove);
        Assert.AreEqual(actor, pp.Actor.Value); Assert.AreEqual(actor, pp.PpOwner.Value); Assert.AreEqual(target, pp.Target.Value);
        Assert.AreEqual(move.Pp, pp.Before); Assert.AreEqual(move.Pp - 1, pp.After); Assert.AreEqual(1, pp.CommandCost);
        Assert.AreEqual(pp.After, state.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);
        Assert.AreEqual(0, events.OfType<SourceMultiHitEvent>().Count());
        if (expected) Contract(id).AssertDispatches(events, actor, target);
        else Contract(id).AssertDenied(events, "blocked spinning invocation", singles: actor < 2 && target < 2);
        for (int index = 0; index < events.Length; index++)
        {
            var battle = Double(events, index);
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.Off).Dispatch);
            if (expected && events[index] == used)
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(
                    battle with { Stage = CampaignDoubleBattleStage.Finished }, SourceBattleScene.On).Dispatch);
            if (actor < 2 && target < 2)
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, index), false, SourceBattleScene.On).Dispatch);
        }
        Assert.AreEqual(receipts, JsonSerializer.Serialize(events), "Matching must not mutate committed receipts.");
        Assert.AreEqual(committed, JsonSerializer.Serialize(state), "Matching must not mutate authoritative state or RNG.");
        Assert.AreEqual(random, JsonSerializer.Serialize(trace), "Matching must not consume or rewrite random draws.");
    }

    [TestMethod]
    [DataRow(5)]
    [DataRow(25)]
    [DataRow(299)]
    public void NaturalSpinningMovesOwnOneOrderedRootAtAllFourPositionsAndSelectedAllies(int id)
    {
        var move = Moves.Single(m => m.Id == id); var definition = Mechanics.Catalog.Moves[Id(move.Name)];
        Assert.AreEqual("MOVE_TARGET_SELECTED", definition.Target);
        Assert.IsTrue(definition.Flags.Contains("FLAG_MAKES_CONTACT"));
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        {
            var result = Ordered(Frame(Bodies(id, actor)), actor, target);
            Assert.IsTrue(result.Events.OfType<SourceHpChangedEvent>().Single().Damage > 0);
            AssertProjection(result.Events, result.State, result.Trace, id, true, actor, target);
        }
    }

    [TestMethod]
    [DataRow(5)]
    [DataRow(25)]
    [DataRow(299)]
    public void ActualMissAndProtectDenyEverySpinningPageAtAllFourPositions(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var blocked = Bodies(id, actor);
            blocked[target] = blocked[target] with { Protected = true };
            var protect = Resolve(Frame(blocked), actor, target);
            Assert.IsTrue(protect.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "protected"));
            Assert.AreEqual(0, Hits(protect.Events).Length); AssertProjection(protect, id, false, actor, target);
            var inaccurate = Bodies(id, actor); inaccurate[actor] = inaccurate[actor] with { AccuracyStage = 0 };
            inaccurate[target] = inaccurate[target] with { EvasionStage = 12 };
            var miss = Find(Frame(inaccurate), actor, target,
                r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "spinning accuracy miss");
            Assert.AreEqual(0, Hits(miss.Events).Length); AssertProjection(miss, id, false, actor, target);
        }
    }

    [TestMethod]
    [DataRow(5, "GASTLY", "LICK", false)]
    [DataRow(25, "GASTLY", "LICK", false)]
    [DataRow(299, "VULPIX", "EMBER", true)]
    public void GhostTypeAndFlashFireImmunityNeverAuthorizeSpinningRoots(int id, string species, string move, bool flashFire)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor); bodies[target] = NaturalMon(species, 100, move);
            if (flashFire) Assert.AreEqual("ABILITY_FLASH_FIRE", bodies[target].Pokemon.AbilityId);
            var result = Resolve(Frame(bodies), actor, target);
            Assert.IsTrue(flashFire
                ? result.Events.OfType<SourceAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_FLASH_FIRE")
                : result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
            Assert.AreEqual(bodies[target].Pokemon.CurrentHp, result.State.Slots[target].Body!.Pokemon.CurrentHp);
            Assert.IsFalse(result.Events.OfType<SourceHpChangedEvent>().Any(e => e.Damage > 0));
            AssertProjection(result, id, false, actor, target);
        }
    }

    [TestMethod]
    [DataRow(5)]
    [DataRow(25)]
    [DataRow(299)]
    public void SurvivingAndBrokenSubstitutesKeepExactlyOneSpinningReceipt(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (int hp in new[] { 1, 100 })
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor);
            bodies[target] = bodies[target] with { CalledVolatile = new() { SubstituteHp = hp } };
            var result = id == 299
                ? Find(Frame(bodies), actor, target, QualifyingBurn, "qualifying Blaze Kick roll against Substitute")
                : Resolve(Frame(bodies), actor, target);
            var hit = result.Events.OfType<SourceSubstituteDamagedEvent>().Single();
            Assert.AreEqual(hp, hit.HpBefore); Assert.AreEqual(hp == 1, hit.HpAfter == 0);
            Assert.AreEqual(0, result.Events.OfType<SourceHpChangedEvent>().Count());
            Assert.AreEqual(bodies[target].Pokemon.CurrentHp, result.State.Slots[target].Body!.Pokemon.CurrentHp);
            Assert.AreEqual(WildBattleStatus.None, result.State.Slots[target].Body!.Status);
            Assert.AreEqual(0, result.Events.OfType<SourceStatusChangedEvent>().Count());
            AssertProjection(result, id, true, actor, target);
        }
    }

    [TestMethod]
    [DataRow(5)]
    [DataRow(25)]
    [DataRow(299)]
    public void TargetKoAndRoughSkinActorKoDoNotEraseOrReplayTheCommittedRoot(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var lethalBodies = Bodies(id, actor);
            lethalBodies[target] = lethalBodies[target] with { Pokemon = lethalBodies[target].Pokemon with { CurrentHp = 1 } };
            var lethal = id == 299
                ? Find(Frame(lethalBodies), actor, target, QualifyingBurn, "qualifying Blaze Kick roll after target KO")
                : Resolve(Frame(lethalBodies), actor, target);
            Assert.AreEqual(0, lethal.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);
            Assert.IsTrue(lethal.Events.OfType<SourceFaintedEvent>().Any(e => e.Side.Value == target));
            Assert.AreEqual(0, lethal.Events.OfType<SourceStatusChangedEvent>().Count());
            AssertProjection(lethal, id, true, actor, target);

            var contactBodies = Bodies(id, actor);
            contactBodies[actor] = contactBodies[actor] with { Pokemon = contactBodies[actor].Pokemon with { CurrentHp = 1 } };
            contactBodies[target] = NaturalMon("SHARPEDO", 100, "FOCUS_ENERGY");
            Assert.AreEqual("ABILITY_ROUGH_SKIN", contactBodies[target].Pokemon.AbilityId);
            var contact = Resolve(Frame(contactBodies), actor, target);
            var damage = contact.Events.OfType<SourceAbilityDamageEvent>().Single();
            Assert.AreEqual(actor, damage.Target.Value); Assert.AreEqual(0, damage.HpAfter);
            Assert.IsTrue(contact.Events.OfType<SourceHpChangedEvent>().Single().Sequence < damage.Sequence);
            AssertProjection(contact, id, true, actor, target);
        }
    }

    [TestMethod]
    public void BlazeKickBurnAndFailedSecondaryRollBothKeepTheSameDamageOwnership()
    {
        Assert.AreEqual(10, Mechanics.Catalog.Moves[Id("BLAZE_KICK")].SecondaryEffectChance);
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        foreach (bool burn in new[] { false, true })
        {
            var result = Find(Frame(Bodies(299, actor)), actor, target,
                r => QualifyingBurn(r) == burn, burn ? "Blaze Kick secondary burn" : "Blaze Kick non-qualifying secondary roll");
            var changed = result.Events.OfType<SourceStatusChangedEvent>().ToArray();
            Assert.AreEqual(burn ? 1 : 0, changed.Length);
            Assert.AreEqual(burn ? WildBattleStatus.Burn : WildBattleStatus.None, result.State.Slots[target].Body!.Status);
            if (burn)
            {
                Assert.AreEqual(target, changed[0].Side.Value); Assert.AreEqual(WildBattleStatus.Burn, changed[0].After);
                Assert.AreEqual("move", changed[0].Reason);
                Assert.IsTrue(result.Events.OfType<SourceHpChangedEvent>().Single().Sequence < changed[0].Sequence);
            }
            AssertProjection(result, 299, true, actor, target);
        }
    }

    [TestMethod]
    [DataRow("WAILMER", "SPLASH", "ABILITY_WATER_VEIL")]
    [DataRow("CATERPIE", "STRING_SHOT", "ABILITY_SHIELD_DUST")]
    [DataRow("CHARMANDER", "GROWL", "ABILITY_BLAZE")]
    public void WaterVeilShieldDustAndFireTypeSilentlyPreventOnlyTheBlazeBurn(string species, string move, string ability)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(299, actor); bodies[target] = NaturalMon(species, 100, move);
            Assert.AreEqual(ability, bodies[target].Pokemon.AbilityId);
            var result = Find(Frame(bodies), actor, target, QualifyingBurn, "ability or Fire-type blocked Blaze Kick secondary");
            Assert.IsTrue(result.Events.OfType<SourceHpChangedEvent>().Single().Damage > 0);
            Assert.IsTrue(result.State.Slots[target].Body!.Pokemon.CurrentHp > 0);
            Assert.AreEqual(WildBattleStatus.None, result.State.Slots[target].Body!.Status);
            Assert.AreEqual(0, result.Events.OfType<SourceStatusChangedEvent>().Count());
            Assert.AreEqual(0, result.Events.OfType<SourceAbilityPreventedEvent>().Count(),
                "A natural 10% secondary is not CERTAIN and must not become primary ability immunity.");
            Assert.AreEqual(0, result.Events.OfType<SourceMoveFailedEvent>().Count());
            AssertProjection(result, 299, true, actor, target);
        }
    }

    [TestMethod]
    public void SafeguardAndExistingStatusBlockOnlyTheBlazeSecondaryAtAllFourPositions()
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (bool safeguard in new[] { false, true })
        {
            int target = actor ^ 1; var bodies = Bodies(299, actor);
            if (!safeguard) bodies[target] = bodies[target] with { Status = WildBattleStatus.Paralysis };
            var frame = Frame(bodies);
            if (safeguard) frame = frame with { Sides = frame.Sides.SetItem(target & 1,
                frame.Sides[target & 1] with { SafeguardTurnsRemaining = 5 }) };
            var result = Find(frame, actor, target, QualifyingBurn, "Safeguard or existing-status blocked Blaze Kick secondary");
            Assert.IsTrue(result.Events.OfType<SourceHpChangedEvent>().Single().Damage > 0);
            Assert.AreEqual(safeguard ? WildBattleStatus.None : WildBattleStatus.Paralysis, result.State.Slots[target].Body!.Status);
            Assert.AreEqual(0, result.Events.OfType<SourceStatusChangedEvent>().Count());
            Assert.AreEqual(0, result.Events.OfType<SourceMoveFailedEvent>().Count());
            Assert.AreEqual(0, result.Events.OfType<SourceAbilityPreventedEvent>().Count());
            AssertProjection(result, 299, true, actor, target);
        }
    }
}

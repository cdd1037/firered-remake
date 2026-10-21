using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class CosmicPowerKernelDispatchTests
{
    private static readonly MoveId CosmicPower = new("pret:MOVE_COSMIC_POWER");
    private static SourceMechanicsCatalog Mechanics => CurrentCampaignFixture.Catalog.PokemonContent!.Pokemon.Mechanics;
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("cosmic-power", new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("cosmic-power", null!, new(), events,
            index, null!, null!, CampaignBattlePhase.Events, [], 0, []);

    private static WildBattleCombatant NaturalMon(string species, int level, string move)
    {
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        MoveId id = new("pret:MOVE_" + move);
        Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == id && m.Level <= level),
            $"The pinned natural learnset must contain {species}/{level}/{move}.");
        int pp = Mechanics.Catalog.Moves[id].Pp;
        return new(pokemon with { Moves = [new(id, pp, pp)] });
    }

    private static WildBattleCombatant[] Bodies(int actor, string species = "CLEFAIRY", int level = 33)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("MAGIKARP", 5, "SPLASH")).ToArray();
        bodies[actor] = NaturalMon(species, level, "COSMIC_POWER");
        return bodies;
    }

    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies)
    {
        static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
            new(body.Pokemon, body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
        SourceBattleFrame frame = new()
        {
            Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version, BattleKind = WildBattleCalledBattleKind.Trainer,
            Rng = new(31), Parties = [new(BattleSide.Player, [Member(bodies[0]), Member(bodies[2])]),
                new(BattleSide.Opponent, [Member(bodies[1]), Member(bodies[3])])],
            Slots = Enumerable.Range(0, 4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray()
        };
        var entry = SourceFourEntryAbilities.InitializeEntry(Mechanics.Catalog, frame, frame.Revision);
        Assert.IsTrue(entry.Succeeded, entry.Fault);
        Assert.IsNull(SourceBattleIdentityRules.Validate(entry.State));
        Assert.IsFalse(entry.Events.OfType<SourceHeldItemConsumedEvent>().Any());
        return entry.State with { Rng = new(31) };
    }

    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor)
    {
        string before = JsonSerializer.Serialize(frame);
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(actor)));
        Assert.IsTrue(result.Succeeded, result.Fault);
        Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.AreEqual(before, JsonSerializer.Serialize(frame), "The public action must not mutate its input.");
        Assert.AreEqual(frame.Rng, result.State.Rng, "Ordinary self Cosmic Power has no accuracy or effect roll.");
        Assert.AreEqual(0, result.Trace.Length);
        Assert.AreEqual(frame.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp - 1,
            result.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);
        Assert.AreEqual(frame.Slots[actor].Body!.Pokemon.CurrentHp, result.State.Slots[actor].Body!.Pokemon.CurrentHp);
        for (int slot = 0; slot < 4; slot++)
            if (slot != actor) Assert.AreEqual(frame.Slots[slot].Body, result.State.Slots[slot].Body);
        return result;
    }

    private static ImmutableArray<SourceFourActionResult> Turn(SourceBattleFrame frame)
    {
        var choices = frame.Slots.Select(c => new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, 0, c.Slot)).ToImmutableArray();
        var next = SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision, choices);
        var batches = ImmutableArray.CreateBuilder<SourceFourActionResult>();
        for (int step = 0; step < 16; step++)
        {
            Assert.IsTrue(next.Succeeded, next.Fault);
            Assert.IsNull(SourceBattleIdentityRules.Validate(next.State));
            batches.Add(next);
            if (next.CompletedCleanTurn) return batches.ToImmutable();
            Assert.IsNotNull(next.Continuation);
            next = SourceFourActionTurns.Advance(Mechanics.Catalog, next.State, next.Continuation.Value);
        }
        throw new InvalidOperationException("The public Cosmic Power turn did not reach its clean boundary.");
    }

    private static SourceBattleEvent[] AssertPair(ImmutableArray<SourceBattleEvent> events, int actor, int defense, int specialDefense)
    {
        var used = events.OfType<SourceMoveUsedEvent>().Single(e => e.Move == CosmicPower);
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        Assert.AreEqual(actor, used.Actor.Value); Assert.AreEqual(actor, used.Target.Value);
        Assert.AreEqual(CosmicPower, attack.ChosenMove); Assert.AreEqual(CosmicPower, attack.CurrentMove);
        Assert.AreEqual(CosmicPower, pp.ChosenMove); Assert.AreEqual(CosmicPower, pp.CurrentMove);
        Assert.AreEqual(actor, pp.PpOwner.Value); Assert.AreEqual(1, pp.CommandCost);
        Assert.AreEqual(events.IndexOf(attack) + 1, events.IndexOf(used));
        Assert.AreEqual(events.IndexOf(used) + 1, events.IndexOf(pp));
        var pair = events.Skip(events.IndexOf(pp) + 1).Take(2).ToArray();
        Assert.AreEqual(2, pair.Length);
        AssertResult(pair[0], WildBattleStat.Defense, defense);
        AssertResult(pair[1], WildBattleStat.SpecialDefense, specialDefense);
        Assert.IsTrue(pp.Sequence < pair[0].Sequence && pair[0].Sequence < pair[1].Sequence);
        Assert.AreEqual(2, events.Count(e => e is SourceStatChangedEvent or SourceStatAtMaximumEvent));
        Assert.AreEqual(0, events.OfType<SourceMoveFailedEvent>().Count());
        Assert.AreEqual(0, events.OfType<SourceAbilityPreventedEvent>().Count());
        return pair;

        void AssertResult(SourceBattleEvent receipt, WildBattleStat stat, int before)
        {
            if (before == 12)
            {
                Assert.IsInstanceOfType<SourceStatAtMaximumEvent>(receipt);
                var capped = (SourceStatAtMaximumEvent)receipt;
                Assert.AreEqual(stat, capped.Stat); Assert.AreEqual(actor, capped.Actor.Value); Assert.AreEqual(actor, capped.Target.Value);
            }
            else
            {
                Assert.IsInstanceOfType<SourceStatChangedEvent>(receipt);
                var changed = (SourceStatChangedEvent)receipt;
                Assert.AreEqual(stat, changed.Stat); Assert.AreEqual(actor, changed.Actor.Value); Assert.AreEqual(actor, changed.Target.Value);
                Assert.AreEqual(before, changed.Before); Assert.AreEqual(before + 1, changed.After);
            }
        }
    }

    private static void AssertDispatch(ImmutableArray<SourceBattleEvent> events, int actor, long first, int argument)
    {
        string before = JsonSerializer.Serialize(events);
        var used = events.OfType<SourceMoveUsedEvent>().Single(e => e.Move == CosmicPower);
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        var invocation = Double(events, events.IndexOf(used));
        Assert.AreEqual<long?>(first, CampaignStatAnimationPresentation.GroupedMoveResultSequence(invocation,moves:CurrentCampaignFixture.Catalog.PokemonContent!.Catalog.Moves));
        for (int index = 0; index < events.Length; index++)
        {
            var battle = Double(events, index);
            var root = AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On);
            if (events[index] == used)
            {
                Assert.IsNotNull(root.Dispatch, root.Reason);
                Assert.AreEqual(322, root.Dispatch.SourceMoveId); Assert.AreEqual(first, root.Dispatch.ResultSequence);
                Assert.AreEqual(actor, root.Dispatch.Actor.Value); Assert.AreEqual(actor, root.Dispatch.Target.Value);
                Assert.AreEqual(attack.Sequence, root.Dispatch.AttackSequence); Assert.AreEqual(pp.Sequence, root.Dispatch.PpSequence);
                Assert.IsFalse(root.Dispatch.HitsSubstitute);
            }
            else Assert.IsNull(root.Dispatch, "Only the actual attack-text receipt may own the dedicated root.");
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.Off).Dispatch);
            var wave = AnimationDispatchFixture.StatAtResult(battle);
            if (events[index].Sequence == first)
            {
                Assert.IsNotNull(wave, "The source wave runs before the first DEF command, even when DEF is capped.");
                Assert.AreEqual(322, wave.SourceMoveId); Assert.AreEqual(argument, wave.AnimationArgument);
                Assert.AreEqual(actor, wave.Affected.Value); Assert.AreEqual(first, wave.Sequence);
                Assert.AreEqual(attack.Sequence, wave.AttackSequence); Assert.AreEqual(pp.Sequence, wave.PpSequence);
            }
            else Assert.IsNull(wave, "The second stat command and all later receipts cannot replay the grouped wave.");
            Assert.IsNull(AnimationDispatchFixture.StatAtResult(battle, true));
            bool cap = events[index] is SourceStatAtMaximumEvent;
            Assert.AreEqual(cap, CampaignStatAnimationPresentation.IsSilentResultReceipt(battle,moves:CurrentCampaignFixture.Catalog.PokemonContent!.Catalog.Moves));
            if (actor < 2)
            {
                var single = Single(events, index);
                Assert.AreEqual(root.Dispatch, AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.On).Dispatch);
                Assert.AreEqual(wave, AnimationDispatchFixture.StatAtResult(single, true));
                Assert.IsNull(AnimationDispatchFixture.StatAtResult(single, false));
                Assert.IsNull(AnimationDispatchFixture.StatAtResult(single, true, true));
                Assert.AreEqual(cap, CampaignStatAnimationPresentation.IsSilentResultReceipt(single, true,moves:CurrentCampaignFixture.Catalog.PokemonContent!.Catalog.Moves));
                Assert.IsFalse(CampaignStatAnimationPresentation.IsSilentResultReceipt(single, false,moves:CurrentCampaignFixture.Catalog.PokemonContent!.Catalog.Moves));
            }
        }
        Assert.AreEqual(before, JsonSerializer.Serialize(events));
    }

    [TestMethod]
    [DataRow("CLEFAIRY", 33)] [DataRow("LUNATONE", 31)] [DataRow("SOLROCK", 31)]
    public void NaturallyLearnedCosmicPowerHasOneOrderedSelfRootAtEveryPosition(string species, int level)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var batches = Turn(Frame(Bodies(actor, species, level)));
            var batch = batches.Single(b => b.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Move == CosmicPower));
            var pair = AssertPair(batch.Events, actor, 6, 6);
            Assert.AreEqual(7, batch.State.Slots[actor].Body!.DefenseStage);
            Assert.AreEqual(1, batch.State.Slots[actor].Body!.SpecialDefenseStageDelta);
            AssertDispatch(batch.Events, actor, pair[0].Sequence, 55);
        }
    }

    [TestMethod]
    [DataRow(6, 6, 55)] [DataRow(11, 11, 55)]
    [DataRow(12, 6, 19)] [DataRow(12, 11, 19)]
    [DataRow(6, 12, 16)] [DataRow(11, 12, 16)]
    public void PartialCapsKeepBothTypedCommandsButSilenceOnlyCappedTextWithoutExtraRandomness(int defense, int specialDefense, int argument)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var bodies = Bodies(actor);
            bodies[actor] = bodies[actor] with { DefenseStage = defense, SpecialDefenseStageDelta = specialDefense - 6 };
            var result = Resolve(Frame(bodies), actor);
            var pair = AssertPair(result.Events, actor, defense, specialDefense);
            Assert.AreEqual(Math.Min(12, defense + 1), result.State.Slots[actor].Body!.DefenseStage);
            Assert.AreEqual(Math.Min(12, specialDefense + 1) - 6, result.State.Slots[actor].Body!.SpecialDefenseStageDelta);
            // ChangeStatBuffs sets MOVE_RESULT_MISSED on a capped command with
            // STAT_CHANGE_ALLOW_PTR even though Cosmic Power skips its cap text.
            // MOVEEND_UPDATE_LAST_MOVES must retain that native history consequence.
            bool capped = defense == 12 || specialDefense == 12;
            Assert.AreEqual(capped ? new MoveId("pret:MOVE_UNAVAILABLE") : CosmicPower,
                result.State.Slots[actor].Body!.LastLandedMove);
            Assert.AreEqual(capped ? null : "TYPE_PSYCHIC", result.State.Slots[actor].Registers.History!.LastHitByType);
            AssertDispatch(result.Events, actor, pair[0].Sequence, argument);
        }
    }

    [TestMethod]
    [DataRow(12, 6, 19)] [DataRow(6, 12, 16)]
    public void PartialCapInvalidatesLandedMoveButRetainsPriorHitType(int defense, int specialDefense, int argument)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var bodies = Bodies(actor);
            bodies[actor] = bodies[actor] with { DefenseStage = defense, SpecialDefenseStageDelta = specialDefense - 6,
                LastLandedMove = new("pret:MOVE_TACKLE") };
            var frame = Frame(bodies); var cell = frame.Slots[actor];
            frame = frame with { Slots = frame.Slots.SetItem(actor, cell with { Registers = cell.Registers with
                { History = (cell.Registers.History ?? new()) with { LastHitByType = "TYPE_NORMAL" } } }) };
            var result = Resolve(frame, actor);
            var pair = AssertPair(result.Events, actor, defense, specialDefense);
            Assert.AreEqual(Math.Min(12, defense + 1), result.State.Slots[actor].Body!.DefenseStage);
            Assert.AreEqual(Math.Min(12, specialDefense + 1) - 6, result.State.Slots[actor].Body!.SpecialDefenseStageDelta);
            Assert.AreEqual(new MoveId("pret:MOVE_UNAVAILABLE"), result.State.Slots[actor].Body!.LastLandedMove);
            Assert.AreEqual("TYPE_NORMAL", result.State.Slots[actor].Registers.History!.LastHitByType);
            AssertDispatch(result.Events, actor, pair[0].Sequence, argument);
        }
    }

    [TestMethod]
    public void BothCapsFailBeforeEitherStatCommandAndNeverAuthorizeAnimations()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var bodies = Bodies(actor);
            bodies[actor] = bodies[actor] with { DefenseStage = 12, SpecialDefenseStageDelta = 6 };
            var result = Resolve(Frame(bodies), actor);
            var failure = result.Events.OfType<SourceMoveFailedEvent>().Single();
            Assert.AreEqual("stats-at-maximum", failure.Reason); Assert.AreEqual(CosmicPower, failure.Move);
            Assert.AreEqual(actor, failure.Actor.Value); Assert.AreEqual(actor, failure.Target.Value);
            var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreEqual(result.Events.IndexOf(pp) + 1, result.Events.IndexOf(failure));
            Assert.AreEqual(0, result.Events.Count(e => e is SourceStatChangedEvent or SourceStatAtMaximumEvent));
            Assert.AreEqual(12, result.State.Slots[actor].Body!.DefenseStage);
            Assert.AreEqual(6, result.State.Slots[actor].Body!.SpecialDefenseStageDelta);
            Assert.AreEqual(new MoveId("pret:MOVE_UNAVAILABLE"), result.State.Slots[actor].Body!.LastLandedMove);
            for (int index = 0; index < result.Events.Length; index++)
            {
                var battle = Double(result.Events, index);
                Assert.IsNull(CampaignStatAnimationPresentation.GroupedMoveResultSequence(battle,moves:CurrentCampaignFixture.Catalog.PokemonContent!.Catalog.Moves));
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.StatAtResult(battle));
                Assert.IsFalse(CampaignStatAnimationPresentation.IsSilentResultReceipt(battle,moves:CurrentCampaignFixture.Catalog.PokemonContent!.Catalog.Moves), "The whole-move failure text remains visible.");
                if (actor >= 2) continue;
                var single = Single(result.Events, index);
                Assert.IsNull(CampaignStatAnimationPresentation.GroupedMoveResultSequence(single, true,moves:CurrentCampaignFixture.Catalog.PokemonContent!.Catalog.Moves));
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.StatAtResult(single, true));
                Assert.IsFalse(CampaignStatAnimationPresentation.IsSilentResultReceipt(single, true,moves:CurrentCampaignFixture.Catalog.PokemonContent!.Catalog.Moves));
            }
        }
    }

    [TestMethod]
    [DataRow("ABILITY_CLEAR_BODY")] [DataRow("ABILITY_WHITE_SMOKE")]
    [DataRow("ABILITY_HYPER_CUTTER")] [DataRow("ABILITY_KEEN_EYE")]
    public void SelfIncreaseIgnoresMistAndAcquiredStatProtectionAbilitiesWhilePreservingSubstitute(string ability)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var frame = Frame(Bodies(actor));
            var cell = frame.Slots[actor];
            // A source-valid post-Skill Swap register changes only the battle ability,
            // preserving Clefairy's authenticated permanent identity and natural move.
            frame = frame with
            {
                Slots = frame.Slots.SetItem(actor, cell with { Body = cell.Body! with
                    { CalledOverlay = new() { Ability = ability }, CalledVolatile = new() { SubstituteHp = 10 } } }),
                Sides = frame.Sides.SetItem(actor & 1, frame.Sides[actor & 1] with { MistTurnsRemaining = 5 })
            };
            var result = Resolve(frame, actor);
            var pair = AssertPair(result.Events, actor, 6, 6);
            Assert.AreEqual(ability, WildBattleEffective.Ability(result.State.Slots[actor].Body!));
            Assert.AreEqual(10, result.State.Slots[actor].Body!.CalledVolatile!.SubstituteHp);
            Assert.AreEqual(0, result.Events.OfType<SourceSubstituteDamagedEvent>().Count());
            Assert.AreEqual(5, result.State.Sides[actor & 1].MistTurnsRemaining);
            AssertDispatch(result.Events, actor, pair[0].Sequence, 55);
        }
    }

    [TestMethod]
    public void LastPpLeppaActivationBelongsToEndTurnAndCannotReplayCosmicPower()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var bodies = Bodies(actor);
            var mon = bodies[actor];
            bodies[actor] = mon with { HeldItem = "ITEM_LEPPA_BERRY", Pokemon = mon.Pokemon with
                { Moves = mon.Pokemon.Moves.SetItem(0, mon.Pokemon.Moves[0] with { CurrentPp = 1 }) } };
            var batches = Turn(Frame(bodies));
            var moveBatch = batches.Single(b => b.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Move == CosmicPower));
            var pair = AssertPair(moveBatch.Events, actor, 6, 6);
            AssertDispatch(moveBatch.Events, actor, pair[0].Sequence, 55);
            Assert.AreEqual(0, moveBatch.Events.OfType<SourceHeldItemConsumedEvent>().Count(), "Leppa is a Normal end-turn item, not a fabricated move-end suffix.");
            var itemBatch = batches.Single(b => b.Events.OfType<SourceHeldItemConsumedEvent>().Any(e => e.Side.Value == actor));
            var consumed = itemBatch.Events.OfType<SourceHeldItemConsumedEvent>().Single(e => e.Side.Value == actor);
            Assert.AreEqual("ITEM_LEPPA_BERRY", consumed.Item); Assert.IsNotNull(consumed.PpRecovery);
            Assert.AreEqual(CosmicPower, consumed.PpRecovery.Move); Assert.AreEqual(0, consumed.PpRecovery.Before); Assert.AreEqual(10, consumed.PpRecovery.After);
            Assert.IsTrue(consumed.Sequence > pair[1].Sequence);
            int heldCount = 0;
            for (int index = 0; index < itemBatch.Events.Length; index++)
            {
                var battle = Double(itemBatch.Events, index);
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.StatAtResult(battle));
                var held = CampaignHeldItemAnimationPresentation.AtResult(battle, SourceBattleScene.On, new MoveId?[4]);
                if (held is null) continue;
                heldCount++; Assert.AreEqual(consumed.Sequence, held.ReceiptSequence); Assert.AreEqual(actor, held.Affected.Value);
            }
            Assert.AreEqual(1, heldCount);
            Assert.AreEqual(10, batches[^1].State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);
            Assert.AreEqual("ITEM_NONE", batches[^1].State.Slots[actor].Body!.HeldItem);
        }
    }
}

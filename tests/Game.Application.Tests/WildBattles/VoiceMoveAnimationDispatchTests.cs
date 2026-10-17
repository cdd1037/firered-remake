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

// Natural learners and committed public-kernel receipts are the integration authority.
// Growl owns one attack root and one Attack-down wave per successful target pass.
// Hyper Voice owns one attack root, including a real zero-loss Endure result; typed
// prevention/effectiveness receipts, never HP deltas, distinguish immune passes.
[TestClass, TestCategory("Game")]
public sealed class VoiceMoveAnimationDispatchTests
{
    private static SourceMechanicsCatalog Mechanics => TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static MoveId Move(int id) => new("pret:MOVE_" + (id switch { 45 => "GROWL", 304 => "HYPER_VOICE", _ => throw new ArgumentOutOfRangeException(nameof(id)) }));
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("voice", new("test"), "test", new(), CampaignDoubleBattleStage.Combat, events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("voice", null!, new(), events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(), index, null!, null!, CampaignBattlePhase.Events, [], 0, []) { SourceEvents = events };

    private static WildBattleCombatant Mon(string species, string name)
    {
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), 50, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        var move = new MoveId("pret:MOVE_" + name);
        Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(x => x.Move == move && x.Level <= 50),
            $"Require a natural source learner: {species}/50/{name}.");
        int pp = Mechanics.Catalog.Moves[move].Pp;
        return new(pokemon with { Moves = [new(move, pp, pp)] });
    }
    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Mon("SNORLAX", "TACKLE")).ToArray();
        bodies[actor] = Mon(id == 45 ? "BULBASAUR" : "WHISMUR", id == 45 ? "GROWL" : "HYPER_VOICE") with { AccuracyStage = 12 };
        return bodies;
    }
    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies, bool single = false)
    {
        static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
            new(body.Pokemon, body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
        var frame = new SourceBattleFrame {
            Layout = single ? SourceBattleLayout.Singles : SourceBattleLayout.PresetDoubles,
            KoReplacementTiming = single ? KoReplacementTiming.SourceImmediate : KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version,
            BattleKind = single ? WildBattleCalledBattleKind.Wild : WildBattleCalledBattleKind.Trainer,
            SourceAbsentMask = (byte)(single ? 12 : 0), TurnStartAbsentMask = (byte)(single ? 12 : 0), Rng = new(31),
            Parties = [new(BattleSide.Player, single ? [Member(bodies[0])] : [Member(bodies[0]), Member(bodies[2])]),
                new(BattleSide.Opponent, single ? [Member(bodies[1])] : [Member(bodies[1]), Member(bodies[3])])],
            Slots = Enumerable.Range(0, 4).Select(i => single && i >= 2 ? new SourceBattleSlot(new(i), null, 0, null)
                : new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray()
        };
        var entry = SourceFourEntryAbilities.InitializeEntry(Mechanics.Catalog, frame, frame.Revision);
        Assert.IsTrue(entry.Succeeded, entry.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(entry.State));
        return entry.State with { Rng = new(0) };
    }
    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor = 0, int target = 1)
    {
        Assert.IsNull(SourceBattleIdentityRules.Validate(frame));
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty); return result;
    }
    private static SourceKernelResult Find(WildBattleCombatant[] bodies, int actor, int target,
        Func<SourceKernelResult, bool> predicate, string branch, bool single = false)
    {
        var entered = Frame(bodies, single);
        // Bounded branch discovery observes real receipts; it never regenerates golden data.
        for (uint seed = 0; seed < 1024; seed++)
        {
            var result = Resolve(entered with { Rng = new(seed) }, actor, target);
            if (predicate(result)) return result;
        }
        throw new InvalidOperationException($"No actual {branch} in 1024 action-start seeds.");
    }
    private static BattlerSlot Target(SourceBattleEvent e) => e switch {
        SourceStatChangedEvent s => s.Target, SourceHpChangedEvent h => h.Target,
        SourceSubstituteDamagedEvent s => s.Target, _ => throw new InvalidOperationException("Expected a committed voice result.")
    };
    private static SourceBattleEvent[] Outcomes(SourceKernelResult result, int id, int actor)
    {
        var move = Move(id); var events = result.Events;
        return events.Where(e => id == 45
            ? e is SourceStatChangedEvent s && s.Actor.Value == actor && s.Stat == WildBattleStat.Attack && s.After < s.Before
            : e is SourceHpChangedEvent h && h.Actor.Value == actor && h.Move == move ||
              e is SourceSubstituteDamagedEvent d && d.Actor.Value == actor && d.Move == move)
            .Where(outcome => {
                var target = Target(outcome);
                var owner = events.OfType<SourcePpCommandEvent>().LastOrDefault(p => p.Sequence < outcome.Sequence);
                Assert.IsNotNull(owner, "Every actual result must have its own preceding source PP pass.");
                Assert.AreEqual(actor, owner.Actor.Value); Assert.AreEqual(new BattlerSlot(actor), owner.PpOwner);
                Assert.AreEqual(move, owner.CurrentMove); Assert.AreEqual(target, owner.Target);
                var next = events.OfType<SourcePpCommandEvent>().FirstOrDefault(p => p.Sequence > owner.Sequence);
                return !events.Any(e => e.Sequence > owner.Sequence && (next is null || e.Sequence < next.Sequence) &&
                    (e is SourceMoveMissedEvent miss && miss.Actor.Value == actor && miss.Target == target && miss.Move == move ||
                     e is SourceMoveFailedEvent failed && failed.Actor.Value == actor && failed.Target == target && failed.Move == move ||
                     e is SourceAbilityPreventedEvent prevented && prevented.Actor.Value == actor && prevented.Target == target && prevented.Move == move ||
                     e is SourceEffectivenessEvent effectiveness && effectiveness.Actor.Value == actor && effectiveness.Target == target &&
                         effectiveness.Move == move && effectiveness.Effectiveness == WildBattleEffectiveness.NoEffect));
            }).ToArray();
    }
    private static void Check(SourceKernelResult result, int id, int actor, bool single = false)
    {
        string snapshot = JsonSerializer.Serialize(result);
        var events = result.Events; var outcomes = Outcomes(result, id, actor);
        var used = events.OfType<SourceMoveUsedEvent>().Single(); var attack = events.OfType<SourceAttackStringEvent>().Single();
        Assert.AreEqual(Move(id), used.Move); Assert.AreEqual(actor, used.Actor.Value);
        int roots = 0, waves = 0;
        for (int i = 0; i < events.Length; i++)
        {
            CampaignMoveAnimationDecision At(SourceBattleScene scene) => single
                ? AnimationDispatchFixture.AtAttackText(Single(events, i), true, scene)
                : AnimationDispatchFixture.AtAttackText(Double(events, i), scene);
            var decision = At(SourceBattleScene.On);
            Assert.AreEqual(decision, At(SourceBattleScene.On)); Assert.IsNull(At(SourceBattleScene.Off).Dispatch);
            Assert.IsNull(single ? AnimationDispatchFixture.AtHitResult(Single(events, i), true, SourceBattleScene.On).Dispatch
                : AnimationDispatchFixture.AtHitResult(Double(events, i), SourceBattleScene.On).Dispatch);
            if (single) Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, i), false, SourceBattleScene.On).Dispatch);
            if (events[i] == used && outcomes.Length > 0)
            {
                var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); roots++;
                var first = outcomes[0]; var target = Target(first);
                var owner = events.OfType<SourcePpCommandEvent>().Single(p => p.Target == target);
                Assert.AreEqual(id, cue.SourceMoveId); Assert.AreEqual(actor, cue.Actor.Value); Assert.AreEqual(target, cue.Target);
                Assert.AreEqual(used.Sequence, cue.Sequence); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
                Assert.AreEqual(first.Sequence, cue.ResultSequence); Assert.AreEqual(owner.Sequence, cue.PpSequence);
                Assert.IsTrue(owner.Sequence < first.Sequence); Assert.IsTrue(cue.AcknowledgesCurrentReceipt); Assert.IsNull(cue.MoveTurn);
                Assert.AreEqual(first is SourceSubstituteDamagedEvent, cue.HitsSubstitute);
            }
            else Assert.IsNull(decision.Dispatch, $"{events[i].GetType().Name} must not create another voice root.");
            var wave = single ? AnimationDispatchFixture.StatAtResult(Single(events, i), true)
                : AnimationDispatchFixture.StatAtResult(Double(events, i));
            if (id == 45 && outcomes.Contains(events[i]))
            {
                Assert.IsNotNull(wave); waves++; var stat = (SourceStatChangedEvent)events[i];
                Assert.AreEqual(45, wave.SourceMoveId); Assert.AreEqual(22, wave.AnimationArgument);
                Assert.AreEqual(stat.Target, wave.Affected); Assert.AreEqual(stat.Sequence, wave.Sequence);
                Assert.AreEqual(attack.Sequence, wave.AttackSequence);
                Assert.AreEqual(events.OfType<SourcePpCommandEvent>().Single(p => p.Target == stat.Target).Sequence, wave.PpSequence);
                Assert.AreEqual(Math.Max(0, stat.Before - 1), stat.After);
            }
            else Assert.IsNull(wave, "Item restoration and later receipts cannot replay a move stat wave.");
            Assert.IsNull(single ? AnimationDispatchFixture.StatAtResult(Single(events, i), true, true)
                : AnimationDispatchFixture.StatAtResult(Double(events, i), true));
            if (single) Assert.IsNull(AnimationDispatchFixture.StatAtResult(Single(events, i), false));
        }
        Assert.AreEqual(outcomes.Length == 0 ? 0 : 1, roots); Assert.AreEqual(id == 45 ? outcomes.Length : 0, waves);
        Assert.AreEqual(snapshot, JsonSerializer.Serialize(result), "Projection must preserve committed frame, RNG, trace and every receipt exactly.");
    }
    private static void Pp(SourceKernelResult result, int actor, int target, bool single = false, int cost = 1)
    {
        var rows = result.Events.OfType<SourcePpCommandEvent>().ToArray(); var used = result.Events.OfType<SourceMoveUsedEvent>().Single();
        Assert.AreEqual(single ? 1 : 2, rows.Length);
        CollectionAssert.AreEqual(single ? new[] { target } : new[] { target, target ^ 2 }, rows.Select(p => p.Target.Value).ToArray());
        Assert.AreEqual(used.PpBefore, rows[0].Before); Assert.AreEqual(used.PpAfter, rows[0].After);
        foreach (var row in rows)
        {
            Assert.AreEqual(actor, row.Actor.Value); Assert.AreEqual(actor, row.PpOwner.Value); Assert.AreEqual(cost, row.CommandCost);
            Assert.AreEqual(used.Move, row.ChosenMove); Assert.AreEqual(used.Move, row.CurrentMove); Assert.AreEqual(used.Slot, row.PpSlot);
            Assert.AreEqual(used.Turn, row.Turn); Assert.IsTrue(row.Sequence > used.Sequence);
        }
        Assert.AreEqual(Math.Max(0, rows[0].Before - cost), rows[0].After);
        foreach (var row in rows.Skip(1)) { Assert.AreEqual(rows[0].After, row.Before); Assert.AreEqual(row.Before, row.After); }
        Assert.AreEqual(rows[^1].After, result.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);
    }

    [TestMethod, DataRow(45), DataRow(304)]
    public void NaturalVoiceMovesUseOneRootAndActualSpreadReceiptsAtEveryPosition(int id)
    {
        Assert.AreEqual("MOVE_TARGET_BOTH", Mechanics.Catalog.Moves[Move(id)].Target);
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in single ? new[] { actor ^ 1 } : new[] { actor ^ 1, actor ^ 3 })
        {
            var result = Resolve(Frame(Bodies(id, actor), single), actor, target);
            Assert.AreEqual(single ? 1 : 2, Outcomes(result, id, actor).Length);
            Assert.IsFalse(result.Events.OfType<SourceMoveFailedEvent>().Any()); Pp(result, actor, target, single); Check(result, id, actor, single);
        }
    }
    [TestMethod, DataRow(45), DataRow(304)]
    public void ActualProtectedPassesCannotStealTheSuccessfulPartnersRoot(int id)
    {
        for (int actor = 0; actor < 4; actor++) foreach (bool firstBlocked in new[] { false, true })
        {
            int target = actor ^ 1, blocked = firstBlocked ? target : target ^ 2; var bodies = Bodies(id, actor);
            bodies[blocked] = bodies[blocked] with { Protected = true };
            var result = Resolve(Frame(bodies), actor, target);
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(f => f.Target.Value == blocked && f.Reason == "protected"));
            Assert.AreEqual(blocked ^ 2, Target(Outcomes(result, id, actor).Single()).Value);
            Pp(result, actor, target); Check(result, id, actor);
        }
    }
    [TestMethod, DataRow("cap"), DataRow("substitute"), DataRow("clear-body"), DataRow("white-smoke"), DataRow("hyper-cutter")]
    public void GrowlBlockedPassKeepsOnlyTheOtherTargetsActualAttackReduction(string branch)
    {
        for (int actor = 0; actor < 4; actor++) foreach (bool firstBlocked in new[] { false, true })
        {
            int target = actor ^ 1, blocked = firstBlocked ? target : target ^ 2; var bodies = Bodies(45, actor);
            bodies[blocked] = branch switch {
                "cap" => bodies[blocked] with { AttackStage = 0 },
                "substitute" => bodies[blocked] with { CalledVolatile = new() { SubstituteHp = 1 } },
                "clear-body" => Mon("TENTACOOL", "ACID"), "white-smoke" => Mon("TORKOAL", "EMBER"),
                "hyper-cutter" => Mon("KRABBY", "BUBBLE"), _ => throw new InvalidOperationException()
            };
            string? ability = branch switch { "clear-body" => "ABILITY_CLEAR_BODY", "white-smoke" => "ABILITY_WHITE_SMOKE", "hyper-cutter" => "ABILITY_HYPER_CUTTER", _ => null };
            if (ability is not null) Assert.AreEqual(ability, WildBattleEffective.Ability(bodies[blocked]));
            var result = Resolve(Frame(bodies), actor, target);
            Assert.AreEqual(blocked ^ 2, Target(Outcomes(result, 45, actor).Single()).Value);
            if (branch == "cap") Assert.AreEqual(blocked, result.Events.OfType<SourceStatAtMinimumEvent>().Single().Target.Value);
            else if (ability is not null) Assert.IsTrue(result.Events.OfType<SourceAbilityPreventedEvent>().Any(a => a.Target.Value == blocked && a.Ability == ability));
            else Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(f => f.Target.Value == blocked && f.Reason == "substitute"));
            Pp(result, actor, target); Check(result, 45, actor);
        }
    }
    [TestMethod]
    public void HyperVoiceSubstituteBreakAndKoStillOwnOnlyOneRoot()
    {
        for (int actor = 0; actor < 4; actor++) foreach (bool firstSubstitute in new[] { false, true })
        {
            int target = actor ^ 1, substitute = firstSubstitute ? target : target ^ 2, ko = substitute ^ 2; var bodies = Bodies(304, actor);
            bodies[substitute] = bodies[substitute] with { CalledVolatile = new() { SubstituteHp = 1 } };
            bodies[ko] = bodies[ko] with { Pokemon = bodies[ko].Pokemon with { CurrentHp = 1 } };
            var result = Resolve(Frame(bodies), actor, target);
            var broken = result.Events.OfType<SourceSubstituteDamagedEvent>().Single(); Assert.AreEqual(substitute, broken.Target.Value); Assert.AreEqual(0, broken.HpAfter);
            var fainted = result.Events.OfType<SourceHpChangedEvent>().Single(); Assert.AreEqual(ko, fainted.Target.Value); Assert.AreEqual(0, fainted.HpAfter);
            Assert.IsTrue(result.Events.OfType<SourceFaintedEvent>().Any(f => f.Side.Value == ko)); Assert.AreEqual(2, Outcomes(result, 304, actor).Length);
            Assert.AreEqual(bodies[substitute].Pokemon.CurrentHp, result.State.Slots[substitute].Body!.Pokemon.CurrentHp);
            Pp(result, actor, target); Check(result, 304, actor);
        }
    }
    [TestMethod, DataRow(45), DataRow(304)]
    public void ActualMixedAccuracyPassesKeepTheFirstSuccessfulReceiptAndItsOwnPp(int id)
    {
        for (int actor = 0; actor < 4; actor++) foreach (bool firstMisses in new[] { false, true })
        {
            int target = actor ^ 1, missed = firstMisses ? target : target ^ 2; var bodies = Bodies(id, actor);
            bodies[actor] = bodies[actor] with { AccuracyStage = 0 };
            var result = Find(bodies, actor, target, r => Outcomes(r, id, actor).Length == 1 &&
                Target(Outcomes(r, id, actor)[0]).Value == (missed ^ 2) && r.Events.OfType<SourceMoveMissedEvent>().Any(m => m.Target.Value == missed), "voice mixed hit/miss");
            Pp(result, actor, target); Check(result, id, actor);
        }
    }
    [TestMethod, DataRow(45), DataRow(304)]
    public void SoundproofPassesCannotOwnSuccessfulVoiceReceipts(int id)
    {
        for (int actor = 0; actor < 4; actor++) foreach (bool firstBlocked in new[] { false, true })
        {
            int target = actor ^ 1, blocked = firstBlocked ? target : target ^ 2; var bodies = Bodies(id, actor);
            bodies[blocked] = Mon("WHISMUR", "POUND"); Assert.AreEqual("ABILITY_SOUNDPROOF", WildBattleEffective.Ability(bodies[blocked]));
            var result = Resolve(Frame(bodies), actor, target);
            var prevention = result.Events.OfType<SourceAbilityPreventedEvent>().Single(a => a.Ability == "ABILITY_SOUNDPROOF");
            Assert.AreEqual(blocked, prevention.Target.Value); Assert.AreEqual(Move(id), prevention.Move);
            Assert.AreEqual(blocked ^ 2, Target(Outcomes(result, id, actor).Single()).Value); Pp(result, actor, target); Check(result, id, actor);
        }
    }
    [TestMethod, DataRow(45), DataRow(304)]
    public void AllSoundproofProtectedOrMissedPassesNeverInventARootOrWave(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (string branch in new[] { "soundproof", "protect", "miss" })
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor);
            foreach (int blocked in single ? new[] { target } : new[] { target, target ^ 2 })
                bodies[blocked] = branch switch { "soundproof" => Mon("WHISMUR", "POUND"), "protect" => bodies[blocked] with { Protected = true }, _ => bodies[blocked] };
            if (branch == "miss") bodies[actor] = bodies[actor] with { AccuracyStage = 0 };
            var result = branch == "miss" ? Find(bodies, actor, target,
                r => r.Events.OfType<SourceMoveMissedEvent>().Count() == (single ? 1 : 2), "all voice passes miss", single)
                : Resolve(Frame(bodies, single), actor, target);
            Assert.AreEqual(0, Outcomes(result, id, actor).Length);
            if (branch == "soundproof") Assert.AreEqual(single ? 1 : 2, result.Events.OfType<SourceAbilityPreventedEvent>().Count(a => a.Ability == "ABILITY_SOUNDPROOF"));
            Pp(result, actor, target, single); Check(result, id, actor, single);
        }
    }
    [TestMethod]
    public void NormalTypeImmunityIsNotConfusedWithZeroLossEndureOrGrowl()
    {
        for (int actor = 0; actor < 4; actor++) foreach (bool firstImmune in new[] { false, true })
        {
            int target = actor ^ 1, immune = firstImmune ? target : target ^ 2, endured = immune ^ 2;
            var bodies = Bodies(304, actor); bodies[immune] = Mon("GASTLY", "LICK");
            bodies[endured] = bodies[endured] with { Enduring = true, Pokemon = bodies[endured].Pokemon with { CurrentHp = 1 } };
            var result = Resolve(Frame(bodies), actor, target);
            Assert.IsTrue(result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Target.Value == immune && e.Effectiveness == WildBattleEffectiveness.NoEffect));
            var adjustment = result.Events.OfType<SourceDamageAdjustedEvent>().Single(a => a.Target.Value == endured);
            Assert.IsTrue(adjustment.Endured); Assert.IsTrue(adjustment.DamageBefore > 0); Assert.AreEqual(0, adjustment.DamageAfter);
            var eligible = (SourceHpChangedEvent)Outcomes(result, 304, actor).Single();
            Assert.AreEqual(endured, eligible.Target.Value); Assert.AreEqual(1, eligible.HpBefore); Assert.AreEqual(1, eligible.HpAfter);
            Assert.AreEqual(bodies[immune].Pokemon.CurrentHp, result.State.Slots[immune].Body!.Pokemon.CurrentHp);
            Pp(result, actor, target); Check(result, 304, actor);
            var growlBodies = Bodies(45, actor); growlBodies[target] = Mon("GASTLY", "LICK"); growlBodies[target ^ 2] = Mon("GASTLY", "LICK");
            var growl = Resolve(Frame(growlBodies), actor, target); Assert.AreEqual(2, Outcomes(growl, 45, actor).Length);
            Assert.IsFalse(growl.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect)); Check(growl, 45, actor);
        }
    }
    [TestMethod]
    public void BothNormalImmuneTargetsNeverCreateAHyperVoiceRoot()
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(304, actor); bodies[target] = Mon("GASTLY", "LICK"); bodies[target ^ 2] = Mon("GASTLY", "LICK");
            var result = Resolve(Frame(bodies, single), actor, target);
            Assert.AreEqual(single ? 1 : 2, result.Events.OfType<SourceEffectivenessEvent>().Count(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
            Assert.AreEqual(0, Outcomes(result, 304, actor).Length); Pp(result, actor, target, single); Check(result, 304, actor, single);
        }
    }
    [TestMethod]
    public void GrowlActualCapsAndOneRemainingAttackStageUseTheRequestedOneStageWave()
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++) foreach (int stage in new[] { 0, 1 })
        {
            int target = actor ^ 1; var bodies = Bodies(45, actor);
            bodies[target] = bodies[target] with { AttackStage = stage }; bodies[target ^ 2] = bodies[target ^ 2] with { AttackStage = stage };
            var result = Resolve(Frame(bodies, single), actor, target); var outcomes = Outcomes(result, 45, actor);
            Assert.AreEqual(stage == 0 ? 0 : single ? 1 : 2, outcomes.Length);
            foreach (var reduction in outcomes.Cast<SourceStatChangedEvent>()) { Assert.AreEqual(1, reduction.Before); Assert.AreEqual(0, reduction.After); }
            Assert.AreEqual(stage == 0 ? single ? 1 : 2 : 0, result.Events.OfType<SourceStatAtMinimumEvent>().Count());
            Pp(result, actor, target, single); Check(result, 45, actor, single);
        }
    }
    [TestMethod, DataRow(45), DataRow(304)]
    public void PressureCostsAndLastPpClampPreserveEveryActualPass(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++) foreach (int remaining in new[] { 1, 2, 5 })
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor);
            bodies[target] = Mon("ARTICUNO", "GUST"); bodies[target ^ 2] = Mon("ARTICUNO", "GUST");
            Assert.AreEqual("ABILITY_PRESSURE", WildBattleEffective.Ability(bodies[target]));
            bodies[actor] = bodies[actor] with { Pokemon = bodies[actor].Pokemon with {
                Moves = bodies[actor].Pokemon.Moves.SetItem(0, bodies[actor].Pokemon.Moves[0] with { CurrentPp = remaining }) } };
            var result = Resolve(Frame(bodies, single), actor, target);
            Assert.AreEqual(single ? 1 : 2, Outcomes(result, id, actor).Length);
            Pp(result, actor, target, single, single ? 2 : 3); Check(result, id, actor, single);
        }
    }
    [TestMethod]
    public void GrowlWhiteHerbSuffixAuthenticatesItsOwnRestorationWithoutReplayingRootOrWaves()
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (bool firstHerb in single ? new[] { true } : new[] { false, true })
        {
            int target = actor ^ 1, holder = firstHerb ? target : target ^ 2; var bodies = Bodies(45, actor);
            bodies[holder] = bodies[holder] with { HeldItem = "ITEM_WHITE_HERB" };
            var result = Resolve(Frame(bodies, single), actor, target); var events = result.Events;
            var reduced = events.OfType<SourceStatChangedEvent>().Single(s => s.Actor.Value == actor && s.Target.Value == holder);
            var restored = events.OfType<SourceStatChangedEvent>().Single(s => s.Actor.Value == holder && s.Target.Value == holder);
            var consumed = events.OfType<SourceHeldItemConsumedEvent>().Single();
            Assert.AreEqual("ITEM_WHITE_HERB", consumed.Item); Assert.AreEqual(holder, consumed.Side.Value);
            Assert.AreEqual(WildBattleStat.Attack, restored.Stat); Assert.AreEqual(5, restored.Before); Assert.AreEqual(6, restored.After);
            Assert.IsNotNull(consumed.StatChanges); Assert.AreEqual(1, consumed.StatChanges.Value.Length);
            Assert.AreEqual(new SourceItemStatChange(WildBattleStat.Attack, 5, 6), consumed.StatChanges.Value[0]);
            Assert.IsTrue(reduced.Sequence < restored.Sequence && restored.Sequence < consumed.Sequence);
            Assert.AreEqual(6, result.State.Slots[holder].Body!.AttackStage); Assert.AreEqual("ITEM_NONE", result.State.Slots[holder].Body!.HeldItem);
            Assert.AreEqual(single ? 1 : 2, Outcomes(result, 45, actor).Length); Pp(result, actor, target, single); Check(result, 45, actor, single);
            string snapshot = JsonSerializer.Serialize(result); int heldRoots = 0;
            for (int i = 0; i < events.Length; i++)
            {
                var held = single ? CampaignHeldItemAnimationPresentation.AtResult(Single(events, i), true, SourceBattleScene.On, new MoveId?[2])
                    : CampaignHeldItemAnimationPresentation.AtResult(Double(events, i), SourceBattleScene.On, new MoveId?[4]);
                if (events[i] == restored)
                {
                    Assert.IsNotNull(held); heldRoots++; Assert.AreEqual(restored.Sequence, held.Sequence);
                    Assert.AreEqual(consumed.Sequence, held.ReceiptSequence); Assert.AreEqual(holder, held.Affected.Value);
                }
                else Assert.IsNull(held);
            }
            Assert.AreEqual(1, heldRoots); Assert.AreEqual(snapshot, JsonSerializer.Serialize(result));
            // A forged restoration suffix must not acquire an item animation or replay a move wave.
            int itemIndex = events.IndexOf(consumed), restoredIndex = events.IndexOf(restored);
            foreach (var invalid in new[] { events.RemoveAt(itemIndex), events.SetItem(itemIndex,
                consumed with { StatChanges = [new(WildBattleStat.Attack, 4, 6)] }) })
            {
                Assert.IsNull(single ? CampaignHeldItemAnimationPresentation.AtResult(Single(invalid, restoredIndex), true, SourceBattleScene.On, new MoveId?[2])
                    : CampaignHeldItemAnimationPresentation.AtResult(Double(invalid, restoredIndex), SourceBattleScene.On, new MoveId?[4]));
                Assert.IsNull(single ? AnimationDispatchFixture.StatAtResult(Single(invalid, restoredIndex), true)
                    : AnimationDispatchFixture.StatAtResult(Double(invalid, restoredIndex)));
                if (!single)
                {
                    int usedIndex = Array.FindIndex(invalid.ToArray(), e => e is SourceMoveUsedEvent);
                    Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(invalid, usedIndex), SourceBattleScene.On).Dispatch,
                        "An unauthenticated self-restoration cannot be discarded from a BOTH-target stat invocation.");
                }
            }
        }
    }
    [TestMethod, DataRow(45), DataRow(304)]
    public void MissingMetadataOrResolvedRootNeverAdmitsVoiceAnimation(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = Resolve(Frame(Bodies(id), single)); int index = Array.FindIndex(result.Events.ToArray(), e => e is SourceMoveUsedEvent);
            if (single)
            {
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(result.Events, index), true, SourceBattleScene.On, null).Dispatch);
                Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(Single(result.Events, index), true, SourceBattleScene.On, Mechanics.Catalog.Moves, (_, _) => false).Dispatch);
            }
            else
            {
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(result.Events, index), SourceBattleScene.On, null).Dispatch);
                Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(Double(result.Events, index), SourceBattleScene.On, Mechanics.Catalog.Moves, (_, _) => false).Dispatch);
            }
        }
    }
}

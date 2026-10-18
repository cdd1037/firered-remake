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

// Real source definitions and natural learners exercise the public kernel. These
// are controlled mechanics fixtures, not claims of natural campaign traversal.
[TestClass, TestCategory("Game")]
public sealed class SpreadSecondaryKernelTests
{
    private static SourceMechanicsCatalog Mechanics =>
        TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static ImmutableDictionary<MoveId, PokemonMoveDefinition> Moves => Mechanics.Catalog.Moves;
    private static MoveId Id(string name) => new("pret:MOVE_" + name);
    private static (string Species, int Level, WildBattleStat Stat, int Argument, int SourceId) Spec(string name) => name switch
    {
        "ACID" => ("TENTACOOL", 19, WildBattleStat.Defense, 23, 51),
        "BUBBLE" => ("SQUIRTLE", 7, WildBattleStat.Speed, 24, 145),
        "ICY_WIND" => ("SEEL", 17, WildBattleStat.Speed, 24, 196),
        "MUDDY_WATER" => ("MARSHTOMP", 37, WildBattleStat.Accuracy, 27, 330),
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("spread-secondary", new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("spread-secondary", null!, new(), events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(),
            index, null!, null!, CampaignBattlePhase.Events, [], 0, []) { SourceEvents = events };

    private static WildBattleCombatant NaturalMon(string species, int level, string name)
    {
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        var move = Id(name);
        Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level),
            $"Natural source learner required: {species}/{level}/{name}.");
        int pp = Moves[move].Pp;
        return new(pokemon with { Moves = [new(move, pp, pp)] });
    }
    private static WildBattleCombatant[] Bodies(string name, int actor = 0)
    {
        var spec = Spec(name);
        // Neutral Speed is essential: Bubble and Icy Wind must have room to lower it.
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL")).ToArray();
        bodies[actor] = NaturalMon(spec.Species, spec.Level, name) with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }
    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies, bool singles = false)
    {
        static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
            new(body.Pokemon, body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
        var frame = new SourceBattleFrame
        {
            Layout = singles ? SourceBattleLayout.Singles : SourceBattleLayout.PresetDoubles,
            KoReplacementTiming = singles ? KoReplacementTiming.SourceImmediate : KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version,
            BattleKind = singles ? WildBattleCalledBattleKind.Wild : WildBattleCalledBattleKind.Trainer,
            Rng = new(31), SourceAbsentMask = singles ? (byte)12 : (byte)0, TurnStartAbsentMask = singles ? (byte)12 : (byte)0,
            Parties = singles
                ? [new(BattleSide.Player, [Member(bodies[0])]), new(BattleSide.Opponent, [Member(bodies[1])])]
                : [new(BattleSide.Player, [Member(bodies[0]), Member(bodies[2])]),
                   new(BattleSide.Opponent, [Member(bodies[1]), Member(bodies[3])])],
            Slots = Enumerable.Range(0, 4).Select(i => singles && i >= 2
                ? new SourceBattleSlot(new(i), null, 0, null)
                : new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray()
        };
        var entered = SourceFourEntryAbilities.InitializeEntry(Mechanics.Catalog, frame, frame.Revision);
        Assert.IsTrue(entered.Succeeded, entered.Fault);
        Assert.IsNull(SourceBattleIdentityRules.Validate(entered.State));
        Assert.IsFalse(entered.Events.OfType<SourceHeldItemConsumedEvent>().Any(), "The fixture item must survive initial entry.");
        return entered.State;
    }
    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor, int target)
    {
        string original = JsonSerializer.Serialize(frame);
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault);
        Assert.IsFalse(result.Events.IsDefaultOrEmpty, "A real public action must produce receipts.");
        Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.AreEqual(original, JsonSerializer.Serialize(frame), "Resolving must preserve the immutable input frame.");
        return result;
    }
    private static SourceKernelResult Find(SourceBattleFrame frame, int actor, int target,
        Func<SourceKernelResult, bool> predicate, string branch)
    {
        // Bounded receipt-branch discovery; no product-output golden or fallback pass.
        for (uint seed = 0; seed < 1024; seed++)
        {
            var result = Resolve(frame with { Rng = new(seed) }, actor, target);
            if (predicate(result)) return result;
        }
        throw new InvalidOperationException($"No actual {branch} within 1024 action-start seeds.");
    }
    private static SourceStatChangedEvent[] Direct(SourceKernelResult result, string name, int actor) =>
        result.Events.OfType<SourceStatChangedEvent>().Where(s => s.Actor.Value == actor &&
            s.Target.Value != actor && s.Stat == Spec(name).Stat && s.After == s.Before - 1).ToArray();
    private static bool FirstRollQualifies(SourceKernelResult result, string name) =>
        result.Trace.First(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal)).Raw % 100
            <= Moves[Id(name)].SecondaryEffectChance;
    private static void AssertProjection(SourceKernelResult result, string name, int actor, int target,
        params int[] affected)
    {
        string eventsBefore = JsonSerializer.Serialize(result.Events), stateBefore = JsonSerializer.Serialize(result.State),
            traceBefore = JsonSerializer.Serialize(result.Trace);
        var direct = Direct(result, name, actor);
        CollectionAssert.AreEqual(affected, direct.Select(s => s.Target.Value).ToArray());
        var attack = result.Events.OfType<SourceAttackStringEvent>().Single();
        var used = result.Events.OfType<SourceMoveUsedEvent>().Single();
        var passes = result.Events.OfType<SourcePpCommandEvent>().ToArray();
        bool singles = result.State.Layout == SourceBattleLayout.Singles;
        CollectionAssert.AreEqual(singles ? new[] { target } : new[] { target, target ^ 2 }, passes.Select(p => p.Target.Value).ToArray());
        Assert.AreEqual(used.Sequence - 1, attack.Sequence);
        Assert.AreEqual(actor, used.Actor.Value); Assert.AreEqual(target, used.Target.Value); Assert.AreEqual(Id(name), used.Move);
        Assert.AreEqual(used.PpBefore, passes[0].Before); Assert.AreEqual(used.PpAfter, passes[0].After);
        Assert.IsTrue(passes[0].Before > 0); Assert.IsTrue(passes[0].CommandCost > 0);
        Assert.AreEqual(Math.Max(0, passes[0].Before - passes[0].CommandCost), passes[0].After);
        if (!singles)
        {
            Assert.IsTrue(passes[1].CommandCost > 0, "Suppression does not erase calculated command cost.");
            Assert.AreEqual(passes[0].After, passes[1].Before);
            Assert.AreEqual(passes[1].Before, passes[1].After, "The repeated spread PP command cannot spend again.");
        }
        foreach (var pass in passes)
        {
            Assert.AreEqual(used.Actor, pass.Actor); Assert.AreEqual(used.Actor, pass.PpOwner);
            Assert.AreEqual(used.Move, pass.ChosenMove); Assert.AreEqual(used.Move, pass.CurrentMove);
            Assert.AreEqual(used.Slot, pass.PpSlot);
        }
        var expected = direct.ToDictionary(s => s.Sequence);
        int count = 0;
        for (int i = 0; i < result.Events.Length; i++)
        {
            var battle = Double(result.Events, i);
            var dispatch = AnimationDispatchFixture.StatAtResult(battle, moves: Moves);
            Assert.AreEqual(dispatch, AnimationDispatchFixture.StatAtResult(battle, moves: Moves));
            if (expected.TryGetValue(result.Events[i].Sequence, out var stat))
            {
                count++; Assert.IsNotNull(dispatch, $"Missing {name} wave for actual receipt {stat.Sequence}.");
                var owner = passes.Last(p => p.Sequence < stat.Sequence);
                Assert.AreEqual(stat.Sequence, dispatch.Sequence); Assert.AreEqual(attack.Sequence, dispatch.AttackSequence);
                Assert.AreEqual(owner.Sequence, dispatch.PpSequence); Assert.AreEqual(Spec(name).SourceId, dispatch.SourceMoveId);
                Assert.AreEqual(stat.Target.Value, dispatch.Affected.Value); Assert.AreEqual(Spec(name).Argument, dispatch.AnimationArgument);
                Assert.IsTrue(result.Events.OfType<SourceHpChangedEvent>().Any(h => h.Sequence > owner.Sequence &&
                    h.Sequence < stat.Sequence && h.Target == stat.Target && h.Move == used.Move && h.HpAfter > 0));
            }
            else Assert.IsNull(dispatch, $"{name}: unrelated receipt {result.Events[i].GetType().Name} cannot borrow a hit.");
            Assert.IsNull(AnimationDispatchFixture.StatAtResult(battle, true, Moves));
            Assert.IsFalse(CampaignStatAnimationPresentation.IsSilentResultReceipt(battle,moves:CurrentCampaignFixture.Catalog.PokemonContent!.Catalog.Moves));
            if (singles)
            {
                var single = Single(result.Events, i);
                Assert.AreEqual(dispatch, AnimationDispatchFixture.StatAtResult(single, true, moves: Moves));
                Assert.IsNull(AnimationDispatchFixture.StatAtResult(single, false, moves: Moves));
                Assert.IsNull(AnimationDispatchFixture.StatAtResult(single, true, true, Moves));
            }
        }
        Assert.AreEqual(affected.Length, count);
        Assert.AreEqual(eventsBefore, JsonSerializer.Serialize(result.Events));
        Assert.AreEqual(stateBefore, JsonSerializer.Serialize(result.State));
        Assert.AreEqual(traceBefore, JsonSerializer.Serialize(result.Trace));
    }

    [TestMethod]
    [DataRow("ACID")][DataRow("BUBBLE")][DataRow("ICY_WIND")][DataRow("MUDDY_WATER")]
    public void NaturalBothSuccessesOwnSeparateWavesAtEveryActorAndTargetOrder(string name)
    {
        var definition = Moves[Id(name)];
        Assert.AreEqual(Spec(name).SourceId, definition.SourceMoveId);
        Assert.AreEqual("MOVE_TARGET_BOTH", definition.Target);
        Assert.IsFalse(definition.Flags.Contains("FLAG_MAKES_CONTACT"));
        for (int actor = 0; actor < 4; actor++) foreach (int target in new[] { actor ^ 1, actor ^ 3 })
        {
            var result = Find(Frame(Bodies(name, actor)), actor, target,
                r => Direct(r, name, actor).Length == 2, name + "/both source secondaries");
            AssertProjection(result, name, actor, target, target, target ^ 2);
            Assert.AreEqual(2, result.Trace.Count(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal)));
        }
    }

    [TestMethod]
    [DataRow("ACID")][DataRow("BUBBLE")][DataRow("ICY_WIND")][DataRow("MUDDY_WATER")]
    public void FirstMissProtectImmunityAbilityCapSubstituteOrKoDoesNotCancelThePartnerWave(string name)
    {
        foreach (string branch in new[] { "miss", "protect", "immune", "clear-body", "cap", "substitute-survives", "substitute-breaks", "ko" })
        {
            var bodies = Bodies(name);
            if (branch == "miss")
            {
                bodies[0] = bodies[0] with { AccuracyStage = 6 };
                bodies[1] = bodies[1] with { EvasionStage = 12 };
            }
            else if (branch == "protect") bodies[1] = bodies[1] with { Protected = true };
            else if (branch == "immune") bodies[1] = NaturalMon("SHEDINJA", 100, "HARDEN");
            else if (branch == "clear-body")
            {
                bodies[1] = NaturalMon("TENTACOOL", 100, "BARRIER");
                Assert.AreEqual("ABILITY_CLEAR_BODY", bodies[1].Pokemon.AbilityId);
            }
            else if (branch == "cap") bodies[1] = Spec(name).Stat switch
            {
                WildBattleStat.Defense => bodies[1] with { DefenseStage = 0 },
                WildBattleStat.Speed => bodies[1] with { SpeedStage = 0 },
                WildBattleStat.Accuracy => bodies[1] with { AccuracyStage = 0 },
                _ => throw new InvalidOperationException()
            };
            else if (branch.StartsWith("substitute", StringComparison.Ordinal))
                bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = branch == "substitute-breaks" ? 1 : 100 } };
            else bodies[1] = bodies[1] with { Pokemon = bodies[1].Pokemon with { CurrentHp = 1 } };
            var result = Find(Frame(bodies), 0, 1, r => Direct(r, name, 0) is [{ Target.Value: 3 }] &&
                (branch == "miss" ? r.Events.OfType<SourceMoveMissedEvent>().Any(m => m.Target.Value == 1) :
                 branch == "protect" ? r.Events.OfType<SourceMoveFailedEvent>().Any(m => m.Target.Value == 1 && m.Reason == "protected") :
                 FirstRollQualifies(r, name)),
                name + "/first " + branch + "/second eligible");
            AssertProjection(result, name, 0, 1, 3);
            if (branch == "immune") Assert.IsTrue(result.Events.OfType<SourceAbilityPreventedEvent>()
                .Any(e => e.Target.Value == 1 && e.Ability == "ABILITY_WONDER_GUARD"));
            if (branch == "clear-body")
            {
                Assert.IsTrue(result.Events.OfType<SourceHpChangedEvent>().Any(h => h.Target.Value == 1 &&
                    h.Move == Id(name) && h.Damage > 0 && h.HpAfter > 0 && h.HpAfter < h.HpBefore));
                Assert.IsFalse(result.Events.OfType<SourceStatChangedEvent>().Any(s => s.Target.Value == 1));
                Assert.IsFalse(result.Events.OfType<SourceAbilityPreventedEvent>().Any(e => e.Target.Value == 1));
                Assert.IsFalse(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Target.Value == 1));
            }
            if (branch == "cap")
            {
                Assert.IsFalse(result.Events.OfType<SourceStatAtMinimumEvent>().Any());
                Assert.IsFalse(result.Events.OfType<SourceStatChangedEvent>().Any(s => s.Target.Value == 1));
            }
            if (branch.StartsWith("substitute", StringComparison.Ordinal))
            {
                var hit = result.Events.OfType<SourceSubstituteDamagedEvent>().Single();
                Assert.AreEqual(1, hit.Target.Value); Assert.AreEqual(branch == "substitute-breaks", hit.HpAfter == 0);
            }
            if (branch == "ko") Assert.IsTrue(result.Events.OfType<SourceFaintedEvent>().Any(e => e.Side.Value == 1));
        }
    }

    [TestMethod]
    [DataRow("ACID")][DataRow("BUBBLE")][DataRow("ICY_WIND")][DataRow("MUDDY_WATER")]
    public void NaturalSpreadDefinitionsUseOneOrdinaryPassInActualSingles(string name)
    {
        for (int actor = 0; actor < 2; actor++)
        {
            int target = actor ^ 1;
            var result = Find(Frame(Bodies(name, actor), singles: true), actor, target,
                r => Direct(r, name, actor).Length == 1, name + "/actual singles");
            AssertProjection(result, name, actor, target, target);
        }
    }

    [TestMethod]
    [DataRow("ACID")][DataRow("BUBBLE")][DataRow("ICY_WIND")][DataRow("MUDDY_WATER")]
    public void FirstRageAndWhiteHerbFinishBeforeTheSecondPassAndKeepTheirOwnResults(string name)
    {
        var bodies = Bodies(name);
        bodies[1] = bodies[1] with { Rage = true, HeldItem = "ITEM_WHITE_HERB" };
        var result = Find(Frame(bodies), 0, 1, r => Direct(r, name, 0).Length == 2 &&
            r.Events.OfType<SourceHeldItemConsumedEvent>().Any(i => i.Item == "ITEM_WHITE_HERB"), name + "/inter-pass White Herb");
        var direct = Direct(result, name, 0);
        var rage = result.Events.OfType<SourceStatChangedEvent>().Single(s => s.Actor.Value == 1 && s.Target.Value == 1 && s.Stat == WildBattleStat.Attack);
        var restored = result.Events.OfType<SourceStatChangedEvent>().Single(s => s.Actor.Value == 1 && s.Target.Value == 1 && s.Stat == Spec(name).Stat);
        var consumed = result.Events.OfType<SourceHeldItemConsumedEvent>().Single();
        var second = result.Events.OfType<SourcePpCommandEvent>().Last();
        Assert.AreEqual(6, rage.Before); Assert.AreEqual(7, rage.After);
        Assert.AreEqual(5, restored.Before); Assert.AreEqual(6, restored.After);
        Assert.IsTrue(direct[0].Sequence < rage.Sequence && rage.Sequence < restored.Sequence && restored.Sequence < consumed.Sequence &&
            consumed.Sequence < second.Sequence && second.Sequence < direct[1].Sequence);
        Assert.IsNotNull(consumed.StatChanges); Assert.AreEqual(1, consumed.StatChanges.Value.Length);
        Assert.AreEqual(new SourceItemStatChange(Spec(name).Stat, 5, 6), consumed.StatChanges.Value[0]);
        AssertProjection(result, name, 0, 1, 1, 3);
        var held = CampaignHeldItemAnimationPresentation.AtResult(Double(result.Events, result.Events.IndexOf(restored)),
            SourceBattleScene.On, new MoveId?[4]);
        Assert.IsNotNull(held); Assert.AreEqual(restored.Sequence, held.Sequence); Assert.AreEqual(consumed.Sequence, held.ReceiptSequence);
    }

    [TestMethod]
    [DataRow("ACID")][DataRow("BUBBLE")][DataRow("ICY_WIND")][DataRow("MUDDY_WATER")]
    public void BothPressureHoldersChargeOnceAndLastPpStillAllowsThePartnerSecondary(string name)
    {
        foreach (int pp in new[] { 1, 4 })
        {
            var bodies = Bodies(name);
            bodies[0] = bodies[0] with { Pokemon = bodies[0].Pokemon with
                { Moves = bodies[0].Pokemon.Moves.SetItem(0, bodies[0].Pokemon.Moves[0] with { CurrentPp = pp }) } };
            bodies[1] = NaturalMon("DUSCLOPS", 100, "LEER");
            bodies[3] = NaturalMon("DUSCLOPS", 100, "LEER");
            Assert.AreEqual("ABILITY_PRESSURE", bodies[1].Pokemon.AbilityId);
            Assert.AreEqual("ABILITY_PRESSURE", bodies[3].Pokemon.AbilityId);
            var result = Find(Frame(bodies), 0, 1, r => Direct(r, name, 0).Length == 2, name + "/Pressure with " + pp + " PP");
            var commands = result.Events.OfType<SourcePpCommandEvent>().ToArray();
            CollectionAssert.AreEqual(new[] { 3, 3 }, commands.Select(p => p.CommandCost).ToArray());
            Assert.AreEqual(pp, commands[0].Before); Assert.AreEqual(Math.Max(0, pp - 3), commands[0].After);
            Assert.AreEqual(commands[0].After, commands[1].Before); Assert.AreEqual(commands[1].Before, commands[1].After);
            Assert.AreEqual(Math.Max(0, pp - 3), result.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp);
            AssertProjection(result, name, 0, 1, 1, 3);
        }
    }
}

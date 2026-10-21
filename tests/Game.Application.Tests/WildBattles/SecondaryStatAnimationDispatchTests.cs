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
public sealed class SecondaryStatAnimationDispatchTests
{
    private static SourceMechanicsCatalog Mechanics =>
        TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static ImmutableDictionary<MoveId, PokemonMoveDefinition> Moves => Mechanics.Catalog.Moves;
    private static MoveId Id(string name) => new("pret:MOVE_" + name);
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("secondary-stat", new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("secondary-stat", null!, new(), events,
            index, null!, null!, CampaignBattlePhase.Events, [], 0, []);

    private static ImmutableArray<SourceBattleEvent> Invocation(PokemonMoveDefinition move, int actor, int target,
        params SourceBattleEvent[] results)
    {
        int pp = Math.Min(10, move.Pp);
        return [new SourceAttackStringEvent(1, 1, new(actor), move.Id, move.Id),
            new SourceMoveUsedEvent(2, 1, new(actor), new(target), 0, move.Id, pp, pp - 1),
            new SourcePpCommandEvent(3, 1, new(actor), new(target), 0, move.Id, move.Id, pp, pp - 1, 1, 0),
            .. results];
    }
    private static ImmutableArray<SourceBattleEvent> Changed(PokemonMoveDefinition move, WildBattleStat stat,
        bool self, int actor = 0, int target = 1, int before = 6) => Invocation(move, actor, target,
            new SourceHpChangedEvent(4, 1, new(actor), new(target), move.Id, 100, 90, 10, false, 100),
            new SourceStatChangedEvent(5, 1, new(actor), new(self ? actor : target), stat, before, before + (self ? 1 : -1)));

    private static void AssertOne(ImmutableArray<SourceBattleEvent> events, long sequence, int sourceMove,
        int affected, int argument, IReadOnlyDictionary<MoveId, PokemonMoveDefinition>? catalog = null,
        bool singles = true)
    {
        catalog ??= Moves;
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Last(p => p.Sequence < sequence);
        string before = JsonSerializer.Serialize(events);
        int count = 0;
        for (int index = 0; index < events.Length; index++)
        {
            // An empty committed Frame prevents inference from the final occupant or its stages.
            var battle = Double(events, index);
            var dispatch = AnimationDispatchFixture.StatAtResult(battle, moves: catalog);
            if (events[index].Sequence == sequence)
            {
                Assert.IsNotNull(dispatch, $"Missing wave for move {sourceMove}, receipt {sequence}.");
                count++;
                Assert.AreEqual("secondary-stat", dispatch.BattleId);
                Assert.AreEqual(sequence, dispatch.Sequence);
                Assert.AreEqual(attack.Sequence, dispatch.AttackSequence);
                Assert.AreEqual(pp.Sequence, dispatch.PpSequence);
                Assert.AreEqual(sourceMove, dispatch.SourceMoveId);
                Assert.AreEqual(affected, dispatch.Affected.Value);
                Assert.AreEqual(argument, dispatch.AnimationArgument);
                Assert.AreEqual(dispatch, AnimationDispatchFixture.StatAtResult(battle, moves: catalog),
                    "The pure projection is repeatable; continuation deduplication belongs to the view.");
            }
            else Assert.IsNull(dispatch, $"Unrelated receipt {index} cannot replay the secondary wave.");
            Assert.IsNull(AnimationDispatchFixture.StatAtResult(battle, true, catalog));
            Assert.IsFalse(CampaignStatAnimationPresentation.IsSilentResultReceipt(battle,moves:CurrentCampaignFixture.Catalog.PokemonContent!.Catalog.Moves));
            if (!singles) continue;
            var single = Single(events, index);
            Assert.AreEqual(dispatch, AnimationDispatchFixture.StatAtResult(single, true, moves: catalog));
            Assert.IsNull(AnimationDispatchFixture.StatAtResult(single, false, moves: catalog));
            Assert.IsNull(AnimationDispatchFixture.StatAtResult(single, true, true, catalog));
        }
        Assert.AreEqual(1, count);
        Assert.AreEqual(before, JsonSerializer.Serialize(events));
        Assert.AreEqual(used.Move, pp.CurrentMove);
    }

    private static void AssertNone(ImmutableArray<SourceBattleEvent> events, string reason,
        IReadOnlyDictionary<MoveId, PokemonMoveDefinition>? catalog = null, bool singles = true)
    {
        catalog ??= Moves;
        for (int index = 0; index < events.Length; index++)
        {
            Assert.IsNull(AnimationDispatchFixture.StatAtResult(Double(events, index), moves: catalog), reason);
            if (singles) Assert.IsNull(AnimationDispatchFixture.StatAtResult(Single(events, index), true, moves: catalog), reason);
        }
    }

    [TestMethod]
    [DataRow("AURORA_BEAM", 62, "EFFECT_ATTACK_DOWN_HIT", WildBattleStat.Attack, false, 22)]
    [DataRow("CRUSH_CLAW", 306, "EFFECT_DEFENSE_DOWN_HIT", WildBattleStat.Defense, false, 23)]
    [DataRow("BUBBLE_BEAM", 61, "EFFECT_SPEED_DOWN_HIT", WildBattleStat.Speed, false, 24)]
    [DataRow("MIST_BALL", 296, "EFFECT_SPECIAL_ATTACK_DOWN_HIT", WildBattleStat.SpecialAttack, false, 25)]
    [DataRow("PSYCHIC", 94, "EFFECT_SPECIAL_DEFENSE_DOWN_HIT", WildBattleStat.SpecialDefense, false, 26)]
    [DataRow("MUD_SLAP", 189, "EFFECT_ACCURACY_DOWN_HIT", WildBattleStat.Accuracy, false, 27)]
    [DataRow("METAL_CLAW", 232, "EFFECT_ATTACK_UP_HIT", WildBattleStat.Attack, true, 15)]
    [DataRow("STEEL_WING", 211, "EFFECT_DEFENSE_UP_HIT", WildBattleStat.Defense, true, 16)]
    public void ExactSourceMetadataMapsEightEffectsAtEveryPositionAndSelectedAllies(
        string name, int sourceMove, string effect, WildBattleStat stat, bool self, int argument)
    {
        var move = Moves[Id(name)];
        Assert.AreEqual(sourceMove, move.SourceMoveId);
        Assert.AreEqual(effect, move.Effect);
        Assert.AreEqual("MOVE_TARGET_SELECTED", move.Target);
        Assert.IsTrue(SourceMoveDefinitionRules.IsValid(move));
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        foreach (int before in self ? new[] { 6, 11 } : new[] { 6, 1 })
            AssertOne(Changed(move, stat, self, actor, target, before), 5, sourceMove,
                self ? actor : target, argument, singles: actor < 2 && target < 2);
    }

    [TestMethod]
    public void StructuralMetadataRatherThanANumericMoveAllowlistChoosesTheSecondary()
    {
        var entries = new[] {
            ("EFFECT_ATTACK_DOWN_HIT", WildBattleStat.Attack, false, 22),
            ("EFFECT_DEFENSE_DOWN_HIT", WildBattleStat.Defense, false, 23),
            ("EFFECT_SPEED_DOWN_HIT", WildBattleStat.Speed, false, 24),
            ("EFFECT_SPECIAL_ATTACK_DOWN_HIT", WildBattleStat.SpecialAttack, false, 25),
            ("EFFECT_SPECIAL_DEFENSE_DOWN_HIT", WildBattleStat.SpecialDefense, false, 26),
            ("EFFECT_ACCURACY_DOWN_HIT", WildBattleStat.Accuracy, false, 27),
            ("EFFECT_ATTACK_UP_HIT", WildBattleStat.Attack, true, 15),
            ("EFFECT_DEFENSE_UP_HIT", WildBattleStat.Defense, true, 16)
        };
        foreach (var (effect, stat, self, argument) in entries)
        {
            // A typed presentation fixture, not a replacement source gameplay definition.
            var move = Moves[Id("TACKLE")] with { Id = Id("TEST_SECONDARY"), Effect = effect, SecondaryEffectChance = 30 };
            Assert.IsTrue(SourceMoveDefinitionRules.IsValid(move));
            var catalog = ImmutableDictionary<MoveId, PokemonMoveDefinition>.Empty.Add(move.Id, move);
            AssertOne(Changed(move, stat, self), 5, 33, self ? 0 : 1, argument, catalog);
        }
    }

    [TestMethod]
    public void MissingMalformedOrUnsupportedMetadataFailsClosed()
    {
        var move = Moves[Id("METAL_CLAW")];
        var events = Changed(move, WildBattleStat.Attack, true);
        for (int index = 0; index < events.Length; index++)
        {
            Assert.IsNull(AnimationDispatchFixture.StatAtResult(Double(events, index), moves: null));
            Assert.IsNull(AnimationDispatchFixture.StatAtResult(Single(events, index), true, moves: null));
        }
        AssertNone(events, "An absent dictionary entry cannot authorize a secondary.",
            ImmutableDictionary<MoveId, PokemonMoveDefinition>.Empty);
        foreach (var bad in new[] {
            move with { Id = Id("TACKLE") }, move with { SourceMoveId = 0 }, move with { Power = 0 },
            move with { SecondaryEffectChance = 0 }, move with { SecondaryEffectChance = 101 },
            move with { Target = "MOVE_TARGET_BOTH" }, move with { Target = "MOVE_TARGET_USER" },
            move with { Effect = "EFFECT_HIT" }, move with { Effect = "EFFECT_ALL_STATS_UP_HIT" },
            move with { Effect = "EFFECT_SUPERPOWER" }, move with { Effect = "EFFECT_OVERHEAT" },
            move with { BattleEffectImplemented = true }, move with { Flags = default },
            move with { SourceMoveId = 355 }, move with { Pp = 0 }, move with { Pp = 41 },
            move with { Power = 256 }, move with { Accuracy = 101 }, move with { Priority = 8 },
            move with { Type = "TYPE_" }, move with { Flags = ["INVALID_FLAG"] }
        }) AssertNone(events, $"Invalid or unapproved definition: {bad}.", Moves.SetItem(move.Id, bad));
    }

    private static WildBattleCombatant NaturalMon(string species, int level, string move)
    {
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        var id = Id(move);
        Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == id && m.Level <= level),
            $"The source natural learnset must contain {species}/{level}/{move}.");
        int pp = Moves[id].Pp;
        return new(pokemon with { Moves = [new(id, pp, pp)] });
    }
    private static WildBattleCombatant[] Bodies(string move, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4)
            .Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL") with { SpeedStage = move == "BUBBLE_BEAM" ? 6 : 0 }).ToArray();
        bodies[actor] = move switch {
            "AURORA_BEAM" => NaturalMon("SEEL", 21, move),
            "BUBBLE_BEAM" => NaturalMon("TENTACOOL", 25, move),
            "MIST_BALL" => NaturalMon("LATIAS", 35, move),
            "MUD_SLAP" => NaturalMon("BARBOACH", 1, move),
            "METAL_CLAW" => NaturalMon("CHARMANDER", 13, move),
            "STEEL_WING" => NaturalMon("SKARMORY", 32, move),
            "CRUSH_CLAW" => NaturalMon("ZANGOOSE", 31, move),
            "PSYCHIC" => NaturalMon("KADABRA", 36, move),
            _ => throw new ArgumentOutOfRangeException(nameof(move))
        };
        bodies[actor] = bodies[actor] with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }
    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies)
    {
        static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
            new(body.Pokemon, body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
        var frame = new SourceBattleFrame {
            Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version, BattleKind = WildBattleCalledBattleKind.Trainer,
            Rng = new(31), Parties = [new(BattleSide.Player, [Member(bodies[0]), Member(bodies[2])]),
                new(BattleSide.Opponent, [Member(bodies[1]), Member(bodies[3])])],
            Slots = Enumerable.Range(0, 4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray()
        };
        var entry = SourceFourEntryAbilities.InitializeEntry(Mechanics.Catalog, frame, frame.Revision);
        Assert.IsTrue(entry.Succeeded, entry.Fault);
        Assert.IsNull(SourceBattleIdentityRules.Validate(entry.State));
        Assert.IsFalse(entry.Events.OfType<SourceHeldItemConsumedEvent>().Any(), "The test item must remain for this move's own suffix.");
        return entry.State;
    }
    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor = 0, int target = 1)
    {
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault);
        Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        return result;
    }
    private static SourceKernelResult Find(SourceBattleFrame frame, Func<SourceKernelResult, bool> predicate,
        string branch, int actor = 0, int target = 1)
    {
        // A bounded, deterministic receipt-branch search, not a source golden or a random campaign scan.
        for (uint seed = 0; seed < 128; seed++)
        {
            var result = Resolve(frame with { Rng = new(seed) }, actor, target);
            if (predicate(result)) return result;
        }
        throw new InvalidOperationException($"No real {branch} within 128 action-start seeds.");
    }
    private static bool Qualifying(SourceKernelResult result, string name) =>
        result.Trace.Single(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal)).Raw % 100
            <= Moves[Id(name)].SecondaryEffectChance;
    private static SourceStatChangedEvent Direct(ImmutableArray<SourceBattleEvent> events, int actor, int affected,
        WildBattleStat stat) => events.OfType<SourceStatChangedEvent>()
            .Single(s => s.Actor.Value == actor && s.Target.Value == affected && s.Stat == stat);
    private static void AssertNatural(SourceKernelResult result, string name, WildBattleStat stat, bool self,
        int argument, int actor = 0, int target = 1)
    {
        var changed = Direct(result.Events, actor, self ? actor : target, stat);
        var hit = result.Events.Single(e => e is SourceHpChangedEvent or SourceSubstituteDamagedEvent);
        Assert.IsTrue(hit.Sequence < changed.Sequence);
        Assert.AreEqual(self ? 1 : -1, changed.After - changed.Before);
        string state = JsonSerializer.Serialize(result.State), trace = JsonSerializer.Serialize(result.Trace);
        AssertOne(result.Events, changed.Sequence, Moves[Id(name)].SourceMoveId, self ? actor : target,
            argument, singles: actor < 2 && target < 2);
        Assert.AreEqual(state, JsonSerializer.Serialize(result.State));
        Assert.AreEqual(trace, JsonSerializer.Serialize(result.Trace));
    }

    [TestMethod]
    [DataRow("AURORA_BEAM", WildBattleStat.Attack, false, 22)]
    [DataRow("CRUSH_CLAW", WildBattleStat.Defense, false, 23)]
    [DataRow("BUBBLE_BEAM", WildBattleStat.Speed, false, 24)]
    [DataRow("MIST_BALL", WildBattleStat.SpecialAttack, false, 25)]
    [DataRow("PSYCHIC", WildBattleStat.SpecialDefense, false, 26)]
    [DataRow("MUD_SLAP", WildBattleStat.Accuracy, false, 27)]
    [DataRow("METAL_CLAW", WildBattleStat.Attack, true, 15)]
    [DataRow("STEEL_WING", WildBattleStat.Defense, true, 16)]
    public void PublicKernelNaturalMovesOwnOneStatWaveAtAllPositionsAndSelectedAllies(
        string name, WildBattleStat stat, bool self, int argument)
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        {
            int affected = self ? actor : target;
            var result = Find(Frame(Bodies(name, actor)),
                r => r.Events.OfType<SourceStatChangedEvent>().Any(s => s.Actor.Value == actor && s.Target.Value == affected && s.Stat == stat),
                $"{name} secondary at {actor}->{target}", actor, target);
            AssertNatural(result, name, stat, self, argument, actor, target);
        }
    }

    [TestMethod]
    [DataRow("METAL_CLAW", WildBattleStat.Attack, 15)]
    [DataRow("STEEL_WING", WildBattleStat.Defense, 16)]
    public void ActorIncreaseSurvivesTargetKoAndBothSubstituteOutcomes(string name, WildBattleStat stat, int argument)
    {
        foreach (string branch in new[] { "ko", "substitute-survives", "substitute-breaks" })
        {
            var bodies = Bodies(name);
            bodies[1] = branch == "ko" ? bodies[1] with { Pokemon = bodies[1].Pokemon with { CurrentHp = 1 } }
                : bodies[1] with { CalledVolatile = new() { SubstituteHp = branch == "substitute-breaks" ? 1 : 100 } };
            var result = Find(Frame(bodies), r => r.Events.OfType<SourceStatChangedEvent>().Any(s => s.Target.Value == 0 && s.Stat == stat), name + "/" + branch);
            if (branch == "ko")
            {
                Assert.AreEqual(0, result.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);
                Assert.IsTrue(result.Events.OfType<SourceFaintedEvent>().Any(e => e.Side.Value == 1));
            }
            else
            {
                var hit = result.Events.OfType<SourceSubstituteDamagedEvent>().Single();
                Assert.AreEqual(branch == "substitute-breaks", hit.HpAfter == 0);
                Assert.AreEqual(0, result.Events.OfType<SourceHpChangedEvent>().Count());
            }
            AssertNatural(result, name, stat, true, argument);
        }
    }

    [TestMethod]
    [DataRow("CRUSH_CLAW", WildBattleStat.Defense)]
    [DataRow("PSYCHIC", WildBattleStat.SpecialDefense)]
    public void TargetReductionIsAbsentAfterEitherSubstituteHitOrTargetKo(string name, WildBattleStat stat)
    {
        foreach (string branch in new[] { "ko", "substitute-survives", "substitute-breaks" })
        {
            var bodies = Bodies(name);
            bodies[1] = branch == "ko" ? bodies[1] with { Pokemon = bodies[1].Pokemon with { CurrentHp = 1 } }
                : bodies[1] with { CalledVolatile = new() { SubstituteHp = branch == "substitute-breaks" ? 1 : 100 } };
            var result = Find(Frame(bodies), r => Qualifying(r, name), name + "/qualifying-" + branch);
            Assert.AreEqual(0, result.Events.OfType<SourceStatChangedEvent>().Count());
            AssertNone(result.Events, "The source suppresses target secondaries on substitute hits and fainted targets.");
            // Even an attached plausible stat receipt must not turn a breaking substitute into a Pokemon hit.
            var forged = result.Events.Add(new SourceStatChangedEvent(result.Events[^1].Sequence + 1, result.Events[0].Turn,
                new(0), new(1), stat, 6, 5));
            AssertNone(forged, "A forged target stat cannot bypass the committed hit's substitute/KO identity.");
        }
    }

    [TestMethod]
    [DataRow("METAL_CLAW", WildBattleStat.Attack, true, 15)]
    [DataRow("STEEL_WING", WildBattleStat.Defense, true, 16)]
    [DataRow("CRUSH_CLAW", WildBattleStat.Defense, false, 23)]
    [DataRow("PSYCHIC", WildBattleStat.SpecialDefense, false, 26)]
    public void EndureAtOneHpStillOwnsTheRealZeroLossSecondary(string name, WildBattleStat stat, bool self, int argument)
    {
        var bodies = Bodies(name);
        bodies[1] = bodies[1] with { Enduring = true, Pokemon = bodies[1].Pokemon with { CurrentHp = 1 } };
        var result = Find(Frame(bodies), r => r.Events.OfType<SourceStatChangedEvent>().Any(), name + "/Endure secondary");
        var hp = result.Events.OfType<SourceHpChangedEvent>().Single();
        Assert.AreEqual(1, hp.HpBefore); Assert.AreEqual(1, hp.HpAfter); Assert.AreEqual(0, hp.Damage);
        Assert.IsTrue(result.Events.OfType<SourceDamageAdjustedEvent>().Single().Endured);
        AssertNatural(result, name, stat, self, argument);
    }

    [TestMethod]
    [DataRow("METAL_CLAW", WildBattleStat.Attack, true)]
    [DataRow("STEEL_WING", WildBattleStat.Defense, true)]
    [DataRow("CRUSH_CLAW", WildBattleStat.Defense, false)]
    [DataRow("PSYCHIC", WildBattleStat.SpecialDefense, false)]
    public void ActualMissCapAndFailedSecondaryRollDoNotCreateAStatWave(string name, WildBattleStat stat, bool self)
    {
        var missed = Bodies(name); missed[0] = missed[0] with { AccuracyStage = 0 }; missed[1] = missed[1] with { EvasionStage = 12 };
        var miss = Find(Frame(missed), r => r.Events.OfType<SourceMoveMissedEvent>().Any(), name + "/miss");
        AssertNone(miss.Events, "A miss has no committed hit or stat.");
        var capped = Bodies(name); int recipient = self ? 0 : 1;
        capped[recipient] = stat switch {
            WildBattleStat.Attack => capped[recipient] with { AttackStage = 12 },
            WildBattleStat.Defense => capped[recipient] with { DefenseStage = self ? 12 : 0 },
            WildBattleStat.SpecialDefense => capped[recipient] with { SpecialDefenseStageDelta = -6 },
            _ => throw new ArgumentOutOfRangeException(nameof(stat))
        };
        var cap = Find(Frame(capped), r => Qualifying(r, name), name + "/qualifying capped roll");
        Assert.AreEqual(0, cap.Events.OfType<SourceStatChangedEvent>().Count());
        Assert.AreEqual(0, cap.Events.OfType<SourceStatAtMaximumEvent>().Count());
        Assert.AreEqual(0, cap.Events.OfType<SourceStatAtMinimumEvent>().Count());
        AssertNone(cap.Events, "A secondary cap is silent and has no stat animation.");
        var failed = Find(Frame(Bodies(name)), r => !Qualifying(r, name), name + "/failed secondary roll");
        Assert.AreEqual(0, failed.Events.OfType<SourceStatChangedEvent>().Count());
        AssertNone(failed.Events, "The damage receipt does not imply that the secondary roll succeeded.");
    }

    [TestMethod]
    [DataRow("TENTACOOL", "BARRIER", "ABILITY_CLEAR_BODY")]
    [DataRow("TORKOAL", "IRON_DEFENSE", "ABILITY_WHITE_SMOKE")]
    [DataRow("CATERPIE", "STRING_SHOT", "ABILITY_SHIELD_DUST")]
    public void ActualSilentSecondaryPreventionCannotBeMistakenForAStatChange(string species, string move, string ability)
    {
        var bodies = Bodies("CRUSH_CLAW"); bodies[1] = NaturalMon(species, 100, move);
        Assert.AreEqual(ability, bodies[1].Pokemon.AbilityId);
        var blocked = Find(Frame(bodies), r => Qualifying(r, "CRUSH_CLAW"), "qualifying " + ability);
        Assert.AreEqual(0, blocked.Events.OfType<SourceStatChangedEvent>().Count());
        Assert.AreEqual(0, blocked.Events.OfType<SourceAbilityPreventedEvent>().Count());
        AssertNone(blocked.Events, "Silent ability prevention cannot produce General_StatsChange.");
        var frame = Frame(Bodies("CRUSH_CLAW"));
        frame = frame with { Sides = frame.Sides.SetItem(1, frame.Sides[1] with { MistTurnsRemaining = 5 }) };
        var mist = Find(frame, r => Qualifying(r, "CRUSH_CLAW"), "qualifying Mist prevention");
        Assert.AreEqual(0, mist.Events.OfType<SourceStatChangedEvent>().Count());
        Assert.AreEqual(0, mist.Events.OfType<SourceMoveFailedEvent>().Count());
        AssertNone(mist.Events, "Silent Mist prevention cannot produce General_StatsChange.");
    }

    private static SourceFourActionResult Ordered(SourceBattleFrame frame, int actor, int target)
    {
        var selected = frame.Slots[actor].Body!.Pokemon.Moves[0].Move;
        var choices = frame.Slots.Select(c => new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move,
            0, new(c.Slot.Value == actor ? target : c.Slot.Value))).ToImmutableArray();
        var result = SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision, choices);
        for (int step = 0; step < 12; step++)
        {
            Assert.IsTrue(result.Succeeded, result.Fault);
            Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            if (result.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == actor && e.Move == selected)) return result;
            Assert.IsNotNull(result.Continuation);
            result = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
        }
        throw new InvalidOperationException("The selected natural move did not execute in one public turn.");
    }

    [TestMethod]
    [DataRow("CRUSH_CLAW", WildBattleStat.Defense, 23)]
    [DataRow("PSYCHIC", WildBattleStat.SpecialDefense, 26)]
    public void ActualWhiteHerbSuffixHasItsOwnAnimationAndCannotReplayTheMoveStat(string name, WildBattleStat stat, int argument)
    {
        var bodies = Bodies(name);
        // White Herb must not restore this fixture's low initiative stage on entry.
        bodies[1] = bodies[1] with { SpeedStage = 6, HeldItem = "ITEM_WHITE_HERB" };
        var frame = Frame(bodies);
        ImmutableArray<SourceBattleEvent> events = [];
        for (uint seed = 0; seed < 128; seed++)
        {
            var candidate = Ordered(frame with { Rng = new(seed) }, 0, 1);
            if (!candidate.Events.OfType<SourceHeldItemConsumedEvent>().Any(e => e.Item == "ITEM_WHITE_HERB")) continue;
            events = candidate.Events; break;
        }
        Assert.IsFalse(events.IsDefaultOrEmpty, "A bounded real successful secondary must consume White Herb.");
        var direct = Direct(events, 0, 1, stat);
        var restored = Direct(events, 1, 1, stat);
        var item = events.OfType<SourceHeldItemConsumedEvent>().Single();
        Assert.AreEqual("ITEM_WHITE_HERB", item.Item);
        Assert.AreEqual(5, restored.Before); Assert.AreEqual(6, restored.After);
        Assert.IsNotNull(item.StatChanges); Assert.AreEqual(1, item.StatChanges.Value.Length);
        Assert.AreEqual(new SourceItemStatChange(stat, 5, 6), item.StatChanges.Value[0]);
        Assert.IsTrue(direct.Sequence < restored.Sequence && restored.Sequence < item.Sequence);
        AssertOne(events, direct.Sequence, Moves[Id(name)].SourceMoveId, 1, argument);
        var held = CampaignHeldItemAnimationPresentation.AtResult(Double(events, events.IndexOf(restored)),
            SourceBattleScene.On, new MoveId?[4]);
        Assert.IsNotNull(held);
        Assert.AreEqual(restored.Sequence, held.Sequence); Assert.AreEqual(item.Sequence, held.ReceiptSequence);
    }

    [TestMethod]
    public void ActualEndTurnLiechiIncreaseCannotBorrowAnEarlierMetalClawHit()
    {
        var bodies = Bodies("METAL_CLAW");
        bodies[0] = bodies[0] with { HeldItem = "ITEM_LIECHI_BERRY",
            Pokemon = bodies[0].Pokemon with { CurrentHp = bodies[0].Pokemon.Stats.MaxHp / 4 } };
        var entered = Frame(bodies);
        ImmutableArray<SourceBattleEvent> events = [];
        for (uint seed = 0; seed < 128; seed++)
        {
            var frame = entered with { Rng = new(seed) };
            var choices = frame.Slots.Select(c => new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move,
                0, new(c.Slot.Value == 0 ? 1 : c.Slot.Value))).ToImmutableArray();
            var result = SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision, choices);
            var all = ImmutableArray.CreateBuilder<SourceBattleEvent>();
            for (int step = 0; step < 16; step++)
            {
                Assert.IsTrue(result.Succeeded, result.Fault);
                Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
                all.AddRange(result.Events);
                if (result.CompletedCleanTurn) break;
                Assert.IsNotNull(result.Continuation);
                result = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
            }
            Assert.IsTrue(result.CompletedCleanTurn, "The real berry fixture must complete its bounded public turn.");
            if (all.OfType<SourceStatChangedEvent>().Count(e => e.Actor.Value == 0 && e.Target.Value == 0 && e.Stat == WildBattleStat.Attack) != 2) continue;
            events = all.ToImmutable(); break;
        }
        Assert.IsFalse(events.IsDefaultOrEmpty, "Require both the actual Metal Claw increase and actual Liechi increase.");
        var increases = events.OfType<SourceStatChangedEvent>()
            .Where(e => e.Actor.Value == 0 && e.Target.Value == 0 && e.Stat == WildBattleStat.Attack).ToArray();
        Assert.AreEqual(6, increases[0].Before); Assert.AreEqual(7, increases[0].After);
        Assert.AreEqual(7, increases[1].Before); Assert.AreEqual(8, increases[1].After);
        var item = events.OfType<SourceHeldItemConsumedEvent>().Single(e => e.Item == "ITEM_LIECHI_BERRY");
        Assert.IsNotNull(item.StatChanges);
        Assert.AreEqual(new SourceItemStatChange(WildBattleStat.Attack, 7, 8), item.StatChanges.Value.Single());
        var move = AnimationDispatchFixture.StatAtResult(Double(events, events.IndexOf(increases[0])), moves: Moves);
        Assert.IsNotNull(move); Assert.AreEqual(232, move.SourceMoveId); Assert.AreEqual(15, move.AnimationArgument);
        Assert.IsNull(AnimationDispatchFixture.StatAtResult(Double(events, events.IndexOf(increases[1])), moves: Moves));
        var held = CampaignHeldItemAnimationPresentation.AtResult(Double(events, events.IndexOf(increases[1])),
            SourceBattleScene.On, new MoveId?[4]);
        Assert.IsNotNull(held); Assert.AreEqual(item.Sequence, held.ReceiptSequence);
    }

    [TestMethod]
    public void AuthenticatedItemStatCannotStandInForTheMissingMoveSecondary()
    {
        var move = Moves[Id("METAL_CLAW")];
        var berry = Changed(move, WildBattleStat.Attack, true).Add(
            new SourceHeldItemConsumedEvent(6, 1, new(0), "ITEM_LIECHI_BERRY", 10, 10, 0)
            { StatChanges = [new(WildBattleStat.Attack, 6, 7)] });
        AssertNone(berry, "The only matching stat is the berry's authenticated prelude, not Metal Claw's secondary.");
        var held = CampaignHeldItemAnimationPresentation.AtResult(Double(berry, 4), SourceBattleScene.On, new MoveId?[4]);
        Assert.IsNotNull(held); Assert.AreEqual(5L, held.Sequence); Assert.AreEqual(6L, held.ReceiptSequence);
        var complete = Changed(move, WildBattleStat.Attack, true).Add(
            new SourceStatChangedEvent(6, 1, new(0), new(0), WildBattleStat.Attack, 7, 8)).Add(
            new SourceHeldItemConsumedEvent(7, 1, new(0), "ITEM_LIECHI_BERRY", 10, 10, 0)
            { StatChanges = [new(WildBattleStat.Attack, 7, 8)] });
        AssertOne(complete, 5, 232, 0, 15);
    }

    [TestMethod]
    public void LaterMalformedOrUnrelatedItemSuffixDoesNotRevokeTheDirectTargetWave()
    {
        var move = Moves[Id("CRUSH_CLAW")];
        var good = Changed(move, WildBattleStat.Defense, false)
            .Add(new SourceStatChangedEvent(6, 1, new(1), new(1), WildBattleStat.Defense, 5, 6))
            .Add(new SourceHeldItemConsumedEvent(7, 1, new(1), "ITEM_WHITE_HERB", 90, 90, 0)
                { StatChanges = [new(WildBattleStat.Defense, 5, 6)] });
        AssertOne(good, 5, 306, 1, 23);
        var item = (SourceHeldItemConsumedEvent)good[6];
        var restored = (SourceStatChangedEvent)good[5];
        var cases = new (string Reason, ImmutableArray<SourceBattleEvent> Events)[] {
            ("missing typed item receipt", good.RemoveAt(6)),
            ("missing item stat metadata", good.SetItem(6, item with { StatChanges = null })),
            ("wrong item stat metadata", good.SetItem(6, item with { StatChanges = [new(WildBattleStat.Defense, 4, 6)] })),
            ("wrong item recipient", good.SetItem(6, item with { Side = new(0) })),
            ("wrong restoration actor", good.SetItem(5, restored with { Actor = new(0) })),
            ("item crosses a turn", good.SetItem(6, item with { Turn = 2 })),
            ("duplicate item sequence", good.SetItem(6, item with { Sequence = 6 })),
            ("item HP accounting is invalid", good.SetItem(6, item with { HpAfter = 91 })),
            ("extra unauthenticated stat", good.Add(new SourceStatChangedEvent(8, 1, new(1), new(1), WildBattleStat.Speed, 6, 7)))
        };
        // The reduction belongs to the complete prefix through sequence 5. The later
        // restoration cannot borrow that hit, regardless of its item authentication.
        foreach (var (_, events) in cases) AssertOne(events, 5, 306, 1, 23);
    }

    [TestMethod]
    public void MalformedSelfOwnedItemPreludeCannotMasqueradeAsTheMoveSecondary()
    {
        var move = Moves[Id("METAL_CLAW")];
        var prefix = Changed(move, WildBattleStat.Attack, true);
        var item = new SourceHeldItemConsumedEvent(6, 1, new(0), "ITEM_LIECHI_BERRY", 10, 10, 0)
            { StatChanges = [new(WildBattleStat.Attack, 6, 7)] };
        foreach (var malformed in new[] {
            item with { StatChanges = null }, item with { StatChanges = [] },
            item with { StatChanges = [new(WildBattleStat.Defense, 6, 7)] },
            item with { StatChanges = [new(WildBattleStat.Attack, 5, 6)] },
            item with { StatChanges = [new(WildBattleStat.Attack, 6, 7), new(WildBattleStat.Attack, 6, 7)] },
            item with { HpAfter = 11 }, item with { Item = "ITEM_NONE" },
            item with { FocusEnergyRaised = true }
        }) AssertNone(prefix.Add(malformed), "An invalid same-owner consumed prelude cannot authenticate its only stat as a move secondary.");

        // A later consumption with another owner or turn is not this candidate's
        // prelude. Its invalid metadata cannot retroactively erase a complete hit.
        AssertOne(prefix.Add(item with { Side = new(1), StatChanges = null }), 5, 232, 0, 15);
        AssertOne(prefix.Add(item with { Turn = 2, StatChanges = null }), 5, 232, 0, 15);
    }

    [TestMethod]
    public void LaterDamageDuplicateStatRageAndContactCannotRevokeOrReplayTheFirstWave()
    {
        var move = Moves[Id("METAL_CLAW")]; var good = Changed(move, WildBattleStat.Attack, true);
        var hit = (SourceHpChangedEvent)good[3]; var stat = (SourceStatChangedEvent)good[4];
        AssertOne(good.Add(hit with { Sequence = 6 }), 5, 232, 0, 15);
        AssertOne(good.Add(stat with { Sequence = 6, Before = 7, After = 8 }), 5, 232, 0, 15);
        // BuildRage emits a self-owned target Attack increase after the move's
        // secondary; contact status follows that move-end command.
        var suffix = good.Add(new SourceStatChangedEvent(6, 1, new(1), new(1), WildBattleStat.Attack, 6, 7))
            .Add(new SourceAbilityActivatedEvent(7, 1, new(1), "ABILITY_STATIC"))
            .Add(new SourceStatusChangedEvent(8, 1, new(0), 0, WildBattleStatus.Paralysis, "static"))
            .Add(stat with { Sequence = 9, Before = 7, After = 8 });
        AssertOne(suffix, 5, 232, 0, 15);
        var target = Changed(Moves[Id("CRUSH_CLAW")], WildBattleStat.Defense, false)
            .Add(new SourceStatChangedEvent(6, 1, new(1), new(1), WildBattleStat.Attack, 6, 7));
        AssertOne(target, 5, 306, 1, 23);
    }

    [TestMethod]
    public void UnknownOrUnrelatedPreStatReceiptCannotBeSkippedToBorrowDamage()
    {
        var move = Moves[Id("METAL_CLAW")]; var good = Changed(move, WildBattleStat.Attack, true);
        var stat = (SourceStatChangedEvent)good[4];
        SourceBattleEvent[] intervening = [
            new SourceMtMoonVolatileEvent(5, 1, new(0), "unrecognized-secondary-prefix", 0, 1, "test"),
            new SourceAbilityActivatedEvent(5, 1, new(1), "ABILITY_STATIC"),
            new SourceStatusChangedEvent(5, 1, new(0), 0, WildBattleStatus.Paralysis, "static"),
            new SourceStatChangedEvent(5, 1, new(1), new(1), WildBattleStat.Attack, 6, 7)
        ];
        foreach (var receipt in intervening)
            AssertNone(good.SetItem(4, receipt).Add(stat with { Sequence = 6 }),
                "Only the required source hit-result prefix may precede the direct stat.");
    }

    [TestMethod]
    public void CriticalEffectivenessAndFocusBandReceiptsMustMatchTheRequiredPrefix()
    {
        var move = Moves[Id("CRUSH_CLAW")];
        var hp = new SourceHpChangedEvent(4, 1, new(0), new(1), move.Id, 100, 90, 10, true, 100);
        var critical = new SourceCriticalHitEvent(5, 1, new(0), new(1));
        var effectiveness = new SourceEffectivenessEvent(6, 1, new(0), new(1), move.Id, WildBattleEffectiveness.SuperEffective);
        var stat = new SourceStatChangedEvent(7, 1, new(0), new(1), WildBattleStat.Defense, 6, 5);
        var good = Invocation(move, 0, 1, hp, critical, effectiveness, stat);
        AssertOne(good, 7, 306, 1, 23);
        AssertOne(good.SetItem(5, effectiveness with { Effectiveness = WildBattleEffectiveness.NotVeryEffective }), 7, 306, 1, 23);
        AssertOne(good.RemoveAt(5), 7, 306, 1, 23);
        AssertOne(good.SetItem(3, hp with { Critical = false }).RemoveAt(4), 7, 306, 1, 23);
        var criticalCases = new (string Reason, ImmutableArray<SourceBattleEvent> Events)[] {
            ("missing required critical", good.RemoveAt(4)),
            ("unexpected critical", good.SetItem(3, hp with { Critical = false })),
            ("foreign critical actor", good.SetItem(4, critical with { Actor = new(1) })),
            ("foreign critical target", good.SetItem(4, critical with { Target = new(0) })),
            ("effectiveness precedes critical", good.SetItem(4, effectiveness with { Sequence = 5 }).SetItem(5, critical with { Sequence = 6 })),
            ("foreign effectiveness actor", good.SetItem(5, effectiveness with { Actor = new(1) })),
            ("foreign effectiveness target", good.SetItem(5, effectiveness with { Target = new(0) })),
            ("foreign effectiveness move", good.SetItem(5, effectiveness with { Move = Id("TACKLE") })),
            ("no-effect result", good.SetItem(5, effectiveness with { Effectiveness = WildBattleEffectiveness.NoEffect })),
            ("unknown effectiveness value", good.SetItem(5, effectiveness with { Effectiveness = (WildBattleEffectiveness)99 }))
        };
        foreach (var (reason, events) in criticalCases) AssertNone(events, reason);

        // Focus Band's successful resultmessage can preserve one HP with zero
        // loss. There is no critical receipt when the committed damage is zero.
        var adjusted = new SourceDamageAdjustedEvent(4, 1, new(1), move.Id, "ITEM_FOCUS_BAND", 10, 0, false, true);
        var zero = hp with { Sequence = 5, HpBefore = 1, HpAfter = 1, Damage = 0 };
        var survived = new SourceHeldItemSurvivalEvent(6, 1, new(1), move.Id, "ITEM_FOCUS_BAND");
        var band = Invocation(move, 0, 1, adjusted, zero, survived, stat);
        AssertOne(band, 7, 306, 1, 23);
        var survivalCases = new (string Reason, ImmutableArray<SourceBattleEvent> Events)[] {
            ("missing survival result", band.RemoveAt(5)),
            ("unexplained zero loss", band.RemoveAt(5).RemoveAt(3)),
            ("foreign adjustment target", band.SetItem(3, adjusted with { Target = new(0) })),
            ("foreign adjustment move", band.SetItem(3, adjusted with { Move = Id("TACKLE") })),
            ("foreign adjustment item", band.SetItem(3, adjusted with { Item = "ITEM_LIECHI_BERRY" })),
            ("zero initial adjustment", band.SetItem(3, adjusted with { DamageBefore = 0 })),
            ("adjusted damage disagrees", band.SetItem(3, adjusted with { DamageAfter = 1 })),
            ("ambiguous survival mode", band.SetItem(3, adjusted with { Endured = true })),
            ("missing survival mode", band.SetItem(3, adjusted with { HungOn = false })),
            ("foreign survival target", band.SetItem(5, survived with { Target = new(0) })),
            ("foreign survival move", band.SetItem(5, survived with { Move = Id("TACKLE") })),
            ("foreign survival item", band.SetItem(5, survived with { Item = "ITEM_LIECHI_BERRY" })),
            ("nonlethal adjustment", band.SetItem(3, adjusted with { DamageBefore = 20, DamageAfter = 10 })
                .SetItem(4, zero with { HpBefore = 100, HpAfter = 90, Damage = 10, Critical = false })),
            ("critical receipt on zero loss", band.Insert(5, critical with { Sequence = 6 })
                .SetItem(6, survived with { Sequence = 7 }).SetItem(7, stat with { Sequence = 8 }))
        };
        foreach (var (reason, events) in survivalCases) AssertNone(events, reason);
    }

    [TestMethod]
    public void ForeignMoveActorTargetPpOrSequenceCannotAuthorizeASecondary()
    {
        var move = Moves[Id("CRUSH_CLAW")];
        var good = Changed(move, WildBattleStat.Defense, false);
        var attack = (SourceAttackStringEvent)good[0]; var used = (SourceMoveUsedEvent)good[1];
        var pp = (SourcePpCommandEvent)good[2]; var hit = (SourceHpChangedEvent)good[3];
        var stat = (SourceStatChangedEvent)good[4]; var foreign = Id("TACKLE");
        var cases = new (string Reason, ImmutableArray<SourceBattleEvent> Events)[] {
            ("foreign attack actor", good.SetItem(0, attack with { Actor = new(1) })),
            ("called attack choice", good.SetItem(0, attack with { ChosenMove = foreign })),
            ("foreign current attack", good.SetItem(0, attack with { CurrentMove = foreign })),
            ("attack after move receipt", good.SetItem(0, attack with { Sequence = 2 })),
            ("foreign used actor", good.SetItem(1, used with { Actor = new(1) })),
            ("foreign selected target", good.SetItem(1, used with { Target = new(0) })),
            ("foreign used move", good.SetItem(1, used with { Move = foreign })),
            ("used crosses a turn", good.SetItem(1, used with { Turn = 2 })),
            ("used move slot outside list", good.SetItem(1, used with { Slot = 4 }).SetItem(2, pp with { PpSlot = 4 })),
            ("foreign PP actor", good.SetItem(2, pp with { Actor = new(1) })),
            ("foreign PP owner", good.SetItem(2, pp with { PpOwner = new(1) })),
            ("foreign PP target", good.SetItem(2, pp with { Target = new(0) })),
            ("foreign PP slot", good.SetItem(2, pp with { PpSlot = 1 })),
            ("foreign PP current move", good.SetItem(2, pp with { CurrentMove = foreign })),
            ("called PP choice", good.SetItem(2, pp with { ChosenMove = foreign })),
            ("PP before differs", good.SetItem(2, pp with { Before = 11 })),
            ("PP after differs", good.SetItem(2, pp with { After = 8 })),
            ("PP cost differs", good.SetItem(2, pp with { CommandCost = 2 })),
            ("unspent PP", good.SetItem(1, used with { PpAfter = 10 }).SetItem(2, pp with { After = 10 })),
            ("duplicate PP sequence", good.SetItem(2, pp with { Sequence = 2 })),
            ("foreign hit actor", good.SetItem(3, hit with { Actor = new(1) })),
            ("foreign hit target", good.SetItem(3, hit with { Target = new(0) })),
            ("foreign hit move", good.SetItem(3, hit with { Move = foreign })),
            ("foreign hit turn", good.SetItem(3, hit with { Turn = 2 })),
            ("foreign stat actor", good.SetItem(4, stat with { Actor = new(1) })),
            ("foreign stat target", good.SetItem(4, stat with { Target = new(0) })),
            ("foreign stat turn", good.SetItem(4, stat with { Turn = 2 })),
            ("stat sequence duplicates damage", good.SetItem(4, stat with { Sequence = 4 })),
            ("missing PP", good.RemoveAt(2)), ("missing attack", good.RemoveAt(0))
        };
        foreach (var (reason, events) in cases) AssertNone(events, reason);
    }

    [TestMethod]
    public void OneCommittedHitAndCorrectStageChangeAreRequiredWithinThePrefix()
    {
        var move = Moves[Id("CRUSH_CLAW")]; var good = Changed(move, WildBattleStat.Defense, false);
        var hit = (SourceHpChangedEvent)good[3]; var stat = (SourceStatChangedEvent)good[4];
        var cases = new (string Reason, ImmutableArray<SourceBattleEvent> Events)[] {
            ("missing committed damage", good.RemoveAt(3)),
            ("reversed damage/stat order", good.SetItem(3, stat with { Sequence = 4 }).SetItem(4, hit with { Sequence = 5 })),
            ("duplicate damage", good.Insert(4, hit with { Sequence = 5 }).SetItem(5, stat with { Sequence = 6 })),
            ("wrong stat", good.SetItem(4, stat with { Stat = WildBattleStat.Attack })),
            ("no stat delta", good.SetItem(4, stat with { After = 6 })),
            ("wrong sign", good.SetItem(4, stat with { After = 7 })),
            ("wrong delta", good.SetItem(4, stat with { After = 4 })),
            ("stage underflow", good.SetItem(4, stat with { Before = 0, After = -1 })),
            ("stage overflow", good.SetItem(4, stat with { Before = 13, After = 12 })),
            ("negative HP", good.SetItem(3, hit with { HpAfter = -1, Damage = 101 })),
            ("healing is not damage", good.SetItem(3, hit with { HpAfter = 110, Damage = -10 })),
            ("inconsistent HP accounting", good.SetItem(3, hit with { Damage = 9 })),
            ("non-positive initial HP", good.SetItem(3, hit with { HpBefore = 0, HpAfter = 0, Damage = 0 })),
            ("invalid damage random percentage", good.SetItem(3, hit with { RandomPercent = 84 })),
            ("synthetic minimum cap", good.SetItem(4, new SourceStatAtMinimumEvent(5, 1, new(0), new(1), WildBattleStat.Defense))),
            ("miss before stat", good.SetItem(3, new SourceMoveMissedEvent(4, 1, new(0), new(1), move.Id)))
        };
        foreach (var (reason, events) in cases) AssertNone(events, reason);
    }

    [TestMethod]
    public void MoveSwitchTurnAndSpreadPassBoundariesCannotLendTheirEarlierDamage()
    {
        var move = Moves[Id("CRUSH_CLAW")]; var good = Changed(move, WildBattleStat.Defense, false);
        var stat = (SourceStatChangedEvent)good[4];
        SourceBattleEvent[] boundaries = [new SourceTurnReadyEvent(5, 1, 0),
            new SourceCalledPartySwitchedEvent(5, 1, new(1), 0, 1, WildBattleCalledSwitchKind.Forced),
            new SourceAttackStringEvent(5, 1, new(0), Id("TACKLE"), Id("TACKLE")),
            new SourceMoveUsedEvent(5, 1, new(0), new(1), 0, Id("TACKLE"), 10, 9)];
        foreach (var boundary in boundaries)
            AssertNone(good.SetItem(4, boundary).Add(stat with { Sequence = 6 }), "A boundary splits damage from the stat.");
        var revisit = good.Add(new SourcePpCommandEvent(6, 1, new(0), new(3), 0, move.Id, move.Id, 9, 9, 0, 0))
            .Add(new SourceHpChangedEvent(7, 1, new(0), new(3), move.Id, 100, 90, 10, false, 100))
            .Add(stat with { Sequence = 8, Target = new(3) });
        AssertOne(revisit, 5, 306, 1, 23, singles: false);
        // A later complete invocation remains independent of an earlier one.
        var later = good.Select(e => e with { Sequence = e.Sequence + 10, Turn = 2 }).ToImmutableArray();
        var both = good.AddRange(later);
        Assert.AreEqual(5L, AnimationDispatchFixture.StatAtResult(Double(both, 4), moves: Moves)!.Sequence);
        Assert.AreEqual(15L, AnimationDispatchFixture.StatAtResult(Double(both, 9), moves: Moves)!.Sequence);
    }

    [TestMethod]
    [DataRow("ANCIENT_POWER", WildBattleStat.Attack, true)]
    [DataRow("SUPERPOWER", WildBattleStat.Attack, true)]
    public void AllStatsAndSuperpowerRemainOutsideTheOneStatFamily(string name, WildBattleStat stat, bool self)
    {
        var move = Moves[Id(name)];
        var events = Changed(move, stat, self);
        if (name == "SUPERPOWER")
        {
            var result = (SourceStatChangedEvent)events[4];
            events = events.SetItem(4, result with { After = 5 });
        }
        AssertNone(events, "These source scripts require their own admission and cannot enter the selected-target one-stat family.");
    }

    [TestMethod]
    [DataRow("OVERHEAT", 6)]
    [DataRow("OVERHEAT", 1)]
    [DataRow("PSYCHO_BOOST", 6)]
    [DataRow("PSYCHO_BOOST", 1)]
    public void CertainSelfSpecialAttackDropUsesItsSourceEffectAndRequestedTwoStageWave(string name, int before)
    {
        var move = Moves[Id(name)];
        Assert.AreEqual("EFFECT_OVERHEAT", move.Effect);
        Assert.AreEqual(100, move.SecondaryEffectChance);
        var events = Changed(move, WildBattleStat.SpecialAttack, true, before: before);
        var result = (SourceStatChangedEvent)events[4];
        events = events.SetItem(4, result with { After = Math.Max(0, before - 2) });
        AssertOne(events, 5, move.SourceMoveId, 0, 49);
        AssertNone(events.SetItem(4, result with { Target = new(1), After = Math.Max(0, before - 2) }),
            "The certain self drop cannot borrow a selected-target stat receipt.");
        AssertNone(events.SetItem(4, result with { After = before }), "A capped/no-change result owns no source wave.");
    }

    [TestMethod]
    public void PhaseIndexSourceRulesAndAffectedSubstituteGatesRemainClosed()
    {
        var events = Changed(Moves[Id("METAL_CLAW")], WildBattleStat.Attack, true);
        Assert.IsNull(AnimationDispatchFixture.StatAtResult(Double(events, 4) with { Stage = CampaignDoubleBattleStage.Finished }, moves: Moves));
        Assert.IsNull(AnimationDispatchFixture.StatAtResult(Single(events, 4) with { Phase = CampaignBattlePhase.Results }, true, moves: Moves));
        foreach (int index in new[] { -1, events.Length, events.Length + 1 })
        {
            Assert.IsNull(AnimationDispatchFixture.StatAtResult(Double(events, index), moves: Moves));
            Assert.IsNull(AnimationDispatchFixture.StatAtResult(Single(events, index), true, moves: Moves));
        }
        Assert.IsNull(AnimationDispatchFixture.StatAtResult(Double(events, 4), true, Moves));
        Assert.IsNull(AnimationDispatchFixture.StatAtResult(Single(events, 4), true, true, Moves));
        Assert.IsNull(AnimationDispatchFixture.StatAtResult(Single(events, 4), false, moves: Moves));
        Assert.IsTrue(CampaignResultAnimationPolicy.Allows(CampaignResultAnimationKind.StatChange, SourceBattleScene.On));
        Assert.IsTrue(CampaignResultAnimationPolicy.Allows(CampaignResultAnimationKind.StatChange, SourceBattleScene.Off));
        Assert.IsFalse(CampaignResultAnimationPolicy.Allows(CampaignResultAnimationKind.HeldItem, SourceBattleScene.Off));
    }

    [TestMethod]
    public void MetalSoundPrimaryKeepsItsBaselineAndCatalogSelectedAllyReduction()
    {
        var move = Moves[Id("METAL_SOUND")];
        Assert.AreEqual("MOVE_TARGET_SELECTED", move.Target);
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 2 })
        {
            var events = Invocation(move, actor, target,
                new SourceStatChangedEvent(4, 1, new(actor), new(target), WildBattleStat.SpecialDefense, 6, 4));
            var dispatch = AnimationDispatchFixture.StatAtResult(Double(events, 3), moves: Moves);
            Assert.IsNotNull(dispatch); Assert.AreEqual(319, dispatch.SourceMoveId);
            Assert.AreEqual(50, dispatch.AnimationArgument); Assert.AreEqual(target, dispatch.Affected.Value);
            if (target == (actor ^ 1))
                Assert.AreEqual(dispatch, AnimationDispatchFixture.StatAtResult(Double(events, 3)),
                    "The primary enemy path uses the same canonical catalog as the fixture.");
            if (actor < 2 && target < 2)
                Assert.AreEqual(dispatch, AnimationDispatchFixture.StatAtResult(Single(events, 3), true, moves: Moves));
        }
    }
}

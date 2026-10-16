using System.Collections.Immutable;
using System.Reflection;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using FireRed.Tests.Battle.Encounters;

namespace FireRed.Tests.Battle.TrainerPolicy;

[TestClass, TestCategory("Game")]
public sealed class TrainerDamagePredictionTests
{
    private static SourceMechanicsCatalog Source => TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static PokemonCatalog Catalog => Source.Catalog;
    private static MoveId Move(string name) => new("pret:MOVE_" + name);
    private static WildBattleCombatant Mon(string species, string move, uint personality = 0)
    {
        var pokemon = SourcePokemonFactory.Create(Source,
            new(new("pret:SPECIES_" + species), 50, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(personality)), new(0)).Pokemon;
        var definition = Catalog.Moves[Move(move)];
        return new(pokemon with { Moves = [new(definition.Id, definition.Pp, definition.Pp)] });
    }
    private static WildBattleCalledPartyMember Member(WildBattleCombatant mon) => new(mon.Pokemon, mon.HeldItem, mon.Status);
    private static SourceBattleFrame Frame(WildBattleCombatant user, WildBattleCombatant target,
        SourceFieldRegisters? field = null, WildBattleCombatant? partner = null)
    {
        var otherPlayer = partner is null ? null : Mon("CLEFAIRY", "TACKLE");
        return new() {
            Layout = partner is null ? SourceBattleLayout.Singles : SourceBattleLayout.PresetDoubles,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version,
            BattleKind = WildBattleCalledBattleKind.Wild, Rng = new(99), Field = field ?? new(),
            SourceAbsentMask = partner is null ? (byte)12 : (byte)0,
            TurnStartAbsentMask = partner is null ? (byte)12 : (byte)0,
            Parties = [new(BattleSide.Player, otherPlayer is null ? [Member(target)] : [Member(target), Member(otherPlayer)]),
                new(BattleSide.Opponent, partner is null ? [Member(user)] : [Member(user), Member(partner)])],
            Slots = [new(new(0), new(BattleSide.Player, 0), 1, target), new(new(1), new(BattleSide.Opponent, 0), 1, user),
                new(new(2), otherPlayer is null ? null : new(BattleSide.Player, 1), otherPlayer is null ? 0 : 1, otherPlayer),
                new(new(3), partner is null ? null : new(BattleSide.Opponent, 1), partner is null ? 0 : 1, partner)]
        };
    }
    // The internal context is opaque in production. Tests supply explicit slots without adding a
    // public bypass around the real trainer metadata/roster validation exercised below.
    private static SourceTrainerAiSlotContext Context(SourceBattleFrame frame, int percent = 100, uint flags = 1,
        string? knownHoldEffect = null) => (SourceTrainerAiSlotContext)Activator.CreateInstance(
            typeof(SourceTrainerAiSlotContext), BindingFlags.Instance | BindingFlags.NonPublic, null,
            [Catalog, frame, new BattlerSlot(1), new BattlerSlot(0), ImmutableArray.Create(percent, percent, percent, percent),
                frame.Rng, flags, ImmutableArray<SourceActionSelection>.Empty, default(MoveId), knownHoldEffect], null)!;
    private static CeruleanTrainerAiDecision Score(SourceBattleFrame frame, int percent = 100, uint flags = 1,
        string? knownAbility = null, string? knownHoldEffect = null)
    {
        var result = CeruleanTrainerAi.Choose(new(frame.Get(new(1)).Body!, frame.Get(new(0)).Body!, knownAbility, flags),
            frame.Rng, Context(frame, percent, flags, knownHoldEffect),
            rain: SourceWeatherRules.HasRain(frame.Field), reflect: frame.Sides[0].ReflectTurnsRemaining > 0);
        Assert.IsTrue(result.Succeeded, result.Diagnostic?.Code);
        return result;
    }
    private static int ActualDamage(SourceBattleFrame frame)
    {
        var user = frame.Get(new(1)).Body!; var target = frame.Get(new(0)).Body!;
        var move = Catalog.Moves[user.Pokemon.Moves[0].Move];
        var field = frame.Slots.Take(frame.BattlerCount).Where(c => c.Body is not null).Select(c => c.Body!).ToArray();
        var opposite = WildBattleEffective.Ability(user) switch { "ABILITY_PLUS" => "ABILITY_MINUS", "ABILITY_MINUS" => "ABILITY_PLUS", _ => "" };
        return SourceBattleDamage.Calculate(Catalog, user, target, move, false, 100,
            rain: SourceWeatherRules.HasRain(frame.Field), sun: SourceWeatherRules.HasSun(frame.Field),
            weatherSuppressed: SourceWeatherRules.IsSuppressed(frame, includeFainted: true),
            sourceSolarBeamWeakWeather: move.Effect == "EFFECT_SOLAR_BEAM" && (SourceWeatherRules.HasRain(frame.Field) || SourceWeatherRules.HasSandstorm(frame.Field) || frame.Field.HailTurnsRemaining > 0),
            sourceDoubleBattle: frame.Layout == SourceBattleLayout.PresetDoubles,
            sourceDefenderCount: frame.Slots.Count(c => c.Slot.Side == BattleSide.Player && !frame.IsSourceAbsent(c.Slot)),
            sourcePlusMinusBoost: opposite.Length > 0 && field.Any(m => WildBattleEffective.Ability(m) == opposite),
            sourceMudSport: field.Any(m => m.MudSport), sourceWaterSport: field.Any(m => m.WaterSport)).Damage;
    }
    private static void AssertDamage(SourceBattleFrame frame, int expected)
    {
        // Both entry points face an independent numeric oracle, rather than asserting they agree.
        Assert.AreEqual(expected, ActualDamage(frame), "execution versus source arithmetic");
        Assert.AreEqual(expected, Score(frame).PredictedDamage[0], "AI versus source arithmetic");
        Assert.AreEqual(Math.Max(1, expected * 85 / 100), Score(frame, 85).PredictedDamage[0], "AI percent remains after TypeCalc");
    }

    [TestMethod]
    public void BadgesAndHeldItemsUseTheSameSourceIntegerOrder()
    {
        var target = Mon("CLEFAIRY", "TACKLE"); var user = Mon("RATICATE", "HYPER_FANG");
        Assert.AreEqual(101, user.Pokemon.Stats.Attack); Assert.AreEqual(68, target.Pokemon.Stats.Defense);
        // floor(floor(101 * 80 * 22 / 68) / 50) + 2, then floor(* 1.5 STAB).
        AssertDamage(Frame(user, target), 81);
        AssertDamage(Frame(user, target with { DefenseBadgeBoost = true }), 75);
        AssertDamage(Frame(user with { AttackBadgeBoost = true }, target), 88);
        AssertDamage(Frame(user with { HeldItem = "ITEM_CHOICE_BAND" }, target), 120);
        var at77 = target with { DefenseBadgeBoost = true, Pokemon = target.Pokemon with { CurrentHp = 77 } };
        Assert.AreEqual(100, Score(Frame(user, at77), flags: 7).Scores[0], "75 damage cannot earn a KO bonus at 77 HP");
        var fire = Mon("CHARIZARD", "FLAMETHROWER");
        Assert.AreEqual(129, fire.Pokemon.Stats.SpecialAttack); Assert.AreEqual(85, target.Pokemon.Stats.SpecialDefense);
        AssertDamage(Frame(fire, target), 97);
        AssertDamage(Frame(fire, target with { SpecialBadgeBoost = true }), 88);
        AssertDamage(Frame(fire with { SpecialBadgeBoost = true }, target), 106);
        AssertDamage(Frame(fire with { HeldItem = "ITEM_CHARCOAL" }, target), 106);
        AssertDamage(Frame(Mon("PIKACHU", "THUNDERBOLT") with { HeldItem = "ITEM_LIGHT_BALL" }, target), 105);
        AssertDamage(Frame(Mon("CLAMPERL", "WATER_GUN") with { HeldItem = "ITEM_DEEP_SEA_TOOTH" }, target), 60);
        AssertDamage(Frame(Mon("LATIOS", "DRAGON_BREATH") with { HeldItem = "ITEM_SOUL_DEW" }, target), 106);
        AssertDamage(Frame(fire, Mon("CLAMPERL", "WATER_GUN") with { HeldItem = "ITEM_DEEP_SEA_SCALE" }), 27);
        AssertDamage(Frame(fire, Mon("LATIOS", "DRAGON_BREATH") with { HeldItem = "ITEM_SOUL_DEW" }), 21);
    }

    [TestMethod]
    public void AttackAndDefenseAbilitiesParticipateBeforeStages()
    {
        var target = Mon("CLEFAIRY", "TACKLE");
        // PID 25 keeps Hardy's neutral stats and selects ability slot 2.
        var huge = Mon("AZUMARILL", "BODY_SLAM", 25);
        Assert.AreEqual("ABILITY_HUGE_POWER", huge.Pokemon.AbilityId);
        AssertDamage(Frame(huge, target), 79); // 70 * 2 attack; non-STAB 85 power.
        AssertDamage(Frame(Mon("MEDICHAM", "BRICK_BREAK"), target), 236); // 80 * 2, STAB, weakness.
        AssertDamage(Frame(Mon("TOGETIC", "DOUBLE_EDGE"), target), 106); // 60 * 150 / 100, STAB.
        var marvel = Mon("MILOTIC", "SURF") with { Status = WildBattleStatus.Poison };
        AssertDamage(Frame(Mon("RATICATE", "HYPER_FANG"), marvel), 39); // 99 * 150 / 100 defense.
    }

    [TestMethod]
    public void DamageWeatherQueryIncludesFaintedBodiesWithoutChangingAbilityKnowledge()
    {
        var fire = Mon("CHARIZARD", "FLAMETHROWER");
        var cloud = Mon("GOLDUCK", "SURF", 25);
        Assert.AreEqual("ABILITY_CLOUD_NINE", cloud.Pokemon.AbilityId);
        // Golduck has 100 special defense; damage is floor(82 / 2) = 41 after Water resistance.
        AssertDamage(Frame(fire, cloud), 41);
        AssertDamage(Frame(fire, cloud, new() { SunTurnsRemaining = 5 }), 41);
        AssertDamage(Frame(fire, cloud, new() { RainTurnsRemaining = 5 }), 41);
        foreach (var suppressor in new[] { cloud, Mon("RAYQUAZA", "FLY") })
        {
            var retained = suppressor with { Pokemon = suppressor.Pokemon with { CurrentHp = 0 } };
            var frame = Frame(fire, Mon("CLEFAIRY", "TACKLE"), new() { SunTurnsRemaining = 5 }, retained)
                with { SourceAbsentMask = 8, TurnStartAbsentMask = 8 };
            AssertDamage(frame, 97);
        }
        var unknown = Score(Frame(fire, cloud, new() { SunTurnsRemaining = 5 }));
        var known = Score(Frame(fire, cloud, new() { SunTurnsRemaining = 5 }), knownAbility: "ABILITY_DAMP");
        Assert.AreEqual(41, unknown.PredictedDamage[0]); Assert.AreEqual(41, known.PredictedDamage[0]);
        Assert.IsTrue(unknown.AbilityReads.Any(r => r.Guessed));
        Assert.IsTrue(known.AbilityReads.All(r => !r.Guessed && r.Ability == "ABILITY_DAMP"));
    }

    [TestMethod]
    public void CloudNinePreventsRainFromMakingTheControllerPreferWeakerStrength()
    {
        var fire = Mon("CHARIZARD", "FLAMETHROWER");
        var strength = Catalog.Moves[Move("STRENGTH")];
        fire = fire with { Pokemon = fire.Pokemon with {
            Moves = fire.Pokemon.Moves.Add(new(strength.Id, strength.Pp, strength.Pp)) } };
        var frame = Frame(fire, Mon("GOLDUCK", "SURF", 25), new() { RainTurnsRemaining = 5 });
        var result = Score(frame, flags: 7, knownAbility: "ABILITY_CLOUD_NINE");
        CollectionAssert.AreEqual(new[] { 41, 39, 0, 0 }, result.PredictedDamage.ToArray());
        CollectionAssert.AreEqual(new[] { 100, 99, 0, 0 }, result.Scores.ToArray());
        Assert.AreEqual(0, result.MoveSlot);
        Assert.AreEqual(1, result.Trace.Length, "known ability needs only the existing choose-best draw");
    }

    [TestMethod]
    public void FieldSportsAndPlusMinusReadOtherRetainedPositions()
    {
        var target = Mon("CLEFAIRY", "TACKLE"); var minus = Mon("MINUN", "THUNDERBOLT");
        var plus = Mon("PLUSLE", "THUNDERBOLT");
        AssertDamage(Frame(plus, target), 79);
        AssertDamage(Frame(plus, target, partner: minus), 118);
        AssertDamage(Frame(plus, target, partner: minus with { Pokemon = minus.Pokemon with { CurrentHp = 0 } })
            with { SourceAbsentMask = 8, TurnStartAbsentMask = 8 }, 118);
        AssertDamage(Frame(Mon("CHARIZARD", "FLAMETHROWER"), target, partner: minus with { WaterSport = true }), 49);
        AssertDamage(Frame(Mon("PIKACHU", "THUNDERBOLT"), target, partner: minus with { MudSport = true }), 28);
    }

    [TestMethod]
    public void PredictionRetainsStaticPowerLevitateAndKnowledgeBoundaries()
    {
        var user = Mon("RATICATE", "HYPER_FANG");
        var ditto = Mon("DITTO", "TRANSFORM") with { HeldItem = "ITEM_METAL_POWDER" };
        var unknown = Score(Frame(user, ditto));
        var known = Score(Frame(user, ditto), knownHoldEffect: "HOLD_EFFECT_METAL_POWDER");
        Assert.AreEqual(42, unknown.PredictedDamage[0]);
        CollectionAssert.AreEqual(unknown.PredictedDamage.ToArray(), known.PredictedDamage.ToArray());
        // AI_CalcDmg uses Rollout's static 30 power even after the action's rolling multiplier.
        var rollout = Mon("MARILL", "ROLLOUT") with { RolloutTurnsRemaining = 3 };
        var staticRoll = Score(Frame(rollout, Mon("CLEFAIRY", "TACKLE")));
        Assert.AreEqual(9, staticRoll.PredictedDamage[0]);
        // TypeCalc sets a discarded Levitate flag, so prediction remains nonzero; damage does not.
        var ground = Frame(Mon("SANDSLASH", "EARTHQUAKE"), Mon("GASTLY", "LICK"));
        Assert.AreEqual(0, ActualDamage(ground));
        Assert.AreEqual(160, Score(ground).PredictedDamage[0]);
    }

    [TestMethod]
    public void RealTrainerControllerChangesMoveForDefenseBadgeWithoutAddingRandomDraws()
    {
        var opponents = RegionalWildDefinitionTests.Owned.Opponents;
        var original = SharedTrainerCatalog.LoadProject(TestAssets.Root).Definition(new("pret:TRAINER_YOUNGSTER_BEN"));
        var trainer = original with { PartyFlags = 1, AiFlags = 7,
            AiFlagSymbols = ["AI_SCRIPT_CHECK_BAD_MOVE", "AI_SCRIPT_CHECK_VIABILITY", "AI_SCRIPT_TRY_TO_FAINT"],
            EncodedTrainerName = [1], Party = [original.Party[0] with {
                Species = new("pret:SPECIES_RATICATE"), SourceSpeciesId = 20, Level = 50, SourceIv = 255,
                EncodedSpeciesName = [24], HeldItem = "ITEM_NONE", CustomMoves = [Move("HYPER_FANG"), Move("ICE_BEAM")] }] };
        var setup = SourceTrainerEncounterEntry.Prepare(new(42), "damage-regression");
        var made = TrainerPartyFactory.Create(ImmutableDictionary<TrainerId, TrainerDefinition>.Empty.Add(trainer.Id, trainer), opponents, trainer.Id, setup.Rng);
        var entered = SourceTrainerEncounterEntry.Begin(Catalog, setup, made, [Mon("VENUSAUR", "TACKLE")], 0);
        Assert.IsTrue(entered.Succeeded, entered.Fault);
        var plain = entered.State! with { Rng = new(99) };
        Assert.AreEqual(101, plain.Get(new(1)).Body!.Pokemon.Stats.Attack);
        Assert.AreEqual(70, plain.Get(new(1)).Body!.Pokemon.Stats.SpecialAttack);
        var badge = plain with { Slots = plain.Slots.SetItem(0, plain.Slots[0] with { Body = plain.Slots[0].Body! with { DefenseBadgeBoost = true } }) };
        var before = SourceFourTrainerAi.ChooseMove(Catalog, trainer, plain, plain.Revision, new(1));
        var after = SourceFourTrainerAi.ChooseMove(Catalog, trainer, badge, badge.Revision, new(1));
        Assert.IsTrue(before.Succeeded, before.Fault); Assert.IsTrue(after.Succeeded, after.Fault);
        // Independent arithmetic: Hyper Fang 54 -> 49; Ice Beam 52, unchanged.
        Assert.AreEqual(0, before.Selections.Single().MoveSlot);
        Assert.AreEqual(1, after.Selections.Single().MoveSlot);
        CollectionAssert.AreEqual(new[] { 100, 100, 99, 96 }, after.Choices.Single().SimulatedDamagePercent.ToArray());
        CollectionAssert.AreEqual(new[] { 100, 99, 0, 0 }, before.Choices.Single().Scores.ToArray());
        CollectionAssert.AreEqual(new[] { 99, 100, 0, 0 }, after.Choices.Single().Scores.ToArray());
        Assert.AreEqual(before.State.Rng, after.State.Rng);
        CollectionAssert.AreEqual(before.Trace.Select(t => (t.Purpose, t.Before, t.After, t.Raw)).ToArray(),
            after.Trace.Select(t => (t.Purpose, t.Before, t.After, t.Raw)).ToArray());
        Assert.AreEqual(5, after.Trace.Length, "four damage-percent draws and the existing choose-best draw");
    }
}

using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Game.Domain.Rules;

namespace Game.Domain.Battle;

public static class SourceBattleDamage
{
    // Gen III integer arithmetic order, shared by ordinary, called and delayed moves.
    public static DamageResult Calculate(PokemonCatalog catalog, WildBattleCombatant user,
        WildBattleCombatant target, PokemonMoveDefinition move, bool critical, int randomPercent,
        bool pursuitSwitch = false, int? dynamicPower = null, bool rain = false, bool reflect = false,
        ElementType? sourceEffectiveType = null, int sourceDamageMultiplier = 1, bool sun = false,
        bool lightScreen = false, bool weatherSuppressed = false, bool sourceSolarBeamWeakWeather = false,
        bool sourceDoubleBattle = false, int sourceDefenderCount = 1, bool sourcePlusMinusBoost = false,
        bool sourceHelpingHand = false, bool sourceMudSport = false, bool sourceWaterSport = false)
    {
        var mechanics = catalog.MechanicsData ?? throw new ArgumentException("Battle mechanics data required.", nameof(catalog));
        if (randomPercent is < 85 or > 100) throw new ArgumentOutOfRangeException(nameof(randomPercent));
        if (!SourceMoveDefinitionRules.IsValid(move)) throw new NotSupportedException("called_damage_source_definition");
        var moveId = move.Id;
        bool struggle = moveId == WildBattleRules.StruggleMove;
        if (!struggle && move.Power == 0) throw new NotSupportedException("wild_battle_damage_move");
        if (sourceSolarBeamWeakWeather && (move.Effect != "EFFECT_SOLAR_BEAM")) throw new NotSupportedException("called_solar_weather_context");
        if (sourceDefenderCount is < 0 or > 2)
            throw new NotSupportedException("called_damage_topology_context");
        bool validEffectiveType = sourceEffectiveType is { } effective && Enum.IsDefined(effective) && move.Effect switch {
            "EFFECT_HIDDEN_POWER" => effective is not (ElementType.Normal or ElementType.Mystery),
            "EFFECT_WEATHER_BALL" => effective is ElementType.Normal or ElementType.Water or ElementType.Rock or ElementType.Fire or ElementType.Ice,
            _ => false };
        if (sourceEffectiveType is not null && (!validEffectiveType))
            throw new NotSupportedException("called_damage_effective_type");
        if (sourceDamageMultiplier != 1 && !(move.Effect == "EFFECT_SPIT_UP" && sourceDamageMultiplier is 2 or 3) && (move.Effect is not ("EFFECT_FACADE" or "EFFECT_SMELLINGSALT" or "EFFECT_WEATHER_BALL" or "EFFECT_EARTHQUAKE" or "EFFECT_MAGNITUDE" or "EFFECT_GUST" or "EFFECT_TWISTER") && move.Id.Value is not ("pret:MOVE_SURF" or "pret:MOVE_WHIRLPOOL") || sourceDamageMultiplier != 2))
            throw new NotSupportedException("called_damage_multiplier");
        if ((move.Effect is "EFFECT_RETURN" or "EFFECT_FRUSTRATION" or "EFFECT_HIDDEN_POWER" or "EFFECT_ERUPTION" or "EFFECT_PRESENT" or "EFFECT_TRIPLE_KICK") && dynamicPower is null)
            throw new NotSupportedException("called_damage_dynamic_power_required");
        if (move.Effect == "EFFECT_HIDDEN_POWER")
        {
            if (sourceEffectiveType is null) throw new NotSupportedException("called_damage_effective_type_required");
            var iv = user.Pokemon.Ivs;
            int Bits(int bit) => ((iv.Hp >> bit) & 1) | (((iv.Attack >> bit) & 1) << 1) |
                (((iv.Defense >> bit) & 1) << 2) | (((iv.Speed >> bit) & 1) << 3) |
                (((iv.SpecialAttack >> bit) & 1) << 4) | (((iv.SpecialDefense >> bit) & 1) << 5);
            int expectedType = Bits(0) * 15 / 63 + 1;
            if (expectedType >= (int)ElementType.Mystery) expectedType++;
            if ((int)sourceEffectiveType != expectedType || dynamicPower != Bits(1) * 40 / 63 + 30)
                throw new NotSupportedException("called_damage_hidden_power_context");
        }
        ElementType Type(string symbol) => Enum.Parse<ElementType>(symbol[5..], true);
        var type = sourceEffectiveType ?? (struggle ? ElementType.Normal : Type(move.Type));
        var category = TypeRules.Category(type);
        if (move.Effect == "EFFECT_LEVEL_DAMAGE")
        {
            var species = WildBattleEffective.Types(catalog, target);
            bool levelImmune = TypeRules.OrderedEffects(type, Type(species.Type1), Type(species.Type2)).Any(e => e.MultiplierTenths == 0);
            return new(levelImmune ? 0 : user.Pokemon.Level, category, [new("level-damage", levelImmune ? 0 : user.Pokemon.Level)]);
        }
        if (move.Effect == "EFFECT_SONICBOOM")
        {
            var species = WildBattleEffective.Types(catalog, target);
            bool sonicImmune = TypeRules.OrderedEffects(type, Type(species.Type1), Type(species.Type2)).Any(e => e.MultiplierTenths == 0);
            return new(sonicImmune ? 0 : 20, category, [new("sonicboom-fixed-damage", sonicImmune ? 0 : 20)]);
        }
        if (move.Effect == "EFFECT_DRAGON_RAGE") return new(40, category, [new("fixed-damage", 40)]);
        if (move.Effect == "EFFECT_MAGNITUDE" && dynamicPower is null) throw new NotSupportedException("mtmoon_magnitude_power_required");
        int power = struggle ? 50 : move.Effect == "EFFECT_LOW_KICK"
            ? SourceDynamicPowerRules.Weight(mechanics.SpeciesMechanics[target.Pokemon.Species].WeightHectograms) : move.Power;
        if (move.Effect == "EFFECT_FLAIL")
        {
            int fraction = user.Pokemon.CurrentHp * 48 / user.Pokemon.Stats.MaxHp;
            power = fraction <= 1 ? 200 : fraction <= 4 ? 150 : fraction <= 9 ? 100 : fraction <= 16 ? 80 : fraction <= 32 ? 40 : 20;
        }
        if (move.Effect == "EFFECT_ROLLOUT" && dynamicPower is null)
            power *= (1 << (user.RolloutTurnsRemaining == 0 ? 0 : 5 - user.RolloutTurnsRemaining)) * (user.DefenseCurled ? 2 : 1);
        if (move.Effect == "EFFECT_FURY_CUTTER" && dynamicPower is null) power = WildBattleRules.NextFuryCutterPower(user.FuryCutterCounter);
        if (dynamicPower is { } overridden)
        {
            bool sourcePower = move.Effect switch {
                "EFFECT_RETURN" => overridden == Math.Max(1, user.Pokemon.Friendship * 10 / 25),
                "EFFECT_FRUSTRATION" => overridden == Math.Max(1, (255 - user.Pokemon.Friendship) * 10 / 25),
                "EFFECT_HIDDEN_POWER" => overridden is >= 30 and <= 70,
                "EFFECT_ERUPTION" => sourceDoubleBattle ? overridden >= 1 && overridden <= move.Power : overridden == Math.Max(1, user.Pokemon.CurrentHp * move.Power / user.Pokemon.Stats.MaxHp),
                "EFFECT_FLAIL" => overridden == SourceDynamicPowerRules.Flail(user.Pokemon.CurrentHp, user.Pokemon.Stats.MaxHp),
                "EFFECT_LOW_KICK" => overridden == SourceDynamicPowerRules.Weight(mechanics.SpeciesMechanics[target.Pokemon.Species].WeightHectograms),
                "EFFECT_PRESENT" => overridden is 40 or 80 or 120,
                "EFFECT_TRIPLE_KICK" => overridden is 10 or 20 or 30,
                "EFFECT_ROLLOUT" => overridden is 30 or 60 or 120 or 240 or 480 or 960,
                _ => false
            };
            if (!sourcePower && !(moveId.Value == "pret:MOVE_FURY_CUTTER" && overridden is 10 or 20 or 40 or 80 or 160) && !(moveId.Value == "pret:MOVE_MAGNITUDE" && overridden is 10 or 30 or 50 or 70 or 90 or 110 or 150) && (moveId.Value != "pret:MOVE_ROLLOUT" || overridden is < 30 or > 960)) throw new NotSupportedException("route3_dynamic_power");
            power = overridden;
        }
        if (pursuitSwitch && (moveId.Value != "pret:MOVE_PURSUIT"))
            throw new NotSupportedException("route22_pursuit_switch_move");
        var steps = ImmutableArray.CreateBuilder<DamageStep>();
        int value = CalculateBase(catalog, user, target, power, type, move.Effect, move.Target,
            new(Critical: critical, Rain: rain, Reflect: reflect, Sun: sun, LightScreen: lightScreen,
                WeatherSuppressed: weatherSuppressed, SolarBeamWeakWeather: sourceSolarBeamWeakWeather,
                DoubleBattle: sourceDoubleBattle, DefenderCount: sourceDefenderCount,
                PlusMinusBoost: sourcePlusMinusBoost, MudSport: sourceMudSport, WaterSport: sourceWaterSport), out int sourceMovePower, steps);
        if (critical) value *= 2;
        steps.Add(new("critical", value));
        if (sourceDamageMultiplier == 1)
            sourceDamageMultiplier = SourceChargeRules.DamageMultiplier(WildBattleRules.CalledSemiInvulnerable(target), move);
        if (sourceDamageMultiplier != 1) { value = checked(value * sourceDamageMultiplier); steps.Add(new("source-damage-multiplier", value)); }
        if (move.Effect == "EFFECT_REVENGE" && user.DamagedByOpponentThisTurn) { value *= 2; steps.Add(new("revenge", value)); }
        if (user.ChargeTurnsRemaining > 0 && (move.Type == "TYPE_ELECTRIC")) { value *= 2; steps.Add(new("charge", value)); }
        // Cmd_damagecalc: after base damage, critical/script multiplier and Charge; before typecalc/STAB and random.
        if (sourceHelpingHand) { value = checked(value * 15) / 10; steps.Add(new("helping-hand", value)); }
        if (move.Effect == "EFFECT_FLINCH_MINIMIZE_HIT" && target.Minimized) { value *= 2; steps.Add(new("minimized", value)); }
        if (pursuitSwitch)
        {
            value *= 2;
            steps.Add(new("pursuit-switch", value));
        }
        var a = WildBattleEffective.Types(catalog, user); var d = WildBattleEffective.Types(catalog, target);
        if (!struggle && (type == Type(a.Type1) || type == Type(a.Type2))) value = value * 15 / 10;
        steps.Add(new("stab", value));
        bool immune = !struggle && type == ElementType.Ground && WildBattleEffective.Ability(target) == "ABILITY_LEVITATE";
        if (!struggle)
            foreach (var effect in TypeRules.OrderedEffects(type, Type(d.Type1), Type(d.Type2)))
            {
                if (target.Identified && effect.Defense == ElementType.Ghost && type is ElementType.Normal or ElementType.Fighting) continue;
                value = value * effect.MultiplierTenths / 10;
                if (effect.MultiplierTenths == 0) immune = true;
                else if (value == 0) value = 1;
                steps.Add(new($"type-{effect.Defense}", value));
            }
        if (immune) value = 0;
        value = value == 0 ? 0 : Math.Max(1, value * randomPercent / 100);
        steps.Add(new("random-percent", value));
        return new(value, category, steps.ToImmutable()) { SourceMovePower = sourceMovePower };
    }

    // pokemon.c CalculateBaseDamage: no variable-power preprocessing, TypeCalc, randomness,
    // critical multiplier or script multipliers. AI deliberately supplies base move power and
    // keeps its own TypeCalc semantics; execution supplies its already-resolved power and type.
    internal static int CalculateBase(PokemonCatalog catalog, WildBattleCombatant user,
        WildBattleCombatant target, int power, ElementType type, string? effect, string? moveTarget,
        SourceBaseDamageContext context, ImmutableArray<DamageStep>.Builder? steps = null) =>
        CalculateBase(catalog, user, target, power, type, effect, moveTarget, context, out _, steps);

    private static int CalculateBase(PokemonCatalog catalog, WildBattleCombatant user,
        WildBattleCombatant target, int power, ElementType type, string? effect, string? moveTarget,
        SourceBaseDamageContext context, out int sourceMovePower, ImmutableArray<DamageStep>.Builder? steps)
    {
        bool physical = TypeRules.Category(type) == DamageCategory.Physical;
        bool critical = context.Critical, reflect = context.Reflect, lightScreen = context.LightScreen;
        bool rain = context.Rain, sun = context.Sun, weatherSuppressed = context.WeatherSuppressed;
        bool sourceDoubleBattle = context.DoubleBattle, sourcePlusMinusBoost = context.PlusMinusBoost;
        int sourceDefenderCount = context.DefenderCount;
        bool sourceSolarBeamWeakWeather = context.SolarBeamWeakWeather;
        bool sourceMudSport = context.MudSport, sourceWaterSport = context.WaterSport;
        int attack = physical ? user.Pokemon.Stats.Attack : user.Pokemon.Stats.SpecialAttack;
        int defense = physical ? target.Pokemon.Stats.Defense : target.Pokemon.Stats.SpecialDefense;
        string userAbility = context.UserAbility ?? WildBattleEffective.Ability(user);
        // Ability arithmetic is shared by execution and AI_CalcDmg.
        if (physical && userAbility is "ABILITY_HUGE_POWER" or "ABILITY_PURE_POWER")
        {
            // pokemon.c:2437 precedes the attack badge's independent integer division.
            attack *= 2;
            steps?.Add(new("huge-pure-power", attack));
        }
        if (physical && user.AttackBadgeBoost)
        {
            // pokemon.c:2440 precedes Guts (2490) and stat stages; preserve both integer divisions.
            attack = attack * 110 / 100;
            steps?.Add(new("attack-badge", attack));
        }
        if (physical && target.DefenseBadgeBoost)
        {
            // pokemon.c:2442 applies the player's fifth badge before held items, abilities,
            // Explosion's defense halving and stat stages. Keep its own integer truncation.
            defense = defense * 110 / 100;
            steps?.Add(new("defense-badge", defense));
        }
        // pokemon.c:2444-2447 applies each special badge multiplication before held effects.
        if (!physical && user.SpecialBadgeBoost)
        { attack = attack * 110 / 100; steps?.Add(new("special-attack-badge", attack)); }
        if (!physical && target.SpecialBadgeBoost)
        { defense = defense * 110 / 100; steps?.Add(new("special-defense-badge", defense)); }
        if (user.HeldItem is not ("ITEM_HARD_STONE" or "ITEM_BLACK_GLASSES" or "ITEM_SPELL_TAG"))
        {
            int held = SourceHeldItemModifiers.TypeAttack(catalog, user, type, attack);
            if (held != attack) { attack = held; steps?.Add(new("held-type", attack)); }
        }
        if (user.HeldItem == "ITEM_HARD_STONE" && type == ElementType.Rock) { attack = attack * 110 / 100; steps?.Add(new("hard-stone", attack)); }
        if (user.HeldItem == "ITEM_BLACK_GLASSES" && type == ElementType.Dark) { attack = attack * 110 / 100; steps?.Add(new("black-glasses", attack)); }
        if (user.HeldItem == "ITEM_SPELL_TAG" && type == ElementType.Ghost) { attack = attack * 110 / 100; steps?.Add(new("spell-tag", attack)); }
        if (physical && user.HeldItem == "ITEM_CHOICE_BAND")
        { attack = SourceBattleHeldItems.PhysicalAttack(attack, user.HeldItem); steps?.Add(new("choice-band", attack)); }
        if (!physical)
        {
            int held = SourceHeldItemModifiers.SpecialAttack(catalog, user, attack);
            if (held != attack) { attack = held; steps?.Add(new("held-species-special-attack", attack)); }
            held = SourceHeldItemModifiers.SpecialDefense(catalog, target, defense);
            if (held != defense) { defense = held; steps?.Add(new("held-species-special-defense", defense)); }
        }
        if (physical && user.HeldItem == "ITEM_THICK_CLUB" && user.Pokemon.SourceSpeciesId is 104 or 105) { attack *= 2; steps?.Add(new("thick-club", attack)); }
        if (WildBattleEffective.Ability(target) == "ABILITY_THICK_FAT" && type is ElementType.Fire or ElementType.Ice)
        { attack /= 2; steps?.Add(new("thick-fat",attack)); }
        if (physical && target.HeldItem == "ITEM_METAL_POWDER" && target.Pokemon.SourceSpeciesId == 132)
        { defense *= 2; steps?.Add(new("metal-powder", defense)); }
        if (physical && userAbility == "ABILITY_HUSTLE")
        { attack = attack * 150 / 100; steps?.Add(new("hustle", attack)); }
        if (sourcePlusMinusBoost && !physical)
        { attack = attack * 150 / 100; steps?.Add(new("plus-minus", attack)); }
        if (physical && userAbility == "ABILITY_GUTS" && user.Status != 0)
            attack = attack * 150 / 100;
        if (physical && WildBattleEffective.Ability(target) == "ABILITY_MARVEL_SCALE" && target.Status != 0)
        { defense = defense * 150 / 100; steps?.Add(new("marvel-scale", defense)); }
        if (type == ElementType.Fire && (sourceWaterSport || (user.WaterSport || target.WaterSport))) power /= 2;
        if (type == ElementType.Electric && (sourceMudSport || (user.MudSport || target.MudSport))) power /= 2;
        if (effect == "EFFECT_EXPLOSION") defense = Math.Max(1, defense / 2);
        if (user.Pokemon.CurrentHp <= user.Pokemon.Stats.MaxHp / 3 &&
            (type, userAbility) is (ElementType.Grass, "ABILITY_OVERGROW") or
                (ElementType.Fire, "ABILITY_BLAZE") or (ElementType.Water, "ABILITY_TORRENT") or
                (ElementType.Bug, "ABILITY_SWARM")) power = power * 150 / 100;
        sourceMovePower = power;
        // Critical hits ignore unfavorable attack stages and favorable defense stages.
        int attackStage = physical ? user.AttackStage : 6 + user.SpecialAttackStageDelta, defenseStage = physical ? target.DefenseStage : 6 + target.SpecialDefenseStageDelta;
        int Stage(int basis, int stage) => stage < 6 ? basis * 2 / (8 - stage) : basis * (stage - 4) / 2;
        attack = Math.Max(1, Stage(attack, critical && attackStage <= 6 ? 6 : attackStage));
        defense = Math.Max(1, Stage(defense, critical && defenseStage >= 6 ? 6 : defenseStage));
        int value = checked(attack * power);
        steps?.Add(new("attack-times-power", value));
        value = checked(value * (2 * user.Pokemon.Level / 5 + 2));
        steps?.Add(new("times-level-factor", value));
        value /= defense; steps?.Add(new("divide-defense", value));
        value /= 50; steps?.Add(new("divide-50", value));
        if (physical && user.Status == WildBattleStatus.Burn && userAbility != "ABILITY_GUTS") value /= 2;
        steps?.Add(new("burn", value));
        bool twoDefenders = sourceDoubleBattle && sourceDefenderCount == 2;
        bool spread = twoDefenders && moveTarget == "MOVE_TARGET_BOTH";
        if (reflect && physical && !critical)
        { value = twoDefenders ? 2 * (value / 3) : value / 2; steps?.Add(new("reflect", value)); }
        if (physical && spread) { value /= 2; steps?.Add(new("source-both-targets", value)); }
        if (physical) value = Math.Max(1, value);
        steps?.Add(new("physical-minimum-before-plus-two", value));
        if (lightScreen && !physical && !critical)
        { value = twoDefenders ? 2 * (value / 3) : value / 2; steps?.Add(new("light-screen", value)); }
        if (!physical && spread) { value /= 2; steps?.Add(new("source-both-targets", value)); }
        if (rain && !physical && !weatherSuppressed)
        {
            if (type == ElementType.Fire) value /= 2;
            else if (type == ElementType.Water) value = value * 15 / 10;
            steps?.Add(new("rain", value));
        }
        if (sourceSolarBeamWeakWeather && !weatherSuppressed) { value /= 2; steps?.Add(new("solar-beam-weather", value)); }
        if (sun && !physical && !weatherSuppressed)
        {
            if (type == ElementType.Fire) value = value * 15 / 10;
            else if (type == ElementType.Water) value /= 2;
            steps?.Add(new("sun", value));
        }
        if (user.FlashFireBoost && type == ElementType.Fire) { value = value * 15 / 10; steps?.Add(new("flash-fire", value)); }
        value += 2; steps?.Add(new("plus-two", value));
        return value;
    }

}


/// <summary>Pure CalculateBaseDamage inputs, separated from action execution and AI knowledge.</summary>
internal readonly record struct SourceBaseDamageContext(bool Critical = false, bool Rain = false,
    bool Reflect = false, bool Sun = false, bool LightScreen = false, bool WeatherSuppressed = false,
    bool SolarBeamWeakWeather = false, bool DoubleBattle = false, int DefenderCount = 1,
    bool PlusMinusBoost = false, bool MudSport = false, bool WaterSport = false, string? UserAbility = null);

using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal static class SourceDynamicTypeRules
{
    // Cmd_hiddenpowercalc: HP, Attack, Defense, Speed, Sp. Attack, Sp. Defense.
    // Neither command consumes RNG. Hidden Power skips Normal and the mystery type.
    internal static (int Power, ElementType Type) HiddenPower(SixStats iv)
    {
        int Bits(int bit) => ((iv.Hp >> bit) & 1) | (((iv.Attack >> bit) & 1) << 1) |
            (((iv.Defense >> bit) & 1) << 2) | (((iv.Speed >> bit) & 1) << 3) |
            (((iv.SpecialAttack >> bit) & 1) << 4) | (((iv.SpecialDefense >> bit) & 1) << 5);
        int type = 15 * Bits(0) / 63 + 1;
        if (type >= (int)ElementType.Mystery) type++;
        return (40 * Bits(1) / 63 + 30, (ElementType)type);
    }
    internal static (ElementType Type, int Multiplier) WeatherBall(SourceFieldRegisters weather, bool suppressed) =>
        WildBattleSourceFieldEffects.WeatherBall(SourceWeatherRules.HasRain(weather), SourceWeatherRules.HasSandstorm(weather),
            SourceWeatherRules.HasSun(weather), weather.HailTurnsRemaining > 0, suppressed);
    internal static string Symbol(ElementType type) => "TYPE_" + type.ToString().ToUpperInvariant();
}

internal sealed partial class FrameRecipeContext
{
    internal static bool HasDynamicTypeRecipe(string effect) => effect is "EFFECT_HIDDEN_POWER" or "EFFECT_WEATHER_BALL";
    private string? dynamicHitType;
    private int dynamicTypeMultiplier = 1;
    internal void SetDynamicHitType(string? type) => dynamicHitType = type;
    internal string DynamicHitType(PokemonMoveDefinition move) => dynamicHitType ?? move.Type;

    // Weather Ball executes setweatherballtype before attackcanceler; Hidden Power's
    // hiddenpowercalc follows it. Preparation also clears a previous called move's type.
    internal void PrepareDynamicType(PokemonMoveDefinition move)
    {
        SetDynamicHitType(null); dynamicTypeMultiplier = 1;
        if (move.Effect != "EFFECT_WEATHER_BALL") return;
        var value = SourceDynamicTypeRules.WeatherBall(SettingWeather, SourceCalledWeatherSuppressed);
        SetDynamicHitType(SourceDynamicTypeRules.Symbol(value.Type));
        dynamicTypeMultiplier = value.Multiplier;
    }
    internal bool DynamicTypeAbsorb(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string type) =>
        Route11WaterAbsorb(actor, target, move with { Type = type });
    internal void DynamicTypeReport(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string type)
    {
        if (SourceTypeReport(catalog, actor, target, move, type)) return;
        SourceDamageResultMessage(actor, target, move);
        var result = DynamicTypeResult(catalog, target, type);
        if (result.Immune && type == "TYPE_GROUND" && WildBattleEffective.Ability(Get(target)) == "ABILITY_LEVITATE")
        {
            RecordAbility(target, "ABILITY_LEVITATE");
            Add(n => new SourceAbilityPreventedEvent(n, Turn, actor, target, move.Id, "ABILITY_LEVITATE"));
        }
        if (result.Effectiveness is { } effect)
            Add(n => new SourceEffectivenessEvent(n, Turn, actor, target, move.Id, effect));
    }
    private (bool Immune, WildBattleEffectiveness? Effectiveness) DynamicTypeResult(PokemonCatalog catalog, BattlerSlot target, string symbol)
    {
        var result = SourceTypeResult(catalog, target, catalog.Moves[Invocation!.CurrentMove], symbol);
        return (result.Immune, result.Effectiveness);
    }

    internal void ExecuteDynamicType(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, int slot,
        PokemonMoveDefinition move, string purpose)
    {
        if (!HasDynamicTypeRecipe(move.Effect) || !SourceMoveDefinitionRules.IsValid(move))
            throw new SourceRecipeUnsupported("source_dynamic_type_definition");
        int? power = null;
        if (move.Effect == "EFFECT_HIDDEN_POWER")
        {
            var value = SourceDynamicTypeRules.HiddenPower(Get(actor).Pokemon.Ivs);
            power = value.Power; SetDynamicHitType(SourceDynamicTypeRules.Symbol(value.Type));
        }
        string type = DynamicHitType(move);
        // Accuracy/abilities use dynamic type; damage receives the authenticated original
        // definition plus its separate source register. Never mutate permanent move metadata.
        var effectiveMove = move with { Type = type };
        bool hit = FixedDamageAccuracy(actor, target, effectiveMove, purpose);
        SpendEarlyPartyPp(actor, target, slot, move);
        if (!hit) { SourceGenericMiss(actor, target, move); return; }
        if (DynamicTypeAbsorb(actor, target, move, type) || SsAnneFlashFire(actor, target, effectiveMove)) return;
        bool critical = SourceGenericCritical(Get(actor), Get(target), move, purpose);
        int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
        int raw = SourceGenericDamage(catalog, Get(actor), Get(target), target, move, critical, percent, power,
            FixedDamageElementType(type), dynamicTypeMultiplier);
        int adjusted = FixedDamageAdjust(target, move, raw, purpose);
        bool immune = DynamicTypeResult(catalog, target, type).Immune;
        int lost = FixedDamageApply(actor, target, move, adjusted, critical, percent, immune);
        if (critical && lost == 0 && !immune && !FixedDamageZeroCriticalPrinted(target))
            Add(n => new SourceCriticalHitEvent(n, Turn, actor, target));
        DynamicTypeReport(catalog, actor, target, move, type);
        SourceGenericSecondary(catalog, actor, target, move, purpose, immune ? 0 : raw);
        if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
        if (lost > 0) MtMoonRageOnDamage(actor, target, move.Id);
        SourceGenericContact(catalog, actor, target, move, purpose, lost);
        ForestOutcome(true);
    }
}

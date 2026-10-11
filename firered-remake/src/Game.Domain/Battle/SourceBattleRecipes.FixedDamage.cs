using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasFixedDamageRecipe(string effect) => effect is
        "EFFECT_FALSE_SWIPE" or "EFFECT_DRAGON_RAGE" or "EFFECT_SONICBOOM" or "EFFECT_LEVEL_DAMAGE" or
        "EFFECT_PSYWAVE" or "EFFECT_SUPER_FANG" or "EFFECT_ENDEAVOR" or "EFFECT_OHKO";

    // One command owner for canonical singles and real four-position execution. Historical
    // profiles keep their pre-existing adapters; fixed damage does not imply a normal hit script.
    internal void ExecuteFixedDamage(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, int slot,
        PokemonMoveDefinition move, string purpose)
    {
        if (!HasFixedDamageRecipe(move.Effect) || !MetronomeSourceMoveDefinition.Matches(move))
            throw new SourceRecipeUnsupported("source_fixed_damage_definition");
        if (move.Effect == "EFFECT_OHKO") { ExecuteOneHitKo(catalog, actor, target, slot, move, purpose); return; }
        bool endeavor = move.Effect == "EFFECT_ENDEAVOR", fang = move.Effect == "EFFECT_SUPER_FANG";
        bool swipe = move.Effect == "EFFECT_FALSE_SWIPE";
        int damage = 0;
        if (endeavor)
        {
            // EffectEndeavor spends PP and branches on HP before accuracy and typecalc.
            SpendEarlyPartyPp(actor, target, slot, move);
            damage = Get(target).Pokemon.CurrentHp - Get(actor).Pokemon.CurrentHp;
            if (damage <= 0)
            {
                Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move.Id, "endeavor-hp-not-lower"));
                return;
            }
        }
        bool hit = FixedDamageAccuracy(actor, target, move, purpose);
        if (!endeavor) SpendEarlyPartyPp(actor, target, slot, move);
        if (!hit) { SourceGenericMiss(actor, target, move); return; }
        bool critical = false; int percent = 100;
        var typeResult = FixedDamageType(catalog, target, move);
        if (swipe)
        {
            critical = SourceGenericCritical(Get(actor), Get(target), move, purpose);
            percent = 100 - Draw($"{purpose}.damage", 16) % 16;
            damage = SourceGenericDamage(catalog, Get(actor), Get(target), target, move, critical, percent);
        }
        else if (move.Effect == "EFFECT_PSYWAVE")
        {
            // Original FireRed Cmd_psywavedamageeffect: reject 11..15, then truncate.
            // This deliberately permits level-one zero damage and never adds a minimum.
            int roll; do { roll = Draw($"{purpose}.psywave", 16) % 16; } while (roll > 10);
            damage = Get(actor).Pokemon.Level * (roll * 10 + 50) / 100;
        }
        else if (fang) damage = Math.Max(1, Get(target).Pokemon.CurrentHp / 2);
        else if (!endeavor) damage = move.Effect switch {
            "EFFECT_DRAGON_RAGE" => 40, "EFFECT_SONICBOOM" => 20, _ => Get(actor).Pokemon.Level };

        int rawDamage = damage;
        // Super Fang never executes adjustsetdamage: no Focus Band draw or Endure clamp.
        // Endeavor skips that command on immunity; other fixed scripts execute it anyway.
        if (!fang && !(endeavor && typeResult.Immune))
            damage = FixedDamageAdjust(target, move, damage, purpose);
        int lost = FixedDamageApply(actor, target, move, damage, critical, percent, typeResult.Immune);
        if (critical && lost == 0 && !typeResult.Immune && !FixedDamageZeroCriticalPrinted(target))
            Add(n => new SourceCriticalHitEvent(n, Turn, actor, target));
        SourceDamageResultMessage(actor, target, move);
        if (typeResult.Effectiveness is { } effectiveness && (swipe || typeResult.Immune))
            Add(n => new SourceEffectivenessEvent(n, Turn, actor, target, move.Id, effectiveness));
        // Every one of these scripts reaches the ordinary zero-chance secondary command.
        // The previous canonical Super Fang adapter omitted this source RNG draw.
        SourceGenericSecondary(catalog, actor, target, move, purpose, typeResult.Immune ? 0 : rawDamage);
        if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
        if (lost > 0) MtMoonRageOnDamage(actor, target, move.Id);
        SourceGenericContact(catalog, actor, target, move, purpose, lost);
        ForestOutcome(true);
    }

    internal virtual int FixedDamageAdjust(BattlerSlot target, PokemonMoveDefinition move, int damage, string purpose) =>
        AdjustHeldDamage(target, move.Id, damage, purpose, falseSwipe: move.Effect == "EFFECT_FALSE_SWIPE");
    internal virtual int FixedDamageApply(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move,
        int damage, bool critical, int percent, bool immune) =>
        SourceGenericApplyDamage(actor, target, move, immune ? 0 : damage, critical, percent);
    internal virtual bool FixedDamageZeroCriticalPrinted(BattlerSlot target) => false;

    private bool FixedDamageAccuracy(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose)
    {
        var user = Get(actor); var defender = Get(target);
        if (defender.Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED")) return false;
        if (CalledSureHit(actor, target)) return true;
        if (BillUnderground(actor, target, move)) return false;
        int accuracy = WildBattleDamage.Accuracy(move.Accuracy, user.AccuracyStage, defender.Identified ? 6 : defender.EvasionStage);
        if (WildBattleEffective.Ability(user) == "ABILITY_COMPOUND_EYES") accuracy = accuracy * 130 / 100;
        if (!SourceCalledWeatherSuppressed && StruggleSandstorm && WildBattleEffective.Ability(defender) == "ABILITY_SAND_VEIL") accuracy = accuracy * 80 / 100;
        if (WildBattleEffective.Ability(user) == "ABILITY_HUSTLE" && TypeRules.Category(FixedDamageElementType(move.Type)) == DamageCategory.Physical) accuracy = accuracy * 80 / 100;
        if (defender.HeldItem == "ITEM_BRIGHT_POWDER") accuracy = accuracy * 90 / 100;
        if (defender.HeldItem == "ITEM_LAX_INCENSE") accuracy = accuracy * 95 / 100;
        return Draw($"{purpose}.accuracy", 100) % 100 + 1 <= accuracy;
    }

    private (bool Immune, WildBattleEffectiveness? Effectiveness) FixedDamageType(PokemonCatalog catalog,
        BattlerSlot target, PokemonMoveDefinition move)
    {
        var defender = Get(target); var types = WildBattleEffective.Types(catalog, defender);
        var type = FixedDamageElementType(move.Type); int multiplier = 100;
        foreach (var row in TypeRules.OrderedEffects(type, FixedDamageElementType(types.Type1), FixedDamageElementType(types.Type2)))
        {
            if (defender.Identified && row.Defense == ElementType.Ghost && type is ElementType.Normal or ElementType.Fighting) continue;
            multiplier = multiplier * row.MultiplierTenths / 10;
        }
        bool immune = multiplier == 0 || WildBattleEffective.Ability(defender) == "ABILITY_WONDER_GUARD" && multiplier <= 100;
        return (immune, immune ? WildBattleEffectiveness.NoEffect : multiplier < 100 ? WildBattleEffectiveness.NotVeryEffective :
            multiplier > 100 ? WildBattleEffectiveness.SuperEffective : null);
    }
    private static ElementType FixedDamageElementType(string symbol) => Enum.Parse<ElementType>(symbol[5..], true);
}

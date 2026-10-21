using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal bool CanonicalSecondarySemantics => true;

    internal SourceSecondaryEffectSelection SecondaryEffect(BattlerSlot actor, PokemonMoveDefinition move,
        string purpose, bool noEffect, bool certain = false, bool hasEffect = true, bool strictTwineedle = false)
    {
        bool serene = WildBattleEffective.Ability(Get(actor)) == "ABILITY_SERENE_GRACE" &&
            (!strictTwineedle || CanonicalSecondarySemantics);
        var result = SourceSecondaryEffectRules.Select(move.SecondaryEffectChance, serene,
            (byte)((hasEffect ? 1 : 0) | (certain ? SourceSecondaryEffectRules.CertainFlag : 0)), noEffect,
            () => Draw($"{purpose}.secondary-effect", 100), strictTwineedle);
        return result with { Certain = result.Certain && CanonicalSecondarySemantics };
    }

    // CERTAIN affects the source prevention message, not Shield Dust, Substitute,
    // Safeguard or the status immunity itself. These guards precede existing status.
    internal bool SecondaryStatusPrevention(BattlerSlot target, uint status, bool certain)
    {
        if (!certain) return false;
        var body = Get(target);
        if (body.Pokemon.CurrentHp == 0 || SourceCalledSecondaryBlocked(target) ||
            WildBattleEffective.Ability(body) == "ABILITY_SHIELD_DUST" || Safeguard(target)) return false;
        string ability = WildBattleEffective.Ability(body);
        bool prevents = status is WildBattleStatus.Poison or WildBattleSourceFieldEffects.ToxicPoison && ability == "ABILITY_IMMUNITY" ||
            status == WildBattleStatus.Burn && ability == "ABILITY_WATER_VEIL" ||
            status == WildBattleStatus.Paralysis && ability == "ABILITY_LIMBER";
        if (!prevents) return false;
        ReportCertainSecondaryPrevention(target, ability);
        return true;
    }

    private void ReportCertainSecondaryPrevention(BattlerSlot target, string ability)
    {
        RecordAbility(target, ability);
        var invocation = Invocation!;
        Add(n => new SourceAbilityPreventedEvent(n, Turn, invocation.Actor, target, invocation.CurrentMove, ability));
    }

    internal void ApplySecondaryFlinch(BattlerSlot target, bool certain)
    {
        var body = Get(target);
        if (certain && body.Pokemon.CurrentHp > 0 && !SourceCalledSecondaryBlocked(target) &&
            WildBattleEffective.Ability(body) == "ABILITY_INNER_FOCUS")
        { ReportCertainSecondaryPrevention(target, "ABILITY_INNER_FOCUS"); return; }
        ApplyFlinch(target);
    }
}

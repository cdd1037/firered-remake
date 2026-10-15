using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasRetaliationRecipe(string effect) => effect is
        "EFFECT_COUNTER" or "EFFECT_MIRROR_COAT" or "EFFECT_VITAL_THROW";
    internal abstract SourceDamageRegisters? RetaliationReceipt(BattlerSlot actor);
    internal abstract void RetaliationTarget(BattlerSlot target);
    internal abstract void RetaliationSuppressPressure();
    internal virtual BattlerSlot? RetaliationFollowMe(BattleSide side) => null;

    internal void ExecuteRetaliation(PokemonCatalog catalog, BattlerSlot actor, int slot,
        PokemonMoveDefinition move, string purpose, ref bool handled)
    {
        if (!HasRetaliationRecipe(move.Effect)) return;
        if (Invocation is null || !SourceMoveDefinitionRules.IsValid(move))
            throw new SourceRecipeUnsupported("source_retaliation_definition");
        handled = true;
        // Vital Throw is the ordinary EffectHit script with an accuracy-helper bypass.
        if (move.Effect == "EFFECT_VITAL_THROW")
        { SourceGenericHit(catalog, actor, Invocation.Target, slot, move, purpose); return; }
        var receipt = RetaliationReceipt(actor);
        if (receipt?.Turn != Turn) receipt = null;
        bool physical = move.Effect == "EFFECT_COUNTER";
        int damage = physical ? receipt?.PhysicalDamage ?? 0 : receipt?.SpecialDamage ?? 0;
        var source = physical ? receipt?.PhysicalSource : receipt?.SpecialSource;
        // The source stores a battler position, not an occupant token. Switching and fainting
        // preserve this receipt until TurnValuesCleanUp; check the position's current HP.
        if (damage == 0 || source is not { } attacker || attacker.Side == actor.Side || Get(attacker).Pokemon.CurrentHp == 0)
        {
            RetaliationSuppressPressure();
            SpendEarlyPartyPp(actor, Invocation.Target, slot, move);
            Add(n => new SourceMoveFailedEvent(n, Turn, actor, Invocation.Target, move.Id,
                physical ? "counter-no-physical-damage" : "mirror-coat-no-special-damage"));
            return;
        }
        var target = RetaliationFollowMe(attacker.Side) is { } follow && Get(follow).Pokemon.CurrentHp > 0 ? follow : attacker;
        RetaliationTarget(target);
        bool hit = FixedDamageAccuracy(actor, target, move, purpose);
        SpendEarlyPartyPp(actor, target, slot, move);
        if (!hit) { SourceGenericMiss(actor, target, move); return; }
        // typecalc2 retains immunity alone; no STAB, resistance or weakness multiplier.
        bool immune = SourceTypeResult(catalog, target, move, command: SourceTypeCommand.TypeCalc2, apply: true).Immune;
        int adjusted = FixedDamageAdjust(target, move, checked(damage * 2), purpose);
        int lost = FixedDamageApply(actor, target, move, adjusted, false, 100, immune);
        SourceDamageResultMessage(actor, target, move);
        bool guardReported = SourceTypeReport(catalog, actor, target, move, command: SourceTypeCommand.TypeCalc2);
        if (immune && !guardReported) Add(n => new SourceEffectivenessEvent(n, Turn, actor, target, move.Id, WildBattleEffectiveness.NoEffect));
        SourceGenericSecondary(catalog, actor, target, move, purpose, immune ? 0 : adjusted);
        if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
        if (lost > 0) MtMoonRageOnDamage(actor, target, move.Id);
        SourceGenericContact(catalog, actor, target, move, purpose, lost);
        ForestOutcome(true);
    }
}

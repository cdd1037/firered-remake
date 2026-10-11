using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasCriticalStatusHitRecipe(string effect) => effect is
        "EFFECT_BLAZE_KICK" or "EFFECT_POISON_TAIL" or "EFFECT_POISON_FANG";

    // All three source scripts set their status effect byte then enter EffectHit.
    // The shared hit commands retain each storage owner's scalar command hooks.
    internal void ExecuteCriticalStatusHit(PokemonCatalog catalog, BattlerSlot actor,
        BattlerSlot target, int slot, PokemonMoveDefinition move, string purpose) =>
        ExecuteStatusHit(catalog, actor, target, slot, move, purpose);

    internal abstract void MarkSecondaryNoEffect();

    internal void ApplyToxicSecondary(PokemonCatalog catalog, BattlerSlot target, bool certain = false)
    {
        if (SecondaryStatusPrevention(target, WildBattleSourceFieldEffects.ToxicPoison, certain)) return;
        var body = Get(target);
        if (body.Pokemon.CurrentHp == 0 || body.Status != 0 || SourceCalledSecondaryBlocked(target) ||
            WildBattleEffective.Ability(body) == "ABILITY_SHIELD_DUST" || Safeguard(target)) return;
        var types = WildBattleEffective.Types(catalog, body);
        if (types.Type1 is "TYPE_POISON" or "TYPE_STEEL" || types.Type2 is "TYPE_POISON" or "TYPE_STEEL")
        {
            // SetMoveEffect's toxic branch alone sets DOESNT_AFFECT_FOE here,
            // after resultmessage. Retain it for contact/item/history commands.
            MarkSecondaryNoEffect();
            var invocation = Invocation!;
            Add(n => new SourceEffectivenessEvent(n, Turn, invocation.Actor, target,
                invocation.CurrentMove, WildBattleEffectiveness.NoEffect));
            return;
        }
        if (WildBattleEffective.Ability(body) == "ABILITY_IMMUNITY") return;
        PreparePrimaryToxicStorage();
        ChangeStatus(target, WildBattleSourceFieldEffects.ToxicPoison, "move");
        // Source Synchronize deliberately downgrades the reflected toxic effect.
        SynchronizeStatus(catalog, target, WildBattleStatus.Poison);
    }
}

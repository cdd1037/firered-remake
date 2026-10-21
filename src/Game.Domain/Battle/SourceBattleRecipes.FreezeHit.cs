using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal static bool HasFreezeHitRecipe(string effect) => effect == "EFFECT_FREEZE_HIT";

    // BattleScript_EffectFreezeHit sets MOVE_EFFECT_FREEZE then enters the ordinary
    // hit script with canonical frame storage.
    internal void ExecuteFreezeHit(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        int slot, PokemonMoveDefinition move, string purpose)
        => ExecuteStatusHit(catalog, actor, target, slot, move, purpose);

    internal void ExecuteTriAttack(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        int slot, PokemonMoveDefinition move, string purpose)
        => ExecuteStatusHit(catalog, actor, target, slot, move, purpose);

    private void ExecuteStatusHit(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        int slot, PokemonMoveDefinition move, string purpose)
    {
        bool hit = FreezeHitAccuracy(actor, target, move, purpose);
        SpendEarlyPartyPp(actor, target, slot, move);
        if (!hit) { SourceGenericMiss(actor, target, move); return; }
        if (move.Type == "TYPE_FIRE" && SsAnneFlashFire(actor, target, move)) return;
        bool critical = SourceGenericCritical(Get(actor), Get(target), move, purpose);
        int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
        int damage = FreezeHitDamage(catalog, actor, target, move, critical, percent);
        int effectiveDamage = damage;
        damage = FreezeHitAdjust(target, move, damage, purpose);
        FreezeHitApplyDamage(catalog, actor, target, move, damage, critical, percent);
        FreezeHitReportType(catalog, actor, target, move);
        // Cmd_seteffectwithchance still draws on every reached hit, including
        // substitute, lethal damage, existing status and immunity to this effect.
        var secondary = SecondaryEffect(actor, move, purpose, FreezeHitSecondaryDamage(effectiveDamage, damage) == 0);
        if (secondary.Apply)
        {
            if (move.Effect == "EFFECT_TRI_ATTACK") ApplyTriAttack(catalog, target, purpose);
            else if (move.Effect is "EFFECT_THAW_HIT" or "EFFECT_BLAZE_KICK") InflictStatus(catalog, target, WildBattleStatus.Burn, false, certain: secondary.Certain);
            else if (move.Effect == "EFFECT_POISON_TAIL") InflictStatus(catalog, target, WildBattleStatus.Poison, false, certain: secondary.Certain);
            else if (move.Effect == "EFFECT_POISON_FANG") ApplyToxicSecondary(catalog, target, secondary.Certain);
            else ApplyFreeze(catalog, target);
        }
        if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
        if (damage > 0) MtMoonRageOnDamage(actor, target, move.Id);
        SourceGenericContact(catalog, actor, target, move, purpose, damage);
        ForestOutcome(true);
    }

    // SetMoveEffect's shared freeze branch. No Synchronize effect is recorded.
    internal void ApplyFreeze(PokemonCatalog catalog, BattlerSlot target)
    {
        var body = Get(target);
        if (body.Pokemon.CurrentHp == 0 || body.Status != 0 || SourceCalledSecondaryBlocked(target) ||
            WildBattleEffective.Ability(body) == "ABILITY_SHIELD_DUST" || Safeguard(target)) return;
        var types = WildBattleEffective.Types(catalog, body);
        if (types.Type1 == "TYPE_ICE" || types.Type2 == "TYPE_ICE" ||
            WildBattleEffective.Ability(body) == "ABILITY_MAGMA_ARMOR" || SourceCalledSun && !SourceCalledWeatherSuppressed) return;
        CancellerCancelMultiTurn(target, "freeze");
        ChangeStatus(target, WildBattleStatus.Freeze, "move");
    }

    internal int FreezeHitSecondaryDamage(int effectiveDamage, int adjustedDamage) => effectiveDamage;
    internal bool FreezeHitAccuracy(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose) =>
        SourceGenericAccuracy(actor, target, move, purpose);
    internal int FreezeHitDamage(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, bool critical, int percent) =>
        SourceGenericDamage(catalog, Get(actor), Get(target), target, move, critical, percent);
    internal int FreezeHitAdjust(BattlerSlot target, PokemonMoveDefinition move, int damage, string purpose) =>
        AdjustHeldDamage(target, move.Id, damage, purpose);
    internal void FreezeHitApplyDamage(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, int damage, bool critical, int percent)
    {
        SourceGenericApplyDamage(actor, target, move, damage, critical, percent);
        if (critical && damage == 0 && HeldDamageEffectiveZero(target))
            Add(n => new SourceCriticalHitEvent(n, Turn, actor, target));
    }
    internal void FreezeHitReportType(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move) =>
        SourceGenericEffectiveness(catalog, actor, target, move);
}

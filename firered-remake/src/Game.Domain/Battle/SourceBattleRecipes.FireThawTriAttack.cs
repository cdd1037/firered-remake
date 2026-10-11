using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool IsThawHitMove(MoveId move) => move.Value is "pret:MOVE_FLAME_WHEEL" or "pret:MOVE_SACRED_FIRE";
    internal void ThawByMove(BattlerSlot actor, MoveId move)
    {
        // The early frozen check still consumes its 1/5 natural-thaw draw. Move thaw
        // occurs only after the other cancellers, including infatuation and Bide.
        if (CancellerMoveThawEnabled && IsThawHitMove(move) && Get(actor).Status == WildBattleStatus.Freeze)
            ChangeStatus(actor, WildBattleStatus.None, "thawed-by-move");
    }
    internal void ExecuteThawHit(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        int slot, PokemonMoveDefinition move, string purpose) => ExecuteStatusHit(catalog, actor, target, slot, move, purpose);

    internal static bool FireHitFailureSetsNoEffect(string reason) => reason is
        "existing-status" or "fire-type-immunity" or "accuracy" or "substitute" or "protected" or "no-pp";

    internal virtual bool FireHitThawEnabled => true;
    internal virtual bool FireHitThawNoEffect => ContactCommandNoEffect;

    // Cmd_moveend MOVEEND_DEFROST consumes the retained specialDmg register.
    // There is intentionally no power, opponent-side, Substitute or fresh-hit gate.
    // Dynamic type is read here; datahpupdate separately decides the receipt category.
    internal void FireHitThaw(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move)
    {
        if (!FireHitThawEnabled || actor == target || FireHitThawNoEffect ||
            TypeChangeHitType(move) != "TYPE_FIRE" || HitItemReceipt(target).Special == 0) return;
        var body = Get(target);
        if (body.Pokemon.CurrentHp != 0 && (body.Status & WildBattleStatus.Freeze) != 0)
            ChangeStatus(target, body.Status & ~WildBattleStatus.Freeze, "fire-hit-thaw");
    }

    internal void ApplyTriAttack(PokemonCatalog catalog, BattlerSlot target, string purpose)
    {
        var body = Get(target);
        // Effect 9 passes Safeguard's <=7 check, then recursively calls SetMoveEffect.
        // Shield Dust, faint, Substitute and existing status stop before the choice RNG.
        if (body.Pokemon.CurrentHp == 0 || body.Status != 0 || SourceCalledSecondaryBlocked(target) ||
            WildBattleEffective.Ability(body) == "ABILITY_SHIELD_DUST") return;
        int effect = Draw($"{purpose}.tri-attack-status", 3) % 3;
        if (effect == 1) ApplyFreeze(catalog, target);
        else InflictStatus(catalog, target, effect == 0 ? WildBattleStatus.Burn : WildBattleStatus.Paralysis, false);
    }
}

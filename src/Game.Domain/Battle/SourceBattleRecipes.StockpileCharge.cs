using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasStockpileChargeRecipe(string effect) => effect is
        "EFFECT_STOCKPILE" or "EFFECT_SPIT_UP" or "EFFECT_SWALLOW" or "EFFECT_CHARGE";

    private void ChangeStockpile(BattlerSlot actor, int count, string reason)
    {
        var body = Get(actor); var previous = body.CalledVolatile ?? new();
        var next = previous with { StockpileCount = count };
        Set(actor, body with { CalledVolatile = next == new WildBattleCalledVolatileState() ? null : next });
        Add(n => new SourceMtMoonVolatileEvent(n, Turn, actor, "stockpile", previous.StockpileCount, count, reason));
    }
    internal virtual void StockpileProtectionMiss() { }

    internal void ExecuteStockpileCharge(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        int slot, PokemonMoveDefinition move, string purpose)
    {
        SpendEarlyPartyPp(actor, target, slot, move);
        int count = Get(actor).CalledVolatile?.StockpileCount ?? 0;
        if (move.Effect == "EFFECT_CHARGE")
        {
            var body = Get(actor); int before = body.ChargeTurnsRemaining;
            Set(actor, body with { ChargeTurnsRemaining = 2 });
            // setcharge is a successful command even when the timer is already two.
            // Preserve that invocation receipt instead of making presentation infer success from PP.
            Add(n => new SourceMtMoonVolatileEvent(n, Turn, actor, "charge", before, 2, "move"));
            return;
        }
        if (move.Effect == "EFFECT_STOCKPILE")
        {
            if (count == 3) { SourceStatRecoveryFail(actor, actor, move, "stockpile-full"); return; }
            // Gen III has only a counter, with no Defense/Special Defense boosts.
            ChangeStockpile(actor, count + 1, "move"); return;
        }
        if (move.Effect == "EFFECT_SWALLOW")
        {
            if (count == 0) { SourceStatRecoveryFail(actor, actor, move, "stockpile-empty"); return; }
            // stockpiletohpheal consumes the counter even on its full-HP failure branch.
            ChangeStockpile(actor, 0, "swallow");
            SourceStatRecoveryHeal(actor, move, Math.Max(1, Get(actor).Pokemon.Stats.MaxHp / (1 << (3 - count))));
            return;
        }
        if (move.Effect != "EFFECT_SPIT_UP") throw new SourceRecipeUnsupported("source_stockpile_charge_effect");
        bool protect = Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED");
        if (protect) StockpileProtectionMiss();
        else if (!SourceGenericAccuracy(actor, target, move, purpose))
        {
            // PrintMoveMissed executes a second ppreduce; stockpiletobasedamage was not reached.
            SpendEarlyPartyPp(actor, target, slot, move); SourceGenericMiss(actor, target, move); return;
        }
        if (count == 0) { SourceStatRecoveryFail(actor, target, move, "stockpile-empty"); return; }
        // The script multiplies CalculateBaseDamage's +2 result, then Helping Hand,
        // STAB/type and adjustsetdamage. It has neither critcalc nor random damage.
        ChangeStockpile(actor, 0, "spit-up");
        int damage = protect ? 0 : SourceGenericDamage(catalog, Get(actor), Get(target), target,
            move, critical: false, percent: 100, damageMultiplier: count);
        if (protect) { SourceStatRecoveryFail(actor, target, move, "protected"); return; }
        damage = AdjustHeldDamage(target, move.Id, damage, purpose);
        int lost = SourceGenericApplyDamage(actor, target, move, damage, false, 100);
        SourceGenericEffectiveness(catalog, actor, target, move);
        SourceGenericSecondary(catalog, actor, target, move, purpose, damage);
        RolloutFinish(catalog, actor, target, move, purpose, lost);
    }
}

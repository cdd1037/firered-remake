using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal static bool HasConditionalHitRecipe(string effect) => effect is
        "EFFECT_FACADE" or "EFFECT_SMELLINGSALT" or "EFFECT_REVENGE" or "EFFECT_BRICK_BREAK";

    internal void ExecuteConditionalHit(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        int slot, PokemonMoveDefinition move, string purpose)
    {
        if (!HasConditionalHitRecipe(move.Effect) || !SourceMoveDefinitionRules.IsValid(move))
            throw new SourceRecipeUnsupported("source_conditional_hit_definition");
        var user = Get(actor);
        // These script prefixes precede EffectHit. In particular a Substitute broken
        // by Smelling Salt does not retroactively install its certain cure effect.
        bool cure = move.Effect == "EFFECT_SMELLINGSALT" && !SubstitutePresent(target);
        int multiplier = move.Effect == "EFFECT_FACADE" &&
            (user.Status & (WildBattleStatus.Poison | WildBattleStatus.Burn | WildBattleStatus.Paralysis | WildBattleSourceFieldEffects.ToxicPoison)) != 0 ||
            cure && (Get(target).Status & WildBattleStatus.Paralysis) != 0 ? 2 : 1;
        if (move.Effect == "EFFECT_REVENGE")
        {
            var receipt = RetaliationReceipt(actor);
            // Cmd_doubledamagedealtifdamaged compares the selected battler position
            // independently against both real datahpupdate category receipts.
            bool retaliate = receipt?.Turn == Turn &&
                (receipt.PhysicalDamage > 0 && receipt.PhysicalSource == target ||
                 receipt.SpecialDamage > 0 && receipt.SpecialSource == target);
            // Reuse the existing arithmetic hook without mutating persistent state or
            // allowing its historical coarse flag to substitute for source receipts.
            user = user with { DamagedByOpponentThisTurn = retaliate };
        }
        bool hit = FixedDamageAccuracy(actor, target, move, purpose);
        SpendEarlyPartyPp(actor, target, slot, move);
        if (!hit) { SourceGenericMiss(actor, target, move); return; }
        // Brick Break's accuracy command precedes removal, but removal precedes type
        // immunity and damage calculation. It always clears the attacker's opposing
        // SIDE, even if an ally was deliberately selected as the hit target.
        if (move.Effect == "EFFECT_BRICK_BREAK") ConditionalBreakScreens(actor, target);
        if (Route11WaterAbsorb(actor, target, move) || SsAnneFlashFire(actor, target, move)) return;
        bool critical = SourceGenericCritical(user, Get(target), move, purpose);
        int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
        int rawDamage = SourceGenericDamage(catalog, user, Get(target), target, move, critical, percent, damageMultiplier: multiplier);
        int adjusted = FixedDamageAdjust(target, move, rawDamage, purpose);
        if (move.Effect == "EFFECT_BRICK_BREAK")
        {
            // Its second type command reestablishes immunity after the animation's
            // temporary result-flag clearing. No second damage or variance occurs.
            if (SourceTypeResult(catalog, target, move, command: SourceTypeCommand.TypeCalc2, apply: true).Immune)
                adjusted = 0;
        }
        int lost = SourceGenericApplyDamage(actor, target, move, adjusted, critical, percent);
        if (critical && lost == 0 && rawDamage > 0 && !FixedDamageZeroCriticalPrinted(target))
            Add(n => new SourceCriticalHitEvent(n, Turn, actor, target));
        ConditionalHitReportType(catalog, actor, target, move);
        if (cure)
        {
            // MOVE_EFFECT_CERTAIN skips Random on an effective hit, including zero
            // HP loss. Immunity falls through to the ordinary secondary draw.
            var secondary = SecondaryEffect(actor, move, purpose, rawDamage == 0, certain: true);
            if (secondary.Apply && Get(target).Pokemon.CurrentHp > 0 && !SubstitutePresent(target) &&
                (Get(target).Status & WildBattleStatus.Paralysis) != 0)
                ChangeStatus(target, Get(target).Status & ~WildBattleStatus.Paralysis, "smelling-salt");
        }
        else SourceGenericSecondary(catalog, actor, target, move, purpose, rawDamage);
        if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
        if (lost > 0) MtMoonRageOnDamage(actor, target, move.Id);
        SourceGenericContact(catalog, actor, target, move, purpose, lost);
        ForestOutcome(true);
    }

    internal void ConditionalHitReportType(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move) =>
        SourceGenericEffectiveness(catalog, actor, target, move);

    private void ConditionalBreakScreens(BattlerSlot actor, BattlerSlot target)
    {
        var side = (BattleSide)((int)actor.Side ^ 1);
        var affected = target.Side == side ? target : new BattlerSlot((int)side);
        foreach (string effect in new[] { "reflect", "light-screen" })
        {
            int before = FieldSettingTimer(side, effect);
            if (before == 0) continue;
            SetFieldSettingTimer(side, effect, 0);
            Add(n => new SourceMtMoonVolatileEvent(n, Turn, affected, effect, before, 0, "brick-break"));
        }
    }
}

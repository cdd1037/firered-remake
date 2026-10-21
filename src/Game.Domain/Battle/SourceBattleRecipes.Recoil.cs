using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal static class SourceRecoilRules
{
    // SetMoveEffect uses gHpDealt (actual body/Substitute HP loss), not computed damage.
    internal static int Recoil(int hpDealt, int divisor) => Math.Max(1, hpDealt / divisor);
    // FireRed DMG_RECOIL_FROM_MISS caps by the TARGET's max HP, including maxHP == 1.
    internal static int Crash(int adjustedDamage, int targetMaxHp) => Math.Min(Math.Max(1, adjustedDamage / 2), targetMaxHp / 2);
}

internal sealed partial class FrameRecipeContext
{
    internal static bool HasRecoilRecipe(string effect) => effect is "EFFECT_RECOIL" or "EFFECT_DOUBLE_EDGE" or "EFFECT_RECOIL_IF_MISS";

    internal void ExecuteRecoil(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, int slot,
        PokemonMoveDefinition move, string purpose)
    {
        if (!HasRecoilRecipe(move.Effect) || !SourceMoveDefinitionRules.IsValid(move))
            throw new SourceRecipeUnsupported("source_recoil_definition");
        if (move.Id == WildBattleRules.StruggleMove) { ExecuteStruggle(catalog, actor, target, slot, move, purpose); return; }
        bool hit = FixedDamageAccuracy(actor, target, move, purpose);
        SpendEarlyPartyPp(actor, target, slot, move);
        if (!hit)
        {
            SourceGenericMiss(actor, target, move);
            if (move.Effect != "EFFECT_RECOIL_IF_MISS") return;
            // Cmd_accuracycheck calls CheckWonderGuardAndLevitate only after a normal
            // accuracy roll. Protect and ALL semi-invulnerable misses skip that command.
            // Its type-table immunity sets DOESNT_AFFECT_FOE; Wonder Guard only changes
            // the miss message there. Later typecalc can still prevent crash HP mutation.
            var type = RecoilType(catalog, target, move);
            bool earlyMiss = Get(target).Protected || BillUnderground(actor, target, move);
            if (!earlyMiss && type.TypeImmune)
            {
                if (!SourceTypeResult(catalog, target, move, command: SourceTypeCommand.AccuracyMiss).WonderGuard)
                    RecoilReportType(catalog, actor, target, move);
                return;
            }
            int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
            int damage = FixedDamageAdjust(target, move,
                SourceGenericDamage(catalog, Get(actor), Get(target), target, move, false, percent, suppressTypeMiss: false, typeCommand: SourceTypeCommand.MissedTypeCalc), purpose);
            // The script clears MISSED before crash datahpupdate. Wonder Guard only
            // set that flag, so it cannot block crash; natural immunity still can.
            if (!type.TypeImmune) ApplyRecoilDamage(actor, target, move, SourceRecoilRules.Crash(damage, Get(target).Pokemon.Stats.MaxHp), crash: true);
            else if (!SourceTypeResult(catalog, target, move).WonderGuard) RecoilReportType(catalog, actor, target, move);
            ForestOutcome(true);
            return;
        }
        if (Route11WaterAbsorb(actor, target, move) || SsAnneFlashFire(actor, target, move)) return;
        bool critical = SourceGenericCritical(Get(actor), Get(target), move, purpose);
        int randomPercent = 100 - Draw($"{purpose}.damage", 16) % 16;
        int rawDamage = SourceGenericDamage(catalog, Get(actor), Get(target), target, move, critical, randomPercent);
        int adjusted = FixedDamageAdjust(target, move, rawDamage, purpose);
        int lost = SourceGenericApplyDamage(actor, target, move, adjusted, critical, randomPercent);
        if (critical && lost == 0 && rawDamage > 0 && !FixedDamageZeroCriticalPrinted(target))
            Add(n => new SourceCriticalHitEvent(n, Turn, actor, target));
        RecoilReportType(catalog, actor, target, move);
        if (move.Effect is "EFFECT_RECOIL" or "EFFECT_DOUBLE_EDGE" && rawDamage > 0)
        {
            // MOVE_EFFECT_CERTAIN skips Random, including Rock Head. The damage command
            // register changes before the ability prevents its passive HP update.
            int recoil = SourceRecoilRules.Recoil(lost, move.Effect == "EFFECT_RECOIL" ? 4 : 3);
            HitItemsCommandDamage(recoil, ContactCommandNoEffect);
            if (WildBattleEffective.Ability(Get(actor)) != "ABILITY_ROCK_HEAD")
                ApplyRecoilDamage(actor, target, move, recoil, crash: false);
        }
        else SourceGenericSecondary(catalog, actor, target, move, purpose, rawDamage);
        if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
        if (lost > 0) MtMoonRageOnDamage(actor, target, move.Id);
        SourceGenericContact(catalog, actor, target, move, purpose, lost);
        ForestOutcome(true);
    }

    private (bool TypeImmune, bool Immune, WildBattleEffectiveness? Effectiveness) RecoilType(PokemonCatalog catalog,
        BattlerSlot target, PokemonMoveDefinition move)
    {
        var result = SourceTypeResult(catalog, target, move);
        return (result.TypeImmune, result.Immune, result.Effectiveness);
    }
    internal void RecoilReportType(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move)
    {
        if (SourceTypeReport(catalog, actor, target, move)) return;
        SourceDamageResultMessage(actor, target, move);
        if (RecoilType(catalog, target, move).Effectiveness is { } effect)
            Add(n => new SourceEffectivenessEvent(n, Turn, actor, target, move.Id, effect));
    }

    private void ApplyRecoilDamage(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, int damage, bool crash)
    {
        HitItemsCommandDamage(damage, ContactCommandNoEffect || crash);
        var user = Get(actor); int before = user.Pokemon.CurrentHp, after = Math.Max(0, before - damage);
        Set(actor, user with { Pokemon = user.Pokemon with { CurrentHp = after } });
        // Crash has IGNORE_SUBSTITUTE but no PASSIVE_DAMAGE. Recoil has both flags.
        if (crash) RecordCrashDamage(actor, target, move, before - after);
        Add(n => new SourceRecoilEvent(n, Turn, actor, move.Id, before, after, damage));
        if (after == 0) ForestFaint(actor);
    }
    private void RecordCrashDamageCore(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, int lost) =>
        RecordHitItemDamage(actor, lost, substitute: false, noEffect: false,
            physical: TypeRules.Category(FixedDamageElementType(move.Type)) == DamageCategory.Physical);
}

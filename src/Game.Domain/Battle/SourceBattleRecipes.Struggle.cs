using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    // BattleScript_EffectRecoil -> EffectHit. Forced selection owns NO_PPDEDUCT;
    // the recipe retains the real Struggle ID (physical damage, no STAB/typecalc).
    // Both the singles scalar adapter and the four-position context use this recipe.
    internal void ExecuteStruggle(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, int slot,
        PokemonMoveDefinition move, string purpose)
    {
        if (move.Id != WildBattleRules.StruggleMove || !SourceMoveDefinitionRules.IsValid(move))
            throw new SourceRecipeUnsupported("source_struggle_definition");
        bool hit = StruggleAccuracy(actor, target, move, purpose);
        SpendEarlyPartyPp(actor, target, slot, move);
        if (!hit) { SourceGenericMiss(actor, target, move); return; }
        bool critical = SourceGenericCritical(Get(actor), Get(target), move, purpose);
        int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
        int rawDamage = SourceGenericDamage(catalog, Get(actor), Get(target), target, move, critical, percent);
        int damage = StruggleAdjustedDamage(target, move, rawDamage, purpose);
        int lost = SourceGenericApplyDamage(actor, target, move, damage, critical, percent);
        if (critical && lost == 0 && !(SourceHeldDamageEnabled && HeldDamageEffectiveZero(target)))
            Add(n => new SourceCriticalHitEvent(n, Turn, actor, target));
        SourceDamageResultMessage(actor, target, move);
        if (rawDamage > 0)
        {
            // Source gHpDealt is capped by actual lost HP. Struggle bypasses Rock Head.
            int recoil = Math.Max(1, lost / 4);
            HitItemsCommandDamage(recoil, ContactCommandNoEffect);
            var user = Get(actor); int before = user.Pokemon.CurrentHp, after = Math.Max(0, before - recoil);
            Set(actor, user with { Pokemon = user.Pokemon with { CurrentHp = after } });
            Add(n => new SourceRecoilEvent(n, Turn, actor, move.Id, before, after, recoil));
            if (after == 0) ForestFaint(actor);
        }
        if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
        if (lost > 0) MtMoonRageOnDamage(actor, target, move.Id);
        SourceGenericContact(catalog, actor, target, move, purpose, lost);
        ForestOutcome(true);
    }

    internal virtual int StruggleAdjustedDamage(BattlerSlot target, PokemonMoveDefinition move, int damage, string purpose) =>
        AdjustHeldDamage(target, move.Id, damage, purpose);
    internal virtual bool StruggleSandstorm => SandstormTurnsRemaining > 0;

    private bool StruggleAccuracy(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose)
    {
        var user = Get(actor); var defender = Get(target);
        if (defender.Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED")) return false;
        if (CalledSureHit(actor, target)) return true;
        if (BillUnderground(actor, target, move)) return false;
        int accuracy = WildBattleDamage.Accuracy(move.Accuracy, user.AccuracyStage, defender.Identified ? 6 : defender.EvasionStage);
        if (WildBattleEffective.Ability(user) == "ABILITY_COMPOUND_EYES") accuracy = accuracy * 130 / 100;
        if (!SourceCalledWeatherSuppressed && StruggleSandstorm && WildBattleEffective.Ability(defender) == "ABILITY_SAND_VEIL") accuracy = accuracy * 80 / 100;
        if (WildBattleEffective.Ability(user) == "ABILITY_HUSTLE") accuracy = accuracy * 80 / 100;
        if (defender.HeldItem == "ITEM_BRIGHT_POWDER") accuracy = accuracy * 90 / 100;
        if (defender.HeldItem == "ITEM_LAX_INCENSE") accuracy = accuracy * 95 / 100;
        return Draw($"{purpose}.accuracy", 100) % 100 + 1 <= accuracy;
    }
}

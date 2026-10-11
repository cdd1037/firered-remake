using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasSleepAttackRecipe(string effect) => effect is "EFFECT_REST" or "EFFECT_SNORE" or "EFFECT_DREAM_EATER";

    internal void ExecuteSleepAttack(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        int slot, PokemonMoveDefinition move, string purpose)
    {
        // attackcanceler already decremented sleep and checked Soundproof. Preserve
        // its Protect history marker while allowing each script's own command order.
        SleepResidualPrepare(actor, target, move);
        if (move.Effect == "EFFECT_REST") { ExecuteRest(actor, slot, move); return; }
        bool snore = move.Effect == "EFFECT_SNORE";
        if (snore)
        {
            bool sleeping = WildBattleStatus.IsSleeping(Get(actor).Status);
            SpendEarlyPartyPp(actor, target, slot, move);
            if (!sleeping)
            {
                if (Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED")) SleepAttackMiss(actor, target, move);
                else SourceStatRecoveryFail(actor, target, move, "user-awake");
                return;
            }
        }
        else if (SubstitutePresent(target) || !WildBattleStatus.IsSleeping(Get(target).Status))
        {
            // Both branches jump to WasntAffected before accuracy. That direct
            // string does not dispatch resultmessage's earlier Protect marker.
            SpendEarlyPartyPp(actor, target, slot, move);
            SourceStatRecoveryFail(actor, target, move, SubstitutePresent(target) ? "substitute" : "target-awake");
            return;
        }
        bool hit = SleepAttackAccuracy(actor, target, move, purpose);
        if (!snore) SpendEarlyPartyPp(actor, target, slot, move);
        if (!hit) { SleepAttackMiss(actor, target, move); return; }
        bool critical = SourceGenericCritical(Get(actor), Get(target), move, purpose);
        int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
        int effectiveDamage = SleepAttackDamage(catalog, actor, target, move, critical, percent);
        int damage = SleepAttackAdjust(target, move, effectiveDamage, purpose);
        int lost = SleepAttackApplyDamage(catalog, actor, target, move, damage, critical, percent);
        SleepAttackReportType(catalog, actor, target, move);
        if (snore)
        {
            // seteffectwithchance draws even when typecalc or Substitute prevents
            // the effect; effective zero-loss from Endure does not suppress it.
            int roll = Draw($"{purpose}.secondary-effect", 100) % 100;
            int chance = move.SecondaryEffectChance * (WildBattleEffective.Ability(Get(actor)) == "ABILITY_SERENE_GRACE" ? 2 : 1);
            if (roll <= chance && effectiveDamage > 0) ApplyFlinch(target);
        }
        else
        {
            // Dream Eater calls negativedamage directly. It does not execute
            // Absorb's Liquid Ooze branch. datahpupdate suppresses immune healing.
            int drain = SleepAttackDrainAmount(lost);
            HitItemsCommandDamage(-drain, noEffect: effectiveDamage == 0);
            if (effectiveDamage > 0)
            {
                var body = Get(actor); int before = body.Pokemon.CurrentHp;
                int after = Math.Min(body.Pokemon.Stats.MaxHp, before + drain);
                Set(actor, body with { Pokemon = body.Pokemon with { CurrentHp = after } });
                Add(n => new SourceDrainEvent(n, Turn, actor, target, move.Id, lost, before, after, after - before));
            }
        }
        if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
        if (damage > 0) MtMoonRageOnDamage(actor, target, move.Id);
        SourceGenericContact(catalog, actor, target, move, purpose, damage);
        ForestOutcome(true);
    }

    internal static int SleepAttackDrainAmount(int lost) => Math.Max(1, lost / 2);
    private void SleepAttackMiss(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move)
    {
        if (Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED"))
            SourceStatRecoveryFail(actor, target, move, "protected");
        else SourceGenericMiss(actor, target, move);
    }

    private void ExecuteRest(BattlerSlot actor, int slot, PokemonMoveDefinition move)
    {
        SpendEarlyPartyPp(actor, actor, slot, move);
        var body = Get(actor);
        if (WildBattleStatus.IsSleeping(body.Status)) { SourceStatRecoveryFail(actor, actor, move, "already-asleep"); return; }
        if (UproarPreventsSleep(actor)) { SourceStatRecoveryFail(actor, actor, move, "uproar"); return; }
        string ability = WildBattleEffective.Ability(body);
        if (ability is "ABILITY_INSOMNIA" or "ABILITY_VITAL_SPIRIT")
        {
            RecordAbility(actor, ability);
            Add(n => new SourceAbilityPreventedEvent(n, Turn, actor, actor, move.Id, ability));
            return;
        }
        // trysetrest assigns negative maxHP even on its full-HP failure branch.
        HitItemsCommandDamage(-body.Pokemon.Stats.MaxHp);
        if (body.Pokemon.CurrentHp == body.Pokemon.Stats.MaxHp)
        { SourceStatRecoveryFail(actor, actor, move, "already-full-hp"); return; }
        ChangeStatus(actor, 3, "rest");
        int before = body.Pokemon.CurrentHp, after = body.Pokemon.Stats.MaxHp;
        Set(actor, Get(actor) with { Pokemon = Get(actor).Pokemon with { CurrentHp = after } });
        Add(n => new SourceRecoveredEvent(n, Turn, actor, move.Id, before, after, after - before));
    }

    internal virtual bool SleepAttackAccuracy(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose) =>
        SourceGenericAccuracy(actor, target, move, purpose);
    internal virtual int SleepAttackDamage(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, bool critical, int percent) =>
        SourceGenericDamage(catalog, Get(actor), Get(target), target, move, critical, percent);
    internal virtual int SleepAttackAdjust(BattlerSlot target, PokemonMoveDefinition move, int damage, string purpose) =>
        AdjustHeldDamage(target, move.Id, damage, purpose);
    internal virtual int SleepAttackApplyDamage(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, int damage, bool critical, int percent)
    {
        int lost = SourceGenericApplyDamage(actor, target, move, damage, critical, percent);
        if (critical && damage == 0 && HeldDamageEffectiveZero(target)) Add(n => new SourceCriticalHitEvent(n, Turn, actor, target));
        return lost;
    }
    internal virtual void SleepAttackReportType(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move) =>
        SourceGenericEffectiveness(catalog, actor, target, move);
}

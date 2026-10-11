using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    // TARGET_TURN_DAMAGED is retained SpecialStatus physical/special damage, not the
    // current damage command or merely a fresh positive hit. Future attacks can carry it.
    internal virtual bool CanonicalContactReceipts => true;
    internal virtual bool ContactCommandNoEffect => false;
    internal virtual bool ContactConfusionSelfDamage => false;
    internal virtual bool ContactObliviousGuard => true;
    internal abstract BattlerSlot? InfatuationSource(BattlerSlot slot);
    internal abstract void SetInfatuationSource(BattlerSlot slot, BattlerSlot? source);

    internal void ChangeInfatuation(BattlerSlot slot, BattlerSlot? source, string reason)
    {
        var before = InfatuationSource(slot);
        if (before == source) return;
        SetInfatuationSource(slot, source);
        Add(n => new SourceInfatuationChangedEvent(n, Turn, slot, before, source, reason));
    }
    internal bool InfatuationPreventsMove(BattlerSlot slot, MoveId move, string purpose)
    {
        if (InfatuationSource(slot) is not { } source || (Draw($"{purpose}.infatuation-action", 2) & 1) != 0) return false;
        CancellerCancelMultiTurn(slot, "infatuation");
        Add(n => new SourceInfatuationBlockedEvent(n, Turn, slot, source, move));
        return true;
    }
    // Four-position execution follows TARGET_TURN_DAMAGED. The singles bridge explicitly
    // retains earlier command-damage gates where no naturally reached divergence is proven.
    internal virtual bool ContactUsesRetainedDamage(string ability) => CanonicalContactReceipts;
    internal virtual bool ContactEffectSporeEnabled => true;
    internal virtual bool ContactAbilityBlocked(BattlerSlot actor, BattlerSlot target,
        PokemonMoveDefinition? move, string ability) => ContactConfusionSelfDamage;
    internal virtual bool ContactSporeSleepBlocked(BattlerSlot actor) =>
        WildBattleEffective.Ability(Get(actor)) is "ABILITY_VITAL_SPIRIT" or "ABILITY_INSOMNIA" || UproarPreventsSleep(actor);
    internal virtual void ContactInflictStatus(PokemonCatalog catalog, BattlerSlot actor, uint status, string reason) =>
        InflictStatus(catalog, actor, status, true, reason);

    internal void ContactAbility(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        PokemonMoveDefinition? move, string purpose, int damage)
    {
        string ability = WildBattleEffective.Ability(Get(target));
        if (ability == "ABILITY_CUTE_CHARM") { ContactCuteCharm(actor, target, move, purpose, damage); return; }
        if (ability is not ("ABILITY_ROUGH_SKIN" or "ABILITY_POISON_POINT" or "ABILITY_FLAME_BODY" or "ABILITY_EFFECT_SPORE") ||
            ability == "ABILITY_EFFECT_SPORE" && !ContactEffectSporeEnabled) return;
        var attacker = Get(actor);
        bool retained = ContactUsesRetainedDamage(ability);
        bool damaged = retained ? HitItemReceipt(target) is { } receipt && (receipt.Physical != 0 || receipt.Special != 0)
            : damage != 0 && SourceCalledHitReachedPokemon(target);
        // Source has no holder HP or absence guard here. All eligibility guards precede RNG;
        // status, types and status-immunity abilities follow the contact chance draw.
        if (!damaged || retained && ContactCommandNoEffect || attacker.Pokemon.CurrentHp == 0 ||
            move is not null && !move.Flags.Contains("FLAG_MAKES_CONTACT") || ContactAbilityBlocked(actor, target, move, ability)) return;
        if (ability == "ABILITY_ROUGH_SKIN")
        {
            if (move is null) return;
            int before = attacker.Pokemon.CurrentHp;
            int amount = Math.Max(1, attacker.Pokemon.Stats.MaxHp / 16), after = Math.Max(0, before - amount);
            // PASSIVE_DAMAGE ignores Substitute and leaves SpecialStatus receipts untouched.
            HitItemsCommandDamage(amount);
            RecordAbility(target, ability);
            Add(n => new SourceAbilityActivatedEvent(n, Turn, target, ability));
            Set(actor, attacker with { Pokemon = attacker.Pokemon with { CurrentHp = after } });
            Add(n => new SourceAbilityDamageEvent(n, Turn, target, actor, move.Id, ability, before, after, before - after));
            if (after == 0) ForestFaint(actor);
            return;
        }
        string reason = ability switch { "ABILITY_POISON_POINT" => "poison-point", "ABILITY_FLAME_BODY" => "flame-body", _ => "effect-spore" };
        int bound = ability == "ABILITY_EFFECT_SPORE" ? 10 : 3;
        if (Draw($"{purpose}.{reason}", bound) % bound != 0) return;
        int effect = ability == "ABILITY_POISON_POINT" ? 2 : 4;
        if (ability == "ABILITY_EFFECT_SPORE")
            do effect = Draw($"{purpose}.effect-spore-status", 4) & 3; while (effect == 0);
        RecordAbility(target, ability);
        if (attacker.Status != 0) return;
        if (effect == 1)
        {
            if (ContactSporeSleepBlocked(actor)) return;
            Add(n => new SourceAbilityActivatedEvent(n, Turn, target, ability));
            ChangeStatus(actor, (uint)((Draw($"{purpose}.effect-spore-sleep-duration", 4) & 3) + 2), reason);
            return;
        }
        var types = WildBattleEffective.Types(catalog, attacker);
        if (effect == 2 && (types.Type1 is "TYPE_POISON" or "TYPE_STEEL" || types.Type2 is "TYPE_POISON" or "TYPE_STEEL") ||
            effect == 4 && (types.Type1 == "TYPE_FIRE" || types.Type2 == "TYPE_FIRE" || WildBattleEffective.Ability(attacker) == "ABILITY_WATER_VEIL")) return;
        Add(n => new SourceAbilityActivatedEvent(n, Turn, target, ability));
        // HITMARKER_STATUS_ABILITY_EFFECT bypasses Shield Dust, Safeguard and own Substitute.
        ContactInflictStatus(catalog, actor, effect == 2 ? WildBattleStatus.Poison : effect == 3 ? WildBattleStatus.Paralysis : WildBattleStatus.Burn, reason);
    }

    internal void ContactCuteCharm(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition? move, string purpose, int damage)
    {
        var attacker = Get(actor); var holder = Get(target);
        if (WildBattleEffective.Ability(holder) != "ABILITY_CUTE_CHARM") return;
        bool damaged = CanonicalContactReceipts
            ? HitItemReceipt(target) is { } receipt && (receipt.Physical != 0 || receipt.Special != 0)
            : damage != 0 && SourceCalledHitReachedPokemon(target);
        // battle_util.c ABILITY_CUTE_CHARM: every guard before Random must remain before it;
        // gender, existing infatuation and Oblivious intentionally consume the failed roll.
        if (ContactCommandNoEffect || attacker.Pokemon.CurrentHp == 0 || ContactConfusionSelfDamage ||
            move is not null && !move.Flags.Contains("FLAG_MAKES_CONTACT") || !damaged || holder.Pokemon.CurrentHp == 0 ||
            Draw($"{purpose}.cute-charm", 3) % 3 != 0 ||
            ContactObliviousGuard && WildBattleEffective.Ability(attacker) == "ABILITY_OBLIVIOUS" ||
            attacker.Pokemon.Gender == holder.Pokemon.Gender || InfatuationSource(actor) is not null ||
            attacker.Pokemon.Gender == PokemonGender.Genderless || holder.Pokemon.Gender == PokemonGender.Genderless) return;
        RecordAbility(target, "ABILITY_CUTE_CHARM");
        Add(n => new SourceAbilityActivatedEvent(n, Turn, target, "ABILITY_CUTE_CHARM"));
        ChangeInfatuation(actor, target, "cute-charm");
    }
}

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

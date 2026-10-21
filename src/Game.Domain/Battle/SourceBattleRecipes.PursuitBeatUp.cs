using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal static class SourcePursuitRules
{
    // Cmd_jumpifnopursuitswitchdmg. The switch owner supplies the chosen (not redirected)
    // target and iterates the opponent RIGHT position before LEFT, independently of speed.
    internal static bool Eligible(WildBattleCombatant actor, WildBattleCombatant outgoing) =>
        !WildBattleStatus.IsSleeping(actor.Status) && actor.Status != WildBattleStatus.Freeze &&
        !actor.TruantLoafing && outgoing.Pokemon.CurrentHp > 0;
}

internal sealed partial class FrameRecipeContext
{
    internal static bool HasPursuitBeatUpRecipe(string effect) => effect is "EFFECT_PURSUIT" or "EFFECT_BEAT_UP";
    internal void BeatUpSynchronize() { }
    internal int BeatUpAdjust(BattlerSlot target, PokemonMoveDefinition move, int damage, string purpose) =>
        AdjustHeldDamage(target, move.Id, damage, purpose);
    internal void BeatUpResultMessage(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move) => SourceDamageResultMessage(actor, target, move);
    internal void BeatUpMoveEnd(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move) =>
        SourceMultiHitMoveEnd(catalog, actor, target, move);

    internal void ExecutePursuitBeatUp(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        int slot, PokemonMoveDefinition move, string purpose)
    {
        if (move.Effect == "EFFECT_PURSUIT") { SourceGenericHit(catalog, actor, target, slot, move, purpose); return; }
        bool hit = SourceGenericAccuracy(actor, target, move, purpose);
        SpendEarlyPartyPp(actor, target, slot, move);
        if (!hit) { SourceGenericMiss(actor, target, move); return; }
        BeatUpSynchronize();
        bool contributed = false;
        for (int index = 0; index < BeatUpParty(actor.Side).Length; index++)
        {
            if (Get(target).Pokemon.CurrentHp == 0) break;
            var member = BeatUpParty(actor.Side)[index];
            if (member.IsEgg || member.Pokemon.CurrentHp == 0 || member.Status != 0) continue;
            contributed = true;
            BeatUpBeginHit(target);
            Add(n => new SourceCalledBeatUpMemberEvent(n, Turn, actor.Side, index, member.Pokemon.Species));
            int damage = WildBattleSourcePartyEffects.BeatUpBaseDamage(catalog, member, Get(target), move.Power);
            if (HelpingHand(actor)) damage = damage * 15 / 10;
            bool critical = SourceGenericCritical(Get(actor), Get(target), move, purpose);
            if (critical) damage *= 2;
            int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
            damage = Math.Max(1, damage * percent / 100);
            damage = BeatUpAdjust(target, move, damage, purpose);
            int lost = SourceGenericApplyDamage(actor, target, move, damage, critical, percent);
            PartyHitCriticalMessage(actor, target, critical, damage);
            BeatUpResultMessage(actor, target, move);
            if (lost > 0 && SourceCalledHitReachedPokemon(target)) MtMoonRageOnDamage(actor, target, move.Id);
            if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
            // Beat Up has no contact flag and has no typecalc/STAB/type multiplier command.
            SourceColorChange(catalog, actor, target, move);
            BeatUpMoveEnd(catalog, actor, target, move);
        }
        if (!contributed) Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move.Id, "no-eligible-contributor"));
        ForestOutcome(true);
    }

    internal bool PursuitCritical(WildBattleCombatant actor, WildBattleCombatant target, PokemonMoveDefinition move, string purpose) =>
        SourceGenericCritical(actor, target, move, purpose);
    internal int PursuitDamage(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        PokemonMoveDefinition move, bool critical, int percent) =>
        SourceGenericDamage(catalog, Get(actor), Get(target), target, move, critical, percent, pursuitSwitch: true);
    internal int PursuitAdjust(BattlerSlot target, PokemonMoveDefinition move, int damage, string purpose) =>
        AdjustHeldDamage(target, move.Id, damage, purpose);
    internal void PursuitReportType(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move) =>
        SourceGenericEffectiveness(catalog, actor, target, move);

    // Switch interception deliberately omits attackcanceler, accuracycheck and secondary RNG.
    // Only moveend states 3..6 run; normal history, berries and hit items are not reached.
    internal void ExecutePursuitIntercept(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        int slot, PokemonMoveDefinition move, string purpose)
    {
        var before = Get(actor);
        Add(n => new SourcePursuitInterceptedEvent(n, Turn, actor, target, slot));
        SpendEarlyPartyPp(actor, target, slot, move);
        bool critical = PursuitCritical(before, Get(target), move, purpose);
        int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
        int damage = PursuitAdjust(target, move, PursuitDamage(catalog, actor, target, move, critical, percent), purpose);
        int lost = SourceGenericApplyDamage(actor, target, move, damage, critical, percent);
        PartyHitCriticalMessage(actor, target, critical, damage);
        if (SourceCalledHitReachedPokemon(target)) PursuitRecordDamage(actor, target, move, lost);
        PursuitReportType(catalog, actor, target, move);
        if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
        if (damage > 0) MtMoonRageOnDamage(actor, target, move.Id);
        PursuitMoveEnd(catalog, actor, target, move, purpose, damage, before);
    }
}

using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;
/// <summary>Bounded original GetMostSuitableMonToSwitchInto, including its stale-move and active-stat bugs.
/// This is opponent replacement after fainting, not voluntary switching. It never consumes RNG.</summary>
public static class Route3TrainerAiReplacement
{
    public static ForestTrainerReplacementDecision Choose(PokemonCatalog catalog, ForestTrainerReplacementInput input)
    {
        ForestTrainerReplacementDecision Fault(string code) => new(TransitionStatus.Fault, null, [],
            new(code, "Input is outside the bounded Route3 trainer replacement profile."));
        string? error = Validate(catalog, input);
        if (error is not null) return Fault(error);
        var active = input.FaintedActive.Pokemon;
        var a = catalog.Species[active.Species]; var d = catalog.Species[input.Target.Pokemon.Species];
        ElementType Type(string value) => Enum.Parse<ElementType>(value[5..], true);
        bool none = input.StaleCurrentMove.Value == "pret:MOVE_NONE";
        bool struggle = input.StaleCurrentMove == WildBattleRules.StruggleMove;
        var stale = none || struggle ? null : catalog.Moves[input.StaleCurrentMove];
        int power = none ? 0 : struggle ? 50 : stale!.Power;
        var staleType = none || struggle ? ElementType.Normal : Type(stale!.Type);
        bool physical = TypeRules.Category(staleType) == DamageCategory.Physical;
        // FaintClearSetData reset all active stages/status. Cmd_getexp case6 clears its ability
        // and item before any replacement. The source nevertheless uses that fainted mon's stats.
        int attack = physical ? active.Stats.Attack : active.Stats.SpecialAttack;
        int defense = physical ? input.Target.Pokemon.Stats.Defense : input.Target.Pokemon.Stats.SpecialDefense;
        if (physical) defense = Math.Max(1, WildBattleDamage.Stat(defense, input.Target.DefenseStage));
        int baseDamage = attack * power * (2 * active.Level / 5 + 2) / defense / 50;
        if (physical) baseDamage = Math.Max(1, baseDamage);
        baseDamage += 2;
        var scores = new int[input.Party.Length]; Array.Fill(scores, -1);
        int chosen = -1; byte bestDamage = 0;
        for (int i = 0; i < input.Party.Length; i++)
        {
            if (i == input.FaintedPartyIndex || input.Party[i].Pokemon.CurrentHp == 0) continue;
            scores[i] = 0;
            // Multiple possible reserves occur only in bug parties with Tackle/Poison Sting;
            // none is super-effective in this closure. All other parties have a unique reserve.
            // Source does not check PP here, nor substitute candidate stats/current move.
            foreach (var slot in input.Party[i].Pokemon.Moves)
            {
                var move = catalog.Moves[slot.Move];
                int damage = baseDamage;
                var type = Type(move.Type);
                if (type == Type(a.Type1) || type == Type(a.Type2)) damage = damage * 15 / 10;
                foreach (var effect in TypeRules.OrderedEffects(type, Type(d.Type1), Type(d.Type2)))
                {
                    damage = damage * effect.MultiplierTenths / 10;
                    if (damage == 0 && effect.MultiplierTenths != 0) damage = 1;
                }
                scores[i] = Math.Max(scores[i], damage);
                if (bestDamage < damage) { bestDamage = unchecked((byte)damage); chosen = i; }
            }
        }
        // OpponentHandleChoosePokemon falls back to the first living, inactive party slot.
        if (chosen < 0) chosen = Enumerable.Range(0, input.Party.Length).First(i => i != input.FaintedPartyIndex && input.Party[i].Pokemon.CurrentHp > 0);
        return new(TransitionStatus.Accepted, chosen, [.. scores], null);
    }

    private static string? Validate(PokemonCatalog? catalog, ForestTrainerReplacementInput? input)
    {
        if (catalog?.Species.Count != 40 || input is null || input.Party.IsDefaultOrEmpty || input.Party.Length > 4 ||
            input.FaintedPartyIndex < 0 || input.FaintedPartyIndex >= input.Party.Length || input.FaintedActive is null || input.Target is null)
            return "route3_trainer_replacement_input";
        if (input.Party.Any(mon => mon is null || mon.HeldItem != "ITEM_NONE" || mon.AttackBadgeBoost) || input.FaintedActive.HeldItem != "ITEM_NONE") return "route3_trainer_replacement_enemy_item";
        foreach (var mon in input.Party.Append(input.FaintedActive).Append(input.Target))
            if (Route3TrainerAi.ValidateCombatant(catalog, mon) is not null) return "route3_trainer_replacement_combatant";
        if (input.FaintedActive.Pokemon.CurrentHp != 0 || input.Target.Pokemon.CurrentHp == 0) return "route3_trainer_replacement_faint_state";
        var active = input.Party[input.FaintedPartyIndex].Pokemon;
        if (active with { Moves = input.FaintedActive.Pokemon.Moves } != input.FaintedActive.Pokemon ||
            !active.Moves.SequenceEqual(input.FaintedActive.Pokemon.Moves)) return "route3_trainer_replacement_active_identity";
        if (!input.Party.Where((_,i) => i != input.FaintedPartyIndex).Any(x => x.Pokemon.CurrentHp > 0)) return "route3_trainer_replacement_no_reserve";
        if (!Route3TrainerPartyFactory.SupportedTrainers.Any(id => Route3TrainerPartyFactory.Expected(id).Party.SequenceEqual(
                input.Party.Select(m => (m.Pokemon.SourceSpeciesId, m.Pokemon.Level)))) ||
            input.Party.Any(m => !Route3TrainerAi.ValidUser(catalog, m.Pokemon))) return "route3_trainer_replacement_party_scope";
        if (input.StaleCurrentMove.Value is not ("pret:MOVE_NONE" or "pret:MOVE_STRUGGLE") &&
            (!catalog.Moves.TryGetValue(input.StaleCurrentMove, out var move) || !Route3BattleScope.ValidDefinition(move)))
            return "route3_trainer_replacement_stale_move";
        return null;
    }
}

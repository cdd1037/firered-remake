using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Shared pinned replacement, including defensive-type ranking, stale-current-move,
/// active-stat and byte best-damage source bugs. Voluntary switching uses CeruleanTrainerActionAi.</summary>
public static class CeruleanTrainerAiReplacement
{
    public static ForestTrainerReplacementDecision Choose(ForestTrainerReplacementInput input,
        SourceTrainerAiSlotContext slots, bool rain = false, bool reflect = false)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var catalog = slots.Catalog;
        ForestTrainerReplacementDecision Fault(string code) => new(TransitionStatus.Fault, null, [],
            new(code, "Invalid trainer replacement battle context."));
        string? error = !slots.Matches(input.FaintedActive, input.Target) || !slots.ActualParty.SequenceEqual(input.Party)
            ? "source_four_trainer_replacement_context" : null;
        if (error is not null) return Fault(error);
        var active = input.FaintedActive.Pokemon;
        var a = WildBattleEffective.Types(catalog,input.FaintedActive); var d = WildBattleEffective.Types(catalog,input.Target);
        ElementType Type(string value) => Enum.Parse<ElementType>(value[5..], true);
        bool Living(int index) => slots.SwitchEligible(index);
        var scores = new int[input.Party.Length]; Array.Fill(scores, -1);
        var invalid = new bool[input.Party.Length];
        // Both target type slots run even when identical. The source ranks damage those target
        // types deal to candidate types, despite its offensive-suitability comment.
        while (true)
        {
            int best = -1; byte bestType = 0;
            for (int i = 0; i < input.Party.Length; i++)
            {
                if (invalid[i] || !Living(i)) { invalid[i] = true; continue; }
                var candidate = catalog.Species[input.Party[i].Pokemon.Species]; byte value = 10;
                foreach (var attackType in new[] { d.Type1, d.Type2 })
                    foreach (var effect in TypeRules.OrderedEffects(Type(attackType), Type(candidate.Type1), Type(candidate.Type2)))
                        value = unchecked((byte)(value * effect.MultiplierTenths / 10));
                if (bestType < value) { bestType = value; best = i; }
            }
            if (best < 0) break;
            foreach (var slot in input.Party[best].Pokemon.Moves)
            {
                var move = catalog.Moves[slot.Move];
                if (move.Power == 0 || WildBattleEffective.Ability(input.Target) == "ABILITY_LEVITATE" && move.Type == "TYPE_GROUND") continue;
                int value = 10;
                foreach (var effect in TypeRules.OrderedEffects(Type(move.Type), Type(d.Type1), Type(d.Type2)))
                    if (!(input.Target.Identified && effect.MultiplierTenths == 0 && move.Type is "TYPE_NORMAL" or "TYPE_FIGHTING"))
                        value = value * effect.MultiplierTenths / 10;
                if (value > 10) return new(TransitionStatus.Accepted, best, [.. scores], null);
            }
            invalid[best] = true;
        }
        bool none = input.StaleCurrentMove == SourceTrainerEncounterPolicy.NoMove;
        bool struggle = input.StaleCurrentMove == WildBattleRules.StruggleMove;
        var stale = none || struggle ? null : catalog.Moves[input.StaleCurrentMove];
        int power = none ? 0 : struggle ? 50 : stale!.Power;
        var staleType = none || struggle ? ElementType.Normal : Type(stale!.Type);
        // Source AI_CalcDmg reads active stats, stale gCurrentMove and cleared faint ability.
        int baseDamage = CeruleanTrainerAi.BasePrediction(input.FaintedActive, input.Target,
            power, staleType, stale?.Effect, clearUserAbility: input.FaintedActive.Pokemon.CurrentHp == 0, rain: rain, reflect: reflect, slots: slots, moveTarget: stale?.Target);
        int chosen = -1; byte bestDamage = 0;
        for (int i = 0; i < input.Party.Length; i++)
        {
            if (!Living(i)) continue;
            scores[i] = 0;
            foreach (var slot in input.Party[i].Pokemon.Moves)
            {
                var move = catalog.Moves[slot.Move]; int damage = 0;
                // Excludes power ONE, but includes power ZERO status moves.
                if (move.Power != 1)
                {
                    damage = baseDamage; var type = Type(move.Type);
                    if (type == Type(a.Type1) || type == Type(a.Type2)) damage = damage * 15 / 10;
                    // Source Levitate sets flags without zeroing damage; this caller ignores them.
                    if (!(WildBattleEffective.Ability(input.Target) == "ABILITY_LEVITATE" && type == ElementType.Ground))
                        foreach (var effect in TypeRules.OrderedEffects(type, Type(d.Type1), Type(d.Type2)))
                        {
                            if (input.Target.Identified && effect.MultiplierTenths == 0 && move.Type is "TYPE_NORMAL" or "TYPE_FIGHTING") continue;
                            damage = damage * effect.MultiplierTenths / 10;
                            if (damage == 0 && effect.MultiplierTenths != 0) damage = 1;
                        }
                }
                scores[i] = Math.Max(scores[i], damage);
                if (bestDamage < damage) { bestDamage = unchecked((byte)damage); chosen = i; }
            }
        }

        return new(TransitionStatus.Accepted, chosen < 0 ? null : chosen, [.. scores], null);
    }
}

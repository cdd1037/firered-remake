using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Pinned singles faint replacement, including defensive-type ranking, stale-current-move,
/// active-stat and byte best-damage source bugs. This does not introduce voluntary switching.</summary>
public static class MtMoonTrainerAiReplacement
{
    public static ForestTrainerReplacementDecision Choose(PokemonCatalog catalog, ForestTrainerReplacementInput input)
    {
        ForestTrainerReplacementDecision Fault(string code) => new(TransitionStatus.Fault, null, [],
            new(code, "Input is outside the bounded MtMoon trainer replacement profile."));
        string? error = Validate(catalog, input);
        if (error is not null) return Fault(error);
        var active = input.FaintedActive.Pokemon;
        var a = catalog.Species[active.Species]; var d = catalog.Species[input.Target.Pokemon.Species];
        ElementType Type(string value) => Enum.Parse<ElementType>(value[5..], true);
        bool Living(int index) => index != input.FaintedPartyIndex && input.Party[index].Pokemon.CurrentHp > 0;
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
                if (move.Power == 0 || input.Target.Pokemon.AbilityId == "ABILITY_LEVITATE" && move.Type == "TYPE_GROUND") continue;
                int value = 10;
                foreach (var effect in TypeRules.OrderedEffects(Type(move.Type), Type(d.Type1), Type(d.Type2)))
                    value = value * effect.MultiplierTenths / 10;
                if (value > 10) return new(TransitionStatus.Accepted, best, [.. scores], null);
            }
            invalid[best] = true;
        }
        bool none = input.StaleCurrentMove == ForestTrainerCombatRules.NoMove;
        bool struggle = input.StaleCurrentMove == WildBattleRules.StruggleMove;
        var stale = none || struggle ? null : catalog.Moves[input.StaleCurrentMove];
        int power = none ? 0 : struggle ? 50 : stale!.Power;
        var staleType = none || struggle ? ElementType.Normal : Type(stale!.Type);
        bool physical = TypeRules.Category(staleType) == DamageCategory.Physical;
        // Faint cleanup reset active stages/status, and EXP cleared its ability/item. The source
        // nevertheless calculates from that fainted active and stale gCurrentMove power.
        int attack = physical ? active.Stats.Attack : active.Stats.SpecialAttack;
        int defense = physical ? input.Target.Pokemon.Stats.Defense : input.Target.Pokemon.Stats.SpecialDefense;
        if (stale?.Effect == "EFFECT_EXPLOSION") defense /= 2;
        defense = Math.Max(1, WildBattleDamage.Stat(defense,
            physical ? input.Target.DefenseStage : input.Target.SpecialDefenseStageDelta + 6));
        if (staleType == ElementType.Electric && input.Target.MudSport) power /= 2;
        int baseDamage = attack * power * (2 * active.Level / 5 + 2) / defense / 50;
        if (physical) baseDamage = Math.Max(1, baseDamage);
        baseDamage += 2;
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
                    if (!(input.Target.Pokemon.AbilityId == "ABILITY_LEVITATE" && type == ElementType.Ground))
                        foreach (var effect in TypeRules.OrderedEffects(type, Type(d.Type1), Type(d.Type2)))
                        {
                            damage = damage * effect.MultiplierTenths / 10;
                            if (damage == 0 && effect.MultiplierTenths != 0) damage = 1;
                        }
                }
                scores[i] = Math.Max(scores[i], damage);
                if (bestDamage < damage) { bestDamage = unchecked((byte)damage); chosen = i; }
            }
        }
        if (chosen < 0) chosen = Enumerable.Range(0, input.Party.Length).First(Living);
        return new(TransitionStatus.Accepted, chosen, [.. scores], null);
    }
    private static string? Validate(PokemonCatalog? catalog, ForestTrainerReplacementInput? input)
    {
        if (catalog?.Species.Count != 56 || input is null || input.Party.IsDefaultOrEmpty || input.Party.Length > 4 ||
            input.FaintedPartyIndex < 0 || input.FaintedPartyIndex >= input.Party.Length || input.FaintedActive is null || input.Target is null)
            return "mtmoon_trainer_replacement_input";
        if (input.Party.Any(mon => mon is null || mon.HeldItem != "ITEM_NONE" || mon.AttackBadgeBoost) || input.FaintedActive.HeldItem != "ITEM_NONE")
            return "mtmoon_trainer_replacement_enemy_item";
        foreach (var mon in input.Party.Append(input.FaintedActive).Append(input.Target))
            if (MtMoonTrainerAi.ValidateCombatant(catalog, mon) is not null) return "mtmoon_trainer_replacement_combatant";
        if (input.FaintedActive.Pokemon.CurrentHp != 0 || input.Target.Pokemon.CurrentHp == 0) return "mtmoon_trainer_replacement_faint_state";
        var cleared = input.FaintedActive;
        if (cleared.Status != 0 || cleared.AttackStage != 6 || cleared.DefenseStage != 6 || cleared.SpeedStage != 6 ||
            cleared.AccuracyStage != 6 || cleared.EvasionStage != 6 || cleared.DefenseCurled || cleared.BindTurnsRemaining != 0 ||
            cleared.LeechSeedSource is not null || cleared.ConfusionTurnsRemaining != 0 || cleared.FocusEnergy || cleared.Flinched ||
            !WildBattleRules.Route3Neutral(cleared) || !WildBattleRules.MtMoonNeutral(cleared))
            return "mtmoon_trainer_replacement_faint_cleanup";
        var active = input.Party[input.FaintedPartyIndex].Pokemon;
        if (active with { Moves = input.FaintedActive.Pokemon.Moves } != input.FaintedActive.Pokemon ||
            !active.Moves.SequenceEqual(input.FaintedActive.Pokemon.Moves)) return "mtmoon_trainer_replacement_active_identity";
        if (!input.Party.Where((_, i) => i != input.FaintedPartyIndex).Any(x => x.Pokemon.CurrentHp > 0)) return "mtmoon_trainer_replacement_no_reserve";
        if (!SupportedParty(catalog, input.Party)) return "mtmoon_trainer_replacement_party_scope";
        if (input.StaleCurrentMove.Value is not ("pret:MOVE_NONE" or "pret:MOVE_STRUGGLE") &&
            (!catalog.Moves.TryGetValue(input.StaleCurrentMove, out var move) || !MtMoonBattleScope.ValidDefinition(move)))
            return "mtmoon_trainer_replacement_stale_move";
        return null;
    }
    private static bool SupportedParty(PokemonCatalog catalog, ImmutableArray<WildBattleCombatant> party)
    {
        var roster = party.Select(m => (m.Pokemon.SourceSpeciesId, m.Pokemon.Level));
        if (MtMoonTrainerPartyFactory.SupportedTrainers.Any(id => MtMoonTrainerPartyFactory.Expected(id).Party.SequenceEqual(roster)))
            return party.All(m => MtMoonTrainerAi.ValidUser(catalog, m.Pokemon));
        if (Route3TrainerPartyFactory.SupportedTrainers.Any(id => Route3TrainerPartyFactory.Expected(id).Party.SequenceEqual(roster)))
            return party.All(m => Route3TrainerAi.ValidUser(catalog, m.Pokemon));
        if (party.Length == 2 && party[0].Pokemon.SourceSpeciesId == 16 && party.All(m => Route22TrainerAi.ValidUser(m.Pokemon)))
            return party[1].Pokemon.SourceSpeciesId is 1 or 4 or 7;
        foreach (uint flags in new[] { 1u, 7u })
            if (roster.SequenceEqual(flags == 1 ? new[] { (74, 10), (27, 11) } : [(74, 12), (95, 14)]))
                return party.All(m => m.Pokemon.Ivs == SixStats.Zero && m.Pokemon.Evs == SixStats.Zero &&
                    m.Pokemon.Moves.All(x => x.PpUps == 0) && PewterTrainerAi.ValidUser(m.Pokemon, flags));
        (int, int)[][] forest = [[(13, 6), (10, 6)], [(13, 7), (14, 7), (13, 7)], [(13, 9)], [(10, 7), (10, 8)], [(11, 7), (10, 7), (11, 7)]];
        return forest.Any(r => r.SequenceEqual(roster)) && party.All(m => m.Pokemon.Ivs == SixStats.Zero && m.Pokemon.Evs == SixStats.Zero &&
            m.Pokemon.Moves.All(x => x.PpUps == 0) && m.Pokemon.Moves.Select(x => x.Move).SequenceEqual(
                PokemonMoveRules.InitialMoves(catalog, catalog.Species[m.Pokemon.Species], m.Pokemon.Level).Select(x => x.Move)));
    }
}

using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Pinned singles faint replacement, including defensive-type ranking, stale-current-move,
/// active-stat and byte best-damage source bugs. Voluntary switching uses CeruleanTrainerActionAi.</summary>
public static class CeruleanTrainerAiReplacement
{
    public static ForestTrainerReplacementDecision Choose(PokemonCatalog catalog, ForestTrainerReplacementInput input, bool billRules = false, bool vermilionRules = false, bool ssAnneRules = false, bool vermilionGymRules = false, bool route11Rules = false, bool rain = false, bool rockTunnelRules = false, bool reflect = false, bool celadonGymRules = false)
    {
        rockTunnelRules |= celadonGymRules;
        route11Rules |= rockTunnelRules;
        vermilionGymRules |= route11Rules; ssAnneRules |= vermilionGymRules; vermilionRules |= ssAnneRules; billRules |= vermilionRules;
        ForestTrainerReplacementDecision Fault(string code) => new(TransitionStatus.Fault, null, [],
            new(code, "Input is outside the bounded MtMoon trainer replacement profile."));
        if (catalog is null || (catalog.Species.Count == 98) != celadonGymRules) return Fault("celadon_gym_trainer_ai_catalog_scope");
        string? error = Validate(catalog, input, billRules, vermilionRules, ssAnneRules, vermilionGymRules, route11Rules, rockTunnelRules);
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
        // Source AI_CalcDmg reads active stats, stale gCurrentMove and cleared faint ability.
        int baseDamage = CeruleanTrainerAi.BasePrediction(input.FaintedActive, input.Target,
            power, staleType, stale?.Effect, clearUserAbility: true, rain: route11Rules && rain, reflect: rockTunnelRules && reflect);
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
    private static string? Validate(PokemonCatalog? catalog, ForestTrainerReplacementInput? input, bool billRules, bool vermilionRules, bool ssAnneRules, bool vermilionGymRules, bool route11Rules, bool rockTunnelRules = false)
    {
        route11Rules |= rockTunnelRules;
        if (catalog?.Species.Count != (rockTunnelRules ? CeladonApproachBattleScope.SharedCatalogCount(catalog) : route11Rules ? 79 : ssAnneRules ? 73 : vermilionRules ? 70 : billRules ? 68 : 65) || input is null || input.Party.IsDefaultOrEmpty || input.Party.Length > (catalog.Species.Count is 94 or 98 && rockTunnelRules ? 6 : 4) ||
            input.FaintedPartyIndex < 0 || input.FaintedPartyIndex >= input.Party.Length || input.FaintedActive is null || input.Target is null)
            return "cerulean_trainer_replacement_input";
        if (input.Party.Any(mon => mon is null || mon.HeldItem != "ITEM_NONE" || mon.AttackBadgeBoost || mon.SpeedBadgeBoost) || input.FaintedActive.HeldItem != "ITEM_NONE")
            return "cerulean_trainer_replacement_enemy_item";
        foreach (var mon in input.Party.Append(input.FaintedActive).Append(input.Target))
            if (CeruleanTrainerAi.ValidateCombatant(catalog, mon, billRules, vermilionRules, ssAnneRules, vermilionGymRules, route11Rules, rockTunnelRules) is not null) return "cerulean_trainer_replacement_combatant";
        if (input.FaintedActive.Pokemon.CurrentHp != 0 || input.Target.Pokemon.CurrentHp == 0) return "cerulean_trainer_replacement_faint_state";
        var cleared = input.FaintedActive;
        if (cleared.Imprison || cleared.Status != 0 || cleared.AttackStage != 6 || cleared.DefenseStage != 6 || cleared.SpeedStage != 6 ||
            cleared.AccuracyStage != 6 || cleared.EvasionStage != 6 || cleared.DefenseCurled || cleared.BindTurnsRemaining != 0 ||
            cleared.LeechSeedSource is not null || cleared.ConfusionTurnsRemaining != 0 || cleared.FocusEnergy || cleared.Flinched ||
            !WildBattleRules.Route3Neutral(cleared) || !WildBattleRules.MtMoonNeutral(cleared) || cleared.WaterSport)
            return "cerulean_trainer_replacement_faint_cleanup";
        var active = input.Party[input.FaintedPartyIndex].Pokemon;
        if (active with { Moves = input.FaintedActive.Pokemon.Moves } != input.FaintedActive.Pokemon ||
            !active.Moves.SequenceEqual(input.FaintedActive.Pokemon.Moves)) return "cerulean_trainer_replacement_active_identity";
        if (!input.Party.Where((_, i) => i != input.FaintedPartyIndex).Any(x => x.Pokemon.CurrentHp > 0)) return "cerulean_trainer_replacement_no_reserve";
        if (!SupportedParty(catalog, input.Party, billRules: billRules, vermilionRules: vermilionRules, ssAnneRules: ssAnneRules, vermilionGymRules: vermilionGymRules, route11Rules: route11Rules, rockTunnelRules: rockTunnelRules)) return "cerulean_trainer_replacement_party_scope";
        if (input.StaleCurrentMove.Value is not ("pret:MOVE_NONE" or "pret:MOVE_STRUGGLE") &&
            (!catalog.Moves.TryGetValue(input.StaleCurrentMove, out var move) || !(rockTunnelRules ? CeladonApproachBattleScope.ValidDefinitionForCatalog(catalog, move) : route11Rules ? Route11BattleScope.ValidDefinition(move) : vermilionGymRules ? VermilionGymBattleScope.ValidDefinition(move) : ssAnneRules ? SsAnneBattleScope.ValidDefinition(move) : vermilionRules ? VermilionBattleScope.ValidDefinition(move) : billRules ? BillBattleScope.ValidDefinition(move) : CeruleanBattleScope.ValidDefinition(move))))
            return "cerulean_trainer_replacement_stale_move";
        return null;
    }
    internal static bool SupportedParty(PokemonCatalog catalog, ImmutableArray<WildBattleCombatant> party, Game.Domain.Ids.TrainerId? trainerId = null, bool billRules = false, bool vermilionRules = false, bool ssAnneRules = false, bool vermilionGymRules = false, bool route11Rules = false, bool rockTunnelRules = false)
    {
        route11Rules |= rockTunnelRules;
        vermilionGymRules |= route11Rules; ssAnneRules |= vermilionGymRules; vermilionRules |= ssAnneRules; billRules |= vermilionRules;
        if (party.IsDefaultOrEmpty || party.Any(p => p is null)) return false;
        if (rockTunnelRules && catalog.Species.Count is 94 or 98)
        {
            foreach (var id in CeladonGymTrainerPartyFactory.SupportedTrainers)
        {
            if (catalog.Species.Count != 98 || trainerId is {} requested && requested != id) continue;
            var e = CeladonGymTrainerPartyFactory.Expected[id];
            if (party.Length == e.Party.Length && party.All(p => CeladonGymTrainerPartyFactory.ValidPartyMember(catalog,p.Pokemon,id))) return true;
        }
        foreach (var id in CeladonApproachTrainerPartyFactory.SupportedTrainers)
            {
                if (trainerId is {} selected && selected != id) continue;
                var e = CeladonApproachTrainerPartyFactory.Expected[id];
                if (party.Length == e.Party.Length && party.Select(p => p.Pokemon.Species.Value).SequenceEqual(e.Party.Select(p => "pret:SPECIES_" + p.Symbol)) &&
                    party.All(p => CeladonApproachTrainerPartyFactory.ValidPartyMember(catalog,p.Pokemon,id))) return true;
            }
            if (trainerId is {} selectedCeladon && CeladonApproachTrainerPartyFactory.SupportedTrainers.Contains(selectedCeladon)) return false;
        }
        if (rockTunnelRules)
            foreach (var id in RockTunnelTrainerPartyFactory.SupportedTrainers)
                if ((trainerId is null || trainerId == id) && party.Length == RockTunnelTrainerPartyFactory.Expected[id].Party.Length &&
                    party.All(m => RockTunnelTrainerPartyFactory.ValidPartyMember(catalog,m.Pokemon,id,RockTunnelTrainerPartyFactory.Expected[id].AiFlags))) return true;
        if (route11Rules)
        {
            foreach (var id in Route11TrainerPartyFactory.SupportedTrainers)
            {
                if (trainerId is {} selected && selected != id) continue;
                var e = Route11TrainerPartyFactory.Expected[id];
                if (party.Select(p => (p.Pokemon.SourceSpeciesId,p.Pokemon.Level)).SequenceEqual(e.Party.Select(p => (p.Species,p.Level))) &&
                    party.All(p => Route11TrainerPartyFactory.ValidPartyMember(catalog,p.Pokemon,id))) return true;
            }
            if (trainerId is {} selectedRoute11 && Route11TrainerPartyFactory.SupportedTrainers.Contains(selectedRoute11)) return false;
        }
        if (vermilionGymRules)
        {
            foreach (var id in VermilionGymTrainerPartyFactory.SupportedTrainers)
            {
                if (trainerId is {} selected && selected != id) continue;
                var e = VermilionGymTrainerPartyFactory.Expected[id];
                if (party.Length == e.Party.Length && party.Select(p => (p.Pokemon.SourceSpeciesId, p.Pokemon.Level)).SequenceEqual(e.Party.Select(p => (p.Species,p.Level))) &&
                    party.All(p => VermilionGymTrainerPartyFactory.ValidPartyMember(catalog,p.Pokemon,id))) return true;
            }
            if (trainerId is {} selectedGym && VermilionGymTrainerPartyFactory.SupportedTrainers.Contains(selectedGym)) return false;
        }
        if (ssAnneRules)
        {
            foreach (var id in SsAnneTrainerPartyFactory.SupportedTrainers)
            {
                if (trainerId is {} selected && selected != id) continue;
                var e = SsAnneTrainerPartyFactory.Expected[id];
                if (party.Length == e.Party.Length && party.Select(p => (p.Pokemon.SourceSpeciesId, p.Pokemon.Level)).SequenceEqual(e.Party.Select(p => (p.Species,p.Level))) &&
                    party.All(p => SsAnneTrainerPartyFactory.ValidPartyMember(catalog,p.Pokemon,id))) return true;
            }
            if (trainerId is {} selectedShip && SsAnneTrainerPartyFactory.SupportedTrainers.Contains(selectedShip)) return false;
        }
        if (vermilionRules)
        {
            foreach (var id in VermilionTrainerPartyFactory.SupportedTrainers)
            {
                if (trainerId is {} selected && selected != id) continue;
                var e=VermilionTrainerPartyFactory.Expected[id];
                if(party.Select(p=>(p.Pokemon.SourceSpeciesId,p.Pokemon.Level)).SequenceEqual(e.Party.Select(p=>(p.Species,p.Level))) &&
                    party.All(p=>VermilionTrainerPartyFactory.ValidPartyMember(catalog,p.Pokemon,id))) return true;
            }
            if(trainerId is {} selectedVermilion && VermilionTrainerPartyFactory.SupportedTrainers.Contains(selectedVermilion)) return false;
        }
        if (billRules || vermilionRules)
        {
            foreach (var id in BillTrainerPartyFactory.SupportedTrainers)
            {
                if (trainerId is {} selected && selected != id) continue;
                var e=BillTrainerPartyFactory.Expected[id];
                if(party.Select(p=>(p.Pokemon.SourceSpeciesId,p.Pokemon.Level)).SequenceEqual(e.Party.Select(p=>(p.Species,p.Level))) &&
                    party.All(p=>BillTrainerPartyFactory.ValidPartyMember(catalog,p.Pokemon,id))) return true;
            }
            if(trainerId is {} selectedBill && BillTrainerPartyFactory.SupportedTrainers.Contains(selectedBill)) return false;
        }
        foreach (var id in CeruleanTrainerPartyFactory.SupportedTrainers)
        {
            if (trainerId is { } selected && selected != id) continue;
            var expected = CeruleanTrainerPartyFactory.Expected(id);
            if (party.Select(p => (p.Pokemon.SourceSpeciesId,p.Pokemon.Level)).SequenceEqual(expected.Party.Select(p => (p.Species,p.Level))))
                return party.All(p => CeruleanTrainerAi.ValidPartyMember(catalog,p.Pokemon,id));
        }
        if (trainerId is { } cerulean && CeruleanTrainerPartyFactory.SupportedTrainers.Contains(cerulean)) return false;
        var roster = party.Select(m => (m.Pokemon.SourceSpeciesId, m.Pokemon.Level));
        if (MtMoonTrainerPartyFactory.SupportedTrainers.Any(id => (trainerId is null || trainerId == id) && MtMoonTrainerPartyFactory.Expected(id).Party.SequenceEqual(roster)))
            return party.All(m => MtMoonTrainerAi.ValidUser(catalog, m.Pokemon));
        if (Route3TrainerPartyFactory.SupportedTrainers.Any(id => (trainerId is null || trainerId == id) && Route3TrainerPartyFactory.Expected(id).Party.SequenceEqual(roster)))
            return party.All(m => Route3TrainerAi.ValidUser(catalog, m.Pokemon));
        if ((trainerId is null || Route22TrainerPartyFactory.SupportedTrainers.Contains(trainerId.Value)) && party.Length == 2 && party[0].Pokemon.SourceSpeciesId == 16 && party.All(m => Route22TrainerAi.ValidUser(m.Pokemon)))
            return party[1].Pokemon.SourceSpeciesId is 1 or 4 or 7 && (trainerId is null ||
                trainerId.Value.Value == "pret:TRAINER_RIVAL_ROUTE22_EARLY_" + CeruleanTrainerPartyFactory.SpeciesSymbol(party[1].Pokemon.SourceSpeciesId));
        foreach (uint flags in new[] { 1u, 7u })
            if ((trainerId is null || trainerId == (flags == 1 ? PewterTrainerPartyFactory.Liam : PewterTrainerPartyFactory.Brock)) && roster.SequenceEqual(flags == 1 ? new[] { (74, 10), (27, 11) } : [(74, 12), (95, 14)]))
                return party.All(m => m.Pokemon.Ivs == SixStats.Zero && m.Pokemon.Evs == SixStats.Zero &&
                    m.Pokemon.Moves.All(x => x.PpUps == 0) && PewterTrainerAi.ValidUser(m.Pokemon, flags));
        (int, int)[][] forest = [[(13, 6), (10, 6)], [(13, 7), (14, 7), (13, 7)], [(13, 9)], [(10, 7), (10, 8)], [(11, 7), (10, 7), (11, 7)]];
        return forest.Where((r,i) => trainerId is null || trainerId == ForestTrainerPartyFactory.SupportedTrainers[i]).Any(r => r.SequenceEqual(roster)) && party.All(m => m.Pokemon.Ivs == SixStats.Zero && m.Pokemon.Evs == SixStats.Zero &&
            m.Pokemon.Moves.All(x => x.PpUps == 0) && m.Pokemon.Moves.Select(x => x.Move).SequenceEqual(
                PokemonMoveRules.InitialMoves(catalog, catalog.Species[m.Pokemon.Species], m.Pokemon.Level).Select(x => x.Move)));
    }
}

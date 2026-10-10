using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Source CHECK_BAD_MOVE for the twelve MtMoon natural-move parties. Independent ability
/// reads and source penalties only; no viability/faint bonuses at flags 1.</summary>
public static class MtMoonTrainerAi
{
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string RulesVersion = "firered-mtmoon-trainer-ai-semantic-v1";

    public static ForestTrainerAiDecision Choose(PokemonCatalog catalog, ForestTrainerAiInput input, RngState rng, bool ceruleanRules = false, bool billRules = false, bool vermilionRules = false, bool ssAnneRules = false, bool vermilionGymRules = false, bool route11Rules = false, bool rockTunnelRules = false)
    {
        route11Rules |= rockTunnelRules;
        ForestTrainerAiDecision Fault(string code) => new(TransitionStatus.Fault, null, [], [], rng, [],
            new(code, "Input is outside the bounded MtMoon trainer AI profile."));
        vermilionGymRules |= route11Rules; ssAnneRules |= vermilionGymRules;
        vermilionRules |= ssAnneRules;
        billRules |= vermilionRules;
        ceruleanRules |= billRules;
        string? error = Validate(catalog, input, ceruleanRules, billRules, vermilionRules, ssAnneRules, vermilionGymRules, route11Rules, rockTunnelRules);
        if (error is not null) return Fault(error);
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) return Fault("mtmoon_trainer_ai_rng_algorithm");
        bool Usable(int slot) => WildBattleRules.IsMoveSelectable(input.User, slot) && (!route11Rules || !input.Target.Imprison || slot >= input.User.Pokemon.Moves.Length || !input.Target.Pokemon.Moves.Any(m => m.Move == input.User.Pokemon.Moves[slot].Move));
        if (!Enumerable.Range(0, 4).Any(Usable)) return Fault("mtmoon_trainer_ai_struggle_required");
        var current = rng;
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        var reads = ImmutableArray.CreateBuilder<ForestTrainerAbilityRead>();
        int Draw(string purpose, int bound)
        {
            var next = Gen3Rng.Next(current, purpose, bound);
            current = next.State; trace.Add(next.Trace); return next.Trace.Result;
        }
        var targetSpecies = catalog.Species[input.Target.Pokemon.Species];
        string Ability(int slot, string reason)
        {
            // Cmd_get_ability consults side history, then reveals actual trapping abilities,
            // otherwise independently guesses a dual ability on every script instruction.
            bool trappedAbility = input.Target.Pokemon.AbilityId == "ABILITY_MAGNET_PULL" || route11Rules && input.Target.Pokemon.AbilityId == "ABILITY_ARENA_TRAP";
            bool guess = input.KnownTargetAbility is null && !trappedAbility && targetSpecies.Ability2 != "ABILITY_NONE";
            string ability = input.KnownTargetAbility ?? (guess
                ? Draw($"mtmoon-trainer.ai.slot.{slot}.ability.{reason}", 2) != 0 ? targetSpecies.Ability1 : targetSpecies.Ability2
                : trappedAbility ? input.Target.Pokemon.AbilityId : targetSpecies.Ability1);
            reads.Add(new(slot, reason, ability, guess));
            return ability;
        }
        try
        {
            var scores = new int[4]; var simulated = new int[4];
            for (int slot = 0; slot < 4; slot++)
            {
                scores[slot] = Usable(slot) ? 100 : 0;
                simulated[slot] = 100 - Draw($"mtmoon-trainer.ai.setup.slot.{slot}.damage-percent", 16);
            }
            void Score(int slot, int change) => scores[slot] = Math.Max(0, scores[slot] + change);
            for (int slot = 0; slot < 4; slot++)
            {
                if (!Usable(slot)) continue;
                var move = catalog.Moves[input.User.Pokemon.Moves[slot].Move];
                if (move.Power > 1)
                {
                    int effectiveness = TypeEffectiveness(move.Type, targetSpecies);
                    if (effectiveness == 0) { Score(slot, -10); continue; }
                    string ability = Ability(slot, "damage-immunity");
                    if ((ability == "ABILITY_VOLT_ABSORB" && move.Type == "TYPE_ELECTRIC") ||
                        (ability == "ABILITY_WATER_ABSORB" && move.Type == "TYPE_WATER") ||
                        (ability == "ABILITY_FLASH_FIRE" && move.Type == "TYPE_FIRE"))
                    { Score(slot, -12); continue; }
                    if ((ability == "ABILITY_WONDER_GUARD" && effectiveness != 20) ||
                        (ability == "ABILITY_LEVITATE" && move.Type == "TYPE_GROUND"))
                    { Score(slot, -10); continue; }
                }
                string soundAbility = Ability(slot, "soundproof");
                if (soundAbility == "ABILITY_SOUNDPROOF" && IsSoundMove(move.Id))
                { Score(slot, -10); continue; }
                switch (move.Effect)
                {
                    case "EFFECT_SLEEP":
                        if (Ability(slot, "sleep-immunity") is "ABILITY_INSOMNIA" or "ABILITY_VITAL_SPIRIT" || input.Target.Status != 0) Score(slot, -10);
                        break;
                    case "EFFECT_DISABLE":
                        if (input.Target.DisabledMove is not null) Score(slot, -8);
                        break;
                    case "EFFECT_PARALYZE":
                        if (TypeEffectiveness(move.Type, targetSpecies) == 0) { Score(slot, -10); break; }
                        if (Ability(slot, "paralysis-immunity") == "ABILITY_LIMBER" || input.Target.Status != 0) Score(slot, -10);
                        break;
                    case "EFFECT_ATTACK_DOWN":
                        if (input.Target.AttackStage == 0) { Score(slot, -10); break; }
                        if (Ability(slot, "attack-ability") == "ABILITY_HYPER_CUTTER") { Score(slot, -10); break; }
                        if (Ability(slot, "stat-change") is "ABILITY_CLEAR_BODY" or "ABILITY_WHITE_SMOKE") Score(slot, -10);
                        break;
                    case "EFFECT_DEFENSE_DOWN_2":
                    case "EFFECT_DEFENSE_DOWN":
                        if (input.Target.DefenseStage == 0) { Score(slot, -10); break; }
                        if (Ability(slot, "stat-change") is "ABILITY_CLEAR_BODY" or "ABILITY_WHITE_SMOKE") Score(slot, -10);
                        break;
                    case "EFFECT_ACCURACY_DOWN":
                        if (input.Target.AccuracyStage == 0) { Score(slot, -10); break; }
                        if (Ability(slot, "accuracy-ability") == "ABILITY_KEEN_EYE") { Score(slot, -10); break; }
                        if (Ability(slot, "stat-change") is "ABILITY_CLEAR_BODY" or "ABILITY_WHITE_SMOKE") Score(slot, -10);
                        break;
                    case "EFFECT_ENCORE":
                        if (input.Target.EncoreTurnsRemaining > 0) Score(slot, -8);
                        break;
                    case "EFFECT_SPECIAL_ATTACK_UP":
                        if (input.User.SpecialAttackStageDelta == 6) Score(slot, -10);
                        break;
                    case "EFFECT_SPECIAL_DEFENSE_DOWN_2":
                        if (input.Target.SpecialDefenseStageDelta == -6) { Score(slot, -10); break; }
                        if (Ability(slot, "stat-change") is "ABILITY_CLEAR_BODY" or "ABILITY_WHITE_SMOKE") Score(slot, -10);
                        break;
                    case "EFFECT_EVASION_DOWN":
                        if (input.Target.EvasionStage == 0) { Score(slot, -10); break; }
                        if (Ability(slot, "stat-change") is "ABILITY_CLEAR_BODY" or "ABILITY_WHITE_SMOKE") Score(slot, -10);
                        break;
                    case "EFFECT_CONFUSE":
                        if (input.Target.ConfusionTurnsRemaining > 0) { Score(slot, -5); break; }
                        if (Ability(slot, "confusion-immunity") == "ABILITY_OWN_TEMPO") Score(slot, -10);
                        break;
                    case "EFFECT_POISON":
                        if (targetSpecies.Type1 is "TYPE_POISON" or "TYPE_STEEL" ||
                            targetSpecies.Type2 is "TYPE_POISON" or "TYPE_STEEL") { Score(slot, -10); break; }
                        if (Ability(slot, "poison-immunity") == "ABILITY_IMMUNITY" || input.Target.Status != 0) Score(slot, -10);
                        break;
                    case "EFFECT_MUD_SPORT":
                        if (input.User.MudSport) Score(slot, -10);
                        break;
                    case "EFFECT_FOCUS_ENERGY":
                        if (input.User.FocusEnergy) Score(slot, -10);
                        break;
                    case "EFFECT_DEFENSE_CURL":
                    case "EFFECT_DEFENSE_UP":
                        if (input.User.DefenseStage == 12) Score(slot, -10);
                        break;
                    case "EFFECT_SPEED_DOWN":
                        if (input.Target.SpeedStage == 0) { Score(slot, -10); break; }
                        string ability = Ability(slot, "stat-change");
                        if (ability is "ABILITY_CLEAR_BODY" or "ABILITY_WHITE_SMOKE") Score(slot, -10);
                        break;
                    // Explicit no-dispatch effects from the actual initial parties.
                    case "EFFECT_HIT": case "EFFECT_QUICK_ATTACK": case "EFFECT_POISON_HIT":
                    case "EFFECT_PARALYZE_HIT": case "EFFECT_FLINCH_HIT": case "EFFECT_FLINCH_MINIMIZE_HIT":
                    case "EFFECT_TRAP": case "EFFECT_ABSORB": case "EFFECT_MULTI_HIT": case "EFFECT_CHARGE":
                        break;
                    default: return Fault("mtmoon_trainer_ai_effect_outside_source_parties");
                }
            }
            var best = new List<int> { 0 }; int maximum = scores[0];
            for (int slot = 1; slot < 4; slot++)
            {
                if (scores[slot] > maximum) { maximum = scores[slot]; best.Clear(); best.Add(slot); }
                if (scores[slot] == maximum) best.Add(slot);
            }
            int chosen = best[Draw("mtmoon-trainer.ai.choose-best", best.Count)];
            return new(TransitionStatus.Accepted, chosen, [.. scores], [.. simulated], current, trace.ToImmutable(), null)
                { AbilityReads = reads.ToImmutable() };
        }
        catch (OverflowException) { return Fault("mtmoon_trainer_ai_rng_exhausted"); }
    }

    private static int TypeEffectiveness(string moveType, PokemonSpeciesDefinition target)
    {
        ElementType Type(string value) => Enum.Parse<ElementType>(value["TYPE_".Length..], true);
        int value = 10;
        foreach (var effect in TypeRules.OrderedEffects(Type(moveType), Type(target.Type1), Type(target.Type2)))
            value = value * effect.MultiplierTenths / 10;
        return value;
    }
    private static string? Validate(PokemonCatalog? catalog, ForestTrainerAiInput? input, bool ceruleanRules, bool billRules, bool vermilionRules, bool ssAnneRules, bool vermilionGymRules, bool route11Rules, bool rockTunnelRules = false)
    {
        route11Rules |= rockTunnelRules;
        if (input is null) return "mtmoon_trainer_ai_input_missing";
        if (input.AiFlags != 1) return "mtmoon_trainer_ai_flags_unsupported";
        if (catalog?.Species.Count != (rockTunnelRules ? CeladonApproachBattleScope.SharedCatalogCount(catalog) : route11Rules ? 79 : ssAnneRules ? 73 : vermilionRules ? 70 : billRules ? 68 : ceruleanRules ? 65 : 56)) return "mtmoon_trainer_ai_catalog_scope";
        foreach (var (name, mon) in new[] { ("user", input.User), ("target", input.Target) })
        {
            string? error = ValidateCombatant(catalog, mon, ceruleanRules, billRules, vermilionRules, ssAnneRules, vermilionGymRules, route11Rules, rockTunnelRules);
            if (error is not null) return "mtmoon_trainer_ai_" + name + "_" + error;
            if (mon.Pokemon.CurrentHp == 0) return "mtmoon_trainer_ai_" + name + "_fainted";
        }
        if (!ValidUser(catalog, input.User.Pokemon) || input.User.SpeedBadgeBoost || input.User.AttackBadgeBoost || input.User.HeldItem != "ITEM_NONE") return "mtmoon_trainer_ai_user_roster";
        foreach (var slot in input.User.Pokemon.Moves)
            if (!MtMoonBattleScope.ValidDefinition(catalog.Moves[slot.Move])) return "mtmoon_trainer_ai_move_definition";
        if (input.KnownTargetAbility is { } known && !(rockTunnelRules ? RockTunnelBattleScope.SupportsAbility(known) : route11Rules ? Route11BattleScope.SupportsAbility(known) : ssAnneRules ? SsAnneBattleScope.SupportsAbility(known) : vermilionRules ? VermilionBattleScope.SupportsAbility(known) : billRules ? BillBattleScope.SupportsAbility(known) : ceruleanRules ? CeruleanTrainerAi.SupportsAbility(known) : SupportsAbility(known))) return "mtmoon_trainer_ai_ability_history";
        return null;
    }
    internal static string? ValidateCombatant(PokemonCatalog? catalog, WildBattleCombatant? mon, bool ceruleanRules = false, bool billRules = false, bool vermilionRules = false, bool ssAnneRules = false, bool vermilionGymRules = false, bool route11Rules = false, bool rockTunnelRules = false) =>
        ceruleanRules ? CeruleanTrainerAi.ValidateCombatant(catalog, mon, billRules, vermilionRules, ssAnneRules, vermilionGymRules, route11Rules, rockTunnelRules) : WildBattleRules.ValidateCombatant(catalog!, mon!, "mtmoon-ai", true, true, true, true, true, true, true) ??
        (!ValidSpecies(catalog!.Species[mon!.Pokemon.Species]) ? "mtmoon_ai_species_type_ability" : null);
    internal static bool ValidUser(PokemonCatalog catalog, PokemonInstance mon) =>
        MtMoonTrainerPartyFactory.SupportedTrainers.Any(id => MtMoonTrainerPartyFactory.Expected(id).Party.Contains((mon.SourceSpeciesId, mon.Level))) &&
        mon.Ivs == SixStats.Zero && mon.Evs == SixStats.Zero && mon.Moves.All(m => m.PpUps == 0) &&
        mon.Moves.Select(x => x.Move).SequenceEqual(PokemonMoveRules.InitialMoves(catalog, catalog.Species[mon.Species], mon.Level).Select(x => x.Move));
    internal static bool IsSoundMove(MoveId move) => move.Value is "pret:MOVE_GROWL" or "pret:MOVE_ROAR" or
        "pret:MOVE_SING" or "pret:MOVE_SUPERSONIC" or "pret:MOVE_SCREECH" or "pret:MOVE_SNORE" or
        "pret:MOVE_UPROAR" or "pret:MOVE_METAL_SOUND" or "pret:MOVE_GRASS_WHISTLE";
    internal static bool SupportsAbility(string ability) => Route3TrainerAi.SupportsAbility(ability) || ability is
        "ABILITY_INNER_FOCUS" or "ABILITY_CHLOROPHYLL" or "ABILITY_EFFECT_SPORE" or "ABILITY_MAGNET_PULL" or
        "ABILITY_STENCH" or "ABILITY_STICKY_HOLD" or "ABILITY_SOUNDPROOF" or "ABILITY_LEVITATE";
    internal static bool ValidSpecies(PokemonSpeciesDefinition s) => Route3TrainerAi.ValidSpecies(s) || s.SourceSpeciesId switch
    {
        24 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_POISON", "TYPE_POISON", "ABILITY_INTIMIDATE", "ABILITY_SHED_SKIN"),
        35 or 36 or 40 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_NORMAL", "TYPE_NORMAL", "ABILITY_CUTE_CHARM", "ABILITY_NONE"),
        31 or 34 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_POISON", "TYPE_GROUND", "ABILITY_POISON_POINT", "ABILITY_NONE"),
        41 or 42 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_POISON", "TYPE_FLYING", "ABILITY_INNER_FOCUS", "ABILITY_NONE"),
        43 or 69 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_GRASS", "TYPE_POISON", "ABILITY_CHLOROPHYLL", "ABILITY_NONE"),
        46 or 47 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_BUG", "TYPE_GRASS", "ABILITY_EFFECT_SPORE", "ABILITY_NONE"),
        81 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_ELECTRIC", "TYPE_STEEL", "ABILITY_MAGNET_PULL", "ABILITY_STURDY"),
        88 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_POISON", "TYPE_POISON", "ABILITY_STENCH", "ABILITY_STICKY_HOLD"),
        100 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_ELECTRIC", "TYPE_ELECTRIC", "ABILITY_SOUNDPROOF", "ABILITY_STATIC"),
        109 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_POISON", "TYPE_POISON", "ABILITY_LEVITATE", "ABILITY_NONE"),
        _ => false
    };
}

using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Source CHECK_BAD_MOVE for eight Route3 natural-move parties. Sleep, Disable and stat
/// checks retain source read order and penalties; flags 1 grants no viability/faint bonuses.</summary>
public static class Route3TrainerAi
{
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string RulesVersion = "firered-route3-trainer-ai-semantic-v1";

    public static ForestTrainerAiDecision Choose(PokemonCatalog catalog, ForestTrainerAiInput input, RngState rng, bool mtMoonRules = false, bool ceruleanRules = false, bool billRules = false, bool vermilionRules = false, bool ssAnneRules = false, bool vermilionGymRules = false, bool route11Rules = false, bool rockTunnelRules = false, bool celadonGymRules = false, bool rocketHideoutRules = false, bool towerRules = false)
    {
        rocketHideoutRules |= towerRules;
        celadonGymRules |= rocketHideoutRules;
        rockTunnelRules |= celadonGymRules;
        route11Rules |= rockTunnelRules;
        ForestTrainerAiDecision Fault(string code) => new(TransitionStatus.Fault, null, [], [], rng, [],
            new(code, "Input is outside the bounded Route3 trainer AI profile."));
        vermilionGymRules |= route11Rules; ssAnneRules |= vermilionGymRules;
        vermilionRules |= ssAnneRules;
        billRules |= vermilionRules;
        ceruleanRules |= billRules;
        mtMoonRules |= ceruleanRules;
        if (catalog is null || (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 98) != (celadonGymRules && !rocketHideoutRules) || (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 99) != (rocketHideoutRules && !towerRules) || (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) is 101 or 102) != towerRules) return Fault("celadon_gym_trainer_ai_catalog_scope");
        string? error = Validate(catalog, input, mtMoonRules || ceruleanRules, ceruleanRules, billRules, vermilionRules, ssAnneRules, vermilionGymRules, route11Rules, rockTunnelRules);
        if (error is not null) return Fault(error);
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) return Fault("route3_trainer_ai_rng_algorithm");
        bool Usable(int slot) => WildBattleRules.IsMoveSelectable(input.User, slot) && (!route11Rules || !input.Target.Imprison || slot >= input.User.Pokemon.Moves.Length || !input.Target.Pokemon.Moves.Any(m => m.Move == input.User.Pokemon.Moves[slot].Move));
        if (!Enumerable.Range(0, 4).Any(Usable)) return Fault("route3_trainer_ai_struggle_required");

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
            // Cmd_get_ability executes this independently at every script instruction.
            // For unknown Rattata/Raticate, odd chooses ability 0, even chooses ability 1.
            bool trappedAbility = mtMoonRules && input.Target.Pokemon.AbilityId == "ABILITY_MAGNET_PULL" || route11Rules && input.Target.Pokemon.AbilityId == "ABILITY_ARENA_TRAP";
            bool guess = input.KnownTargetAbility is null && !trappedAbility && targetSpecies.Ability2 != "ABILITY_NONE";
            string ability = input.KnownTargetAbility ?? (guess
                ? Draw($"route3-trainer.ai.slot.{slot}.ability.{reason}", 2) != 0 ? targetSpecies.Ability1 : targetSpecies.Ability2
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
                simulated[slot] = 100 - Draw($"route3-trainer.ai.setup.slot.{slot}.damage-percent", 16);
            }
            void Score(int slot, int change) => scores[slot] = Math.Max(0, scores[slot] + change);
            for (int slot = 0; slot < 4; slot++)
            {
                if (!Usable(slot)) continue;
                var move = catalog.Moves[input.User.Pokemon.Moves[slot].Move];
                // The admitted damaging effects have no discouraged-power special case. A status
                // move is discouraged and skips the damage-only immunity/ability branch.
                if (move.Power > 1)
                {
                    int effectiveness = TypeEffectiveness(move.Type, targetSpecies);
                    if (effectiveness == 0) { Score(slot, -10); continue; }
                    string ability = Ability(slot, "damage-immunity");
                    // These immunity holders are not among the admitted species, but retain
                    // the source branch semantics; malformed species metadata is rejected.
                    if ((ability == "ABILITY_VOLT_ABSORB" && move.Type == "TYPE_ELECTRIC") ||
                        (ability == "ABILITY_WATER_ABSORB" && move.Type == "TYPE_WATER") ||
                        (ability == "ABILITY_FLASH_FIRE" && move.Type == "TYPE_FIRE"))
                    { Score(slot, -12); continue; }
                    if ((ability == "ABILITY_WONDER_GUARD" && effectiveness != 20) ||
                        (ability == "ABILITY_LEVITATE" && move.Type == "TYPE_GROUND"))
                    { Score(slot, -10); continue; }
                }
                // Every admitted move reaches get_ability at CheckSoundproof, including non-sound moves.
                // No admitted current/history ability is Soundproof.
                string soundAbility = Ability(slot, "soundproof");
                if (mtMoonRules && soundAbility == "ABILITY_SOUNDPROOF" && MtMoonTrainerAi.IsSoundMove(move.Id))
                { Score(slot, -10); continue; }
                switch (move.Effect)
                {
                    case "EFFECT_SLEEP":
                        if (Ability(slot, "sleep-immunity") is "ABILITY_INSOMNIA" or "ABILITY_VITAL_SPIRIT" || input.Target.Status != 0) Score(slot, -10);
                        break;
                    case "EFFECT_DISABLE":
                        // CHECK_BAD_MOVE does not penalize missing last-used move.
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
                    case "EFFECT_DEFENSE_DOWN":
                        if (input.Target.DefenseStage == 0) { Score(slot, -10); break; }
                        if (Ability(slot, "stat-change") is "ABILITY_CLEAR_BODY" or "ABILITY_WHITE_SMOKE") Score(slot, -10);
                        break;
                    case "EFFECT_ACCURACY_DOWN":
                        if (input.Target.AccuracyStage == 0) { Score(slot, -10); break; }
                        if (Ability(slot, "accuracy-ability") == "ABILITY_KEEN_EYE") { Score(slot, -10); break; }
                        if (Ability(slot, "stat-change") is "ABILITY_CLEAR_BODY" or "ABILITY_WHITE_SMOKE") Score(slot, -10);
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
                    // EFFECT_POISON_HIT is deliberately not EFFECT_POISON. Poison typing,
                    // Shield Dust and an existing status do not penalize Poison Sting.
                }
            }
            var best = new List<int> { 0 }; int maximum = scores[0];
            for (int slot = 1; slot < 4; slot++)
            {
                // Independent source ifs duplicate a newly encountered maximum.
                if (scores[slot] > maximum) { maximum = scores[slot]; best.Clear(); best.Add(slot); }
                if (scores[slot] == maximum) best.Add(slot);
            }
            int chosen = best[Draw("route3-trainer.ai.choose-best", best.Count)];
            return new(TransitionStatus.Accepted, chosen, [.. scores], [.. simulated], current, trace.ToImmutable(), null)
                { AbilityReads = reads.ToImmutable() };
        }
        catch (OverflowException) { return Fault("route3_trainer_ai_rng_exhausted"); }
    }

    private static int TypeEffectiveness(string moveType, PokemonSpeciesDefinition target)
    {
        ElementType Type(string value) => Enum.Parse<ElementType>(value["TYPE_".Length..], true);
        int value = 10;
        foreach (var effect in TypeRules.OrderedEffects(Type(moveType), Type(target.Type1), Type(target.Type2)))
            value = value * effect.MultiplierTenths / 10;
        return value;
    }

    private static string? Validate(PokemonCatalog? catalog, ForestTrainerAiInput? input, bool mtMoonRules, bool ceruleanRules, bool billRules, bool vermilionRules, bool ssAnneRules, bool vermilionGymRules, bool route11Rules, bool rockTunnelRules = false)
    {
        route11Rules |= rockTunnelRules;
        if (input is null) return "route3_trainer_ai_input_missing";
        if (input.AiFlags != 1) return "route3_trainer_ai_flags_unsupported";
        if (catalog is null || Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) != (rockTunnelRules ? CeladonApproachBattleScope.SharedCatalogCount(catalog) : route11Rules ? 79 : ssAnneRules ? 73 : vermilionRules ? 70 : billRules ? 68 : ceruleanRules ? 65 : mtMoonRules ? 56 : 40)) return "route3_trainer_ai_catalog_scope";
        foreach (var (name, mon) in new[] { ("user", input.User), ("target", input.Target) })
        {
            string? error = ceruleanRules ? CeruleanTrainerAi.ValidateCombatant(catalog, mon, billRules, vermilionRules, ssAnneRules, vermilionGymRules, route11Rules, rockTunnelRules) : mtMoonRules ? MtMoonTrainerAi.ValidateCombatant(catalog, mon) : ValidateCombatant(catalog, mon);
            if (error is not null) return "route3_trainer_ai_" + name + "_" + error;
            if (mon.Pokemon.CurrentHp == 0) return "route3_trainer_ai_" + name + "_fainted";
        }
        if (!ValidUser(catalog, input.User.Pokemon) || input.User.SpeedBadgeBoost || input.User.AttackBadgeBoost || input.User.HeldItem != "ITEM_NONE") return "route3_trainer_ai_user_roster";
        foreach (var slot in input.User.Pokemon.Moves)
            if (!Route3BattleScope.ValidDefinition(catalog.Moves[slot.Move])) return "route3_trainer_ai_move_definition";
        if (input.KnownTargetAbility is { } known && !(rockTunnelRules ? (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 102 ? SnorlaxBattleScope.SupportsAbility(known) : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) is 101 or 102 ? RocketHideoutBattleScope.SupportsAbility(known) : RocketHideoutBattleScope.SupportsAbilityForCatalog(catalog, known)) : route11Rules ? Route11BattleScope.SupportsAbility(known) : ssAnneRules ? SsAnneBattleScope.SupportsAbility(known) : vermilionRules ? VermilionBattleScope.SupportsAbility(known) : billRules ? BillBattleScope.SupportsAbility(known) : ceruleanRules ? CeruleanTrainerAi.SupportsAbility(known) : mtMoonRules ? MtMoonTrainerAi.SupportsAbility(known) : SupportsAbility(known))) return "route3_trainer_ai_ability_history";
        return null;
    }
    internal static string? ValidateCombatant(PokemonCatalog? catalog, WildBattleCombatant? mon) =>
        WildBattleRules.ValidateCombatant(catalog!, mon!, "route3-ai", true, true, true, true, true, true) ??
        (!ValidSpecies(catalog!.Species[mon!.Pokemon.Species]) ? "route3_ai_species_type_ability" : null);
    internal static bool ValidUser(PokemonCatalog catalog, PokemonInstance mon) =>
        Route3TrainerPartyFactory.SupportedTrainers.Any(id => Route3TrainerPartyFactory.Expected(id).Party.Contains((mon.SourceSpeciesId, mon.Level))) &&
        mon.Ivs == SixStats.Zero && mon.Evs == SixStats.Zero && mon.Moves.All(m => m.PpUps == 0) &&
        mon.Moves.Select(x => x.Move).SequenceEqual(PokemonMoveRules.InitialMoves(catalog, catalog.Species[mon.Species], mon.Level).Select(x => x.Move));
    internal static bool SupportsAbility(string ability) => Route22TrainerAi.SupportsAbility(ability) ||
        ability is "ABILITY_INTIMIDATE" or "ABILITY_SWIFT_SWIM" or "ABILITY_POISON_POINT" or "ABILITY_CUTE_CHARM";
    internal static bool ValidSpecies(PokemonSpeciesDefinition species) => Route22TrainerAi.ValidSpecies(species) || species.SourceSpeciesId switch
    {
        23 => (species.Type1, species.Type2, species.Ability1, species.Ability2) == ("TYPE_POISON", "TYPE_POISON", "ABILITY_INTIMIDATE", "ABILITY_SHED_SKIN"),
        29 or 30 or 32 or 33 => (species.Type1, species.Type2, species.Ability1, species.Ability2) == ("TYPE_POISON", "TYPE_POISON", "ABILITY_POISON_POINT", "ABILITY_NONE"),
        39 => (species.Type1, species.Type2, species.Ability1, species.Ability2) == ("TYPE_NORMAL", "TYPE_NORMAL", "ABILITY_CUTE_CHARM", "ABILITY_NONE"),
        129 => (species.Type1, species.Type2, species.Ability1, species.Ability2) == ("TYPE_WATER", "TYPE_WATER", "ABILITY_SWIFT_SWIM", "ABILITY_NONE"),
        130 => (species.Type1, species.Type2, species.Ability1, species.Ability2) == ("TYPE_WATER", "TYPE_FLYING", "ABILITY_INTIMIDATE", "ABILITY_NONE"),
        _ => false
    };
}

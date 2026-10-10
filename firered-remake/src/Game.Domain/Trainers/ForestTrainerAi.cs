using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>The source aiFlags=1 CHECK_BAD_MOVE closure of the five Forest Bug Catchers.
/// No viability/faint bonuses, random wild slot selection, switch selection or frame-time RNG.</summary>
public static class ForestTrainerAi
{
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string RulesVersion = "firered-forest-trainer-ai-semantic-v1";

    public static ForestTrainerAiDecision Choose(PokemonCatalog catalog, ForestTrainerAiInput input, RngState rng, bool pewterRules = false, bool earlyStarterRules = false, bool route22Rules = false, bool route3Rules = false, bool mtMoonRules = false, bool ceruleanRules = false, bool billRules = false, bool vermilionRules = false, bool ssAnneRules = false, bool vermilionGymRules = false, bool route11Rules = false, bool rockTunnelRules = false)
    {
        route11Rules |= rockTunnelRules;
        ForestTrainerAiDecision Fault(string code) => new(TransitionStatus.Fault, null, [], [], rng, [],
            new(code, "Input is outside the supported forest trainer AI profile."));
        vermilionGymRules |= route11Rules; ssAnneRules |= vermilionGymRules;
        vermilionRules |= ssAnneRules;
        billRules |= vermilionRules;
        ceruleanRules |= billRules;
        mtMoonRules |= ceruleanRules;
        route3Rules |= mtMoonRules;
        route22Rules |= route3Rules;
        earlyStarterRules |= route22Rules;
        string? error = Validate(catalog, input, pewterRules || earlyStarterRules, earlyStarterRules, route22Rules, route3Rules, mtMoonRules, ceruleanRules, billRules, vermilionRules, ssAnneRules, vermilionGymRules, route11Rules, rockTunnelRules);
        if (error is not null) return Fault(error);
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) return Fault("forest_trainer_ai_rng_algorithm");
        bool Usable(int slot) => route3Rules ? WildBattleRules.IsMoveSelectable(input.User, slot) && (!route11Rules || !input.Target.Imprison || slot >= input.User.Pokemon.Moves.Length || !input.Target.Pokemon.Moves.Any(m => m.Move == input.User.Pokemon.Moves[slot].Move)) : slot < input.User.Pokemon.Moves.Length && input.User.Pokemon.Moves[slot].CurrentPp > 0;
        if (!Enumerable.Range(0, 4).Any(Usable)) return Fault("forest_trainer_ai_struggle_required");

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
                ? Draw($"forest-trainer.ai.slot.{slot}.ability.{reason}", 2) != 0 ? targetSpecies.Ability1 : targetSpecies.Ability2
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
                simulated[slot] = 100 - Draw($"forest-trainer.ai.setup.slot.{slot}.damage-percent", 16);
            }
            void Score(int slot, int change) => scores[slot] = Math.Max(0, scores[slot] + change);
            for (int slot = 0; slot < 4; slot++)
            {
                if (!Usable(slot)) continue;
                var move = catalog.Moves[input.User.Pokemon.Moves[slot].Move];
                // The four admitted effects have no discouraged-power special case. A status
                // move is discouraged and skips the damage-only immunity/ability branch.
                if (move.Power > 1)
                {
                    int effectiveness = TypeEffectiveness(move.Type, targetSpecies);
                    if (effectiveness == 0) { Score(slot, -10); continue; }
                    string ability = Ability(slot, "damage-immunity");
                    // These immunity holders are not among the closed 22 species, but retain
                    // the source branch semantics; malformed species metadata is rejected.
                    if ((ability == "ABILITY_VOLT_ABSORB" && move.Type == "TYPE_ELECTRIC") ||
                        (ability == "ABILITY_WATER_ABSORB" && move.Type == "TYPE_WATER") ||
                        (ability == "ABILITY_FLASH_FIRE" && move.Type == "TYPE_FIRE"))
                    { Score(slot, -12); continue; }
                    if ((ability == "ABILITY_WONDER_GUARD" && effectiveness != 20) ||
                        (ability == "ABILITY_LEVITATE" && move.Type == "TYPE_GROUND"))
                    { Score(slot, -10); continue; }
                }
                // Every admitted move reaches get_ability at CheckSoundproof, including
                // non-sound moves. None of the four moves matches its nine sound moves.
                string soundAbility = Ability(slot, "soundproof");
                if (mtMoonRules && soundAbility == "ABILITY_SOUNDPROOF" && MtMoonTrainerAi.IsSoundMove(move.Id))
                { Score(slot, -10); continue; }
                switch (move.Effect)
                {
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
            int chosen = best[Draw("forest-trainer.ai.choose-best", best.Count)];
            return new(TransitionStatus.Accepted, chosen, [.. scores], [.. simulated], current, trace.ToImmutable(), null)
                { AbilityReads = reads.ToImmutable() };
        }
        catch (OverflowException) { return Fault("forest_trainer_ai_rng_exhausted"); }
    }

    private static int TypeEffectiveness(string moveType, PokemonSpeciesDefinition target)
    {
        ElementType Type(string value) => Enum.Parse<ElementType>(value["TYPE_".Length..], true);
        int value = 10;
        foreach (var effect in TypeRules.OrderedEffects(Type(moveType), Type(target.Type1), Type(target.Type2)))
            value = value * effect.MultiplierTenths / 10;
        return value;
    }

    private static string? Validate(PokemonCatalog? catalog, ForestTrainerAiInput? input, bool pewterRules, bool earlyStarterRules, bool route22Rules, bool route3Rules, bool mtMoonRules, bool ceruleanRules, bool billRules, bool vermilionRules, bool ssAnneRules, bool vermilionGymRules, bool route11Rules, bool rockTunnelRules = false)
    {
        route11Rules |= rockTunnelRules;
        if (input is null) return "forest_trainer_ai_input_missing";
        if (input.AiFlags != 1) return "forest_trainer_ai_flags_unsupported";
        if (route3Rules && (input.User is { AttackBadgeBoost: true } or { SpeedBadgeBoost: true })) return "trainer_ai_enemy_badge";
        if (mtMoonRules && catalog?.Species.Count != (rockTunnelRules ? CeladonApproachBattleScope.SharedCatalogCount(catalog) : route11Rules ? 79 : ssAnneRules ? 73 : vermilionRules ? 70 : billRules ? 68 : ceruleanRules ? 65 : 56)) return "mtmoon_trainer_ai_catalog_scope";
        if (route3Rules && !mtMoonRules && catalog?.Species.Count != 40) return "route3_trainer_ai_catalog_scope";
        if (route22Rules && !route3Rules && catalog?.Species.Count != 32) return "route22_trainer_ai_catalog_scope";
        foreach (var (name, mon) in new[] { ("user", input.User), ("target", input.Target) })
        {
            if (mon is null) return "forest_trainer_ai_" + name + "_missing";
            string? invalid = PokemonStateValidator.Validate(catalog, mon.Pokemon);
            if (invalid is not null) return "forest_trainer_ai_" + name + "_" + invalid;
            if (mon.Pokemon.CurrentHp == 0) return "forest_trainer_ai_" + name + "_fainted";
            if (!route3Rules && !WildBattleRules.Route3Neutral(mon)) return "trainer_ai_route3_volatile_unsupported";
            if (mon.HeldItem != "ITEM_NONE" && !(name == "target" && (rockTunnelRules ? RockTunnelBattleScope.SupportsHeldItem(mon.HeldItem) : route11Rules ? Route11BattleScope.SupportsHeldItem(mon.HeldItem) : ssAnneRules ? SsAnneBattleScope.SupportsHeldItem(mon.HeldItem) : vermilionRules ? VermilionBattleScope.SupportsHeldItem(mon.HeldItem) : route3Rules && mon.HeldItem == "ITEM_ORAN_BERRY"))) return "forest_trainer_ai_held_item_unsupported";
            if (!WildBattleStatus.IsSupported(mon.Status, earlyStarterRules, route11Rules)) return "forest_trainer_ai_status_unsupported";
            if (mon.AttackStage is < 0 or > 12 || mon.DefenseStage is < 0 or > 12 || mon.SpeedStage is < 0 or > 12 ||
                mon.AccuracyStage is < 0 or > 12 || mon.EvasionStage is < 0 or > 12)
                return "forest_trainer_ai_stat_stage";
            if (!(rockTunnelRules ? CeladonApproachBattleScope.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : route11Rules ? Route11BattleScope.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : ssAnneRules ? SsAnneBattleScope.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : vermilionRules ? VermilionBattleScope.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : billRules ? BillBattleScope.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : ceruleanRules ? CeruleanTrainerAi.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : mtMoonRules ? MtMoonTrainerAi.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : route3Rules ? Route3TrainerAi.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : route22Rules ? Route22TrainerAi.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : pewterRules ? PewterTrainerAi.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : ValidSpecies(catalog!.Species[mon.Pokemon.Species]))) return "forest_trainer_ai_species_type_ability";
        }
        if (route22Rules && (Route22TrainerAi.ValidateCombatant(catalog, input.User, route3Rules, mtMoonRules, ceruleanRules, billRules, vermilionRules, ssAnneRules, vermilionGymRules, route11Rules, rockTunnelRules) is not null || Route22TrainerAi.ValidateCombatant(catalog, input.Target, route3Rules, mtMoonRules, ceruleanRules, billRules, vermilionRules, ssAnneRules, vermilionGymRules, route11Rules, rockTunnelRules) is not null)) return "forest_trainer_ai_route22_combatant";
        if (input.User.Pokemon.SourceSpeciesId is not (10 or 11 or 13 or 14)) return "forest_trainer_ai_user_species";
        if (input.User.Pokemon.Moves.Any(m => m.Move.Value is not
            ("pret:MOVE_TACKLE" or "pret:MOVE_STRING_SHOT" or "pret:MOVE_POISON_STING" or "pret:MOVE_HARDEN")))
            return "forest_trainer_ai_user_moves";
        foreach (var symbol in new[] { "TACKLE", "STRING_SHOT", "POISON_STING", "HARDEN" })
            if (!catalog!.Moves.TryGetValue(new MoveId("pret:MOVE_" + symbol), out var move) || !ForestBattleScope.ValidDefinition(move))
                return "forest_trainer_ai_move_definition";
        // RecordAbilityBattle stores by side and source has no per-switch history clear.
        // A previously revealed Static is still consulted after a switch to Rattata.
        if (input.KnownTargetAbility is { } known && !(rockTunnelRules ? RockTunnelBattleScope.SupportsAbility(known) : route11Rules ? Route11BattleScope.SupportsAbility(known) : ssAnneRules ? SsAnneBattleScope.SupportsAbility(known) : vermilionRules ? VermilionBattleScope.SupportsAbility(known) : billRules ? BillBattleScope.SupportsAbility(known) : ceruleanRules ? CeruleanTrainerAi.SupportsAbility(known) : mtMoonRules ? MtMoonTrainerAi.SupportsAbility(known) : route3Rules ? Route3TrainerAi.SupportsAbility(known) : route22Rules ? Route22BattleScope.SupportsAbility(known) : pewterRules ? PewterBattleScope.SupportsAbility(known) : ForestBattleScope.SupportsAbility(known)))
            return "forest_trainer_ai_ability_history";
        return null;
    }

    internal static bool ValidSpecies(PokemonSpeciesDefinition species)
    {
        var expected = species.SourceSpeciesId switch
        {
            1 or 2 or 3 => ("TYPE_GRASS", "TYPE_POISON", "ABILITY_OVERGROW", "ABILITY_NONE"),
            4 or 5 => ("TYPE_FIRE", "TYPE_FIRE", "ABILITY_BLAZE", "ABILITY_NONE"),
            6 => ("TYPE_FIRE", "TYPE_FLYING", "ABILITY_BLAZE", "ABILITY_NONE"),
            7 or 8 or 9 => ("TYPE_WATER", "TYPE_WATER", "ABILITY_TORRENT", "ABILITY_NONE"),
            10 => ("TYPE_BUG", "TYPE_BUG", "ABILITY_SHIELD_DUST", "ABILITY_NONE"),
            11 => ("TYPE_BUG", "TYPE_BUG", "ABILITY_SHED_SKIN", "ABILITY_NONE"),
            12 => ("TYPE_BUG", "TYPE_FLYING", "ABILITY_COMPOUND_EYES", "ABILITY_NONE"),
            13 => ("TYPE_BUG", "TYPE_POISON", "ABILITY_SHIELD_DUST", "ABILITY_NONE"),
            14 => ("TYPE_BUG", "TYPE_POISON", "ABILITY_SHED_SKIN", "ABILITY_NONE"),
            15 => ("TYPE_BUG", "TYPE_POISON", "ABILITY_SWARM", "ABILITY_NONE"),
            16 or 17 or 18 => ("TYPE_NORMAL", "TYPE_FLYING", "ABILITY_KEEN_EYE", "ABILITY_NONE"),
            19 or 20 => ("TYPE_NORMAL", "TYPE_NORMAL", "ABILITY_RUN_AWAY", "ABILITY_GUTS"),
            25 or 26 => ("TYPE_ELECTRIC", "TYPE_ELECTRIC", "ABILITY_STATIC", "ABILITY_NONE"),
            _ => default
        };
        return expected != default && (species.Type1, species.Type2, species.Ability1, species.Ability2) == expected;
    }
}

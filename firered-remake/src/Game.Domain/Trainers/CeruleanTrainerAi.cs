using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Pinned Cerulean CHECK_BAD_MOVE, CHECK_VIABILITY and TRY_TO_FAINT in bit order.
/// Retains source command bugs and semantic RNG without inventing trainer strategy.</summary>
public static class CeruleanTrainerAi
{
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string RulesVersion = "firered-cerulean-trainer-ai-semantic-v1";

    public static CeruleanTrainerAiDecision Choose(PokemonCatalog catalog, CeruleanTrainerAiInput input, RngState rng, bool billRules = false, bool vermilionRules = false, bool ssAnneRules = false, bool vermilionGymRules = false, bool route11Rules = false, bool rain = false, bool rockTunnelRules = false, bool reflect = false, bool celadonGymRules = false, bool userReflect = false, bool rocketHideoutRules = false, bool towerRules = false)
    {
        rocketHideoutRules |= towerRules;
        celadonGymRules |= rocketHideoutRules;
        rockTunnelRules |= celadonGymRules;
        route11Rules |= rockTunnelRules;
        CeruleanTrainerAiDecision Fault(string code) => new(TransitionStatus.Fault, null, [], [], rng, [],
            new(code, "Input is outside the bounded Cerulean trainer AI profile."));
        vermilionGymRules |= route11Rules; ssAnneRules |= vermilionGymRules;
        vermilionRules |= ssAnneRules;
        billRules |= vermilionRules;
        if (catalog is null || (catalog.Species.Count == 98) != (celadonGymRules && !rocketHideoutRules) || (catalog.Species.Count == 99) != (rocketHideoutRules && !towerRules) || (catalog.Species.Count == 101) != towerRules) return Fault("celadon_gym_trainer_ai_catalog_scope");
        string? error = Validate(catalog, input, billRules, vermilionRules, ssAnneRules, vermilionGymRules, route11Rules, rockTunnelRules);
        if (error is not null) return Fault(error);
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) return Fault("cerulean_trainer_ai_rng_algorithm");
        bool Usable(int slot) => WildBattleRules.IsMoveSelectable(input.User, slot) && (!route11Rules || !input.Target.Imprison || slot >= input.User.Pokemon.Moves.Length || !input.Target.Pokemon.Moves.Any(m => m.Move == input.User.Pokemon.Moves[slot].Move));
        if (!Enumerable.Range(0, 4).Any(Usable)) return Fault("cerulean_trainer_ai_struggle_required");
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
                ? Draw($"cerulean-trainer.ai.slot.{slot}.ability.{reason}", 2) != 0 ? targetSpecies.Ability1 : targetSpecies.Ability2
                : trappedAbility ? input.Target.Pokemon.AbilityId : targetSpecies.Ability1);
            reads.Add(new(slot, reason, ability, guess));
            return ability;
        }
        try
        {
            var scores = new int[4]; var simulated = new int[4]; var prediction = new int[4];
            for (int slot = 0; slot < 4; slot++)
            {
                scores[slot] = Usable(slot) ? 100 : 0;
                simulated[slot] = 100 - Draw($"cerulean-trainer.ai.setup.slot.{slot}.damage-percent", 16);
            }
            for (int slot = 0; slot < input.User.Pokemon.Moves.Length; slot++)
            {
                var move = catalog.Moves[input.User.Pokemon.Moves[slot].Move];
                if (move.Power > 1) prediction[slot] = Predict(catalog, input.User, input.Target, move, simulated[slot], rain: route11Rules && rain, reflect: rockTunnelRules && reflect);
            }
            void Score(int slot, int change) => scores[slot] = Math.Max(0, scores[slot] + change);
            for (int slot = 0; slot < 4; slot++)
            {
                if (!Usable(slot)) continue;
                var move = catalog.Moves[input.User.Pokemon.Moves[slot].Move];
                if (move.Power > 1 && !(rockTunnelRules && catalog.Species.Count is 94 or 98 or 99 or 101 && move.Effect == "EFFECT_EXPLOSION"))
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
                    // Original AI_CBM_Curse checks stat stages even for a Ghost user.
                    case "EFFECT_CURSE" when towerRules:
                        if (input.User.AttackStage == 12) Score(slot, -10);
                        else if (input.User.DefenseStage == 12) Score(slot, -8);
                        break;
                    // Neither effect is dispatched by source CHECK_BAD_MOVE.
                    case "EFFECT_SPITE" or "EFFECT_RAMPAGE" when towerRules: break;
                    case "EFFECT_RAGE" when rocketHideoutRules: break;
                    case "EFFECT_FAKE_OUT" when rocketHideoutRules:
                        if (input.User.FirstTurnCounter == 0) Score(slot, -10);
                        break;
                    case "EFFECT_HELPING_HAND" when rockTunnelRules && catalog.Species.Count is 94 or 98 or 99 or 101:
                        // Source AI_CBM_HelpingHand penalizes Helping Hand in any singles battle.
                        Score(slot, -10);
                        break;
                    case "EFFECT_EXPLOSION" when rockTunnelRules && catalog.Species.Count is 94 or 98 or 99 or 101:
                        // Source counts living reserves, excluding the active Pokémon on each side.
                        if (TypeEffectiveness(move.Type,targetSpecies) == 0 || Ability(slot,"explosion-immunity") == "ABILITY_DAMP") { Score(slot,-10); break; }
                        if (!input.UserHasReserves) Score(slot,input.TargetHasReserves ? -10 : -1);
                        break;
                    case "EFFECT_MAGNITUDE" when rockTunnelRules:
                        if (Ability(slot,"magnitude-immunity") == "ABILITY_LEVITATE" || TypeEffectiveness(move.Type,targetSpecies) == 0) Score(slot,-10);
                        break;
                    case "EFFECT_FORESIGHT" when rockTunnelRules:
                        if (input.Target.Identified) Score(slot,-10); break;
                    case "EFFECT_KNOCK_OFF" when rockTunnelRules:
                        if (Ability(slot,"knock-off") == "ABILITY_STICKY_HOLD") Score(slot,-10); break;
                    case "EFFECT_FOLLOW_ME" or "EFFECT_PAY_DAY" or "EFFECT_ROLLOUT" when rockTunnelRules: break;
                    case "EFFECT_ATTACK_UP" when rockTunnelRules:
                        if (input.User.AttackStage == 12) Score(slot,-10); break;
                    case "EFFECT_SPEED_UP_2" when rockTunnelRules:
                        if (input.User.SpeedStage == 12) Score(slot,-10); break;
                    case "EFFECT_COSMIC_POWER" when rockTunnelRules:
                        if (input.User.DefenseStage == 12 && input.User.SpecialDefenseStageDelta == 6) Score(slot,-10); break;
                    case "EFFECT_FLAIL" or "EFFECT_MOONLIGHT" or "EFFECT_SPECIAL_DEFENSE_DOWN_HIT" or "EFFECT_TWISTER" or "EFFECT_VITAL_THROW" when rockTunnelRules: break;

                    case "EFFECT_WILL_O_WISP" when route11Rules:
                        if (Ability(slot, "burn-immunity") == "ABILITY_WATER_VEIL" || input.Target.Status != 0 ||
                            TypeEffectiveness(move.Type, targetSpecies) is 0 or 5 or 2) Score(slot, -10);
                        break;
                    case "EFFECT_DEFENSE_DOWN_HIT" when celadonGymRules: break;
                    case "EFFECT_REFLECT" when celadonGymRules:
                        if (userReflect) Score(slot, -8); break;
                    case "EFFECT_INGRAIN" when celadonGymRules:
                        if (input.User.Rooted) Score(slot, -10); break;
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
                    case "EFFECT_ATTACK_DOWN_2" when rockTunnelRules:
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
                    case "EFFECT_WATER_SPORT":
                        if (input.User.WaterSport) Score(slot, -10);
                        break;
                    case "EFFECT_TELEPORT":
                        Score(slot, -10);
                        break;
                    case "EFFECT_LEECH_SEED":
                        if (input.Target.LeechSeedSource is not null || targetSpecies.Type1 == "TYPE_GRASS" || targetSpecies.Type2 == "TYPE_GRASS") Score(slot, -10);
                        break;
                    case "EFFECT_ROAR" when ssAnneRules:
                        if (!input.TargetHasReserves) { Score(slot, -10); break; }
                        if (Ability(slot, "roar-immunity") == "ABILITY_SUCTION_CUPS" || celadonGymRules && input.Target.Rooted) Score(slot, -10);
                        break;
                    case "EFFECT_SONICBOOM" when vermilionGymRules:
                    case "EFFECT_LEVEL_DAMAGE" when ssAnneRules:
                    case "EFFECT_LOW_KICK":
                        if (TypeEffectiveness(move.Type, targetSpecies) == 0) { Score(slot, -10); break; }
                        if (Ability(slot, "high-risk-damage") == "ABILITY_WONDER_GUARD" && TypeEffectiveness(move.Type, targetSpecies) != 20) Score(slot, -10);
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
                    case "EFFECT_SPEED_DOWN_2" when rockTunnelRules:
                    case "EFFECT_SPEED_DOWN":
                        if (input.Target.SpeedStage == 0) { Score(slot, -10); break; }
                        string ability = Ability(slot, "stat-change");
                        if (ability is "ABILITY_CLEAR_BODY" or "ABILITY_WHITE_SMOKE") Score(slot, -10);
                        break;
                    // Explicit no-dispatch effects from the actual initial parties.
                    case "EFFECT_HIT": case "EFFECT_QUICK_ATTACK": case "EFFECT_POISON_HIT":
                    case "EFFECT_PARALYZE_HIT": case "EFFECT_FLINCH_HIT": case "EFFECT_FLINCH_MINIMIZE_HIT":
                    case "EFFECT_TRAP": case "EFFECT_ABSORB": case "EFFECT_MULTI_HIT": case "EFFECT_CHARGE":
                    case "EFFECT_RESTORE_HP": case "EFFECT_ALWAYS_HIT": case "EFFECT_RAPID_SPIN":
                    case "EFFECT_CONFUSE_HIT": case "EFFECT_GUST": case "EFFECT_HIGH_CRITICAL":
                    case "EFFECT_ATTACK_UP_HIT": case "EFFECT_BURN_HIT": case "EFFECT_SPEED_DOWN_HIT":
                        break;
                    case "EFFECT_DOUBLE_HIT" when billRules: break;
                    case "EFFECT_MINIMIZE" when rockTunnelRules:
                    case "EFFECT_EVASION_UP" when vermilionRules:
                        if (input.User.EvasionStage == 12) Score(slot, -10);
                        break;
                    default: return Fault("cerulean_trainer_ai_effect_outside_source_parties");
                }
            }
            if (input.AiFlags == 7)
            {
                int userHp = 100 * input.User.Pokemon.CurrentHp / input.User.Pokemon.Stats.MaxHp;
                int targetHp = 100 * input.Target.Pokemon.CurrentHp / input.Target.Pokemon.Stats.MaxHp;
                for (int slot = 0; slot < 4; slot++)
                {
                    if (!Usable(slot)) continue;
                    int Roll(string reason) => Draw($"cerulean-trainer.ai.viability.slot.{slot}.{reason}", 256);
                    bool TargetFaster()
                    {
                        int u = Speed(input.User, route11Rules && rain), t = Speed(input.Target, route11Rules && rain);
                        // GetWhoStrikesFirst returns 0/2 on ties; ==1 is false for either.
                        if (u == t) _ = Draw($"cerulean-trainer.ai.viability.slot.{slot}.speed-tie", 2);
                        return t > u;
                    }
                    switch (catalog.Moves[input.User.Pokemon.Moves[slot].Move].Effect)
                    {
                        case "EFFECT_ROAR" when towerRules:
                            if (new[] { input.Target.AttackStage, input.Target.DefenseStage, input.Target.SpecialAttackStageDelta + 6, input.Target.SpecialDefenseStageDelta + 6, input.Target.EvasionStage }.Any(stage => stage > 8))
                            { if (Roll("roar-target-boosted") >= 128) Score(slot, 2); }
                            else Score(slot, -3);
                            break;
                        case "EFFECT_REFLECT" when towerRules:
                            if (userHp < 50 || (TypeRules.Category(ParseType(targetSpecies.Type1)) != DamageCategory.Physical && TypeRules.Category(ParseType(targetSpecies.Type2)) != DamageCategory.Physical && Roll("reflect-nonphysical-type") >= 50)) Score(slot, -2);
                            break;
                        case "EFFECT_ABSORB" when celadonGymRules:
                            if (TypeEffectiveness(catalog.Moves[input.User.Pokemon.Moves[slot].Move].Type, targetSpecies) is 5 or 2 && Roll("absorb-resisted") >= 50) Score(slot, -3);
                            break;
                        case "EFFECT_DISABLE" when ssAnneRules:
                            if (TargetFaster()) break;
                            var previousMove = input.Target.LastUsedMove is {} previousId && catalog.Moves.TryGetValue(previousId, out var previousDefinition) ? previousDefinition : null;
                            if (previousMove is { Power: > 0 }) Score(slot, 1);
                            else if (Roll("disable-last-status") >= 100) Score(slot, -1);
                            break;
                        case "EFFECT_SLEEP":
                            // Pinned Cmd_if_has_move_with_effect(AI_TARGET) always branches,
                            // so this encouragement runs even without known Dream Eater.
                            if (Roll("sleep-phantom-dream-eater") >= 128) Score(slot, 1);
                            break;
                        case "EFFECT_RESTORE_HP":
                            if (userHp == 100) { Score(slot, -3); break; }
                            if (!TargetFaster()) { Score(slot, -8); break; }
                            if (userHp >= 70 && Roll("recover-high-hp") >= 30) { Score(slot, -3); break; }
                            // The AI_TARGET doesnt_have_effect command always falls through.
                            if (Roll("recover-phantom-snatch") < 100) break;
                            if (Roll("recover-encourage") >= 20) Score(slot, 2);
                            break;
                        case "EFFECT_DEFENSE_UP":
                            if (input.User.DefenseStage >= 9)
                            { if (Roll("defense-high-stage") >= 100) Score(slot, -1); }
                            else if (userHp == 100 && Roll("defense-full-hp") >= 128) Score(slot, 2);
                            if (userHp >= 70 && Roll("defense-healthy") < 200) break;
                            if (userHp < 40) { Score(slot, -2); break; }
                            var last = input.Target.LastUsedMove is { } lastId && catalog.Moves.TryGetValue(lastId, out var lastMove) ? lastMove : null;
                            if (last is { Power: > 0 })
                            {
                                if (TypeRules.Category(ParseType(last.Type)) != DamageCategory.Physical) { Score(slot, -2); break; }
                                if (Roll("defense-last-physical") < 60) break;
                            }
                            if (Roll("defense-last-move") >= 60) Score(slot, -2);
                            break;
                        case "EFFECT_EVASION_UP" when vermilionGymRules:
                            if (userHp >= 90 && Roll("evasion-healthy") >= 100) Score(slot, 3);
                            if (input.User.EvasionStage >= 9 && Roll("evasion-high-stage") >= 128) Score(slot, -1);
                            // Toxic, Ingrain and Curse are outside this admitted state schema.
                            if (input.Target.LeechSeedSource is not null && Roll("evasion-leech-seed") >= 70) Score(slot, 3);
                            if (userHp <= 70 && input.User.EvasionStage != 6 &&
                                (userHp < 40 || targetHp < 40 || Roll("evasion-repeated-low-hp") >= 70)) Score(slot, -2);
                            break;
                        case "EFFECT_PARALYZE" when vermilionGymRules:
                            if (TargetFaster()) { if (Roll("paralyze-target-faster") >= 20) Score(slot, 3); }
                            else if (userHp <= 70) Score(slot, -1);
                            break;
                        case "EFFECT_ALWAYS_HIT":
                            if (input.Target.EvasionStage > 10 || input.User.AccuracyStage < 2)
                            { Score(slot, 1); if (Roll("always-hit") >= 100) Score(slot, 1); }
                            else if ((input.Target.EvasionStage > 8 || input.User.AccuracyStage < 4) && Roll("always-hit") >= 100) Score(slot, 1);
                            break;
                        case "EFFECT_ATTACK_DOWN_2" when rockTunnelRules:
                    case "EFFECT_ATTACK_DOWN":
                            if (input.Target.AttackStage != 6)
                            {
                                Score(slot, -1);
                                if (userHp <= 90) Score(slot, -1);
                                if (input.Target.AttackStage <= 3 && Roll("attack-down-low-stage") >= 50) Score(slot, -2);
                            }
                            if (targetHp <= 70) Score(slot, -2);
                            if (!PhysicalHeuristicType(targetSpecies.Type1) && !PhysicalHeuristicType(targetSpecies.Type2) && Roll("attack-down-nonphysical-type") >= 50) Score(slot, -2);
                            break;
                        case "EFFECT_DEFENSE_DOWN_2" when vermilionGymRules:
                        case "EFFECT_DEFENSE_DOWN":
                            if ((userHp < 70 || input.Target.DefenseStage <= 3) && Roll("defense-down-low-hp-or-stage") >= 50) Score(slot, -2);
                            if (targetHp <= 70) Score(slot, -2);
                            break;
                        case "EFFECT_ACCURACY_DOWN":
                            if ((userHp < 70 || targetHp <= 70) && Roll("accuracy-down-low-hp") >= 100) Score(slot, -1);
                            if (input.User.AccuracyStage <= 4 && Roll("accuracy-down-user-accuracy") >= 80) Score(slot, -2);
                            if (input.Target.LeechSeedSource is not null && Roll("accuracy-down-leech-seed") >= 70) Score(slot, 2);
                            if (userHp <= 70 && input.Target.AccuracyStage != 6 &&
                                (userHp < 40 || targetHp < 40 || Roll("accuracy-down-repeated-low-hp") >= 70)) Score(slot, -2);
                            break;
                        case "EFFECT_POISON":
                            if (userHp < 50 || targetHp <= 50) Score(slot, -1);
                            break;
                        case "EFFECT_LEECH_SEED":
                            if (input.User.Pokemon.Moves.Any(m => catalog.Moves[m.Move].Power > 0))
                            {
                                if (userHp <= 50 && Roll("seed-user-low-hp") >= 50) Score(slot, -3);
                                if (targetHp <= 50 && Roll("seed-target-low-hp") >= 50) Score(slot, -3);
                            }
                            break;
                    }
                }
                for (int slot = 0; slot < 4; slot++)
                {
                    if (!Usable(slot)) continue;
                    var move = catalog.Moves[input.User.Pokemon.Moves[slot].Move];
                    if (move.Power < 2) continue;
                    if (input.Target.Pokemon.CurrentHp <= prediction[slot]) Score(slot, move.Effect == "EFFECT_QUICK_ATTACK" ? 6 : 4);
                    else if (prediction.Any(damage => damage > prediction[slot])) Score(slot, -1);
                }
            }
            var best = new List<int> { 0 }; int maximum = scores[0];
            for (int slot = 1; slot < 4; slot++)
            {
                if (scores[slot] > maximum) { maximum = scores[slot]; best.Clear(); best.Add(slot); }
                if (scores[slot] == maximum) best.Add(slot);
            }
            int chosen = best[Draw("cerulean-trainer.ai.choose-best", best.Count)];
            return new(TransitionStatus.Accepted, chosen, [.. scores], [.. simulated], current, trace.ToImmutable(), null)
                { AbilityReads = reads.ToImmutable(), PredictedDamage = [.. prediction] };
        }
        catch (OverflowException) { return Fault("cerulean_trainer_ai_rng_exhausted"); }
    }

    private static int TypeEffectiveness(string moveType, PokemonSpeciesDefinition target)
    {
        ElementType Type(string value) => Enum.Parse<ElementType>(value["TYPE_".Length..], true);
        int value = 10;
        foreach (var effect in TypeRules.OrderedEffects(Type(moveType), Type(target.Type1), Type(target.Type2)))
            value = value * effect.MultiplierTenths / 10;
        return value;
    }
    private static string? Validate(PokemonCatalog? catalog, CeruleanTrainerAiInput? input, bool billRules, bool vermilionRules, bool ssAnneRules, bool vermilionGymRules, bool route11Rules, bool rockTunnelRules = false)
    {
        route11Rules |= rockTunnelRules;
        if (input is null) return "cerulean_trainer_ai_input_missing";
        if (input.AiFlags is not (1 or 7)) return "cerulean_trainer_ai_flags_unsupported";
        if (catalog?.Species.Count != (rockTunnelRules ? CeladonApproachBattleScope.SharedCatalogCount(catalog) : route11Rules ? 79 : ssAnneRules ? 73 : vermilionRules ? 70 : billRules ? 68 : 65)) return "cerulean_trainer_ai_catalog_scope";
        foreach (var (name, mon) in new[] { ("user", input.User), ("target", input.Target) })
        {
            string? error = ValidateCombatant(catalog, mon, billRules, vermilionRules, ssAnneRules, vermilionGymRules, route11Rules, rockTunnelRules);
            if (error is not null) return "cerulean_trainer_ai_" + name + "_" + error;
            if (mon.Pokemon.CurrentHp == 0) return "cerulean_trainer_ai_" + name + "_fainted";
        }
        if (!(catalog.Species.Count == 101 && PokemonTowerTrainerPartyFactory.SupportedTrainers.Any(id => PokemonTowerTrainerPartyFactory.ValidPartyMember(catalog,input.User.Pokemon,id,input.AiFlags)) || catalog.Species.Count is 99 or 101 && RocketHideoutTrainerPartyFactory.SupportedTrainers.Any(id => RocketHideoutTrainerPartyFactory.ValidPartyMember(catalog,input.User.Pokemon,id,input.AiFlags)) || celadonGymRulesForCatalog(catalog) && CeladonGymTrainerPartyFactory.SupportedTrainers.Any(id => CeladonGymTrainerPartyFactory.ValidPartyMember(catalog,input.User.Pokemon,id,input.AiFlags)) || rockTunnelRules && catalog.Species.Count is 94 or 98 or 99 or 101 && CeladonApproachTrainerPartyFactory.SupportedTrainers.Any(id => CeladonApproachTrainerPartyFactory.ValidPartyMember(catalog,input.User.Pokemon,id,input.AiFlags)) || rockTunnelRules && RockTunnelTrainerPartyFactory.SupportedTrainers.Any(id => RockTunnelTrainerPartyFactory.ValidPartyMember(catalog,input.User.Pokemon,id,input.AiFlags)) || route11Rules && Route11TrainerPartyFactory.SupportedTrainers.Any(id => Route11TrainerPartyFactory.ValidPartyMember(catalog,input.User.Pokemon,id,input.AiFlags)) || vermilionGymRules && VermilionGymTrainerPartyFactory.SupportedTrainers.Any(id => VermilionGymTrainerPartyFactory.ValidPartyMember(catalog,input.User.Pokemon,id,input.AiFlags)) || ssAnneRules && SsAnneTrainerPartyFactory.SupportedTrainers.Any(id => SsAnneTrainerPartyFactory.ValidPartyMember(catalog,input.User.Pokemon,id,input.AiFlags)) || vermilionRules && VermilionTrainerPartyFactory.SupportedTrainers.Any(id => VermilionTrainerPartyFactory.ValidPartyMember(catalog,input.User.Pokemon,id,input.AiFlags)) || (billRules ? BillTrainerPartyFactory.SupportedTrainers.Any(id => BillTrainerPartyFactory.ValidPartyMember(catalog,input.User.Pokemon,id,input.AiFlags)) || ValidUser(catalog,input.User.Pokemon,input.AiFlags) : ValidUser(catalog,input.User.Pokemon,input.AiFlags))) || input.User.SpeedBadgeBoost || input.User.AttackBadgeBoost || input.User.HeldItem != "ITEM_NONE") return "cerulean_trainer_ai_user_roster";
        foreach (var slot in input.User.Pokemon.Moves)
            if (!(rockTunnelRules ? CeladonApproachBattleScope.ValidDefinitionForCatalog(catalog, catalog.Moves[slot.Move]) : route11Rules ? Route11BattleScope.ValidDefinition(catalog.Moves[slot.Move]) : vermilionGymRules ? VermilionGymBattleScope.ValidDefinition(catalog.Moves[slot.Move]) : ssAnneRules ? SsAnneBattleScope.ValidDefinition(catalog.Moves[slot.Move]) : vermilionRules ? VermilionBattleScope.ValidDefinition(catalog.Moves[slot.Move]) : billRules ? BillBattleScope.ValidDefinition(catalog.Moves[slot.Move]) : CeruleanBattleScope.ValidDefinition(catalog.Moves[slot.Move]))) return "cerulean_trainer_ai_move_definition";
        if (input.KnownTargetAbility is { } known && !(catalog.Species.Count == 101 && rockTunnelRules ? RocketHideoutBattleScope.SupportsAbility(known) : rockTunnelRules ? (catalog?.Species.Count == 101 ? RocketHideoutBattleScope.SupportsAbility(known) : RocketHideoutBattleScope.SupportsAbilityForCatalog(catalog, known)) : route11Rules ? Route11BattleScope.SupportsAbility(known) : ssAnneRules ? SsAnneBattleScope.SupportsAbility(known) : vermilionRules ? VermilionBattleScope.SupportsAbility(known) : billRules ? BillBattleScope.SupportsAbility(known) : SupportsAbility(known))) return "cerulean_trainer_ai_ability_history";
        return null;
    }
    internal static string? ValidateCombatant(PokemonCatalog? catalog, WildBattleCombatant? mon, bool billRules = false, bool vermilionRules = false, bool ssAnneRules = false, bool vermilionGymRules = false, bool route11Rules = false, bool rockTunnelRules = false) =>
        WildBattleRules.ValidateCombatant(catalog!, mon!, "cerulean-ai", true, true, true, true, true, true, true, ceruleanRules: true, billRules: billRules, vermilionRules: vermilionRules, ssAnneRules: ssAnneRules, vermilionGymRules: vermilionGymRules, route11Rules: route11Rules, rockTunnelRules: rockTunnelRules) ??
        (!(catalog!.Species.Count == 101 && rockTunnelRules ? PokemonTowerBattleScope.ValidSpecies(catalog!.Species[mon!.Pokemon.Species]) : rockTunnelRules ? RocketHideoutBattleScope.ValidSharedSpecies(catalog!.Species[mon!.Pokemon.Species]) : route11Rules ? Route11BattleScope.ValidSpecies(catalog!.Species[mon!.Pokemon.Species]) : ssAnneRules ? SsAnneBattleScope.ValidSpecies(catalog!.Species[mon!.Pokemon.Species]) : vermilionRules ? VermilionBattleScope.ValidSpecies(catalog!.Species[mon!.Pokemon.Species]) : billRules ? BillBattleScope.ValidSpecies(catalog!.Species[mon!.Pokemon.Species]) : ValidSpecies(catalog!.Species[mon!.Pokemon.Species])) ? "cerulean_ai_species_type_ability" : null);

    private static bool celadonGymRulesForCatalog(PokemonCatalog catalog) => catalog.Species.Count is 98 or 99 or 101;
    internal static bool ValidUser(PokemonCatalog catalog, PokemonInstance mon, uint? flags = null) =>
        CeruleanTrainerPartyFactory.SupportedTrainers.Any(id => ValidPartyMember(catalog, mon, id, flags));
    internal static bool ValidPartyMember(PokemonCatalog catalog, PokemonInstance mon, TrainerId id, uint? flags = null)
    {
        var expected = CeruleanTrainerPartyFactory.Expected(id);
        if (flags is { } f && f != (expected.Flags == 1 ? 7u : 1u)) return false;
        uint hash = 0;
        foreach (var p in expected.Party)
        {
            foreach (char c in expected.Name + CeruleanTrainerPartyFactory.SpeciesSymbol(p.Species)) hash += (uint)(0xbb + c - 'A');
            uint personality = (expected.Id is 150 or 415 ? 0x78u : 0x88u) + (hash << 8);
            int iv = p.Iv * 31 / 255;
            if (mon.SourceSpeciesId != p.Species || mon.Level != p.Level || mon.Personality != personality ||
                mon.Ivs != new SixStats(iv,iv,iv,iv,iv,iv) || mon.Evs != SixStats.Zero || mon.Moves.Any(m => m.PpUps != 0)) continue;
            IEnumerable<MoveId> moves = expected.Flags == 1 ? p.Moves.TakeWhile(m => m != "NONE").Select(m => new MoveId("pret:MOVE_" + m)) :
                CeruleanTrainerPartyFactory.NaturalMoves(p.Species).Select(m => new MoveId("pret:MOVE_" + m));
            if (mon.Moves.Select(m => m.Move).SequenceEqual(moves)) return true;
        }
        return false;
    }
    internal static bool SupportsAbility(string ability) => CeruleanBattleScope.SupportsAbility(ability);
    internal static bool ValidSpecies(PokemonSpeciesDefinition s) => MtMoonTrainerAi.ValidSpecies(s) || s.SourceSpeciesId switch
    {
        63 or 64 => (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_PSYCHIC","TYPE_PSYCHIC","ABILITY_SYNCHRONIZE","ABILITY_INNER_FOCUS"),
        66 => (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_FIGHTING","TYPE_FIGHTING","ABILITY_GUTS","ABILITY_NONE"),
        96 => (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_PSYCHIC","TYPE_PSYCHIC","ABILITY_INSOMNIA","ABILITY_NONE"),
        90 => (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_WATER","TYPE_WATER","ABILITY_SHELL_ARMOR","ABILITY_NONE"),
        116 => (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_WATER","TYPE_WATER","ABILITY_SWIFT_SWIM","ABILITY_NONE"),
        118 => (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_WATER","TYPE_WATER","ABILITY_SWIFT_SWIM","ABILITY_WATER_VEIL"),
        120 => (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_WATER","TYPE_WATER","ABILITY_ILLUMINATE","ABILITY_NATURAL_CURE"),
        121 => (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_WATER","TYPE_PSYCHIC","ABILITY_ILLUMINATE","ABILITY_NATURAL_CURE"),
        _ => false
    };
    private static bool IsSoundMove(MoveId move) => MtMoonTrainerAi.IsSoundMove(move);
    private static bool PhysicalHeuristicType(string type) => type is "TYPE_NORMAL" or "TYPE_FIGHTING" or "TYPE_GROUND" or "TYPE_ROCK" or "TYPE_BUG" or "TYPE_STEEL";
    private static ElementType ParseType(string type) => Enum.Parse<ElementType>(type[5..], true);
    private static int Speed(WildBattleCombatant mon, bool rain = false)
    {
        int speed = WildBattleDamage.Stat(mon.Pokemon.Stats.Speed * (rain && mon.Pokemon.AbilityId == "ABILITY_SWIFT_SWIM" ? 2 : 1), mon.SpeedStage);
        if (mon.SpeedBadgeBoost) speed = speed * 110 / 100;
        return mon.Status == WildBattleStatus.Paralysis ? speed / 4 : speed;
    }

    // AI_CalcDmg uses base power, without execution's variable-power preprocessing. TypeCalc
    // applies STAB but Levitate only sets a discarded local flag, leaving damage nonzero.
    internal static int Predict(PokemonCatalog catalog, WildBattleCombatant user, WildBattleCombatant target,
        PokemonMoveDefinition move, int percent, bool clearUserAbility = false, bool rain = false, bool reflect = false)
    {
        int value = BasePrediction(user, target, move.Power, ParseType(move.Type), move.Effect, clearUserAbility, rain, reflect);
        value = TypePrediction(catalog, user, target, move, value);
        return Math.Max(1, value * percent / 100);
    }
    internal static int BasePrediction(WildBattleCombatant user, WildBattleCombatant target, int power, ElementType type,
        string? effect, bool clearUserAbility = false, bool rain = false, bool reflect = false)
    {
        bool physical = TypeRules.Category(type) == DamageCategory.Physical;
        int attack = physical ? user.Pokemon.Stats.Attack : user.Pokemon.Stats.SpecialAttack;
        int defense = physical ? target.Pokemon.Stats.Defense : target.Pokemon.Stats.SpecialDefense;
        string ability = clearUserAbility ? "ABILITY_NONE" : user.Pokemon.AbilityId;
        if (physical && user.AttackBadgeBoost) attack = attack * 110 / 100;
        if (user.HeldItem == "ITEM_BLACK_GLASSES" && type == ElementType.Dark) attack = attack * 110 / 100;
        if (physical && ability == "ABILITY_GUTS" && user.Status != 0) attack = attack * 150 / 100;
        if (type == ElementType.Electric && (user.MudSport || target.MudSport)) power /= 2;
        if (type == ElementType.Fire && (user.WaterSport || target.WaterSport)) power /= 2;
        if (effect == "EFFECT_EXPLOSION") defense /= 2;
        if (user.Pokemon.CurrentHp <= user.Pokemon.Stats.MaxHp / 3 && (type,ability) is
            (ElementType.Grass,"ABILITY_OVERGROW") or (ElementType.Fire,"ABILITY_BLAZE") or
            (ElementType.Water,"ABILITY_TORRENT") or (ElementType.Bug,"ABILITY_SWARM")) power = power * 150 / 100;
        attack = Math.Max(1, WildBattleDamage.Stat(attack, physical ? user.AttackStage : user.SpecialAttackStageDelta + 6));
        defense = Math.Max(1, WildBattleDamage.Stat(defense, physical ? target.DefenseStage : target.SpecialDefenseStageDelta + 6));
        int value = checked(attack * power * (2 * user.Pokemon.Level / 5 + 2)) / defense / 50;
        if (physical && user.Status == WildBattleStatus.Burn && ability != "ABILITY_GUTS") value /= 2;
        if (physical && reflect) value /= 2;
        if (physical) value = Math.Max(1, value);
        if (rain && type == ElementType.Fire) value /= 2;
        if (rain && type == ElementType.Water) value = value * 15 / 10;
        if (user.FlashFireBoost && type == ElementType.Fire) value = value * 15 / 10;
        value += 2;
        if (user.ChargeTurnsRemaining > 0 && type == ElementType.Electric) value *= 2;
        return value;
    }
    internal static int TypePrediction(PokemonCatalog catalog, WildBattleCombatant user, WildBattleCombatant target,
        PokemonMoveDefinition move, int value)
    {
        var a = catalog.Species[user.Pokemon.Species]; var d = catalog.Species[target.Pokemon.Species];
        if (move.Type == a.Type1 || move.Type == a.Type2) value = value * 15 / 10;
        if (target.Pokemon.AbilityId == "ABILITY_LEVITATE" && move.Type == "TYPE_GROUND") return value;
        foreach (var effect in TypeRules.OrderedEffects(ParseType(move.Type), ParseType(d.Type1), ParseType(d.Type2)))
        {
            value = value * effect.MultiplierTenths / 10;
            if (value == 0 && effect.MultiplierTenths != 0) value = 1;
        }
        return value;
    }
}

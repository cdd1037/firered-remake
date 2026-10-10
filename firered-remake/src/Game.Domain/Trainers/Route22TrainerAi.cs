using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>KnownTargetAbility is side-indexed source battle history, which may remain stale after a player switch.</summary>
public sealed record Route22TrainerAiInput(WildBattleCombatant User, WildBattleCombatant Target,
    string? KnownTargetAbility = null, uint AiFlags = 7);
public sealed record Route22TrainerAbilityRead(int MoveSlot, string Reason, string Ability, bool Guessed);
public sealed record Route22TrainerAiDecision(TransitionStatus Status, int? MoveSlot, ImmutableArray<int> Scores,
    ImmutableArray<int> SimulatedDamagePercent, RngState Rng, ImmutableArray<RandomTrace> Trace, Diagnostic? Diagnostic)
{
    public bool Succeeded => Status == TransitionStatus.Accepted;
    public ImmutableArray<Route22TrainerAbilityRead> AbilityReads { get; init; } = [];
    public ImmutableArray<int> PredictedDamage { get; init; } = [];
}

/// <summary>Source flags 7 in bit order for Pidgey and the three level-nine counterstarters.
/// Semantic-call RNG only; not a general AI interpreter, battle executor or frame emulator.</summary>
public static class Route22TrainerAi
{
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string RulesVersion = "firered-route22-rival-ai-semantic-v1";

    public static Route22TrainerAiDecision Choose(PokemonCatalog catalog, Route22TrainerAiInput input, RngState rng, bool route3Rules = false, bool mtMoonRules = false, bool ceruleanRules = false, bool billRules = false, bool vermilionRules = false, bool ssAnneRules = false, bool vermilionGymRules = false, bool route11Rules = false, bool rain = false, bool rockTunnelRules = false, bool reflect = false, bool celadonGymRules = false, bool rocketHideoutRules = false, bool towerRules = false, bool fuchsiaRules = false)
    {
        towerRules |= fuchsiaRules;
        rocketHideoutRules |= towerRules;
        celadonGymRules |= rocketHideoutRules;
        rockTunnelRules |= celadonGymRules;
        route11Rules |= rockTunnelRules;
        Route22TrainerAiDecision Fault(string code) => new(TransitionStatus.Fault, null, [], [], rng, [],
            new(code, "Input is outside the bounded Route22 early-rival AI profile."));
        vermilionGymRules |= route11Rules; ssAnneRules |= vermilionGymRules;
        vermilionRules |= ssAnneRules;
        billRules |= vermilionRules;
        ceruleanRules |= billRules;
        mtMoonRules |= ceruleanRules;
        route3Rules |= mtMoonRules;
        if (catalog is null || (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 98) != (celadonGymRules && !rocketHideoutRules) || (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 99) != (rocketHideoutRules && !towerRules) || (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) is 101 or 102) != (towerRules && !fuchsiaRules) || (catalog.Species.Count == 107) != fuchsiaRules) return Fault("celadon_gym_trainer_ai_catalog_scope");
        string? error = Validate(catalog, input, route3Rules, mtMoonRules, ceruleanRules, billRules, vermilionRules, ssAnneRules, vermilionGymRules, route11Rules, rockTunnelRules);
        if (error is not null) return Fault(error);
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) return Fault("route22_trainer_ai_rng_algorithm");
        bool Usable(int slot) => route3Rules ? WildBattleRules.IsMoveSelectable(input.User, slot) && (!route11Rules || !input.Target.Imprison || slot >= input.User.Pokemon.Moves.Length || !input.Target.Pokemon.Moves.Any(m => m.Move == input.User.Pokemon.Moves[slot].Move)) : slot < input.User.Pokemon.Moves.Length && input.User.Pokemon.Moves[slot].CurrentPp > 0;
        if (!Enumerable.Range(0, 4).Any(Usable)) return Fault("route22_trainer_ai_struggle_required");
        var current = rng;
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        var reads = ImmutableArray.CreateBuilder<Route22TrainerAbilityRead>();
        int Draw(string reason, int bound)
        {
            var d = Gen3Rng.Next(current, reason, bound);
            current = d.State; trace.Add(d.Trace); return d.Trace.Result;
        }
        var targetSpecies = catalog.Species[input.Target.Pokemon.Species];
        string Ability(int slot, string reason)
        {
            // Every get_ability instruction independently guesses; no accidental per-turn memoization.
            bool trappedAbility = mtMoonRules && input.Target.Pokemon.AbilityId == "ABILITY_MAGNET_PULL" || route11Rules && input.Target.Pokemon.AbilityId == "ABILITY_ARENA_TRAP";
            bool guessed = input.KnownTargetAbility is null && !trappedAbility && targetSpecies.Ability2 != "ABILITY_NONE";
            string ability = input.KnownTargetAbility ?? (guessed
                ? Draw($"route22-trainer.ai.slot.{slot}.ability.{reason}", 2) != 0 ? targetSpecies.Ability1 : targetSpecies.Ability2
                : trappedAbility ? input.Target.Pokemon.AbilityId : targetSpecies.Ability1);
            reads.Add(new(slot, reason, ability, guessed));
            return ability;
        }
        try
        {
            var scores = new int[4]; var simulated = new int[4]; var predictions = new int[4];
            for (int slot = 0; slot < 4; slot++)
            {
                scores[slot] = Usable(slot) ? 100 : 0;
                simulated[slot] = 100 - Draw($"route22-trainer.ai.setup.slot.{slot}.damage-percent", 16);
            }
            // AI_CalcDmg considers depleted slots too. Only slot zero is damaging in these fixed parties.
            for (int slot = 0; slot < input.User.Pokemon.Moves.Length; slot++)
            {
                var move = catalog.Moves[input.User.Pokemon.Moves[slot].Move];
                if (move.Power > 1) predictions[slot] = Predict(catalog, input.User, input.Target, move, simulated[slot], rain: route11Rules && rain);
            }
            void Score(int slot, int change) => scores[slot] = Math.Max(0, scores[slot] + change);
            for (int slot = 0; slot < 4; slot++)
            {
                if (!Usable(slot)) continue;
                var move = catalog.Moves[input.User.Pokemon.Moves[slot].Move];
                // No admitted target or source history ability grants Normal immunity. Nevertheless
                // the damage-immunity and soundproof reads still execute and may consume independent RNG.
                if (move.Power > 1) _ = Ability(slot, "damage-immunity");
                string soundAbility = Ability(slot, "soundproof");
                if (mtMoonRules && soundAbility == "ABILITY_SOUNDPROOF" && MtMoonTrainerAi.IsSoundMove(move.Id))
                { Score(slot, -10); continue; }
                switch (move.Effect)
                {
                    case "EFFECT_ATTACK_DOWN":
                        if (input.Target.AttackStage == 0) { Score(slot, -10); break; }
                        _ = Ability(slot, "attack-ability");
                        _ = Ability(slot, "stat-change");
                        break;
                    case "EFFECT_DEFENSE_DOWN":
                        if (input.Target.DefenseStage == 0) { Score(slot, -10); break; }
                        _ = Ability(slot, "stat-change");
                        break;
                    case "EFFECT_ACCURACY_DOWN":
                        if (input.Target.AccuracyStage == 0) { Score(slot, -10); break; }
                        if (Ability(slot, "accuracy-ability") == "ABILITY_KEEN_EYE") { Score(slot, -10); break; }
                        _ = Ability(slot, "stat-change");
                        break;
                }
            }
            int userHp = 100 * input.User.Pokemon.CurrentHp / input.User.Pokemon.Stats.MaxHp;
            int targetHp = 100 * input.Target.Pokemon.CurrentHp / input.Target.Pokemon.Stats.MaxHp;
            // CHECK_VIABILITY still runs after a bad-move penalty; source does not short-circuit the next phase.
            for (int slot = 0; slot < 4; slot++)
            {
                if (!Usable(slot)) continue;
                int Roll(string reason) => Draw($"route22-trainer.ai.viability.slot.{slot}.{reason}", 256);
                switch (input.User.Pokemon.Moves[slot].Move.Value)
                {
                    case "pret:MOVE_GROWL":
                        if (input.Target.AttackStage != 6)
                        {
                            Score(slot, -1);
                            if (userHp <= 90) Score(slot, -1);
                            if (input.Target.AttackStage <= 3 && Roll("growl.low-attack") >= 50) Score(slot, -2);
                        }
                        if (targetHp <= 70) Score(slot, -2);
                        // Pinned AI intentionally omits Poison, Flying and Ghost from this six-type list.
                        if (!PhysicalHeuristicType(targetSpecies.Type1) && !PhysicalHeuristicType(targetSpecies.Type2) &&
                            Roll("growl.nonphysical-type") >= 50) Score(slot, -2);
                        break;
                    case "pret:MOVE_TAIL_WHIP":
                        if ((userHp < 70 || input.Target.DefenseStage <= 3) && Roll("tail-whip.low-hp-or-defense") >= 50) Score(slot, -2);
                        if (targetHp <= 70) Score(slot, -2);
                        break;
                    case "pret:MOVE_SAND_ATTACK":
                        if ((userHp < 70 || targetHp <= 70) && Roll("sand-attack.low-hp") >= 100) Score(slot, -1);
                        // This is AI_USER accuracy in the original, not AI_TARGET accuracy.
                        if (input.User.AccuracyStage <= 4 && Roll("sand-attack.user-accuracy") >= 80) Score(slot, -2);
                        // Toxic, rooted and cursed are rejected/unrepresented by this battle profile.
                        if (input.Target.LeechSeedSource is not null && Roll("sand-attack.target-leech-seed") >= 70) Score(slot, 2);
                        if (userHp <= 70 && input.Target.AccuracyStage != 6 &&
                            (userHp < 40 || targetHp < 40 || Roll("sand-attack.repeated-low-hp") >= 70)) Score(slot, -2);
                        break;
                }
            }
            // TRY_TO_FAINT uses saved simulation percentages, without crit/hit rolls. The lone damage
            // move is always MOST_POWERFUL; status moves are DISCOURAGED, never NOT_MOST_POWERFUL.
            for (int slot = 0; slot < 4; slot++)
                if (Usable(slot) && predictions[slot] > 0 && input.Target.Pokemon.CurrentHp <= predictions[slot]) Score(slot, 4);
            var best = new List<int> { 0 }; int maximum = scores[0];
            for (int slot = 1; slot < 4; slot++)
            {
                // Independent source ifs duplicate a newly encountered maximum.
                if (scores[slot] > maximum) { maximum = scores[slot]; best.Clear(); best.Add(slot); }
                if (scores[slot] == maximum) best.Add(slot);
            }
            int chosen = best[Draw("route22-trainer.ai.choose-best", best.Count)];
            return new(TransitionStatus.Accepted, chosen, [.. scores], [.. simulated], current, trace.ToImmutable(), null)
                { AbilityReads = reads.ToImmutable(), PredictedDamage = [.. predictions] };
        }
        catch (OverflowException) { return Fault("route22_trainer_ai_rng_exhausted"); }
    }

    private static bool PhysicalHeuristicType(string type) => type is
        "TYPE_NORMAL" or "TYPE_FIGHTING" or "TYPE_GROUND" or "TYPE_ROCK" or "TYPE_BUG" or "TYPE_STEEL";

    private static int Predict(PokemonCatalog catalog, WildBattleCombatant user, WildBattleCombatant target,
        PokemonMoveDefinition move, int percent, bool rain = false)
    {
        int attack = WildBattleDamage.Stat(user.Pokemon.Stats.Attack, user.AttackStage);
        int defense = WildBattleDamage.Stat(target.Pokemon.Stats.Defense, target.DefenseStage);
        int value = checked(attack * move.Power * (2 * user.Pokemon.Level / 5 + 2)) / defense / 50;
        if (user.Status == WildBattleStatus.Burn) value /= 2;
        value = Math.Max(1, value);
        if (rain && move.Type == "TYPE_FIRE") value /= 2;
        if (rain && move.Type == "TYPE_WATER") value = value * 15 / 10;
        value += 2;
        ElementType Type(string symbol) => Enum.Parse<ElementType>(symbol[5..], true);
        var type = Type(move.Type);
        var a = catalog.Species[user.Pokemon.Species]; var d = catalog.Species[target.Pokemon.Species];
        if (type == Type(a.Type1) || type == Type(a.Type2)) value = value * 15 / 10;
        foreach (var effect in TypeRules.OrderedEffects(type, Type(d.Type1), Type(d.Type2)))
        {
            value = value * effect.MultiplierTenths / 10;
            if (value == 0 && effect.MultiplierTenths != 0) value = 1;
        }
        return Math.Max(1, value * percent / 100);
    }

    private static string? Validate(PokemonCatalog? catalog, Route22TrainerAiInput? input, bool route3Rules, bool mtMoonRules, bool ceruleanRules, bool billRules, bool vermilionRules, bool ssAnneRules, bool vermilionGymRules, bool route11Rules, bool rockTunnelRules = false)
    {
        route11Rules |= rockTunnelRules;
        if (input is null) return "route22_trainer_ai_input_missing";
        if (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) != (rockTunnelRules ? CeladonApproachBattleScope.SharedCatalogCount(catalog) : route11Rules ? 79 : ssAnneRules ? 73 : vermilionRules ? 70 : billRules ? 68 : ceruleanRules ? 65 : mtMoonRules ? 56 : route3Rules ? 40 : 32)) return "route22_trainer_ai_catalog_scope";
        if (input.AiFlags != 7) return "route22_trainer_ai_flags_unsupported";
        foreach (var (name, mon) in new[] { ("user", input.User), ("target", input.Target) })
        {
            string? error = ValidateCombatant(catalog, mon, route3Rules, mtMoonRules, ceruleanRules, billRules, vermilionRules, ssAnneRules, vermilionGymRules, route11Rules, rockTunnelRules);
            if (error is not null) return "route22_trainer_ai_" + name + "_" + error;
            if (mon.Pokemon.CurrentHp == 0) return "route22_trainer_ai_" + name + "_fainted";
        }
        if (!ValidUser(input.User.Pokemon)) return "route22_trainer_ai_user_roster";
        if (input.User.HeldItem != "ITEM_NONE") return "route22_trainer_ai_enemy_item";
        if (input.User.SpeedBadgeBoost || input.User.AttackBadgeBoost) return "route22_trainer_ai_enemy_badge";
        foreach (var slot in input.User.Pokemon.Moves)
            if (!ValidMove(catalog!.Moves[slot.Move])) return "route22_trainer_ai_move_definition";
        if (input.KnownTargetAbility is { } known && !(rockTunnelRules ? (catalog?.Species.Count == 107 ? FuchsiaBattleScope.SupportsAbility(known) : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 102 ? SnorlaxBattleScope.SupportsAbility(known) : Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) is 101 or 102 or 107 ? RocketHideoutBattleScope.SupportsAbility(known) : RocketHideoutBattleScope.SupportsAbilityForCatalog(catalog, known)) : route11Rules ? Route11BattleScope.SupportsAbility(known) : ssAnneRules ? SsAnneBattleScope.SupportsAbility(known) : vermilionRules ? VermilionBattleScope.SupportsAbility(known) : billRules ? BillBattleScope.SupportsAbility(known) : ceruleanRules ? CeruleanTrainerAi.SupportsAbility(known) : mtMoonRules ? MtMoonTrainerAi.SupportsAbility(known) : route3Rules ? Route3TrainerAi.SupportsAbility(known) : SupportsAbility(known))) return "route22_trainer_ai_ability_history";
        return null;
    }

    internal static string? ValidateCombatant(PokemonCatalog? catalog, WildBattleCombatant? mon, bool route3Rules = false, bool mtMoonRules = false, bool ceruleanRules = false, bool billRules = false, bool vermilionRules = false, bool ssAnneRules = false, bool vermilionGymRules = false, bool route11Rules = false, bool rockTunnelRules = false)
    {
        route11Rules |= rockTunnelRules;
        if (ceruleanRules) return CeruleanTrainerAi.ValidateCombatant(catalog, mon, billRules, vermilionRules, ssAnneRules, vermilionGymRules, route11Rules, rockTunnelRules);
        if (mtMoonRules) return MtMoonTrainerAi.ValidateCombatant(catalog, mon);
        if (route3Rules) return Route3TrainerAi.ValidateCombatant(catalog, mon);
        if (mon is null) return "missing";
        if (!WildBattleRules.Route3Neutral(mon)) return "route3_volatile_unsupported";
        string? invalid = WildBattleTransformationRules.Validate(catalog, mon, catalog?.Species.Count == 107);
        if (invalid is not null) return invalid;
        if (mon.HeldItem != "ITEM_NONE") return "held_item_unsupported";
        if (!WildBattleStatus.IsSupported(mon.Status, true)) return "status_unsupported";
        if (mon.Pokemon.AbilityId == "ABILITY_VITAL_SPIRIT" && WildBattleStatus.IsSleeping(mon.Status)) return "vital_spirit_asleep";
        if (mon.AttackStage is < 0 or > 12 || mon.DefenseStage is < 0 or > 12 || mon.SpeedStage is < 0 or > 12 ||
            mon.AccuracyStage is < 0 or > 12 || mon.EvasionStage is < 0 or > 12 || mon.ConfusionTurnsRemaining is < 0 or > 5 ||
            mon.BindTurnsRemaining is < 0 or > 5 || mon.LeechSeedSource is { } seed && !Enum.IsDefined(seed)) return "stage_or_volatile";
        if (!ValidSpecies(catalog!.Species[mon.Pokemon.Species])) return "species_type_ability";
        return null;
    }

    internal static bool ValidUser(PokemonInstance p)
    {
        string[]? moves = p.SourceSpeciesId switch
        {
            16 => ["TACKLE", "SAND_ATTACK"], 7 => ["TACKLE", "TAIL_WHIP"],
            1 => ["TACKLE", "GROWL"], 4 => ["SCRATCH", "GROWL"], _ => null
        };
        return moves is not null && p.Level == 9 && p.Ivs == new SixStats(6, 6, 6, 6, 6, 6) && p.Evs == SixStats.Zero &&
            p.Moves.All(m => m.PpUps == 0) && p.Moves.Select(x => x.Move.Value).SequenceEqual(moves.Select(x => "pret:MOVE_" + x));
    }
    internal static bool SupportsAbility(string ability) => PewterTrainerAi.SupportsAbility(ability) || ability == "ABILITY_VITAL_SPIRIT";
    internal static bool ValidSpecies(PokemonSpeciesDefinition s) => PewterTrainerAi.ValidSpecies(s) || s.SourceSpeciesId switch
    {
        21 or 22 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_NORMAL", "TYPE_FLYING", "ABILITY_KEEN_EYE", "ABILITY_NONE"),
        56 => (s.Type1, s.Type2, s.Ability1, s.Ability2) == ("TYPE_FIGHTING", "TYPE_FIGHTING", "ABILITY_VITAL_SPIRIT", "ABILITY_NONE"),
        _ => false
    };
    internal static bool ValidMove(PokemonMoveDefinition m)
    {
        if (m.Id.Value is not ("pret:MOVE_TACKLE" or "pret:MOVE_SCRATCH" or "pret:MOVE_GROWL" or "pret:MOVE_TAIL_WHIP" or "pret:MOVE_SAND_ATTACK") ||
            !ForestBattleScope.ValidDefinition(m)) return false;
        string[] flags = m.Power > 1
            ? ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"]
            : ["FLAG_PROTECT_AFFECTED", "FLAG_MAGIC_COAT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"];
        return m.Flags.Length == flags.Length && m.Flags.ToHashSet().SetEquals(flags);
    }
}

using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Ordinary single battle before the first badge, no held items/weather/screens or unsupported volatile statuses.
/// KnownTargetAbility is the source side-indexed history, which can remain stale after a switch.</summary>
public sealed record PewterTrainerAiInput(WildBattleCombatant User, WildBattleCombatant Target,
    string? KnownTargetAbility = null, uint AiFlags = 1);
public sealed record PewterTrainerAbilityRead(int MoveSlot, string Reason, string Ability, bool Guessed);
public sealed record PewterTrainerAiDecision(TransitionStatus Status, int? MoveSlot, ImmutableArray<int> Scores,
    ImmutableArray<int> SimulatedDamagePercent, RngState Rng, ImmutableArray<RandomTrace> Trace, Diagnostic? Diagnostic)
{
    public bool Succeeded => Status == TransitionStatus.Accepted;
    public ImmutableArray<PewterTrainerAbilityRead> AbilityReads { get; init; } = [];
    public ImmutableArray<int> PredictedDamage { get; init; } = [];
}

/// <summary>Liam CHECK_BAD_MOVE and Brock CHECK_BAD_MOVE/CHECK_VIABILITY/TRY_TO_FAINT in source bit order.
/// Pure semantic-call RNG; neither frame emulation nor a general trainer AI.</summary>
public static class PewterTrainerAi
{
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string RulesVersion = "firered-pewter-trainer-ai-semantic-v1";

    public static PewterTrainerAiDecision Choose(PokemonCatalog catalog, PewterTrainerAiInput input, RngState rng, bool earlyStarterRules = false, bool route22Rules = false, bool route3Rules = false, bool mtMoonRules = false, bool ceruleanRules = false, bool billRules = false, bool vermilionRules = false, bool ssAnneRules = false, bool vermilionGymRules = false, bool route11Rules = false, bool rain = false, bool rockTunnelRules = false, bool reflect = false, bool celadonGymRules = false, bool rocketHideoutRules = false)
    {
        celadonGymRules |= rocketHideoutRules;
        rockTunnelRules |= celadonGymRules;
        route11Rules |= rockTunnelRules;
        PewterTrainerAiDecision Fault(string code) => new(TransitionStatus.Fault, null, [], [], rng, [],
            new(code, "Input is outside the supported Pewter trainer AI profile."));
        vermilionGymRules |= route11Rules; ssAnneRules |= vermilionGymRules;
        vermilionRules |= ssAnneRules;
        billRules |= vermilionRules;
        ceruleanRules |= billRules;
        mtMoonRules |= ceruleanRules;
        route3Rules |= mtMoonRules;
        route22Rules |= route3Rules;
        earlyStarterRules |= route22Rules;
        if (catalog is null || (catalog.Species.Count == 98) != (celadonGymRules && !rocketHideoutRules) || (catalog.Species.Count == 99) != rocketHideoutRules) return Fault("celadon_gym_trainer_ai_catalog_scope");
        string? error = Validate(catalog, input, earlyStarterRules, route22Rules, route3Rules, mtMoonRules, ceruleanRules, billRules, vermilionRules, ssAnneRules, vermilionGymRules, route11Rules, rockTunnelRules);
        if (error is not null) return Fault(error);
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) return Fault("pewter_trainer_ai_rng_algorithm");
        bool Usable(int slot) => route3Rules ? WildBattleRules.IsMoveSelectable(input.User, slot) && (!route11Rules || !input.Target.Imprison || slot >= input.User.Pokemon.Moves.Length || !input.Target.Pokemon.Moves.Any(m => m.Move == input.User.Pokemon.Moves[slot].Move)) : slot < input.User.Pokemon.Moves.Length && input.User.Pokemon.Moves[slot].CurrentPp > 0;
        if (!Enumerable.Range(0, 4).Any(Usable)) return Fault("pewter_trainer_ai_struggle_required");
        var current = rng;
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        var reads = ImmutableArray.CreateBuilder<PewterTrainerAbilityRead>();
        int Draw(string purpose, int bound)
        {
            var result = Gen3Rng.Next(current, purpose, bound);
            current = result.State; trace.Add(result.Trace); return result.Trace.Result;
        }
        var targetSpecies = catalog.Species[input.Target.Pokemon.Species];
        string Ability(int slot, string reason)
        {
            // Each script get_ability independently guesses. A known history value takes precedence
            // over the present species and actual ability, even following a player switch.
            bool trappedAbility = mtMoonRules && input.Target.Pokemon.AbilityId == "ABILITY_MAGNET_PULL" || route11Rules && input.Target.Pokemon.AbilityId == "ABILITY_ARENA_TRAP";
            bool guess = input.KnownTargetAbility is null && !trappedAbility && targetSpecies.Ability2 != "ABILITY_NONE";
            string ability = input.KnownTargetAbility ?? (guess
                ? Draw($"pewter-trainer.ai.slot.{slot}.ability.{reason}", 2) != 0 ? targetSpecies.Ability1 : targetSpecies.Ability2
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
                simulated[slot] = 100 - Draw($"pewter-trainer.ai.setup.slot.{slot}.damage-percent", 16);
            }
            // Source get_how_powerful_move_is evaluates all damaging slots, including zero PP.
            // None of the six admitted effects is in sDiscouragedPowerfulMoveEffects.
            for (int slot = 0; slot < input.User.Pokemon.Moves.Length; slot++)
            {
                var move = catalog.Moves[input.User.Pokemon.Moves[slot].Move];
                if (move.Power > 1) prediction[slot] = Predict(catalog, input.User, input.Target, move, simulated[slot], rain: route11Rules && rain);
            }
            void Score(int slot, int change) => scores[slot] = Math.Max(0, scores[slot] + change);
            // Bit 0: CHECK_BAD_MOVE. Normal/Rock cannot be immune against the closed 29 species;
            // their known-history abilities likewise contain no applicable damaging immunity.
            for (int slot = 0; slot < 4; slot++)
            {
                if (!Usable(slot)) continue;
                var move = catalog.Moves[input.User.Pokemon.Moves[slot].Move];
                if (move.Power > 1) _ = Ability(slot, "damage-immunity");
                string soundAbility = Ability(slot, "soundproof");
                if (mtMoonRules && soundAbility == "ABILITY_SOUNDPROOF" && MtMoonTrainerAi.IsSoundMove(move.Id))
                { Score(slot, -10); continue; }
                switch (move.Effect)
                {
                    case "EFFECT_DEFENSE_CURL":
                        if (input.User.DefenseStage == 12) Score(slot, -10);
                        break;
                    case "EFFECT_ACCURACY_DOWN":
                        if (input.Target.AccuracyStage == 0) { Score(slot, -10); break; }
                        if (Ability(slot, "accuracy-ability") == "ABILITY_KEEN_EYE") { Score(slot, -10); break; }
                        // A second independent get_ability follows even though no current species
                        // has Clear Body/White Smoke. This matters for unknown dual-ability targets.
                        _ = Ability(slot, "stat-change");
                        break;
                }
            }
            if (input.AiFlags == 7)
            {
                // Bit 1: CHECK_VIABILITY. Defense Curl has NO dispatch in the pinned source.
                // Bind only reacts to toxic/curse/perish-song/infatuation, absent from this profile.
                for (int slot = 0; slot < 4; slot++)
                {
                    if (!Usable(slot) || input.User.Pokemon.Moves[slot].Move.Value != "pret:MOVE_ROCK_TOMB") continue;
                    int userSpeed = Speed(input.User, route11Rules && rain), targetSpeed = Speed(input.Target, route11Rules && rain);
                    if (userSpeed == targetSpeed)
                    {
                        // GetWhoStrikesFirst returns 0 or 2 on ties. if_target_faster compares ==1,
                        // so BOTH outcomes mean false, but the tie still consumes a draw.
                        _ = Draw($"pewter-trainer.ai.viability.slot.{slot}.speed-tie", 2);
                    }
                    if (targetSpeed <= userSpeed) Score(slot, -3);
                    else if (Draw($"pewter-trainer.ai.viability.slot.{slot}.rock-tomb.faster-target", 256) >= 70) Score(slot, 2);
                }
                if (route3Rules && input.Target.InfatuationSource is not null)
                    for (int slot = 0; slot < 4; slot++)
                        if (Usable(slot) && catalog.Moves[input.User.Pokemon.Moves[slot].Move].Effect == "EFFECT_TRAP" &&
                            Draw($"pewter-trainer.ai.viability.slot.{slot}.trap.infatuation", 256) >= 128) Score(slot, 1);
                // Bit 2: TRY_TO_FAINT. Saved per-slot simulation percentages; no crit/accuracy roll.
                for (int slot = 0; slot < 4; slot++)
                {
                    if (!Usable(slot)) continue;
                    var move = catalog.Moves[input.User.Pokemon.Moves[slot].Move];
                    if (move.Power < 2) continue;
                    if (input.Target.Pokemon.CurrentHp <= prediction[slot]) Score(slot, 4);
                    else if (prediction.Any(value => value > prediction[slot])) Score(slot, -1);
                }
            }
            var best = new List<int> { 0 }; int maximum = scores[0];
            for (int slot = 1; slot < 4; slot++)
            {
                // Separate source if statements duplicate a newly encountered maximum.
                if (scores[slot] > maximum) { maximum = scores[slot]; best.Clear(); best.Add(slot); }
                if (scores[slot] == maximum) best.Add(slot);
            }
            int chosen = best[Draw("pewter-trainer.ai.choose-best", best.Count)];
            return new(TransitionStatus.Accepted, chosen, [.. scores], [.. simulated], current, trace.ToImmutable(), null)
                { AbilityReads = reads.ToImmutable(), PredictedDamage = [.. prediction] };
        }
        catch (OverflowException) { return Fault("pewter_trainer_ai_rng_exhausted"); }
    }

    private static int Speed(WildBattleCombatant mon, bool rain = false)
    {
        int speed = WildBattleDamage.Stat(mon.Pokemon.Stats.Speed * (rain && mon.Pokemon.AbilityId == "ABILITY_SWIFT_SWIM" ? 2 : 1), mon.SpeedStage);
        if (mon.SpeedBadgeBoost) speed = speed * 110 / 100;
        return mon.Status == WildBattleStatus.Paralysis ? speed / 4 : speed;
    }

    // Source CalculateBaseDamage/AI_CalcDmg/TypeCalc closure. These trainer moves are physical;
    // no admitted user ability modifies attack or the Normal/Rock base power. Kept separate from
    // execution because prediction uses no hit/critical rolls and considers depleted slots.
    private static int Predict(PokemonCatalog catalog, WildBattleCombatant user,
        WildBattleCombatant target, PokemonMoveDefinition move, int percent, bool rain = false)
    {
        int attack = WildBattleDamage.Stat(user.Pokemon.Stats.Attack, user.AttackStage);
        int defense = WildBattleDamage.Stat(target.Pokemon.Stats.Defense, target.DefenseStage);
        int value = checked(attack * move.Power * (2 * user.Pokemon.Level / 5 + 2)) / defense / 50;
        if (user.Status == WildBattleStatus.Burn) value /= 2;
        value = Math.Max(1, value) + 2;
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

    private static string? Validate(PokemonCatalog? catalog, PewterTrainerAiInput? input, bool earlyStarterRules, bool route22Rules, bool route3Rules, bool mtMoonRules, bool ceruleanRules, bool billRules, bool vermilionRules, bool ssAnneRules, bool vermilionGymRules, bool route11Rules, bool rockTunnelRules = false)
    {
        route11Rules |= rockTunnelRules;
        if (input is null) return "pewter_trainer_ai_input_missing";
        if (input.AiFlags is not (1 or 7)) return "pewter_trainer_ai_flags_unsupported";
        if (route3Rules && (input.User is { AttackBadgeBoost: true } or { SpeedBadgeBoost: true })) return "trainer_ai_enemy_badge";
        if (mtMoonRules && catalog?.Species.Count != (rockTunnelRules ? CeladonApproachBattleScope.SharedCatalogCount(catalog) : route11Rules ? 79 : ssAnneRules ? 73 : vermilionRules ? 70 : billRules ? 68 : ceruleanRules ? 65 : 56)) return "mtmoon_trainer_ai_catalog_scope";
        if (route3Rules && !mtMoonRules && catalog?.Species.Count != 40) return "route3_trainer_ai_catalog_scope";
        if (route22Rules && !route3Rules && catalog?.Species.Count != 32) return "route22_trainer_ai_catalog_scope";
        foreach (var (name, mon) in new[] { ("user", input.User), ("target", input.Target) })
        {
            if (mon is null) return "pewter_trainer_ai_" + name + "_missing";
            string? invalid = PokemonStateValidator.Validate(catalog, mon.Pokemon);
            if (invalid is not null) return "pewter_trainer_ai_" + name + "_" + invalid;
            if (mon.Pokemon.CurrentHp == 0) return "pewter_trainer_ai_" + name + "_fainted";
            if (!route3Rules && !WildBattleRules.Route3Neutral(mon)) return "trainer_ai_route3_volatile_unsupported";
            if (mon.HeldItem != "ITEM_NONE" && !(name == "target" && (rockTunnelRules ? RocketHideoutBattleScope.SupportsHeldItemForCatalog(catalog, mon.HeldItem) : route11Rules ? Route11BattleScope.SupportsHeldItem(mon.HeldItem) : ssAnneRules ? SsAnneBattleScope.SupportsHeldItem(mon.HeldItem) : vermilionRules ? VermilionBattleScope.SupportsHeldItem(mon.HeldItem) : route3Rules && mon.HeldItem == "ITEM_ORAN_BERRY"))) return "pewter_trainer_ai_held_item_unsupported";
            if (!WildBattleStatus.IsSupported(mon.Status, earlyStarterRules, route11Rules)) return "pewter_trainer_ai_status_unsupported";
            if (mon.AttackStage is < 0 or > 12 || mon.DefenseStage is < 0 or > 12 || mon.SpeedStage is < 0 or > 12 ||
                mon.AccuracyStage is < 0 or > 12 || mon.EvasionStage is < 0 or > 12) return "pewter_trainer_ai_stat_stage";
            if (!(rockTunnelRules ? RocketHideoutBattleScope.ValidSharedSpecies(catalog!.Species[mon.Pokemon.Species]) : route11Rules ? Route11BattleScope.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : ssAnneRules ? SsAnneBattleScope.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : vermilionRules ? VermilionBattleScope.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : billRules ? BillBattleScope.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : ceruleanRules ? CeruleanTrainerAi.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : mtMoonRules ? MtMoonTrainerAi.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : route3Rules ? Route3TrainerAi.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : route22Rules ? Route22TrainerAi.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : ValidSpecies(catalog!.Species[mon.Pokemon.Species]))) return "pewter_trainer_ai_species_type_ability";
        }
        if (route22Rules && (Route22TrainerAi.ValidateCombatant(catalog, input.User, route3Rules, mtMoonRules, ceruleanRules, billRules, vermilionRules, ssAnneRules, vermilionGymRules, route11Rules, rockTunnelRules) is not null || Route22TrainerAi.ValidateCombatant(catalog, input.Target, route3Rules, mtMoonRules, ceruleanRules, billRules, vermilionRules, ssAnneRules, vermilionGymRules, route11Rules, rockTunnelRules) is not null)) return "pewter_trainer_ai_route22_combatant";
        if (!ValidUser(input.User.Pokemon, input.AiFlags)) return "pewter_trainer_ai_user_roster";
        foreach (var slot in input.User.Pokemon.Moves)
            if (!ValidMove(catalog!.Moves[slot.Move])) return "pewter_trainer_ai_move_definition";
        if (input.KnownTargetAbility is { } known && !(rockTunnelRules ? RocketHideoutBattleScope.SupportsAbilityForCatalog(catalog, known) : route11Rules ? Route11BattleScope.SupportsAbility(known) : ssAnneRules ? SsAnneBattleScope.SupportsAbility(known) : vermilionRules ? VermilionBattleScope.SupportsAbility(known) : billRules ? BillBattleScope.SupportsAbility(known) : ceruleanRules ? CeruleanTrainerAi.SupportsAbility(known) : mtMoonRules ? MtMoonTrainerAi.SupportsAbility(known) : route3Rules ? Route3TrainerAi.SupportsAbility(known) : route22Rules ? Route22BattleScope.SupportsAbility(known) : SupportsAbility(known))) return "pewter_trainer_ai_ability_history";
        return null;
    }

    internal static bool ValidUser(PokemonInstance p, uint flags)
    {
        string[]? moves = (flags, p.SourceSpeciesId, p.Level) switch
        {
            (1, 74, 10) or (7, 74, 12) => ["TACKLE", "DEFENSE_CURL"],
            (1, 27, 11) => ["SCRATCH", "DEFENSE_CURL", "SAND_ATTACK"],
            (7, 95, 14) => ["TACKLE", "BIND", "ROCK_TOMB"],
            _ => null
        };
        return moves is not null && p.Moves.Select(x => x.Move.Value).SequenceEqual(moves.Select(x => "pret:MOVE_" + x));
    }

    internal static bool SupportsAbility(string ability) => ForestBattleScope.SupportsAbility(ability) ||
        ability is "ABILITY_ROCK_HEAD" or "ABILITY_STURDY" or "ABILITY_SAND_VEIL";
    internal static bool ValidSpecies(PokemonSpeciesDefinition s)
    {
        if (ForestTrainerAi.ValidSpecies(s)) return true;
        var expected = s.SourceSpeciesId switch
        {
            74 or 75 or 76 or 95 => ("TYPE_ROCK", "TYPE_GROUND", "ABILITY_ROCK_HEAD", "ABILITY_STURDY"),
            27 or 28 => ("TYPE_GROUND", "TYPE_GROUND", "ABILITY_SAND_VEIL", "ABILITY_NONE"),
            208 => ("TYPE_STEEL", "TYPE_GROUND", "ABILITY_ROCK_HEAD", "ABILITY_STURDY"),
            _ => default
        };
        return expected != default && (s.Type1, s.Type2, s.Ability1, s.Ability2) == expected;
    }

    internal static bool ValidMove(PokemonMoveDefinition m)
    {
        var e = m.Id.Value switch
        {
            "pret:MOVE_TACKLE" => (33, 35, 35, 95, "TYPE_NORMAL", "EFFECT_HIT", "MOVE_TARGET_SELECTED", 0),
            "pret:MOVE_SCRATCH" => (10, 35, 40, 100, "TYPE_NORMAL", "EFFECT_HIT", "MOVE_TARGET_SELECTED", 0),
            "pret:MOVE_DEFENSE_CURL" => (111, 40, 0, 0, "TYPE_NORMAL", "EFFECT_DEFENSE_CURL", "MOVE_TARGET_USER", 0),
            "pret:MOVE_SAND_ATTACK" => (28, 15, 0, 100, "TYPE_GROUND", "EFFECT_ACCURACY_DOWN", "MOVE_TARGET_SELECTED", 0),
            "pret:MOVE_BIND" => (20, 20, 15, 75, "TYPE_NORMAL", "EFFECT_TRAP", "MOVE_TARGET_SELECTED", 100),
            "pret:MOVE_ROCK_TOMB" => (317, 10, 50, 80, "TYPE_ROCK", "EFFECT_SPEED_DOWN_HIT", "MOVE_TARGET_SELECTED", 100),
            _ => default
        };
        string[] flags = m.Id.Value switch
        {
            "pret:MOVE_DEFENSE_CURL" => ["FLAG_SNATCH_AFFECTED"],
            "pret:MOVE_SAND_ATTACK" => ["FLAG_PROTECT_AFFECTED", "FLAG_MAGIC_COAT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_ROCK_TOMB" => ["FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            _ => ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"]
        };
        return e != default && (m.SourceMoveId, m.Pp, m.Power, m.Accuracy, m.Type, m.Effect, m.Target, m.SecondaryEffectChance) == e &&
            m.Priority == 0 && !m.Flags.IsDefault && m.Flags.Length == flags.Length && m.Flags.ToHashSet().SetEquals(flags);
    }
}

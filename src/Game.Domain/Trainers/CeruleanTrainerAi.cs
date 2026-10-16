using System.Collections.Immutable;
using Game.Domain.Battle;
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
    public static CeruleanTrainerAiDecision Choose(CeruleanTrainerAiInput input,
        RngState rng, SourceTrainerAiSlotContext slots, bool rain = false, bool reflect = false)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var catalog = slots.Catalog;
        CeruleanTrainerAiDecision Fault(string code) => new(TransitionStatus.Fault, null, [], [], rng, [],
            new(code, "Invalid trainer AI battle context."));
        string? error = slots.Validate(input, rng);
        if (error is not null) return Fault(error);
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) return Fault("cerulean_trainer_ai_rng_algorithm");
        bool Usable(int slot) => slots.IsUsable(slot);
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
        var facts = new SourceTrainerAiFacts(slots);
        string Ability(int slot, string reason)
        {
            // Cmd_get_ability consults side history, then reveals actual trapping abilities,
            // otherwise independently guesses a dual ability on every script instruction.
            string actualAbility = WildBattleEffective.Ability(input.Target);
            bool trappedAbility = actualAbility == "ABILITY_MAGNET_PULL" || actualAbility == "ABILITY_ARENA_TRAP" || actualAbility == "ABILITY_SHADOW_TAG";
            bool guess = input.KnownTargetAbility is null && !trappedAbility && targetSpecies.Ability2 != "ABILITY_NONE";
            string ability = input.KnownTargetAbility ?? (guess
                ? Draw($"cerulean-trainer.ai.slot.{slot}.ability.{reason}", 2) != 0 ? targetSpecies.Ability1 : targetSpecies.Ability2
                : trappedAbility ? actualAbility : targetSpecies.Ability1);
            reads.Add(new(slot, reason, ability, guess));
            return ability;
        }
        try
        {
            var scores = new int[4]; var simulated = new int[4]; var prediction = new int[4];
            MoveId? sourceCurrentMove = null;
            for (int slot = 0; slot < 4; slot++)
            {
                scores[slot] = Usable(slot) ? 100 : 0;
                simulated[slot] = slots.Simulated[slot];
            }
            for (int slot = 0; slot < input.User.Pokemon.Moves.Length; slot++)
            {
                var move = catalog.Moves[input.User.Pokemon.Moves[slot].Move];
                if (move.Power > 1) prediction[slot] = Predict(catalog, input.User, input.Target, move, simulated[slot], rain: rain, reflect: reflect, slots: slots);
            }
            sourceCurrentMove = SourceTrainerAiScript.Score(facts, input.AiFlags, scores, prediction,
                slot => slot < input.User.Pokemon.Moves.Length && input.User.Pokemon.Moves[slot].CurrentPp > 0,
                Draw, Ability);
            var best = new List<int> { 0 }; int maximum = scores[0];
            for (int slot = 1; slot < 4; slot++)
            {
                if (scores[slot] > maximum) { maximum = scores[slot]; best.Clear(); best.Add(slot); }
                if (scores[slot] == maximum) best.Add(slot);
            }
            int chosen = best[Draw("cerulean-trainer.ai.choose-best", best.Count)];
            return new(TransitionStatus.Accepted, chosen, [.. scores], [.. simulated], current, trace.ToImmutable(), null)
                { AbilityReads = reads.ToImmutable(), PredictedDamage = [.. prediction], SourceCurrentMove = sourceCurrentMove };
        }
        catch (SourceTrainerAiScriptFault ex) { return Fault(ex.Message); }
        catch (TypeInitializationException ex) when (ex.InnerException is InvalidOperationException invalid &&
            invalid.Message.StartsWith("source_trainer_ai_program_", StringComparison.Ordinal))
        { return Fault(ex.InnerException!.Message); }
        catch (OverflowException) { return Fault("cerulean_trainer_ai_rng_exhausted"); }
    }

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
    private static ElementType ParseType(string type) => Enum.Parse<ElementType>(type[5..], true);

    // AI_CalcDmg uses base power, without execution's variable-power preprocessing. TypeCalc
    // applies STAB but Levitate only sets a discarded local flag, leaving damage nonzero.
    internal static int Predict(PokemonCatalog catalog, WildBattleCombatant user, WildBattleCombatant target,
        PokemonMoveDefinition move, int percent, SourceTrainerAiSlotContext slots, bool clearUserAbility = false, bool rain = false, bool reflect = false)
    {
        int value = BasePrediction(user, target, move.Power, ParseType(move.Type), move.Effect, slots, clearUserAbility, rain, reflect, move.Target);
        value = TypePrediction(catalog, user, target, move, value);
        return Math.Max(1, value * percent / 100);
    }
    internal static int BasePrediction(WildBattleCombatant user, WildBattleCombatant target, int power, ElementType type,
        string? effect, SourceTrainerAiSlotContext slots, bool clearUserAbility = false, bool rain = false, bool reflect = false, string? moveTarget = null)
    {
        var facts = new SourceTrainerAiFacts(slots);
        string ability = clearUserAbility ? "ABILITY_NONE" : WildBattleEffective.Ability(user);
        // AI_CalcDmg reads the actual battle stats/items/field, independently of the script's
        // guessed ability and known held-effect history. Those script queries stay unchanged.
        string oppositeAbility = ability switch { "ABILITY_PLUS" => "ABILITY_MINUS", "ABILITY_MINUS" => "ABILITY_PLUS", _ => "" };
        bool weatherSuppressed = false, plusMinusBoost = false, mudSport = false, waterSport = false;
        // FIELD_SPORT scans every retained body, including fainted/absent positions. Replacement
        // calculation observes the source's cleared fainted attacker ability, not its party ability.
        foreach (var cell in slots.Frame.Slots.Take(slots.Frame.BattlerCount))
        {
            if (cell.Body is not { } body) continue;
            string fieldAbility = cell.Slot == slots.Actor ? ability : WildBattleEffective.Ability(body);
            weatherSuppressed |= fieldAbility is "ABILITY_CLOUD_NINE" or "ABILITY_AIR_LOCK";
            plusMinusBoost |= oppositeAbility.Length != 0 && fieldAbility == oppositeAbility;
            mudSport |= body.MudSport;
            waterSport |= body.WaterSport;
        }
        int value = SourceBattleDamage.CalculateBase(slots.Catalog, user, target, power, type, effect, moveTarget,
            new(Rain: rain || facts.Rain, Reflect: reflect, Sun: facts.Sun, LightScreen: facts.TargetLightScreen,
                WeatherSuppressed: weatherSuppressed,
                SolarBeamWeakWeather: effect == "EFFECT_SOLAR_BEAM" && (rain || facts.Rain || facts.Sandstorm || facts.Hail),
                DoubleBattle: facts.Doubles, DefenderCount: facts.TwoTargets ? 2 : 1,
                PlusMinusBoost: plusMinusBoost, MudSport: mudSport, WaterSport: waterSport, UserAbility: ability));
        if (user.ChargeTurnsRemaining > 0 && type == ElementType.Electric) value *= 2;
        if (facts.HelpingHand == true) value = value * 15 / 10;
        return value;
    }
    internal static int TypePrediction(PokemonCatalog catalog, WildBattleCombatant user, WildBattleCombatant target,
        PokemonMoveDefinition move, int value)
    {
        if (move.Id == WildBattleRules.StruggleMove) return value;
        var a = WildBattleEffective.Types(catalog, user); var d = WildBattleEffective.Types(catalog, target);
        if (move.Type == a.Type1 || move.Type == a.Type2) value = value * 15 / 10;
        if (WildBattleEffective.Ability(target) == "ABILITY_LEVITATE" && move.Type == "TYPE_GROUND") return value;
        foreach (var effect in TypeRules.OrderedEffects(ParseType(move.Type), ParseType(d.Type1), ParseType(d.Type2)))
        {
            if (target.Identified && effect.MultiplierTenths == 0 && move.Type is "TYPE_NORMAL" or "TYPE_FIGHTING") continue;
            value = value * effect.MultiplierTenths / 10;
            if (value == 0 && effect.MultiplierTenths != 0) value = 1;
        }
        return value;
    }
}

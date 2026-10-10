using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.FirstBattle;

/// <summary>
/// The aiFlags=7 semantic-call profile for the original level-five lab starters.
/// This is the reachable four-move script closure, not a general AI interpreter or frame RNG emulator.
/// </summary>
public static class FirstBattleAi
{
    public const string RulesVersion = "firered-first-battle-ai-semantic-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    private const string None = "pret:MOVE_NONE";
    private const string Growl = "pret:MOVE_GROWL";
    private const string TailWhip = "pret:MOVE_TAIL_WHIP";

    public static FirstBattleAiDecision Choose(PokemonCatalog catalog, FirstBattleAiInput input, RngState rng)
    {
        FirstBattleAiDecision Fault(string code, string message) =>
            new(TransitionStatus.Fault, null, [], [], rng, [], new(code, message));

        string? error = Validate(catalog, input);
        if (error is not null) return Fault(error, "Input is outside the supported lab first-battle AI profile.");
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId)
            return Fault("first_battle_ai_rng_algorithm", "The semantic profile requires the Gen III LCG.");
        if (input.User.Moves.Take(2).All(m => m.CurrentPp == 0))
            return Fault("first_battle_ai_struggle_required", "All moves have zero PP. The battle controller must select Struggle before calling AI.");

        int userHp = 100 * input.User.CurrentHp / input.User.Stats.MaxHp;
        int targetHp = 100 * input.Target.CurrentHp / input.Target.Stats.MaxHp;
        bool Usable(int slot) => slot < input.User.Moves.Length &&
            input.User.Moves[slot].Move.Value != None && input.User.Moves[slot].CurrentPp > 0;
        // These branches depend only on validated input, so reserve the exact draw count before setup.
        int drawCount = 5;
        for (int slot = 0; slot < 4; slot++)
        {
            if (!Usable(slot)) continue;
            switch (input.User.Moves[slot].Move.Value)
            {
                case Growl:
                    drawCount++; // No original starter type occurs in the source's six-entry physical list.
                    if (input.TargetAttackStage <= 3) drawCount++;
                    break;
                case TailWhip:
                    if (userHp < 70 || input.TargetDefenseStage <= 3) drawCount++;
                    break;
            }
        }
        if (rng.Cursor > ulong.MaxValue - (ulong)drawCount)
            return Fault("first_battle_ai_rng_exhausted", "The RNG cursor cannot accommodate this complete AI decision.");

        var currentRng = rng;
        var trace = ImmutableArray.CreateBuilder<RandomTrace>(drawCount);
        int Draw(string purpose, int bound)
        {
            var draw = Gen3Rng.Next(currentRng, purpose, bound);
            currentRng = draw.State;
            trace.Add(draw.Trace);
            return draw.Trace.Result;
        }
        var scores = new int[4];
        var simulated = new int[4];
        for (int slot = 0; slot < 4; slot++)
        {
            scores[slot] = Usable(slot) ? 100 : 0;
            // Setup samples even empty slots and zero-PP slots.
            simulated[slot] = 100 - Draw($"first-battle.ai.setup.slot.{slot}.damage-percent", 16);
        }
        void Score(int slot, int change) => scores[slot] = Math.Max(0, scores[slot] + change);

        // Bit 0: CheckBadMove. In this closure Normal is effective, and the real starter
        // abilities have no applicable immunity. Each species has one ability, hence no guess draw.
        for (int slot = 0; slot < 4; slot++)
        {
            if (!Usable(slot)) continue;
            string move = input.User.Moves[slot].Move.Value;
            if ((move == Growl && input.TargetAttackStage == 0) ||
                (move == TailWhip && input.TargetDefenseStage == 0)) Score(slot, -10);
        }

        // Bit 1: CheckViability. Keep source branch order and strict inequalities.
        for (int slot = 0; slot < 4; slot++)
        {
            if (!Usable(slot)) continue;
            switch (input.User.Moves[slot].Move.Value)
            {
                case Growl:
                    if (input.TargetAttackStage != 6)
                    {
                        Score(slot, -1);
                        if (userHp <= 90) Score(slot, -1);
                        if (input.TargetAttackStage <= 3 &&
                            Draw($"first-battle.ai.viability.slot.{slot}.growl.low-attack", 256) >= 50)
                            Score(slot, -2);
                    }
                    if (targetHp <= 70) Score(slot, -2);
                    // Source omits Poison, Flying and Ghost. Bulbasaur's Poison must not skip this draw.
                    if (Draw($"first-battle.ai.viability.slot.{slot}.growl.nonphysical-type", 256) >= 50)
                        Score(slot, -2);
                    break;
                case TailWhip:
                    if ((userHp < 70 || input.TargetDefenseStage <= 3) &&
                        Draw($"first-battle.ai.viability.slot.{slot}.tail-whip.low-hp-or-defense", 256) >= 50)
                        Score(slot, -2);
                    if (targetHp <= 70) Score(slot, -2);
                    break;
            }
        }

        // Bit 2: TryToFaint. The sole damage move is MOST_POWERFUL; status is DISCOURAGED,
        // never NOT_MOST_POWERFUL. Prediction reuses setup's percentage and never rolls crit/accuracy.
        for (int slot = 0; slot < 4; slot++)
        {
            if (!Usable(slot)) continue;
            var move = catalog.Moves[input.User.Moves[slot].Move];
            if (move.Power < 2) continue;
            int damage = FirstBattleDamage.Calculate(catalog, input.User, input.Target, move.Id,
                input.UserAttackStage, input.TargetDefenseStage, false, simulated[slot]).Damage;
            if (input.Target.CurrentHp <= damage) Score(slot, 4);
        }

        // These two source if statements are independent: a new maximum is inserted twice.
        // In particular [0,100,0,0] produces [1,1], and its final draw has bound 2, not 1.
        var best = new List<int> { 0 };
        int maximum = scores[0];
        for (int slot = 1; slot < 4; slot++)
        {
            if (maximum < scores[slot])
            {
                maximum = scores[slot];
                best.Clear();
                best.Add(slot);
            }
            if (maximum == scores[slot]) best.Add(slot);
        }
        int chosen = best[Draw("first-battle.ai.choose-best", best.Count)];
        return new(TransitionStatus.Accepted, chosen, [.. scores], [.. simulated], currentRng, trace.ToImmutable(), null);
    }

    private static string? Validate(PokemonCatalog? catalog, FirstBattleAiInput? input)
    {
        if (input is null) return "first_battle_ai_input_missing";
        if (input.AiFlags != 7) return "first_battle_ai_flags_unsupported";
        if (input.UserAttackStage is < 0 or > 12 || input.UserDefenseStage is < 0 or > 12 ||
            input.TargetAttackStage is < 0 or > 12 || input.TargetDefenseStage is < 0 or > 12)
            return "first_battle_ai_stat_stage";
        foreach (var (name, pokemon) in new[] { ("user", input.User), ("target", input.Target) })
        {
            if (pokemon is null) return $"first_battle_ai_{name}_missing";
            string? damageMove = pokemon.Species.Value switch
            {
                "pret:SPECIES_BULBASAUR" or "pret:SPECIES_SQUIRTLE" => "pret:MOVE_TACKLE",
                "pret:SPECIES_CHARMANDER" => "pret:MOVE_SCRATCH",
                _ => null
            };
            if (damageMove is null) return $"first_battle_ai_{name}_species";
            if (pokemon.Level != 5 || pokemon.Experience != 135 || pokemon.Evs != SixStats.Zero)
                return $"first_battle_ai_{name}_initial_starter";
            if (pokemon.Moves.IsDefault || pokemon.Moves.Length is < 2 or > 4 || pokemon.Moves.Any(m => m is null))
                return $"first_battle_ai_{name}_moves";
            string statusMove = pokemon.Species.Value == "pret:SPECIES_SQUIRTLE" ? TailWhip : Growl;
            if (pokemon.Moves[0].Move.Value != damageMove || pokemon.Moves[1].Move.Value != statusMove ||
                pokemon.Moves.Any(m => m.PpUps != 0) ||
                pokemon.Moves.Skip(2).Any(m => m.Move.Value != None || m.CurrentPp != 0 || m.MaximumPp != 0))
                return $"first_battle_ai_{name}_moves";
            // PokemonInstance normally stores a compact move array. Explicit trailing NONE is
            // accepted here only as zero-PP padding and removed for the common state validator.
            string? invalid = PokemonStateValidator.Validate(catalog, pokemon with { Moves = [.. pokemon.Moves.Take(2)] });
            if (invalid is not null) return $"first_battle_ai_{name}_{invalid}";
            if (pokemon.CurrentHp == 0) return $"first_battle_ai_{name}_fainted";
            var species = catalog!.Species[pokemon.Species];
            var expected = pokemon.Species.Value switch
            {
                "pret:SPECIES_BULBASAUR" => ("TYPE_GRASS", "TYPE_POISON", "ABILITY_OVERGROW"),
                "pret:SPECIES_CHARMANDER" => ("TYPE_FIRE", "TYPE_FIRE", "ABILITY_BLAZE"),
                _ => ("TYPE_WATER", "TYPE_WATER", "ABILITY_TORRENT")
            };
            if ((species.Type1, species.Type2, species.Ability1) != expected || species.Ability2 != "ABILITY_NONE")
                return $"first_battle_ai_{name}_type_ability";
        }
        foreach (var spec in new[]
        {
            ("TACKLE", 33, 35, 35, 95, "EFFECT_HIT", "MOVE_TARGET_SELECTED"),
            ("SCRATCH", 10, 35, 40, 100, "EFFECT_HIT", "MOVE_TARGET_SELECTED"),
            ("GROWL", 45, 40, 0, 100, "EFFECT_ATTACK_DOWN", "MOVE_TARGET_BOTH"),
            ("TAIL_WHIP", 39, 30, 0, 100, "EFFECT_DEFENSE_DOWN", "MOVE_TARGET_BOTH")
        })
        {
            if (!catalog!.Moves.TryGetValue(new MoveId("pret:MOVE_" + spec.Item1), out var move) ||
                move.SourceMoveId != spec.Item2 || move.Pp != spec.Item3 || move.Power != spec.Item4 ||
                move.Accuracy != spec.Item5 || move.Effect != spec.Item6 || move.Target != spec.Item7 ||
                move.Type != "TYPE_NORMAL" || move.SecondaryEffectChance != 0 || move.Priority != 0)
                return "first_battle_ai_move_definition";
        }
        return null;
    }
}

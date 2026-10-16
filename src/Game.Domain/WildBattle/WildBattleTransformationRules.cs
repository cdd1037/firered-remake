using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.Rules;

namespace Game.Domain.WildBattle;

/// <summary>Party identity and the authenticated donor at the instant of Transform. Neither is the current battle view.</summary>
public sealed record WildBattleTransformation(PokemonInstance OriginalPokemon, PokemonInstance CopiedPokemon,
    ImmutableArray<int> OriginalPpUps)
{
    // Poke Flute independently tests party ability and copied battle ability.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public uint? PartyStatusOverride { get; init; }
    // Cmd_getexp recalculates every battle stat after a level gain, even under Transform.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public BattleStats? LevelUpStats { get; init; }
}

public sealed record WildBattleTransformedEvent(long Sequence, int Turn, WildBattleSide Actor,
    WildBattleSide Target, SpeciesId OriginalSpecies, SpeciesId AppearanceSpecies,
    uint AppearancePersonality, uint AppearanceOriginalTrainerId, PokemonInstance BattleView) : WildBattleEvent(Sequence, Turn);

/// <summary>Only Pokemon is the live battle view. Persistence, capture delivery and reward recipients use PersistentPokemon.</summary>
public static class WildBattleTransformationRules
{
    public static PokemonInstance PersistentPokemon(WildBattleCombatant mon) => mon.Transform is { } transform
        ? transform.OriginalPokemon with { CurrentHp = mon.Pokemon.CurrentHp } : WildBattleCopyMoveRules.PersistentPokemon(mon);

    public static uint PersistentStatus(WildBattleCombatant mon) => SourcePartyStatusRules.FromBattle(
        mon.SourcePartyStatusOverride ?? mon.Transform?.PartyStatusOverride ?? mon.Status);

    public static WildBattleCombatant Restore(WildBattleCombatant mon) => mon.Transform is null && mon.CopyMoves is null && mon.SourcePartyStatusOverride is null ? mon :
        mon with { Pokemon = PersistentPokemon(mon), Status = PersistentStatus(mon), Transform = null, CopyMoves = null, CalledOverlay = null, SourcePartyStatusOverride = null };

    /// <summary>Apply a validated party medicine/EXP update without replacing copied moves. A level gain uses the source stat refresh.</summary>
    public static WildBattleCombatant ApplyPersistentPokemon(WildBattleCombatant mon, PokemonInstance pokemon)
    {
        if (mon.Transform is not { } transform) return WildBattleCopyMoveRules.ApplyPersistentPokemon(mon, pokemon);
        bool levelUp = pokemon.Level != transform.OriginalPokemon.Level && mon.Pokemon.CurrentHp > 0;
        var updated = transform with { OriginalPokemon = pokemon, LevelUpStats = levelUp ? pokemon.Stats : transform.LevelUpStats };
        var view = mon.Pokemon with {
            Level = pokemon.Level, Experience = pokemon.Experience, Evs = pokemon.Evs,
            StatCalculationEvs = pokemon.StatCalculationEvs, Friendship = pokemon.Friendship,
            CurrentHp = pokemon.CurrentHp,
            Stats = levelUp ? pokemon.Stats : mon.Pokemon.Stats with { MaxHp = pokemon.Stats.MaxHp }
        };
        return SourceBattleHeldItems.ReconcileChoice(mon with { Pokemon = view, Transform = updated });
    }

    // BattleScript_EffectTransform has no accuracy, Protect, Safeguard or Substitute command.
    // Cmd_transformdataexecution checks only the target's transformed/semi-invulnerable flags.
    public static string? FailureReason(WildBattleCombatant target) => target.Transform is not null
        ? "target-transformed" : WildBattleRules.CalledSemiInvulnerable(target) != WildBattleSemiInvulnerable.None ? "semi-invulnerable" : null;

    internal static WildBattleCombatant Copy(PokemonCatalog catalog, WildBattleCombatant actor, WildBattleCombatant target)
    {
        var original = PersistentPokemon(actor);
        // Re-transforming is allowed; ppBonuses are retained from the CURRENT battle slots.
        var bonuses = Enumerable.Range(0, 4).Select(i => i < actor.Pokemon.Moves.Length ? actor.Pokemon.Moves[i].PpUps : 0).ToImmutableArray();
        var transformation = new WildBattleTransformation(original, target.Pokemon, bonuses) { PartyStatusOverride = actor.Transform?.PartyStatusOverride };
        var moves = target.Pokemon.Moves.Select((slot, i) => {
            int pp = catalog.Moves[slot.Move].Pp;
            return new PokemonMoveSlot(slot.Move, Math.Min(5, pp), pp + pp * 20 * bonuses[i] / 100, bonuses[i]);
        }).ToImmutableArray();
        var view = BuildView(catalog, transformation, moves, actor.Pokemon.CurrentHp);
        return actor with { Pokemon = view, Transform = transformation, CopyMoves = null, CalledOverlay = target.CalledOverlay,
            AttackStage = target.AttackStage, DefenseStage = target.DefenseStage, SpeedStage = target.SpeedStage,
            AccuracyStage = target.AccuracyStage, EvasionStage = target.EvasionStage,
            SpecialAttackStageDelta = target.SpecialAttackStageDelta, SpecialDefenseStageDelta = target.SpecialDefenseStageDelta,
            DisabledMove = null, DisableTurnsRemaining = 0 };
    }

    private static PokemonInstance BuildView(PokemonCatalog catalog, WildBattleTransformation transform,
        ImmutableArray<PokemonMoveSlot> moves, int hp)
    {
        var original = transform.OriginalPokemon; var copied = transform.CopiedPokemon;
        return original with { Species = copied.Species, SourceSpeciesId = copied.SourceSpeciesId,
            Stats = transform.LevelUpStats ?? copied.Stats with { MaxHp = original.Stats.MaxHp },
            Ivs = copied.Ivs, AbilityId = copied.AbilityId, AbilitySlot = copied.AbilitySlot,
            Gender = PokemonFactory.GenderFromPersonality(catalog.Species[copied.Species].GenderRatio, original.Personality),
            Moves = moves, CurrentHp = hp };
    }

    internal static string? Validate(PokemonCatalog? catalog, WildBattleCombatant mon, bool enabled,
        TrainerTowerEncounterRoster? originalTower = null, TrainerTowerEncounterRoster? donorTower = null)
    {
        if (mon.Transform is not { } transform) return SourcePokemonValidator.ValidateForBattle(catalog, mon.Pokemon, originalTower);
        if (!enabled) return "transform_profile";
        if (SourcePokemonValidator.ValidateForBattle(catalog, transform.OriginalPokemon, originalTower) is { } originalError) return "transform_original_" + originalError;
        if (SourcePokemonValidator.ValidateForBattle(catalog, transform.CopiedPokemon, donorTower) is { } targetError) return "transform_target_" + targetError;
        if (transform.PartyStatusOverride is { } partyStatus && !(
            partyStatus == 0 && WildBattleStatus.IsSleeping(mon.Status) ||
            WildBattleStatus.IsSleeping(partyStatus) && mon.Status == 0)) return "transform_party_status";
        if (transform.OriginalPpUps.IsDefault || transform.OriginalPpUps.Length != 4 || transform.OriginalPpUps.Any(x => x is < 0 or > 3)) return "transform_pp_ups";
        if (transform.LevelUpStats is not null && transform.LevelUpStats != transform.OriginalPokemon.Stats) return "transform_level_up_stats";
        if (mon.Pokemon.CurrentHp < 0 || mon.Pokemon.CurrentHp > transform.OriginalPokemon.Stats.MaxHp) return "transform_hp";
        if (mon.Pokemon.Moves.IsDefault || mon.Pokemon.Moves.Length != transform.CopiedPokemon.Moves.Length) return "transform_moves";
        for (int i = 0; i < mon.Pokemon.Moves.Length; i++)
        {
            var move = mon.Pokemon.Moves[i];
            int pp = catalog!.Moves[transform.CopiedPokemon.Moves[i].Move].Pp, bonus = transform.OriginalPpUps[i];
            if (move is null || move.Move != transform.CopiedPokemon.Moves[i].Move || move.PpUps != bonus ||
                move.MaximumPp != pp + pp * 20 * bonus / 100 || move.CurrentPp < 0 || move.CurrentPp > Math.Min(5, pp)) return "transform_pp";
        }
        var expected = BuildView(catalog!, transform, mon.Pokemon.Moves, mon.Pokemon.CurrentHp);
        return mon.Pokemon != expected ? "transform_view" : null;
    }
}

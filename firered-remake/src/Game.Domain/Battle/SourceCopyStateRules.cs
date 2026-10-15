using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
namespace Game.Domain.Battle;
internal static class SourceCopyStateRules
{
    private static bool PokemonShape(PokemonInstance? pokemon) => pokemon is not null && pokemon.Stats is not null &&
        pokemon.Ivs is not null && pokemon.Evs is not null && !pokemon.Moves.IsDefault && pokemon.Moves.All(m => m is not null);
    internal static string? ValidateShape(WildBattleCombatant body)
    {
        if (!PokemonShape(body.Pokemon)) return "source_copy_pokemon_shape";
        if (body.Transform is { } transform && (!PokemonShape(transform.OriginalPokemon) || !PokemonShape(transform.CopiedPokemon) ||
            transform.OriginalPpUps.IsDefault || transform.OriginalPpUps.Length != 4)) return "source_copy_transform_shape";
        if (body.CopyMoves is { } copies && (body.Transform is not null || copies.RulesVersion != WildBattleRules.CalledMoveKernelRulesVersion ||
            copies.Slots.IsDefaultOrEmpty || copies.Slots.Length > 4 || copies.Slots.Any(c => c is null || c.PersistentSlot is null ||
                c.Slot < 0 || c.Slot >= body.Pokemon.Moves.Length) || copies.Slots.Select(c => c.Slot).Distinct().Count() != copies.Slots.Length))
            return "source_copy_slots";
        return null;
    }
    internal static string? Validate(PokemonCatalog catalog, WildBattleCombatant body, SourceBattleFrame? frame = null, BattleSide side = BattleSide.Player)
    {
        var tower = frame?.TrainerTowerEncounter?.Roster;
        var originalTower = side == BattleSide.Opponent ? tower : null;
        if (ValidateShape(body) is { } shape) return shape;
        if (WildBattleTransformationRules.Validate(catalog, body, enabled: true, originalTower, tower) is { } live) return "source_copy_live_" + live;
        if (SourcePokemonValidator.ValidateForBattle(catalog, WildBattleTransformationRules.PersistentPokemon(body), originalTower) is { } permanent)
            return "source_copy_permanent_" + permanent;
        return null;
    }
}

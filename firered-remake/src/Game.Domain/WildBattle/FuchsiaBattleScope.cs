using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

/// <summary>Explicit107-species southern-route successor. Historical profiles remain strict.</summary>
internal static class FuchsiaBattleScope
{
    internal static bool Supports(MoveId id) => SnorlaxBattleScope.Supports(id) || FuchsiaPhysicalMoveDefinition.Supports(id) || id.Value == "pret:MOVE_TRANSFORM";
    internal static bool SupportsAbility(string ability) => SnorlaxBattleScope.SupportsAbility(ability);
    internal static bool SupportsSpecies(SpeciesId species) => PokemonFactory.FuchsiaSupportedSpecies.Contains(species);
    internal static bool SupportsHeldItem(string item) => SnorlaxBattleScope.SupportsHeldItem(item) || item == "ITEM_METAL_POWDER";
    internal static bool ValidSpecies(PokemonSpeciesDefinition? species) => EeveeBattleScope.ValidSharedSpecies(species) || PokemonFactory.ValidFuchsiaSpeciesDefinition(species);
    internal static bool SupportsWildOpponent(int sourceSpeciesId) => PokemonTowerBattleScope.SupportsWildOpponent(sourceSpeciesId) || sourceSpeciesId is 17 or 20 or 22 or 44 or 48 or 84 or 132;
    internal static bool ValidDefinition(PokemonMoveDefinition move) => SnorlaxBattleScope.ValidDefinition(move) || FuchsiaPhysicalMoveDefinition.ValidDefinition(move) ||
        move.Id.Value == "pret:MOVE_TRANSFORM" &&
        (move.SourceMoveId, move.Pp, move.Power, move.Accuracy, move.Type, move.Effect, move.Target, move.SecondaryEffectChance, move.Priority) ==
        (144, 10, 0, 0, "TYPE_NORMAL", "EFFECT_TRANSFORM", "MOVE_TARGET_SELECTED", 0, 0) &&
        !move.Flags.IsDefault && move.Flags.IsEmpty;

    // pokedex_entries.h weights in hectograms. Species-dependent damage reads the current battle species.
    internal static int WeightHectograms(int sourceSpeciesId) => sourceSpeciesId switch {
        83 => 150, 84 => 392, 85 => 852, 132 => 40, 133 => 65, 143 => 4600,
        _ => throw new ArgumentOutOfRangeException(nameof(sourceSpeciesId)) };
}

using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

/// <summary>Closed first-badge extension. Metadata imports never make arbitrary effects executable.</summary>
internal static class PewterBattleScope
{
    internal static bool Supports(MoveId move) => ForestBattleScope.Supports(move) || move.Value is
        "pret:MOVE_DEFENSE_CURL" or "pret:MOVE_BIND" or "pret:MOVE_ROCK_TOMB" or "pret:MOVE_METAL_CLAW";
    internal static bool SupportsOpponent(int species) => ForestBattleScope.SupportsOpponent(species) || species is 27 or 74 or 95;
    internal static bool SupportsAbility(string ability) => ForestBattleScope.SupportsAbility(ability) ||
        ability is "ABILITY_SAND_VEIL" or "ABILITY_ROCK_HEAD" or "ABILITY_STURDY";
    internal static bool ValidDefinition(PokemonMoveDefinition m)
    {
        if (ForestBattleScope.Supports(m.Id)) return ForestBattleScope.ValidDefinition(m);
        var expected = m.Id.Value switch
        {
            "pret:MOVE_DEFENSE_CURL" => (111, 40, 0, 0, "TYPE_NORMAL", "EFFECT_DEFENSE_CURL", "MOVE_TARGET_USER", 0, false),
            "pret:MOVE_BIND" => (20, 20, 15, 75, "TYPE_NORMAL", "EFFECT_TRAP", "MOVE_TARGET_SELECTED", 100, true),
            "pret:MOVE_ROCK_TOMB" => (317, 10, 50, 80, "TYPE_ROCK", "EFFECT_SPEED_DOWN_HIT", "MOVE_TARGET_SELECTED", 100, false),
            "pret:MOVE_METAL_CLAW" => (232, 35, 50, 95, "TYPE_STEEL", "EFFECT_ATTACK_UP_HIT", "MOVE_TARGET_SELECTED", 10, true),
            _ => default
        };
        return expected.Item1 != 0 && !m.Flags.IsDefault && m.Priority == 0 &&
            (m.SourceMoveId, m.Pp, m.Power, m.Accuracy, m.Type, m.Effect, m.Target, m.SecondaryEffectChance,
                m.Flags.Contains("FLAG_MAKES_CONTACT")) == expected;
    }
}

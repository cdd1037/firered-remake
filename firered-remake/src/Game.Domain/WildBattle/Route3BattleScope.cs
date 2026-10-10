using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

/// <summary>Ordinary singles with bounded Oran Berry support through Route3 / Route4 Center; no weather or Thrash.</summary>
internal static class Route3BattleScope
{
    internal static bool Supports(MoveId move) => Route22BattleScope.Supports(move) || move.Value is
        "pret:MOVE_POUND" or "pret:MOVE_DOUBLE_KICK" or "pret:MOVE_HORN_ATTACK" or "pret:MOVE_WRAP" or
        "pret:MOVE_SING" or "pret:MOVE_DISABLE" or "pret:MOVE_GLARE" or "pret:MOVE_SPLASH" or "pret:MOVE_ROLLOUT";
    internal static bool SupportsSpecies(SpeciesId species) => Route22BattleScope.SupportsSpecies(species) || Route3BattleData.HasWeight(species);
    internal static bool SupportsWildOpponent(int species) => Route22BattleScope.SupportsWildOpponent(species) || species is 23 or 29 or 32 or 39;
    internal static bool SupportsAbility(string ability) => Route22BattleScope.SupportsAbility(ability) || ability is
        "ABILITY_INTIMIDATE" or "ABILITY_SWIFT_SWIM" or "ABILITY_POISON_POINT" or "ABILITY_CUTE_CHARM";
    internal static bool ValidDefinition(PokemonMoveDefinition m)
    {
        if (Route22BattleScope.Supports(m.Id)) return Route22BattleScope.ValidDefinition(m);
        var expected = m.Id.Value switch
        {
            "pret:MOVE_POUND" => (1, 35, 40, 100, "TYPE_NORMAL", "EFFECT_HIT", "MOVE_TARGET_SELECTED", 0),
            "pret:MOVE_DOUBLE_KICK" => (24, 30, 30, 100, "TYPE_FIGHTING", "EFFECT_DOUBLE_HIT", "MOVE_TARGET_SELECTED", 0),
            "pret:MOVE_HORN_ATTACK" => (30, 25, 65, 100, "TYPE_NORMAL", "EFFECT_HIT", "MOVE_TARGET_SELECTED", 0),
            "pret:MOVE_WRAP" => (35, 20, 15, 85, "TYPE_NORMAL", "EFFECT_TRAP", "MOVE_TARGET_SELECTED", 100),
            "pret:MOVE_SING" => (47, 15, 0, 55, "TYPE_NORMAL", "EFFECT_SLEEP", "MOVE_TARGET_SELECTED", 0),
            "pret:MOVE_DISABLE" => (50, 20, 0, 55, "TYPE_NORMAL", "EFFECT_DISABLE", "MOVE_TARGET_SELECTED", 0),
            "pret:MOVE_GLARE" => (137, 30, 0, 75, "TYPE_NORMAL", "EFFECT_PARALYZE", "MOVE_TARGET_SELECTED", 0),
            "pret:MOVE_SPLASH" => (150, 40, 0, 0, "TYPE_NORMAL", "EFFECT_SPLASH", "MOVE_TARGET_USER", 0),
            "pret:MOVE_ROLLOUT" => (205, 20, 30, 90, "TYPE_ROCK", "EFFECT_ROLLOUT", "MOVE_TARGET_SELECTED", 0),
            _ => default
        };
        string[] flags = m.Id.Value switch
        {
            "pret:MOVE_SPLASH" => [],
            "pret:MOVE_DISABLE" => ["FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_SING" or "pret:MOVE_GLARE" => ["FLAG_PROTECT_AFFECTED", "FLAG_MAGIC_COAT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            _ => ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"]
        };
        return expected.Item1 != 0 && m.Priority == 0 &&
            (m.SourceMoveId, m.Pp, m.Power, m.Accuracy, m.Type, m.Effect, m.Target, m.SecondaryEffectChance) == expected &&
            !m.Flags.IsDefault && m.Flags.Length == flags.Length && flags.All(m.Flags.Contains);
    }
}

public static class Route3BattleData
{
    private static readonly IReadOnlyDictionary<string, int> Weights = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["pret:SPECIES_EKANS"] = 69, ["pret:SPECIES_NIDORAN_F"] = 70, ["pret:SPECIES_NIDORINA"] = 200,
        ["pret:SPECIES_NIDORAN_M"] = 90, ["pret:SPECIES_NIDORINO"] = 195, ["pret:SPECIES_JIGGLYPUFF"] = 55,
        ["pret:SPECIES_MAGIKARP"] = 100, ["pret:SPECIES_GYARADOS"] = 2350
    };
    internal static bool HasWeight(SpeciesId species) => Weights.ContainsKey(species.Value);
    public static int WeightHectograms(SpeciesId species) => Weights.TryGetValue(species.Value, out var weight)
        ? weight : Route22BattleData.WeightHectograms(species);
}

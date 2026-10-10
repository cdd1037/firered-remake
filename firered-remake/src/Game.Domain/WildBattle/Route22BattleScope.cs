using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

/// <summary>Fixed FireRed first-badge extension; importing a move does not enable its effect.</summary>
internal static class Route22BattleScope
{
    internal static bool Supports(MoveId move) => EarlyPartyBattleScope.Supports(move) || move.Value is
        "pret:MOVE_LEER" or "pret:MOVE_PECK" or "pret:MOVE_LOW_KICK" or "pret:MOVE_KARATE_CHOP" or
        "pret:MOVE_FURY_SWIPES" or "pret:MOVE_PURSUIT";

    internal static bool SupportsSpecies(SpeciesId species) => Route22BattleData.HasWeight(species);
    internal static bool SupportsWildOpponent(int species) => ForestBattleScope.SupportsOpponent(species) || species is 21 or 56;
    internal static bool SupportsAbility(string ability) => PewterBattleScope.SupportsAbility(ability) || ability == "ABILITY_VITAL_SPIRIT";

    internal static bool ValidDefinition(PokemonMoveDefinition m)
    {
        if (EarlyPartyBattleScope.Supports(m.Id)) return EarlyPartyBattleScope.ValidDefinition(m);
        var expected = m.Id.Value switch
        {
            "pret:MOVE_LEER" => (43, 30, 0, 100, "TYPE_NORMAL", "EFFECT_DEFENSE_DOWN", "MOVE_TARGET_BOTH"),
            "pret:MOVE_PECK" => (64, 35, 35, 100, "TYPE_FLYING", "EFFECT_HIT", "MOVE_TARGET_SELECTED"),
            "pret:MOVE_LOW_KICK" => (67, 20, 1, 100, "TYPE_FIGHTING", "EFFECT_LOW_KICK", "MOVE_TARGET_SELECTED"),
            "pret:MOVE_KARATE_CHOP" => (2, 25, 50, 100, "TYPE_FIGHTING", "EFFECT_HIGH_CRITICAL", "MOVE_TARGET_SELECTED"),
            "pret:MOVE_FURY_SWIPES" => (154, 15, 18, 80, "TYPE_NORMAL", "EFFECT_MULTI_HIT", "MOVE_TARGET_SELECTED"),
            "pret:MOVE_PURSUIT" => (228, 20, 40, 100, "TYPE_DARK", "EFFECT_PURSUIT", "MOVE_TARGET_SELECTED"),
            _ => default
        };
        string[] flags = m.Id.Value switch
        {
            "pret:MOVE_LEER" => ["FLAG_PROTECT_AFFECTED", "FLAG_MAGIC_COAT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_PURSUIT" => ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            _ => ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"]
        };
        return expected.Item1 != 0 && m.Priority == 0 && m.SecondaryEffectChance == 0 &&
            (m.SourceMoveId, m.Pp, m.Power, m.Accuracy, m.Type, m.Effect, m.Target) == expected &&
            !m.Flags.IsDefault && m.Flags.Length == flags.Length && flags.All(m.Flags.Contains);
    }
}

/// <summary>Hectograms from the pinned FireRed Pokédex. Unknown targets fail rather than using power=1.</summary>
public static class Route22BattleData
{
    private static readonly IReadOnlyDictionary<string, int> Weights = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["pret:SPECIES_BULBASAUR"] = 69, ["pret:SPECIES_IVYSAUR"] = 130,
        ["pret:SPECIES_CHARMANDER"] = 85, ["pret:SPECIES_CHARMELEON"] = 190,
        ["pret:SPECIES_SQUIRTLE"] = 90, ["pret:SPECIES_WARTORTLE"] = 225,
        ["pret:SPECIES_CATERPIE"] = 29, ["pret:SPECIES_METAPOD"] = 99, ["pret:SPECIES_BUTTERFREE"] = 320,
        ["pret:SPECIES_WEEDLE"] = 32, ["pret:SPECIES_KAKUNA"] = 100, ["pret:SPECIES_BEEDRILL"] = 295,
        ["pret:SPECIES_PIDGEY"] = 18, ["pret:SPECIES_PIDGEOTTO"] = 300,
        ["pret:SPECIES_RATTATA"] = 35, ["pret:SPECIES_RATICATE"] = 185, ["pret:SPECIES_PIKACHU"] = 60,
        ["pret:SPECIES_SPEAROW"] = 20, ["pret:SPECIES_FEAROW"] = 380, ["pret:SPECIES_MANKEY"] = 280,
        ["pret:SPECIES_SANDSHREW"] = 120, ["pret:SPECIES_GEODUDE"] = 200, ["pret:SPECIES_ONIX"] = 2100
    };

    internal static bool HasWeight(SpeciesId species) => Weights.ContainsKey(species.Value);
    public static int WeightHectograms(SpeciesId species) => Weights.TryGetValue(species.Value, out int weight)
        ? weight : throw new NotSupportedException("route22_target_weight_unsupported");

    public static int LowKickPower(int weightHectograms)
    {
        if (weightHectograms < 1) throw new ArgumentOutOfRangeException(nameof(weightHectograms));
        return weightHectograms switch { < 100 => 20, < 250 => 40, < 500 => 60, < 1000 => 80, < 2000 => 100, _ => 120 };
    }
}

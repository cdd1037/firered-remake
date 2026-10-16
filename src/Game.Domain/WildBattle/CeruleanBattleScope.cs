using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

/// <summary>Cerulean trainer effects and TM03. Metadata-only later effects remain rejected.</summary>
internal static class CeruleanBattleScope
{
    internal static bool Supports(MoveId move) => MtMoonBattleScope.Supports(move) || move.Value is
        "pret:MOVE_RECOVER" or "pret:MOVE_SWIFT" or "pret:MOVE_WATER_PULSE" or "pret:MOVE_TELEPORT" or "pret:MOVE_ICICLE_SPEAR" or "pret:MOVE_WATER_SPORT" or "pret:MOVE_HYPNOSIS" or "pret:MOVE_HEADBUTT";
    internal static bool SupportsSpecies(SpeciesId species) => MtMoonBattleScope.SupportsSpecies(species) || CeruleanBattleData.HasWeight(species);
    internal static bool SupportsAbility(string ability) => MtMoonBattleScope.SupportsAbility(ability) || ability is
        "ABILITY_SYNCHRONIZE" or "ABILITY_INSOMNIA" or "ABILITY_SHELL_ARMOR" or "ABILITY_WATER_VEIL" or "ABILITY_ILLUMINATE" or "ABILITY_NATURAL_CURE";
    internal static bool ValidDefinition(PokemonMoveDefinition m)
    {
        if (MtMoonBattleScope.Supports(m.Id)) return MtMoonBattleScope.ValidDefinition(m);
        var expected = m.Id.Value switch
        {
            "pret:MOVE_RECOVER" => (105, 20, 0, 0, "TYPE_NORMAL", "EFFECT_RESTORE_HP", "MOVE_TARGET_USER", 0, 0),
            "pret:MOVE_SWIFT" => (129, 20, 60, 0, "TYPE_NORMAL", "EFFECT_ALWAYS_HIT", "MOVE_TARGET_BOTH", 0, 0),
            "pret:MOVE_WATER_PULSE" => (352, 20, 60, 100, "TYPE_WATER", "EFFECT_CONFUSE_HIT", "MOVE_TARGET_SELECTED", 20, 0),
            "pret:MOVE_TELEPORT" => (100, 20, 0, 0, "TYPE_PSYCHIC", "EFFECT_TELEPORT", "MOVE_TARGET_USER", 0, 0),
            "pret:MOVE_ICICLE_SPEAR" => (333, 30, 10, 100, "TYPE_ICE", "EFFECT_MULTI_HIT", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_WATER_SPORT" => (346, 15, 0, 100, "TYPE_WATER", "EFFECT_WATER_SPORT", "MOVE_TARGET_USER", 0, 0),
            "pret:MOVE_HYPNOSIS" => (95, 20, 0, 60, "TYPE_PSYCHIC", "EFFECT_SLEEP", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_HEADBUTT" => (29, 15, 70, 100, "TYPE_NORMAL", "EFFECT_FLINCH_HIT", "MOVE_TARGET_SELECTED", 30, 0),
            _ => default
        };
        string[] flags = m.Id.Value switch
        {
            "pret:MOVE_RECOVER" => ["FLAG_SNATCH_AFFECTED"],
            "pret:MOVE_SWIFT" => ["FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_WATER_PULSE" => ["FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_TELEPORT" => [],
            "pret:MOVE_ICICLE_SPEAR" => ["FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_WATER_SPORT" => [],
            "pret:MOVE_HYPNOSIS" => ["FLAG_PROTECT_AFFECTED", "FLAG_MAGIC_COAT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_HEADBUTT" => ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            _ => []
        };
        return expected.Item1 != 0 &&
            (m.SourceMoveId, m.Pp, m.Power, m.Accuracy, m.Type, m.Effect, m.Target, m.SecondaryEffectChance, m.Priority) == expected &&
            !m.Flags.IsDefault && m.Flags.Length == flags.Length && flags.All(m.Flags.Contains);
    }
}

public static class CeruleanBattleData
{
    private static readonly IReadOnlyDictionary<string, int> Weights = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        // Legal final starter evolutions already exist in the catalog. Admit them only
        // in this profile; weights are the pinned FireRed Pokédex values in hectograms.
        ["pret:SPECIES_VENUSAUR"] = 1000,
        ["pret:SPECIES_CHARIZARD"] = 905,
        ["pret:SPECIES_BLASTOISE"] = 855,
        ["pret:SPECIES_ABRA"] = 195,
        ["pret:SPECIES_KADABRA"] = 565,
        ["pret:SPECIES_MACHOP"] = 195,
        ["pret:SPECIES_DROWZEE"] = 324,
        ["pret:SPECIES_SHELLDER"] = 40,
        ["pret:SPECIES_HORSEA"] = 80,
        ["pret:SPECIES_GOLDEEN"] = 150,
        ["pret:SPECIES_STARYU"] = 345,
        ["pret:SPECIES_STARMIE"] = 800,
    };
    internal static bool HasWeight(SpeciesId species) => Weights.ContainsKey(species.Value);
    public static int WeightHectograms(SpeciesId species) => Weights.TryGetValue(species.Value, out var weight) ? weight : MtMoonBattleData.WeightHectograms(species);
}

using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

internal static class MtMoonBattleScope
{
    internal static bool Supports(MoveId move) => Route3BattleScope.Supports(move) || move.Value is
        "pret:MOVE_BULLET_SEED" or "pret:MOVE_THIEF" or "pret:MOVE_ROAR" or
        "pret:MOVE_ABSORB" or "pret:MOVE_AERIAL_ACE" or "pret:MOVE_ASTONISH" or "pret:MOVE_BODY_SLAM" or "pret:MOVE_CHARGE" or "pret:MOVE_DOUBLE_SLAP" or "pret:MOVE_DRAGON_RAGE" or "pret:MOVE_ENCORE" or "pret:MOVE_FOLLOW_ME" or "pret:MOVE_GROWTH" or "pret:MOVE_HELPING_HAND" or "pret:MOVE_LEECH_LIFE" or "pret:MOVE_MAGNITUDE" or "pret:MOVE_METAL_SOUND" or "pret:MOVE_MINIMIZE" or "pret:MOVE_MUD_SPORT" or "pret:MOVE_POISON_GAS" or "pret:MOVE_RAGE" or "pret:MOVE_RAPID_SPIN" or "pret:MOVE_ROCK_THROW" or "pret:MOVE_SCREECH" or "pret:MOVE_SELF_DESTRUCT" or "pret:MOVE_SMOG" or "pret:MOVE_SPORE" or "pret:MOVE_SWEET_SCENT" or "pret:MOVE_THRASH" or "pret:MOVE_WING_ATTACK";
    internal static bool SupportsSpecies(SpeciesId species) => Route3BattleScope.SupportsSpecies(species) || MtMoonBattleData.HasWeight(species);
    internal static bool SupportsWildOpponent(int species) => Route3BattleScope.SupportsWildOpponent(species) || species is 35 or 41 or 46 or 74;
    internal static bool SupportsAbility(string ability) => Route3BattleScope.SupportsAbility(ability) || ability is
        "ABILITY_INNER_FOCUS" or "ABILITY_EFFECT_SPORE" or "ABILITY_MAGNET_PULL" or "ABILITY_SOUNDPROOF" or "ABILITY_CHLOROPHYLL" or "ABILITY_STENCH" or "ABILITY_STICKY_HOLD" or "ABILITY_LEVITATE";
    internal static bool SupportsHeldItem(string item) => item is "ITEM_NONE" or "ITEM_ORAN_BERRY" or "ITEM_TINY_MUSHROOM" or "ITEM_BIG_MUSHROOM" or "ITEM_MOON_STONE";
    internal static bool ValidDefinition(PokemonMoveDefinition m)
    {
        if (Route3BattleScope.Supports(m.Id)) return Route3BattleScope.ValidDefinition(m);
        var expected = m.Id.Value switch
        {
            "pret:MOVE_ABSORB" => (71, 20, 20, 100, "TYPE_GRASS", "EFFECT_ABSORB", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_AERIAL_ACE" => (332, 20, 60, 0, "TYPE_FLYING", "EFFECT_ALWAYS_HIT", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_ASTONISH" => (310, 15, 30, 100, "TYPE_GHOST", "EFFECT_FLINCH_MINIMIZE_HIT", "MOVE_TARGET_SELECTED", 30, 0),
            "pret:MOVE_BODY_SLAM" => (34, 15, 85, 100, "TYPE_NORMAL", "EFFECT_PARALYZE_HIT", "MOVE_TARGET_SELECTED", 30, 0),
            "pret:MOVE_CHARGE" => (268, 20, 0, 100, "TYPE_ELECTRIC", "EFFECT_CHARGE", "MOVE_TARGET_USER", 0, 0),
            "pret:MOVE_DOUBLE_SLAP" => (3, 10, 15, 85, "TYPE_NORMAL", "EFFECT_MULTI_HIT", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_DRAGON_RAGE" => (82, 10, 1, 100, "TYPE_DRAGON", "EFFECT_DRAGON_RAGE", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_ENCORE" => (227, 5, 0, 100, "TYPE_NORMAL", "EFFECT_ENCORE", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_FOLLOW_ME" => (266, 20, 0, 100, "TYPE_NORMAL", "EFFECT_FOLLOW_ME", "MOVE_TARGET_USER", 0, 3),
            "pret:MOVE_GROWTH" => (74, 40, 0, 0, "TYPE_NORMAL", "EFFECT_SPECIAL_ATTACK_UP", "MOVE_TARGET_USER", 0, 0),
            "pret:MOVE_HELPING_HAND" => (270, 20, 0, 100, "TYPE_NORMAL", "EFFECT_HELPING_HAND", "MOVE_TARGET_USER", 0, 5),
            "pret:MOVE_LEECH_LIFE" => (141, 15, 20, 100, "TYPE_BUG", "EFFECT_ABSORB", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_MAGNITUDE" => (222, 30, 1, 100, "TYPE_GROUND", "EFFECT_MAGNITUDE", "MOVE_TARGET_FOES_AND_ALLY", 0, 0),
            "pret:MOVE_METAL_SOUND" => (319, 40, 0, 85, "TYPE_STEEL", "EFFECT_SPECIAL_DEFENSE_DOWN_2", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_MINIMIZE" => (107, 20, 0, 0, "TYPE_NORMAL", "EFFECT_MINIMIZE", "MOVE_TARGET_USER", 0, 0),
            "pret:MOVE_MUD_SPORT" => (300, 15, 0, 100, "TYPE_GROUND", "EFFECT_MUD_SPORT", "MOVE_TARGET_USER", 0, 0),
            "pret:MOVE_POISON_GAS" => (139, 40, 0, 55, "TYPE_POISON", "EFFECT_POISON", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_RAGE" => (99, 20, 20, 100, "TYPE_NORMAL", "EFFECT_RAGE", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_RAPID_SPIN" => (229, 40, 20, 100, "TYPE_NORMAL", "EFFECT_RAPID_SPIN", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_ROCK_THROW" => (88, 15, 50, 90, "TYPE_ROCK", "EFFECT_HIT", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_SCREECH" => (103, 40, 0, 85, "TYPE_NORMAL", "EFFECT_DEFENSE_DOWN_2", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_SELF_DESTRUCT" => (120, 5, 200, 100, "TYPE_NORMAL", "EFFECT_EXPLOSION", "MOVE_TARGET_FOES_AND_ALLY", 0, 0),
            "pret:MOVE_SMOG" => (123, 20, 20, 70, "TYPE_POISON", "EFFECT_POISON_HIT", "MOVE_TARGET_SELECTED", 40, 0),
            "pret:MOVE_SPORE" => (147, 15, 0, 100, "TYPE_GRASS", "EFFECT_SLEEP", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_SWEET_SCENT" => (230, 20, 0, 100, "TYPE_NORMAL", "EFFECT_EVASION_DOWN", "MOVE_TARGET_BOTH", 0, 0),
            "pret:MOVE_THRASH" => (37, 20, 90, 100, "TYPE_NORMAL", "EFFECT_RAMPAGE", "MOVE_TARGET_RANDOM", 100, 0),
            "pret:MOVE_WING_ATTACK" => (17, 35, 60, 100, "TYPE_FLYING", "EFFECT_HIT", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_BULLET_SEED" => (331, 30, 10, 100, "TYPE_GRASS", "EFFECT_MULTI_HIT", "MOVE_TARGET_SELECTED", 0, 0),
            "pret:MOVE_THIEF" => (168, 10, 40, 100, "TYPE_DARK", "EFFECT_THIEF", "MOVE_TARGET_SELECTED", 100, 0),
            "pret:MOVE_ROAR" => (46, 20, 0, 100, "TYPE_NORMAL", "EFFECT_ROAR", "MOVE_TARGET_SELECTED", 0, -6),
            _ => default
        };
        string[] flags = m.Id.Value switch
        {
            "pret:MOVE_ABSORB" => ["FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_AERIAL_ACE" => ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_ASTONISH" => ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_BODY_SLAM" => ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_CHARGE" => ["FLAG_SNATCH_AFFECTED"],
            "pret:MOVE_DOUBLE_SLAP" => ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_DRAGON_RAGE" => ["FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_ENCORE" => ["FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_FOLLOW_ME" => [],
            "pret:MOVE_GROWTH" => ["FLAG_SNATCH_AFFECTED"],
            "pret:MOVE_HELPING_HAND" => [],
            "pret:MOVE_LEECH_LIFE" => ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_MAGNITUDE" => ["FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_METAL_SOUND" => ["FLAG_PROTECT_AFFECTED", "FLAG_MAGIC_COAT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_MINIMIZE" => ["FLAG_SNATCH_AFFECTED"],
            "pret:MOVE_MUD_SPORT" => [],
            "pret:MOVE_POISON_GAS" => ["FLAG_PROTECT_AFFECTED", "FLAG_MAGIC_COAT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_RAGE" => ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_RAPID_SPIN" => ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_ROCK_THROW" => ["FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_SCREECH" => ["FLAG_PROTECT_AFFECTED", "FLAG_MAGIC_COAT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_SELF_DESTRUCT" => ["FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_SMOG" => ["FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_SPORE" => ["FLAG_PROTECT_AFFECTED", "FLAG_MAGIC_COAT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_SWEET_SCENT" => ["FLAG_PROTECT_AFFECTED", "FLAG_MAGIC_COAT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_THRASH" => ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_WING_ATTACK" => ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_BULLET_SEED" => ["FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_THIEF" => ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_ROAR" => ["FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            _ => []
        };
        return expected.Item1 != 0 &&
            (m.SourceMoveId, m.Pp, m.Power, m.Accuracy, m.Type, m.Effect, m.Target, m.SecondaryEffectChance, m.Priority) == expected &&
            !m.Flags.IsDefault && m.Flags.Length == flags.Length && flags.All(m.Flags.Contains);
    }
}

public static class MtMoonBattleData
{
    private static readonly IReadOnlyDictionary<string, int> Weights = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["pret:SPECIES_ARBOK"] = 650,
        ["pret:SPECIES_CLEFAIRY"] = 75,
        ["pret:SPECIES_ZUBAT"] = 75,
        ["pret:SPECIES_GOLBAT"] = 550,
        ["pret:SPECIES_ODDISH"] = 54,
        ["pret:SPECIES_PARAS"] = 54,
        ["pret:SPECIES_PARASECT"] = 295,
        ["pret:SPECIES_BELLSPROUT"] = 40,
        ["pret:SPECIES_MAGNEMITE"] = 60,
        ["pret:SPECIES_GRIMER"] = 300,
        ["pret:SPECIES_VOLTORB"] = 104,
        ["pret:SPECIES_KOFFING"] = 10,
        ["pret:SPECIES_NIDOQUEEN"] = 600,
        ["pret:SPECIES_NIDOKING"] = 620,
        ["pret:SPECIES_CLEFABLE"] = 400,
        ["pret:SPECIES_WIGGLYTUFF"] = 120,
    };
    internal static bool HasWeight(SpeciesId species) => Weights.ContainsKey(species.Value);
    public static int WeightHectograms(SpeciesId species) => Weights.TryGetValue(species.Value, out var weight) ? weight : Route3BattleData.WeightHectograms(species);
    public static (int Magnitude, int Power) Magnitude(int roll) => roll switch
    {
        < 0 or >= 100 => throw new ArgumentOutOfRangeException(nameof(roll)),
        < 5 => (4, 10), < 15 => (5, 30), < 35 => (6, 50), < 65 => (7, 70),
        < 85 => (8, 90), < 95 => (9, 110), _ => (10, 150)
    };
    internal static string WildHeldItem(int species, int roll) => species switch
    {
        39 when roll is >= 45 and < 95 => "ITEM_ORAN_BERRY",
        46 when roll >= 95 => "ITEM_BIG_MUSHROOM",
        46 when roll >= 45 => "ITEM_TINY_MUSHROOM",
        35 when roll >= 95 => "ITEM_MOON_STONE",
        _ => "ITEM_NONE"
    };
}

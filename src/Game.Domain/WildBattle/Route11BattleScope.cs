using Game.Domain.Ids;
using Game.Domain.Pokemon;
namespace Game.Domain.WildBattle;

/// <summary>Route 11 / Diglett's Cave extension. Full learnsets do not implicitly enable later effects.</summary>
internal static class Route11BattleScope
{
    internal static bool Supports(MoveId move) => VermilionGymBattleScope.Supports(move) || move.Value is
        "pret:MOVE_IMPRISON" or "pret:MOVE_MUD_SLAP" or "pret:MOVE_TRI_ATTACK" or "pret:MOVE_WILL_O_WISP" or "pret:MOVE_FLASH" or "pret:MOVE_RAIN_DANCE" or "pret:MOVE_SAND_TOMB" or "pret:MOVE_CONFUSE_RAY" or "pret:MOVE_FLAMETHROWER";
    internal static bool SupportsSpecies(SpeciesId species) => VermilionGymBattleScope.SupportsSpecies(species) || species.Value is
        "pret:SPECIES_DIGLETT" or "pret:SPECIES_DUGTRIO" or "pret:SPECIES_MAGNETON" or "pret:SPECIES_POLIWAG" or "pret:SPECIES_POLIWHIRL" or "pret:SPECIES_VULPIX";
    internal static bool SupportsWildOpponent(int species) => VermilionBattleScope.SupportsWildOpponent(species) || species is 50 or 51 or 96;
    internal static bool SupportsHeldItem(string item) => SsAnneBattleScope.SupportsHeldItem(item) || item == "ITEM_X_DEFEND";
    internal static bool SupportsAbility(string ability) => SsAnneBattleScope.SupportsAbility(ability) || ability is "ABILITY_ARENA_TRAP" or "ABILITY_WATER_ABSORB" or "ABILITY_DAMP";
    internal static bool ValidSpecies(PokemonSpeciesDefinition s) => VermilionGymBattleScope.ValidSpecies(s) || s.SourceSpeciesId switch
    {
        50 => s.Id.Value == "pret:SPECIES_DIGLETT" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_GROUND","TYPE_GROUND","ABILITY_SAND_VEIL","ABILITY_ARENA_TRAP"),
        51 => s.Id.Value == "pret:SPECIES_DUGTRIO" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_GROUND","TYPE_GROUND","ABILITY_SAND_VEIL","ABILITY_ARENA_TRAP"),
        82 => s.Id.Value == "pret:SPECIES_MAGNETON" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_ELECTRIC","TYPE_STEEL","ABILITY_MAGNET_PULL","ABILITY_STURDY"),
        60 => s.Id.Value == "pret:SPECIES_POLIWAG" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_WATER","TYPE_WATER","ABILITY_WATER_ABSORB","ABILITY_DAMP"),
        61 => s.Id.Value == "pret:SPECIES_POLIWHIRL" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_WATER","TYPE_WATER","ABILITY_WATER_ABSORB","ABILITY_DAMP"),
        37 => s.Id.Value == "pret:SPECIES_VULPIX" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_FIRE","TYPE_FIRE","ABILITY_FLASH_FIRE","ABILITY_NONE"),
        _ => false
    };
    internal static bool ValidDefinition(PokemonMoveDefinition m)
    {
        if (VermilionGymBattleScope.Supports(m.Id)) return VermilionGymBattleScope.ValidDefinition(m);
        var expected = m.Id.Value switch
        {
            "pret:MOVE_IMPRISON" => (286,10,0,100,"TYPE_PSYCHIC","EFFECT_IMPRISON","MOVE_TARGET_USER",0,0),
            "pret:MOVE_MUD_SLAP" => (189,10,20,100,"TYPE_GROUND","EFFECT_ACCURACY_DOWN_HIT","MOVE_TARGET_SELECTED",100,0),
            "pret:MOVE_TRI_ATTACK" => (161,10,80,100,"TYPE_NORMAL","EFFECT_TRI_ATTACK","MOVE_TARGET_SELECTED",20,0),
            "pret:MOVE_WILL_O_WISP" => (261,15,0,75,"TYPE_FIRE","EFFECT_WILL_O_WISP","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_FLASH" => (148,20,0,70,"TYPE_NORMAL","EFFECT_ACCURACY_DOWN","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_FLAMETHROWER" => (53,15,95,100,"TYPE_FIRE","EFFECT_BURN_HIT","MOVE_TARGET_SELECTED",10,0),
            "pret:MOVE_CONFUSE_RAY" => (109,10,0,100,"TYPE_GHOST","EFFECT_CONFUSE","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_SAND_TOMB" => (328,15,15,70,"TYPE_GROUND","EFFECT_TRAP","MOVE_TARGET_SELECTED",100,0),
            "pret:MOVE_RAIN_DANCE" => (240,5,0,0,"TYPE_WATER","EFFECT_RAIN_DANCE","MOVE_TARGET_USER",0,0),
            _ => default
        };
        string[] flags = m.Id.Value switch
        {
            "pret:MOVE_IMPRISON" => ["FLAG_PROTECT_AFFECTED"],
            "pret:MOVE_MUD_SLAP" or "pret:MOVE_TRI_ATTACK" or "pret:MOVE_FLAMETHROWER" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_SAND_TOMB" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED","FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_WILL_O_WISP" or "pret:MOVE_FLASH" or "pret:MOVE_CONFUSE_RAY" => ["FLAG_PROTECT_AFFECTED","FLAG_MAGIC_COAT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            _ => []
        };
        return expected.Item1 != 0 && (m.SourceMoveId,m.Pp,m.Power,m.Accuracy,m.Type,m.Effect,m.Target,m.SecondaryEffectChance,m.Priority) == expected &&
            !m.Flags.IsDefault && m.Flags.Length == flags.Length && flags.All(m.Flags.Contains);
    }
}
public static class Route11BattleData
{
    public static int WeightHectograms(SpeciesId species) => species.Value switch
    {
        "pret:SPECIES_DIGLETT" => 8, "pret:SPECIES_DUGTRIO" => 333, "pret:SPECIES_MAGNETON" => 600,
        "pret:SPECIES_POLIWAG" => 124, "pret:SPECIES_POLIWHIRL" => 200, "pret:SPECIES_VULPIX" => 99,
        _ => VermilionGymBattleData.WeightHectograms(species)
    };
}

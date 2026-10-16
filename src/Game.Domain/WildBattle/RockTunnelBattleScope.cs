using Game.Domain.Ids;
using Game.Domain.Pokemon;
namespace Game.Domain.WildBattle;

internal static class RockTunnelBattleScope
{
    internal static bool Supports(MoveId move) => Route11BattleScope.Supports(move) || move.Value is
        "pret:MOVE_AGILITY" or "pret:MOVE_AIR_CUTTER" or "pret:MOVE_BONE_CLUB" or "pret:MOVE_COSMIC_POWER" or "pret:MOVE_CROSS_CHOP" or "pret:MOVE_DRAGON_BREATH" or "pret:MOVE_FEATHER_DANCE" or "pret:MOVE_FLAIL" or "pret:MOVE_FORESIGHT" or "pret:MOVE_KNOCK_OFF" or "pret:MOVE_MEDITATE" or "pret:MOVE_MOONLIGHT" or "pret:MOVE_ODOR_SLEUTH" or "pret:MOVE_PIN_MISSILE" or "pret:MOVE_PSYBEAM" or "pret:MOVE_PSYCHIC" or "pret:MOVE_ROCK_BLAST" or "pret:MOVE_SLASH" or "pret:MOVE_STOMP" or "pret:MOVE_SUPER_FANG" or "pret:MOVE_THUNDERBOLT" or "pret:MOVE_TWISTER" or "pret:MOVE_VITAL_THROW" or "pret:MOVE_REFLECT" or "pret:MOVE_REVENGE" or "pret:MOVE_SANDSTORM" or "pret:MOVE_PROTECT" or "pret:MOVE_DETECT" or "pret:MOVE_ENDURE";
    internal static bool SupportsSpecies(SpeciesId species) => PokemonFactory.RockTunnelSupportedSpecies.Contains(species);
    internal static bool SupportsWildOpponent(int species) => Route11BattleScope.SupportsWildOpponent(species) || species is 66 or 95 or 100;
    internal static bool SupportsHeldItem(string item) => Route11BattleScope.SupportsHeldItem(item) || item == "ITEM_HARD_STONE";
    internal static bool SupportsAbility(string ability) => Route11BattleScope.SupportsAbility(ability) || ability == "ABILITY_LIGHTNING_ROD";
    internal static bool ValidSpecies(PokemonSpeciesDefinition s) => Route11BattleScope.ValidSpecies(s) || s.SourceSpeciesId switch
    {
        104 => s.Id.Value == "pret:SPECIES_CUBONE" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_GROUND","TYPE_GROUND","ABILITY_ROCK_HEAD","ABILITY_LIGHTNING_ROD"),
        101 => s.Id.Value == "pret:SPECIES_ELECTRODE" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_ELECTRIC","TYPE_ELECTRIC","ABILITY_SOUNDPROOF","ABILITY_STATIC"),
        97 => s.Id.Value == "pret:SPECIES_HYPNO" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_PSYCHIC","TYPE_PSYCHIC","ABILITY_INSOMNIA","ABILITY_NONE"),
        108 => s.Id.Value == "pret:SPECIES_LICKITUNG" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_NORMAL","TYPE_NORMAL","ABILITY_OWN_TEMPO","ABILITY_OBLIVIOUS"),
        67 => s.Id.Value == "pret:SPECIES_MACHOKE" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_FIGHTING","TYPE_FIGHTING","ABILITY_GUTS","ABILITY_NONE"),
        105 => s.Id.Value == "pret:SPECIES_MAROWAK" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_GROUND","TYPE_GROUND","ABILITY_ROCK_HEAD","ABILITY_LIGHTNING_ROD"),
        57 => s.Id.Value == "pret:SPECIES_PRIMEAPE" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_FIGHTING","TYPE_FIGHTING","ABILITY_VITAL_SPIRIT","ABILITY_NONE"),
        111 => s.Id.Value == "pret:SPECIES_RHYHORN" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_GROUND","TYPE_ROCK","ABILITY_LIGHTNING_ROD","ABILITY_ROCK_HEAD"),
        117 => s.Id.Value == "pret:SPECIES_SEADRA" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_WATER","TYPE_WATER","ABILITY_POISON_POINT","ABILITY_NONE"),
        119 => s.Id.Value == "pret:SPECIES_SEAKING" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_WATER","TYPE_WATER","ABILITY_SWIFT_SWIM","ABILITY_WATER_VEIL"),
        73 => s.Id.Value == "pret:SPECIES_TENTACRUEL" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_WATER","TYPE_POISON","ABILITY_CLEAR_BODY","ABILITY_LIQUID_OOZE"),
        49 => s.Id.Value == "pret:SPECIES_VENOMOTH" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_BUG","TYPE_POISON","ABILITY_SHIELD_DUST","ABILITY_NONE"),
        48 => s.Id.Value == "pret:SPECIES_VENONAT" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_BUG","TYPE_POISON","ABILITY_COMPOUND_EYES","ABILITY_NONE"),
        110 => s.Id.Value == "pret:SPECIES_WEEZING" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_POISON","TYPE_POISON","ABILITY_LEVITATE","ABILITY_NONE"),
        _ => false
    };
    internal static bool ValidDefinition(PokemonMoveDefinition m)
    {
        if (Route11BattleScope.Supports(m.Id)) return Route11BattleScope.ValidDefinition(m);
        var expected = m.Id.Value switch
        {
            "pret:MOVE_AGILITY" => (97,30,0,0,"TYPE_PSYCHIC","EFFECT_SPEED_UP_2","MOVE_TARGET_USER",0,0),
            "pret:MOVE_AIR_CUTTER" => (314,25,55,95,"TYPE_FLYING","EFFECT_HIGH_CRITICAL","MOVE_TARGET_BOTH",0,0),
            "pret:MOVE_BONE_CLUB" => (125,20,65,85,"TYPE_GROUND","EFFECT_FLINCH_HIT","MOVE_TARGET_SELECTED",10,0),
            "pret:MOVE_COSMIC_POWER" => (322,20,0,0,"TYPE_PSYCHIC","EFFECT_COSMIC_POWER","MOVE_TARGET_USER",0,0),
            "pret:MOVE_CROSS_CHOP" => (238,5,100,80,"TYPE_FIGHTING","EFFECT_HIGH_CRITICAL","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_DRAGON_BREATH" => (225,20,60,100,"TYPE_DRAGON","EFFECT_PARALYZE_HIT","MOVE_TARGET_SELECTED",30,0),
            "pret:MOVE_FEATHER_DANCE" => (297,15,0,100,"TYPE_FLYING","EFFECT_ATTACK_DOWN_2","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_FLAIL" => (175,15,1,100,"TYPE_NORMAL","EFFECT_FLAIL","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_FORESIGHT" => (193,40,0,100,"TYPE_NORMAL","EFFECT_FORESIGHT","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_KNOCK_OFF" => (282,20,20,100,"TYPE_DARK","EFFECT_KNOCK_OFF","MOVE_TARGET_SELECTED",100,0),
            "pret:MOVE_MEDITATE" => (96,40,0,0,"TYPE_PSYCHIC","EFFECT_ATTACK_UP","MOVE_TARGET_USER",0,0),
            "pret:MOVE_MOONLIGHT" => (236,5,0,0,"TYPE_NORMAL","EFFECT_MOONLIGHT","MOVE_TARGET_USER",0,0),
            "pret:MOVE_ODOR_SLEUTH" => (316,40,0,100,"TYPE_NORMAL","EFFECT_FORESIGHT","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_PIN_MISSILE" => (42,20,14,85,"TYPE_BUG","EFFECT_MULTI_HIT","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_PSYBEAM" => (60,20,65,100,"TYPE_PSYCHIC","EFFECT_CONFUSE_HIT","MOVE_TARGET_SELECTED",10,0),
            "pret:MOVE_PSYCHIC" => (94,10,90,100,"TYPE_PSYCHIC","EFFECT_SPECIAL_DEFENSE_DOWN_HIT","MOVE_TARGET_SELECTED",10,0),
            "pret:MOVE_ROCK_BLAST" => (350,10,25,80,"TYPE_ROCK","EFFECT_MULTI_HIT","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_SLASH" => (163,20,70,100,"TYPE_NORMAL","EFFECT_HIGH_CRITICAL","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_STOMP" => (23,20,65,100,"TYPE_NORMAL","EFFECT_FLINCH_MINIMIZE_HIT","MOVE_TARGET_SELECTED",30,0),
            "pret:MOVE_SUPER_FANG" => (162,10,1,90,"TYPE_NORMAL","EFFECT_SUPER_FANG","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_THUNDERBOLT" => (85,15,95,100,"TYPE_ELECTRIC","EFFECT_PARALYZE_HIT","MOVE_TARGET_SELECTED",10,0),
            "pret:MOVE_TWISTER" => (239,20,40,100,"TYPE_DRAGON","EFFECT_TWISTER","MOVE_TARGET_BOTH",20,0),
            "pret:MOVE_VITAL_THROW" => (233,10,70,100,"TYPE_FIGHTING","EFFECT_VITAL_THROW","MOVE_TARGET_SELECTED",0,-1),
            "pret:MOVE_REFLECT" => (115,20,0,0,"TYPE_PSYCHIC","EFFECT_REFLECT","MOVE_TARGET_USER",0,0),
            "pret:MOVE_REVENGE" => (279,10,60,100,"TYPE_FIGHTING","EFFECT_REVENGE","MOVE_TARGET_SELECTED",0,-4),
            "pret:MOVE_SANDSTORM" => (201,10,0,0,"TYPE_ROCK","EFFECT_SANDSTORM","MOVE_TARGET_USER",0,0),
            "pret:MOVE_PROTECT" => (182,10,0,0,"TYPE_NORMAL","EFFECT_PROTECT","MOVE_TARGET_USER",0,3),
            "pret:MOVE_DETECT" => (197,5,0,0,"TYPE_FIGHTING","EFFECT_PROTECT","MOVE_TARGET_USER",0,3),
            "pret:MOVE_ENDURE" => (203,10,0,0,"TYPE_NORMAL","EFFECT_ENDURE","MOVE_TARGET_USER",0,3),
            _ => default
        };
        string[] flags = m.Id.Value switch
        {
            "pret:MOVE_AGILITY" => ["FLAG_SNATCH_AFFECTED"],
            "pret:MOVE_AIR_CUTTER" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED","FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_BONE_CLUB" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_COSMIC_POWER" => ["FLAG_SNATCH_AFFECTED"],
            "pret:MOVE_CROSS_CHOP" => ["FLAG_MAKES_CONTACT","FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED","FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_DRAGON_BREATH" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED","FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_FEATHER_DANCE" => ["FLAG_PROTECT_AFFECTED","FLAG_MAGIC_COAT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_FLAIL" => ["FLAG_MAKES_CONTACT","FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED","FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_FORESIGHT" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_KNOCK_OFF" => ["FLAG_MAKES_CONTACT","FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_MEDITATE" => ["FLAG_SNATCH_AFFECTED"],
            "pret:MOVE_MOONLIGHT" => ["FLAG_SNATCH_AFFECTED"],
            "pret:MOVE_ODOR_SLEUTH" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_PIN_MISSILE" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED","FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_PSYBEAM" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_PSYCHIC" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_ROCK_BLAST" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED","FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_SLASH" => ["FLAG_MAKES_CONTACT","FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED","FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_STOMP" => ["FLAG_MAKES_CONTACT","FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_SUPER_FANG" => ["FLAG_MAKES_CONTACT","FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_THUNDERBOLT" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_TWISTER" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED","FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_VITAL_THROW" => ["FLAG_MAKES_CONTACT","FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED","FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_REFLECT" => ["FLAG_SNATCH_AFFECTED"],
            "pret:MOVE_REVENGE" => ["FLAG_MAKES_CONTACT","FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED","FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_SANDSTORM" => [],
            "pret:MOVE_PROTECT" => [],
            "pret:MOVE_DETECT" => [],
            "pret:MOVE_ENDURE" => [],
            _ => []
        };
        return expected.Item1 != 0 && (m.SourceMoveId,m.Pp,m.Power,m.Accuracy,m.Type,m.Effect,m.Target,m.SecondaryEffectChance,m.Priority) == expected &&
            !m.Flags.IsDefault && m.Flags.Length == flags.Length && flags.All(m.Flags.Contains);
    }
}
public static class RockTunnelBattleData
{
    public static int WeightHectograms(SpeciesId species) => species.Value switch
    {
        "pret:SPECIES_KANGASKHAN" => 800,
        "pret:SPECIES_VILEPLUME" => 186,
        "pret:SPECIES_VICTREEBEL" => 155,
        "pret:SPECIES_EXEGGCUTE" => 25,
        "pret:SPECIES_TANGELA" => 350,
        "pret:SPECIES_CUBONE" => 65,
        "pret:SPECIES_ELECTRODE" => 666,
        "pret:SPECIES_HYPNO" => 756,
        "pret:SPECIES_LICKITUNG" => 655,
        "pret:SPECIES_MACHOKE" => 705,
        "pret:SPECIES_MAROWAK" => 450,
        "pret:SPECIES_PRIMEAPE" => 320,
        "pret:SPECIES_RHYHORN" => 1150,
        "pret:SPECIES_SEADRA" => 250,
        "pret:SPECIES_SEAKING" => 390,
        "pret:SPECIES_TENTACRUEL" => 550,
        "pret:SPECIES_VENOMOTH" => 125,
        "pret:SPECIES_VENONAT" => 300,
        "pret:SPECIES_MUK" => 300,
        "pret:SPECIES_WEEZING" => 95,
        _ => Route11BattleData.WeightHectograms(species)
    };
}

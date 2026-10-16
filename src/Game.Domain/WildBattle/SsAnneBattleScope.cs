using Game.Domain.Ids;
using Game.Domain.Pokemon;
namespace Game.Domain.WildBattle;

/// <summary>Exact S.S. Anne singles closure. Complete learnset metadata never enables an unsupported effect.</summary>
internal static class SsAnneBattleScope
{
    internal static bool Supports(MoveId move) => VermilionBattleScope.Supports(move) || move.Value is
        "pret:MOVE_CONSTRICT" or "pret:MOVE_KINESIS" or "pret:MOVE_SEISMIC_TOSS" or "pret:MOVE_CUT" or "pret:MOVE_BRICK_BREAK" or "pret:MOVE_REST";
    internal static bool SupportsSpecies(SpeciesId species) => VermilionBattleScope.SupportsSpecies(species) || species.Value is "pret:SPECIES_GROWLITHE" or "pret:SPECIES_PONYTA" or "pret:SPECIES_TENTACOOL";
    internal static bool SupportsHeldItem(string item) => VermilionBattleScope.SupportsHeldItem(item) || item is "ITEM_STARDUST" or "ITEM_X_ATTACK" or "ITEM_ETHER" or "ITEM_SUPER_POTION" or "ITEM_GREAT_BALL" or "ITEM_HYPER_POTION" or "ITEM_LAVA_COOKIE";
    internal static bool SupportsAbility(string ability) => VermilionBattleScope.SupportsAbility(ability) || ability is "ABILITY_FLASH_FIRE" or "ABILITY_CLEAR_BODY" or "ABILITY_LIQUID_OOZE";
    internal static bool ValidSpecies(PokemonSpeciesDefinition s) => VermilionBattleScope.ValidSpecies(s) || s.SourceSpeciesId switch
    {
        58 => s.Id.Value == "pret:SPECIES_GROWLITHE" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_FIRE","TYPE_FIRE","ABILITY_INTIMIDATE","ABILITY_FLASH_FIRE"),
        77 => s.Id.Value == "pret:SPECIES_PONYTA" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_FIRE","TYPE_FIRE","ABILITY_RUN_AWAY","ABILITY_FLASH_FIRE"),
        72 => s.Id.Value == "pret:SPECIES_TENTACOOL" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_WATER","TYPE_POISON","ABILITY_CLEAR_BODY","ABILITY_LIQUID_OOZE"),
        _ => false
    };
    internal static bool ValidDefinition(PokemonMoveDefinition m)
    {
        if (VermilionBattleScope.Supports(m.Id)) return VermilionBattleScope.ValidDefinition(m);
        var expected = m.Id.Value switch
        {
            "pret:MOVE_CONSTRICT" => (132,35,10,100,"TYPE_NORMAL","EFFECT_SPEED_DOWN_HIT","MOVE_TARGET_SELECTED",10,0),
            "pret:MOVE_KINESIS" => (134,15,0,80,"TYPE_PSYCHIC","EFFECT_ACCURACY_DOWN","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_SEISMIC_TOSS" => (69,20,1,100,"TYPE_FIGHTING","EFFECT_LEVEL_DAMAGE","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_CUT" => (15,30,50,95,"TYPE_NORMAL","EFFECT_HIT","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_BRICK_BREAK" => (280,15,75,100,"TYPE_FIGHTING","EFFECT_BRICK_BREAK","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_REST" => (156,10,0,0,"TYPE_PSYCHIC","EFFECT_REST","MOVE_TARGET_USER",0,0),
            _ => default
        };
        string[] flags = m.Id.Value switch
        {
            "pret:MOVE_CONSTRICT" => ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_KINESIS" => ["FLAG_PROTECT_AFFECTED", "FLAG_MAGIC_COAT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_SEISMIC_TOSS" => ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_CUT" => ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_BRICK_BREAK" => ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_REST" => ["FLAG_SNATCH_AFFECTED"],
            _ => []
        };
        return expected.Item1 != 0 && (m.SourceMoveId,m.Pp,m.Power,m.Accuracy,m.Type,m.Effect,m.Target,m.SecondaryEffectChance,m.Priority) == expected &&
            !m.Flags.IsDefault && m.Flags.Length == flags.Length && flags.All(m.Flags.Contains);
    }
}
public static class SsAnneBattleData
{
    public static int WeightHectograms(SpeciesId species) => species.Value switch
    { "pret:SPECIES_GROWLITHE" => 190, "pret:SPECIES_PONYTA" => 300, "pret:SPECIES_TENTACOOL" => 455, _ => VermilionBattleData.WeightHectograms(species) };
}

using Game.Domain.Pokemon;
using Game.Domain.Ids;
namespace Game.Domain.WildBattle;
/// <summary>Only the two additional source effects required by Celadon Gym. Full learnset metadata never implies executable admission.</summary>
internal static class CeladonGymBattleScope
{
    internal static bool Supports(MoveId move) => CeladonApproachBattleScope.Supports(move) || move.Value is "pret:MOVE_GIGA_DRAIN" or "pret:MOVE_INGRAIN";
    internal static bool ValidDefinition(PokemonMoveDefinition move)
    {
        if (CeladonApproachBattleScope.ValidDefinition(move)) return true;
        var expected = move.Id.Value switch
        {
            "pret:MOVE_GIGA_DRAIN" => (202,5,60,100,"TYPE_GRASS","EFFECT_ABSORB","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_INGRAIN" => (275,20,0,100,"TYPE_GRASS","EFFECT_INGRAIN","MOVE_TARGET_USER",0,0),
            _ => default
        };
        string[] flags = move.Id.Value == "pret:MOVE_GIGA_DRAIN" ? ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"] : ["FLAG_SNATCH_AFFECTED"];
        return expected != default && (move.SourceMoveId,move.Pp,move.Power,move.Accuracy,move.Type,move.Effect,move.Target,move.SecondaryEffectChance,move.Priority) == expected &&
            !move.Flags.IsDefault && move.Flags.Length == flags.Length && flags.All(move.Flags.Contains);
    }
    internal static bool SupportsSharedSpecies(SpeciesId id) => PokemonFactory.CeladonGymSupportedSpecies.Contains(id);
    internal static bool ValidSharedSpecies(PokemonSpeciesDefinition s) => CeladonApproachBattleScope.ValidSpecies(s) || s.SourceSpeciesId switch
    {
        45 => s.Id.Value == "pret:SPECIES_VILEPLUME" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_GRASS","TYPE_POISON","ABILITY_CHLOROPHYLL","ABILITY_NONE"),
        71 => s.Id.Value == "pret:SPECIES_VICTREEBEL" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_GRASS","TYPE_POISON","ABILITY_CHLOROPHYLL","ABILITY_NONE"),
        102 => s.Id.Value == "pret:SPECIES_EXEGGCUTE" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_GRASS","TYPE_PSYCHIC","ABILITY_CHLOROPHYLL","ABILITY_NONE"),
        114 => s.Id.Value == "pret:SPECIES_TANGELA" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_GRASS","TYPE_GRASS","ABILITY_CHLOROPHYLL","ABILITY_NONE"),
        _ => false
    };
}

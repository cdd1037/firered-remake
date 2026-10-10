using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
namespace Game.Domain.WildBattle;

/// <summary>Bill initial parties plus natural player evolutions through level 25. Later metadata is rejected.</summary>
internal static class BillBattleScope
{
    internal static bool Supports(MoveId move) => CeruleanBattleScope.Supports(move) || move.Value is "pret:MOVE_ACID" or "pret:MOVE_ATTRACT" or "pret:MOVE_SECRET_POWER" or "pret:MOVE_DIG";
    internal static bool SupportsSpecies(SpeciesId species) => CeruleanBattleScope.SupportsSpecies(species) || species.Value is "pret:SPECIES_SLOWPOKE" or "pret:SPECIES_GLOOM" or "pret:SPECIES_WEEPINBELL";
    internal static bool SupportsAbility(string ability) => CeruleanBattleScope.SupportsAbility(ability) || ability is "ABILITY_OBLIVIOUS" or "ABILITY_OWN_TEMPO";
    internal static bool SupportsWildOpponent(int id) => MtMoonBattleScope.SupportsWildOpponent(id) || id is 43 or 63;
    internal static bool ValidSpecies(PokemonSpeciesDefinition s) => CeruleanTrainerAi.ValidSpecies(s) || s.SourceSpeciesId switch {
        44 or 70 => (s.Type1,s.Type2,s.Ability1,s.Ability2)==("TYPE_GRASS","TYPE_POISON","ABILITY_CHLOROPHYLL","ABILITY_NONE"),
        79 => (s.Type1,s.Type2,s.Ability1,s.Ability2)==("TYPE_WATER","TYPE_PSYCHIC","ABILITY_OBLIVIOUS","ABILITY_OWN_TEMPO"), _=>false };
    internal static bool ValidDefinition(PokemonMoveDefinition m)
    {
        if(CeruleanBattleScope.Supports(m.Id))return CeruleanBattleScope.ValidDefinition(m);
        var expected=m.Id.Value switch {
            "pret:MOVE_ACID"=>(51,30,40,100,"TYPE_POISON","EFFECT_DEFENSE_DOWN_HIT","MOVE_TARGET_BOTH",10,0),
            "pret:MOVE_ATTRACT"=>(213,15,0,100,"TYPE_NORMAL","EFFECT_ATTRACT","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_SECRET_POWER"=>(290,20,70,100,"TYPE_NORMAL","EFFECT_SECRET_POWER","MOVE_TARGET_SELECTED",30,0),
            "pret:MOVE_DIG"=>(91,10,60,100,"TYPE_GROUND","EFFECT_SEMI_INVULNERABLE","MOVE_TARGET_SELECTED",0,0), _=>default };
        string[] flags=m.Id.Value switch {
            "pret:MOVE_ACID" or "pret:MOVE_SECRET_POWER"=>["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_ATTRACT"=>["FLAG_PROTECT_AFFECTED","FLAG_MAGIC_COAT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_DIG"=>["FLAG_MAKES_CONTACT","FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED","FLAG_KINGS_ROCK_AFFECTED"],_=>[] };
        return expected.Item1!=0 && (m.SourceMoveId,m.Pp,m.Power,m.Accuracy,m.Type,m.Effect,m.Target,m.SecondaryEffectChance,m.Priority)==expected &&
            !m.Flags.IsDefault && m.Flags.Length==flags.Length && flags.All(m.Flags.Contains);
    }
}
public static class BillBattleData
{
    public static int WeightHectograms(SpeciesId species) => species.Value switch {
        "pret:SPECIES_GLOOM"=>86,"pret:SPECIES_WEEPINBELL"=>64,"pret:SPECIES_SLOWPOKE"=>360,_=>CeruleanBattleData.WeightHectograms(species) };
}

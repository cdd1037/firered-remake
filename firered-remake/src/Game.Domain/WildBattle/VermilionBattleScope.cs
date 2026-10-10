using Game.Domain.Ids;
using Game.Domain.Pokemon;
namespace Game.Domain.WildBattle;

/// <summary>Explicit Route5/6 extension. Admission follows supported species, abilities and moves, without a new level cap. Meowth and Persian retain source abilities.</summary>
internal static class VermilionBattleScope
{
    internal static bool Supports(MoveId move) => BillBattleScope.Supports(move) || move.Value is "pret:MOVE_PAY_DAY" or "pret:MOVE_FAINT_ATTACK";
    internal static bool SupportsSpecies(SpeciesId species) => BillBattleScope.SupportsSpecies(species) || species.Value is "pret:SPECIES_MEOWTH" or "pret:SPECIES_PERSIAN";
    internal static bool SupportsAbility(string ability) => BillBattleScope.SupportsAbility(ability) || ability is "ABILITY_PICKUP" or "ABILITY_LIMBER";
    internal static bool SupportsWildOpponent(int id) => BillBattleScope.SupportsWildOpponent(id) || id == 52;
    internal static bool SupportsHeldItem(string item) => MtMoonBattleScope.SupportsHeldItem(item) || VermilionPickupRules.Items.Contains(item);
    internal static bool ValidSpecies(PokemonSpeciesDefinition s) => BillBattleScope.ValidSpecies(s) ||
        s.SourceSpeciesId == 52 && s.Id.Value == "pret:SPECIES_MEOWTH" &&
        (s.Type1,s.Type2,s.Ability1,s.Ability2)==("TYPE_NORMAL","TYPE_NORMAL","ABILITY_PICKUP","ABILITY_NONE") ||
        s.SourceSpeciesId == 53 && s.Id.Value == "pret:SPECIES_PERSIAN" &&
        (s.Type1,s.Type2,s.Ability1,s.Ability2)==("TYPE_NORMAL","TYPE_NORMAL","ABILITY_LIMBER","ABILITY_NONE");
    internal static bool ValidDefinition(PokemonMoveDefinition m)
    {
        if(BillBattleScope.Supports(m.Id))return BillBattleScope.ValidDefinition(m);
        var expected=m.Id.Value switch {
            "pret:MOVE_PAY_DAY"=>(6,20,40,100,"TYPE_NORMAL","EFFECT_PAY_DAY","MOVE_TARGET_SELECTED",100,0),
            "pret:MOVE_FAINT_ATTACK"=>(185,20,60,0,"TYPE_DARK","EFFECT_ALWAYS_HIT","MOVE_TARGET_SELECTED",0,0), _=>default };
        string[] flags=["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED","FLAG_KINGS_ROCK_AFFECTED"];
        return expected.Item1!=0 && (m.SourceMoveId,m.Pp,m.Power,m.Accuracy,m.Type,m.Effect,m.Target,m.SecondaryEffectChance,m.Priority)==expected &&
            !m.Flags.IsDefault && m.Flags.Length==flags.Length && flags.All(m.Flags.Contains);
    }
}
public static class VermilionBattleData
{
    public static int WeightHectograms(SpeciesId species) => species.Value switch { "pret:SPECIES_MEOWTH"=>42,"pret:SPECIES_PERSIAN"=>320,_=>BillBattleData.WeightHectograms(species) };
}

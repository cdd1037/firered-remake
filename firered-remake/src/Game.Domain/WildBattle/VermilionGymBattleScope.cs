using Game.Domain.Ids;
using Game.Domain.Pokemon;
namespace Game.Domain.WildBattle;

/// <summary>Exact third-badge singles extension; no new species or implicit future effects.</summary>
internal static class VermilionGymBattleScope
{
    internal static bool Supports(MoveId move) => SsAnneBattleScope.Supports(move) || move.Value is "pret:MOVE_SONIC_BOOM" or "pret:MOVE_SHOCK_WAVE" or "pret:MOVE_SPARK";
    internal static bool SupportsSpecies(SpeciesId species) => SsAnneBattleScope.SupportsSpecies(species) || species.Value == "pret:SPECIES_RAICHU";
    internal static bool ValidSpecies(PokemonSpeciesDefinition s) => SsAnneBattleScope.ValidSpecies(s);
    internal static bool ValidDefinition(PokemonMoveDefinition m)
    {
        if (SsAnneBattleScope.Supports(m.Id)) return SsAnneBattleScope.ValidDefinition(m);
        var expected = m.Id.Value switch
        {
            "pret:MOVE_SONIC_BOOM" => (49,20,1,90,"TYPE_NORMAL","EFFECT_SONICBOOM","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_SHOCK_WAVE" => (351,20,60,0,"TYPE_ELECTRIC","EFFECT_ALWAYS_HIT","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_SPARK" => (209,20,65,100,"TYPE_ELECTRIC","EFFECT_PARALYZE_HIT","MOVE_TARGET_SELECTED",30,0),
            _ => default
        };
        string[] flags = m.Id.Value == "pret:MOVE_SPARK" ? ["FLAG_MAKES_CONTACT","FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"] : ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED","FLAG_KINGS_ROCK_AFFECTED"];
        return expected.Item1 != 0 && (m.SourceMoveId,m.Pp,m.Power,m.Accuracy,m.Type,m.Effect,m.Target,m.SecondaryEffectChance,m.Priority) == expected &&
            !m.Flags.IsDefault && m.Flags.Length == flags.Length && flags.All(m.Flags.Contains);
    }
}

public static class VermilionGymBattleData
{
    public static int WeightHectograms(SpeciesId species) => species.Value == "pret:SPECIES_RAICHU" ? 300 : SsAnneBattleData.WeightHectograms(species);
}

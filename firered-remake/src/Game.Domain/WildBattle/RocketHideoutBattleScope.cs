using Game.Domain.Ids;
using Game.Domain.Pokemon;
namespace Game.Domain.WildBattle;
/// <summary>Authenticated Rocket Hideout successor. Move metadata does not grant executable admission.</summary>
internal static class RocketHideoutBattleScope
{
    internal static bool Supports(MoveId id) => CeladonGymBattleScope.Supports(id) || id.Value is
        "pret:MOVE_COMET_PUNCH" or "pret:MOVE_FAKE_OUT" or "pret:MOVE_MEGA_PUNCH" or "pret:MOVE_DIZZY_PUNCH" or "pret:MOVE_REVERSAL";
    internal static bool ValidDefinition(PokemonMoveDefinition m)
    {
        if (CeladonGymBattleScope.ValidDefinition(m)) return true;
        var expected = m.Id.Value switch
        {
            "pret:MOVE_COMET_PUNCH" => (4,15,18,85,"TYPE_NORMAL","EFFECT_MULTI_HIT","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_MEGA_PUNCH" => (5,20,80,85,"TYPE_NORMAL","EFFECT_HIT","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_DIZZY_PUNCH" => (146,10,70,100,"TYPE_NORMAL","EFFECT_CONFUSE_HIT","MOVE_TARGET_SELECTED",20,0),
            "pret:MOVE_REVERSAL" => (179,15,1,100,"TYPE_FIGHTING","EFFECT_FLAIL","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_FAKE_OUT" => (252,10,40,100,"TYPE_NORMAL","EFFECT_FAKE_OUT","MOVE_TARGET_SELECTED",0,1),
            _ => default
        };
        string[] flags = m.Id.Value switch
        {
            "pret:MOVE_FAKE_OUT" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_DIZZY_PUNCH" => ["FLAG_MAKES_CONTACT","FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            _ => ["FLAG_MAKES_CONTACT","FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED","FLAG_KINGS_ROCK_AFFECTED"]
        };
        return expected != default && (m.SourceMoveId,m.Pp,m.Power,m.Accuracy,m.Type,m.Effect,m.Target,m.SecondaryEffectChance,m.Priority)==expected &&
            !m.Flags.IsDefault && m.Flags.Length==flags.Length && flags.All(m.Flags.Contains);
    }
    internal static bool SupportsSharedSpecies(SpeciesId id) => PokemonFactory.RocketHideoutSupportedSpecies.Contains(id);
    internal static bool ValidSharedSpecies(PokemonSpeciesDefinition s) => CeladonGymBattleScope.ValidSharedSpecies(s) ||
        s.Id.Value=="pret:SPECIES_KANGASKHAN" && s.SourceSpeciesId==115 &&
        (s.Type1,s.Type2,s.Ability1,s.Ability2)==("TYPE_NORMAL","TYPE_NORMAL","ABILITY_EARLY_BIRD","ABILITY_NONE");
    internal static bool SupportsAbility(string value) => value=="ABILITY_EARLY_BIRD" || RockTunnelBattleScope.SupportsAbility(value);
    internal static bool SupportsHeldItem(string value) => value=="ITEM_BLACK_GLASSES" || RockTunnelBattleScope.SupportsHeldItem(value);
}

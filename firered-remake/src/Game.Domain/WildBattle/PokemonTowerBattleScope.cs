using Game.Domain.Ids;
using Game.Domain.Pokemon;
namespace Game.Domain.WildBattle;
/// <summary>Tower successor admission. Explicit special wild entry validates the additional Ghost battle flags.</summary>
internal static class PokemonTowerBattleScope
{
    internal static bool Supports(MoveId id) => RocketHideoutBattleScope.Supports(id) || id.Value is
        "pret:MOVE_LICK" or "pret:MOVE_NIGHT_SHADE" or "pret:MOVE_SPITE" or "pret:MOVE_CURSE" or "pret:MOVE_SHADOW_PUNCH" or "pret:MOVE_BONEMERANG" or "pret:MOVE_DESTINY_BOND" or "pret:MOVE_DREAM_EATER" or "pret:MOVE_MEAN_LOOK" or "pret:MOVE_NIGHTMARE" or "pret:MOVE_SHADOW_BALL";
    internal static bool ValidDefinition(PokemonMoveDefinition m)
    {
        if (RocketHideoutBattleScope.ValidDefinition(m)) return true;
        var expected = m.Id.Value switch
        {
            "pret:MOVE_LICK" => (122,30,20,100,"TYPE_GHOST","EFFECT_PARALYZE_HIT","MOVE_TARGET_SELECTED",30,0),
            "pret:MOVE_NIGHT_SHADE" => (101,15,1,100,"TYPE_GHOST","EFFECT_LEVEL_DAMAGE","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_SPITE" => (180,10,0,100,"TYPE_GHOST","EFFECT_SPITE","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_CURSE" => (174,10,0,0,"TYPE_MYSTERY","EFFECT_CURSE","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_SHADOW_PUNCH" => (325,20,60,0,"TYPE_GHOST","EFFECT_ALWAYS_HIT","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_BONEMERANG" => (155,10,50,90,"TYPE_GROUND","EFFECT_DOUBLE_HIT","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_DESTINY_BOND" => (194,5,0,0,"TYPE_GHOST","EFFECT_DESTINY_BOND","MOVE_TARGET_USER",0,0),
            "pret:MOVE_DREAM_EATER" => (138,15,100,100,"TYPE_PSYCHIC","EFFECT_DREAM_EATER","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_MEAN_LOOK" => (212,5,0,100,"TYPE_NORMAL","EFFECT_MEAN_LOOK","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_NIGHTMARE" => (171,15,0,100,"TYPE_GHOST","EFFECT_NIGHTMARE","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_SHADOW_BALL" => (247,15,80,100,"TYPE_GHOST","EFFECT_SPECIAL_DEFENSE_DOWN_HIT","MOVE_TARGET_SELECTED",20,0),
            _ => default
        };
        string[] flags = m.Id.Value switch
        {
            "pret:MOVE_LICK" => ["FLAG_MAKES_CONTACT","FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_NIGHT_SHADE" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED","FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_SPITE" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_CURSE" => [],
            "pret:MOVE_SHADOW_PUNCH" => ["FLAG_MAKES_CONTACT","FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED","FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_BONEMERANG" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED","FLAG_KINGS_ROCK_AFFECTED"],
            "pret:MOVE_DESTINY_BOND" => [],
            "pret:MOVE_DREAM_EATER" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_MEAN_LOOK" => ["FLAG_PROTECT_AFFECTED","FLAG_MAGIC_COAT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_NIGHTMARE" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_SHADOW_BALL" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            _ => []
        };
        return expected != default && (m.SourceMoveId,m.Pp,m.Power,m.Accuracy,m.Type,m.Effect,m.Target,m.SecondaryEffectChance,m.Priority) == expected &&
            !m.Flags.IsDefault && m.Flags.Length == flags.Length && flags.All(m.Flags.Contains);
    }
    internal static bool SupportsSpecies(SpeciesId id) => PokemonFactory.PokemonTowerSupportedSpecies.Contains(id);
    internal static bool ValidSpecies(PokemonSpeciesDefinition s) => RocketHideoutBattleScope.ValidSharedSpecies(s) ||
        (s.Id.Value,s.SourceSpeciesId) is ("pret:SPECIES_GASTLY",92) or ("pret:SPECIES_HAUNTER",93) &&
        (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_GHOST","TYPE_POISON","ABILITY_LEVITATE","ABILITY_NONE");
    internal static bool SupportsWildOpponent(int species) => RockTunnelBattleScope.SupportsWildOpponent(species) || species is 58 or 92 or 93 or 104;
    internal static bool SupportsHeldItem(string item) => RocketHideoutBattleScope.SupportsHeldItem(item) || item is "ITEM_THICK_CLUB" or "ITEM_SPELL_TAG" or "ITEM_CLEANSE_TAG" or "ITEM_SOOTHE_BELL";
}

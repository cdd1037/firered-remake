using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
namespace Game.Domain.WildBattle;

/// <summary>The explicit 102-species successor. Original profiles never admit these definitions.</summary>
internal static class SnorlaxBattleScope
{
    internal static bool Supports(MoveId id) => PokemonTowerBattleScope.Supports(id) || id.Value is
        "pret:MOVE_YAWN" or "pret:MOVE_SNORE" or "pret:MOVE_SLEEP_TALK";
    internal static bool SupportsAbility(string ability) => RocketHideoutBattleScope.SupportsAbility(ability) || ability is "ABILITY_IMMUNITY" or "ABILITY_THICK_FAT";
    internal static bool SupportsSpecies(SpeciesId species) => PokemonFactory.SnorlaxSupportedSpecies.Contains(species);
    internal static bool SupportsHeldItem(string item) => PokemonTowerBattleScope.SupportsHeldItem(item);
    internal static bool ValidSpecies(PokemonSpeciesDefinition? s)
    {
        if (s is null) return false;
        if (s.SourceSpeciesId != 143) return PokemonTowerBattleScope.ValidSpecies(s);
        return s.Id.Value == "pret:SPECIES_SNORLAX" && s.BaseStats == new SixStats(160,110,65,30,65,110) &&
            (s.Type1,s.Type2,s.CatchRate,s.ExperienceYield,s.GrowthRate,s.GenderRatio,s.Ability1,s.Ability2,s.Friendship) ==
            ("TYPE_NORMAL","TYPE_NORMAL",25,154,GrowthRate.Slow,31,"ABILITY_IMMUNITY","ABILITY_THICK_FAT",70) &&
            s.EvYield == new SixStats(2,0,0,0,0,0) && !s.Evolutions.IsDefault && s.Evolutions.Length == 0 &&
            !s.LevelUpMoves.IsDefault && s.LevelUpMoves.Select(x => (x.Level,x.Move.Value)).SequenceEqual(new[] {
                (1,"pret:MOVE_TACKLE"),(5,"pret:MOVE_AMNESIA"),(9,"pret:MOVE_DEFENSE_CURL"),(13,"pret:MOVE_BELLY_DRUM"),
                (17,"pret:MOVE_HEADBUTT"),(21,"pret:MOVE_YAWN"),(25,"pret:MOVE_REST"),(29,"pret:MOVE_SNORE"),
                (33,"pret:MOVE_BODY_SLAM"),(37,"pret:MOVE_SLEEP_TALK"),(41,"pret:MOVE_BLOCK"),(45,"pret:MOVE_COVET"),
                (49,"pret:MOVE_ROLLOUT"),(53,"pret:MOVE_HYPER_BEAM") });
    }
    internal static bool ValidDefinition(PokemonMoveDefinition m)
    {
        if (PokemonTowerBattleScope.ValidDefinition(m)) return true;
        var expected = m.Id.Value switch {
            "pret:MOVE_YAWN" => (281,10,0,100,"TYPE_NORMAL","EFFECT_YAWN","MOVE_TARGET_SELECTED",0,0),
            "pret:MOVE_SNORE" => (173,15,40,100,"TYPE_NORMAL","EFFECT_SNORE","MOVE_TARGET_SELECTED",30,0),
            "pret:MOVE_SLEEP_TALK" => (214,10,0,0,"TYPE_NORMAL","EFFECT_SLEEP_TALK","MOVE_TARGET_DEPENDS",0,0),
            _ => default };
        string[] flags = m.Id.Value switch {
            "pret:MOVE_YAWN" => ["FLAG_PROTECT_AFFECTED","FLAG_MAGIC_COAT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED"],
            "pret:MOVE_SNORE" => ["FLAG_PROTECT_AFFECTED","FLAG_MIRROR_MOVE_AFFECTED","FLAG_KINGS_ROCK_AFFECTED"],
            _ => [] };
        return expected != default && (m.SourceMoveId,m.Pp,m.Power,m.Accuracy,m.Type,m.Effect,m.Target,m.SecondaryEffectChance,m.Priority) == expected &&
            !m.Flags.IsDefault && m.Flags.Length == flags.Length && flags.All(m.Flags.Contains);
    }
}

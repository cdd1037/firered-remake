using Game.Domain.Growth;
namespace Game.Domain.Pokemon;
/// <summary>Exact new FireRed route species; this contract never grants battle effect execution.</summary>
public static class FuchsiaRoutePokemonContract
{
    public static bool IsNewDefinition(PokemonSpeciesDefinition? s)
    {
        if (s is null || s.LevelUpMoves.IsDefault || s.Evolutions.IsDefault) return false;
        return s.SourceSpeciesId switch
        {
            83 => s.Id.Value=="pret:SPECIES_FARFETCHD" && s.BaseStats==new SixStats(52,65,55,60,58,62) && s.EvYield==new SixStats(0,1,0,0,0,0) &&
                (s.Type1,s.Type2,s.CatchRate,s.ExperienceYield,s.GenderRatio,s.Ability1,s.Ability2,s.Friendship)==("TYPE_NORMAL","TYPE_FLYING",45,94,127,"ABILITY_KEEN_EYE","ABILITY_INNER_FOCUS",70) && s.GrowthRate==GrowthRate.MediumFast &&
                s.LevelUpMoves.Select(m=>(m.Level,m.Move.Value)).SequenceEqual(new[] {(1,"pret:MOVE_PECK"),(6,"pret:MOVE_SAND_ATTACK"),(11,"pret:MOVE_LEER"),(16,"pret:MOVE_FURY_ATTACK"),(21,"pret:MOVE_KNOCK_OFF"),(26,"pret:MOVE_FURY_CUTTER"),(31,"pret:MOVE_SWORDS_DANCE"),(36,"pret:MOVE_AGILITY"),(41,"pret:MOVE_SLASH"),(46,"pret:MOVE_FALSE_SWIPE")}) && s.Evolutions.IsEmpty,
            84 => s.Id.Value=="pret:SPECIES_DODUO" && s.BaseStats==new SixStats(35,85,45,75,35,35) && s.EvYield==new SixStats(0,1,0,0,0,0) &&
                (s.Type1,s.Type2,s.CatchRate,s.ExperienceYield,s.GenderRatio,s.Ability1,s.Ability2,s.Friendship)==("TYPE_NORMAL","TYPE_FLYING",190,96,127,"ABILITY_RUN_AWAY","ABILITY_EARLY_BIRD",70) && s.GrowthRate==GrowthRate.MediumFast &&
                s.LevelUpMoves.Select(m=>(m.Level,m.Move.Value)).SequenceEqual(new[] {(1,"pret:MOVE_PECK"),(1,"pret:MOVE_GROWL"),(9,"pret:MOVE_PURSUIT"),(13,"pret:MOVE_FURY_ATTACK"),(21,"pret:MOVE_TRI_ATTACK"),(25,"pret:MOVE_RAGE"),(33,"pret:MOVE_UPROAR"),(37,"pret:MOVE_DRILL_PECK"),(45,"pret:MOVE_AGILITY")}) && s.Evolutions.Length==1 && s.Evolutions[0].Method==PokemonEvolutionMethod.Level && s.Evolutions[0].Level==31 && s.Evolutions[0].Parameter is null && s.Evolutions[0].Target.Value=="pret:SPECIES_DODRIO",
            85 => s.Id.Value=="pret:SPECIES_DODRIO" && s.BaseStats==new SixStats(60,110,70,100,60,60) && s.EvYield==new SixStats(0,2,0,0,0,0) &&
                (s.Type1,s.Type2,s.CatchRate,s.ExperienceYield,s.GenderRatio,s.Ability1,s.Ability2,s.Friendship)==("TYPE_NORMAL","TYPE_FLYING",45,158,127,"ABILITY_RUN_AWAY","ABILITY_EARLY_BIRD",70) && s.GrowthRate==GrowthRate.MediumFast &&
                s.LevelUpMoves.Select(m=>(m.Level,m.Move.Value)).SequenceEqual(new[] {(1,"pret:MOVE_PECK"),(1,"pret:MOVE_GROWL"),(1,"pret:MOVE_PURSUIT"),(1,"pret:MOVE_FURY_ATTACK"),(9,"pret:MOVE_PURSUIT"),(13,"pret:MOVE_FURY_ATTACK"),(21,"pret:MOVE_TRI_ATTACK"),(25,"pret:MOVE_RAGE"),(38,"pret:MOVE_UPROAR"),(47,"pret:MOVE_DRILL_PECK"),(60,"pret:MOVE_AGILITY")}) && s.Evolutions.IsEmpty,
            132 => s.Id.Value=="pret:SPECIES_DITTO" && s.BaseStats==new SixStats(48,48,48,48,48,48) && s.EvYield==new SixStats(1,0,0,0,0,0) &&
                (s.Type1,s.Type2,s.CatchRate,s.ExperienceYield,s.GenderRatio,s.Ability1,s.Ability2,s.Friendship)==("TYPE_NORMAL","TYPE_NORMAL",35,61,255,"ABILITY_LIMBER","ABILITY_NONE",70) && s.GrowthRate==GrowthRate.MediumFast &&
                s.LevelUpMoves.Select(m=>(m.Level,m.Move.Value)).SequenceEqual(new[] {(1,"pret:MOVE_TRANSFORM")}) && s.Evolutions.IsEmpty,
            _ => false
        };
    }
}

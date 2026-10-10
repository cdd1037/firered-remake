using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
namespace Game.Domain.WildBattle;

/// <summary>Exact gift species103 catalog extends the102-species battle mechanics, not the wild opponent roster.</summary>
public static class EeveeBattleScope
{
    // Older trainer/encounter executors explicitly share the Snorlax mechanics; the factory still
    // validates the real103-member identity set. Other unknown counts never collapse to this set.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PokemonCatalog, object> ValidatedCatalogs = new();
    public static int MechanicsCount(PokemonCatalog? catalog)
    {
        if (catalog?.Species.Count != 103) return catalog?.Species.Count ?? 0;
        // Immutable catalogs are verified once per object. A different catalog, including a forged
        // with-expression copy, must independently pass every identity and exact Eevee field check.
        _ = ValidatedCatalogs.GetValue(catalog, value => { PokemonFactory.ValidateCatalog(value); return new object(); });
        return 102;
    }
    internal static bool SupportsSharedSpecies(SpeciesId id) => PokemonFactory.EeveeSupportedSpecies.Contains(id);
    internal static bool ValidSharedSpecies(PokemonSpeciesDefinition? s) => IsEeveeDefinition(s) || SnorlaxBattleScope.ValidSpecies(s);
    public static bool IsEeveeDefinition(PokemonSpeciesDefinition? s) => s is not null &&
        s.Id.Value == "pret:SPECIES_EEVEE" && s.SourceSpeciesId == 133 && s.BaseStats == new SixStats(55,55,50,55,45,65) &&
        (s.Type1,s.Type2,s.CatchRate,s.ExperienceYield,s.GrowthRate,s.GenderRatio,s.Ability1,s.Ability2,s.Friendship) ==
        ("TYPE_NORMAL","TYPE_NORMAL",45,92,GrowthRate.MediumFast,31,"ABILITY_RUN_AWAY","ABILITY_NONE",70) &&
        s.EvYield == new SixStats(0,0,0,0,0,1) && !s.Evolutions.IsDefault && s.Evolutions.IsEmpty &&
        !s.LevelUpMoves.IsDefault && s.LevelUpMoves.Select(e => (e.Level,e.Move.Value)).SequenceEqual(new[] {
            (1,"pret:MOVE_TACKLE"),(1,"pret:MOVE_TAIL_WHIP"),(1,"pret:MOVE_HELPING_HAND"),(8,"pret:MOVE_SAND_ATTACK"),
            (16,"pret:MOVE_GROWL"),(23,"pret:MOVE_QUICK_ATTACK"),(30,"pret:MOVE_BITE"),(36,"pret:MOVE_BATON_PASS"),(42,"pret:MOVE_TAKE_DOWN") });
}

using Game.Domain.Rules;

namespace Game.Domain.Pokemon;

/// <summary>Permanent statistics calculated from explicit immutable mechanics data.</summary>
internal static class PokemonStatCalculation
{
    internal static BattleStats Calculate(PokemonSpeciesDefinition species, int level, Nature nature,
        SixStats ivs, SixStats evs, PokemonMechanicsData? mechanics = null)
    {
        var b = mechanics?.FormStatOverrides.FirstOrDefault(f => f.Species == species.Id)?.BaseStats ?? species.BaseStats;
        int Other(int basis, int iv, int ev, Stat stat) => StatRules.CalculateOther(basis, iv, ev, level, nature, stat);
        return new(StatRules.CalculateHp(b.Hp, ivs.Hp, evs.Hp, level, mechanics?.MaximumHpOneSpecies == species.Id),
            Other(b.Attack, ivs.Attack, evs.Attack, Stat.Attack),
            Other(b.Defense, ivs.Defense, evs.Defense, Stat.Defense),
            Other(b.SpecialAttack, ivs.SpecialAttack, evs.SpecialAttack, Stat.SpecialAttack),
            Other(b.SpecialDefense, ivs.SpecialDefense, evs.SpecialDefense, Stat.SpecialDefense),
            Other(b.Speed, ivs.Speed, evs.Speed, Stat.Speed));
    }
}

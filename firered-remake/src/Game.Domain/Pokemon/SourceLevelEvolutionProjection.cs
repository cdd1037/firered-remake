using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Pokemon;

/// <summary>Projects only original active normal level-up methods from authenticated source triples.
/// RTC friendship and trade methods are deliberately outside this normal evolution admission.</summary>
public static class SourceLevelEvolutionProjection
{
    public static ImmutableArray<PokemonEvolution> Project(SourceMechanicsCatalog source, SpeciesId species,
        IReadOnlySet<SpeciesId> admittedTargets)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(admittedTargets);
        var result = ImmutableArray.CreateBuilder<PokemonEvolution>();
        foreach (var row in source.SpeciesMechanics[species].SourceEvolutions)
        {
            PokemonEvolutionMethod? method = (row.Method, row.SourceMethodId) switch {
                ("EVO_FRIENDSHIP", 1) => PokemonEvolutionMethod.Friendship,
                ("EVO_LEVEL", 4) => PokemonEvolutionMethod.Level,
                ("EVO_LEVEL_ATK_GT_DEF", 8) => PokemonEvolutionMethod.LevelAttackGreaterDefense,
                ("EVO_LEVEL_ATK_EQ_DEF", 9) => PokemonEvolutionMethod.LevelAttackEqualDefense,
                ("EVO_LEVEL_ATK_LT_DEF", 10) => PokemonEvolutionMethod.LevelAttackLessDefense,
                _ => null
            };
            if (method is null || !admittedTargets.Contains(row.Target)) continue;
            if (row.Parameter != row.ParameterValue.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
                (method == PokemonEvolutionMethod.Friendship ? row.ParameterValue != 0 : row.ParameterValue is < 1 or > 100))
                throw new ArgumentException("normal_evolution_source_parameter");
            result.Add(new(row.ParameterValue, row.Target, row.Source) { Method = method.Value });
        }
        return result.ToImmutable();
    }
    internal static bool IsNormalMethod(PokemonEvolutionMethod method) => method is PokemonEvolutionMethod.Level or
        PokemonEvolutionMethod.Friendship or PokemonEvolutionMethod.LevelAttackGreaterDefense or
        PokemonEvolutionMethod.LevelAttackEqualDefense or PokemonEvolutionMethod.LevelAttackLessDefense;
    internal static bool Qualifies(PokemonEvolution evolution, PokemonInstance pokemon) =>
        Qualifies(evolution, pokemon.Level, pokemon.Friendship, pokemon.Stats.Attack, pokemon.Stats.Defense);

    /// <summary>Shared original trigger predicate for live level-up state and persisted trigger witnesses.
    /// The caller authenticates the evolution definition and validates the represented values.</summary>
    public static bool Qualifies(PokemonEvolution evolution, int level, int friendship, int attack, int defense) => evolution.Method switch {
        PokemonEvolutionMethod.Level => evolution.Level <= level,
        PokemonEvolutionMethod.Friendship => friendship >= 220,
        PokemonEvolutionMethod.LevelAttackGreaterDefense => evolution.Level <= level && attack > defense,
        PokemonEvolutionMethod.LevelAttackEqualDefense => evolution.Level <= level && attack == defense,
        PokemonEvolutionMethod.LevelAttackLessDefense => evolution.Level <= level && attack < defense,
        _ => false
    };
}

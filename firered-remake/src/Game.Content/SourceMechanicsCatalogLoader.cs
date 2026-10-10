using System.Collections.Immutable;
using Game.Domain.Pokemon;

namespace Game.Content;

public static class SourceMechanicsCatalogLoader
{
    public static SourceMechanicsCatalog LoadAuthenticated(string projectRoot) => LoadAuthenticated(projectRoot, projectRoot);

    public static SourceMechanicsCatalog LoadAuthenticated(string catalogRoot, string moveProjectRoot)
    {
        var content = PretCanonicalPokemonCatalog.Load(catalogRoot, moveProjectRoot);
        return SourceMechanicsCatalog.Authenticate(content.Catalog,
            content.Metadata.Values.Select(m => new SourceSpeciesMechanics(m.Id, m.WeightHectograms,
                m.SourceEvolutions.Select(e => new SourceMechanicsEvolution(e.Method, e.SourceMethodId,
                    e.Parameter, e.ParameterValue, e.Target, e.Source)).ToImmutableArray())).ToImmutableArray(),
            content.Abilities.Values.Select(a => new SourceAbilityIdentity(a.Symbol, a.SourceAbilityId)).ToImmutableArray(),
            content.GenerationContract.MaximumHpOneSpecies,
            content.GenerationContract.FormStatOverrides.Select(f => new SourceFormStatOverride(f.Species,
                f.Scope, f.BaseStats, f.Source, f.RuleSource)).ToImmutableArray());
    }
}

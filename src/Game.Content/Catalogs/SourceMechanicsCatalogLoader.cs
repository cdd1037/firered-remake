using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.Battle;

namespace Game.Content;

public static class SourceMechanicsCatalogLoader
{
    public static SourceMechanicsCatalog LoadAuthenticated(string projectRoot) => LoadAuthenticated(projectRoot, projectRoot);

    public static SourceMechanicsCatalog LoadAuthenticated(string catalogRoot, string moveProjectRoot)
    {
        var content = PretCanonicalPokemonCatalog.Load(catalogRoot, moveProjectRoot);
        return LoadAuthenticated(content, catalogRoot);
    }

    public static SourceMechanicsCatalog LoadAuthenticated(PretCanonicalPokemonCatalog content, string catalogRoot)
        => LoadAuthenticated(content, catalogRoot, SharedItemCatalog.LoadProject(catalogRoot));

    public static SourceMechanicsCatalog LoadAuthenticated(PretCanonicalPokemonCatalog content, string catalogRoot, SharedItemCatalog items)
    {
        var mechanics = SourceMechanicsCatalog.Create(content.Catalog,
            content.Metadata.Values.Select(m => new SourceSpeciesMechanics(m.Id, m.WeightHectograms,
                m.SourceEvolutions.Select(e => new SourceMechanicsEvolution(e.Method, e.SourceMethodId,
                    e.Parameter, e.ParameterValue, e.Target)).ToImmutableArray()) {
                    ItemCommon = m.ItemCommon, ItemRare = m.ItemRare }).ToImmutableArray(),
            content.Abilities.Values.Select(a => new SourceAbilityIdentity(a.Symbol, a.SourceAbilityId)).ToImmutableArray(),
            content.GenerationContract.MaximumHpOneSpecies,
            content.GenerationContract.FormStatOverrides.Select(f => new SourceFormStatOverride(f.Species,
                f.Scope, f.BaseStats)).ToImmutableArray());
        return SourceMechanicsCatalog.FromDefinitions(mechanics.Catalog with {
            BattleItems = SourceBattleItemCatalogLoader.LoadAuthenticated(catalogRoot, items) });
    }
}

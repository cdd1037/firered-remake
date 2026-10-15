using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

public sealed record SourceMechanicsEvolution(string Method, int SourceMethodId, string Parameter,
    int ParameterValue, SpeciesId Target, PokemonSourceRef Source);
public sealed record SourceSpeciesMechanics(SpeciesId Species, int WeightHectograms,
    ImmutableArray<SourceMechanicsEvolution> SourceEvolutions);
public sealed record SourceAbilityIdentity(string Symbol, int SourceAbilityId);
public sealed record SourceFormStatOverride(SpeciesId Species, string Scope, SixStats BaseStats,
    PokemonSourceRef Source, PokemonSourceRef RuleSource);

/// <summary>Immutable source definitions and mechanics metadata, separate from campaign eligibility.</summary>
public sealed class SourceMechanicsCatalog
{
    public const string SemanticVersion = "firered-source-mechanics-v1";
    private static readonly ConditionalWeakTable<PokemonCatalog, SourceMechanicsCatalog> Authenticated = new();
    public PokemonCatalog Catalog { get; }
    public ImmutableDictionary<SpeciesId, SourceSpeciesMechanics> SpeciesMechanics { get; }
    public ImmutableDictionary<string, SourceAbilityIdentity> Abilities { get; }
    public SpeciesId MaximumHpOneSpecies { get; }
    public ImmutableArray<SourceFormStatOverride> FormStatOverrides { get; }

    private SourceMechanicsCatalog(PokemonCatalog catalog, ImmutableArray<SourceSpeciesMechanics> metadata,
        ImmutableArray<SourceAbilityIdentity> abilities, SpeciesId maximumHpOneSpecies,
        ImmutableArray<SourceFormStatOverride> formStatOverrides)
    {
        Catalog = catalog;
        SpeciesMechanics = metadata.ToImmutableDictionary(m => m.Species);
        Abilities = abilities.ToImmutableDictionary(a => a.Symbol, StringComparer.Ordinal);
        MaximumHpOneSpecies = maximumHpOneSpecies;
        FormStatOverrides = formStatOverrides;
    }

    /// <summary>Bind parsed definitions and metadata to one immutable catalog owner.</summary>
    public static SourceMechanicsCatalog Authenticate(PokemonCatalog catalog,
        ImmutableArray<SourceSpeciesMechanics> metadata, ImmutableArray<SourceAbilityIdentity> abilities,
        SpeciesId maximumHpOneSpecies, ImmutableArray<SourceFormStatOverride> formStatOverrides)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != PokemonFactory.SourceCommit || catalog.Species is null || catalog.Moves is null ||
            metadata.IsDefault || abilities.IsDefault || formStatOverrides.IsDefault ||
            catalog.Species.Any(p => p.Value is null || p.Key != p.Value.Id) ||
            catalog.Moves.Any(p => p.Key != p.Value?.Id || !Game.Domain.WildBattle.SourceMoveDefinitionRules.IsValid(p.Value)))
            throw new ArgumentException("source_mechanics_identity");
        if (metadata.Any(m => m is null || m.WeightHectograms <= 0 || m.SourceEvolutions.IsDefault ||
                m.SourceEvolutions.Any(e => e is null || !catalog.Species.ContainsKey(e.Target))) ||
            !metadata.Select(m => m.Species).ToHashSet().SetEquals(catalog.Species.Keys) ||
            abilities.Any(a => a is null || string.IsNullOrWhiteSpace(a.Symbol) || a.SourceAbilityId < 0) ||
            !catalog.Species.ContainsKey(maximumHpOneSpecies) ||
            formStatOverrides.Any(f => f is null || !catalog.Species.ContainsKey(f.Species) || f.BaseStats is null ||
                f.BaseStats.Values.Any(value => value is < 1 or > 255)))
            throw new ArgumentException("source_mechanics_metadata");
        // Clone the root so each owner has an independent immutable catalog identity.
        var result = new SourceMechanicsCatalog(catalog with { }, metadata, abilities, maximumHpOneSpecies, formStatOverrides);
        Authenticated.Add(result.Catalog, result);
        return result;
    }

    internal static SourceMechanicsCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Authenticated.TryGetValue(catalog, out var proof) ? proof : null;

}

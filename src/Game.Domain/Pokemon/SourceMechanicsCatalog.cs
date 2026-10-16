using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

public sealed record SourceMechanicsEvolution(string Method, int SourceMethodId, string Parameter,
    int ParameterValue, SpeciesId Target);
public sealed record SourceSpeciesMechanics(SpeciesId Species, int WeightHectograms,
    ImmutableArray<SourceMechanicsEvolution> SourceEvolutions)
{
    public string ItemCommon { get; init; } = "ITEM_NONE";
    public string ItemRare { get; init; } = "ITEM_NONE";
}
public sealed record SourceAbilityIdentity(string Symbol, int SourceAbilityId);
public sealed record SourceFormStatOverride(SpeciesId Species, string Scope, SixStats BaseStats);

public sealed record PokemonMechanicsData(
    ImmutableDictionary<SpeciesId, SourceSpeciesMechanics> SpeciesMechanics,
    ImmutableDictionary<string, SourceAbilityIdentity> Abilities,
    SpeciesId MaximumHpOneSpecies,
    ImmutableArray<SourceFormStatOverride> FormStatOverrides);

/// <summary>Immutable source definitions and mechanics metadata, separate from campaign eligibility.</summary>
public sealed class SourceMechanicsCatalog
{
    public const string SemanticVersion = "firered-source-mechanics-v1";
    public PokemonCatalog Catalog { get; }
    public PokemonMechanicsData Data => Catalog.MechanicsData!;
    public ImmutableDictionary<SpeciesId, SourceSpeciesMechanics> SpeciesMechanics => Data.SpeciesMechanics;
    public ImmutableDictionary<string, SourceAbilityIdentity> Abilities => Data.Abilities;
    public SpeciesId MaximumHpOneSpecies => Data.MaximumHpOneSpecies;
    public ImmutableArray<SourceFormStatOverride> FormStatOverrides => Data.FormStatOverrides;

    private SourceMechanicsCatalog(PokemonCatalog catalog) { Catalog = catalog; }

    /// <summary>Compose definitions with their explicit mechanics data; no identity registry is consulted.</summary>
    public static SourceMechanicsCatalog FromDefinitions(PokemonCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.MechanicsData is null) throw new ArgumentException("source_mechanics_missing");
        return new(catalog);
    }

    /// <summary>Validate and compose definitions with immutable mechanics metadata.</summary>
    public static SourceMechanicsCatalog Create(PokemonCatalog catalog,
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
                m.ItemCommon is null || !m.ItemCommon.StartsWith("ITEM_", StringComparison.Ordinal) ||
                m.ItemRare is null || !m.ItemRare.StartsWith("ITEM_", StringComparison.Ordinal) ||
                m.SourceEvolutions.Any(e => e is null || !catalog.Species.ContainsKey(e.Target))) ||
            !metadata.Select(m => m.Species).ToHashSet().SetEquals(catalog.Species.Keys) ||
            abilities.Any(a => a is null || string.IsNullOrWhiteSpace(a.Symbol) || a.SourceAbilityId < 0) ||
            !catalog.Species.ContainsKey(maximumHpOneSpecies) ||
            formStatOverrides.Any(f => f is null || !catalog.Species.ContainsKey(f.Species) || f.BaseStats is null ||
                f.BaseStats.Values.Any(value => value is < 1 or > 255)))
            throw new ArgumentException("source_mechanics_metadata");
        var data = new PokemonMechanicsData(metadata.ToImmutableDictionary(m => m.Species),
            abilities.ToImmutableDictionary(a => a.Symbol, StringComparer.Ordinal), maximumHpOneSpecies, formStatOverrides);
        return new(catalog with { MechanicsData = data });
    }
}

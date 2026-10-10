using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

public sealed record SourceMechanicsEvolution(string Method, int SourceMethodId, string Parameter,
    int ParameterValue, SpeciesId Target, PokemonSourceRef Source);
public sealed record SourceSpeciesMechanics(SpeciesId Species, int WeightHectograms,
    ImmutableArray<SourceMechanicsEvolution> SourceEvolutions);
public sealed record SourceAbilityIdentity(string Symbol, int SourceAbilityId);
public sealed record SourceFormStatOverride(SpeciesId Species, string Scope, SixStats BaseStats,
    PokemonSourceRef Source, PokemonSourceRef RuleSource);

/// <summary>Immutable, authenticated source definitions, separate from campaign eligibility.
/// Authentication covers every species/move field, weights, original evolution triples, ability IDs
/// and the FireRed ordinary non-link stat overrides. A cloned catalog never inherits this proof.</summary>
public sealed class SourceMechanicsCatalog
{
    public const string SemanticVersion = "firered-source-mechanics-v1";
    public const string SemanticSha256 = "c84202efe70af7801c2d603c9165b460407dabce1fec7df3b0f08641399d2f3b";
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

    /// <summary>Content transports definitions, not a trusted flag. Domain independently authenticates
    /// their complete semantic value against the fixed fingerprint generated from pinned source.</summary>
    public static SourceMechanicsCatalog Authenticate(PokemonCatalog catalog,
        ImmutableArray<SourceSpeciesMechanics> metadata, ImmutableArray<SourceAbilityIdentity> abilities,
        SpeciesId maximumHpOneSpecies, ImmutableArray<SourceFormStatOverride> formStatOverrides)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != PokemonFactory.SourceCommit || catalog.Species is null || catalog.Moves is null ||
            metadata.IsDefault || abilities.IsDefault || formStatOverrides.IsDefault ||
            catalog.Species.Any(p => p.Value is null || p.Key != p.Value.Id) ||
            catalog.Moves.Any(p => p.Value is null || p.Key != p.Value.Id))
            throw new ArgumentException("source_mechanics_identity");
        string hash = Fingerprint(catalog, metadata, abilities, maximumHpOneSpecies, formStatOverrides);
        if (!string.Equals(hash, SemanticSha256, StringComparison.Ordinal))
            throw new ArgumentException("source_mechanics_fingerprint");
        // Clone the root record so every proof owns its own exact immutable catalog identity.
        var result = new SourceMechanicsCatalog(catalog with { }, metadata, abilities, maximumHpOneSpecies, formStatOverrides);
        Authenticated.Add(result.Catalog, result);
        return result;
    }

    internal static SourceMechanicsCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Authenticated.TryGetValue(catalog, out var proof) ? proof : null;

    private static string Fingerprint(PokemonCatalog catalog, ImmutableArray<SourceSpeciesMechanics> metadata,
        ImmutableArray<SourceAbilityIdentity> abilities, SpeciesId maximumHpOneSpecies,
        ImmutableArray<SourceFormStatOverride> formStatOverrides)
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info => {
            if (info.Type == typeof(SixStats))
                foreach (var property in info.Properties.Where(p => p.Name == "values").ToArray()) info.Properties.Remove(property);
        });
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, TypeInfoResolver = resolver,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
        var document = JsonSerializer.SerializeToElement(new {
            catalog.SourceCommit,
            Species = catalog.Species.Values.OrderBy(s => s.SourceSpeciesId).ToArray(),
            Moves = catalog.Moves.Values.OrderBy(m => m.SourceMoveId).ToArray(),
            Metadata = metadata.OrderBy(m => m.Species.Value, StringComparer.Ordinal).ToArray(),
            Abilities = abilities.OrderBy(a => a.Symbol, StringComparer.Ordinal).ToArray(),
            MaximumHpOneSpecies = maximumHpOneSpecies,
            FormStatOverrides = formStatOverrides
        }, options);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            WriteCanonical(writer, document);
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            { writer.WritePropertyName(property.Name); WriteCanonical(writer, property.Value); }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item); writer.WriteEndArray();
        }
        else value.WriteTo(writer);
    }
}

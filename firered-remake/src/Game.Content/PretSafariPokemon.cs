using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record SafariHeldItems(SpeciesId Species, string Common, string Rare);
public sealed record SafariMachineRow(SpeciesId Species, ImmutableArray<string> MachineSymbols);
public sealed record SafariSpeciesRates(SpeciesId Species, int CatchRate, int FleeRate);
public sealed record SafariSpeciesEvolutions(SpeciesId Species, ImmutableArray<CanonicalSourceEvolution> SourceEvolutions);
public sealed record SafariSpeciesCry(SpeciesId Species, int SourceSpeciesId, string CueId, int NormalTableIndex, string SourceSymbol,
    string SourceSamplePath, PokemonSourceRef NormalSource, PokemonSourceRef ReverseSource,
    PokemonSourceRef SampleBindingSource);
public sealed record SafariPokemonDocument(int SchemaVersion, string Profile, string SourceCommit, ImmutableArray<ForestPokemonInput> Inputs,
    ImmutableArray<PokemonSpeciesDefinition> Species, ImmutableArray<PretRoute1SpeciesName> SpeciesNames,
    ImmutableArray<ForestPokemonConstant> Constants, ImmutableArray<PewterPokemonEncodedName> EncodedNames,
    ImmutableArray<MoveId> RequiredMoves, int? NaturalLevelLimit, ImmutableArray<SafariHeldItems> HeldItems,
    ImmutableArray<SafariMachineRow> Machines, ImmutableArray<SafariSpeciesRates> SafariRates,
    ImmutableArray<SafariSpeciesEvolutions> SourceEvolutions, ImmutableArray<SafariSpeciesCry> Cries,
    ImmutableArray<string> Limitations);

/// <summary>Authenticated three-species additive metadata; preserves source evolution triples without executing them.</summary>
public sealed class PretSafariPokemon
{
    public const string Profile = "firered-safari-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string CatalogRelativePath = "content/generated/safari-pokemon/catalog.json";
    public PokemonCatalog Catalog { get; }
    public SafariPokemonDocument Document { get; }
    public ImmutableSortedDictionary<int, string> AdditionalSpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> AdditionalMoveNames { get; } = ImmutableDictionary<MoveId,string>.Empty;
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }

    private PretSafariPokemon(PokemonCatalog catalog, SafariPokemonDocument document,
        ImmutableArray<PretWorldSourceFile> files, PretCanonicalPokemonCatalog canonical)
    {
        Catalog = catalog; Document = document; SourceFiles = files;
        AdditionalSpeciesNames = document.SpeciesNames.ToImmutableSortedDictionary(n => catalog.Species[n.Id].SourceSpeciesId, n => n.Name);
        Constants = document.Constants.ToImmutableDictionary(c => c.Symbol, c => c.Value, StringComparer.Ordinal);
        EncodedNames = document.EncodedNames.ToImmutableDictionary(n => n.Symbol, StringComparer.Ordinal);
    }

    /// <summary>Resolve regional presentation against the current admitted catalog.</summary>
    public static PretSafariPokemon Load(PretCanonicalPokemonCatalog canonical, PokemonCatalog catalog, string projectRoot)
    {
        var doc = SafariSource.Parse<SafariPokemonDocument>(SafariSource.Read(projectRoot, CatalogRelativePath), canonical: canonical);
        SafariSource.Require(doc.SchemaVersion == 1 && !doc.Species.IsDefaultOrEmpty &&
            doc.Species.All(species => catalog.Species.ContainsKey(species.Id)) &&
            doc.RequiredMoves.All(catalog.Moves.ContainsKey), "Safari presentation references are not admitted.");
        foreach (var name in doc.EncodedNames)
            SafariSource.Require(!name.Bytes.IsDefaultOrEmpty && name.Bytes[^1] == 255,
                "Safari encoded name is unterminated.");
        var files = doc.Inputs.Select(input => new PretWorldSourceFile(input.Path)).ToImmutableArray();
        return new(catalog, doc, files, canonical);
    }
}

/// <summary>Local resource reads and JSON decoding.</summary>
internal static class SafariSource
{
    internal static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    internal static byte[] Read(string root, string relative)
        => File.ReadAllBytes(PretRuntimeMapValidation.SafeFile(root, relative));
    internal static T Parse<T>(byte[] bytes, string? machineRoot = null, PretCanonicalPokemonCatalog? canonical = null)
    {
        using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 64 });
        void Unique(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in e.EnumerateObject()) { Require(names.Add(p.Name), "Duplicate Safari property."); Unique(p.Value); }
            }
            else if (e.ValueKind == JsonValueKind.Array) foreach (var child in e.EnumerateArray()) Unique(child);
        }
        Unique(json.RootElement);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true, MaxDepth = 64 };
        options.Converters.Add(new SafariEnumConverter<GrowthRate>());
        options.Converters.Add(new SafariEnumConverter<PokemonEvolutionMethod>());
        canonical?.AddReferenceConverters(options);
        if (machineRoot is not null) SharedMachineCatalog.AddConverters(options, machineRoot);
        return JsonSerializer.Deserialize<T>(bytes, options) ?? throw new InvalidDataException("Null Safari document.");
    }
    private sealed class SafariEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
                foreach (var value in Enum.GetValues<T>())
                    if (JsonNamingPolicy.CamelCase.ConvertName(value.ToString()) == reader.GetString()) return value;
            throw new JsonException("Expected exact Safari enum.");
        }
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => throw new NotSupportedException();
    }
}

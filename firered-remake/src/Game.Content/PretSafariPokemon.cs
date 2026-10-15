using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record SafariHeldItems(SpeciesId Species, string Common, string Rare, PokemonSourceRef Source);
public sealed record SafariMachineRow(SpeciesId Species, ImmutableArray<string> MachineSymbols, PokemonSourceRef Source);
public sealed record SafariSpeciesRates(SpeciesId Species, int CatchRate, int FleeRate, PokemonSourceRef Source);
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

    /// <summary>Strict107/354 predecessor; old records retain identity. Domain admission remains the caller's separate policy.</summary>
    public static PretSafariPokemon Load(PokemonCatalog baseCatalog, string projectRoot, string? dependencyProjectRoot = null)
    {
        PokemonFactory.ValidateCatalog(baseCatalog);
        SafariSource.Require(baseCatalog.Species.Count == 107 && baseCatalog.Moves.Count == 354,
            "Safari requires the complete107-species,354-move Fuchsia Gym predecessor.");
        string root = Path.GetFullPath(projectRoot), dependency = Path.GetFullPath(dependencyProjectRoot ?? root);
        var bytes = SafariSource.Read(root,CatalogRelativePath);
        var doc = SafariSource.Parse<SafariPokemonDocument>(bytes);
        SafariSource.Require(doc.SchemaVersion == 1 && doc.Species.Length == 3 && doc.SpeciesNames.Length == 3 &&
            doc.EncodedNames.Length == 3 && doc.RequiredMoves.Length == 31 && doc.HeldItems.Length == 3 &&
            doc.Machines.Length == 3 && doc.SafariRates.Length == 3 && doc.SourceEvolutions.Length == 3 &&
            doc.Cries.Length == 3 && doc.NaturalLevelLimit is null && !doc.Limitations.IsDefaultOrEmpty , "Safari source document scope differs.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        
        foreach (var input in doc.Inputs)
        {
            files.Add(new(input.Path));
        }
        var canonical = PretCanonicalPokemonCatalog.Load(dependency, dependency);
        var moveSource = PretMetronomeMoveCatalog.Load(dependency);
        SafariSource.Require(baseCatalog.Moves.Values.All(moveSource.MatchesSource), "Safari predecessor move source differs.");
        SafariSource.Require(doc.Species.Select(s => s.SourceSpeciesId).SequenceEqual([113,123,128]), "Safari exact species delta differs.");
        var species = baseCatalog.Species.ToBuilder();
        foreach (var s in doc.Species)
        {
            SafariSource.Require(JsonSerializer.Serialize(s) == JsonSerializer.Serialize(canonical.Catalog.Species[s.Id]),
                "Safari species differs from authenticated canonical386: " + s.Id.Value);
            SafariSource.Require(s.Evolutions.IsEmpty, "Deferred Safari evolutions cannot be executed by this metadata projection.");
            species.Add(s.Id, s);
            var metadata = canonical.Metadata[s.Id];
            var originals = doc.SourceEvolutions.Single(e => e.Species == s.Id);
            SafariSource.Require(JsonSerializer.Serialize(originals.SourceEvolutions) == JsonSerializer.Serialize(metadata.SourceEvolutions),
                "Safari original evolution triples differ.");
            var h = doc.HeldItems.Single(h => h.Species == s.Id);
            SafariSource.Require(h.Common == metadata.OriginalFields["itemCommon"] && h.Rare == metadata.OriginalFields["itemRare"],
                "Safari original held-item slots differ.");             var rate = doc.SafariRates.Single(r => r.Species == s.Id);
            SafariSource.Require(rate.CatchRate == s.CatchRate && rate.FleeRate == int.Parse(metadata.OriginalFields["safariZoneFleeRate"]),
                "Safari source catch/escape rates differ.");         }
        SafariSource.Require(doc.RequiredMoves.ToHashSet().SetEquals(doc.Species.SelectMany(s => s.LevelUpMoves).Select(m => m.Move)) &&
            doc.RequiredMoves.All(baseCatalog.Moves.ContainsKey), "Safari full natural move closure differs.");
        foreach (var n in doc.EncodedNames)
        {
            SafariSource.Require(n.Bytes.Length == n.Name.Length + 1 && n.Bytes[^1] == 255, "Safari encoded name differs.");
        }
        foreach (var cry in doc.Cries)
        {
            SafariSource.Require(cry.SourceSpeciesId == species[cry.Species].SourceSpeciesId && cry.NormalTableIndex == cry.SourceSpeciesId - 1 , "Safari cry binding differs.");
        }
        var result = new PokemonCatalog(SourceCommit, species.ToImmutable(), baseCatalog.Moves);
        SafariSource.Require(result.Species.Count == 110 && result.Moves.Count == 354 &&
            baseCatalog.Species.All(p => ReferenceEquals(p.Value, result.Species[p.Key])) && ReferenceEquals(result.Moves, baseCatalog.Moves),
            "Safari inherited metadata changed.");
        return new(result, doc, files.ToImmutable(), canonical);
    }
}

/// <summary>Local resource reads and JSON decoding.</summary>
internal static class SafariSource
{
    internal static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    internal static byte[] Read(string root, string relative)
        => File.ReadAllBytes(PretRuntimeMapValidation.SafeFile(root, relative));
    internal static T Parse<T>(byte[] bytes, string? machineRoot = null)
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

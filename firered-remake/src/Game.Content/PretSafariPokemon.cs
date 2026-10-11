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
public sealed record SafariSpeciesCry(SpeciesId Species, int SourceSpeciesId, string CueId, int NormalTableIndex,
    string SourceSymbol, string SourceSamplePath, string SourceSampleSha256, PokemonSourceRef NormalSource,
    PokemonSourceRef ReverseSource, PokemonSourceRef SampleBindingSource);
public sealed record SafariPokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ForestPokemonInput> Inputs, ImmutableArray<PokemonSpeciesDefinition> Species,
    ImmutableArray<PretRoute1SpeciesName> SpeciesNames, ImmutableArray<ForestPokemonConstant> Constants,
    ImmutableArray<PewterPokemonEncodedName> EncodedNames, ImmutableArray<MoveId> RequiredMoves,
    int? NaturalLevelLimit, ImmutableArray<SafariHeldItems> HeldItems, ImmutableArray<SafariMachineRow> Machines,
    ImmutableArray<SafariSpeciesRates> SafariRates, ImmutableArray<SafariSpeciesEvolutions> SourceEvolutions,
    ImmutableArray<SafariSpeciesCry> Cries, string CanonicalCatalogSha256, ImmutableArray<string> Limitations);

/// <summary>Authenticated three-species additive metadata; preserves source evolution triples without executing them.</summary>
public sealed class PretSafariPokemon
{
    public const string Profile = "firered-safari-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string LockSha256 = "573fc41300b93c5e68af2b86b536907efe83985d2fba139412f3e4374835b668";
    public const int LockByteLength = 12425;
    public const int InputCount = 51;
    public const string CatalogSha256 = "e99fb0a963d97c62226a6e0bb98861861e8a79d737b18a9428d4fcff48dab89b";
    public const int CatalogByteLength = 41194;
    public const string CatalogRelativePath = "content/generated/safari-pokemon/catalog.json";
    public const string FixturesSha256 = "9d5c052bfde3822c7889dc5cf4a2d77ed1580ec37caf353e374b5b825dd31806";
    public PokemonCatalog Catalog { get; }
    public SafariPokemonDocument Document { get; }
    public ImmutableSortedDictionary<int, string> AdditionalSpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> AdditionalMoveNames { get; } = ImmutableDictionary<MoveId,string>.Empty;
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }

    private PretSafariPokemon(PokemonCatalog catalog, SafariPokemonDocument document,
        ImmutableArray<PretWorldSourceFile> files, PretCanonicalPokemonCatalog canonical)
    {
        Catalog = catalog; Document = document; SourceFiles = files;
        AdditionalSpeciesNames = document.SpeciesNames.ToImmutableSortedDictionary(n => catalog.Species[n.Id].SourceSpeciesId, n => n.Name);
        Constants = document.Constants.ToImmutableDictionary(c => c.Symbol, c => c.Value, StringComparer.Ordinal);
        EncodedNames = document.EncodedNames.ToImmutableDictionary(n => n.Symbol, StringComparer.Ordinal);
        AdditionalSourceHashes = ["safari-pokemon-lock:" + LockSha256, "safari-pokemon-catalog:" + CatalogSha256,
            .. files.Select(f => "safari-pokemon:" + f.Path + ":" + f.Sha256), .. canonical.SourceHashes];
    }

    /// <summary>Strict107/354 predecessor; old records retain identity. Domain admission remains the caller's separate policy.</summary>
    public static PretSafariPokemon Load(PokemonCatalog baseCatalog, string projectRoot, string? dependencyProjectRoot = null)
    {
        PokemonFactory.ValidateCatalog(baseCatalog);
        SafariSource.Require(baseCatalog.Species.Count == 107 && baseCatalog.Moves.Count == 354,
            "Safari requires the complete107-species,354-move Fuchsia Gym predecessor.");
        string root = Path.GetFullPath(projectRoot), dependency = Path.GetFullPath(dependencyProjectRoot ?? root);
        var bytes = SafariSource.Read(root, CatalogRelativePath, CatalogSha256, CatalogByteLength);
        _ = SafariSource.Read(root, "scripts/safari-pokemon.lock.json", LockSha256, LockByteLength);
        var doc = SafariSource.Parse<SafariPokemonDocument>(bytes);
        SafariSource.Require(doc.SchemaVersion == 1 && doc.Profile == Profile && doc.SourceCommit == SourceCommit &&
            doc.Inputs.Length == InputCount && doc.Species.Length == 3 && doc.SpeciesNames.Length == 3 &&
            doc.EncodedNames.Length == 3 && doc.RequiredMoves.Length == 31 && doc.HeldItems.Length == 3 &&
            doc.Machines.Length == 3 && doc.SafariRates.Length == 3 && doc.SourceEvolutions.Length == 3 &&
            doc.Cries.Length == 3 && doc.NaturalLevelLimit is null && !doc.Limitations.IsDefaultOrEmpty &&
            doc.CanonicalCatalogSha256 == PretCanonicalPokemonCatalog.CatalogSha256, "Safari source document scope differs.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        var lineCounts = new Dictionary<string,int>(StringComparer.Ordinal);
        foreach (var input in doc.Inputs)
        {
            var source = SafariSource.Read(root, "content/raw/safari-pokemon/" + input.Path, input.Sha256, input.ByteLength);
            SafariSource.Require(SafariSource.GitBlob(source) == input.GitBlobSha1, "Safari Git blob differs: " + input.Path);
            if (!input.Path.EndsWith(".png", StringComparison.Ordinal) && !input.Path.EndsWith(".wav", StringComparison.Ordinal))
                lineCounts.Add(input.Path, new UTF8Encoding(false,true).GetString(source).Count(c => c == '\n') + 1);
            files.Add(new(input.Path, input.GitBlobSha1, input.ByteLength, input.Sha256,
                $"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{input.Path}"));
        }
        void Source(PokemonSourceRef r) => SafariSource.Require(doc.Inputs.Any(f => f.Path == r.Path && f.Sha256 == r.Sha256) &&
            r.StartLine > 0 && r.EndLine >= r.StartLine && lineCounts.TryGetValue(r.Path, out int lines) &&
            r.EndLine <= lines && !string.IsNullOrWhiteSpace(r.Symbol), "Safari source reference differs.");
        var canonical = PretCanonicalPokemonCatalog.Load(dependency, dependency);
        var moveSource = PretMetronomeMoveCatalog.Load(dependency);
        SafariSource.Require(baseCatalog.Moves.Values.All(moveSource.MatchesSource), "Safari predecessor move source differs.");
        SafariSource.Require(doc.Species.Select(s => s.SourceSpeciesId).SequenceEqual([113,123,128]), "Safari exact species delta differs.");
        var species = baseCatalog.Species.ToBuilder();
        foreach (var s in doc.Species)
        {
            SafariSource.Require(JsonSerializer.Serialize(s) == JsonSerializer.Serialize(canonical.Catalog.Species[s.Id]),
                "Safari species differs from authenticated canonical386: " + s.Id.Value);
            Source(s.Source); Source(s.LearnsetSource);
            foreach (var row in s.LevelUpMoves) Source(row.Source);
            SafariSource.Require(s.Evolutions.IsEmpty, "Deferred Safari evolutions cannot be executed by this metadata projection.");
            species.Add(s.Id, s);
            var metadata = canonical.Metadata[s.Id];
            var originals = doc.SourceEvolutions.Single(e => e.Species == s.Id);
            SafariSource.Require(JsonSerializer.Serialize(originals.SourceEvolutions) == JsonSerializer.Serialize(metadata.SourceEvolutions),
                "Safari original evolution triples differ.");
            foreach (var e in originals.SourceEvolutions) Source(e.Source);
            var h = doc.HeldItems.Single(h => h.Species == s.Id);
            SafariSource.Require(h.Common == metadata.OriginalFields["itemCommon"] && h.Rare == metadata.OriginalFields["itemRare"],
                "Safari original held-item slots differ."); Source(h.Source);
            var rate = doc.SafariRates.Single(r => r.Species == s.Id);
            SafariSource.Require(rate.CatchRate == s.CatchRate && rate.FleeRate == int.Parse(metadata.OriginalFields["safariZoneFleeRate"]),
                "Safari source catch/escape rates differ."); Source(rate.Source);
        }
        SafariSource.Require(doc.RequiredMoves.ToHashSet().SetEquals(doc.Species.SelectMany(s => s.LevelUpMoves).Select(m => m.Move)) &&
            doc.RequiredMoves.All(baseCatalog.Moves.ContainsKey), "Safari full natural move closure differs.");
        foreach (var n in doc.SpeciesNames) Source(n.Source);
        foreach (var c in doc.Constants) Source(c.Source);
        foreach (var n in doc.EncodedNames)
        {
            Source(n.Source); Source(n.EncodingSource);
            SafariSource.Require(n.Bytes.Length == n.Name.Length + 1 && n.Bytes[^1] == 255, "Safari encoded name differs.");
        }
        foreach (var m in doc.Machines) Source(m.Source);
        foreach (var cry in doc.Cries)
        {
            Source(cry.NormalSource); Source(cry.ReverseSource); Source(cry.SampleBindingSource);
            SafariSource.Require(cry.SourceSpeciesId == species[cry.Species].SourceSpeciesId && cry.NormalTableIndex == cry.SourceSpeciesId - 1 &&
                doc.Inputs.Any(i => i.Path == cry.SourceSamplePath && i.Sha256 == cry.SourceSampleSha256), "Safari cry binding differs.");
        }
        var result = new PokemonCatalog(SourceCommit, species.ToImmutable(), baseCatalog.Moves);
        SafariSource.Require(result.Species.Count == 110 && result.Moves.Count == 354 &&
            baseCatalog.Species.All(p => ReferenceEquals(p.Value, result.Species[p.Key])) && ReferenceEquals(result.Moves, baseCatalog.Moves),
            "Safari inherited metadata changed.");
        return new(result, doc, files.ToImmutable(), canonical);
    }
}

/// <summary>Bounded fixed-identity reads, strict JSON, and complete parent-path symlink rejection.</summary>
internal static class SafariSource
{
    internal static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static string GitBlob(byte[] bytes) => Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes]));
    internal static byte[] Read(string root, string relative, string sha256, int byteLength)
    {
        Require(byteLength > 0 && byteLength <= 8 * 1024 * 1024 && !Path.IsPathRooted(relative) && !relative.Contains('\\') &&
            !relative.Split('/').Any(p => p is "" or "." or ".."), "Invalid Safari path/length.");
        string path = Path.GetFullPath(Path.Combine(root, relative));
        var info = new FileInfo(path);
        Require(info.Exists && info.LinkTarget is null && info.Length == byteLength, "Safari file length/path differs: " + relative);
        for (var directory = info.Directory; directory is not null; directory = directory.Parent)
            Require(directory.LinkTarget is null, "Safari symlink directory rejected.");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        Require(stream.Length == byteLength, "Safari opened length differs.");
        byte[] bytes = new byte[byteLength]; stream.ReadExactly(bytes);
        Require(stream.ReadByte() == -1 && stream.Length == byteLength && Hash(bytes) == sha256, "Safari content hash differs: " + relative);
        return bytes;
    }
    internal static T Parse<T>(byte[] bytes)
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

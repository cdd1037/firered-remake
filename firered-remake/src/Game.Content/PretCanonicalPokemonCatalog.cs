using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record CanonicalPokemonInput(string Path, int ByteLength, string GitBlobSha1, string Sha256);
public sealed record CanonicalPokemonArtifact(string Path, int ByteLength, string Sha256);
public sealed record CanonicalMoveDependency(string Profile, string SourceCommit, ImmutableArray<CanonicalPokemonArtifact> Files);
public sealed record CanonicalSourceEvolution(string Method, int SourceMethodId, string Parameter,
    int ParameterValue, SpeciesId Target, PokemonSourceRef Source);
public sealed record CanonicalSpeciesMetadata(SpeciesId Id, int NationalDexNumber, int HeightDecimeters,
    int WeightHectograms, ImmutableDictionary<string, string> OriginalFields,
    ImmutableArray<CanonicalSourceEvolution> SourceEvolutions,
    PokemonSourceRef DexSource, PokemonSourceRef LearnsetPointerSource);
public sealed record CanonicalAbilityDonor(SpeciesId Species, int SourceSpeciesId, ImmutableArray<int> Slots);
public sealed record CanonicalAbilityDefinition(string Symbol, int SourceAbilityId,
    ImmutableArray<CanonicalAbilityDonor> Donors, PokemonSourceRef Source);
public sealed record CanonicalFormStatOverride(SpeciesId Species, string Scope, SixStats BaseStats,
    PokemonSourceRef Source, PokemonSourceRef RuleSource);
public sealed record CanonicalGenerationContract(string Profile, int LevelMinimum, int LevelMaximum,
    ImmutableArray<string> PersonalityDrawOrder, ImmutableArray<string> RandomIvDrawOrder,
    string AbilitySelection, string InitialMoves, SpeciesId MaximumHpOneSpecies,
    string EvolutionProjection, ImmutableArray<PokemonSourceRef> References,
    ImmutableArray<CanonicalFormStatOverride> FormStatOverrides);
public sealed record CanonicalPokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    string SourceTree, string GameEdition, int RomRevision, ImmutableArray<CanonicalPokemonInput> Inputs,
    CanonicalMoveDependency MoveDependency, ImmutableArray<string> Exclusions,
    ImmutableArray<PokemonSpeciesDefinition> Species, ImmutableArray<CanonicalSpeciesMetadata> Metadata,
    ImmutableArray<CanonicalAbilityDefinition> Abilities, CanonicalGenerationContract GenerationContract);

/// <summary>
/// Authenticated complete source metadata, explicitly separate from campaign or battle admission.
/// Only Load can create this sealed wrapper. Exposing Catalog does not make legacy Domain gates accept it.
/// The original evolution triples are retained separately; typed Evolutions is deliberately empty.
/// </summary>
public sealed class PretCanonicalPokemonCatalog
{
    public const string Profile = "firered-canonical-species-metadata-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockRelativePath = "content/sources/canonical-pokemon/source-lock.json";
    public const string CatalogRelativePath = "content/generated/canonical-pokemon/catalog.json";
    public const string ManifestRelativePath = "content/generated/canonical-pokemon/manifest.json";
    public const string LockSha256 = "5ceceb897d1eee35e6c49f8b6ea842fc9272d6cd0a1e382a12f014068e0edee9";
    public const string CatalogSha256 = "c5ef32d4b425c9ce720b0b29305d5855e6c7f5eff8ff05cc36d2cb51584eb9bc";
    public const string ManifestSha256 = "a5f4b207efcf4936643714cd615536bc85a9968f4a89645bae9a210cdd912dd3";
    public const int CatalogByteLength = 1929796;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public CanonicalPokemonDocument Document { get; }
    public PokemonCatalog Catalog { get; }
    public ImmutableDictionary<SpeciesId, CanonicalSpeciesMetadata> Metadata { get; }
    public ImmutableDictionary<string, CanonicalAbilityDefinition> Abilities { get; }
    public CanonicalGenerationContract GenerationContract => Document.GenerationContract;
    public ImmutableArray<string> SourceHashes { get; }

    private PretCanonicalPokemonCatalog(CanonicalPokemonDocument document, PretMetronomeMoveCatalog moves)
    {
        Document = document;
        Catalog = new(SourceCommit, document.Species.ToImmutableDictionary(s => s.Id), moves.Moves);
        Metadata = document.Metadata.ToImmutableDictionary(s => s.Id);
        Abilities = document.Abilities.ToImmutableDictionary(a => a.Symbol, StringComparer.Ordinal);
        SourceHashes = ["canonical-pokemon-lock:" + LockSha256, "canonical-pokemon-catalog:" + CatalogSha256,
            "canonical-pokemon-manifest:" + ManifestSha256,
            .. document.Inputs.Select(i => "canonical-pokemon:" + i.Path + ":" + i.Sha256),
            .. moves.AdditionalSourceHashes];
    }

    /// <param name="catalogRoot">Root containing this candidate's authenticated raw/generated/lock files.</param>
    /// <param name="moveProjectRoot">Root containing the existing authenticated 354-move dependency.</param>
    public static PretCanonicalPokemonCatalog Load(string catalogRoot, string moveProjectRoot)
    {
        string root = Path.GetFullPath(catalogRoot);
        var sourceLock = Parse<SourceLock>(Read(root, LockRelativePath, LockSha256, 3185));
        Require(sourceLock.SchemaVersion == 1 && sourceLock.Repository == "pret/pokefirered" &&
            sourceLock.Commit == SourceCommit && sourceLock.Tree == SourceTree && sourceLock.Profile == Profile &&
            sourceLock.GameEdition == "FIRERED" && sourceLock.RomRevision == 0 &&
            sourceLock.CanonicalSpeciesCount == 386 && sourceLock.OfficialAbilityCount == 76 && sourceLock.Files.Length == 15,
            "Canonical source inventory differs.");
        var manifest = Parse<Manifest>(Read(root, ManifestRelativePath, ManifestSha256, 641));
        Require(manifest.SchemaVersion == 1 && manifest.Profile == Profile && manifest.SourceCommit == SourceCommit &&
            manifest.SourceTree == SourceTree && manifest.LockSha256 == LockSha256 && manifest.InputCount == 15 &&
            manifest.Counts == new Counts(386, 76, 354, 4011, 184, 15, 514) &&
            manifest.Outputs.Length == 1 && manifest.Outputs[0] == new CanonicalPokemonArtifact("catalog.json", CatalogByteLength, CatalogSha256) &&
            !manifest.CampaignAdmissionChanged && !manifest.UpstreamScriptsExecuted, "Canonical generated manifest differs.");
        var document = Parse<CanonicalPokemonDocument>(Read(root, CatalogRelativePath, CatalogSha256, CatalogByteLength));
        Require(document.SchemaVersion == 1 && document.Profile == Profile && document.SourceCommit == SourceCommit &&
            document.SourceTree == SourceTree && document.GameEdition == "FIRERED" && document.RomRevision == 0 &&
            document.Inputs.SequenceEqual(sourceLock.Files), "Canonical generated identity differs.");
        var lines = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var input in sourceLock.Files)
        {
            byte[] bytes = Read(root, "content/raw/canonical-pokemon/" + input.Path, input.Sha256, input.ByteLength);
            Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) == input.GitBlobSha1,
                "Canonical source Git blob differs: " + input.Path);
            lines.Add(input.Path, Utf8.GetString(bytes).Count(c => c == '\n') + 1);
        }
        void Reference(PokemonSourceRef reference)
        {
            Require(reference is not null && sourceLock.Files.Any(i => i.Path == reference.Path && i.Sha256 == reference.Sha256) &&
                reference.StartLine > 0 && reference.EndLine >= reference.StartLine &&
                lines.TryGetValue(reference.Path, out int count) && reference.EndLine <= count &&
                !string.IsNullOrWhiteSpace(reference.Symbol), "Canonical source reference differs.");
        }
        var dependency = document.MoveDependency;
        Require(dependency.Profile == PretMetronomeMoveCatalog.Profile && dependency.SourceCommit == SourceCommit &&
            dependency.Files.SequenceEqual(new[] {
                new CanonicalPokemonArtifact(PretMetronomeMoveCatalog.LockRelativePath, 2355, PretMetronomeMoveCatalog.LockSha256),
                new CanonicalPokemonArtifact(PretMetronomeMoveCatalog.CatalogRelativePath, 529625, PretMetronomeMoveCatalog.CatalogSha256),
                new CanonicalPokemonArtifact(PretMetronomeMoveCatalog.ManifestRelativePath, 477, PretMetronomeMoveCatalog.ManifestSha256)
            }), "Canonical move dependency identity differs.");
        // Delegate the already reviewed source metadata boundary; do not create another move parser/loader.
        var moves = PretMetronomeMoveCatalog.Load(moveProjectRoot);
        Require(document.Species.Length == 386 && document.Metadata.Length == 386 && document.Abilities.Length == 76 &&
            document.Species.Select(s => s.SourceSpeciesId).SequenceEqual(Enumerable.Range(1, 251).Concat(Enumerable.Range(277, 135))) &&
            document.Species.Select(s => s.Id).Distinct().Count() == 386 &&
            document.Metadata.Select(m => m.Id).SequenceEqual(document.Species.Select(s => s.Id)) &&
            document.Metadata.Select(m => m.NationalDexNumber).Order().SequenceEqual(Enumerable.Range(1, 386)),
            "Canonical species closure differs (internal source IDs are not National Dex numbers).");
        var species = document.Species.ToImmutableDictionary(s => s.Id);
        var abilities = document.Abilities.ToImmutableDictionary(a => a.Symbol, StringComparer.Ordinal);
        Require(document.Abilities.Select(a => a.SourceAbilityId).SequenceEqual(Enumerable.Range(1, 75).Append(77)) &&
            !abilities.ContainsKey("ABILITY_NONE") && !abilities.ContainsKey("ABILITY_CACOPHONY"), "Official ability closure differs.");
        foreach (var s in document.Species)
        {
            Reference(s.Source); Reference(s.LearnsetSource);
            Require(s.Id.Value == "pret:" + s.Source.Symbol && s.Source.Path == "src/data/pokemon/species_info.h" &&
                s.LearnsetSource.Path == "src/data/pokemon/level_up_learnsets.h" &&
                s.BaseStats.Values.All(v => v is >= 1 and <= 255) && s.EvYield.Values.All(v => v is >= 0 and <= 3) &&
                s.CatchRate is >= 1 and <= 255 && s.ExperienceYield is >= 1 and <= 255 && s.Friendship is >= 0 and <= 255 &&
                s.GenderRatio is >= 0 and <= 255 && Enum.IsDefined(s.GrowthRate) &&
                abilities.ContainsKey(s.Ability1) && (s.Ability2 == "ABILITY_NONE" || abilities.ContainsKey(s.Ability2)) &&
                !s.LevelUpMoves.IsDefaultOrEmpty && !s.Evolutions.IsDefault && s.Evolutions.Length == 0,
                "Canonical species metadata/projection differs: " + s.Id.Value);
            int previous = 0;
            foreach (var entry in s.LevelUpMoves)
            {
                Reference(entry.Source);
                Require(entry.Level >= previous && entry.Level is >= 1 and <= 100 && moves.Moves.ContainsKey(entry.Move) &&
                    entry.Source.Path == s.LearnsetSource.Path && entry.Source.Symbol == s.LearnsetSource.Symbol &&
                    entry.Source.StartLine >= s.LearnsetSource.StartLine && entry.Source.EndLine <= s.LearnsetSource.EndLine,
                    "Canonical learnset differs: " + s.Id.Value);
                previous = entry.Level;
            }
        }
        foreach (var metadata in document.Metadata)
        {
            Reference(metadata.DexSource); Reference(metadata.LearnsetPointerSource);
            Require(metadata.WeightHectograms > 0 && metadata.HeightDecimeters > 0 && metadata.OriginalFields.Count == 26 &&
                metadata.DexSource.Symbol == species[metadata.Id].Source.Symbol.Replace("SPECIES_", "NATIONAL_DEX_", StringComparison.Ordinal) &&
                metadata.LearnsetPointerSource.Symbol == species[metadata.Id].Source.Symbol && !metadata.SourceEvolutions.IsDefault,
                "Canonical dependent metadata differs: " + metadata.Id.Value);
            foreach (var evolution in metadata.SourceEvolutions)
            {
                Reference(evolution.Source);
                Require(evolution.Source.Path == "src/data/pokemon/evolution.h" &&
                    evolution.Source.Symbol == species[metadata.Id].Source.Symbol && species.ContainsKey(evolution.Target) &&
                    evolution.SourceMethodId is >= 1 and <= 15 && evolution.Method.StartsWith("EVO_", StringComparison.Ordinal) &&
                    evolution.ParameterValue >= 0, "Original evolution reference differs.");
            }
        }
        foreach (var ability in document.Abilities)
        {
            Reference(ability.Source);
            Require(ability.Source.Symbol == ability.Symbol && !ability.Donors.IsDefaultOrEmpty &&
                ability.Donors.Select(d => d.Species).Distinct().Count() == ability.Donors.Length, "Canonical ability donors differ.");
            var expected = document.Species.Where(s => s.Ability1 == ability.Symbol || s.Ability2 == ability.Symbol).ToArray();
            Require(ability.Donors.Select(d => d.Species).SequenceEqual(expected.Select(s => s.Id)), "Canonical donor coverage differs.");
            foreach (var donor in ability.Donors)
            {
                var s = species[donor.Species];
                var slots = new[] { s.Ability1, s.Ability2 }.Select((symbol, slot) => (symbol, slot))
                    .Where(p => p.symbol == ability.Symbol).Select(p => p.slot);
                Require(donor.SourceSpeciesId == s.SourceSpeciesId && donor.Slots.SequenceEqual(slots), "Canonical donor ability slots differ.");
            }
        }
        Require(document.Species.Sum(s => s.LevelUpMoves.Length) == 4011 &&
            document.Metadata.Sum(m => m.SourceEvolutions.Length) == 184 &&
            document.Metadata.SelectMany(m => m.SourceEvolutions).Select(e => e.Method).Distinct().Count() == 15 &&
            document.Abilities.Sum(a => a.Donors.Sum(d => d.Slots.Length)) == 514, "Canonical metadata inventory differs.");
        var generation = document.GenerationContract;
        Require(generation.Profile == "firered-canonical-generation-source-v1" && generation.LevelMinimum == 1 && generation.LevelMaximum == 100 &&
            generation.MaximumHpOneSpecies == new SpeciesId("pret:SPECIES_SHEDINJA") &&
            generation.PersonalityDrawOrder.SequenceEqual(new[] { "low16", "high16" }) &&
            generation.RandomIvDrawOrder.SequenceEqual(new[] { "hp-attack-defense", "speed-specialAttack-specialDefense" }) &&
            generation.References.Length == 8 && generation.FormStatOverrides.Length == 1, "Canonical generation source contract differs.");
        foreach (var reference in generation.References) Reference(reference);
        var form = generation.FormStatOverrides[0];
        Reference(form.Source); Reference(form.RuleSource);
        Require(form.Species == new SpeciesId("pret:SPECIES_DEOXYS") && form.Scope == "firered-ordinary-non-link-stat-read" &&
            form.BaseStats == new SixStats(50, 180, 20, 150, 180, 20), "FireRed Deoxys source stat override differs.");
        return new(document, moves);
    }

    // Kept self-contained so this candidate can be compiled against the unchanged Content assembly.
    // No deserialized source field can choose a hash/length/path before fixed artifact authentication.
    private static T Parse<T>(byte[] bytes)
    {
        using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 64 });
        CheckUnique(json.RootElement);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            MaxDepth = 64,
            Converters = { new ExactGrowthRateConverter() }
        };
        return JsonSerializer.Deserialize<T>(bytes, options) ?? throw new InvalidDataException("Null canonical document.");
    }

    private static void CheckUnique(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                Require(names.Add(property.Name), "Duplicate canonical JSON property: " + property.Name);
                CheckUnique(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) CheckUnique(child);
    }

    private static byte[] Read(string root, string relative, string sha256, int byteLength)
    {
        Require(byteLength is > 0 and <= 8 * 1024 * 1024 && !Path.IsPathRooted(relative) &&
            !relative.Split('/').Any(part => part is ".." or "." or ""), "Invalid canonical source path/length.");
        string path = Path.GetFullPath(Path.Combine(root, relative));
        Require(path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Canonical path escapes root.");
        var info = new FileInfo(path);
        Require(info.Exists && info.LinkTarget is null && info.Length == byteLength, "Canonical input length/path differs: " + relative);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        Require(stream.Length == byteLength, "Canonical opened input length differs: " + relative);
        byte[] bytes = new byte[byteLength];
        stream.ReadExactly(bytes);
        Require(stream.ReadByte() == -1 && stream.Length == byteLength, "Canonical input changed during read: " + relative);
        Require(bytes.Length == byteLength && Convert.ToHexStringLower(SHA256.HashData(bytes)) == sha256, "Canonical input hash differs: " + relative);
        _ = Utf8.GetString(bytes);
        return bytes;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private sealed class ExactGrowthRateConverter : JsonConverter<GrowthRate>
    {
        public override GrowthRate Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
                foreach (var value in Enum.GetValues<GrowthRate>())
                    if (JsonNamingPolicy.CamelCase.ConvertName(value.ToString()) == reader.GetString()) return value;
            throw new JsonException("Expected exact source growth curve.");
        }
        public override void Write(Utf8JsonWriter writer, GrowthRate value, JsonSerializerOptions options) =>
            throw new NotSupportedException("Authenticated source loader is read-only.");
    }
    private sealed record SourceLock(int SchemaVersion, string Repository, string Commit, string Tree, string Profile,
        string GameEdition, int RomRevision, int CanonicalSpeciesCount, int OfficialAbilityCount, ImmutableArray<CanonicalPokemonInput> Files);
    private sealed record Counts(int Species, int OfficialAbilities, int Moves, int LevelUpEntries,
        int SourceEvolutionEntries, int SourceEvolutionMethods, int AbilityDonorSlots);
    private sealed record Manifest(int SchemaVersion, string Profile, string SourceCommit, string SourceTree,
        string LockSha256, int InputCount, Counts Counts, ImmutableArray<CanonicalPokemonArtifact> Outputs,
        bool CampaignAdmissionChanged, bool UpstreamScriptsExecuted);
}

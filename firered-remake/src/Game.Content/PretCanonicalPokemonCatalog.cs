using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record CanonicalPokemonInput(string Path);
public sealed record CanonicalPokemonArtifact(string Path);
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
public sealed record CanonicalPokemonDocument(int SchemaVersion, string Profile, string SourceCommit, string GameEdition, int RomRevision,
    ImmutableArray<CanonicalPokemonInput> Inputs, CanonicalMoveDependency MoveDependency,
    ImmutableArray<string> Exclusions, ImmutableArray<PokemonSpeciesDefinition> Species,
    ImmutableArray<CanonicalSpeciesMetadata> Metadata, ImmutableArray<CanonicalAbilityDefinition> Abilities,
    CanonicalGenerationContract GenerationContract);

/// <summary>
/// Complete source metadata, separate from campaign or battle admission.
/// Only Load can create this sealed wrapper. Exposing Catalog does not make legacy Domain gates accept it.
/// The original evolution triples are retained separately; typed Evolutions is deliberately empty.
/// </summary>
public sealed class PretCanonicalPokemonCatalog
{
    public const string Profile = "firered-canonical-species-metadata-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string LockRelativePath = "content/sources/canonical-pokemon/source-lock.json";
    public const string CatalogRelativePath = "content/generated/canonical-pokemon/catalog.json";
    public const string ManifestRelativePath = "content/generated/canonical-pokemon/manifest.json";

    public CanonicalPokemonDocument Document { get; }
    public PokemonCatalog Catalog { get; }
    public ImmutableDictionary<SpeciesId, CanonicalSpeciesMetadata> Metadata { get; }
    public ImmutableDictionary<string, CanonicalAbilityDefinition> Abilities { get; }
    public CanonicalGenerationContract GenerationContract => Document.GenerationContract;

    private PretCanonicalPokemonCatalog(CanonicalPokemonDocument document, PretMetronomeMoveCatalog moves)
    {
        Document = document;
        Catalog = new(SourceCommit, document.Species.ToImmutableDictionary(s => s.Id), moves.Moves);
        Metadata = document.Metadata.ToImmutableDictionary(s => s.Id);
        Abilities = document.Abilities.ToImmutableDictionary(a => a.Symbol, StringComparer.Ordinal);
    }

    /// <param name="catalogRoot">Root containing the generated species catalog.</param>
    /// <param name="moveProjectRoot">Root containing the generated move catalog.</param>
    public static PretCanonicalPokemonCatalog Load(string catalogRoot, string moveProjectRoot)
    {
        string root = Path.GetFullPath(catalogRoot);
        var document = Parse<CanonicalPokemonDocument>(Read(root, CatalogRelativePath));
        Require(document.SchemaVersion == 1, "Unsupported canonical Pokemon schema.");
        // Delegate the already reviewed source metadata boundary; do not create another move parser/loader.
        var moves = PretMetronomeMoveCatalog.Load(moveProjectRoot);
        Require(document.Metadata.Length == 386 && document.Abilities.Length == 76 &&
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
                Require(entry.Level >= previous && entry.Level is >= 1 and <= 100 && moves.Moves.ContainsKey(entry.Move) &&
                    entry.Source.Path == s.LearnsetSource.Path && entry.Source.Symbol == s.LearnsetSource.Symbol &&
                    entry.Source.StartLine >= s.LearnsetSource.StartLine && entry.Source.EndLine <= s.LearnsetSource.EndLine,
                    "Canonical learnset differs: " + s.Id.Value);
                previous = entry.Level;
            }
        }
        foreach (var metadata in document.Metadata)
        {
            Require(metadata.WeightHectograms > 0 && metadata.HeightDecimeters > 0 && metadata.OriginalFields.Count == 26 &&
                metadata.DexSource.Symbol == species[metadata.Id].Source.Symbol.Replace("SPECIES_", "NATIONAL_DEX_", StringComparison.Ordinal) &&
                metadata.LearnsetPointerSource.Symbol == species[metadata.Id].Source.Symbol && !metadata.SourceEvolutions.IsDefault,
                "Canonical dependent metadata differs: " + metadata.Id.Value);
            foreach (var evolution in metadata.SourceEvolutions)
            {
                Require(evolution.Source.Path == "src/data/pokemon/evolution.h" &&
                    evolution.Source.Symbol == species[metadata.Id].Source.Symbol && species.ContainsKey(evolution.Target) &&
                    evolution.SourceMethodId is >= 1 and <= 15 && evolution.Method.StartsWith("EVO_", StringComparison.Ordinal) &&
                    evolution.ParameterValue >= 0, "Original evolution reference differs.");
            }
        }
        foreach (var ability in document.Abilities)
        {
            Require(ability.Source.Symbol == ability.Symbol && !ability.Donors.IsDefaultOrEmpty &&
                ability.Donors.Select(d => d.Species).Distinct().Count() == ability.Donors.Length, "Canonical ability donors differ.");
            var expected = document.Species.Where(s => s.Ability1 == ability.Symbol || s.Ability2 == ability.Symbol).ToArray();
            Require(ability.Donors.Select(d => d.Species).SequenceEqual(expected.Select(s => s.Id)), "Canonical donor coverage differs.");
            foreach (var donor in ability.Donors)
            {
                var s = species[donor.Species];
                var slots = new[] { s.Ability1,s.Ability2 }.Select((symbol, slot) => (symbol, slot))
                    .Where(p => p.symbol == ability.Symbol).Select(p => p.slot);
                Require(donor.SourceSpeciesId == s.SourceSpeciesId && donor.Slots.SequenceEqual(slots), "Canonical donor ability slots differ.");
            }
        }
        var generation = document.GenerationContract;
        Require(generation.LevelMinimum == 1 && generation.LevelMaximum == 100 &&
            generation.MaximumHpOneSpecies == new SpeciesId("pret:SPECIES_SHEDINJA") &&
            generation.PersonalityDrawOrder.SequenceEqual(new[] { "low16","high16" }) &&
            generation.RandomIvDrawOrder.SequenceEqual(new[] { "hp-attack-defense","speed-specialAttack-specialDefense" }) &&
            generation.References.Length == 8 && generation.FormStatOverrides.Length == 1, "Canonical generation source contract differs.");

        var form = generation.FormStatOverrides[0];
        Require(form.Species == new SpeciesId("pret:SPECIES_DEOXYS") && form.Scope == "firered-ordinary-non-link-stat-read" &&
            form.BaseStats == new SixStats(50, 180, 20, 150, 180, 20), "FireRed Deoxys source stat override differs.");
        return new(document, moves);
    }

    private static T Parse<T>(byte[] bytes)
    {
        using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 64 });
        CheckUnique(json.RootElement);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,PropertyNameCaseInsensitive = false,UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,RespectNullableAnnotations = true,RespectRequiredConstructorParameters = true,MaxDepth = 64,Converters = { new ExactGrowthRateConverter() }
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

    private static byte[] Read(string root, string relative)
    {
        Require(!Path.IsPathRooted(relative) && !relative.Split('/').Any(part => part is ".." or "." or ""),
            "Invalid canonical source path.");
        return File.ReadAllBytes(Path.Combine(root, relative));
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
}

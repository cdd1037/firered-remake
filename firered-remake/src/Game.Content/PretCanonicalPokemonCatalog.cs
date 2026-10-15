using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record CanonicalSourceEvolution(string Method, int SourceMethodId, string Parameter,
    int ParameterValue, SpeciesId Target);
public sealed record CanonicalSpeciesMetadata(SpeciesId Id, int NationalDexNumber, int HeightDecimeters,
    int WeightHectograms, ImmutableArray<string> EggGroups, int EggCycles, string ItemCommon, string ItemRare,
    int SafariZoneFleeRate, string BodyColor, bool NoFlip, ImmutableArray<CanonicalSourceEvolution> SourceEvolutions);
public sealed record CanonicalAbilityDefinition(string Symbol, int SourceAbilityId);
public sealed record CanonicalFormStatOverride(SpeciesId Species, string Scope, SixStats BaseStats);
public sealed record CanonicalGenerationContract(SpeciesId MaximumHpOneSpecies,
    ImmutableArray<CanonicalFormStatOverride> FormStatOverrides);
public sealed record CanonicalPokemonDocument(int SchemaVersion,
    ImmutableArray<PokemonSpeciesDefinition> Species, ImmutableArray<CanonicalSpeciesMetadata> Metadata,
    ImmutableArray<CanonicalAbilityDefinition> Abilities, CanonicalGenerationContract GenerationContract);

/// <summary>
/// Authoritative species, move and generation data, separate from regional gameplay eligibility.
/// Regions select species and explicitly project the evolutions they support.
/// </summary>
public sealed class PretCanonicalPokemonCatalog
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string CatalogRelativePath = "content/generated/data/pokemon/canonical/catalog.json";

    public CanonicalPokemonDocument Document { get; }
    public PokemonCatalog Catalog { get; }
    public ImmutableDictionary<SpeciesId, CanonicalSpeciesMetadata> Metadata { get; }
    public ImmutableDictionary<string, CanonicalAbilityDefinition> Abilities { get; }
    public CanonicalGenerationContract GenerationContract => Document.GenerationContract;
    public PretMetronomeMoveCatalog MoveCatalog { get; }

    private PretCanonicalPokemonCatalog(CanonicalPokemonDocument document, PretMetronomeMoveCatalog moves)
    {
        Document = document;
        MoveCatalog = moves;
        Catalog = new(SourceCommit, document.Species.ToImmutableDictionary(s => s.Id), moves.Moves);
        PokemonFactory.ValidateDefinitions(Catalog);
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
            Require(s.Id.Value.StartsWith("pret:SPECIES_", StringComparison.Ordinal) &&
                s.BaseStats.Values.All(v => v is >= 1 and <= 255) && s.EvYield.Values.All(v => v is >= 0 and <= 3) &&
                s.CatchRate is >= 1 and <= 255 && s.ExperienceYield is >= 1 and <= 255 && s.Friendship is >= 0 and <= 255 &&
                s.GenderRatio is >= 0 and <= 255 && Enum.IsDefined(s.GrowthRate) &&
                abilities.ContainsKey(s.Ability1) && (s.Ability2 == "ABILITY_NONE" || abilities.ContainsKey(s.Ability2)) &&
                !s.LevelUpMoves.IsDefaultOrEmpty && !s.Evolutions.IsDefault && s.Evolutions.Length == 0,
                "Canonical species metadata/projection differs: " + s.Id.Value);
            int previous = 0;
            foreach (var entry in s.LevelUpMoves)
            {
                Require(entry.Level >= previous && entry.Level is >= 1 and <= 100 && moves.Moves.ContainsKey(entry.Move),
                    "Canonical learnset differs: " + s.Id.Value);
                previous = entry.Level;
            }
        }
        foreach (var metadata in document.Metadata)
        {
            Require(metadata.WeightHectograms > 0 && metadata.HeightDecimeters > 0 &&
                metadata.EggGroups.Length == 2 && metadata.EggGroups.All(g => g.StartsWith("EGG_GROUP_", StringComparison.Ordinal)) &&
                metadata.EggCycles is >= 0 and <= 255 && metadata.SafariZoneFleeRate is >= 0 and <= 255 &&
                metadata.ItemCommon.StartsWith("ITEM_", StringComparison.Ordinal) && metadata.ItemRare.StartsWith("ITEM_", StringComparison.Ordinal) &&
                metadata.BodyColor.StartsWith("BODY_COLOR_", StringComparison.Ordinal) && !metadata.SourceEvolutions.IsDefault,
                "Canonical dependent metadata differs: " + metadata.Id.Value);
            foreach (var evolution in metadata.SourceEvolutions)
            {
                Require(species.ContainsKey(evolution.Target) &&
                    evolution.SourceMethodId is >= 1 and <= 15 && evolution.Method.StartsWith("EVO_", StringComparison.Ordinal) &&
                    evolution.ParameterValue >= 0, "Original evolution reference differs.");
            }
        }
        var generation = document.GenerationContract;
        Require(generation.MaximumHpOneSpecies == new SpeciesId("pret:SPECIES_SHEDINJA") &&
            generation.FormStatOverrides.Length == 1, "Canonical generation rules differ.");

        var form = generation.FormStatOverrides[0];
        Require(form.Species == new SpeciesId("pret:SPECIES_DEOXYS") && form.Scope == "firered-ordinary-non-link-stat-read" &&
            form.BaseStats == new SixStats(50, 180, 20, 150, 180, 20), "FireRed Deoxys source stat override differs.");
        return new(document, moves);
    }

    public JsonSerializerOptions ReferenceOptions(JsonSerializerOptions? original = null)
    {
        var options = original is null ? ContentJson.CreateOptions() : new JsonSerializerOptions(original);
        AddReferenceConverters(options);
        return options;
    }

    public void AddReferenceConverters(JsonSerializerOptions options)
    {
        var references = ContentJson.CreateOptions();
        references.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        options.Converters.Add(new SpeciesReferenceConverter(this, references));
        options.Converters.Add(new MoveReferenceConverter(this, references));
    }

    public PokemonSpeciesDefinition Project(SpeciesId id, ImmutableArray<PokemonEvolution> evolutions)
    {
        if (!Catalog.Species.TryGetValue(id, out var species) || evolutions.IsDefault)
            throw new JsonException("Unknown species or missing regional evolution projection: " + id.Value);
        foreach (var evolution in evolutions)
        {
            if (evolution is null || !Metadata[id].SourceEvolutions.Any(source =>
                source.Target == evolution.Target && EvolutionMatches(evolution, source)))
                throw new JsonException("Regional evolution is not a canonical evolution: " + id.Value);
        }
        if (evolutions.Select(e => (e.Method, e.Level, e.Parameter, e.Target)).Distinct().Count() != evolutions.Length)
            throw new JsonException("Duplicate regional evolution: " + id.Value);
        return evolutions.IsEmpty ? species : species with { Evolutions = evolutions };
    }

    private static bool EvolutionMatches(PokemonEvolution evolution, CanonicalSourceEvolution source) =>
        evolution.Method switch
        {
            PokemonEvolutionMethod.Level => source.Method == "EVO_LEVEL" && evolution.Level == source.ParameterValue && evolution.Parameter is null,
            PokemonEvolutionMethod.Item => source.Method == "EVO_ITEM" && evolution.Level == 0 && evolution.Parameter == source.Parameter,
            PokemonEvolutionMethod.Trade => source.Method == "EVO_TRADE" && evolution.Level == 0 && evolution.Parameter is null,
            PokemonEvolutionMethod.TradeItem => source.Method == "EVO_TRADE_ITEM" && evolution.Level == 0 && evolution.Parameter == source.Parameter,
            PokemonEvolutionMethod.Friendship => source.Method == "EVO_FRIENDSHIP" && evolution.Level == 0,
            PokemonEvolutionMethod.LevelAttackGreaterDefense => source.Method == "EVO_LEVEL_ATK_GT_DEF" && evolution.Level == source.ParameterValue,
            PokemonEvolutionMethod.LevelAttackEqualDefense => source.Method == "EVO_LEVEL_ATK_EQ_DEF" && evolution.Level == source.ParameterValue,
            PokemonEvolutionMethod.LevelAttackLessDefense => source.Method == "EVO_LEVEL_ATK_LT_DEF" && evolution.Level == source.ParameterValue,
            _ => false
        };

    private sealed record SpeciesReference(SpeciesId Id, ImmutableArray<PokemonEvolution> Evolutions);
    private sealed class SpeciesReferenceConverter(PretCanonicalPokemonCatalog catalog, JsonSerializerOptions references)
        : JsonConverter<PokemonSpeciesDefinition>
    {
        public override PokemonSpeciesDefinition Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            var reference = JsonSerializer.Deserialize<SpeciesReference>(ref reader, references)
                ?? throw new JsonException("Missing species reference.");
            return catalog.Project(reference.Id, reference.Evolutions);
        }
        public override void Write(Utf8JsonWriter writer, PokemonSpeciesDefinition value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, new SpeciesReference(value.Id, value.Evolutions), references);
    }
    private sealed class MoveReferenceConverter(PretCanonicalPokemonCatalog catalog, JsonSerializerOptions references)
        : JsonConverter<PokemonMoveDefinition>
    {
        public override PokemonMoveDefinition Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            var id = JsonSerializer.Deserialize<MoveId>(ref reader, references);
            return catalog.Catalog.Moves.TryGetValue(id, out var move) ? move
                : throw new JsonException("Unknown move reference: " + id.Value);
        }
        public override void Write(Utf8JsonWriter writer, PokemonMoveDefinition value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.Id, references);
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

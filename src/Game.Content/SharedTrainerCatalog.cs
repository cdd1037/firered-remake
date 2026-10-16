using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record SharedTrainerDocument(ImmutableArray<TrainerDefinition> Trainers);

/// <summary>Immutable trainer definitions. Regional membership and battle occurrences remain separate.</summary>
public sealed class SharedTrainerCatalog
{
    public const string RelativePath = "content/generated/data/trainers.json";
    private static readonly ConcurrentDictionary<string, Lazy<SharedTrainerCatalog>> Projects = new(StringComparer.Ordinal);
    private readonly ImmutableDictionary<TrainerId, TrainerDefinition> _trainers;

    private SharedTrainerCatalog(SharedTrainerDocument document)
    {
        if (document.Trainers.IsDefaultOrEmpty || document.Trainers.Any(trainer => trainer is null) ||
            document.Trainers.Select(trainer => trainer.Id).Distinct().Count() != document.Trainers.Length ||
            document.Trainers.Select(trainer => trainer.SourceTrainerId).Distinct().Count() != document.Trainers.Length)
            throw new InvalidDataException("trainer_catalog_shape");
        foreach (var trainer in document.Trainers) Validate(trainer);
        _trainers = document.Trainers.ToImmutableDictionary(trainer => trainer.Id);
    }

    public static SharedTrainerCatalog LoadProject(string root) => Projects.GetOrAdd(Path.GetFullPath(root), path =>
        new Lazy<SharedTrainerCatalog>(() => Load(File.ReadAllText(Path.Combine(path, RelativePath))))).Value;

    public static SharedTrainerCatalog Load(string json) => new(
        ContentJson.ParseStrict<SharedTrainerDocument>(json, ContentJson.CreateOptions()));

    public TrainerDefinition Definition(TrainerId id) => _trainers.TryGetValue(id, out var trainer)
        ? trainer : throw new InvalidDataException("trainer_definition_missing: " + id.Value);

    public ImmutableDictionary<TrainerId, TrainerDefinition> Select(IEnumerable<TrainerId> ids) =>
        ids.Distinct().ToImmutableDictionary(id => id, Definition);

    public JsonSerializerOptions CreateJsonOptions(JsonSerializerOptions? original = null)
    {
        var options = ReferenceOptions(original);
        AddConverters(options);
        return options;
    }

    public static JsonSerializerOptions Options(string root, JsonSerializerOptions? original = null)
    {
        var options = ReferenceOptions(original);
        AddConverters(options, root);
        return options;
    }

    private static JsonSerializerOptions ReferenceOptions(JsonSerializerOptions? original) => new(
        original ?? ContentJson.CreateOptions())
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 64
    };

    public void AddConverters(JsonSerializerOptions options) => options.Converters.Add(new TrainerReferenceConverter(() => this));
    public static void AddConverters(JsonSerializerOptions options, string root)
    {
        // Capture the explicit dependency root. Unrelated documents do not force catalog IO.
        var path = Path.GetFullPath(root);
        options.Converters.Add(new TrainerReferenceConverter(() => LoadProject(path)));
    }

    private static void Validate(TrainerDefinition trainer)
    {
        if (!Symbol(trainer.Id.Value, "pret:TRAINER_") || trainer.SourceTrainerId <= 0 ||
            !Symbol(trainer.TrainerClass, "TRAINER_CLASS_") || trainer.SourceTrainerClass < 0 ||
            !Symbol(trainer.TrainerPic, "TRAINER_PIC_") || trainer.SourceTrainerPic < 0 ||
            string.IsNullOrWhiteSpace(trainer.SourceTrainerName) || !Encoding(trainer.EncodedTrainerName) ||
            trainer.EncounterMusicGender is < 0 or > 255 || trainer.Items.IsDefault || trainer.Items.Length != 4 ||
            trainer.Items.Any(item => !Symbol(item, "ITEM_")) || trainer.PartyFlags is < 0 or > 3 ||
            trainer.AiFlagSymbols.IsDefault || trainer.AiFlagSymbols.Any(symbol => !Symbol(symbol, "AI_SCRIPT_")) ||
            trainer.BaseMoney < 0 || trainer.Party.IsDefaultOrEmpty || trainer.Party.Length > 6)
            throw new InvalidDataException("trainer_catalog_definition: " + trainer.Id.Value);
        foreach (var member in trainer.Party)
        {
            if (member is null || !Symbol(member.Species.Value, "pret:SPECIES_") || member.SourceSpeciesId <= 0 ||
                string.IsNullOrWhiteSpace(member.SpeciesName) || !Encoding(member.EncodedSpeciesName) ||
                member.Level is < 1 or > 100 || member.SourceIv is < 0 or > 255 || !Symbol(member.HeldItem, "ITEM_") ||
                ((trainer.PartyFlags & 2) == 0 && member.HeldItem != "ITEM_NONE") ||
                member.CustomMoves.IsDefault || member.CustomMoves.Length != ((trainer.PartyFlags & 1) == 0 ? 0 : 4) ||
                member.CustomMoves.Any(move => !Symbol(move.Value, "pret:MOVE_")))
                throw new InvalidDataException("trainer_catalog_party: " + trainer.Id.Value);
        }
    }

    private static bool Symbol(string? value, string prefix) => value is not null && value.Length > prefix.Length &&
        value.StartsWith(prefix, StringComparison.Ordinal);
    private static bool Encoding(ImmutableArray<int> bytes) => !bytes.IsDefaultOrEmpty && bytes.All(value => value is >= 0 and <= 255);

    private sealed class TrainerReferenceConverter(Func<SharedTrainerCatalog> catalog) : JsonConverter<TrainerDefinition>
    {
        public override bool HandleNull => true;
        public override TrainerDefinition Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String) throw new JsonException("A trainer reference must be a stable trainer ID.");
            return catalog().Definition(new(reader.GetString()!));
        }
        public override void Write(Utf8JsonWriter writer, TrainerDefinition trainer, JsonSerializerOptions options)
        {
            if (trainer is null) throw new JsonException("A trainer reference must not be null.");
            writer.WriteStringValue(trainer.Id.Value);
        }
    }
}

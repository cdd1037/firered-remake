using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Display-only projections of canonical trainer IDs and source class IDs.</summary>
public sealed class SourceTrainerDisplayNames
{
    public const string ClassNamesRelativePath = "content/generated/data/trainer-class-names.json";
    private readonly SharedTrainerCatalog trainers;
    private readonly ImmutableDictionary<string, string> names, classes;

    private SourceTrainerDisplayNames(SharedTrainerCatalog trainers,
        ImmutableDictionary<string, string> classes, SourceTextPack pack)
    {
        if (classes.Count == 0 || classes.Any(pair =>
                !pair.Key.StartsWith("TRAINER_CLASS_", StringComparison.Ordinal) ||
                pair.Key.Length == "TRAINER_CLASS_".Length ||
                pair.Key.Any(c => c is not (>= 'A' and <= 'Z' or '_')) ||
                string.IsNullOrWhiteSpace(pair.Value)) ||
            trainers.Definitions.Any(trainer => !classes.ContainsKey(trainer.TrainerClass)))
            throw new InvalidDataException("trainer_class_display_names_shape");
        this.trainers = trainers;
        names = pack.Apply("trainer-names", trainers.Definitions.ToImmutableDictionary(
            trainer => trainer.Id.Value, trainer => trainer.SourceTrainerName, StringComparer.Ordinal));
        this.classes = pack.Apply("trainer-class-names", classes);
    }

    public static SourceTrainerDisplayNames LoadProject(string root, SharedTrainerCatalog trainers, SourceTextPack? pack = null) =>
        Load(File.ReadAllText(Path.Combine(root, ClassNamesRelativePath)), trainers, pack);

    public static SourceTrainerDisplayNames Load(string classNamesJson, SharedTrainerCatalog trainers, SourceTextPack? pack = null) =>
        new(trainers, ContentJson.ParseStrict<ImmutableDictionary<string, string>>(classNamesJson,
            ContentJson.CreateOptions()), pack ?? SourceTextPack.Current);

    public string Name(TrainerId id) => names.TryGetValue(id.Value, out var name)
        ? name : throw new InvalidDataException("trainer_display_name_missing: " + id.Value);
    public string ClassName(string id) => classes.TryGetValue(id, out var name)
        ? name : throw new InvalidDataException("trainer_class_display_name_missing: " + id);
    public string ClassName(TrainerId id) => ClassName(trainers.Definition(id).TrainerClass);
}

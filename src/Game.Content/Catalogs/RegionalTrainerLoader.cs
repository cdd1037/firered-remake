using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Content;

public abstract record RegionalTrainerDocument(ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<CeruleanTrainerBattleDefinition> Battles)
{
    private ImmutableDictionary<TrainerId, TrainerDefinition> Membership(string emptyError)
    {
        if (Trainers.IsDefaultOrEmpty || Battles.IsDefaultOrEmpty)
            throw new InvalidDataException(emptyError);
        return Trainers.Distinct().ToImmutableDictionary(t => t.Id);
    }

    public T ByScript<T>(Func<ImmutableDictionary<TrainerId, TrainerDefinition>,
        ImmutableDictionary<string, CeruleanTrainerBattleDefinition>, T> create, string emptyError) =>
        create(Membership(emptyError), Battles.Distinct().ToImmutableDictionary(b => b.ScriptEntry));

    public T ByTrainer<T>(Func<ImmutableDictionary<TrainerId, TrainerDefinition>,
        ImmutableDictionary<TrainerId, CeruleanTrainerBattleDefinition>, T> create, string emptyError) =>
        create(Membership(emptyError), Battles.Distinct().ToImmutableDictionary(b => b.Trainer));
}

/// <summary>Resolve shared definitions and validate the constructed catalog once per load.</summary>
public sealed class RegionalTrainerLoader<TDocument, TCatalog>(string relativePath, string missingError,
    Func<TDocument, TCatalog> create, Action<TCatalog>? validate = null)
    where TDocument : RegionalTrainerDocument
{
    private (TDocument Document, TCatalog Catalog) Load(string root, JsonSerializerOptions? options)
    {
        string path = Path.Combine(root, relativePath);
        if (!File.Exists(path)) throw new FileNotFoundException(missingError, path);
        var jsonOptions = SharedTrainerCatalog.Options(root, options);
        jsonOptions.AllowDuplicateProperties = false;
        var document = JsonSerializer.Deserialize<TDocument>(File.ReadAllBytes(path), jsonOptions)
            ?? throw new InvalidDataException(missingError);
        var catalog = create(document);
        validate?.Invoke(catalog);
        return (document, catalog);
    }

    public TDocument LoadDocument(string root, JsonSerializerOptions? options = null) => Load(root, options).Document;
    public TCatalog LoadCatalog(string root, JsonSerializerOptions? options = null) => Load(root, options).Catalog;
}

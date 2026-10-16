using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record ForestTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<ForestTrainerBattleDefinition> Battles);

/// <summary>Loads compiled trainer parties and battle entries without reading source inventories.</summary>
public static class PretForestTrainers
{
    public const string RelativePath = "content/generated/data/trainer-battles/forest.json";

    public static ForestTrainerCatalog Load(string root)
    {
        var document = ContentJson.ParseStrict<ForestTrainerDocument>(
            File.ReadAllText(Path.Combine(root, RelativePath)), SharedTrainerCatalog.Options(root));
        if (document.SchemaVersion != 1) throw new InvalidDataException("Unsupported Forest trainer document schema.");
        var catalog = new ForestTrainerCatalog(document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.Trainer));
        ForestTrainerPartyFactory.ValidateCatalog(catalog);
        return catalog;
    }

    public static ForestTrainerCatalog Load(PretForestRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return Load(PretRuntimeMapValidation.ProjectRoot(region.GeneratedDirectory));
    }
}

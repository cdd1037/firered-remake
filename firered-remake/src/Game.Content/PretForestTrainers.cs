using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record ForestTrainerDocument(int SchemaVersion, string SourceCommit, string Profile,
    bool CompleteSourceRoster, ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<ForestTrainerBattleDefinition> Battles);

/// <summary>Loads compiled trainer parties and battle entries without reading source inventories.</summary>
public static class PretForestTrainers
{
    public const string RelativePath = "content/generated/forest-trainers/trainers.json";

    public static ForestTrainerCatalog Load(string root)
    {
        var document = ContentJson.ParseStrict<ForestTrainerDocument>(
            File.ReadAllText(Path.Combine(root, RelativePath)), ContentJson.CreateOptions());
        if (document.SchemaVersion != 1 || document.CompleteSourceRoster || document.Trainers.IsDefault ||
            document.Battles.IsDefault || document.Trainers.Length != 5 || document.Battles.Length != 5)
            throw new InvalidDataException("Forest trainer catalog scope differs.");
        var catalog = new ForestTrainerCatalog(document.SourceCommit, document.Profile, false,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.Trainer));
        ForestTrainerPartyFactory.ValidateCatalog(catalog);
        return catalog;
    }

    public static ForestTrainerCatalog Load(PretForestRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return Load(Path.GetFullPath(Path.Combine(region.GeneratedDirectory, "../../..")));
    }
}

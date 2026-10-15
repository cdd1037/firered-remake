using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record Route3TrainerDocument(int SchemaVersion, string SourceCommit, string Profile,
    bool CompleteSourceRoster, ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<ForestTrainerBattleDefinition> Battles);

/// <summary>Loads compiled trainer parties and battle entries without reading source inventories.</summary>
public static class PretRoute3Trainers
{
    public const string RelativePath = "content/generated/route3-trainers/trainers.json";

    public static Route3TrainerCatalog Load(string root)
    {
        var document = ContentJson.ParseStrict<Route3TrainerDocument>(
            File.ReadAllText(Path.Combine(root, RelativePath)), ContentJson.CreateOptions());
        if (document.SchemaVersion != 1 || document.CompleteSourceRoster || document.Trainers.IsDefault ||
            document.Battles.IsDefault || document.Trainers.Length != 8 || document.Battles.Length != 8)
            throw new InvalidDataException("Route3 trainer catalog scope differs.");
        var catalog = new Route3TrainerCatalog(document.SourceCommit, document.Profile, false,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.Trainer));
        Route3TrainerPartyFactory.ValidateCatalog(catalog);
        return catalog;
    }

    public static Route3TrainerCatalog Load(PretRoute3Region region, PretRoute3Pokemon pokemon)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(pokemon);
        return Load(Path.GetFullPath(Path.Combine(region.GeneratedDirectory, "../../..")));
    }
}

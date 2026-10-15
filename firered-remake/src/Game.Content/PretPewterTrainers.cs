using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record PewterTrainerDocument(int SchemaVersion, string SourceCommit, string Profile,
    bool CompleteSourceRoster, ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<PewterTrainerBattleDefinition> Battles);

/// <summary>Loads compiled trainer parties and battle entries without reading source inventories.</summary>
public static class PretPewterTrainers
{
    public const string RelativePath = "content/generated/pewter-trainers/trainers.json";

    public static PewterTrainerCatalog Load(string root)
    {
        var document = ContentJson.ParseStrict<PewterTrainerDocument>(
            File.ReadAllText(Path.Combine(root, RelativePath)), ContentJson.CreateOptions());
        if (document.SchemaVersion != 1 || document.CompleteSourceRoster || document.Trainers.IsDefault ||
            document.Battles.IsDefault || document.Trainers.Length != 2 || document.Battles.Length != 2)
            throw new InvalidDataException("Pewter trainer catalog scope differs.");
        var catalog = new PewterTrainerCatalog(document.SourceCommit, document.Profile, false,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.Trainer));
        PewterTrainerPartyFactory.ValidateCatalog(catalog);
        return catalog;
    }

    public static PewterTrainerCatalog Load(PretPewterRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return Load(Path.GetFullPath(Path.Combine(region.GeneratedDirectory, "../../..")));
    }
}

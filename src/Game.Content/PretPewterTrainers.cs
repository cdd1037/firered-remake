using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record PewterTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<PewterTrainerBattleDefinition> Battles);

/// <summary>Loads compiled trainer parties and battle entries without reading source inventories.</summary>
public static class PretPewterTrainers
{
    public const string RelativePath = "content/generated/data/trainer-battles/pewter.json";

    public static PewterTrainerCatalog Load(string root)
    {
        var document = ContentJson.ParseStrict<PewterTrainerDocument>(
            File.ReadAllText(Path.Combine(root, RelativePath)), SharedTrainerCatalog.Options(root));
        if (document.SchemaVersion != 1) throw new InvalidDataException("Unsupported Pewter trainer document schema.");
        var catalog = new PewterTrainerCatalog(document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.Trainer));
        PewterTrainerPartyFactory.ValidateCatalog(catalog);
        return catalog;
    }

    public static PewterTrainerCatalog Load(PretPewterRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return Load(PretRuntimeMapValidation.ProjectRoot(region.GeneratedDirectory));
    }
}

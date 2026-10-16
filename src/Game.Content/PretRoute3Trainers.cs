using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record Route3TrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<ForestTrainerBattleDefinition> Battles);

/// <summary>Loads compiled trainer parties and battle entries without reading source inventories.</summary>
public static class PretRoute3Trainers
{
    public const string RelativePath = "content/generated/data/trainer-battles/route3.json";

    public static Route3TrainerCatalog Load(string root)
    {
        var document = ContentJson.ParseStrict<Route3TrainerDocument>(
            File.ReadAllText(Path.Combine(root, RelativePath)), SharedTrainerCatalog.Options(root));
        if (document.SchemaVersion != 1) throw new InvalidDataException("Unsupported Route3 trainer document schema.");
        var catalog = new Route3TrainerCatalog(document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.Trainer));
        Route3TrainerPartyFactory.ValidateCatalog(catalog);
        return catalog;
    }

    public static Route3TrainerCatalog Load(PretRoute3Region region, PretRoute3Pokemon pokemon)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(pokemon);
        return Load(PretRuntimeMapValidation.ProjectRoot(region.GeneratedDirectory));
    }
}

using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record Route22TrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<Route22TrainerBattleDefinition> Battles);

/// <summary>Loads compiled trainer parties and battle entries without reading source inventories.</summary>
public static class PretRoute22Trainers
{
    public const string RelativePath = "content/generated/data/trainer-battles/route22.json";

    public static Route22TrainerCatalog Load(string root)
    {
        var document = ContentJson.ParseStrict<Route22TrainerDocument>(
            File.ReadAllText(Path.Combine(root, RelativePath)), SharedTrainerCatalog.Options(root));
        if (document.SchemaVersion != 1) throw new InvalidDataException("Unsupported Route22 trainer document schema.");
        var catalog = new Route22TrainerCatalog(document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.Trainer));
        Route22TrainerPartyFactory.ValidateCatalog(catalog);
        return catalog;
    }

    public static Route22TrainerCatalog Load(PretRoute22Region region, PokemonPresentationPackage pokemon)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(pokemon);
        return Load(PretRuntimeMapValidation.ProjectRoot(region.GeneratedDirectory));
    }
}

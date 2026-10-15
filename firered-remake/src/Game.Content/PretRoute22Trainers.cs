using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record Route22TrainerDocument(int SchemaVersion, string SourceCommit, string Profile,
    bool CompleteSourceRoster, ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<Route22TrainerBattleDefinition> Battles);

/// <summary>Loads compiled trainer parties and battle entries without reading source inventories.</summary>
public static class PretRoute22Trainers
{
    public const string RelativePath = "content/generated/route22-trainers/trainers.json";

    public static Route22TrainerCatalog Load(string root)
    {
        var document = ContentJson.ParseStrict<Route22TrainerDocument>(
            File.ReadAllText(Path.Combine(root, RelativePath)), ContentJson.CreateOptions());
        if (document.SchemaVersion != 1 || document.CompleteSourceRoster || document.Trainers.IsDefault ||
            document.Battles.IsDefault || document.Trainers.Length != 3 || document.Battles.Length != 3)
            throw new InvalidDataException("Route22 trainer catalog scope differs.");
        var catalog = new Route22TrainerCatalog(document.SourceCommit, document.Profile, false,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.Trainer));
        Route22TrainerPartyFactory.ValidateCatalog(catalog);
        return catalog;
    }

    public static Route22TrainerCatalog Load(PretRoute22Region region, PretRoute22Pokemon pokemon)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(pokemon);
        return Load(Path.GetFullPath(Path.Combine(region.GeneratedDirectory, "../../..")));
    }
}

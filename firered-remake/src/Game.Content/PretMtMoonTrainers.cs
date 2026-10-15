using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record MtMoonTrainerDocument(int SchemaVersion, string SourceCommit, string Profile,
    bool CompleteSourceRoster, ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<ForestTrainerBattleDefinition> Battles);

/// <summary>Loads compiled trainer parties and battle entries without reading source inventories.</summary>
public static class PretMtMoonTrainers
{
    public const string RelativePath = "content/generated/mtmoon-trainers/trainers.json";

    public static MtMoonTrainerCatalog Load(string root)
    {
        var document = ContentJson.ParseStrict<MtMoonTrainerDocument>(
            File.ReadAllText(Path.Combine(root, RelativePath)), ContentJson.CreateOptions());
        if (document.SchemaVersion != 1 || document.CompleteSourceRoster || document.Trainers.IsDefault ||
            document.Battles.IsDefault || document.Trainers.Length != 12 || document.Battles.Length != 12)
            throw new InvalidDataException("MtMoon trainer catalog scope differs.");
        var catalog = new MtMoonTrainerCatalog(document.SourceCommit, document.Profile, false,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.Trainer));
        MtMoonTrainerPartyFactory.ValidateCatalog(catalog);
        return catalog;
    }

    public static MtMoonTrainerCatalog Load(PretMtMoonRegion region, PretMtMoonPokemon pokemon)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(pokemon);
        return Load(Path.GetFullPath(Path.Combine(region.GeneratedDirectory, "../../..")));
    }
}

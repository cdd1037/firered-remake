using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record MtMoonTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<ForestTrainerBattleDefinition> Battles);

/// <summary>Loads compiled trainer parties and battle entries without reading source inventories.</summary>
public static class PretMtMoonTrainers
{
    public const string RelativePath = "content/generated/data/trainer-battles/mtmoon.json";

    public static MtMoonTrainerCatalog Load(string root)
    {
        var document = ContentJson.ParseStrict<MtMoonTrainerDocument>(
            File.ReadAllText(Path.Combine(root, RelativePath)), SharedTrainerCatalog.Options(root));
        if (document.SchemaVersion != 1) throw new InvalidDataException("Unsupported MtMoon trainer document schema.");
        var catalog = new MtMoonTrainerCatalog(document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.Trainer));
        MtMoonTrainerPartyFactory.ValidateCatalog(catalog);
        return catalog;
    }

    public static MtMoonTrainerCatalog Load(PretMtMoonRegion region, PokemonPresentationPackage pokemon)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(pokemon);
        return Load(PretRuntimeMapValidation.ProjectRoot(region.GeneratedDirectory));
    }
}

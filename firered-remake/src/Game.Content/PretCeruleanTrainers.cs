using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record CeruleanTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<CeruleanTrainerBattleDefinition> Battles);

/// <summary>Generated parties and battle entries; English name bytes feed the NPC personality calculation.</summary>
public static class PretCeruleanTrainers
{
    public const string RelativePath = "content/generated/data/trainer-battles/cerulean/trainers.json";
    public static CeruleanTrainerCatalog Load(string root)
    {
        var document = ContentJson.ParseStrict<CeruleanTrainerDocument>(
            File.ReadAllText(Path.Combine(root, RelativePath)), SharedTrainerCatalog.Options(root));
        if (document.SchemaVersion != 1) throw new InvalidDataException("Unsupported Cerulean trainer document schema.");
        var catalog = new CeruleanTrainerCatalog(document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.Trainer));
        CeruleanTrainerPartyFactory.ValidateCatalog(catalog);
        return catalog;
    }

    public static CeruleanTrainerCatalog Load(PretCeruleanRegion region, PretCeruleanPokemon pokemon)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(pokemon);
        return Load(PretRuntimeMapValidation.ProjectRoot(region.GeneratedDirectory));
    }

    internal static byte[] ReadPinned(string root, string path)
    {
        if (Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Cerulean trainer path.");
        return File.ReadAllBytes(Path.Combine(root, path));
    }
}

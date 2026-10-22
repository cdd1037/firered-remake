using System.Collections.Immutable;
using System.Text;
using Game.Domain.Trainers;
namespace Game.Content;

public sealed record SsAnneTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles);

/// <summary>Loads compiled ordinary trainer parties and battle entries.</summary>
public static class PretSsAnneTrainers
{
    public static OrdinaryTrainerCatalog Load(string root)
    {
        byte[] Read(string path)
        {
            return File.ReadAllBytes(GeneratedResourceFile.SafePath(root, path));
        }
        var doc=ContentJson.ParseStrict<SsAnneTrainerDocument>(Encoding.UTF8.GetString(Read("content/generated/data/trainer-battles/ssanne.json")),SharedTrainerCatalog.Options(root));
                if(doc.SchemaVersion!=1) throw new InvalidDataException("SsAnne trainer document identity differs.");
        var catalog = new OrdinaryTrainerCatalog(doc.Trainers.ToImmutableDictionary(t => t.Id), doc.Battles, "ssanne-trainer");
        return catalog;
    }
}

using System.Collections.Immutable;
using System.Text;
using Game.Domain.Trainers;
namespace Game.Content;

public sealed record VermilionTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles);

/// <summary>Loads compiled ordinary trainer parties and battle entries.</summary>
public static class PretVermilionTrainers
{
    public static OrdinaryTrainerCatalog Load(string root)
    {
        byte[] Read(string path)
        {
            return File.ReadAllBytes(GeneratedResourceFile.SafePath(root, path));
        }
        var doc=ContentJson.ParseStrict<VermilionTrainerDocument>(Encoding.UTF8.GetString(Read("content/generated/data/trainer-battles/vermilion.json")),SharedTrainerCatalog.Options(root));
                if(doc.SchemaVersion!=1) throw new InvalidDataException("Vermilion trainer document identity differs.");
        return new(doc.Trainers.ToImmutableDictionary(t => t.Id), doc.Battles, "vermilion-trainer");
    }
}

using System.Collections.Immutable;
using System.Text;
using Game.Domain.Trainers;
namespace Game.Content;

public sealed record RockTunnelTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles);

/// <summary>Regional trainer references and initial battle entries.</summary>
public static class PretRockTunnelTrainers
{
    public static OrdinaryTrainerCatalog Load(string root)
    {
        byte[] Read(string path)
        {
            return File.ReadAllBytes(GeneratedResourceFile.SafePath(root, path));
        }
        var doc=ContentJson.ParseStrict<RockTunnelTrainerDocument>(Encoding.UTF8.GetString(Read("content/generated/data/trainer-battles/rocktunnel.json")),SharedTrainerCatalog.Options(root));
                if(doc.SchemaVersion!=1) throw new InvalidDataException("RockTunnel trainer document identity differs.");
        return new(doc.Trainers.ToImmutableDictionary(t => t.Id), doc.Battles, "rocktunnel-trainer");
    }
}

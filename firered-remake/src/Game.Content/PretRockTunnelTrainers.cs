using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Trainers;
namespace Game.Content;

public sealed record RockTunnelTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles);

/// <summary>Regional trainer references and initial battle entries.</summary>
public static class PretRockTunnelTrainers
{
    public static RockTunnelTrainerCatalog Load(string root)
    {
        byte[] Read(string path)
        {
            return File.ReadAllBytes(SafeFile(root, path));
        }
        var doc=ContentJson.ParseStrict<RockTunnelTrainerDocument>(Encoding.UTF8.GetString(Read("content/generated/data/trainer-battles/rocktunnel.json")),SharedTrainerCatalog.Options(root));
                if(doc.SchemaVersion!=1) throw new InvalidDataException("RockTunnel trainer document identity differs.");
        var catalog=new RockTunnelTrainerCatalog(doc.Trainers.ToImmutableDictionary(t=>t.Id),doc.Battles.ToImmutableDictionary(t=>t.Trainer));
        RockTunnelTrainerPartyFactory.ValidateCatalog(catalog);
        return catalog;
    }
    private static string SafeFile(string root,string relative)
    {
        if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or "..")) throw new InvalidDataException("Unsafe RockTunnel trainer path.");
        var f=new FileInfo(Path.GetFullPath(Path.Combine(root,relative)));
        if(f.LinkTarget is not null) throw new InvalidDataException("RockTunnel trainer symlink rejected.");
        for(var d=f.Directory;d is not null;d=d.Parent) if(d.LinkTarget is not null) throw new InvalidDataException("RockTunnel trainer parent symlink rejected.");
        return f.FullName;
    }
}

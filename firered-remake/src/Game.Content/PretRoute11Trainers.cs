using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Trainers;
namespace Game.Content;

public sealed record Route11TrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles);

/// <summary>Hash-locked source tables and initial battle scripts, independent from all earlier catalogs.</summary>
public static class PretRoute11Trainers
{
    public static Route11TrainerCatalog Load(string root)
    {
        byte[] Read(string path)
        {
            return File.ReadAllBytes(SafeFile(root, path));
        }
        var doc=ContentJson.ParseStrict<Route11TrainerDocument>(Encoding.UTF8.GetString(Read("content/generated/data/trainer-battles/route11.json")),SharedTrainerCatalog.Options(root));
                if(doc.SchemaVersion!=1) throw new InvalidDataException("Route11 trainer document identity differs.");
        var catalog=new Route11TrainerCatalog(doc.Trainers.ToImmutableDictionary(t=>t.Id),doc.Battles.ToImmutableDictionary(t=>t.Trainer));
        Route11TrainerPartyFactory.ValidateCatalog(catalog);return catalog;
    }
    private static string SafeFile(string root,string relative)
    {
        if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or "..")) throw new InvalidDataException("Unsafe Route11 trainer path.");
        var f=new FileInfo(Path.GetFullPath(Path.Combine(root,relative)));
        if(f.LinkTarget is not null) throw new InvalidDataException("Route11 trainer symlink rejected.");
        for(var d=f.Directory;d is not null;d=d.Parent) if(d.LinkTarget is not null) throw new InvalidDataException("Route11 trainer parent symlink rejected.");
        return f.FullName;
    }
}

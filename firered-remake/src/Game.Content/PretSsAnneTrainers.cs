using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Trainers;
namespace Game.Content;

public sealed record SsAnneTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles);

/// <summary>Hash-locked source tables and initial battle scripts, independent from all earlier catalogs.</summary>
public static class PretSsAnneTrainers
{
    public static SsAnneTrainerCatalog Load(string root)
    {
        byte[] Read(string path)
        {
            return File.ReadAllBytes(SafeFile(root, path));
        }
        var doc=ContentJson.ParseStrict<SsAnneTrainerDocument>(Encoding.UTF8.GetString(Read("content/generated/data/trainer-battles/ssanne.json")),SharedTrainerCatalog.Options(root));
                if(doc.SchemaVersion!=1) throw new InvalidDataException("SsAnne trainer document identity differs.");
        var catalog=new SsAnneTrainerCatalog(doc.Trainers.ToImmutableDictionary(t=>t.Id),doc.Battles.ToImmutableDictionary(t=>t.Trainer));
        SsAnneTrainerPartyFactory.ValidateCatalog(catalog);return catalog;
    }
    private static string SafeFile(string root,string relative)
    {
        if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or "..")) throw new InvalidDataException("Unsafe SsAnne trainer path.");
        var f=new FileInfo(Path.GetFullPath(Path.Combine(root,relative)));
        if(f.LinkTarget is not null) throw new InvalidDataException("SsAnne trainer symlink rejected.");
        for(var d=f.Directory;d is not null;d=d.Parent) if(d.LinkTarget is not null) throw new InvalidDataException("SsAnne trainer parent symlink rejected.");
        return f.FullName;
    }
}

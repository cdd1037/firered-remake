using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Trainers;
namespace Game.Content;

public sealed record VermilionTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles);

/// <summary>Hash-locked source tables and initial battle scripts, independent from all earlier catalogs.</summary>
public static class PretVermilionTrainers
{
    public static VermilionTrainerCatalog Load(string root)
    {
        byte[] Read(string path)
        {
            return File.ReadAllBytes(SafeFile(root, path));
        }
        var doc=ContentJson.ParseStrict<VermilionTrainerDocument>(Encoding.UTF8.GetString(Read("content/generated/data/trainer-battles/vermilion.json")),SharedTrainerCatalog.Options(root));
                if(doc.SchemaVersion!=1) throw new InvalidDataException("Vermilion trainer document identity differs.");
        var catalog=new VermilionTrainerCatalog(doc.Trainers.ToImmutableDictionary(t=>t.Id),doc.Battles.ToImmutableDictionary(t=>t.Trainer));
        VermilionTrainerPartyFactory.ValidateCatalog(catalog);return catalog;
    }
    private static string SafeFile(string root,string relative)
    {
        if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or "..")) throw new InvalidDataException("Unsafe Vermilion trainer path.");
        var f=new FileInfo(Path.GetFullPath(Path.Combine(root,relative)));
        if(f.LinkTarget is not null) throw new InvalidDataException("Vermilion trainer symlink rejected.");
        for(var d=f.Directory;d is not null;d=d.Parent) if(d.LinkTarget is not null) throw new InvalidDataException("Vermilion trainer parent symlink rejected.");
        return f.FullName;
    }
}

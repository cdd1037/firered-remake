using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Trainers;
namespace Game.Content;

public sealed record BillTrainerDocument(int SchemaVersion, string Profile, string SourceCommit, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    JsonElement ResolvedMoves, ImmutableArray<string> Limitations);

/// <summary>Loads trainer tables and their initial battle scripts.</summary>
public static class PretBillTrainers
{
    public static BillTrainerCatalog Load(string root)
    {
        byte[] Read(string path)
        {
            var bytes=File.ReadAllBytes(SafeFile(root,path));
            return bytes;
        }
        var doc=ContentJson.ParseStrict<BillTrainerDocument>(Encoding.UTF8.GetString(Read("content/generated/bill-trainers/trainers.json")),ContentJson.CreateOptions());
        if(doc.SchemaVersion!=1 || doc.CompleteSourceRoster) throw new InvalidDataException("Bill trainer document identity differs.");
        var catalog=new BillTrainerCatalog(doc.SourceCommit,doc.Profile,false,doc.Trainers.ToImmutableDictionary(t=>t.Id),doc.Battles.ToImmutableDictionary(t=>t.Trainer));
        BillTrainerPartyFactory.ValidateCatalog(catalog);return catalog;
    }
    private static string SafeFile(string root,string relative)
    {
        if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or "..")) throw new InvalidDataException("Unsafe Bill trainer path.");
        var f=new FileInfo(Path.GetFullPath(Path.Combine(root,relative)));
        if(f.LinkTarget is not null) throw new InvalidDataException("Bill trainer symlink rejected.");
        for(var d=f.Directory;d is not null;d=d.Parent) if(d.LinkTarget is not null) throw new InvalidDataException("Bill trainer parent symlink rejected.");
        return f.FullName;
    }
}

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Trainers;
namespace Game.Content;

public sealed record BillTrainerDocument(int SchemaVersion, string Profile, string SourceCommit, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    JsonElement ResolvedMoves, ImmutableArray<string> Limitations);

/// <summary>Hash-locked source tables and initial battle scripts, independent from all earlier catalogs.</summary>
public static class PretBillTrainers
{
    public const string LockSha256 = "f36cb1a8cdc868b4d93e11afd299096df915172c13eea2e9b68c83246e21dc43";
    public const string GeneratedSha256 = "d27736c0b0aca7f2519cf61af64332615fdda3339d8b438ecf5499026c42767f";
    public static BillTrainerCatalog Load(string root)
    {
        byte[] Read(string path,string hash,int? length=null)
        {
            var bytes=PretRuntimeMapValidation.ReadBounded(SafeFile(root,path),length??2*1024*1024);
            if(Hash(bytes)!=hash || length is {} n && bytes.Length!=n) throw new InvalidDataException("Bill trainer pinned bytes differ: "+path);
            return bytes;
        }
        using var sourceLock=JsonDocument.Parse(Read("scripts/bill-trainers.lock.json",LockSha256));
        var s=sourceLock.RootElement;
        if(s.GetProperty("commit").GetString()!=BillTrainerPartyFactory.SourceCommit || s.GetProperty("profile").GetString()!=BillTrainerPartyFactory.RulesVersion ||
            s.GetProperty("repository").GetString()!="pret/pokefirered" || s.GetProperty("files").GetArrayLength()!=15) throw new InvalidDataException("Bill trainer source identity differs.");
        var files=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var f in s.GetProperty("files").EnumerateArray())
        {
            string p=f.GetProperty("path").GetString()!, hash=f.GetProperty("sha256").GetString()!;
            var b=Read("content/raw/bill-trainers/"+p,hash,f.GetProperty("byteLength").GetInt32());
            if(!files.TryAdd(p,hash) || Convert.ToHexStringLower(SHA1.HashData([..Encoding.ASCII.GetBytes($"blob {b.Length}\0"),..b]))!=f.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("Bill trainer Git blob differs.");
        }
        var doc=ContentJson.ParseStrict<BillTrainerDocument>(Encoding.UTF8.GetString(Read("content/generated/bill-trainers/trainers.json",GeneratedSha256)),ContentJson.CreateOptions());
        void Source(TrainerSourceRef r) { if(!files.TryGetValue(r.Path,out var h)||h!=r.Sha256||r.StartLine<=0||r.EndLine<r.StartLine) throw new InvalidDataException("Bill trainer source reference differs."); }
        if(doc.SchemaVersion!=1 || doc.Profile!=BillTrainerPartyFactory.RulesVersion || doc.SourceCommit!=BillTrainerPartyFactory.SourceCommit || doc.CompleteSourceRoster) throw new InvalidDataException("Bill trainer document identity differs.");
        foreach(var t in doc.Trainers) { Source(t.Source);Source(t.MoneySource);foreach(var p in t.Party){Source(p.Source);Source(p.SpeciesNameSource);} }
        foreach(var b in doc.Battles) Source(b.Source);
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
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}

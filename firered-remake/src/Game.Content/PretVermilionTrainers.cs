using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Trainers;
namespace Game.Content;

public sealed record VermilionTrainerDocument(int SchemaVersion, string Profile, string SourceCommit, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    JsonElement ResolvedMoves, ImmutableArray<string> Limitations);

/// <summary>Hash-locked source tables and initial battle scripts, independent from all earlier catalogs.</summary>
public static class PretVermilionTrainers
{
    public const string LockSha256 = "1588bc5ab18923fff39d2d5b3ef0e1e4501d0c7478603e8e8b635ce05415ecf5";
    public const string GeneratedSha256 = "f5b8da584a8a8ea4b4ad911037c3d4f3d18a16bf2bb8222e268507f072ee248b";
    public static VermilionTrainerCatalog Load(string root)
    {
        byte[] Read(string path,string hash,int? length=null)
        {
            var bytes=PretRuntimeMapValidation.ReadBounded(SafeFile(root,path),length??2*1024*1024);
            if(Hash(bytes)!=hash || length is {} n && bytes.Length!=n) throw new InvalidDataException("Vermilion trainer pinned bytes differ: "+path);
            return bytes;
        }
        using var sourceLock=JsonDocument.Parse(Read("scripts/vermilion-trainers.lock.json",LockSha256));
        var s=sourceLock.RootElement;
        if(s.GetProperty("commit").GetString()!=VermilionTrainerPartyFactory.SourceCommit || s.GetProperty("profile").GetString()!=VermilionTrainerPartyFactory.RulesVersion ||
            s.GetProperty("repository").GetString()!="pret/pokefirered" || s.GetProperty("files").GetArrayLength()!=14) throw new InvalidDataException("Vermilion trainer source identity differs.");
        var files=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var f in s.GetProperty("files").EnumerateArray())
        {
            string p=f.GetProperty("path").GetString()!, hash=f.GetProperty("sha256").GetString()!;
            var b=Read("content/raw/vermilion-trainers/"+p,hash,f.GetProperty("byteLength").GetInt32());
            if(!files.TryAdd(p,hash) || Convert.ToHexStringLower(SHA1.HashData([..Encoding.ASCII.GetBytes($"blob {b.Length}\0"),..b]))!=f.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("Vermilion trainer Git blob differs.");
        }
        var doc=ContentJson.ParseStrict<VermilionTrainerDocument>(Encoding.UTF8.GetString(Read("content/generated/vermilion-trainers/trainers.json",GeneratedSha256)),ContentJson.CreateOptions());
        void Source(TrainerSourceRef r) { if(!files.TryGetValue(r.Path,out var h)||h!=r.Sha256||r.StartLine<=0||r.EndLine<r.StartLine) throw new InvalidDataException("Vermilion trainer source reference differs."); }
        if(doc.SchemaVersion!=1 || doc.Profile!=VermilionTrainerPartyFactory.RulesVersion || doc.SourceCommit!=VermilionTrainerPartyFactory.SourceCommit || doc.CompleteSourceRoster) throw new InvalidDataException("Vermilion trainer document identity differs.");
        foreach(var t in doc.Trainers) { Source(t.Source);Source(t.MoneySource);foreach(var p in t.Party){Source(p.Source);Source(p.SpeciesNameSource);} }
        foreach(var b in doc.Battles) Source(b.Source);
        var catalog=new VermilionTrainerCatalog(doc.SourceCommit,doc.Profile,false,doc.Trainers.ToImmutableDictionary(t=>t.Id),doc.Battles.ToImmutableDictionary(t=>t.Trainer));
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
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}

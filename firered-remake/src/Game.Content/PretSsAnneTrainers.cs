using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Trainers;
namespace Game.Content;

public sealed record SsAnneTrainerDocument(int SchemaVersion, string Profile, string SourceCommit, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    JsonElement ResolvedMoves, ImmutableArray<string> Limitations);

/// <summary>Hash-locked source tables and initial battle scripts, independent from all earlier catalogs.</summary>
public static class PretSsAnneTrainers
{
    public const string LockSha256 = "5d6badccd997069d636e848286ba09e1b9196a3b33a91943486e42653140fc44";
    public const string GeneratedSha256 = "e3d103a2010cc618866636f8cdf6431f66b7aef180695fbcb7bac2ec24c5fccf";
    public static SsAnneTrainerCatalog Load(string root)
    {
        byte[] Read(string path,string hash,int? length=null)
        {
            var bytes=PretRuntimeMapValidation.ReadBounded(SafeFile(root,path),length??2*1024*1024);
            if(Hash(bytes)!=hash || length is {} n && bytes.Length!=n) throw new InvalidDataException("SsAnne trainer pinned bytes differ: "+path);
            return bytes;
        }
        using var sourceLock=JsonDocument.Parse(Read("scripts/ssanne-trainers.lock.json",LockSha256));
        var s=sourceLock.RootElement;
        if(s.GetProperty("commit").GetString()!=SsAnneTrainerPartyFactory.SourceCommit || s.GetProperty("profile").GetString()!=SsAnneTrainerPartyFactory.RulesVersion ||
            s.GetProperty("repository").GetString()!="pret/pokefirered" || s.GetProperty("tree").GetString()!="7515cd4067ea55f8cdbc73624478afd2736c58b9" ||
            s.GetProperty("gameEdition").GetString()!="FIRERED" || s.GetProperty("romRevision").GetInt32()!=0 || s.GetProperty("schemaVersion").GetInt32()!=1 || s.GetProperty("files").GetArrayLength()!=28) throw new InvalidDataException("SsAnne trainer source identity differs.");
        var files=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var f in s.GetProperty("files").EnumerateArray())
        {
            string p=f.GetProperty("path").GetString()!, hash=f.GetProperty("sha256").GetString()!;
            var b=Read("content/raw/ssanne-trainers/"+p,hash,f.GetProperty("byteLength").GetInt32());
            if(!files.TryAdd(p,hash) || Convert.ToHexStringLower(SHA1.HashData([..Encoding.ASCII.GetBytes($"blob {b.Length}\0"),..b]))!=f.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("SsAnne trainer Git blob differs.");
        }
        var doc=ContentJson.ParseStrict<SsAnneTrainerDocument>(Encoding.UTF8.GetString(Read("content/generated/ssanne-trainers/trainers.json",GeneratedSha256)),ContentJson.CreateOptions());
        void Source(TrainerSourceRef r) { if(!files.TryGetValue(r.Path,out var h)||h!=r.Sha256||r.StartLine<=0||r.EndLine<r.StartLine) throw new InvalidDataException("SsAnne trainer source reference differs."); }
        if(doc.SchemaVersion!=1 || doc.Profile!=SsAnneTrainerPartyFactory.RulesVersion || doc.SourceCommit!=SsAnneTrainerPartyFactory.SourceCommit || doc.CompleteSourceRoster) throw new InvalidDataException("SsAnne trainer document identity differs.");
        foreach(var t in doc.Trainers) { Source(t.Source);Source(t.MoneySource);foreach(var p in t.Party){Source(p.Source);Source(p.SpeciesNameSource);} }
        foreach(var b in doc.Battles) Source(b.Source);
        var catalog=new SsAnneTrainerCatalog(doc.SourceCommit,doc.Profile,false,doc.Trainers.ToImmutableDictionary(t=>t.Id),doc.Battles.ToImmutableDictionary(t=>t.Trainer));
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
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}

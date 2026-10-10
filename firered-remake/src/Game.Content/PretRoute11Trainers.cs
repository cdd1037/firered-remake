using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Trainers;
namespace Game.Content;

public sealed record Route11TrainerDocument(int SchemaVersion, string Profile, string SourceCommit, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    JsonElement ResolvedMoves, ImmutableArray<string> Limitations);

/// <summary>Hash-locked source tables and initial battle scripts, independent from all earlier catalogs.</summary>
public static class PretRoute11Trainers
{
    public const string LockSha256 = "3f02c4d4ac368192ce812479f71c7a0defd67cc268a2abc9a03a259ec08dc49f";
    public const string GeneratedSha256 = "a13a17bd6237c2d99d5ff99471e88447651d12777d6d110f09597d427469b58d";
    public static Route11TrainerCatalog Load(string root)
    {
        byte[] Read(string path,string hash,int? length=null)
        {
            var bytes=PretRuntimeMapValidation.ReadBounded(SafeFile(root,path),length??2*1024*1024);
            if(Hash(bytes)!=hash || length is {} n && bytes.Length!=n) throw new InvalidDataException("Route11 trainer pinned bytes differ: "+path);
            return bytes;
        }
        using var sourceLock=JsonDocument.Parse(Read("scripts/route11-trainers.lock.json",LockSha256));
        var s=sourceLock.RootElement;
        if(s.GetProperty("commit").GetString()!=Route11TrainerPartyFactory.SourceCommit || s.GetProperty("profile").GetString()!=Route11TrainerPartyFactory.RulesVersion ||
            s.GetProperty("repository").GetString()!="pret/pokefirered" || s.GetProperty("tree").GetString()!="7515cd4067ea55f8cdbc73624478afd2736c58b9" ||
            s.GetProperty("gameEdition").GetString()!="FIRERED" || s.GetProperty("romRevision").GetInt32()!=0 || s.GetProperty("schemaVersion").GetInt32()!=1 || s.GetProperty("files").GetArrayLength()!=21) throw new InvalidDataException("Route11 trainer source identity differs.");
        var files=new Dictionary<string,string>(StringComparer.Ordinal);
        var lineCounts=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var f in s.GetProperty("files").EnumerateArray())
        {
            string p=f.GetProperty("path").GetString()!, hash=f.GetProperty("sha256").GetString()!;
            var b=Read("content/raw/route11-trainers/"+p,hash,f.GetProperty("byteLength").GetInt32());
            if(!files.TryAdd(p,hash) || Convert.ToHexStringLower(SHA1.HashData([..Encoding.ASCII.GetBytes($"blob {b.Length}\0"),..b]))!=f.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("Route11 trainer Git blob differs.");
            lineCounts.Add(p,Encoding.UTF8.GetString(b).Count(c=>c=='\n')+1);
        }
        var doc=ContentJson.ParseStrict<Route11TrainerDocument>(Encoding.UTF8.GetString(Read("content/generated/route11-trainers/trainers.json",GeneratedSha256)),ContentJson.CreateOptions());
        void Source(TrainerSourceRef r) { if(!files.TryGetValue(r.Path,out var h)||h!=r.Sha256||r.StartLine<=0||r.EndLine<r.StartLine||r.EndLine>lineCounts[r.Path]||string.IsNullOrWhiteSpace(r.Symbol)) throw new InvalidDataException("Route11 trainer source reference differs."); }
        if(doc.SchemaVersion!=1 || doc.Profile!=Route11TrainerPartyFactory.RulesVersion || doc.SourceCommit!=Route11TrainerPartyFactory.SourceCommit || doc.CompleteSourceRoster) throw new InvalidDataException("Route11 trainer document identity differs.");
        foreach(var t in doc.Trainers) { Source(t.Source);Source(t.MoneySource);foreach(var p in t.Party){Source(p.Source);Source(p.SpeciesNameSource);} }
        foreach(var b in doc.Battles) Source(b.Source);
        foreach(var trainer in doc.ResolvedMoves.EnumerateArray())
            foreach(var member in trainer.GetProperty("party").EnumerateArray())
                Source(ContentJson.ParseStrict<TrainerSourceRef>(member.GetProperty("learnsetSource").GetRawText(),ContentJson.CreateOptions()));
        var catalog=new Route11TrainerCatalog(doc.SourceCommit,doc.Profile,false,doc.Trainers.ToImmutableDictionary(t=>t.Id),doc.Battles.ToImmutableDictionary(t=>t.Trainer));
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
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}

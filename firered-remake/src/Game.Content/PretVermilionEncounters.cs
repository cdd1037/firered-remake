using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Ids;
namespace Game.Content;
public sealed record VermilionEncounterDocument(int SchemaVersion,string Profile,string SourceCommit,
    ImmutableArray<EarlyLandEncounterTable> Tables,int HeldItemDrawBound,string MeowthRareItem,int MeowthRareMinimumRoll,
    ImmutableArray<string> Limitations);
/// <summary>Fixed source inputs and exact Route5/6 land tables; no water or fishing admission.</summary>
public sealed class PretVermilionEncounters
{
    public const string Profile="firered-vermilion-encounters-v1";
    public const string LockSha256="059ffca76a2fe880b362ee0babe44d342b78a44624636e5251f58dabf01f1428";
    public const string GeneratedSha256="d80ed8568b1f975b1ce168bd08a32e347fb9d9efaecc0f9b32cab84b090d5177";
    public ImmutableDictionary<MapId,EarlyLandEncounterTable> Tables { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    private PretVermilionEncounters(ImmutableDictionary<MapId,EarlyLandEncounterTable> tables,ImmutableArray<string> hashes)
    { Tables=tables;AdditionalSourceHashes=hashes; }
    public static PretVermilionEncounters Load(string root)
    {
        byte[] Read(string path,string hash,int? length=null)
        {
            var bytes=PretRuntimeMapValidation.ReadBounded(SafeFile(root,path),length??2*1024*1024);
            if(Hash(bytes)!=hash || length is {} n && bytes.Length!=n) throw new InvalidDataException("Vermilion encounter pinned bytes differ: "+path);
            return bytes;
        }
        using var sourceLock=JsonDocument.Parse(Read("scripts/vermilion-encounters.lock.json",LockSha256));
        var s=sourceLock.RootElement;
        if(s.GetProperty("commit").GetString()!=EarlyLandEncounterRules.SourceCommit || s.GetProperty("profile").GetString()!=Profile ||
            s.GetProperty("repository").GetString()!="pret/pokefirered" || s.GetProperty("files").GetArrayLength()!=4)
            throw new InvalidDataException("Vermilion encounter source identity differs.");
        var hashes=ImmutableArray.CreateBuilder<string>();hashes.Add("vermilion-encounters-lock:"+LockSha256);hashes.Add("vermilion-encounters:"+GeneratedSha256);
        foreach(var f in s.GetProperty("files").EnumerateArray())
        {
            string p=f.GetProperty("path").GetString()!, hash=f.GetProperty("sha256").GetString()!;
            var b=Read("content/raw/vermilion-encounters/"+p,hash,f.GetProperty("byteLength").GetInt32());
            if(Convert.ToHexStringLower(SHA1.HashData([..Encoding.ASCII.GetBytes($"blob {b.Length}\0"),..b]))!=f.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("Vermilion encounter Git blob differs.");
            hashes.Add("vermilion-encounters:"+p+":"+hash);
        }
        var d=ContentJson.ParseStrict<VermilionEncounterDocument>(Encoding.UTF8.GetString(Read("content/generated/vermilion-encounters/encounters.json",GeneratedSha256)),ContentJson.CreateOptions());
        if(d.SchemaVersion!=1 || d.Profile!=Profile || d.SourceCommit!=EarlyLandEncounterRules.SourceCommit || d.Tables.Length!=2 ||
            d.HeldItemDrawBound!=100 || d.MeowthRareItem!="ITEM_NUGGET" || d.MeowthRareMinimumRoll!=95 || d.Limitations.IsDefaultOrEmpty)
            throw new InvalidDataException("Vermilion encounter document scope differs.");
        foreach(var t in d.Tables)
        {
            var expected=VermilionLandEncounterTables.All.SingleOrDefault(e=>e.Map==t.Map);
            if(expected is null || t with {Slots=expected.Slots}!=expected || !t.Slots.SequenceEqual(expected.Slots))
                throw new InvalidDataException("Vermilion source slot differs.");
            EarlyLandEncounterRules.ValidateTable(t);
        }
        return new(d.Tables.ToImmutableDictionary(t=>t.Map),hashes.ToImmutable());
    }
    private static string SafeFile(string root,string relative)
    {
        if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or "..")) throw new InvalidDataException("Unsafe Vermilion encounter path.");
        var f=new FileInfo(Path.GetFullPath(Path.Combine(root,relative)));
        if(f.LinkTarget is not null) throw new InvalidDataException("Vermilion encounter symlink rejected.");
        for(var d=f.Directory;d is not null;d=d.Parent) if(d.LinkTarget is not null) throw new InvalidDataException("Vermilion encounter parent symlink rejected.");
        return f.FullName;
    }
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}

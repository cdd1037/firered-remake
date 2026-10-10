using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Ids;
namespace Game.Content;
public sealed record Route11EncounterDocument(int SchemaVersion,string Profile,string SourceCommit,
    ImmutableArray<EarlyLandEncounterTable> Tables,int HeldItemDrawBound,ImmutableArray<Route11HeldItemRow> HeldItems,JsonElement ExcludedTables,
    ImmutableArray<string> Limitations);
public sealed record Route11HeldItemRow(string Species,string Common,string Rare,string SourcePath,string SourceSha256);
/// <summary>Fixed source inputs and exact Route11/cave land tables; no water or fishing admission.</summary>
public sealed class PretRoute11Encounters
{
    public const string Profile="firered-route11-encounters-v1";
    public const string LockSha256="e790434594e6af7ff25bc04deaa7791aa471bd1d6d5dd1a9e7d67c2c9856bb0b";
    public const string GeneratedSha256="65904013ad1fcef8c67d73c3432e2570bd44e61e4b06a66cc065c1ac4af3b2a8";
    public ImmutableDictionary<MapId,EarlyLandEncounterTable> Tables { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public JsonElement ExcludedTables { get; }
    private PretRoute11Encounters(ImmutableDictionary<MapId,EarlyLandEncounterTable> tables,ImmutableArray<string> hashes,JsonElement excluded)
    { Tables=tables;AdditionalSourceHashes=hashes;ExcludedTables=excluded; }
    public static PretRoute11Encounters Load(string root)
    {
        byte[] Read(string path,string hash,int? length=null)
        {
            var bytes=PretRuntimeMapValidation.ReadBounded(SafeFile(root,path),length??2*1024*1024);
            if(Hash(bytes)!=hash || length is {} n && bytes.Length!=n) throw new InvalidDataException("Route11 encounter pinned bytes differ: "+path);
            return bytes;
        }
        using var sourceLock=JsonDocument.Parse(Read("scripts/route11-encounters.lock.json",LockSha256));
        var s=sourceLock.RootElement;
        if(s.GetProperty("commit").GetString()!=EarlyLandEncounterRules.SourceCommit || s.GetProperty("profile").GetString()!=Profile ||
            s.GetProperty("repository").GetString()!="pret/pokefirered" || s.GetProperty("files").GetArrayLength()!=4 || s.GetProperty("schemaVersion").GetInt32()!=1 ||
            s.GetProperty("tree").GetString()!="7515cd4067ea55f8cdbc73624478afd2736c58b9" || s.GetProperty("gameEdition").GetString()!="FIRERED" || s.GetProperty("romRevision").GetInt32()!=0)
            throw new InvalidDataException("Route11 encounter source identity differs.");
        var hashes=ImmutableArray.CreateBuilder<string>();hashes.Add("route11-encounters-lock:"+LockSha256);hashes.Add("route11-encounters:"+GeneratedSha256);
        foreach(var f in s.GetProperty("files").EnumerateArray())
        {
            string p=f.GetProperty("path").GetString()!, hash=f.GetProperty("sha256").GetString()!;
            var b=Read("content/raw/route11-encounters/"+p,hash,f.GetProperty("byteLength").GetInt32());
            if(Convert.ToHexStringLower(SHA1.HashData([..Encoding.ASCII.GetBytes($"blob {b.Length}\0"),..b]))!=f.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("Route11 encounter Git blob differs.");
            hashes.Add("route11-encounters:"+p+":"+hash);
        }
        var d=ContentJson.ParseStrict<Route11EncounterDocument>(Encoding.UTF8.GetString(Read("content/generated/route11-encounters/encounters.json",GeneratedSha256)),ContentJson.CreateOptions());
        if(d.SchemaVersion!=1 || d.Profile!=Profile || d.SourceCommit!=EarlyLandEncounterRules.SourceCommit || d.Tables.Length!=2 ||
            d.HeldItemDrawBound!=100 || d.HeldItems.Length!=5 || d.ExcludedTables.GetArrayLength()!=2 || d.Limitations.IsDefaultOrEmpty)
            throw new InvalidDataException("Route11 encounter document scope differs.");
        if(!d.HeldItems.Select(x=>x.Species).ToHashSet(StringComparer.Ordinal).SetEquals(Route11LandEncounterTables.WildSpecies.Select(x=>x.Value)))
            throw new InvalidDataException("Route11 held-item species differs.");
        foreach(var row in d.HeldItems)
            if(row.Common!="ITEM_NONE" || row.Rare!="ITEM_NONE" || row.SourcePath!="src/data/pokemon/species_info.h" ||
                !s.GetProperty("files").EnumerateArray().Any(f=>f.GetProperty("path").GetString()==row.SourcePath && f.GetProperty("sha256").GetString()==row.SourceSha256))
                throw new InvalidDataException("Route11 held-item source differs.");
        foreach(var excluded in d.ExcludedTables.EnumerateArray())
            if(excluded.GetProperty("enabled").GetBoolean() || excluded.GetProperty("map").GetString()!="map.route-11" ||
                excluded.GetProperty("sourceSymbol").GetString()!="sRoute11_FireRed" || excluded.GetProperty("method").GetString() is not ("water_mons" or "fishing_mons"))
                throw new InvalidDataException("Route11 excluded water/fishing scope differs.");
        foreach(var t in d.Tables)
        {
            var expected=Route11LandEncounterTables.All.SingleOrDefault(e=>e.Map==t.Map);
            if(expected is null || t with {Slots=expected.Slots}!=expected || !t.Slots.SequenceEqual(expected.Slots))
                throw new InvalidDataException("Route11 source slot differs.");
            EarlyLandEncounterRules.ValidateTable(t);
        }
        return new(d.Tables.ToImmutableDictionary(t=>t.Map),hashes.ToImmutable(),d.ExcludedTables.Clone());
    }
    private static string SafeFile(string root,string relative)
    {
        if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or "..")) throw new InvalidDataException("Unsafe Route11 encounter path.");
        var f=new FileInfo(Path.GetFullPath(Path.Combine(root,relative)));
        if(f.LinkTarget is not null) throw new InvalidDataException("Route11 encounter symlink rejected.");
        for(var d=f.Directory;d is not null;d=d.Parent) if(d.LinkTarget is not null) throw new InvalidDataException("Route11 encounter parent symlink rejected.");
        return f.FullName;
    }
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Ids;
namespace Game.Content;
public sealed record RockTunnelEncounterDocument(int SchemaVersion,string Profile,string SourceCommit,
    ImmutableArray<EarlyLandEncounterTable> Tables,int HeldItemDrawBound,ImmutableArray<RockTunnelHeldItemRow> HeldItems,JsonElement ExcludedTables,
    ImmutableArray<string> Limitations);
public sealed record RockTunnelHeldItemRow(string Species,string Common,string Rare,string SourcePath,string SourceSha256);
/// <summary>Fixed source inputs and exact RockTunnel/cave land tables; no water or fishing admission.</summary>
public sealed class PretRockTunnelEncounters
{
    public const string Profile="firered-rocktunnel-encounters-v1";
    public const string LockSha256="7057d3fb69d043b3c665b7864b0d43fa5d190c1b720cbbf46e227f7be9e09a90";
    public const string GeneratedSha256="008413d4f3c3d3f010b604b43c995a20d48de2a37245b657d372cc47fb1f0c08";
    public ImmutableDictionary<MapId,EarlyLandEncounterTable> Tables { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public JsonElement ExcludedTables { get; }
    private PretRockTunnelEncounters(ImmutableDictionary<MapId,EarlyLandEncounterTable> tables,ImmutableArray<string> hashes,JsonElement excluded)
    { Tables=tables;AdditionalSourceHashes=hashes;ExcludedTables=excluded; }
    public static PretRockTunnelEncounters Load(string root)
    {
        byte[] Read(string path,string hash,int? length=null)
        {
            var bytes=PretRuntimeMapValidation.ReadBounded(SafeFile(root,path),length??2*1024*1024);
            if(Hash(bytes)!=hash || length is {} n && bytes.Length!=n) throw new InvalidDataException("RockTunnel encounter pinned bytes differ: "+path);
            return bytes;
        }
        using var sourceLock=JsonDocument.Parse(Read("scripts/rocktunnel-encounters.lock.json",LockSha256));
        var s=sourceLock.RootElement;
        if(s.GetProperty("commit").GetString()!=EarlyLandEncounterRules.SourceCommit || s.GetProperty("profile").GetString()!=Profile ||
            s.GetProperty("repository").GetString()!="pret/pokefirered" || s.GetProperty("files").GetArrayLength()!=4 || s.GetProperty("schemaVersion").GetInt32()!=1 ||
            s.GetProperty("tree").GetString()!="7515cd4067ea55f8cdbc73624478afd2736c58b9" || s.GetProperty("gameEdition").GetString()!="FIRERED" || s.GetProperty("romRevision").GetInt32()!=0)
            throw new InvalidDataException("RockTunnel encounter source identity differs.");
        var hashes=ImmutableArray.CreateBuilder<string>();hashes.Add("rocktunnel-encounters-lock:"+LockSha256);hashes.Add("rocktunnel-encounters:"+GeneratedSha256);
        foreach(var f in s.GetProperty("files").EnumerateArray())
        {
            string p=f.GetProperty("path").GetString()!, hash=f.GetProperty("sha256").GetString()!;
            var b=Read("content/raw/rocktunnel-encounters/"+p,hash,f.GetProperty("byteLength").GetInt32());
            if(Convert.ToHexStringLower(SHA1.HashData([..Encoding.ASCII.GetBytes($"blob {b.Length}\0"),..b]))!=f.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("RockTunnel encounter Git blob differs.");
            hashes.Add("rocktunnel-encounters:"+p+":"+hash);
        }
        var d=ContentJson.ParseStrict<RockTunnelEncounterDocument>(Encoding.UTF8.GetString(Read("content/generated/rocktunnel-encounters/encounters.json",GeneratedSha256)),ContentJson.CreateOptions());
        if(d.SchemaVersion!=1 || d.Profile!=Profile || d.SourceCommit!=EarlyLandEncounterRules.SourceCommit || d.Tables.Length!=4 ||
            d.HeldItemDrawBound!=100 || d.HeldItems.Length!=9 || d.ExcludedTables.GetArrayLength()!=3 || d.Limitations.IsDefaultOrEmpty)
            throw new InvalidDataException("RockTunnel encounter document scope differs.");
        if(!d.HeldItems.Select(x=>x.Species).ToHashSet(StringComparer.Ordinal).SetEquals(RockTunnelLandEncounterTables.WildSpecies.Select(x=>x.Value)))
            throw new InvalidDataException("RockTunnel held-item species differs.");
        foreach(var row in d.HeldItems)
            if(row.Common!="ITEM_NONE" || row.Rare!=(row.Species=="pret:SPECIES_ONIX" ? "ITEM_HARD_STONE" : "ITEM_NONE") || row.SourcePath!="src/data/pokemon/species_info.h" ||
                !s.GetProperty("files").EnumerateArray().Any(f=>f.GetProperty("path").GetString()==row.SourcePath && f.GetProperty("sha256").GetString()==row.SourceSha256))
                throw new InvalidDataException("RockTunnel held-item source differs.");
        var expectedExcluded = new HashSet<(string Map, string Symbol, string Method)>
        {
            ("map.route-10", "sRoute10_FireRed", "water_mons"),
            ("map.route-10", "sRoute10_FireRed", "fishing_mons"),
            ("map.rock-tunnel-b1f", "sRockTunnelB1F_FireRed", "rock_smash_mons")
        };
        foreach(var excluded in d.ExcludedTables.EnumerateArray())
            if(excluded.GetProperty("enabled").GetBoolean() || !expectedExcluded.Remove((excluded.GetProperty("map").GetString()!,
                excluded.GetProperty("sourceSymbol").GetString()!, excluded.GetProperty("method").GetString()!)))
                throw new InvalidDataException("RockTunnel excluded water/fishing/rock-smash scope differs.");
        if(expectedExcluded.Count != 0) throw new InvalidDataException("Missing RockTunnel exclusions.");
        foreach(var t in d.Tables)
        {
            var expected=RockTunnelLandEncounterTables.All.SingleOrDefault(e=>e.Map==t.Map);
            if(expected is null || t with {Slots=expected.Slots}!=expected || !t.Slots.SequenceEqual(expected.Slots))
                throw new InvalidDataException("RockTunnel source slot differs.");
            EarlyLandEncounterRules.ValidateTable(t);
        }
        return new(d.Tables.ToImmutableDictionary(t=>t.Map),hashes.ToImmutable(),d.ExcludedTables.Clone());
    }
    private static string SafeFile(string root,string relative)
    {
        if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or "..")) throw new InvalidDataException("Unsafe RockTunnel encounter path.");
        var f=new FileInfo(Path.GetFullPath(Path.Combine(root,relative)));
        if(f.LinkTarget is not null) throw new InvalidDataException("RockTunnel encounter symlink rejected.");
        for(var d=f.Directory;d is not null;d=d.Parent) if(d.LinkTarget is not null) throw new InvalidDataException("RockTunnel encounter parent symlink rejected.");
        return f.FullName;
    }
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}

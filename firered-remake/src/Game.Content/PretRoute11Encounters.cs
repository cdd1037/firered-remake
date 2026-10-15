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
public sealed record Route11HeldItemRow(string Species, string Common, string Rare, string SourcePath);
/// <summary>Fixed source inputs and exact Route11/cave land tables; no water or fishing admission.</summary>
public sealed class PretRoute11Encounters
{
    public const string Profile="firered-route11-encounters-v1";
    public ImmutableDictionary<MapId,EarlyLandEncounterTable> Tables { get; }
    public JsonElement ExcludedTables { get; }
    private PretRoute11Encounters(ImmutableDictionary<MapId,EarlyLandEncounterTable> tables,JsonElement excluded)
    { Tables=tables;ExcludedTables=excluded; }
    public static PretRoute11Encounters Load(string root)
    {
        byte[] Read(string path)
        {
            return File.ReadAllBytes(SafeFile(root, path));
        }
        using var sourceLock=JsonDocument.Parse(Read("scripts/route11-encounters.lock.json"));
        var s=sourceLock.RootElement;
        if(s.GetProperty("schemaVersion").GetInt32()!=1 || s.GetProperty("gameEdition").GetString()!="FIRERED" || s.GetProperty("romRevision").GetInt32()!=0)
            throw new InvalidDataException("Route11 encounter source identity differs.");
        foreach(var f in s.GetProperty("files").EnumerateArray())
        {
            string p=f.GetProperty("path").GetString()!;
        }
                var d=ContentJson.ParseStrict<Route11EncounterDocument>(Encoding.UTF8.GetString(Read("content/generated/route11-encounters/encounters.json")),ContentJson.CreateOptions());
                if(d.SchemaVersion!=1 || d.Tables.Length!=2 ||
            d.HeldItemDrawBound!=100 || d.HeldItems.Length!=5 || d.ExcludedTables.GetArrayLength()!=2 || d.Limitations.IsDefaultOrEmpty)
            throw new InvalidDataException("Route11 encounter document scope differs.");
        if(!d.HeldItems.Select(x=>x.Species).ToHashSet(StringComparer.Ordinal).SetEquals(Route11LandEncounterTables.WildSpecies.Select(x=>x.Value)))
            throw new InvalidDataException("Route11 held-item species differs.");
        foreach(var row in d.HeldItems)
                    if(row.Common!="ITEM_NONE" || row.Rare!="ITEM_NONE" || row.SourcePath!="src/data/pokemon/species_info.h" )
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
                return new(d.Tables.ToImmutableDictionary(t=>t.Map),d.ExcludedTables.Clone());
    }
    private static string SafeFile(string root,string relative)
    {
        if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or "..")) throw new InvalidDataException("Unsafe Route11 encounter path.");
        var f=new FileInfo(Path.GetFullPath(Path.Combine(root,relative)));
        if(f.LinkTarget is not null) throw new InvalidDataException("Route11 encounter symlink rejected.");
        for(var d=f.Directory;d is not null;d=d.Parent) if(d.LinkTarget is not null) throw new InvalidDataException("Route11 encounter parent symlink rejected.");
        return f.FullName;
    }
}

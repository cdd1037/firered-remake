using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Ids;
namespace Game.Content;
public sealed record RockTunnelEncounterDocument(int SchemaVersion,ImmutableArray<EarlyLandEncounterTable> Tables,int HeldItemDrawBound,ImmutableArray<RockTunnelHeldItemRow> HeldItems,JsonElement ExcludedTables);
public sealed record RockTunnelHeldItemRow(string Species, string Common, string Rare, string SourcePath);
/// <summary>Fixed source inputs and exact RockTunnel/cave land tables; no water or fishing admission.</summary>
public sealed class PretRockTunnelEncounters
{
    public const string Profile="firered-rocktunnel-encounters-v1";
    public ImmutableDictionary<MapId,EarlyLandEncounterTable> Tables { get; }
    public JsonElement ExcludedTables { get; }
    private PretRockTunnelEncounters(ImmutableDictionary<MapId,EarlyLandEncounterTable> tables,JsonElement excluded)
    { Tables=tables;ExcludedTables=excluded; }
    public static PretRockTunnelEncounters Load(string root)
    {
        byte[] Read(string path)
        {
            return File.ReadAllBytes(SafeFile(root, path));
        }
        var d=ContentJson.ParseStrict<RockTunnelEncounterDocument>(Encoding.UTF8.GetString(Read("content/generated/data/encounters/rocktunnel.json")),ContentJson.CreateOptions());
                if(d.SchemaVersion!=1 || d.Tables.Length!=4 ||
            d.HeldItemDrawBound!=100 || d.HeldItems.Length!=9 || d.ExcludedTables.GetArrayLength()!=3)
            throw new InvalidDataException("RockTunnel encounter document scope differs.");
        if(!d.HeldItems.Select(x=>x.Species).ToHashSet(StringComparer.Ordinal).SetEquals(RockTunnelLandEncounterTables.WildSpecies.Select(x=>x.Value)))
            throw new InvalidDataException("RockTunnel held-item species differs.");
        foreach(var row in d.HeldItems)
                    if(row.Common!="ITEM_NONE" || row.Rare!=(row.Species=="pret:SPECIES_ONIX" ? "ITEM_HARD_STONE" : "ITEM_NONE") || row.SourcePath!="src/data/pokemon/species_info.h" )
                throw new InvalidDataException("RockTunnel held-item source differs.");
        var expectedExcluded = new HashSet<(string Map, string Symbol, string Method)>
        {
                    ("map.route-10", "sRoute10_FireRed", "water_mons"),("map.route-10", "sRoute10_FireRed", "fishing_mons"),("map.rock-tunnel-b1f", "sRockTunnelB1F_FireRed", "rock_smash_mons")
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
                return new(d.Tables.ToImmutableDictionary(t=>t.Map),d.ExcludedTables.Clone());
    }
    private static string SafeFile(string root,string relative)
    {
        if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or "..")) throw new InvalidDataException("Unsafe RockTunnel encounter path.");
        var f=new FileInfo(Path.GetFullPath(Path.Combine(root,relative)));
        if(f.LinkTarget is not null) throw new InvalidDataException("RockTunnel encounter symlink rejected.");
        for(var d=f.Directory;d is not null;d=d.Parent) if(d.LinkTarget is not null) throw new InvalidDataException("RockTunnel encounter parent symlink rejected.");
        return f.FullName;
    }
}

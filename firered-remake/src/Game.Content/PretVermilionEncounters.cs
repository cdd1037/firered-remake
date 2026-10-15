using System.Collections.Immutable;
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
    public ImmutableDictionary<MapId,EarlyLandEncounterTable> Tables { get; }
    private PretVermilionEncounters(ImmutableDictionary<MapId,EarlyLandEncounterTable> tables)
    { Tables=tables;}
    public static PretVermilionEncounters Load(string root)
    {
        byte[] Read(string path)
        {
            return File.ReadAllBytes(SafeFile(root, path));
        }
        var d=ContentJson.ParseStrict<VermilionEncounterDocument>(Encoding.UTF8.GetString(Read("content/generated/vermilion-encounters/encounters.json")),ContentJson.CreateOptions());
                if(d.SchemaVersion!=1 || d.Tables.Length!=2 ||
            d.HeldItemDrawBound!=100 || d.MeowthRareItem!="ITEM_NUGGET" || d.MeowthRareMinimumRoll!=95 || d.Limitations.IsDefaultOrEmpty)
            throw new InvalidDataException("Vermilion encounter document scope differs.");
        foreach(var t in d.Tables)
        {
            var expected=VermilionLandEncounterTables.All.SingleOrDefault(e=>e.Map==t.Map);
            if(expected is null || t with {Slots=expected.Slots}!=expected || !t.Slots.SequenceEqual(expected.Slots))
                throw new InvalidDataException("Vermilion source slot differs.");
            EarlyLandEncounterRules.ValidateTable(t);
        }
                return new(d.Tables.ToImmutableDictionary(t=>t.Map));
    }
    private static string SafeFile(string root,string relative)
    {
        if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or "..")) throw new InvalidDataException("Unsafe Vermilion encounter path.");
        var f=new FileInfo(Path.GetFullPath(Path.Combine(root,relative)));
        if(f.LinkTarget is not null) throw new InvalidDataException("Vermilion encounter symlink rejected.");
        for(var d=f.Directory;d is not null;d=d.Parent) if(d.LinkTarget is not null) throw new InvalidDataException("Vermilion encounter parent symlink rejected.");
        return f.FullName;
    }
}

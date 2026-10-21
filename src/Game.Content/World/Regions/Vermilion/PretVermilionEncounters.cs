using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Ids;
namespace Game.Content;
public sealed record VermilionEncounterDocument(int SchemaVersion,ImmutableArray<EarlyLandEncounterTable> Tables,int HeldItemDrawBound,string MeowthRareItem,int MeowthRareMinimumRoll);
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
            return File.ReadAllBytes(GeneratedResourceFile.SafePath(root, path));
        }
        var d=ContentJson.ParseStrict<VermilionEncounterDocument>(Encoding.UTF8.GetString(Read("content/generated/data/encounters/vermilion.json")),ContentJson.CreateOptions());
                if(d.SchemaVersion!=1 || d.Tables.Length!=2 ||
            d.HeldItemDrawBound!=100 || d.MeowthRareItem!="ITEM_NUGGET" || d.MeowthRareMinimumRoll!=95)
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
}

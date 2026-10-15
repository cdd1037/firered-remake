using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Ids;
namespace Game.Content;
public sealed record CeladonApproachEncounterDocument(int SchemaVersion,string Profile,string SourceCommit,ImmutableArray<EarlyLandEncounterTable> Tables,
    int HeldItemDrawBound,ImmutableArray<RockTunnelHeldItemRow> HeldItems,JsonElement ExcludedTables,ImmutableArray<string> Limitations);
public sealed class PretCeladonApproachEncounters
{
    public const string Profile="firered-celadon-approach-encounters-v1";
    public ImmutableDictionary<MapId,EarlyLandEncounterTable> Tables { get; }
    public ImmutableArray<RockTunnelHeldItemRow> HeldItems { get; }
    public JsonElement ExcludedTables { get; }
    private PretCeladonApproachEncounters(CeladonApproachEncounterDocument d)
    {Tables=d.Tables.ToImmutableDictionary(t=>t.Map);HeldItems=d.HeldItems;ExcludedTables=d.ExcludedTables.Clone();}
    public static PretCeladonApproachEncounters Load(string root)
    {
        var bytes=PretCeladonApproachSource.Read(root,"content/generated/celadon-approach-encounters/encounters.json");
        var d=ContentJson.ParseStrict<CeladonApproachEncounterDocument>(Encoding.UTF8.GetString(bytes),ContentJson.CreateOptions());
        PretCeladonApproachSource.Require(d.SchemaVersion==1&&d.Tables.Length==2&&
            d.HeldItemDrawBound==100&&d.HeldItems.Length==5&&d.ExcludedTables.GetArrayLength()==0&&!d.Limitations.IsDefaultOrEmpty,"Celadon encounter scope differs.");
        for(int n=0;n<2;n++)
        {
            var t=d.Tables[n];var name=n==0?"Route8":"Route7";
            PretCeladonApproachSource.Require(t.Map.Value==(n==0?"map.route-8":"map.route-7")&&t.SourceSymbol=="s"+name+"_FireRed"&&t.SourcePath=="src/data/wild_encounters.json"&&t.RawEncounterRate==21&&t.Slots.Length==12,"Celadon land table differs.");
var weights=new[]{20,20,10,10,10,10,5,5,4,4,1,1};
            for(int i=0;i<12;i++)PretCeladonApproachSource.Require(t.Slots[i].Index==i&&t.Slots[i].Weight==weights[i]&&t.Slots[i].MinimumLevel==t.Slots[i].MaximumLevel&&t.Slots[i].MinimumLevel is >=15 and <=22,"Celadon slot differs.");
        }
        foreach(var row in d.HeldItems)
        {
            PretCeladonApproachSource.Require(row.Common==(row.Species=="pret:SPECIES_GROWLITHE"?"ITEM_RAWST_BERRY":"ITEM_NONE")&&row.Rare==(row.Species=="pret:SPECIES_MEOWTH"?"ITEM_NUGGET":"ITEM_NONE"),"Celadon held item differs.");
        }
        return new(d);
    }
}

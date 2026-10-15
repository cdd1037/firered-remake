// Reconstructed recovery source. Not compiled or verified in the recovered environment.
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Items;
using Game.Domain.Ids;
namespace Game.Content;
public sealed record PretCeladonApproachItemsDocument(int SchemaVersion,string Profile,string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs,ImmutableArray<OpeningItemSourceRef> RulesSources,
    ImmutableArray<PretBillSourceItem> Items,ImmutableArray<PretPewterMachineIcon> Icons,
    ImmutableArray<PretMtMoonPickup> Pickups,EarlyShopStock Shop,ImmutableArray<string> Limitations);
public static class PretCeladonApproachItems
{
    public const string Profile="firered-celadon-approach-items-v1";
    private static readonly UTF8Encoding Utf8=new(false,true);
    private static void Require(bool x,string m)=>PretCeladonApproachSource.Require(x,m);
    public static PretCeladonApproachItemsDocument Load(string json)
    {
        var options=ContentJson.CreateOptions();options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase,false));
        var d=ContentJson.ParseStrict<PretCeladonApproachItemsDocument>(json,options);
        Require(d.SchemaVersion==1&&d.Items.Length==7&&d.Icons.Length==7&&d.Pickups.Length==13&&!d.RulesSources.IsDefaultOrEmpty&&!d.Limitations.IsDefaultOrEmpty,"Celadon item scope differs.");
        string[] names=["SUPER_REPEL","RAWST_BERRY","LUM_BERRY","LEPPA_BERRY","WEPEAR_BERRY","PP_UP","ICE_HEAL"];
        for(int n=0;n<7;n++)
        {
            var i=d.Items[n];var icon=d.Icons[n];Require(i.Metadata.Item.Id.Value=="pret:ITEM_"+names[n]&&icon.Item==i.Metadata.Item.Id,"Celadon item order differs.");
PretPewterMachines.ValidateIcon(icon);
        }
        foreach(var p in d.Pickups)
        {
            Require(p.Quantity==1&&!p.Underfoot&&p.Elevation is 0 or 3&&p.Map is "Route8" or "Route7" or "UndergroundPath_EastWestTunnel" or "CeladonCity"&&p.X>=0&&p.Y>=0&&
                (p.Kind=="visible"?p.LocalObjectId>0&&p.Script is not null&&p.ScriptSource is not null:p.Kind=="hidden"&&p.LocalObjectId is null&&p.Script is null&&p.ScriptSource is null),"Celadon pickup identity differs.");
        }
        Require(d.Pickups.Count(p=>p.Kind=="hidden")==12,"Celadon hidden count differs.");        Require(d.Shop.Id=="LavenderTown_Mart_Items"&&d.Shop.Items.Select(i=>i.Value).SequenceEqual(new[]{"GREAT_BALL","SUPER_POTION","REVIVE","ANTIDOTE","PARALYZE_HEAL","BURN_HEAL","ICE_HEAL","ESCAPE_ROPE","SUPER_REPEL"}.Select(i=>"pret:ITEM_"+i)),"Lavender stock differs.");
        return d;
    }
    public static PretCeladonApproachItemsDocument LoadProject(string root)
    {
        _=PretCeladonApproachSource.Load(root,"celadon-approach-items");
        var outputs=PretCeladonApproachSource.Outputs(root,"celadon-approach-items");
        return Load(Utf8.GetString(outputs["celadon-approach-items.json"]));
    }
    public static EarlyShopCatalog LoadShopProject(string root,EarlyShopCatalog rockTunnelShop)
    {
        RockTunnelItemRules.ValidateShop(rockTunnelShop);var d=LoadProject(root);
        var result=rockTunnelShop with{Profile=LavenderItemRules.Profile,Items=rockTunnelShop.Items.AddRange(d.Items.Select(Definition)),Shops=rockTunnelShop.Shops.Add(d.Shop)};
        LavenderItemRules.ValidateShop(result);return result;
    }
    public static EarlyShopItemDefinition? Definition(PretCeladonApproachItemsDocument d,ItemId id)
    {var item=d.Items.SingleOrDefault(i=>i.Metadata.Item.Id==id);return item is null?null:Definition(item);}
    private static EarlyShopItemDefinition Definition(PretBillSourceItem item)
    {
        var result=PretSsAnneItems.Definition(item);
        return result.SourceItemSymbol switch
        {
            "ITEM_SUPER_REPEL"=>result with{UseSupport=EarlyShopUseSupport.DeferredRepel},
            "ITEM_RAWST_BERRY"=>result with{UseSupport=EarlyShopUseSupport.StatusCure,CureStatus=16},
            "ITEM_ICE_HEAL"=>result with{UseSupport=EarlyShopUseSupport.StatusCure,CureStatus=32},
            _=>result
        };
    }
}

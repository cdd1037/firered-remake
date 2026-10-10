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
    public const string LockSha256="8edae0ed3dbd9d44f1ea3a731473bb95976708d373359c493867fb457c3404ac";
    public const string GeneratedSha256="f916c37ffa3ab70b818ef0407d1be7f079a207da2b73818da5f1c665e51cbcdf";
    public const string ManifestSha256="0c1817606303e4d8ed29415107683101f017204552bdde1f58ec384994e82d16";
    public const string RawManifestSha256="0435d70c350fcd7b3ac54130f494059457aa655c3fa57596962a4161910c7c39";
    public const string FullSourceHash="64bcc05dbfe6557c5ebce3bb0fcac8494b978da8bff73b573d5d7d87561ecc03";
    private static readonly UTF8Encoding Utf8=new(false,true);
    private static void Require(bool x,string m)=>PretCeladonApproachSource.Require(x,m);
    public static PretCeladonApproachItemsDocument Load(string json)
    {
        Require(PretCeladonApproachSource.Hash(Utf8.GetBytes(json))==GeneratedSha256,"Celadon item document differs.");
        var options=ContentJson.CreateOptions();options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase,false));
        var d=ContentJson.ParseStrict<PretCeladonApproachItemsDocument>(json,options);
        Require(d.SchemaVersion==1&&d.Profile==Profile&&d.SourceCommit==OpeningInventoryRules.SourceCommit&&d.Inputs.Length==32&&d.Items.Length==7&&d.Icons.Length==7&&d.Pickups.Length==13&&!d.RulesSources.IsDefaultOrEmpty&&!d.Limitations.IsDefaultOrEmpty,"Celadon item scope differs.");
        var inputs=d.Inputs.ToDictionary(x=>x.Path,StringComparer.Ordinal);
        Require(PretCeladonApproachSource.Hash(Utf8.GetBytes(string.Concat(d.Inputs.OrderBy(x=>x.Path,StringComparer.Ordinal).Select(x=>x.Path+"\0"+x.Sha256+"\n"))))==FullSourceHash,"Celadon item source set differs.");
        void Source(OpeningItemSourceRef s)=>Require(s is not null&&!string.IsNullOrWhiteSpace(s.Symbol)&&s.StartLine>0&&s.EndLine>=s.StartLine&&inputs.TryGetValue(s.Path,out var i)&&i.Sha256==s.Sha256,"Celadon item source reference differs.");
        foreach(var s in d.RulesSources)Source(s);
        string[] names=["SUPER_REPEL","RAWST_BERRY","LUM_BERRY","LEPPA_BERRY","WEPEAR_BERRY","PP_UP","ICE_HEAL"];
        for(int n=0;n<7;n++)
        {
            var i=d.Items[n];var icon=d.Icons[n];Require(i.Metadata.Item.Id.Value=="pret:ITEM_"+names[n]&&icon.Item==i.Metadata.Item.Id,"Celadon item order differs.");
            Source(i.Metadata.Item.Source);Source(i.Metadata.DescriptionSource);if(i.EffectSource is {} e)Source(e);
            Source(icon.Source);Source(icon.PaletteSource);Source(icon.BindingSource);_=PretPewterMachines.IconRgba(icon);
        }
        foreach(var p in d.Pickups)
        {
            Source(p.Source);if(p.ScriptSource is {} ss)Source(ss);
            Require(p.Quantity==1&&!p.Underfoot&&p.Elevation is 0 or 3&&p.Map is "Route8" or "Route7" or "UndergroundPath_EastWestTunnel" or "CeladonCity"&&p.X>=0&&p.Y>=0&&
                (p.Kind=="visible"?p.LocalObjectId>0&&p.Script is not null&&p.ScriptSource is not null:p.Kind=="hidden"&&p.LocalObjectId is null&&p.Script is null&&p.ScriptSource is null),"Celadon pickup identity differs.");
        }
        Require(d.Pickups.Count(p=>p.Kind=="hidden")==12,"Celadon hidden count differs.");Source(d.Shop.Source);
        Require(d.Shop.Id=="LavenderTown_Mart_Items"&&d.Shop.Items.Select(i=>i.Value).SequenceEqual(new[]{"GREAT_BALL","SUPER_POTION","REVIVE","ANTIDOTE","PARALYZE_HEAL","BURN_HEAL","ICE_HEAL","ESCAPE_ROPE","SUPER_REPEL"}.Select(i=>"pret:ITEM_"+i)),"Lavender stock differs.");
        return d;
    }
    public static PretCeladonApproachItemsDocument LoadProject(string root)
    {
        _=PretCeladonApproachSource.Load(root,"celadon-approach-items",Profile,LockSha256,32);
        _=PretCeladonApproachSource.Read(root,"content/raw/celadon-approach-items/manifest.json",RawManifestSha256);
        var outputs=PretCeladonApproachSource.Outputs(root,"celadon-approach-items",Profile,ManifestSha256,LockSha256,32);
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

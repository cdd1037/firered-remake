using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Items;
using Game.Domain.Ids;
namespace Game.Content;

public sealed record PretCeladonGymItemsDocument(int SchemaVersion,string Profile,string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs,ImmutableArray<OpeningItemSourceRef> RulesSources,
    ImmutableArray<PretBillSourceItem> Items,ImmutableArray<PretPewterMachineIcon> Icons,ImmutableArray<string> Limitations);

public static class PretCeladonGymItems
{
    public const string Profile=CeladonGymItemRules.Profile;
    public const string LockSha256="6afc793bb163e5da94214c76db2dfd95a754752a6fa51a0be8aada1738660df0";
    public const string GeneratedSha256="9f4c6792f2f539ea0c9769ab385f217495697856e89081953cf1b81547eb2064";
    public const string ManifestSha256="656f0ba94edcb3202cdea90a054ad1c29e287e62152a4a925e22451f5a16b63c";
    public const string RawManifestSha256="189401725785981353044c348fcfbb815bb4495ee79eecc7e274c12060fd14b0";
    public const string FullSourceHash="446fd7a5c9035e0b4f8e2fe576e9895d08545f91537531f6f7ae7cd37f4d9c9a";
    private static readonly UTF8Encoding Utf8=new(false,true);
    private static void Require(bool condition,string message)=>PretCeladonApproachSource.Require(condition,message);
    public static PretCeladonGymItemsDocument Load(string json)
    {
        Require(PretCeladonApproachSource.Hash(Utf8.GetBytes(json))==GeneratedSha256,"Celadon Gym item document differs.");
        var options=ContentJson.CreateOptions();options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase,false));
        var document=ContentJson.ParseStrict<PretCeladonGymItemsDocument>(json,options);
        Require(document.SchemaVersion==1&&document.Profile==Profile&&document.SourceCommit==OpeningInventoryRules.SourceCommit&&
            document.Inputs.Length==16&&document.Items.Length==1&&document.Icons.Length==1&&!document.RulesSources.IsDefaultOrEmpty&&
            !document.Limitations.IsDefaultOrEmpty,"Celadon Gym item scope differs.");
        var inputs=document.Inputs.ToDictionary(i=>i.Path,StringComparer.Ordinal);
        Require(PretCeladonApproachSource.Hash(Utf8.GetBytes(string.Concat(document.Inputs.OrderBy(i=>i.Path,StringComparer.Ordinal)
            .Select(i=>i.Path+"\0"+i.Sha256+"\n"))))==FullSourceHash,"Celadon Gym item source set differs.");
        void Source(OpeningItemSourceRef s)=>Require(s is not null&&!string.IsNullOrWhiteSpace(s.Symbol)&&s.StartLine>0&&s.EndLine>=s.StartLine&&
            inputs.TryGetValue(s.Path,out var input)&&input.Sha256==s.Sha256,"Celadon Gym item reference differs.");
        foreach(var s in document.RulesSources)Source(s);
        var item=document.Items.Single();CeladonGymItemRules.ValidateMachine(item.Metadata);
        Require(item.EffectBytes.IsEmpty&&item.EffectSource is null,"TM19 has unexpected effect bytes.");
        Source(item.Metadata.Item.Source);Source(item.Metadata.DescriptionSource);
        var icon=document.Icons.Single();Require(icon.Item==CeladonGymItemRules.Tm19Id&&icon.PngPath=="item_tm19.png","TM19 icon identity differs.");
        Source(icon.Source);Source(icon.PaletteSource);Source(icon.BindingSource);_=PretPewterMachines.IconRgba(icon);
        return document;
    }
    public static PretCeladonGymItemsDocument LoadProject(string root)
    {
        var source=PretCeladonApproachSource.Load(root,"celadon-gym-items",Profile,LockSha256,16);
        _=PretCeladonApproachSource.Read(root,"content/raw/celadon-gym-items/manifest.json",RawManifestSha256);
        var outputs=PretCeladonApproachSource.Outputs(root,"celadon-gym-items",Profile,ManifestSha256,LockSha256,16);
        Require(outputs.Count==3,"Celadon Gym item output set differs.");
        var document=Load(Utf8.GetString(outputs["celadon-gym-items.json"]));
        Require(document.Inputs.Select(f=>(f.Path,f.Sha256,f.GitBlobSha1,f.ByteLength)).SequenceEqual(source.Files.Select(f=>(f.Path,f.Sha256,f.GitBlobSha1,f.ByteLength))),"Celadon Gym item source inventory differs.");
        var icon=document.Icons.Single();Require(PretCeladonApproachSource.Hash(outputs[icon.PngPath])==icon.PngSha256,"TM19 icon texture differs.");
        return document;
    }
    public static EarlyShopCatalog LoadShopProject(string root,EarlyShopCatalog inherited)
    {
        LavenderItemRules.ValidateShop(inherited);var document=LoadProject(root);
        var result=inherited with{Profile=Profile,Items=inherited.Items.Add(Definition(document,CeladonGymItemRules.Tm19Id)!)};
        CeladonGymItemRules.ValidateShop(result);return result;
    }
    public static EarlyShopItemDefinition? Definition(PretCeladonGymItemsDocument document,ItemId id)
    {
        var item=document.Items.SingleOrDefault(i=>i.Metadata.Item.Id==id);
        return item is null?null:PretSsAnneItems.Definition(item);
    }
}

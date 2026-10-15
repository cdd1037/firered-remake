using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Items;
using Game.Domain.Ids;
namespace Game.Content;

public sealed record PretCeladonGymItemsDocument(int SchemaVersion,string Profile,string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs,ImmutableArray<PretBillSourceItem> Items,ImmutableArray<PretPewterMachineIcon> Icons,ImmutableArray<string> Limitations);

public static class PretCeladonGymItems
{
    public const string Profile=CeladonGymItemRules.Profile;
    private static readonly UTF8Encoding Utf8=new(false,true);
    private static void Require(bool condition,string message)=>PretCeladonApproachSource.Require(condition,message);
    public static PretCeladonGymItemsDocument Load(string json, SharedMachineCatalog machines)
    {
        var options=machines.CreateJsonOptions();options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase,false));
        var document=ContentJson.ParseStrict<PretCeladonGymItemsDocument>(json,options);
        Require(document.SchemaVersion==1&&document.Items.Length==1&&document.Icons.Length==1&&!document.Limitations.IsDefaultOrEmpty,"Celadon Gym item scope differs.");
        var item=document.Items.Single();CeladonGymItemRules.ValidateMachine(item.Metadata);
        Require(item.EffectBytes.IsEmpty,"TM19 has unexpected effect bytes.");
        var icon=document.Icons.Single();Require(icon.Item==CeladonGymItemRules.Tm19Id&&icon.Region is not null,"TM19 icon identity differs.");
PretPewterMachines.ValidateIcon(icon);
        return document;
    }
    public static PretCeladonGymItemsDocument LoadProject(string root, SharedMachineCatalog? shared = null)
    {
        var outputs=PretCeladonApproachSource.Outputs(root,"celadon-gym-items");
        var document=Load(Utf8.GetString(outputs["celadon-gym-items.json"]), (shared ?? SharedMachineCatalog.LoadProject(root)));
return document;
    }

    public static EarlyShopItemDefinition? Definition(PretCeladonGymItemsDocument document,ItemId id)
    {
        var item=document.Items.SingleOrDefault(i=>i.Metadata.Item.Id==id);
        return item is null?null:PretSsAnneItems.Definition(item);
    }
}

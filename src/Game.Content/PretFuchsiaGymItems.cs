using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
namespace Game.Content;

public sealed record PretFuchsiaGymItemsDocument(int SchemaVersion,ImmutableArray<PretBillSourceItem> Items,ImmutableArray<ItemIcon> Icons);

/// <summary>Authenticated TM06 metadata for future composition. No reward or teaching handler is registered.</summary>
public static class PretFuchsiaGymItems
{
    public const string Profile="firered-fuchsia-gym-items-v1";
    public static readonly ItemId Tm06Id=new("pret:ITEM_TM06");
    public static PretFuchsiaGymItemsDocument LoadProject(string root, SharedMachineCatalog? shared = null)
    {
        var outputs=PretCeladonApproachSource.Outputs(root,"data/items/fuchsia-gym");
        var document=ContentJson.ParseStrict<PretFuchsiaGymItemsDocument>(Encoding.UTF8.GetString(outputs["fuchsia-gym-items.json"]),(shared ?? SharedMachineCatalog.LoadProject(root)).CreateJsonOptions());
        Need(document.SchemaVersion==1&&
            document.Items.Length==1&&document.Icons.Length==1,"Fuchsia Gym item scope differs.");
        var item=document.Items.Single();var d=item.Metadata.Item;
        Need(!string.IsNullOrWhiteSpace(item.Metadata.Description),"TM06 display text is missing.");
        Need(d.Id==Tm06Id&&d.SourceItemSymbol=="ITEM_TM06"&&d.SourceItemId==294&&d.Price==3000&&
            d.Importance==0&&d.Registrability==0&&d.Pocket=="POCKET_TM_CASE"&&d.Type=="ITEM_TYPE_PARTY_MENU"&&d.HoldEffect=="HOLD_EFFECT_NONE"&&
            d.HoldEffectParam==0&&d.FieldUseFunction=="NULL"&&d.BattleUsage==0&&d.BattleUseFunction=="NULL"&&
            d.SecondaryId==0&&d.CanToss&&d.CanSell&&d.CanGive&&item.EffectBytes.IsEmpty,"TM06 source item differs.");
        var icon=document.Icons.Single();
        Need(icon.Item==Tm06Id&&icon.Width==24&&icon.Height==24&&icon.Region is not null,"TM06 source icon differs.");
ItemIconAssets.Validate(icon);
        return document;
    }
    public static EarlyShopItemDefinition Definition(PretFuchsiaGymItemsDocument document)=>PretSsAnneItems.Definition(document.Items.Single());
    private static void Need(bool condition,string message)=>PretCeladonApproachSource.Require(condition,message);
}

using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
namespace Game.Content;

public sealed record PretFuchsiaGymItemsDocument(int SchemaVersion,string Profile,string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs,ImmutableArray<PretBillSourceItem> Items,ImmutableArray<PretPewterMachineIcon> Icons,ImmutableArray<string> Limitations);

/// <summary>Authenticated TM06 metadata for future composition. No reward or teaching handler is registered.</summary>
public static class PretFuchsiaGymItems
{
    public const string Profile="firered-fuchsia-gym-items-v1";
    public static readonly ItemId Tm06Id=new("pret:ITEM_TM06");
    public static PretFuchsiaGymItemsDocument LoadProject(string root)
    {
        var outputs=PretCeladonApproachSource.Outputs(root,"fuchsia-gym-items");
        var document=ContentJson.ParseStrict<PretFuchsiaGymItemsDocument>(Encoding.UTF8.GetString(outputs["fuchsia-gym-items.json"]),SharedMachineCatalog.Options(root));
        Need(document.SchemaVersion==1&&
            document.Items.Length==1&&document.Icons.Length==1&&!document.Limitations.IsDefaultOrEmpty,"Fuchsia Gym item scope differs.");
        var item=document.Items.Single();var d=item.Metadata.Item;
        Need(d.Id==Tm06Id&&d.SourceItemSymbol=="ITEM_TM06"&&d.SourceItemId==294&&d.Name=="TM06"&&d.Price==3000&&
            d.Importance==0&&d.Registrability==0&&d.Pocket=="POCKET_TM_CASE"&&d.Type=="ITEM_TYPE_PARTY_MENU"&&d.HoldEffect=="HOLD_EFFECT_NONE"&&
            d.HoldEffectParam==0&&d.FieldUseFunction=="NULL"&&d.BattleUsage==0&&d.BattleUseFunction=="NULL"&&
            d.SecondaryId==0&&d.CanToss&&d.CanSell&&d.CanGive&&item.EffectBytes.IsEmpty&&item.EffectSource is null,"TM06 source item differs.");
        var icon=document.Icons.Single();
        Need(icon.Item==Tm06Id&&icon.Width==24&&icon.Height==24&&icon.Region is not null,"TM06 source icon differs.");
PretPewterMachines.ValidateIcon(icon);
        return document;
    }
    public static EarlyShopItemDefinition Definition(PretFuchsiaGymItemsDocument document)=>PretSsAnneItems.Definition(document.Items.Single());
    private static void Need(bool condition,string message)=>PretCeladonApproachSource.Require(condition,message);
}

/// <summary>One TM06 entry and all 107 source compatibility rows. The caller owns campaign composition.</summary>
public static class PretFuchsiaGymMachines
{
    public const string Profile="firered-fuchsia-gym-machines-v1";
    public static PretRockTunnelMachinesDocument LoadProject(string root)
    {
        var items=PretFuchsiaGymItems.LoadProject(root);
        var bytes=PretCeladonApproachSource.Read(root,"content/generated/fuchsia-gym-items/fuchsia-gym-machines.json");
        var document=ContentJson.ParseStrict<PretRockTunnelMachinesDocument>(Encoding.UTF8.GetString(bytes),SharedMachineCatalog.Options(root));
        void Need(bool value,string message)=>PretCeladonApproachSource.Require(value,message);
        Need(document.SchemaVersion==1&&document.Inputs.SequenceEqual(items.Inputs)&&
            document.Machines.Length==1&&document.Icons.Length==1&&!document.Limitations.IsDefaultOrEmpty,"Fuchsia Gym machine scope differs.");
        Need(JsonSerializer.Serialize(document.Icons)==JsonSerializer.Serialize(items.Icons),"Fuchsia Gym machine icon provenance differs.");
        var machine=document.Machines.Single();
        Need(machine.Machine==items.Items.Single().Metadata&&machine.Move.Value=="pret:MOVE_TOXIC"&&machine.SourceMoveId==92&&machine.MovePp==10&&
            machine.Compatibility.Length==107&&machine.Compatibility.Select(c=>c.Species).Distinct().Count()==107&&
            machine.Compatibility.Select(c=>c.SourceSpeciesId).SequenceEqual(machine.Compatibility.Select(c=>c.SourceSpeciesId).Order()),"TM06 mapping/compatibility differs.");
        return document;
    }
}
